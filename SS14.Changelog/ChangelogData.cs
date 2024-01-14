using System;
using System.Collections.Immutable;

namespace SS14.Changelog
{
    public sealed record ChangelogData
    {
        public const string MainCategory = "Main";

        public ChangelogData(string author, ImmutableArray<CategoryData> categories, DateTimeOffset time)
        {
            Author = author;
            Categories = categories;
            Time = time;
        }

        public string Author { get; }
        public ImmutableArray<CategoryData> Categories { get; }
        public DateTimeOffset Time { get; }
        public int Number { get; init; }
        public required string HtmlUrl { get; init; }

        public sealed record CategoryData(string Category, ImmutableArray<Change> Changes);

        public record struct Change(ChangeType Type, string Message);

        public enum ChangeType
        {
            Add,
            Remove,
            Fix,
            Tweak
        }
    }
}