using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SS14.Changelog
{
    // System.Text.Json doesn't handle kebab_case.
    // Great.
    
    public sealed record GHPullRequestEvent
    {
        [JsonConstructor]
        public GHPullRequestEvent(int number, string action, GHPullRequest pullRequest)
        {
            Number = number;
            Action = action;
            PullRequest = pullRequest;
        }

        public int Number { get; }
        public string Action { get; }
        [JsonPropertyName("pull_request")]
        public GHPullRequest PullRequest { get; }
    }

    public sealed record GHPullRequest
    {
        [JsonConstructor]
        public GHPullRequest(bool merged, string body, GHUser user, DateTimeOffset? mergedAt, GHPullRequestBase @base, int number, string url)
        {
            Merged = merged;
            Body = body;
            User = user;
            MergedAt = mergedAt;
            Base = @base;
            Number = number;
            Url = url;
        }

        public bool Merged { get; }
        public string Body { get; }
        public GHUser User { get; }
        [JsonPropertyName("merged_at")]
        public DateTimeOffset? MergedAt { get; }
        public GHPullRequestBase Base { get; }
        public int Number { get; }
        public string Url { get; }
    }

    public sealed record GHPullRequestBase
    {
        public string Ref { get; }

        [JsonConstructor]
        public GHPullRequestBase(string @ref)
        {
            Ref = @ref;
        }
    }

    public sealed record GHUser
    {
        [JsonConstructor]
        public GHUser(string login)
        {
            Login = login;
        }

        public string Login { get; }
    }

    public sealed record GHPushEvent
    {
        [JsonConstructor]
        public GHPushEvent(ImmutableArray<GHPushedCommit> commits, string @ref)
        {
            Commits = commits;
            Ref = @ref;
        }

        public ImmutableArray<GHPushedCommit> Commits { get; }
        public string Ref { get; }
    }

    public sealed record GHPushedCommit
    {
        [JsonConstructor]
        public GHPushedCommit(ImmutableArray<string> added, ImmutableArray<string> modified)
        {
            Added = added;
            Modified = modified;
        }

        public ImmutableArray<string> Added { get; }
        public ImmutableArray<string> Modified { get; }
    }
}