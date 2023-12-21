namespace SS14.Changelog.Configuration
{
    public class ChangelogConfig
    {
        public string? ChangelogRepo { get; set; }
        public string ChangelogBranchName { get; set; } = "";
        public string ChangelogFilename { get; set; } = "Changelog.yml";

        /// <summary>
        /// Git remote address (e.g. <c>git@github.com:space-wizards/space-station-14.git</c>).
        /// </summary>
        /// <remarks>
        /// This is used when cloning the repo for the first time.
        /// </remarks>
        public string? ChangelogRepoRemote { get; set; } = "";

        /// <summary>
        /// Git commit author name used for commits.
        /// </summary>
        public string? CommitAuthorName { get; set; }

        /// <summary>
        /// Git commit author email used for commits.
        /// </summary>
        public string? CommitAuthorEmail { get; set; }

        public string? SshKey { get; set; }
        public string? GitHubSecret { get; set; }
        public int DelaySeconds { get; set; } = 60;
    }
}