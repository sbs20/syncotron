using System;

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
            get { return this.CommonPath.ToLowerInvariant(); }
        }

        public FileItem PrimaryItem
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

        public override string ToString()
        {
            return this.Type.ToString() + ":" + this.Key;
        }
    }
}
