using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
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
