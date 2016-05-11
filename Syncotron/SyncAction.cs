using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class SyncAction
    {
        public SyncActionType Type { get; set; }
        public FileItemPair FilePair { get; set; }
        public Exception Exception { get; set; }

        public SyncAction()
        {
        }

        public string Key
        {
            get { return this.FilePair.Key; }
        }

        public FileItem PrimaryItem
        {
            get
            {
                switch (this.Type)
                {
                    case SyncActionType.KeepBoth:
                    case SyncActionType.DeleteLocal:
                    case SyncActionType.DeleteRemote:
                        // This could be either depending on whether we've scanned or cursored
                        return this.FilePair.Local ?? this.FilePair.Remote;

                    case SyncActionType.Upload:
                    case SyncActionType.None:
                        return this.FilePair.Local;

                    case SyncActionType.Download:
                        return this.FilePair.Remote;

                    default:
                        throw new InvalidOperationException();
                }
            }
        }

        public override string ToString()
        {
            return this.Type.ToString() + ":" + this.Key;
        }

        private static bool MatchesExclusion(string key, IEnumerable<string> exclusions)
        {
            foreach (string exclusion in exclusions)
            {
                if (exclusion.StartsWith("*") && exclusion.EndsWith("*"))
                {
                    if (key.Contains(exclusion.Replace("*", "").ToLowerInvariant()))
                    {
                        return true;
                    }
                }
                else if (key.StartsWith(exclusion.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        public static void AppendAll(Matcher matches, IList<SyncAction> actions)
        {
            foreach (var item in matches.FilePairs)
            {
                if (!MatchesExclusion(item.Key, matches.Context.Exclusions))
                {
                    SyncActionType type = matches.Context.SyncActionTypeChooser.Choose(item.Value);
                    if (type != SyncActionType.None)
                    {
                        actions.Add(new SyncAction
                        {
                            Type = type,
                            FilePair = item.Value
                        });
                    }
                }
            }
        }
    }
}
