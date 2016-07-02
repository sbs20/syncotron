using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Sbs20.Extensions;

namespace Sbs20.Syncotron
{
    public class SyncActionListBuilder
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(Replicator));

        private object mutex = new object();

        public long LocalFileCount { get; set; }
        public long RemoteFileCount { get; set; }
        public ReplicatorContext Context { get; private set; }
        public Dictionary<string, SyncAction> ActionDictionary { get; private set; }
        public IList<SyncAction> Actions { get; private set; }
        public ISyncActionTypeChooser TypeChooser { get; private set; }

        public string RemoteCursor { get; private set; }
        public string LocalCursor { get; private set; }

        public SyncActionListBuilder(ReplicatorContext context)
        {
            this.Context = context;
            this.ActionDictionary = new Dictionary<string, SyncAction>();
            this.Actions = new List<SyncAction>();
            this.TypeChooser = this.CreateSyncActionTypeChooser();
        }

        private ISyncActionTypeChooser CreateSyncActionTypeChooser()
        {
            switch (this.Context.SyncDirection)
            {
                case SyncDirection.TwoWay:
                    return new SyncActionTypeChooserTwoWay(this.Context);

                case SyncDirection.MirrorDown:
                    return new SyncActionTypeChooserMirrorDown();

                case SyncDirection.MirrorUp:
                    return new SyncActionTypeChooserMirrorUp();

                default:
                    throw new NotImplementedException();
            }
        }

        private string Key(FileItem file)
        {
            return this.Context.ToCommonPath(file).ToLowerInvariant();
        }

        private bool IsExclusionMatch(string key)
        {
            foreach (string exclusion in this.Context.Exclusions)
            {
                if (exclusion.StartsWith("*") && exclusion.EndsWith("*"))
                {
                    if (key.Contains(exclusion.Replace("*", "").ToLowerInvariant()))
                    {
                        return true;
                    }
                }
                else if (exclusion.StartsWith("*") && key.EndsWith(exclusion.Replace("*", "")))
                {
                    return true;
                }
                else if (key.StartsWith(exclusion.ToLowerInvariant()))
                {
                    return true;
                }
            }

            return false;
        }

        private void Add(FileItem file)
        {
            string key = this.Key(file);
            if (this.IsExclusionMatch(key))
            {
                return;
            }

            lock (mutex)
            {
                SyncAction action = null;

                if (this.ActionDictionary.ContainsKey(key))
                {
                    action = this.ActionDictionary[key];
                }
                else
                {
                    action = new SyncAction
                    {
                        CommonPath = this.Context.ToCommonPath(file),
                        LocalPath = this.Context.ToLocalPath(file),
                        RemotePath = this.Context.ToRemotePath(file)
                    };

                    this.ActionDictionary[action.Key] = action;
                }

                if (file.Source == FileService.Local)
                {
                    if (action.Local != null)
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot add local file twice: {0}", action.LocalPath));
                    }

                    action.Local = file;
                    this.LocalFileCount++;
                }
                else
                {
                    if (action.Remote != null)
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot add remote file twice: {0}", action.RemotePath));
                    }

                    action.Remote = file;
                    this.RemoteFileCount++;
                }
            }
        }

        private async Task ScanRemoteAsync()
        {
            IFileItemProvider cloudService = this.Context.CloudService;
            if (this.Context.ScanMode == ScanMode.Full)
            {
                this.RemoteCursor = await cloudService.ForEachAsync(this.Context.RemotePath, true, false, (item) => this.Add(item));
            }
            else if (this.Context.SyncDirection != SyncDirection.MirrorUp)
            {
                this.RemoteCursor = await cloudService.ForEachContinueAsync(this.Context.RemoteCursor, (item) => this.Add(item));
            }
            else
            {
                this.RemoteCursor = await cloudService.LatestCursorAsync(this.Context.RemotePath, true, true);
            }
        }

        private async Task ScanLocalAsync()
        {
            if (this.Context.ScanMode == ScanMode.Full)
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachAsync(this.Context.LocalPath, true, true, (item) => this.Add(item));
            }
            else if (this.Context.SyncDirection != SyncDirection.MirrorDown)
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachContinueAsync(this.Context.LocalCursor, (item) => this.Add(item));
            }
            else
            {
                this.LocalCursor = await this.Context.LocalFilesystem.LatestCursorAsync(this.Context.LocalPath, true, true);
            }
        }

        private async Task FurtherScanForContinuation()
        {
            foreach (var action in this.ActionDictionary.Values)
            {
                if (action.Local == null)
                {
                    action.Local = await this.Context.LocalFilesystem.FileSelectAsync(action.LocalPath);
                    this.LocalFileCount++;
                }
                else if (action.Remote == null)
                {
                    action.Remote = await this.Context.CloudService.FileSelectAsync(action.RemotePath);
                    this.RemoteFileCount++;
                }
            }
        }

        private static bool DirectoryIsEmptyOrDoesNotExist(string path)
        {
            if (!System.IO.Directory.Exists(path))
            {
                return true;
            }

            if (!System.IO.Directory.EnumerateFileSystemEntries(path).Any())
            {
                return true;
            }

            return false;
        }

        public async Task BuildAsync()
        {
            if (!string.IsNullOrEmpty(this.Context.LocalCursor) &&
                DirectoryIsEmptyOrDoesNotExist(this.Context.LocalPath))
            {
                // We have a local cursor which means we've run before. And yet the local
                // directory doesn't exist. Depending on our sync direction this either means
                // deleting everything remotely or re-downloading everything. This is sufficiently
                // bad that we will defer to the user. If this is deliberate then the user can 
                // either delete this job, manually delete remotely or reset. If not, perhaps
                // it's a missing UNC share, then we've just saved the data.
                throw new InvalidOperationException(
                    "LocalPath is empty or does not exist, but this job has previously run. Aborting to protect data.");
            }

            // Do we already have stored actions?
            var previous = this.Context.LocalStorage.ActionSelect();
            if (previous.Count() > 0)
            {
                foreach (var action in previous)
                {
                    this.Actions.Add(action);
                }

                this.LocalCursor = this.Context.LocalStorage.SettingsRead<string>("PendingLocalCursor");
                this.RemoteCursor = this.Context.LocalStorage.SettingsRead<string>("PendingRemoteCursor");
                return;
            }

            // Now scan for new ones
            var local = this.ScanLocalAsync();
            var remote = this.ScanRemoteAsync();
            await local;
            await remote;

            if (this.Context.ScanMode == ScanMode.Continue)
            {
                // There's a minor expense in doing a further scan of files but it saves time
                // when a previous job has failed (e.g. timeout) and avoids re-transferring
                // files which already exist
                await this.FurtherScanForContinuation();
            }

            var actions = new List<SyncAction>();
            foreach (var action in this.ActionDictionary.Values)
            {
                SyncActionType type = this.TypeChooser.Choose(action);
                if (type != SyncActionType.None)
                {
                    action.Type = type;
                    actions.Add(action);
                }
            }

            this.Actions.Add(actions.Where(a => a.Type == SyncActionType.DeleteLocal).OrderByDescending(a => a.Key.Length));
            this.Actions.Add(actions.Where(a => a.Type == SyncActionType.DeleteRemote).OrderByDescending(a => a.Key.Length));
            this.Actions.Add(actions.Where(a => a.Type == SyncActionType.Download).OrderBy(a => a.Key.Length));
            this.Actions.Add(actions.Where(a => a.Type == SyncActionType.Upload).OrderBy(a => a.Key.Length));
            this.Actions.Add(actions.Where(a => a.Type == SyncActionType.KeepBoth));

            this.Context.LocalStorage.BeginTransaction();
            foreach (var action in this.Actions)
            {
                this.Context.LocalStorage.ActionWrite(action);
            }

            this.Context.LocalStorage.EndTransaction();
            this.Context.LocalStorage.SettingsWrite("PendingLocalCursor", this.LocalCursor);
            this.Context.LocalStorage.SettingsWrite("PendingRemoteCursor", this.RemoteCursor);
        }
    }
}
