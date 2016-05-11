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
        KeepBoth
    }

    public enum CommandType
    {
        AnalysisOnly,
        Certify,
        Autosync,
        Reset
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

    public enum ConflictStrategy
    {
        None,
        RemoteWin,
        LocalWin,
        LatestWin,
        KeepBoth
    }
}
