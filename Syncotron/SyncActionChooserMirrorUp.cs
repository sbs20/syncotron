namespace Sbs20.Syncotron
{
    public class SyncActionChooserMirrorUp : ISyncActionChooser
    {
        public SyncActionType Choose(FileItemPair pair)
        {
            if (pair.Local == null || pair.Local.IsDeleted)
            {
                return SyncActionType.DeleteRemote;
            }

            if (pair.Remote == null)
            {
                return SyncActionType.Upload;
            }

            if (pair.Remote.IsFolder ||
                pair.Remote.ServerRev == pair.Local.ServerRev ||
                pair.Remote.Hash == pair.Local.Hash)
            {
                return SyncActionType.None;
            }

            return SyncActionType.Upload;
        }
    }
}
