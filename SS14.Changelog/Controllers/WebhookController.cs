using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Serilog;
using SS14.Changelog.Configuration;
using SS14.Changelog.Services;

namespace SS14.Changelog.Controllers
{
    [ApiController]
    public class WebhookController : Controller
    {
        private static readonly Regex IsChangelogFileRegex = new Regex(@"^Resources/Changelog/Parts/.*\.yml$");

        private static readonly Regex ChangelogHeaderRegex =
            new Regex(@"^\s*(?::cl:|🆑) *([a-z0-9_\- ,&]+)?\s*$", RegexOptions.IgnoreCase | RegexOptions.Multiline);

        private static readonly Regex ChangelogEntryRegex =
            new Regex(@"^ *[*-]? *(add|remove|tweak|fix|bug|bugfix): *([^\n\r]+)\r?$", RegexOptions.IgnoreCase);

        private static readonly Regex ChangelogCategoryRegex =
            new Regex(@"^\s*([a-z]+):\s*$", RegexOptions.IgnoreCase);

        private static readonly Regex CommentRegex = new(@"(?<!\\)<!--([^>]+)(?<!\\)-->");

        private readonly IOptions<ChangelogConfig> _cfg;
        private readonly IDiagnosticContext _context;
        private readonly ChangelogService _changelogService;
        private readonly ILogger<WebhookController> _log;

        public WebhookController(IOptions<ChangelogConfig> cfg, IDiagnosticContext context,
            ChangelogService changelogService, ILogger<WebhookController> log)
        {
            _cfg = cfg;
            _context = context;
            _changelogService = changelogService;
            _log = log;
        }

        [HttpPost]
        [Route("/hook")]
        public async Task<IActionResult> PostHook()
        {
            if (Request.Headers.TryGetValue("X-Hub-Delivery", out var delivery))
            {
                _context.Set("GitHubDelivery", delivery[0]);
            }

            if (_cfg.Value.GitHubSecret == null)
            {
                _log.LogError("GitHubSecret not set!");
                return Unauthorized();
            }

            var ms = new MemoryStream();
            await Request.Body.CopyToAsync(ms);
            ms.Position = 0;

            var sig = Request.Headers["X-Hub-Signature-256"][0];
            if (sig == null || !sig.StartsWith("sha256="))
            {
                _log.LogTrace("X-Hub-Signature-256 did not start with sha256");
                return BadRequest();
            }

            var hex = Utility.FromHex(sig.AsSpan("sha256=".Length));

            var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_cfg.Value.GitHubSecret));
            // ReSharper disable once MethodHasAsyncOverload
            hmac.ComputeHash(ms);
            var ourHash = hmac.Hash!;

            if (!Utility.SecretEqual(hex, ourHash))
            {
                _log.LogInformation("Failed authentication attempt: hash mismatch");
                return Unauthorized();
            }

            var eventType = Request.Headers["X-GitHub-Event"][0];

            _log.LogInformation("Handling GitHub event of type {Event}", eventType);

            switch (eventType)
            {
                case "push":
                    HandlePush(DeserializeGitHub<GHPushEvent>(ms));
                    break;

                case "pull_request":
                    HandlePullRequest(DeserializeGitHub<GHPullRequestEvent>(ms));
                    break;
            }

            return Ok();
        }

        private static T DeserializeGitHub<T>(MemoryStream stream)
        {
            return JsonSerializer.Deserialize<T>(
                stream.GetBuffer().AsSpan(0, (int) stream.Length),
                GitHubSerializationContext.Default.Options)!;
        }

        private void HandlePush(GHPushEvent eventData)
        {
            if (eventData.Ref != $"refs/heads/{_cfg.Value.ChangelogBranchName}")
            {
                _log.LogTrace("Push was to different branch, ignoring");
                return;
            }

            if (eventData.Commits
                .SelectMany(c => c.Added.Union(c.Modified))
                .Any(p => IsChangelogFileRegex.IsMatch(p)))
            {
                _log.LogDebug("Commit modified changelogs, queuing update");
                _changelogService.QueueUpdate();
            }
        }

        private void HandlePullRequest(GHPullRequestEvent eventData)
        {
            if (eventData.Action != "closed" || !eventData.PullRequest.Merged)
            {
                _log.LogTrace("Ignoring: PR was not merged");
                return;
            }

            if (eventData.PullRequest.Base.Ref != _cfg.Value.ChangelogBranchName)
            {
                _log.LogTrace("Ignoring: PR is to different branch");
                return;
            }

            var changelogData = ParsePRBody(eventData.PullRequest, _cfg.Value);
            if (changelogData == null)
            {
                _log.LogTrace("Did not find changelog in PR");
                return;
            }

            _log.LogInformation(
                "Parsed {EntryCount} entries in {CategoryCount} categories by {PRAuthor}",
                changelogData.Categories.SelectMany(p => p.Changes).Count(),
                changelogData.Categories.Length,
                changelogData.Author);

            _changelogService.PushPRChangelog(changelogData);
        }

        internal static ChangelogData? ParsePRBody(GHPullRequest pr, ChangelogConfig config)
        {
            var allCategories = config.ExtraCategories
                .Append(ChangelogData.MainCategory)
                .ToHashSet(StringComparer.InvariantCultureIgnoreCase);

            var body = CommentRegex.Replace(pr.Body, "");
            var match = ChangelogHeaderRegex.Match(body);
            if (!match.Success)
                return null;

            var author = match.Groups[1].Success ? match.Groups[1].Value.Trim() : pr.User.Login;
            var changelogBody = body.Substring(match.Index + match.Length);

            var currentCategory = ChangelogData.MainCategory;
            var entries = new List<(string, ChangelogData.Change)>();

            var reader = new StringReader(changelogBody);
            while (reader.ReadLine() is { } line)
            {
                var categoryMatch = ChangelogCategoryRegex.Match(line);
                if (categoryMatch.Success)
                {
                    // Changelog category directive.
                    // Check if it's actually a defined category, skip it otherwise.
                    var categoryName = categoryMatch.Groups[1].Value;
                    if (allCategories.TryGetValue(categoryName, out var matchedCategory))
                        currentCategory = matchedCategory;

                    continue;
                }

                var entryMatch = ChangelogEntryRegex.Match(line);
                if (!entryMatch.Success)
                    continue;

                var type = entryMatch.Groups[1].Value.ToLowerInvariant() switch
                {
                    "add" => ChangelogData.ChangeType.Add,
                    "remove" => ChangelogData.ChangeType.Remove,
                    "fix" or "bugfix" or "bug" => ChangelogData.ChangeType.Fix,
                    "tweak" => ChangelogData.ChangeType.Tweak,
                    _ => (ChangelogData.ChangeType?) null
                };
                
                var message = entryMatch.Groups[2].Value.Trim();

                if (type is { } t)
                    entries.Add((currentCategory, new ChangelogData.Change(t, message)));
            }

            var finalCategories = entries
                .GroupBy(e => e.Item1)
                .Select(g => new ChangelogData.CategoryData(g.Key, g.Select(e => e.Item2).ToImmutableArray()))
                .ToImmutableArray();

            return new ChangelogData(author, finalCategories, pr.MergedAt ?? DateTimeOffset.Now)
            {
                Number = pr.Number,
                HtmlUrl = pr.Html_url
            };
        }
    }
}
