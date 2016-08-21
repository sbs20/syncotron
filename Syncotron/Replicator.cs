using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class Replicator : IDisposable
    {
        private const double BytesPerMeg = 1 << 20;

        private static readonly ILog log = LogManager.GetLogger(typeof(Replicator));
        private SyncActionListBuilder syncActionsBuilder;
        private IList<SyncAction> actions;

        public long ActionsCompleteCount { get; private set; }
        public ulong DownloadedBytes { get; private set; }
        public ulong UploadedBytes { get; private set; }
        public DateTime Start { get; private set; }
        public DateTime TransferStart { get; private set; }

        public event EventHandler<SyncAction> ActionStart;
        public event EventHandler<SyncAction> ActionComplete;
        public event EventHandler<Exception> Exception;
        public event EventHandler<EventArgs> AnalysisComplete;
        public event EventHandler<EventArgs> Finish;

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

            if (context.IsRunningOnMono)
            {
                Environment.SetEnvironmentVariable("MONO_IOMAP", "all");
            }

            if (!context.Recover && context.IsRunning)
            {
                throw new AnotherInstanceIsRunningException();
            }

            context.IsRunning = true;

            context.Persist();
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
            this.ActionsCompleteCount++;

            if (action.Type != SyncActionType.None)
            {
                if (action.Type == SyncActionType.Download)
                {
                    this.DownloadedBytes += action.Size;
                }
                else if (action.Type == SyncActionType.Upload)
                {
                    this.UploadedBytes += action.Size;
                }

                if (this.ActionComplete != null)
                {
                    this.ActionComplete(this, action);
                }
            }
        }

        private void OnException(Exception exception)
        {
            if (this.Exception != null)
            {
                this.Exception(this, exception);
            }
        }

        private void OnAnalysisComplete()
        {
            if (this.AnalysisComplete!= null)
            {
                this.AnalysisComplete(this, null);
            }
        }

        private void OnFinish()
        {
            if (this.Finish != null)
            {
                this.Finish(this, null);
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

        public TimeSpan Duration
        {
            get { return DateTime.Now - this.Start; }
        }

        public TimeSpan TransferDuration
        {
            get { return DateTime.Now - this.TransferStart; }
        }

        public double DownloadedMeg
        {
            get { return this.DownloadedBytes / BytesPerMeg; }
        }

        public double UploadedMeg
        {
            get { return this.UploadedBytes / BytesPerMeg; }
        }

        public double DownloadRate
        {
            get { return this.DownloadedMeg / this.TransferDuration.TotalSeconds; }
        }

        public double UploadRate
        {
            get { return this.UploadedMeg / this.TransferDuration.TotalSeconds; }
        }

        private async Task DoActionAsync(SyncAction action)
        {
            log.Debug("DoAction(" + action.Key + ")");

            if (action.IsUnconstructed)
            {
                await action.Reconstruct(this.Context);
            }

            this.OnActionStart(action);

            switch (action.Type)
            {
                case SyncActionType.DeleteLocal:
                    await this.Context.LocalFilesystem.DeleteAsync(action.LocalPath);
                    this.Context.LocalStorage.IndexDelete(action.LocalPath);
                    break;

                case SyncActionType.Download:
                    if (action.Remote != null)
                    {
                        await this.Context.CloudService.DownloadAsync(action.Remote);
                        var item = this.Context.LocalFilesystem.ToFileItem(action.LocalPath);
                        if (item != null)
                        {
                            item.ServerRev = action.Remote.ServerRev;
                            this.Context.LocalStorage.IndexWrite(item);
                        }
                    }
                    else
                    {
                        log.WarnFormat("Remote file ({0}) no longer exists. Ignoring.", action.RemotePath);
                    }

                    break;

                case SyncActionType.DeleteRemote:
                    await this.Context.CloudService.DeleteAsync(action.RemotePath);
                    this.Context.LocalStorage.IndexDelete(action.LocalPath);
                    break;

                case SyncActionType.Upload:
                    if (action.Local != null)
                    {
                        await this.Context.CloudService.UploadAsync(action.Local);
                        this.Context.LocalStorage.IndexWrite(action.Local);
                    }
                    else
                    {
                        log.WarnFormat("Local file ({0}) no longer exists. Ignoring.", action.LocalPath);
                        this.Context.LocalStorage.IndexDelete(action.LocalPath);
                    }

                    break;

                default:
                    throw new NotImplementedException();
            }

            this.Context.LocalStorage.ActionDelete(action);
            this.OnActionComplete(action);
        }

        private async Task PopulateActionsListAsync()
        {
            this.syncActionsBuilder = new SyncActionListBuilder(this.Context);

            TimerCallback callback = (state) =>
            {
                log.InfoFormat("Scanned {0} local files; {1} remote files", this.LocalFileCount, this.RemoteFileCount);
            };

            using (Timer timer = new Timer(callback, null, TimeSpan.FromSeconds(0), TimeSpan.FromSeconds(10)))
            {
                await this.syncActionsBuilder.BuildAsync();
            }

            this.actions = this.syncActionsBuilder.Actions;
        }

        private async Task ProcessActionsAsync()
        {
            this.TransferStart = DateTime.Now;

            foreach (var action in this.actions)
            {
                await this.DoActionAsync(action);
            }
        }

        public async Task StartAsync()
        {
            try
            {
                this.Start = DateTime.Now;
                this.TransferStart = DateTime.Now;

                switch (this.Context.CommandType)
                {
                    case CommandType.Reset:
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = null;
                        this.Context.RemoteCursor = null;
                        break;

                    case CommandType.Continue:
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = await this.Context.LocalFilesystem.LatestCursorAsync(this.Context.LocalPath, true, true);
                        this.Context.RemoteCursor = await this.Context.CloudService.LatestCursorAsync(this.Context.RemotePath, true, true);
                        this.Context.LocalStorage.SettingsWrite("LastSync", DateTime.Now);
                        break;

                    case CommandType.AnalysisOnly:
                        await this.PopulateActionsListAsync();
                        this.OnAnalysisComplete();
                        break;

                    case CommandType.CertifyLiberal:
                    case CommandType.CertifyStrict:
                        bool isStrict = this.Context.CommandType == CommandType.CertifyStrict;
                        this.Context.LocalStorage.SettingsWrite("IsCertified", false);
                        this.Context.LocalCursor = null;
                        this.Context.RemoteCursor = null;
                        await this.PopulateActionsListAsync();
                        this.OnAnalysisComplete();
                        this.Context.LocalFilesystem.Certify(this.syncActionsBuilder.ActionDictionary.Values, isStrict);
                        this.Context.LocalStorage.SettingsWrite("IsCertified", true);

                        if (this.Context.CommandType == CommandType.CertifyStrict)
                        {
                            this.Context.LocalCursor = this.syncActionsBuilder.LocalCursor;
                            this.Context.RemoteCursor = this.syncActionsBuilder.RemoteCursor;
                            this.Context.LocalStorage.SettingsWrite("LastSync", DateTime.Now);
                        }

                        break;

                    case CommandType.Fullsync:
                    case CommandType.Autosync:
                        await this.PopulateActionsListAsync();
                        this.OnAnalysisComplete();
                        await this.ProcessActionsAsync();
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

            this.OnFinish();
        }

        public void Dispose()
        {
            this.Context.IsRunning = false;
        }

        public string Version
        {
            get { return Assembly.GetExecutingAssembly().GetName().Version.ToString(); }
        }
    }
}
