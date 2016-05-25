using log4net;
using Sbs20.Common;
using Sbs20.Extensions;
using Sbs20.Syncotron;
using System;
using System.Threading.Tasks;

namespace Sbs20
{
    class SyncotronMain
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SyncotronMain));

        private static ReplicatorContext ToReplicatorContext(string[] args)
        {
            var arguments = ConsoleHelper.ReadArguments(args);

            var context = new ReplicatorContext
            {
                RemoteService = FileService.Dropbox,
                LocalPath = arguments["LocalPath"],
                RemotePath = arguments["RemotePath"] ?? string.Empty,
                CommandType = CommandType.Autosync,
                SyncDirection = SyncDirection.TwoWay,
                ProcessingMode = ProcessingMode.Parallel,
                MaximumConcurrency = 3,
                HashProviderType = HashProviderType.DateTimeAndSize,
                Exclusions = { "*/.@__Thumb*", "*/.dropbox", "*/desktop.ini", "/Shared" },
                IgnoreCertificateErrors = true,
                IsDebug = false,
                ConflictStrategy = ConflictStrategy.RemoteWin,
                Recover = false
            };

            if (arguments.ContainsKey("CommandType"))
            {
                context.CommandType = arguments["CommandType"].ToEnum<CommandType>();
            }

            if (arguments.ContainsKey("SyncDirection"))
            {
                context.SyncDirection = arguments["SyncDirection"].ToEnum<SyncDirection>();
            }

            if (arguments.ContainsKey("RemoteService"))
            {
                context.RemoteService = arguments["RemoteService"].ToEnum<FileService>();
            }

            if (arguments.ContainsKey("HashProviderType"))
            {
                context.HashProviderType = arguments["HashProviderType"].ToEnum<HashProviderType>();
            }

            if (arguments.ContainsKey("ConflictStrategy"))
            {
                context.ConflictStrategy = arguments["ConflictStrategy"].ToEnum<ConflictStrategy>();
            }

            if (arguments.ContainsKey("IsDebug"))
            {
                context.IsDebug = true;
            }

            if (arguments.ContainsKey("Recover"))
            {
                context.Recover = true;
            }

            context.CleanAndPersist();

            return context;
        }

        static void Main(string[] args)
        {
            try
            {
                var context = ToReplicatorContext(args);

                using (Replicator replicator = new Replicator(context))
                {
                    log.Info("Starting syncotron");

                    replicator.ActionComplete += (s, a) =>
                    {
                        log.Info(a.ToString() + " [" + FileSizeFormatter.Format(a.PrimaryItem.Size) + "]");
                    };

                    replicator.AnalysisComplete += (s, e) =>
                    {
                        log.InfoFormat("Current user: {0}", replicator.Context.CloudService.CurrentAccountEmail);
                        log.InfoFormat("Distinct files: {0}", replicator.DistinctFileCount);
                        log.InfoFormat("Local files: {0}", replicator.LocalFileCount);
                        log.InfoFormat("Remote files: {0}", replicator.RemoteFileCount);
                        log.InfoFormat("Actions found: {0}", replicator.ActionCount);
                    };

                    replicator.Complete += (s, e) =>
                    {
                        log.InfoFormat("Actions completed: {0}", replicator.ActionsCompleteCount);
                        log.InfoFormat("Downloaded (mb): {0:0.00}", replicator.DownloadedMeg);
                        log.InfoFormat("Uploaded (mb): {0:0.00}", replicator.UploadedMeg);
                        log.InfoFormat("Duration: {0}", replicator.Duration);
                        log.InfoFormat("Download rate (mb/s): {0:0.00}", replicator.DownloadRate);
                        log.InfoFormat("Uploaded rate (mb/s): {0:0.00}", replicator.UploadRate);
                    };

                    replicator.Exception += (s, e) =>
                    {
                        log.Error(e);
                    };

                    while (!context.CloudService.IsAuthorised)
                    {
                        Uri url = context.CloudService.StartAuthorisation();
                        Console.WriteLine("You have not yet authorised syncotron with your cloud service");
                        Console.WriteLine("Please navigate here and log in");
                        Console.WriteLine();
                        Console.WriteLine(url);
                        Console.WriteLine();
                        Console.WriteLine("Then paste the result back in here and press <enter>");
                        string response = Console.ReadLine();
                        try
                        {
                            context.CloudService.FinishAuthorisation(response);
                        }
                        catch (Exception ex)
                        {
                            log.Error(ex);
                            return;
                        }
                    }

                    Task replicatorStart = replicator.StartAsync();

                    while (replicatorStart.Status != TaskStatus.RanToCompletion)
                    {
                        Task.Delay(500).Wait();
                    }

                    replicatorStart.Wait();
                }

                if (context.IsDebug)
                {
                    Console.WriteLine("Finished. Press <enter> to finish");
                    Console.ReadLine();
                }
            }
            catch (InvalidOperationException ex)
            {
                log.Error(ex);
            }
        }
    }
}
