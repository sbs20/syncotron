using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class ReplicatorContext
    {
        private LocalStorage localStorage;
        private LocalFilesystemService localFilesystem;
        private ICloudService cloudService;
        private IHashProvider hashProvider;
        public ISettings Settings { get; private set; }
        public ISyncActionTypeChooser SyncActionTypeChooser { get; private set; }

        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public CommandType CommandType { get; set; }
        public SyncDirection SyncDirection { get; set; }
        public ProcessingMode ProcessingMode { get; set; }
        public FileService RemoteService { get; set; }
        public IList<string> Exclusions { get; private set; }
        public bool IgnoreCertificateErrors { get; set; }
        public HashProviderType HashProviderType { get; set; }
        public int MaximumConcurrency { get; set; }
        public bool IsDebug { get; set; }
        public ConflictStrategy ConflictStrategy { get; set; }
        public bool Recover { get; set; }

        public ReplicatorContext()
        {
            // If building this from scratch - you will need to implement your own settings
            this.Settings = new Settings();
            this.RemoteService = FileService.Dropbox;
            this.CommandType = CommandType.AnalysisOnly;
            this.SyncDirection = SyncDirection.TwoWay;
            this.ProcessingMode = ProcessingMode.Serial;
            this.Exclusions = new List<string>();
            this.IgnoreCertificateErrors = false;
            this.HashProviderType = HashProviderType.DateTimeAndSize;

            // Create helpers
            this.SyncActionTypeChooser = this.CreateSyncActionTypeChooser();
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
            this.LocalStorage.SettingsWrite("ProcessingMode", this.ProcessingMode);
            this.LocalStorage.SettingsWrite("RemoteService", this.RemoteService);
            this.LocalStorage.SettingsWrite("Exclusions", this.Exclusions);
            this.LocalStorage.SettingsWrite("IgnoreCertificateErrors", this.IgnoreCertificateErrors);
            this.LocalStorage.SettingsWrite("HashProviderType", this.HashProviderType);
            this.LocalStorage.SettingsWrite("MaximumConcurrency", this.MaximumConcurrency);
            this.LocalStorage.SettingsWrite("IsDebug", this.IsDebug);
            this.LocalStorage.SettingsWrite("ConflictStrategy", this.ConflictStrategy);
        }

        private ISyncActionTypeChooser CreateSyncActionTypeChooser()
        {
            switch (this.SyncDirection)
            {
                case SyncDirection.TwoWay:
                    return new SyncActionTypeChooserTwoWay(this);

                case SyncDirection.MirrorDown:
                    return new SyncActionTypeChooserMirrorDown();

                case SyncDirection.MirrorUp:
                    return new SyncActionTypeChooserMirrorUp();

                default:
                    throw new NotImplementedException();
            }
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

        public IHashProvider HashProvider
        {
            get
            {
                if (this.hashProvider == null)
                {
                    switch (this.HashProviderType)
                    {
                        case HashProviderType.MD5:
                            this.hashProvider = new MD5Hash();
                            break;

                        case HashProviderType.DateTimeAndSize:
                        default:
                            this.hashProvider = new DateTimeSizeHash();
                            break;
                    }
                }

                return this.hashProvider;
            }
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

        public bool IsRunning
        {
            get { return this.LocalStorage.SettingsRead<bool>("IsRunning"); }
            set { this.LocalStorage.SettingsWrite("IsRunning", value); }
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
    }
}