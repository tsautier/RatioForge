namespace RatioForge.Tests
{
    using System;

    using NUnit.Framework;

    using RatioForge;

    [TestFixture]
    public class VersionCheckerTests
    {
        [Test]
        public void CheckNewVersionShouldDetectNewerSemanticVersion()
        {
            var versionChecker = new VersionChecker(string.Empty, () => "1.1.6");

            var hasNewVersion = versionChecker.CheckNewVersion();

            Assert.That(hasNewVersion, Is.True);
        }

        [Test]
        public void CheckNewVersionShouldRejectInvalidRemoteVersion()
        {
            var versionChecker = new VersionChecker(string.Empty, () => "invalid");

            var hasNewVersion = versionChecker.CheckNewVersion();

            Assert.That(hasNewVersion, Is.False);
            Assert.That(versionChecker.RemoteVersion, Is.EqualTo("error"));
        }

        [Test]
        public void PublicVersionShouldComeFromAssemblyMetadata()
        {
            Assert.Multiple((Action)(() =>
            {
                Assert.That(VersionChecker.PublicVersion, Is.EqualTo("1.1.5"));
                Assert.That(VersionChecker.ReleaseDate, Is.EqualTo("15-09-2026"));
            }));
        }
    }
}
