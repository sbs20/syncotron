using System;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    class Program
    {
        // http://logicalgenetics.com/raspberry-pi-and-mono-hello-world/
        // http://www.mono-project.com/docs/getting-started/mono-basics/
        //  export MONO_IOMAP=all

        private static ReplicatorContext ToReplicatorContext(string[] args)
        {
            var arguments = ConsoleHelper.ReadArguments(args);

            var context = new ReplicatorContext
            {
                LastRun = DateTime.MinValue,
                LocalPath = arguments["LocalPath"],
                RemotePath = arguments["RemotePath"],
                CommandType = CommandType.Autosync,
                ReplicationDirection = SyncDirection.TwoWay,
                ProcessingMode = ProcessingMode.Parallel,
                HashProviderType = HashProviderType.DateTimeAndSize,
                Exclusions = { "*/.@__Thumb*" },
                MaximumConcurrency = 3,
                IgnoreCertificateErrors = true,
                IsDebug = true,
                ConflictStrategy = ConflictStrategy.RemoteWin
            };

            return context;
        }

        static void Main(string[] args)
        {
            var context = ToReplicatorContext(args);
            Replicator replicator = new Replicator(context);

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

            Console.WriteLine("Finished. Press <enter> to finish");
            Console.ReadLine();
        }
    }
}
