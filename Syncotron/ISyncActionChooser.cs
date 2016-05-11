namespace Sbs20.Syncotron
{
    interface ISyncActionChooser
    {
        SyncActionType Choose(FileItemPair pair);
    }
}
