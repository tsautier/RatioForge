namespace RatioForge
{
    using System;
    using System.IO;
    using System.Net;

    public class VersionChecker
    {
        public const string LocalVersion = "1.0.2";
        public const string PublicVersion = "1.0.2";
        public const string ReleaseDate = "18-01-2026";
        private const string ProgramPageVersion = "https://raw.githubusercontent.com/tsautier/RatioForge/master/version.txt";

        private readonly string userAgent;

        public VersionChecker(string log)
        {
            this.userAgent = "RatioForge"
                             + $"/{LocalVersion} ({Environment.OSVersion}; .NET CLR {Environment.Version}; {Environment.UserName}.{Environment.ProcessorCount})";
            this.Log = log;
        }

        // TODO: Replace with StringBuilder
        public string Log { get; private set; }

        internal string RemoteVersion { get; private set; }

        public bool CheckNewVersion()
        {
            try
            {
                bool result = false;
                this.Log = this.Log + ("Local Version: " + LocalVersion + "\n");
                this.Log = this.Log + ("Checking for new version..." + "\n");
                this.RemoteVersion = this.GetServerVersionId();
                //// mainForm.txtRemote.Text = remoteVersion;
                // Allow versions like 1.0.1 (5 chars) or 1.0.0.1 (7 chars). just check if it's too short or too long to be valid
                if (this.RemoteVersion.Length < 3 || this.RemoteVersion.Length > 10)
                {
                    this.RemoteVersion = "error";
                    this.Log = this.Log + ("Error checking new version!!!" + "\n" + "\n");
                    return false;
                }

                this.Log = this.Log + ("Remote Version: " + this.RemoteVersion + "\n" + "\n");
                if (string.Compare(this.RemoteVersion, LocalVersion, StringComparison.Ordinal) > 0)
                {
                    result = true;
                }

                return result;
            }
            catch (Exception exception1)
            {
                this.Log = this.Log + ("Error checking for new version:\n" + exception1.Message + "\n");
                return false;
            }
        }

        public string GetServerVersionId()
        {
            var url = ProgramPageVersion; // + LocalVersion;
            try
            {
                var request1 = (HttpWebRequest)WebRequest.Create(url);
                request1.UserAgent = this.userAgent;
                request1.Timeout = 2500;
                using (var response1 = (HttpWebResponse)request1.GetResponse())
                {
                    using (var reader1 = new StreamReader(response1.GetResponseStream()))
                    {
                        var data = reader1.ReadToEnd();
                        return data;
                    }
                }
            }
            catch (Exception exception1)
            {
                this.Log = this.Log + "Error in GetVersion(string url):\n" + exception1.Message + "\n";
            }

            return string.Empty;
        }
    }
}
