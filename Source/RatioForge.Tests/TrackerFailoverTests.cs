namespace RatioForge.Tests;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

/// <summary>End-to-end failover tests against real HTTP trackers listening on loopback.</summary>
[TestFixture]
public sealed class TrackerFailoverTests
{
    private const string Accepting = "d8:completei4e10:incompletei2e8:intervali900e5:peers0:e";
    private const string Rejecting = "d14:failure reason17:torrent not founde";

    [Test]
    public async Task FailoverShouldSkipUnreachableAndRejectingTrackers()
    {
        using var dropping = new FakeHttpTracker(TrackerBehavior.DropConnection);
        using var rejecting = new FakeHttpTracker(Rejecting);
        using var accepting = new FakeHttpTracker(Accepting);
        string[] trackers = [dropping.AnnounceUrl, rejecting.AnnounceUrl, accepting.AnnounceUrl];
        var attempts = new List<TrackerAttempt>();
        var sending = new List<int>();

        using var client = new TrackerAnnounceClient();
        TrackerFailoverResult failover = await client.AnnounceWithFailoverAsync(
            trackers, 0, Options, (_, index) => sending.Add(index), attempts.Add);

        Assert.Multiple(() =>
        {
            Assert.That(failover.SuccessfulIndex, Is.EqualTo(2));
            Assert.That(failover.Result.IsSuccess, Is.True);
            Assert.That(failover.Result.IntervalSeconds, Is.EqualTo(900));
            Assert.That(failover.Result.Incomplete, Is.EqualTo(2));
            Assert.That(sending, Is.EqualTo(new[] { 0, 1, 2 }));
            Assert.That(attempts.Select(attempt => attempt.Attempt), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(attempts[0].Error, Is.InstanceOf<HttpRequestException>());
            Assert.That(attempts[1].Result?.IsSuccess, Is.False);
            Assert.That(attempts[1].Result?.Message, Does.Contain("torrent not found"));
            Assert.That(accepting.Requests.Single(), Does.Contain("event=started").And.Contain("info_hash="));
        });
    }

    [Test]
    public async Task FailoverShouldStartAtActiveTrackerAndWrapAround()
    {
        using var accepting = new FakeHttpTracker(Accepting);
        using var rejecting = new FakeHttpTracker(Rejecting);
        string[] trackers = [accepting.AnnounceUrl, rejecting.AnnounceUrl];
        var attempts = new List<TrackerAttempt>();

        using var client = new TrackerAnnounceClient();
        TrackerFailoverResult failover = await client.AnnounceWithFailoverAsync(
            trackers, 1, Options, onAttempted: attempts.Add);

        Assert.Multiple(() =>
        {
            Assert.That(failover.SuccessfulIndex, Is.Zero);
            Assert.That(attempts.Select(attempt => attempt.CandidateIndex), Is.EqualTo(new[] { 1, 0 }));
        });
    }

    [Test]
    public async Task RejectionShouldBeReportedWhenLaterCandidatesAreUnreachable()
    {
        using var rejecting = new FakeHttpTracker(Rejecting);
        using var dropping = new FakeHttpTracker(TrackerBehavior.DropConnection);
        string[] trackers = [rejecting.AnnounceUrl, dropping.AnnounceUrl];

        using var client = new TrackerAnnounceClient();
        TrackerFailoverResult failover = await client.AnnounceWithFailoverAsync(trackers, 0, Options);

        Assert.Multiple(() =>
        {
            Assert.That(failover.SuccessfulIndex, Is.Null);
            Assert.That(failover.Result.IsSuccess, Is.False);
            Assert.That(failover.Result.Message, Does.Contain("torrent not found"));
        });
    }

    [Test]
    public void UnreachableTrackersShouldSurfaceTheLastNetworkFailure()
    {
        using var dropping = new FakeHttpTracker(TrackerBehavior.DropConnection);
        string[] trackers = [ClosedPortTracker(), dropping.AnnounceUrl];
        using var client = new TrackerAnnounceClient();

        Assert.That(
            async () => await client.AnnounceWithFailoverAsync(trackers, 0, Options),
            Throws.InstanceOf<HttpRequestException>());
    }

    [Test]
    public async Task TrackerTimeoutShouldFailOverInsteadOfCancelling()
    {
        using var silent = new FakeHttpTracker(TrackerBehavior.NeverRespond);
        using var accepting = new FakeHttpTracker(Accepting);
        string[] trackers = [silent.AnnounceUrl, accepting.AnnounceUrl];
        var attempts = new List<TrackerAttempt>();
        using var httpClient = new HttpClient { Timeout = TimeSpan.FromMilliseconds(300) };

        using var client = new TrackerAnnounceClient(httpClient);
        TrackerFailoverResult failover = await client.AnnounceWithFailoverAsync(
            trackers, 0, Options, onAttempted: attempts.Add);

        Assert.Multiple(() =>
        {
            Assert.That(failover.SuccessfulIndex, Is.EqualTo(1));
            Assert.That(attempts[0].Error, Is.InstanceOf<TaskCanceledException>());
        });
    }

    [Test]
    public void CallerCancellationShouldStopFailover()
    {
        using var silent = new FakeHttpTracker(TrackerBehavior.NeverRespond);
        using var accepting = new FakeHttpTracker(Accepting);
        string[] trackers = [silent.AnnounceUrl, accepting.AnnounceUrl];
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));
        using var client = new TrackerAnnounceClient();

        Assert.Multiple(() =>
        {
            Assert.That(
                async () => await client.AnnounceWithFailoverAsync(trackers, 0, Options, cancellationToken: cancellation.Token),
                Throws.InstanceOf<OperationCanceledException>());
            Assert.That(accepting.Requests, Is.Empty);
        });
    }

    private static TrackerAnnounceOptions Options(string trackerUrl) => new(
        TorrentDocument.Load(Path.Combine(TestContext.CurrentContext.TestDirectory, "Fixtures", "single-file.torrent")),
        ClientProfileCatalog.Default,
        Uploaded: 0,
        Downloaded: 0,
        Port: 6881,
        PeerCount: 50,
        Proxy: new TrackerProxyOptions(TrackerProxyMode.None),
        TrackerUrl: trackerUrl);

    private static string ClosedPortTracker()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return $"http://127.0.0.1:{port}/announce";
    }

    private enum TrackerBehavior
    {
        Respond,
        DropConnection,
        NeverRespond,
    }

    /// <summary>Minimal HTTP/1.1 tracker that answers every announce with the same bencoded body.</summary>
    private sealed class FakeHttpTracker : IDisposable
    {
        private readonly TcpListener listener = new(IPAddress.Loopback, 0);
        private readonly CancellationTokenSource stopping = new();
        private readonly ConcurrentQueue<string> requests = new();
        private readonly Task serving;

        private readonly TrackerBehavior behavior;

        public FakeHttpTracker(string body)
            : this(TrackerBehavior.Respond, body)
        {
        }

        public FakeHttpTracker(TrackerBehavior behavior, string body = "")
        {
            this.behavior = behavior;
            listener.Start();
            AnnounceUrl = $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}/announce";
            serving = ServeAsync(Encoding.ASCII.GetBytes(body));
        }

        public string AnnounceUrl { get; }

        public IReadOnlyList<string> Requests => requests.ToArray();

        public void Dispose()
        {
            stopping.Cancel();
            listener.Dispose();
            try
            {
                serving.Wait(TimeSpan.FromSeconds(5));
            }
            catch (AggregateException)
            {
                // The accept loop ends with a cancellation or socket error once the listener stops.
            }

            stopping.Dispose();
        }

        private async Task ServeAsync(byte[] body)
        {
            while (!stopping.IsCancellationRequested)
            {
                using TcpClient connection = await listener.AcceptTcpClientAsync(stopping.Token);
                await using NetworkStream stream = connection.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                string requestLine = await reader.ReadLineAsync(stopping.Token) ?? string.Empty;
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stopping.Token)))
                {
                    // Discard request headers.
                }

                requests.Enqueue(requestLine);
                if (behavior == TrackerBehavior.DropConnection)
                {
                    continue;
                }

                if (behavior == TrackerBehavior.NeverRespond)
                {
                    await Task.Delay(Timeout.Infinite, stopping.Token);
                }

                byte[] header = Encoding.ASCII.GetBytes(
                    $"HTTP/1.1 200 OK\r\nContent-Type: text/plain\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
                await stream.WriteAsync(header, stopping.Token);
                await stream.WriteAsync(body, stopping.Token);
            }
        }
    }
}
