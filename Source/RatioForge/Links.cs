namespace RatioForge
{
    internal static class Links
    {
        public const string ProgramPage = "https://github.com/tsautier/RatioForge";

        public const string GitHubPage = "https://github.com/tsautier/RatioForge";

        public const string OriginalAuthorPage = "http://nikolay.it";

        public const string OriginalProjectPage = "https://github.com/NikolayIT/RatioMaster.NET";

        // Contact information - update with your own
        public const string ContactEmail = "contact@example.com";

        public const string MailToContact = "mailto:" + ContactEmail;

        public const string PayPal = "https://github.com/tsautier/RatioForge"; // TODO: Update with valid donation link if needed
        public const string AuthorPage = ProgramPage;

        public static void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch (System.Exception ex)
            {
                System.Windows.Forms.MessageBox.Show("Could not open URL: " + url + "\r\nError: " + ex.Message, "Error", System.Windows.Forms.MessageBoxButtons.OK, System.Windows.Forms.MessageBoxIcon.Error);
            }
        }
    }
}