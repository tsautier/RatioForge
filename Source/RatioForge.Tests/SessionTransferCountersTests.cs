namespace RatioForge.Tests;

using System;
using NUnit.Framework;

[TestFixture]
public sealed class SessionTransferCountersTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 29, 12, 0, 0, TimeSpan.Zero);

    [Test]
    public void AdvanceShouldAccumulateRatesPerElapsedSecond()
    {
        var counters = new SessionTransferCounters();
        counters.Start(totalSize: 10 * 1024 * 1024, completedPercent: 0, Start);

        counters.Advance(Start.AddSeconds(10), 10 * 1024 * 1024, uploadKibPerSecond: 100, downloadKibPerSecond: 50, false);

        Assert.Multiple(() =>
        {
            Assert.That(counters.Uploaded, Is.EqualTo(100 * 1024 * 10));
            Assert.That(counters.Downloaded, Is.EqualTo(50 * 1024 * 10));
            Assert.That(counters.Ratio, Is.EqualTo(2d));
        });
    }

    [Test]
    public void DownloadShouldStopAtTorrentSizeAndReportCompletionOnce()
    {
        var counters = new SessionTransferCounters();
        counters.Start(totalSize: 1000 * 1024, completedPercent: 50, Start);

        bool completed = counters.Advance(Start.AddSeconds(60), 1000 * 1024, 0, downloadKibPerSecond: 100, false);
        counters.CompletionAnnounced = true;
        bool completedAgain = counters.Advance(Start.AddSeconds(120), 1000 * 1024, 0, 100, false);

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True);
            Assert.That(completedAgain, Is.False);
            Assert.That(counters.Downloaded, Is.EqualTo(500 * 1024));
            Assert.That(counters.RemainingBytes(1000 * 1024), Is.Zero);
            Assert.That(counters.CompletionPercent(1000 * 1024), Is.EqualTo(100d));
        });
    }

    [Test]
    public void AlreadyCompleteTorrentShouldNotAnnounceCompletion()
    {
        var counters = new SessionTransferCounters();
        counters.Start(totalSize: 1024, completedPercent: 100, Start);

        bool completed = counters.Advance(Start.AddSeconds(5), 1024, 10, 10, false);

        Assert.Multiple(() =>
        {
            Assert.That(counters.CompletionAnnounced, Is.True);
            Assert.That(completed, Is.False);
            Assert.That(counters.Downloaded, Is.Zero);
        });
    }

    [Test]
    public void ZeroLeechersShouldPauseOnlyUploadUntilLeechersReturn()
    {
        var counters = new SessionTransferCounters();
        counters.Start(totalSize: 100 * 1024 * 1024, completedPercent: 0, Start);

        LeecherStateChange paused = counters.UpdateLeechers(0, pauseUploadWhenNoLeechers: true);
        counters.Advance(Start.AddSeconds(10), 100 * 1024 * 1024, 10, 10, true);
        LeecherStateChange unchanged = counters.UpdateLeechers(0, true);
        LeecherStateChange resumed = counters.UpdateLeechers(3, true);
        counters.Advance(Start.AddSeconds(20), 100 * 1024 * 1024, 10, 10, true);

        Assert.Multiple(() =>
        {
            Assert.That(paused, Is.EqualTo(LeecherStateChange.UploadPaused));
            Assert.That(unchanged, Is.EqualTo(LeecherStateChange.Unchanged));
            Assert.That(resumed, Is.EqualTo(LeecherStateChange.UploadResumed));
            Assert.That(counters.Uploaded, Is.EqualTo(10 * 1024 * 10));
            Assert.That(counters.Downloaded, Is.EqualTo(10 * 1024 * 20));
        });
    }

    [Test]
    public void StartShouldKeepUploadedTotalUntilReset()
    {
        var counters = new SessionTransferCounters();
        counters.Start(1024 * 1024, 0, Start);
        counters.Advance(Start.AddSeconds(1), 1024 * 1024, 1, 1, false);

        counters.Start(1024 * 1024, 0, Start.AddSeconds(2));
        long uploadedAfterRestart = counters.Uploaded;
        counters.Reset();

        Assert.Multiple(() =>
        {
            Assert.That(uploadedAfterRestart, Is.EqualTo(1024));
            Assert.That(counters.Uploaded, Is.Zero);
            Assert.That(counters.Downloaded, Is.Zero);
            Assert.That(counters.Ratio, Is.Null);
            Assert.That(counters.LastKnownLeechers, Is.Null);
        });
    }

    [Test]
    public void MissingTorrentShouldReportNoProgressOrRemainingBytes()
    {
        var counters = new SessionTransferCounters();

        bool completed = counters.Advance(Start.AddSeconds(1), null, 1, 1, false);

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.False);
            Assert.That(counters.CompletionPercent(null), Is.Zero);
            Assert.That(counters.RemainingBytes(null), Is.Zero);
        });
    }
}
