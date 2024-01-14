using System;
using System.Diagnostics.CodeAnalysis;
using NUnit.Framework;
using SS14.Changelog.Configuration;
using SS14.Changelog.Controllers;

namespace SS14.Changelog.Tests
{
    [Parallelizable(ParallelScope.All)]
    [TestFixture]
    // NUnit assertion is bugged.
    [SuppressMessage("Assertion", "NUnit2022:Missing property required for constraint")]
    public class ChangelogParseTest
    {
        [Test]
        public void Test()
        {
            const string text = """
                Did stuff!

                :cl: Ev1__l P-JB2323
                - add: Did the thing
                - remove: Removed the thing
                - fix: A
                - bugfix: B
                - bug: C

                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("PJB"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig());

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Ev1__l P-JB2323"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                        new(ChangelogData.ChangeType.Fix, "A"),
                        new(ChangelogData.ChangeType.Fix, "B"),
                        new(ChangelogData.ChangeType.Fix, "C"),
                    }));
            });
        }

        [Test]
        public void TestWithoutName()
        {
            const string text = """

                Did stuff!

                :cl:
                - add: Did the thing
                - remove: Removed the thing

                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig());

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Swept"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                    }));
            });
        }

        [Test]
        public void TestComment()
        {
            const string text = """

                Did stuff!

                <!-- The :cl: symbol
                -->

                :cl:
                - add: Did the thing
                - remove: Removed the thing

                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig());

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Swept"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                    }));
            });
        }

        [Test]
        public void TestBroke()
        {
            const string text =
                "Makes it possible to repair things with a welder.\r\n\r\n**Changelog**\r\n:cl: AJCM\r\n- add: Makes gravity generator and windows repairable with a lit welding tool \r\n\r\n";

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("AJCM-Git"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig());

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("AJCM"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Makes gravity generator and windows repairable with a lit welding tool"),
                    }));
            });
        }

        [Test]
        public void TestCategory()
        {
            const string text = """

                Did stuff!

                :cl:
                ADMIN:
                - add: Did the thing
                - remove: Removed the thing

                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig());

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Swept"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                    }));
            });
        }

        [Test]
        public void TestCategoryMulti()
        {
            const string text = """

                Did stuff!

                :cl:
                ADMIN:
                - add: Did the thing
                - remove: Removed the thing
                MAIN:
                - add: Did more thing
                - remove: Removed more thing
                ADMIN:
                - fix: Fix the thing
                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig { ExtraCategories = new []{"Admin"}});

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Swept"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(2));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo("Admin")
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                        new(ChangelogData.ChangeType.Fix, "Fix the thing"),
                    }));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did more thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed more thing"),
                    }));
            });
        }

        [Test]
        public void TestCategoryInvalid()
        {
            const string text = """

                Did stuff!

                :cl:
                - add: Did the thing
                - remove: Removed the thing
                NOTACATEGORY:
                - add: WOW
                """;

            var time = new DateTimeOffset(2021, 1, 1, 1, 1, 1, TimeSpan.Zero);
            var pr = new GHPullRequest(true, text, new GHUser("Swept"), time, new GHPullRequestBase("master"), 123,
                "https://www.example.com");
            var parsed = WebhookController.ParsePRBody(pr, new ChangelogConfig { ExtraCategories = new []{"Admin"}});

            Assert.Multiple(() =>
            {
                Assert.That(parsed, Is.Not.Null);
                Assert.That(parsed.Author, Is.EqualTo("Swept"));
                Assert.That(parsed.Time, Is.EqualTo(time));
                Assert.That(parsed.HtmlUrl, Is.EqualTo("https://www.example.com"));
                Assert.That(parsed.Categories, Has.Length.EqualTo(1));
                Assert.That(parsed.Categories, Has.One
                    .Property(nameof(ChangelogData.CategoryData.Category)).EqualTo(ChangelogData.MainCategory)
                    .And.Property(nameof(ChangelogData.CategoryData.Changes)).EquivalentTo(new ChangelogData.Change[]
                    {
                        new(ChangelogData.ChangeType.Add, "Did the thing"),
                        new(ChangelogData.ChangeType.Remove, "Removed the thing"),
                        new(ChangelogData.ChangeType.Add, "WOW"),
                    }));
            });
        }
    }
}