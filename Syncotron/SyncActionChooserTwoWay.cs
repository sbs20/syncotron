using System;

namespace Sbs20.Syncotron
{
    public class SyncActionChooserTwoWay : ISyncActionChooser
    {
        ReplicatorContext context;

        public SyncActionChooserTwoWay(ReplicatorContext context)
        {
            this.context = context;
        }

        public FileActionType Choose(FileItemPair pair)
        {
            // With two way, the only way we can get a delete is if it's in 
            // a continuation state. Otherwise it's an initial scan and 
            // everything is real and new
            if (pair.Local != null && pair.Local.IsDeleted)
            {
                return FileActionType.DeleteRemote;
            }
            else if (pair.Remote != null && pair.Remote.IsDeleted)
            {
                return FileActionType.DeleteLocal;
            }

            // Simple case of upload
            if (pair.Local != null && !pair.Local.IsFolder && pair.Remote == null)
            {
                return FileActionType.Upload;
            }

            // And download
            if (pair.Local == null && pair.Remote != null)
            {
                return FileActionType.Download;
            }

            // To get here, both local and remote exist
            if (pair.Local.ServerRev == pair.Remote.ServerRev)
            {
                return FileActionType.None;
            }

            // Conflict
            switch (this.context.ConflictStrategy)
            {
                case ConflictStrategy.None:
                    return FileActionType.None;

                case ConflictStrategy.LocalWin:
                    return FileActionType.Upload;

                case ConflictStrategy.RemoteWin:
                    return FileActionType.Download;

                case ConflictStrategy.LatestWin:
                    return pair.Local.LastModified > pair.Remote.LastModified ? 
                        FileActionType.Upload : 
                        FileActionType.Download;

                case ConflictStrategy.KeepBoth:
                    return FileActionType.KeepBoth;
            }

            throw new InvalidOperationException("Unknown");
        }
    }
}
