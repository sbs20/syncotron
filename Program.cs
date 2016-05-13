using System;
using System.Threading.Tasks;
using Sbs20.Extensions;
using Sbs20.Syncotron;

namespace Sbs20
{
    class Program
    {
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
                Exclusions = { "*/.@__Thumb*", "*/.dropbox", "*/desktop.ini" },
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
                    var outputs = new ConsoleOutputs();

                    replicator.ActionStart += outputs.ActionStartHandler;
                    replicator.ActionComplete += outputs.ActionCompleteHandler;
                    replicator.Exception += outputs.ExceptionHandler;

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
                            Console.WriteLine(ex);
                            Console.ReadLine();
                        }
                    }

                    Console.Clear();

                    Task replicatorStart = replicator.StartAsync();

                    while (replicatorStart.Status != TaskStatus.RanToCompletion)
                    {
                        Task.Delay(200).Wait();
                        outputs.Draw(replicator);
                    }

                    replicatorStart.Wait();
                    outputs.Draw(replicator);
                }

                if (context.IsDebug)
                {
                    Console.WriteLine("Finished. Press <enter> to finish");
                    Console.ReadLine();
                }
            }
            catch (InvalidOperationException ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
    }
}
