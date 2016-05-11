namespace Sbs20.Syncotron
{
    interface ISyncActionTypeChooser
    {
        SyncActionType Choose(FileItemPair pair);
    }
}
