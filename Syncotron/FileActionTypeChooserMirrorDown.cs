using System;

namespace Sbs20.Syncotron
{
    public class FileActionTypeChooserMirrorDown : IFileActionTypeChooser
    {
        public FileActionType Choose(FileItemPair pair)
        {
            if (pair.Remote != null && pair.Local != null)
            {
                if (pair.Local.IsFolder || pair.IsSimilarEnough)
                {
                    Logger.debug(this, "Choose:" + pair.Path + ":None");
                    return FileActionType.None;
                }

                Logger.debug(this, "Choose:" + pair.Path + ":Download");
                return FileActionType.Download;
            }
            else if (pair.Remote != null && pair.Local == null)
            {
                Logger.debug(this, "Choose:" + pair.Path + ":Download");
                return FileActionType.Download;
            }
            else if (pair.Remote == null && pair.Local != null)
            {
                Logger.debug(this, "Choose:" + pair.Path + ":DeleteLocal");
                return FileActionType.DeleteLocal;
            }

            throw new InvalidOperationException(this.GetType().Name + ":Choose:InvalidPairState");
        }
    }
}
