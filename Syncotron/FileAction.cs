using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class FileAction
    {
        public SyncActionType Type { get; set; }
        public FileItemPair FilePair { get; set; }
        public Exception Exception { get; set; }

        public FileAction()
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

        private static ISyncActionTypeChooser CreateFileActionChooser(ReplicatorContext context)
        {
            switch (context.ReplicationDirection)
            {
                case SyncDirection.TwoWay:
                    return new SyncActionTypeChooserTwoWay(context);

                case SyncDirection.MirrorDown:
                    return new SyncActionTypeChooserMirrorDown();

                case SyncDirection.MirrorUp:
                    return new SyncActionTypeChooserMirrorUp();

                default:
                    throw new NotImplementedException();
            }
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

        public static void AppendAll(Matcher matches, IList<FileAction> actions)
        {
            ISyncActionTypeChooser chooser = CreateFileActionChooser(matches.Context);

            foreach (var item in matches.FilePairs)
            {
                if (!MatchesExclusion(item.Key, matches.Context.Exclusions))
                {
                    SyncActionType type = chooser.Choose(item.Value);
                    if (type != SyncActionType.None)
                    {
                        actions.Add(new FileAction
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
