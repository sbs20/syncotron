using System;
using System.Collections.Generic;
using System.IO;

namespace Sbs20.Syncotron
{
    public class ReplicatorContext
    {
        private LocalFilesystemService localFilesystem;
        private ICloudService cloudService;
        private IHashProvider hashProvider;

        public int MaximumConcurrency { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }
        public DateTime LastRun { get; set; }
        public CommandType CommandType { get; set; }
        public ReplicationDirection ReplicationDirection { get; set; }
        public ProcessingMode ProcessingMode { get; set; }
        public IList<string> Exclusions { get; private set; }
        public bool IgnoreCertificateErrors { get; set; }
        public HashProviderType HashProviderType { get; set; }
        public LocalStorage LocalStorage { get; private set; }
        public ISettings Settings { get; private set; }
        public bool IsDebug { get; set; }
        public ConflictStrategy ConflictStrategy { get; set; }

        public ReplicatorContext()
        {
            this.CommandType = CommandType.AnalysisOnly;
            this.ReplicationDirection = ReplicationDirection.TwoWay;
            this.ProcessingMode = ProcessingMode.Serial;
            this.Exclusions = new List<string>();
            this.IgnoreCertificateErrors = false;
            this.HashProviderType = HashProviderType.FileDateTimeAndSize;
            this.LocalStorage = new LocalStorage(this);
            this.Settings = new Settings();
        }

        private static string AsUnixPath(string path)
        {
            return path == null ? null : path.Replace("\\", "/");
        }

        public string ToCommonPath(FileItem file)
        {
            return file.Source == FileService.Local ?
                file.Path.Substring(this.LocalPath.Length) :
                file.Path.Substring(this.RemotePath.Length);
        }

        public string ToLocalPath(string commonPath)
        {
            return AsUnixPath(this.LocalPath + commonPath);
        }

        public string ToRemotePath(string commonPath)
        {
            return AsUnixPath(this.RemotePath + commonPath);
        }

        public string ToOppositePath(FileItem file)
        {
            return file.Source == FileService.Local ?
                this.ToRemotePath(this.ToCommonPath(file)) :
                this.ToLocalPath(this.ToCommonPath(file));
        }

        public IList<string> Errors()
        {
            var errors = new List<string>();
            if (string.IsNullOrEmpty(this.LocalPath)) errors.Add("LocalPath");
            if (this.RemotePath == null) errors.Add("RemotePath");
            return errors;
        }

        public IHashProvider HashProvider
        {
            get
            {
                if (this.hashProvider == null)
                {
                    if (this.HashProviderType == HashProviderType.FileDateTimeAndSize)
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
