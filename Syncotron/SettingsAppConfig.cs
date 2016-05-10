using System.Configuration;

namespace Sbs20.Syncotron
{
    public class SettingsAppConfig : ISettings
    {
        public string Dropbox_ClientId
        {
            get { return ConfigurationManager.AppSettings["Dropbox_ClientId"]; }
        }

        public string Dropbox_Secret
        {
            get { return ConfigurationManager.AppSettings["Dropbox_Secret"]; }
        }
    }
}
