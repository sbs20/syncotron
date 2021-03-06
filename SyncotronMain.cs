﻿using log4net;
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
                HashProviderType = HashProviderType.DateTimeAndSize,
                Exclusions = { "*/.@__Thumb*", "*/.dropbox" },
                IgnoreCertificateErrors = false,
                IsDebug = false,
                ConflictStrategy = ConflictStrategy.RemoteWin,
                Recover = false,
                DataPath = null
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

            if (arguments.ContainsKey("IgnoreCertificateErrors"))
            {
                context.IgnoreCertificateErrors = true;
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

            if (arguments.ContainsKey("Exclusions"))
            {
                var items = arguments["Exclusions"].Split(':');
                foreach (var item in items)
                {
                    context.Exclusions.Add(item);
                }
            }

            if (arguments.ContainsKey("DataPath"))
            {
                context.DataPath = arguments["DataPath"];
            }

            return context;
        }

        private static DateTime LastProgressWrite = DateTime.MinValue;
        public static void TransferProgressWrite(string filepath, ulong size, ulong done, DateTime start)
        {
            if (DateTime.Now - LastProgressWrite > TimeSpan.FromSeconds(0.5) && !ReplicatorContext.IsRunningOnMono)
            {
                int left = Console.CursorLeft;
                int top = Console.CursorTop;

                TimeSpan duration = DateTime.Now.Subtract(start).Add(TimeSpan.FromMilliseconds(1));
                double rate = done / duration.TotalSeconds;
                ulong notDone = size - done;
                TimeSpan timeLeft = rate == 0 ? TimeSpan.FromHours(12) : TimeSpan.FromSeconds(notDone / rate);

                Console.WriteLine("{0} / {1} ({2:p}) {3:0.0}kb/sec [{4:g} left]              ",
                    done,
                    size,
                    (double)done / size,
                    rate / 1024,
                    timeLeft);

                // For some reason this isn't working in Mono - could be log4net or mono bug
                Console.CursorTop = top;
                Console.CursorLeft = left;
                LastProgressWrite = DateTime.Now;
            }
        }

        static void Main(string[] args)
        {
            try
            {
                ReplicatorContext context = null;

                try
                {
                    context = ToReplicatorContext(args);
                }
                catch
                {
                    Console.WriteLine("You must at least specify -LocalPath and -RemotePath arguments.");
                    Console.WriteLine("For more information please visit https://sbs20.github.io/syncotron/");
                    return;
                }

                GlobalContext.Properties["LogFilename"] = context.LogFileInfo.FullName;
                log4net.Config.XmlConfigurator.Configure();

                using (Replicator replicator = new Replicator(context))
                {
                    log.Info("=======================================================");
                    log.InfoFormat("Starting syncotron ({0})", replicator.Version);
                    log.InfoFormat("Current db: {0}", context.LocalStorageFileInfo.FullName);
                    log.InfoFormat("CommandType: {0}", context.CommandType.ToString());
                    log.InfoFormat("Direction: {0}", context.SyncDirection.ToString());
                    log.InfoFormat("ScanMode: {0}", context.ScanMode.ToString());

                    replicator.ActionStart += (s, a) =>
                    {
                        log.InfoFormat("{0} [{1}]", a.ToString(), FileSizeFormatter.Format(a.Size));
                    };

                    replicator.ActionComplete += (s, a) =>
                    {
                    };

                    replicator.AnalysisComplete += (s, e) =>
                    {
                        log.InfoFormat("Distinct files: {0}", replicator.DistinctFileCount);
                        log.InfoFormat("Local files: {0}", replicator.LocalFileCount);
                        log.InfoFormat("Remote files: {0}", replicator.RemoteFileCount);
                        log.InfoFormat("Actions found: {0}", replicator.ActionCount);
                    };

                    replicator.Finish += (s, e) =>
                    {
                        log.InfoFormat("Actions completed: {0} / {1}", replicator.ActionsCompleteCount, replicator.ActionCount);
                        log.InfoFormat("Download: {0:0.00}mb (rate: {1:0.00}mb/s)", replicator.DownloadedMeg, replicator.DownloadRate);
                        log.InfoFormat("Upload: {0:0.00}mb (rate {1:0.00}mb/s)", replicator.UploadedMeg, replicator.UploadRate);
                        log.InfoFormat("Duration: {0}", replicator.Duration);
                    };

                    replicator.Exception += (s, e) =>
                    {
                        if (e is TimeoutException)
                        {
                            log.Warn(e.Message);
                        }
                        else if (e is SyncotronException)
                        {
                            log.Error(e.Message);
                        }
                        else
                        {
                            log.Error(e);
                        }
                    };

                    if (!context.CloudService.IsAuthorised)
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

                    log.InfoFormat("Current user: {0}", replicator.Context.CloudService.CurrentAccountEmail);
                    log.InfoFormat("Local path: {0}", replicator.Context.LocalPath);
                    log.InfoFormat("Remote path: {0}", replicator.Context.RemotePath);

                    Task replicatorStart = replicator.StartAsync();
                    replicatorStart.Wait();
                }

                if (context.IsDebug)
                {
                    Console.WriteLine("Finished. Press <enter> to finish");
                    Console.ReadLine();
                }
            }
            catch (AnotherInstanceIsRunningException)
            {
                log.Info("Syncotron is already running");
            }
            catch (Exception ex)
            {
                // This is really the error handling of last resort
                log.Error(ex);
            }
        }
    }
}
