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

        public SyncActionType Choose(SyncAction action)
        {
            // With two way, the only way we can get a delete is if it's in 
            // a continuation state. Otherwise it's an initial scan and 
            // everything is real and new
            if (action.Local != null && action.Local.IsDeleted)
            {
                return SyncActionType.DeleteRemote;
            }
            else if (action.Remote != null && action.Remote.IsDeleted)
            {
                return SyncActionType.DeleteLocal;
            }

            // Simple case of upload
            if (action.Local != null && !action.Local.IsFolder && action.Remote == null)
            {
                return SyncActionType.Upload;
            }

            // And download
            if (action.Local == null && action.Remote != null)
            {
                return SyncActionType.Download;
            }

            // To get here, both local and remote exist
            if (action.Local.ServerRev == action.Remote.ServerRev)
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
                    return action.Local.LastModified > action.Remote.LastModified ? 
                        SyncActionType.Upload : 
                        SyncActionType.Download;

                case ConflictStrategy.KeepBoth:
                    return SyncActionType.KeepBoth;
            }

            throw new InvalidOperationException("Unknown");
        }
    }
}
