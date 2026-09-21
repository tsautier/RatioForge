namespace RatioForge.Tests;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

[TestFixture]
public sealed class ProfileExchangeTests
{
    [Test]
    public void SessionProfileExchangeShouldBeVersionedMergeableAndPasswordFree()
    {
        string directory = TemporaryDirectory();
        string path = Path.Combine(directory, "sessions.json");
        try
        {
            var settings = new ApplicationSettings
            {
                DefaultProfileName = "Transmission 3.00",
                UploadRateKib = 75,
                ProxyMode = TrackerProxyMode.Http,
                ProxyHost = "proxy.example",
                ProxyUsername = "operator",
                ProxyPassword = "never-export-this",
            };
            SessionProfileStore.Export([SessionProfile.FromSettings("Imported", settings)], path);

            IReadOnlyList<SessionProfile> imported = SessionProfileStore.Import(path);
            IReadOnlyList<SessionProfile> merged = SessionProfileStore.Merge(
                [SessionProfile.FromSettings("Imported", new ApplicationSettings()),
                 SessionProfile.FromSettings("Existing", new ApplicationSettings())],
                imported);
            string json = File.ReadAllText(path);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"FormatVersion\": 1"));
                Assert.That(json, Does.Not.Contain("never-export-this"));
                Assert.That(imported.Single().ProxyUsername, Is.EqualTo("operator"));
                Assert.That(merged, Has.Count.EqualTo(2));
                Assert.That(merged.Single(profile => profile.Name == "Imported").UploadRateKib, Is.EqualTo(75));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SessionProfileImportShouldRetainLegacyArrayCompatibility()
    {
        string directory = TemporaryDirectory();
        string path = Path.Combine(directory, "legacy.json");
        try
        {
            SessionProfileStore.Save(
                [SessionProfile.FromSettings("Legacy", new ApplicationSettings())], path);

            Assert.That(SessionProfileStore.Import(path).Single().Name, Is.EqualTo("Legacy"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void SessionProfileImportShouldRejectUnsupportedFormatVersions()
    {
        string directory = TemporaryDirectory();
        string path = Path.Combine(directory, "future-sessions.json");
        try
        {
            File.WriteAllText(path, "{\"FormatVersion\":2,\"Profiles\":[]}");

            Assert.That(
                () => SessionProfileStore.Import(path),
                Throws.TypeOf<InvalidDataException>().With.Message.Contains("version 2"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ClientCatalogExportShouldRoundTripEveryEffectiveDefinition()
    {
        string directory = TemporaryDirectory();
        string path = Path.Combine(directory, "clients.json");
        try
        {
            ClientProfileCatalog.Export(path);
            string json = File.ReadAllText(path);
            IReadOnlyList<ClientProfileDefinition> parsed = ClientProfileCatalog.ParseDefinitions(json);

            Assert.Multiple(() =>
            {
                Assert.That(json, Does.Contain("\"FormatVersion\": 1"));
                Assert.That(parsed, Has.Count.EqualTo(ClientProfileCatalog.Definitions.Count));
                Assert.That(parsed.Select(profile => profile.Name),
                    Is.EqualTo(ClientProfileCatalog.Definitions.Select(profile => profile.Name)));
            });
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void ClientCatalogShouldRejectUnsupportedFormatVersions()
    {
        Assert.That(
            () => ClientProfileCatalog.ParseDefinitions("{\"FormatVersion\":2,\"Clients\":[]}"),
            Throws.TypeOf<InvalidDataException>().With.Message.Contains("version 2"));
    }

    private static string TemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), $"ratioforge-profile-exchange-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
