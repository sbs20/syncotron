namespace Sbs20.Syncotron
{
    public enum FileService
    {
        Local,
        Dropbox
    }

    public enum SyncActionType
    {
        None,
        Download,
        Upload,
        DeleteLocal,
        DeleteRemote,
        KeepBoth
    }

    public enum CommandType
    {
        AnalysisOnly,
        Certify,
        Autosync,
        Fullsync,
        Reset
    }

    public enum SyncDirection
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
        DateTimeAndSize,
        MD5
    }

    public enum ConflictStrategy
    {
        None,
        RemoteWin,
        LocalWin,
        LatestWin,
        KeepBoth
    }

    public enum ScanMode
    {
        Full,
        Continue
    }
}
