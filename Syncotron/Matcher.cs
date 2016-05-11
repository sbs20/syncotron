using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Matcher
    {
        private object mutex = new object();

        public long LocalFileCount { get; set; }
        public long RemoteFileCount { get; set; }
        public ReplicatorContext Context { get; private set; }
        public Dictionary<string, FileItemPair> FilePairs { get; private set; }
        public string RemoteCursor { get; private set; }
        public string LocalCursor { get; private set; }

        public Matcher(ReplicatorContext context)
        {
            this.Context = context;
            this.FilePairs = new Dictionary<string, FileItemPair>();
        }

        private string Key(FileItem file)
        {
            return this.Context.ToCommonPath(file).ToLowerInvariant();
        }

        private void Add(FileItem file)
        {
            lock(mutex)
            {
                FileItemPair filePair = null;

                if (this.FilePairs.ContainsKey(this.Key(file)))
                {
                    filePair = this.FilePairs[this.Key(file)];
                }
                else
                {
                    filePair = new FileItemPair
                    {
                        CommonPath = this.Context.ToCommonPath(file),
                        LocalPath = this.Context.ToLocalPath(file),
                        RemotePath = this.Context.ToRemotePath(file)
                    };

                    this.FilePairs[filePair.Key] = filePair;
                }

                if (file.Source == FileService.Local)
                {
                    filePair.Local = file;
                    this.LocalFileCount++;
                }
                else
                {
                    filePair.Remote = file;
                    this.RemoteFileCount++;
                }
            }
        }

        private async Task ScanRemoteAsync()
        {
            IFileItemProvider cloudService = new DropboxService(this.Context);
            if (string.IsNullOrEmpty(this.Context.RemoteCursor))
            {
                this.RemoteCursor = await cloudService.ForEachAsync(this.Context.RemotePath, true, false, (item) => this.Add(item));
            }
            else if (this.Context.ReplicationDirection != SyncDirection.MirrorUp)
            {
                this.RemoteCursor = await cloudService.ForEachContinueAsync(this.Context.RemoteCursor, (item) => this.Add(item));
            }
            else
            {
                this.RemoteCursor = await cloudService.LatestCursor(this.Context.RemotePath, true, true);
            }
        }

        private async Task ScanLocalAsync()
        {
            if (string.IsNullOrEmpty(this.Context.LocalCursor))
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachAsync(this.Context.LocalPath, true, true, (item) => this.Add(item));
            }
            else if (this.Context.ReplicationDirection != SyncDirection.MirrorDown)
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachContinueAsync(this.Context.LocalCursor, (item) => this.Add(item));
            }
            else
            {
                this.LocalCursor = await this.Context.LocalFilesystem.LatestCursor(this.Context.LocalPath, true, true);
            }
        }

        public async Task ScanAsync()
        {
            var local = this.ScanLocalAsync();
            var remote = this.ScanRemoteAsync();
            await local;
            await remote;
        }
    }
}
