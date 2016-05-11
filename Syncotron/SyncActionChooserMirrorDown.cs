namespace Sbs20.Syncotron
{
    public class SyncActionChooserMirrorDown : ISyncActionChooser
    {
        public FileActionType Choose(FileItemPair pair)
        {
            if (pair.Remote == null || pair.Remote.IsDeleted)
            {
                return FileActionType.DeleteLocal;
            }

            if (pair.Local == null)
            {
                return FileActionType.Download;
            }

            if (pair.Local.IsFolder ||
                pair.Remote.ServerRev == pair.Local.ServerRev ||
                pair.Remote.Hash == pair.Local.Hash)
            {
                return FileActionType.None;
            }

            return FileActionType.Download;
        }
    }
}
