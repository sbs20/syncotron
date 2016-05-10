using System;

namespace Sbs20.Syncotron
{
    public class FileActionTypeChooserMirrorUp : IFileActionTypeChooser
    {
        public FileActionType Choose(FileItemPair pair)
        {
            if (pair.Remote != null && pair.Local != null)
            {
                if (pair.IsSimilarEnough || pair.Local.IsFolder)
                {
                    Logger.debug(this, "Choose:" + pair.Path + ":None");
                    return FileActionType.None;
                }

                Logger.debug(this, "Choose:" + pair.Path + ":Upload");
                return FileActionType.Upload;
            }
            else if (pair.Remote != null && pair.Local == null)
            {
                Logger.debug(this, "Choose:" + pair.Path + ":DeleteRemote");
                return FileActionType.DeleteRemote;
            }
            else if (pair.Remote == null && pair.Local != null)
            {
                Logger.debug(this, "Choose:" + pair.Path + ":Upload");
                return FileActionType.Upload;
            }

            throw new InvalidOperationException(this.GetType().Name + ":Choose:InvalidPairState");
        }
    }
}
