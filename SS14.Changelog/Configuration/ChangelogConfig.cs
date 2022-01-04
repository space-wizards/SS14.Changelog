namespace SS14.Changelog.Configuration
{
    public class ChangelogConfig
    {
        public string? ChangelogRepo { get; set; }
        public string? ChangelogBranchName { get; set; }
        public string ChangelogFilename { get; set; } = "Changelog.yml";
        public string? SshKey { get; set; }
        public string? GitHubSecret { get; set; }
        public int DelaySeconds { get; set; } = 60;
    }
}