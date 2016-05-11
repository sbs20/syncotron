using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Replicator
    {
        private SyncActionListBuilder syncActionsBuilder;
        private IList<SyncAction> actions;

        public event EventHandler<SyncAction> ActionStart;
        public event EventHandler<SyncAction> ActionComplete;
        public event EventHandler<Exception> Exception;

        public ReplicatorContext Context { get; private set; }

        public Replicator(ReplicatorContext context)
        {
            this.Context = context;
            this.actions = new List<SyncAction>();

            if (context.IgnoreCertificateErrors)
            {
                ServicePointManager.ServerCertificateValidationCallback = (sender, cert, chain, errors) =>
                {
                    return true;
                };
            }
        }

        private void OnActionStart(SyncAction action)
        {
            if (this.ActionStart != null)
            {
                this.ActionStart(this, action);
            }
        }

        private void OnActionComplete(SyncAction action)
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
            get { return this.syncActionsBuilder == null ? 0 : this.syncActionsBuilder.ActionDictionary.Count; }
        }

        public long RemoteFileCount
        {
            get { return this.syncActionsBuilder == null ? 0 : this.syncActionsBuilder.RemoteFileCount; }
        }

        public long LocalFileCount
        {
            get { return this.syncActionsBuilder == null ? 0 : this.syncActionsBuilder.LocalFileCount; }
        }

        public long ActionCount
        {
            get { return this.actions == null ? 0 : this.actions.Count(); }
        }

        private async Task DoAction(SyncAction action)
        {
            Logger.info(this, "DoAction(" + action.Key + ")");
            this.OnActionStart(action);

            switch (action.Type)
            {
                case SyncActionType.DeleteLocal:
                    await this.Context.LocalFilesystem.DeleteAsync(action.LocalPath);
                    this.Context.LocalStorage.FileDelete(action.LocalPath);
                    break;

                case SyncActionType.Download:
                    await this.Context.CloudService.DownloadAsync(action.Remote);
                    var item = this.Context.LocalFilesystem.ToFileItem(action.LocalPath);
                    item.ServerRev = action.Remote.ServerRev;
                    this.Context.LocalStorage.IndexWrite(item);
                    break;

                case SyncActionType.DeleteRemote:
                    await this.Context.CloudService.DeleteAsync(action.RemotePath);
                    this.Context.LocalStorage.FileDelete(action.LocalPath);
                    break;

                case SyncActionType.Upload:
                    await this.Context.CloudService.UploadAsync(action.Local);
                    this.Context.LocalStorage.IndexWrite(action.Local);
                    break;

                default:
                    throw new NotImplementedException();
            }

            this.OnActionComplete(action);
        }

        private async Task PopulateActionsListAsync()
        {
            if ((string.IsNullOrEmpty(this.Context.LocalCursor) && !string.IsNullOrEmpty(this.Context.RemoteCursor)) ||
                (!string.IsNullOrEmpty(this.Context.LocalCursor) && string.IsNullOrEmpty(this.Context.RemoteCursor)))
            {
                // Cursors out of sync. Auto reset. No point in forcing the user to do it
                Logger.info(this, "Cursors out of sync. Resetting.");
                this.Reset();
            }

            this.syncActionsBuilder = new SyncActionListBuilder(this.Context);
            await this.syncActionsBuilder.BuildAsync();
            this.actions = this.syncActionsBuilder.Actions;
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

        private void Reset()
        {
            this.Context.LocalStorage.SettingsWrite("IsCertified", false);
            this.Context.LocalCursor = null;
            this.Context.RemoteCursor = null;
        }

        public async Task StartAsync()
        {
            try
            {
                switch (this.Context.CommandType)
                {
                    case CommandType.Reset:
                        this.Reset();
                        break;

                    case CommandType.AnalysisOnly:
                        await this.PopulateActionsListAsync();
                        break;

                    case CommandType.Certify:
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = null;
                        this.Context.RemoteCursor = null;
                        await this.PopulateActionsListAsync();
                        this.Context.LocalFilesystem.Certify(this.syncActionsBuilder.ActionDictionary.Values);
                        this.Context.LocalStorage.SettingsWrite("IsCertified", true);
                        this.Context.LocalCursor = this.syncActionsBuilder.LocalCursor;
                        this.Context.RemoteCursor = this.syncActionsBuilder.RemoteCursor;
                        this.Context.LocalStorage.SettingsWrite("LastSync", DateTime.Now);
                        break;

                    case CommandType.Autosync:
                        await this.PopulateActionsListAsync();
                        await this.InvokeActionsAsync();
                        this.Context.LocalStorage.SettingsWrite("IsCertified", true);
                        this.Context.LocalCursor = this.syncActionsBuilder.LocalCursor;
                        this.Context.RemoteCursor = this.syncActionsBuilder.RemoteCursor;
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
