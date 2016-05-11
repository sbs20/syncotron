namespace Sbs20.Syncotron
{
    public class SyncActionChooserMirrorUp : ISyncActionChooser
    {
        public FileActionType Choose(FileItemPair pair)
        {
            if (pair.Local == null || pair.Local.IsDeleted)
            {
                return FileActionType.DeleteRemote;
            }

            if (pair.Remote == null)
            {
                return FileActionType.Upload;
            }

            if (pair.Remote.IsFolder ||
                pair.Remote.ServerRev == pair.Local.ServerRev ||
                pair.Remote.Hash == pair.Local.Hash)
            {
                return FileActionType.None;
            }

            return FileActionType.Upload;
        }
    }
}
