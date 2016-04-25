using System;
using System.Net;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    class Program
    {
        // http://logicalgenetics.com/raspberry-pi-and-mono-hello-world/
        // http://www.mono-project.com/docs/getting-started/mono-basics/
        //  export MONO_IOMAP=all

        private static ReplicatorArgs ToReplicatorArgs(string[] args)
        {
            var arguments = ConsoleHelper.ReadArguments(args);

            var replicatorArgs = new ReplicatorArgs
            {
                LastRun = DateTime.MinValue,
                LocalPath = arguments["LocalPath"],
                RemotePath = arguments["RemotePath"],
                ReplicationType = ReplicationType.Snapshot,
                ReplicationDirection = ReplicationDirection.MirrorDown,
                ProcessingMode = ProcessingMode.Parallel,
                Exclusions = { "*/.@__Thumb*" },
                MaximumConcurrency = 3,
                IgnoreCertificateErrors = true
            };

            return replicatorArgs;
        }

        static void Main(string[] args)
        {
            var replicatorArgs = ToReplicatorArgs(args);
            Replicator replicator = new Replicator(replicatorArgs);

            var outputs = new ConsoleOutputs();

            replicator.ActionStart += outputs.ActionStartHandler;
            replicator.ActionComplete += outputs.ActionCompleteHandler;

            Task replicatorStart = replicator.StartAsync();

            while (replicatorStart.Status != TaskStatus.RanToCompletion)
            {
                Task.Delay(200).Wait();
                outputs.Draw(replicator);
            }

            replicatorStart.Wait();
            outputs.Draw(replicator);
        }
    }
}
