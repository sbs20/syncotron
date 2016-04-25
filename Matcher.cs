using System.Collections.Generic;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Matcher
    {
        private object mutex = new object();

        public long LocalFileCount { get; set; }
        public long RemoteFileCount { get; set; }
        public ReplicatorArgs ReplicatorArgs { get; private set; }
        public Dictionary<string, FileItemPair> FilePairs { get; private set; }

        public Matcher(ReplicatorArgs args)
        {
            this.ReplicatorArgs = args;
            this.FilePairs = new Dictionary<string, FileItemPair>();
        }

        private string Path(FileItem file)
        {
            return file.Source == FileService.Local ?
                file.Path.Substring(this.ReplicatorArgs.LocalPath.Length) :
                file.Path.Substring(this.ReplicatorArgs.RemotePath.Length);
        }

        private string Key(FileItem file)
        {
            return Path(file).ToLowerInvariant();
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
                        Path = this.Path(file)
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
            IFileItemProvider cloudService = new DropboxService(this.ReplicatorArgs);
            await cloudService.ForEachAsync(this.ReplicatorArgs.RemotePath, true, false, (item) => this.Add(item));
        }

        private async Task ScanLocalAsync()
        {
            IFileItemProvider service = new LocalFilesystemService();
            await service.ForEachAsync(this.ReplicatorArgs.LocalPath, true, false, (item) => this.Add(item));
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
