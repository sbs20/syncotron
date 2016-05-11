using System;

namespace Sbs20.Syncotron
{
    /// <summary>
    /// Deprecated
    /// </summary>
    internal class SyncActionChooserTwoWayByDate : ISyncActionChooser
    {
        private DateTime lastSync;

        public SyncActionChooserTwoWayByDate(DateTime lastSync)
        {
            this.lastSync = lastSync;
        }

        public FileActionType Choose(FileItemPair pair)
        {
            if (pair.Local != null)
            {
                if (pair.Local.IsFolder)
                {
                    Logger.debug(this, "Choose:" + pair.CommonPath + ":None");
                    return FileActionType.None;
                }
                else if (pair.Local.LastModified <= lastSync)
                {
                    // Local file unchanged
                    if (pair.Remote == null)
                    {
                        Logger.debug(this, "Choose:" + pair.CommonPath + ":DeleteLocal");
                        return FileActionType.DeleteLocal;
                    }
                    else if (pair.Remote.LastModified > lastSync)
                    {
                        Logger.debug(this, "Choose:" + pair.CommonPath + ":Download");
                        return FileActionType.Download;
                    }
                    else
                    {
                        // Remote file unchanged
                        Logger.debug(this, "Choose:" + pair.CommonPath + ":None");
                        return FileActionType.None;
                    }
                }
                else
                {
                    // Local file changed
                    if (pair.Remote == null)
                    {
                        Logger.debug(this, "Choose:" + pair.CommonPath + ":Upload");
                        return FileActionType.Upload;
                    }
                    else if (pair.Remote.LastModified <= lastSync)
                    {
                        Logger.debug(this, "Choose:" + pair.CommonPath + ":Upload");
                        return FileActionType.Upload;
                    }
                    else if (pair.Remote.LastModified > lastSync)
                    {
                        if (pair.IsSimilarEnough)
                        {
                            Logger.debug(this, "Choose:" + pair.CommonPath + ":None");
                            return FileActionType.None;
                        }

                        Logger.debug(this, "Choose:" + pair.CommonPath + ":ResolveConflict");
                        return FileActionType.ResolveConflict;
                    }
                }
            }
            else
            {
                if (pair.Remote.IsFolder)
                {
                    Logger.debug(this, "Choose:" + pair.CommonPath + ":Download");
                    return FileActionType.Download;
                }
                // If the remote file hasn't been touched...
                else if (pair.Remote.LastModified <= lastSync)
                {
                    Logger.debug(this, "Choose:" + pair.CommonPath + ":DeleteRemote");
                    return FileActionType.DeleteRemote;
                }
                else
                {
                    Logger.debug(this, "Choose:" + pair.CommonPath + ":Download");
                    return FileActionType.Download;
                }
            }

            Logger.debug(this, "Choose:" + pair.CommonPath + ":None");
            return FileActionType.None;
        }
    }
}
