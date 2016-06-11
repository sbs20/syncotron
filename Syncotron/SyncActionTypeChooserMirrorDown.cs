namespace Sbs20.Syncotron
{
    public class SyncActionTypeChooserMirrorDown : ISyncActionTypeChooser
    {
        public SyncActionType Choose(SyncAction action)
        {
            if (action.Remote == null || action.Remote.IsDeleted)
            {
                if (action.Local != null)
                {
                    return SyncActionType.DeleteLocal;
                }
                else
                {
                    return SyncActionType.None;
                }
            }

            if (action.Local == null)
            {
                return SyncActionType.Download;
            }

            if (action.Local.IsFolder ||
                action.Remote.ServerRev == action.Local.ServerRev ||
                action.Remote.Hash == action.Local.Hash)
            {
                return SyncActionType.None;
            }

            return SyncActionType.Download;
        }
    }
}
