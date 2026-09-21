namespace RatioForge
{
    using System;
    using System.Net.Http;
    using System.Reflection;
    using System.Text;

    public class VersionChecker
    {
        private readonly Func<string> getServerVersion;
        private readonly StringBuilder logBuilder;
        public static readonly string LocalVersion = GetAssemblyVersion();
        public static readonly string PublicVersion = LocalVersion;
        public const string ReleaseDate = "21-09-2026";

        private readonly string userAgent;

        public VersionChecker(string log)
            : this(log, null)
        {
        }

        internal VersionChecker(string log, Func<string> getServerVersion)
        {
            this.userAgent = "RatioForge"
                             + $"/{LocalVersion} ({Environment.OSVersion}; .NET CLR {Environment.Version}; {Environment.UserName}.{Environment.ProcessorCount})";
            this.getServerVersion = getServerVersion ?? this.GetServerVersionId;
            this.logBuilder = new StringBuilder(log ?? string.Empty);
            this.Log = this.logBuilder.ToString();
        }

        public string Log { get; private set; }

        internal string RemoteVersion { get; private set; }

        public bool CheckNewVersion()
        {
            try
            {
                bool result = false;
                this.AppendLog("Local Version: " + LocalVersion + "\n");
                this.AppendLog("Checking for new version..." + "\n");
                this.RemoteVersion = this.getServerVersion();
                //// mainForm.txtRemote.Text = remoteVersion;
                Version remoteVersion;
                Version localVersion;
                if (!Version.TryParse(this.RemoteVersion, out remoteVersion) || !Version.TryParse(LocalVersion, out localVersion))
                {
                    this.RemoteVersion = "error";
                    this.AppendLog("Error checking new version!!!" + "\n" + "\n");
                    return false;
                }

                this.AppendLog("Remote Version: " + this.RemoteVersion + "\n" + "\n");
                if (remoteVersion > localVersion)
                {
                    result = true;
                }

                return result;
            }
            catch (Exception exception1)
            {
                this.AppendLog("Error checking for new version:\n" + exception1.Message + "\n");
                return false;
            }
        }

        public string GetServerVersionId()
        {
            try
            {
                using var client = new HttpClient { Timeout = TimeSpan.FromMilliseconds(2500) };
                client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", this.userAgent);
                var checker = new ReleaseUpdateChecker(client);
                return checker.CheckAsync(LocalVersion).GetAwaiter().GetResult().LatestVersion.ToString(3);
            }
            catch (Exception exception1)
            {
                this.AppendLog("Error in GetVersion(string url):\n" + exception1.Message + "\n");
            }

            return string.Empty;
        }

        private void AppendLog(string value)
        {
            this.logBuilder.Append(value);
            this.Log = this.logBuilder.ToString();
        }

        private static string GetAssemblyVersion()
        {
            var attribute = typeof(VersionChecker).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>();
            var version = attribute?.InformationalVersion ?? typeof(VersionChecker).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
            var metadataIndex = version.IndexOf('+');
            return metadataIndex >= 0 ? version.Substring(0, metadataIndex) : version;
        }
    }
}
