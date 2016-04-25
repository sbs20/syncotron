namespace Sbs20.Syncotron
{
    public enum FileService
    {
        Local,
        Dropbox
    }

    public enum FileActionType
    {
        None,
        Download,
        Upload,
        DeleteLocal,
        DeleteRemote,
        ResolveConflict
    }

    public enum ReplicationType
    {
        AnalysisOnly,
        Snapshot,
        Watcher
    }

    public enum ReplicationDirection
    {
        MirrorDown,
        MirrorUp,
        TwoWay
    }

    public enum ProcessingMode
    {
        Serial,
        Parallel
    }
}
