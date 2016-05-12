using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class ReplicatorContext
    {
        private LocalFilesystemService localFilesystem;
        private ICloudService cloudService;
        private IHashProvider hashProvider;
        public ISettings Settings { get; private set; }
        public ISyncActionTypeChooser SyncActionTypeChooser { get; private set; }
        public LocalStorage LocalStorage { get; private set; }

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
            this.LocalStorage = new LocalStorage(this);
            this.SyncActionTypeChooser = this.CreateSyncActionTypeChooser();
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
                    if (this.HashProviderType == HashProviderType.DateTimeAndSize)
                    {
                        this.hashProvider = new DateTimeSizeHash();
                    }
                    else
                    {
                        this.hashProvider = new MD5Hash();
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
    }
}
