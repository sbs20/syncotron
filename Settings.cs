using System.Configuration;

namespace Sbs20.Syncotron
{
    public class Settings
    {
        public static string Dropbox_AccessToken
        {
            get { return ConfigurationManager.AppSettings["Dropbox_AccessToken"]; }
        }

        public static string Dropbox_ClientId
        {
            get { return ConfigurationManager.AppSettings["Dropbox_ClientId"]; }
        }

        public static string Dropbox_Secret
        {
            get { return ConfigurationManager.AppSettings["Dropbox_Secret"]; }
        }
    }
}
