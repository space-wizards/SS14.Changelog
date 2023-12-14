using System;
using System.Collections.Immutable;

namespace SS14.Changelog
{
    public sealed record ChangelogData
    {
        public ChangelogData(string author, ImmutableArray<(ChangelogEntryType, string)> changes, DateTimeOffset time)
        {
            Author = author;
            Changes = changes;
            Time = time;
        }

        public string Author { get; }
        public ImmutableArray<(ChangelogEntryType, string)> Changes { get; }
        public DateTimeOffset Time { get; }
        public int Number { get; init; }
        public string Url { get; init; }
    }
    
    public enum ChangelogEntryType
    {
        Add,
        Remove,
        Fix,
        Tweak
    }
}