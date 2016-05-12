using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class SyncActionListBuilder
    {
        private object mutex = new object();

        public long LocalFileCount { get; set; }
        public long RemoteFileCount { get; set; }
        public ReplicatorContext Context { get; private set; }
        public Dictionary<string, SyncAction> ActionDictionary { get; private set; }
        public IList<SyncAction> Actions { get; private set; }

        public string RemoteCursor { get; private set; }
        public string LocalCursor { get; private set; }

        public SyncActionListBuilder(ReplicatorContext context)
        {
            this.Context = context;
            this.ActionDictionary = new Dictionary<string, SyncAction>();
            this.Actions = new List<SyncAction>();
        }

        private string Key(FileItem file)
        {
            return this.Context.ToCommonPath(file).ToLowerInvariant();
        }

        private void Add(FileItem file)
        {
            lock(mutex)
            {
                SyncAction action = null;

                if (this.ActionDictionary.ContainsKey(this.Key(file)))
                {
                    action = this.ActionDictionary[this.Key(file)];
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
                    action.Local = file;
                    this.LocalFileCount++;
                }
                else
                {
                    action.Remote = file;
                    this.RemoteFileCount++;
                }
            }
        }

        private async Task ScanRemoteAsync()
        {
            IFileItemProvider cloudService = new DropboxService(this.Context);
            if (string.IsNullOrEmpty(this.Context.RemoteCursor))
            {
                this.RemoteCursor = await cloudService.ForEachAsync(this.Context.RemotePath, true, false, (item) => this.Add(item));
            }
            else if (this.Context.SyncDirection != SyncDirection.MirrorUp)
            {
                this.RemoteCursor = await cloudService.ForEachContinueAsync(this.Context.RemoteCursor, (item) => this.Add(item));
            }
            else
            {
                this.RemoteCursor = await cloudService.LatestCursor(this.Context.RemotePath, true, true);
            }
        }

        private async Task ScanLocalAsync()
        {
            if (string.IsNullOrEmpty(this.Context.LocalCursor))
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachAsync(this.Context.LocalPath, true, true, (item) => this.Add(item));
            }
            else if (this.Context.SyncDirection != SyncDirection.MirrorDown)
            {
                this.LocalCursor = await this.Context.LocalFilesystem.ForEachContinueAsync(this.Context.LocalCursor, (item) => this.Add(item));
            }
            else
            {
                this.LocalCursor = await this.Context.LocalFilesystem.LatestCursor(this.Context.LocalPath, true, true);
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

        public async Task BuildAsync()
        {
            var local = this.ScanLocalAsync();
            var remote = this.ScanRemoteAsync();
            await local;
            await remote;

            foreach (var action in this.ActionDictionary.Values)
            {
                if (!MatchesExclusion(action.Key, this.Context.Exclusions))
                {
                    SyncActionType type = this.Context.SyncActionTypeChooser.Choose(action);
                    if (type != SyncActionType.None)
                    {
                        action.Type = type;
                        this.Actions.Add(action);
                    }
                }
            }

            this.Actions = this.Actions.OrderBy(a => a.Key).ToList();
        }
    }
}
