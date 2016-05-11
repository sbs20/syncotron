namespace Sbs20.Syncotron
{
    interface ISyncActionChooser
    {
        FileActionType Choose(FileItemPair pair);
    }
}
