namespace RatioForge
{
    using System;
    using System.Diagnostics;
    using System.Windows.Forms;

    internal partial class AboutForm : Form
    {
        internal string version;

        internal AboutForm(string Ver)
        {
            this.InitializeComponent();
            this.version = Ver;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void AboutForm_Load(object sender, EventArgs e)
        {
            this.Text += " version " + this.version;
        }

        private void linkGitHubPage_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Links.OpenUrl(Links.GitHubPage);
        }

        private void linkAuthorWebSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Links.OpenUrl(Links.OriginalAuthorPage);
        }

        private void linkEMail_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Links.OpenUrl(Links.MailToContact);
        }

        private void linkWebSite_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            Links.OpenUrl(Links.ProgramPage);
        }
    }
}
