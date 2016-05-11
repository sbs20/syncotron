using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Replicator
    {
        private Matcher matcher;
        private IList<FileAction> actions;

        public event EventHandler<FileAction> ActionStart;
        public event EventHandler<FileAction> ActionComplete;
        public event EventHandler<Exception> Exception;

        public ReplicatorContext Context { get; private set; }

        public Replicator(ReplicatorContext context)
        {
            this.Context = context;
            this.actions = new List<FileAction>();

            if (context.IgnoreCertificateErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
                {
                    return true;
                };
            }
        }

        private void OnActionStart(FileAction action)
        {
            if (this.ActionStart != null)
            {
                this.ActionStart(this, action);
            }
        }

        private void OnActionComplete(FileAction action)
        {
            if (this.ActionComplete != null)
            {
                this.ActionComplete(this, action);
            }
        }

        private void OnException(Exception exception)
        {
            if (this.Exception != null)
            {
                this.Exception(this, exception);
            }
        }

        public long DistinctFileCount
        {
            get { return this.matcher == null ? 0 : this.matcher.FilePairs.Count; }
        }

        public long RemoteFileCount
        {
            get { return this.matcher == null ? 0 : this.matcher.RemoteFileCount; }
        }

        public long LocalFileCount
        {
            get { return this.matcher == null ? 0 : this.matcher.LocalFileCount; }
        }

        public long ActionCount
        {
            get { return this.actions == null ? 0 : this.actions.Count(); }
        }

        private async Task ResolveConflict(FileItemPair pair)
        {
            IConflictResolver resolver = new ConflictResolverAggressive();
            await resolver.ResolveAsync(pair);
        }

        private async Task DoAction(FileAction action)
        {
            Logger.info(this, "DoAction(" + action.Key + ")");
            this.OnActionStart(action);
            string localPath = this.Context.ToLocalPath(action.FilePair.CommonPath);

            switch (action.Type)
            {
                case FileActionType.DeleteLocal:
                    await this.Context.LocalFilesystem.DeleteAsync(localPath);
                    this.Context.LocalStorage.FileDelete(localPath);
                    break;

                case FileActionType.Download:
                    await this.Context.CloudService.DownloadAsync(action.FilePair.Remote);
                    var item = this.Context.LocalFilesystem.ToFileItem(localPath);
                    item.ServerRev = action.FilePair.Remote.ServerRev;
                    this.Context.LocalStorage.IndexWrite(item);
                    break;

                case FileActionType.DeleteRemote:
                    string remotePath = this.Context.ToRemotePath(action.FilePair.CommonPath);
                    await this.Context.CloudService.DeleteAsync(remotePath);
                    this.Context.LocalStorage.FileDelete(localPath);
                    break;

                case FileActionType.Upload:
                    await this.Context.CloudService.UploadAsync(action.FilePair.Local);
                    this.Context.LocalStorage.IndexWrite(action.FilePair.Local);
                    break;

                case FileActionType.ResolveConflict:
                    await this.ResolveConflict(action.FilePair);
                    break;
            }

            this.OnActionComplete(action);
        }

        private async Task MatchFilesAsync()
        {
            if ((string.IsNullOrEmpty(this.Context.LocalCursor) && !string.IsNullOrEmpty(this.Context.RemoteCursor)) ||
                (!string.IsNullOrEmpty(this.Context.LocalCursor) && string.IsNullOrEmpty(this.Context.RemoteCursor)))
            {
                throw new InvalidOperationException("Cursors out of sync. Run with -reset");
            }


            this.matcher = new Matcher(this.Context);
            await this.matcher.ScanAsync();
        }

        private void CreateActions()
        {
            if (this.matcher == null)
            {
                throw new InvalidOperationException("Call ScanFiles() first");
            }

            FileAction.AppendAll(this.matcher, this.actions);

            this.actions = this.actions.OrderBy(a => a.Key).ToList();
        }

        private async Task InvokeActionsAsync()
        {
            List<Task> tasks = new List<Task>();
            bool abort = false;
            foreach (var action in this.actions)
            {
                Task task = this.DoAction(action);
                tasks.Add(task);

#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                task.ContinueWith(t => tasks.Remove(t), TaskContinuationOptions.OnlyOnRanToCompletion);
                task.ContinueWith(t => abort = true, TaskContinuationOptions.OnlyOnFaulted);
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed

                if (abort)
                {
                    break;
                }

                if (this.Context.ProcessingMode == ProcessingMode.Serial || tasks.Count > this.Context.MaximumConcurrency)
                {
                    await task;
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task StartAsync()
        {
            try
            {
                switch (this.Context.CommandType)
                {
                    case CommandType.Reset:
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = null;
                        this.Context.RemoteCursor = null;
                        break;

                    case CommandType.AnalysisOnly:
                        await this.MatchFilesAsync();
                        this.CreateActions();
                        break;

                    case CommandType.Certify:
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = null;
                        this.Context.RemoteCursor = null;
                        await this.MatchFilesAsync();
                        this.CreateActions();
                        this.Context.LocalFilesystem.Certify(this.matcher.FilePairs.Values);
                        this.Context.LocalStorage.SettingsWrite("IsCertified", true);
                        this.Context.LocalCursor = this.matcher.LocalCursor;
                        this.Context.RemoteCursor = this.matcher.RemoteCursor;
                        this.Context.LocalStorage.SettingsWrite("LastSync", DateTime.Now);
                        break;

                    case CommandType.Autosync:
                        await this.MatchFilesAsync();
                        this.CreateActions();
                        await this.InvokeActionsAsync();
                        this.Context.LocalStorage.SettingsWrite("IsCertified", true);
                        this.Context.LocalCursor = this.matcher.LocalCursor;
                        this.Context.RemoteCursor = this.matcher.RemoteCursor;
                        this.Context.LocalStorage.SettingsWrite("LastSync", DateTime.Now);
                        break;
                }
            }
            catch (Exception ex)
            {
                this.OnException(ex);
            }
        }
    }
}
