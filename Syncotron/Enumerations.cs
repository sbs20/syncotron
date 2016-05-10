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

    public enum CommandType
    {
        AnalysisOnly,
        Certify,
        Snapshot,
        Continue
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

    public enum HashProviderType
    {
        FileDateTimeAndSize,
        MD5
    }
}
