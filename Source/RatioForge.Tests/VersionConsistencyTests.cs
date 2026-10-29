namespace RatioForge.Tests;

using System.IO;
using System.Reflection;
using NUnit.Framework;

[TestFixture]
public sealed class VersionConsistencyTests
{
    [Test]
    public void AssemblyVersionShouldMatchVersionFile()
    {
        string? directory = TestContext.CurrentContext.TestDirectory;
        while (directory is not null && !File.Exists(Path.Combine(directory, "version.txt")))
        {
            directory = Path.GetDirectoryName(directory);
        }

        Assert.That(directory, Is.Not.Null, "version.txt was not found above the test directory.");
        string expected = File.ReadAllText(Path.Combine(directory!, "version.txt")).Trim();
        string? actual = typeof(TorrentDocument).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        Assert.That(actual, Is.EqualTo(expected));
    }
}
