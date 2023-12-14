using System;
using NUnit.Framework;
using SS14.Changelog.Controllers;

namespace SS14.Changelog.Tests
{
    [TestFixture]
    public class ChangelogParseTest
    {
        [Test]
        public void Test()
        {
            const string text = @"
Did stuff!

:cl: Ev1__l P-JB2323
- add: Did the thing
- remove: Removed the thing
- fix: A
- bugfix: B
- bug: C
";

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("PJB"), time, new GHPullRequestBase("master"), 123, "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Author, Is.EqualTo("Ev1__l P-JB2323"));
            Assert.That(parsed.Time, Is.EqualTo(time));
            Assert.That(parsed.Url, Is.EqualTo("https://www.example.com"));
            Assert.That(parsed.Changes, Is.EquivalentTo(new[]
            {
                (ChangelogEntryType.Add, "Did the thing"),
                (ChangelogEntryType.Remove, "Removed the thing"),
                (ChangelogEntryType.Fix, "A"),
                (ChangelogEntryType.Fix, "B"),
                (ChangelogEntryType.Fix, "C"),
            }));
        }

        [Test]
        public void TestWithoutName()
        {
            const string text = @"
Did stuff!

:cl:
- add: Did the thing
- remove: Removed the thing
";

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123, "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Author, Is.EqualTo("Swept"));
            Assert.That(parsed.Time, Is.EqualTo(time));
            Assert.That(parsed.Url, Is.EqualTo("https://www.example.com"));
            Assert.That(parsed.Changes, Is.EquivalentTo(new[]
            {
                (ChangelogEntryType.Add, "Did the thing"),
                (ChangelogEntryType.Remove, "Removed the thing"),
            }));
        }

        [Test]
        public void TestComment()
        {
            const string text = @"
Did stuff!

<!-- The :cl: symbol 
-->

:cl:
- add: Did the thing
- remove: Removed the thing
";

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123, "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Author, Is.EqualTo("Swept"));
            Assert.That(parsed.Time, Is.EqualTo(time));
            Assert.That(parsed.Url, Is.EqualTo("https://www.example.com"));
            Assert.That(parsed.Changes, Is.EquivalentTo(new[]
            {
                (ChangelogEntryType.Add, "Did the thing"),
                (ChangelogEntryType.Remove, "Removed the thing"),
            }));
        }

        [Test]
        public void TestBroke()
        {
            const string text =
                "Makes it possible to repair things with a welder.\r\n\r\n**Changelog**\r\n:cl: AJCM\r\n- add: Makes gravity generator and windows repairable with a lit welding tool \r\n\r\n";

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("AJCM-Git"), time, new GHPullRequestBase("master"), 123, "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr);

            Assert.That(parsed, Is.Not.Null);
            Assert.That(parsed.Author, Is.EqualTo("AJCM"));
            Assert.That(parsed.Time, Is.EqualTo(time));
            Assert.That(parsed.Url, Is.EqualTo("https://www.example.com"));
            Assert.That(parsed.Changes, Is.EquivalentTo(new[]
            {
                (ChangelogEntryType.Add, "Makes gravity generator and windows repairable with a lit welding tool")
            }));
        }
    }
}