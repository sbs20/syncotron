using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class ReplicatorContext
    {
        private const int DefaultHttpReadTimeoutInSeconds = 30;
        private LocalStorage localStorage;
        private LocalFilesystemService localFilesystem;
        private ICloudService cloudService;
        public ISettings Settings { get; private set; }

        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public CommandType CommandType { get; set; }
        public SyncDirection SyncDirection { get; set; }
        public FileService RemoteService { get; set; }
        public IList<string> Exclusions { get; private set; }
        public bool IgnoreCertificateErrors { get; set; }
        public HashProviderType HashProviderType { get; set; }
        public bool IsDebug { get; set; }
        public ConflictStrategy ConflictStrategy { get; set; }
        public bool Recover { get; set; }
        public int HttpReadTimeoutInSeconds { get; set; }

        public ReplicatorContext()
        {
            // If building this from scratch - you will need to implement your own settings
            this.Settings = new Settings();
            this.RemoteService = FileService.Dropbox;
            this.CommandType = CommandType.AnalysisOnly;
            this.SyncDirection = SyncDirection.TwoWay;
            this.Exclusions = new List<string>();
            this.IgnoreCertificateErrors = false;
            this.HashProviderType = HashProviderType.DateTimeAndSize;
            this.HttpReadTimeoutInSeconds = DefaultHttpReadTimeoutInSeconds;
        }

        public string FileSuffix()
        {
            var key = this.RemoteService.ToString() + ":"
                + AsUnixPath(this.LocalPath.ToLowerInvariant()) + ":"
                + AsUnixPath(this.RemotePath.ToLowerInvariant());

            var bytes = new MD5Hash().HashBytes(key);
            var hash = Common.Base32Encoding.ToString(bytes).ToLowerInvariant().Replace("=", "$");
            return hash;
        }

        public string LocalStorageFilename()
        {
            return string.Format("syncotron_{0}.db", this.FileSuffix());
        }

        public string LogFilename()
        {
            return string.Format("syncotron_{0}_{1:yyyy-MM-dd}.log", this.FileSuffix(), DateTime.Today);
        }

        public string LockFilename()
        {
            return string.Format("syncotron_{0}.lok", this.FileSuffix());
        }

        public LocalStorage LocalStorage
        {
            get
            {
                if (this.localStorage == null)
                {
                    this.localStorage = new LocalStorage(this.LocalStorageFilename());
                }

                return this.localStorage;
            }
        }

        public void CleanAndPersist()
        {
            this.LocalPath = AsDirectoryPath(this.LocalPath);
            this.RemotePath = AsDirectoryPath(this.RemotePath);

            this.LocalStorage.SettingsWrite("LocalPath", this.LocalPath);
            this.LocalStorage.SettingsWrite("RemotePath", this.RemotePath);
            this.LocalStorage.SettingsWrite("CommandType", this.CommandType);
            this.LocalStorage.SettingsWrite("SyncDirection", this.SyncDirection);
            this.LocalStorage.SettingsWrite("RemoteService", this.RemoteService);
            this.LocalStorage.SettingsWrite("Exclusions", this.Exclusions);
            this.LocalStorage.SettingsWrite("IgnoreCertificateErrors", this.IgnoreCertificateErrors);
            this.LocalStorage.SettingsWrite("HashProviderType", this.HashProviderType);
            this.LocalStorage.SettingsWrite("IsDebug", this.IsDebug);
            this.LocalStorage.SettingsWrite("ConflictStrategy", this.ConflictStrategy);
        }

        private static string AsUnixPath(string path)
        {
            return path == null ? null : path.Replace("\\", "/");
        }

        private static string AsDirectoryPath(string path)
        {
            var p = AsUnixPath(path);
            if (p.EndsWith("/"))
            {
                p = p.Substring(0, p.Length - 1);
            }

            return p;
        }

        public string ToCommonPath(FileItem file)
        {
            string path = file.Source == FileService.Local ?
                file.Path.Substring(this.LocalPath.Length) :
                file.Path.Substring(this.RemotePath.Length);

            return AsUnixPath(path);
        }

        public string ToLocalPath(FileItem file)
        {
            return AsUnixPath(this.LocalPath + this.ToCommonPath(file));
        }

        public string ToRemotePath(FileItem file)
        {
            return AsUnixPath(this.RemotePath + this.ToCommonPath(file));
        }

        public string ToOppositePath(FileItem file)
        {
            return file.Source == FileService.Local ?
                this.ToRemotePath(file) :
                this.ToLocalPath(file);
        }

        public LocalFilesystemService LocalFilesystem
        {
            get
            {
                if (this.localFilesystem == null)
                {
                    this.localFilesystem = new LocalFilesystemService(this);
                }

                return this.localFilesystem;
            }
        }

        public ICloudService CloudService
        {
            get
            {
                if (this.cloudService == null)
                {
                    this.cloudService = new DropboxService(this);
                }

                return this.cloudService;
            }
        }

        private System.IO.FileInfo LockFileInfo
        {
            get { return new System.IO.FileInfo(this.LockFilename()); }
        }

        public bool IsRunning
        {
            get { return this.LockFileInfo.Exists; }
            set
            {
                if (value)
                {
                    this.LockFileInfo.Create();
                }
                else
                {
                    this.LockFileInfo.Delete();
                }
            }
        }

        public string RemoteCursor
        {
            get { return this.LocalStorage.SettingsRead<string>("RemoteCursor"); }
            set { this.LocalStorage.SettingsWrite("RemoteCursor", value); }
        }

        public string LocalCursor
        {
            get { return this.LocalStorage.SettingsRead<string>("LocalCursor"); }
            set { this.LocalStorage.SettingsWrite("LocalCursor", value); }
        }

        public bool IsRunningOnMono
        {
            get { return Type.GetType("Mono.Runtime") != null; }
        }

        public ScanMode ScanMode
        {
            get
            {
                if (string.IsNullOrEmpty(this.RemoteCursor) ||
                    string.IsNullOrEmpty(this.LocalCursor) ||
                    this.CommandType == CommandType.Fullsync)
                {
                    return ScanMode.Full;
                }

                return ScanMode.Continue;
            }
        }
    }
}