namespace Sbs20.Syncotron
{
    public interface ISyncActionTypeChooser
    {
        SyncActionType Choose(FileItemPair pair);
    }
}
