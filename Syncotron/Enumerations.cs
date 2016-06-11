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
        CertifyStrict,
        CertifyLiberal,
        Autosync,
        Fullsync,
        Reset,
        Continue
    }

    public enum SyncDirection
    {
        MirrorDown,
        MirrorUp,
        TwoWay
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
