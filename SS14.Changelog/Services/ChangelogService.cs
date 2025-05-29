using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SS14.Changelog.Configuration;
using YamlDotNet.Core;
using YamlDotNet.RepresentationModel;

namespace SS14.Changelog.Services
{
    public sealed class ChangelogService : BackgroundService
    {
        private readonly IOptions<ChangelogConfig> _cfg;
        private readonly ILogger<ChangelogService> _log;

        private readonly Channel<MsgQueueBase> _commChannel = Channel.CreateUnbounded<MsgQueueBase>(
            new UnboundedChannelOptions
            {
                SingleReader = true
            });

        // Main loop state, do not touch outside main loop thanks.
        private Task _updateQueueTask = Task.CompletedTask;
        private bool _updateQueued;

        public ChangelogService(IOptions<ChangelogConfig> cfg, ILogger<ChangelogService> log)
        {
            _cfg = cfg;
            _log = log;
        }

        public void PushPRChangelog(ChangelogData data)
        {
            _commChannel.Writer.TryWrite(new MsgWritePRChangelog(data));
            _commChannel.Writer.TryWrite(new MsgQueueUpdate());
        }

        public void QueueUpdate()
        {
            _commChannel.Writer.TryWrite(new MsgQueueUpdate());
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _updateQueueTask = Task.Delay(Timeout.Infinite, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                var readTask = _commChannel.Reader.WaitToReadAsync(stoppingToken);
                await Task.WhenAny(_updateQueueTask, readTask.AsTask());

                ProcessMessages(stoppingToken);

                await CheckRunChangelogUpdate(stoppingToken);
            }
        }

        private async Task CheckRunChangelogUpdate(CancellationToken stoppingToken)
        {
            if (_updateQueueTask.IsCompleted && _updateQueued)
            {
                try
                {
                    await RunChangelogUpdate();
                }
                catch (Exception e)
                {
                    _log.LogError(e, "Exception while running changelog update!");
                }

                _updateQueued = false;
                _updateQueueTask = Task.Delay(Timeout.Infinite, stoppingToken);
            }
        }

        private void ProcessMessages(CancellationToken stoppingToken)
        {
            while (_commChannel.Reader.TryRead(out var msg))
            {
                switch (msg)
                {
                    case MsgWritePRChangelog msgWritePRChangelog:
                        WriteChangelogPart(msgWritePRChangelog.Data);
                        break;

                    case MsgQueueUpdate:
                        _updateQueued = true;
                        if (!stoppingToken.IsCancellationRequested)
                            _updateQueueTask = Task.Delay(_cfg.Value.DelaySeconds * 1000, stoppingToken);
                        break;
                }
            }
        }

        private async Task RunChangelogUpdate()
        {
            var cfg = _cfg.Value;
            _log.LogInformation("Running changelog update!");

            var repo = cfg.ChangelogRepo!;

            await EnsureRepoCloned();

            _log.LogTrace("Ensuring we're on the correct branch...");

            await WaitForSuccessAsync(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = {"checkout", cfg.ChangelogBranchName},
                WorkingDirectory = repo
            });

            _log.LogTrace("Pulling repo...");

            await PullRebase();

            _log.LogTrace("Running changelog script");

            await InvokeChangelogScript(cfg);

            _log.LogTrace("Checking git status...");
            var status = await WaitForSuccessAsync(new ProcessStartInfo
            {
                FileName = "git", ArgumentList = {"status", "--porcelain"},
                WorkingDirectory = repo
            });

            if (string.IsNullOrWhiteSpace(status))
            {
                _log.LogTrace("No files changed, no changelog commit needed");
                return;
            }

            _log.LogTrace("Committing");
            await WaitForSuccessAsync(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = {"add", "Resources/Changelog/"},
                WorkingDirectory = repo
            });

            await WaitForSuccessAsync(GitCommitCommand(new ProcessStartInfo
            {
                FileName = "git",
                ArgumentList = {"commit", "-m", "Automatic changelog update"},
                WorkingDirectory = repo
            }));

            // If we merge something *while* the changelog is running then the push would fail.
            // So if the push fails we pull --rebase again to try to fix that.

            var first = true;
            var failed = true;
            for (var i = 0; i < 3; i++)
            {
                if (!first)
                {
                    _log.LogInformation("Pulling in the hope that fixes the push...");
                    await PullRebase();
                }

                first = false;

                try
                {
                    _log.LogInformation("Pushing...");
                    await Push();
                    failed = false;
                    break;
                }
                catch (ProcessFailedException)
                {
                    _log.LogWarning("Failed to push!");
                }
            }

            if (failed)
            {
                _log.LogError("Failed to push too much, giving up!");
            }

            async Task PullRebase()
            {
                // GitCommitCommand() is needed because git asks for committer information when a rebase needs to happen.
                // Why? Well quite frankly if git was well-coded I wouldn't have needed 5 attempts
                // to figure out something like GitCommitCommand() that actually works, so...
                await WaitForSuccessAsync(GitCommitCommand(GitNetCommand(new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList = {"pull", "--rebase"},
                    WorkingDirectory = repo
                })), timeoutSeconds: 30);
            }

            async Task Push()
            {
                await WaitForSuccessAsync(GitNetCommand(new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList = {"push", "origin"},
                    WorkingDirectory = repo
                }), timeoutSeconds: 30);
            }

            async Task EnsureRepoCloned()
            {
                _log.LogTrace("Ensuring repo is cloned...");

                var dotGitPath = Path.Join(repo, ".git");
                if (Directory.Exists(dotGitPath))
                {
                    _log.LogTrace("{Path} exists, assuming repo cloned already", dotGitPath);
                    return;
                }

                _log.LogInformation("Repo is not initialized yet, cloning it now.");

                if (cfg.ChangelogRepoRemote is not { } remote)
                    throw new Exception("Unable to clone new repository, ChangelogRepoRemote is not set!");

                await WaitForSuccessAsync(GitNetCommand(new ProcessStartInfo
                {
                    FileName = "git",
                    ArgumentList = {"clone", remote, "."},
                    WorkingDirectory = repo
                }));
            }

            ProcessStartInfo GitCommitCommand(ProcessStartInfo info)
            {
                if (cfg.CommitAuthorName is { } authorName)
                {
                    info.ArgumentList.Insert(0, "-c");
                    info.ArgumentList.Insert(1, "user.name=" + authorName);
                }

                if (cfg.CommitAuthorEmail is { } authorEmail)
                {
                    info.ArgumentList.Insert(0, "-c");
                    info.ArgumentList.Insert(1, "user.email=" + authorEmail);
                }

                return info;
            }
        }

        private ProcessStartInfo GitNetCommand(ProcessStartInfo info)
        {
            if (_cfg.Value.SshKey is { } key)
            {
                info.EnvironmentVariables.Add("GIT_SSH_COMMAND", $"ssh -i \"{key}\"");
            }
            
            // Not sure if necessary but git has been hanging which is very annoying.
            info.EnvironmentVariables.Add("GIT_TERMINAL_PROMPT", "0");

            return info;
        }

        private async Task InvokeChangelogScript(ChangelogConfig cfg)
        {
            var repo = _cfg.Value.ChangelogRepo!;

            _log.LogInformation("Running changelog for MAIN changelog:");
            var startMain = MakeBasicStartInfo(_cfg.Value.ChangelogFilename);
            await WaitForSuccessAsync(startMain);

            foreach (var category in cfg.ExtraCategories)
            {
                _log.LogInformation("Running changelog for {Category} changelog:", category);
                var startCategory = MakeBasicStartInfo(category + ".yml");
                startCategory.ArgumentList.Add("--category");
                startCategory.ArgumentList.Add(category);
                await WaitForSuccessAsync(startCategory);
            }

            return;

            ProcessStartInfo MakeBasicStartInfo(string filename)
            {
                var script = Path.Combine(repo, "Tools", "update_changelog.py");
                var parts = Path.Combine(repo, "Resources", "Changelog", "Parts");
                var changelogFile = Path.Combine(repo, "Resources", "Changelog", filename);

                var procStart = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                    ? new ProcessStartInfo("py") { ArgumentList = {script} }
                    : new ProcessStartInfo(script);

                procStart.ArgumentList.Add(changelogFile);
                procStart.ArgumentList.Add(parts);

                return procStart;
            }
        }

        private async Task<string> WaitForSuccessAsync(ProcessStartInfo info, int? timeoutSeconds=null)
        {
            info.RedirectStandardOutput = true;
            info.RedirectStandardError = true;

            var proc = Process.Start(info);

            if (proc == null)
            {
                throw new ProcessFailedException($"Process to start process!");
            }

            try
            {
                CancellationToken ct = default;
                if (timeoutSeconds != null)
                {
                    var cts = new CancellationTokenSource(timeoutSeconds.Value * 1000);
                    ct = cts.Token;
                }

                await proc.WaitForExitAsync(ct);
            }
            catch (OperationCanceledException)
            {
                _log.LogWarning("Command timed out ({TimeoutSeconds} seconds), killing...", timeoutSeconds);
              
                if (OperatingSystem.IsLinux())
                    proc.Kill(Utility.Signum.SIGTERM);
                else
                    proc.Kill();

                await proc.WaitForExitAsync();
            }
            
            var stdout = await proc.StandardOutput.ReadToEndAsync();
            _log.LogInformation("{Process} exited with code {Code}\nStdout: {StdOut}\nStderr: {StdErr}",
                info.FileName,
                proc.ExitCode,
                stdout,
                await proc.StandardError.ReadToEndAsync());

            if (proc.ExitCode != 0)
            {
                throw new ProcessFailedException($"Process failed with exit code {proc.ExitCode}!");
            }

            return stdout;
        }

        private void WriteChangelogPart(ChangelogData data)
        {
            try
            {
                foreach (var category in data.Categories)
                {
                    var fileName = Path.Combine(
                        _cfg.Value.ChangelogRepo!,
                        "Resources", "Changelog", "Parts", $"pr-{data.Number}-{category.Category}.yml");

                    _log.LogTrace("Writing changelog part {PartFileName}", fileName);

                    var yamlMapping = new YamlMappingNode
                    {
                        {"author", Quoted(data.Author)},
                        {"time", Quoted(data.Time.ToString("O"))},
                        {"url", Quoted(data.HtmlUrl)},
                        {
                            "changes",
                            new YamlSequenceNode(
                                category.Changes.Select(c => new YamlMappingNode
                                {
                                    {"type", Quoted(c.Type.ToString())},
                                    {"message", Quoted(c.Message)},
                                }))
                        }
                    };

                    if (category.Category != ChangelogData.MainCategory)
                        yamlMapping.Add("category", category.Category);

                    var yamlStream = new YamlStream(new YamlDocument(yamlMapping));

                    using var writer = new StreamWriter(fileName);
                    yamlStream.Save(writer);
                }
            }
            catch (Exception e)
            {
                _log.LogError(e, "Exception while writing changelog to disk!");
            }
        }

        private static YamlScalarNode Quoted(string content)
        {
            return new YamlScalarNode(content) { Style = ScalarStyle.DoubleQuoted };
        }

        private abstract record MsgQueueBase
        {
        }

        private sealed record MsgWritePRChangelog(ChangelogData Data) : MsgQueueBase
        {
        }

        private sealed record MsgQueueUpdate : MsgQueueBase
        {
        }

        [Serializable]
        public class ProcessFailedException : Exception
        {
            public ProcessFailedException()
            {
            }

            public ProcessFailedException(string message) : base(message)
            {
            }

            public ProcessFailedException(string message, Exception inner) : base(message, inner)
            {
            }
        }
    }
}
