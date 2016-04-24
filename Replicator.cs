using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Replicator
    {
        private IFileItemProvider localService;
        private ICloudService cloudService;
        private Matcher matcher;
        private IList<FileAction> actions;

        public event EventHandler<FileAction> ActionStart;
        public event EventHandler<FileAction> ActionComplete;
        public ReplicatorArgs Arguments { get; set; }
        public IList<Exception> Exceptions { get; private set; }

        public Replicator(ReplicatorArgs args)
        {
            this.Arguments = args;
            this.localService = new LocalFilesystemService();
            this.cloudService = new DropboxService(args);
            this.actions = new List<FileAction>();
            this.Exceptions = new List<Exception>();

            if (args.IgnoreCertificateErrors)
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

            try
            {
                switch (action.Type)
                {
                    case FileActionType.DeleteLocal:
                        await this.localService.DeleteAsync(action.FilePair.Local);
                        break;

                    case FileActionType.Download:
                        await this.cloudService.DownloadAsync(action.FilePair.Remote);
                        break;

                    case FileActionType.DeleteRemote:
                        await this.cloudService.DeleteAsync(action.FilePair.Remote);
                        break;

                    case FileActionType.Upload:
                        await this.cloudService.UploadAsync(action.FilePair.Local);
                        break;

                    case FileActionType.ResolveConflict:
                        await this.ResolveConflict(action.FilePair);
                        break;
                }
            }
            catch (Exception ex)
            {
                action.Exception = ex;
                this.Exceptions.Add(ex);
            }

            this.OnActionComplete(action);
        }

        private async Task MatchFilesAsync()
        {
            this.matcher = new Matcher(this.Arguments);
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
            foreach (var action in this.actions)
            {
                Task task = this.DoAction(action);
                tasks.Add(task);
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                task.ContinueWith(t => tasks.Remove(t));
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                if (this.Arguments.ProcessingMode == ProcessingMode.Serial || tasks.Count > this.Arguments.MaximumConcurrency)
                {
                    await task;
                }
            }

            await Task.WhenAll(tasks);
        }

        public async Task StartAsync()
        {
            // We always have to scan all files when we start up
            try
            {
                await this.MatchFilesAsync();

                // We always need to analyse what actions to do
                this.CreateActions();

                switch (this.Arguments.ReplicationType)
                {
                    case ReplicationType.AnalysisOnly:
                        break;

                    case ReplicationType.Snapshot:
                        await this.InvokeActionsAsync();
                        break;

                    case ReplicationType.Watcher:
                        throw new NotImplementedException();
                }
            }
            catch (Exception ex)
            {
                this.Exceptions.Add(ex);
            }
        }
    }
}
