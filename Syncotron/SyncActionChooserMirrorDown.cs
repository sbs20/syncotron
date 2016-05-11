namespace Sbs20.Syncotron
{
    public class SyncActionChooserMirrorDown : ISyncActionChooser
    {
        public SyncActionType Choose(FileItemPair pair)
        {
            if (pair.Remote == null || pair.Remote.IsDeleted)
            {
                return SyncActionType.DeleteLocal;
            }

            if (pair.Local == null)
            {
                return SyncActionType.Download;
            }

            if (pair.Local.IsFolder ||
                pair.Remote.ServerRev == pair.Local.ServerRev ||
                pair.Remote.Hash == pair.Local.Hash)
            {
                return SyncActionType.None;
            }

            return SyncActionType.Download;
        }
    }
}
