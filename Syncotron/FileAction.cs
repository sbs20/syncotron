using System;
using System.Collections.Generic;

namespace Sbs20.Syncotron
{
    public class FileAction
    {
        public FileActionType Type { get; set; }
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
                    case FileActionType.DeleteLocal:
                    case FileActionType.DeleteRemote:
                        // This could be either depending on whether we've scanned or cursored
                        return this.FilePair.Local ?? this.FilePair.Remote;

                    case FileActionType.Upload:
                    case FileActionType.None:
                    case FileActionType.ResolveConflict:
                        return this.FilePair.Local;

                    case FileActionType.Download:
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

        private static IFileActionTypeChooser CreateFileActionChooser(ReplicatorContext context)
        {
            switch (context.ReplicationDirection)
            {
                case ReplicationDirection.TwoWay:
                    return new FileActionTypeChooserTwoWay(context.LastRun);

                case ReplicationDirection.MirrorDown:
                    return new ActionChooserMirrorDown();

                case ReplicationDirection.MirrorUp:
                    return new ActionChooserMirrorUp();

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
            IFileActionTypeChooser chooser = CreateFileActionChooser(matches.Context);

            foreach (var item in matches.FilePairs)
            {
                if (!MatchesExclusion(item.Key, matches.Context.Exclusions))
                {
                    FileActionType type = chooser.Choose(item.Value);
                    if (type != FileActionType.None)
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
