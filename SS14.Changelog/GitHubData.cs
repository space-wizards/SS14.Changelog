using System;
using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace SS14.Changelog
{
    [JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
    [JsonSerializable(typeof(GHPullRequestEvent))]
    [JsonSerializable(typeof(GHPushEvent))]
    public sealed partial class GitHubSerializationContext : JsonSerializerContext;

    public sealed record GHPullRequestEvent(int Number, string Action, GHPullRequest PullRequest);

    public sealed record GHPullRequest(
        bool Merged,
        string Body,
        GHUser User,
        DateTimeOffset? MergedAt,
        GHPullRequestBase Base,
        int Number,
        string Url);

    public sealed record GHPullRequestBase(string Ref);

    public sealed record GHUser(string Login);

    public sealed record GHPushEvent(ImmutableArray<GHPushedCommit> Commits, string Ref);

    public sealed record GHPushedCommit(ImmutableArray<string> Added, ImmutableArray<string> Modified);
}