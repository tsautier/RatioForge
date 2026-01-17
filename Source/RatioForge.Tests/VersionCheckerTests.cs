namespace RatioForge.Tests
{
    using System;

    using NUnit.Framework;

    using RatioForge;

    [TestFixture]
    public class VersionCheckerTests
    {
        [Test]
        public void GetServerVersionIdShouldReturnExactlyFourCharacters()
        {
            var versionChecker = new VersionChecker(string.Empty);
            var serverVersion = versionChecker.GetServerVersionId();
            Console.WriteLine(serverVersion);
            Assert.That(serverVersion.Length, Is.EqualTo(4));
        }
    }
}
