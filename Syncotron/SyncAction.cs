using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class SyncAction
    {
        public SyncActionType Type { get; set; }
        public Exception Exception { get; set; }

        public string CommonPath { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }

        public FileItem Local { get; set; }
        public FileItem Remote { get; set; }

        public SyncAction()
        {
        }

        public string Key
        {
            get { return this.CommonPath != null ? this.CommonPath.ToLowerInvariant() : string.Empty; }
        }

        private FileItem PrimaryItem
        {
            get
            {
                switch (this.Type)
                {
                    case SyncActionType.KeepBoth:
                    case SyncActionType.DeleteLocal:
                    case SyncActionType.DeleteRemote:
                        // This could be either depending on whether we've scanned or cursored
                        return this.Local ?? this.Remote;

                    case SyncActionType.Upload:
                    case SyncActionType.None:
                        return this.Local;

                    case SyncActionType.Download:
                        return this.Remote;

                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public ulong Size
        {
            get { return this.PrimaryItem != null ? this.PrimaryItem.Size : 0; }
        }

        public override string ToString()
        {
            return this.Type.ToString() + ":" + this.Key;
        }

        public async Task Reconstruct(ReplicatorContext context)
        {
            this.LocalPath = context.LocalPath + this.CommonPath;
            this.RemotePath = context.RemotePath + this.CommonPath;

            if (this.Type == SyncActionType.Download)
            {
                this.Remote = await context.CloudService.FileSelectAsync(this.RemotePath);
            }
            else if (this.Type == SyncActionType.Upload)
            {
                this.Local = await context.LocalFilesystem.FileSelectAsync(this.LocalPath);
            }
        }

        public bool IsUnconstructed
        {
            get { return string.IsNullOrEmpty(this.LocalPath); }
        }
    }
}
