using System;

namespace Sbs20.Syncotron
{
    public class SyncActionTypeChooserTwoWay : ISyncActionTypeChooser
    {
        ReplicatorContext context;

        public SyncActionTypeChooserTwoWay(ReplicatorContext context)
        {
            this.context = context;
        }

        public SyncActionType Choose(FileItemPair pair)
        {
            // With two way, the only way we can get a delete is if it's in 
            // a continuation state. Otherwise it's an initial scan and 
            // everything is real and new
            if (pair.Local != null && pair.Local.IsDeleted)
            {
                return SyncActionType.DeleteRemote;
            }
            else if (pair.Remote != null && pair.Remote.IsDeleted)
            {
                return SyncActionType.DeleteLocal;
            }

            // Simple case of upload
            if (pair.Local != null && !pair.Local.IsFolder && pair.Remote == null)
            {
                return SyncActionType.Upload;
            }

            // And download
            if (pair.Local == null && pair.Remote != null)
            {
                return SyncActionType.Download;
            }

            // To get here, both local and remote exist
            if (pair.Local.ServerRev == pair.Remote.ServerRev)
            {
                return SyncActionType.None;
            }

            // Conflict
            switch (this.context.ConflictStrategy)
            {
                case ConflictStrategy.None:
                    return SyncActionType.None;

                case ConflictStrategy.LocalWin:
                    return SyncActionType.Upload;

                case ConflictStrategy.RemoteWin:
                    return SyncActionType.Download;

                case ConflictStrategy.LatestWin:
                    return pair.Local.LastModified > pair.Remote.LastModified ? 
                        SyncActionType.Upload : 
                        SyncActionType.Download;

                case ConflictStrategy.KeepBoth:
                    return SyncActionType.KeepBoth;
            }

            throw new InvalidOperationException("Unknown");
        }
    }
}
