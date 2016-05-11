namespace Sbs20.Syncotron
{
    public class SyncActionTypeChooserMirrorUp : ISyncActionTypeChooser
    {
        public SyncActionType Choose(SyncAction action)
        {
            if (action.Local == null || action.Local.IsDeleted)
            {
                return SyncActionType.DeleteRemote;
            }

            if (action.Remote == null)
            {
                return SyncActionType.Upload;
            }

            if (action.Remote.IsFolder ||
                action.Remote.ServerRev == action.Local.ServerRev ||
                action.Remote.Hash == action.Local.Hash)
            {
                return SyncActionType.None;
            }

            return SyncActionType.Upload;
        }
    }
}
