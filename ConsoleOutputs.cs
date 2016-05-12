using Sbs20.Syncotron;
using System;

namespace Sbs20
{
    public class ConsoleOutputs
    {
        private const double BytesPerMeg = 1 << 20;
        private int ConsoleX;
        private int ConsoleY;
        private Exception exception;

        public long ActionsCompleteCount { get; set; }
        public string CurrentAction { get; set; }
        public string LastAction { get; set; }
        public ulong DownloadedBytes { get; set; }
        public ulong UploadedBytes { get; set; }
        public DateTime Start { get; set; }

        public ConsoleOutputs()
        {
            this.Start = DateTime.Now;
            Console.Clear();
            this.ConsoleX = Console.CursorLeft;
            this.ConsoleY = Console.CursorTop;
        }

        public TimeSpan Duration
        {
            get { return DateTime.Now - this.Start; }
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
            get { return this.DownloadedMeg / (double)this.Duration.TotalSeconds; }
        }

        public double UploadRate
        {
            get { return this.UploadedMeg / (double)this.Duration.TotalSeconds; }
        }

        private static string ActionString(SyncAction action)
        {
            string type = action.Type.ToString();
            string name = action.PrimaryItem.Name;
            string path = action.CommonPath;
            string size = string.Format("{0:0.00}mb", action.PrimaryItem.Size / BytesPerMeg);
            string error = action.Exception != null ? action.Exception.Message : string.Empty;
            if (path.Length + type.Length + "Action: :".Length > Console.WindowWidth)
            {
                int len = Console.WindowWidth - "Action: :".Length - type.Length;
                path = "..." + path.Substring(path.Length - len);
            }

            string actionString = string.Format("{0}:{1} ({2}) {3}", type, name, size, error);
            actionString = actionString.PadRight(Console.WindowWidth);
            return actionString;
        }

        public void ActionStartHandler(object sender, SyncAction action)
        {
            this.CurrentAction = ActionString(action);
        }

        public void ActionCompleteHandler(object sender, SyncAction action)
        {
            this.LastAction = ActionString(action);
            if (action.Type == SyncActionType.Download)
            {
                this.DownloadedBytes += action.Remote.Size;
            }
            else if (action.Type == SyncActionType.Upload)
            {
                this.UploadedBytes += action.Local.Size;
            }

            this.ActionsCompleteCount++;
        }

        public void ExceptionHandler(object sender, Exception exception)
        {
            this.exception = exception;
        }

        private void WriteLine(string s, params object[] args)
        {
            string line = string.Format(s, args);
            if (line.Length > Console.WindowWidth)
            {
                line = line.Substring(0, Console.WindowWidth);
            }

            Console.WriteLine(line);
        }

        public void Draw(Replicator replicator)
        {
            Console.SetCursorPosition(this.ConsoleX, this.ConsoleY);
            this.WriteLine("Current user: {0}", replicator.Context.CloudService.CurrentAccountEmail);
            this.WriteLine("Distinct files: {0}", replicator.DistinctFileCount);
            this.WriteLine("Local files: {0}", replicator.LocalFileCount);
            this.WriteLine("Remote files: {0}", replicator.RemoteFileCount);
            this.WriteLine("Actions found: {0}", replicator.ActionCount);
            this.WriteLine("Current action: {0}", this.CurrentAction);
            this.WriteLine("Actions completed: {0}", this.ActionsCompleteCount);
            this.WriteLine("Last action: {0}", this.LastAction);
            this.WriteLine("Downloaded (mb): {0:0.00}", this.DownloadedMeg);
            this.WriteLine("Uploaded (mb): {0:0.00}", this.UploadedMeg);
            this.WriteLine("Duration: {0}", this.Duration);
            this.WriteLine("Download rate (mb/s): {0:0.00}", this.DownloadRate);
            this.WriteLine("Uploaded rate (mb/s): {0:0.00}", this.UploadRate);

            if (this.exception != null)
            {
                string message = replicator.Context.IsDebug ? this.exception.ToString() : this.exception.Message;
                Console.WriteLine(message);
            }
        }
    }
}
