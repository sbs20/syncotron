namespace Sbs20.Syncotron
{
    public class FileItemPair
    {
        public string CommonPath { get; set; }
        public string LocalPath { get; set; }
        public string RemotePath { get; set; }

        public FileItem Local { get; set; }
        public FileItem Remote { get; set; }

        public FileItemPair()
        {
        }

        public string Key
        {
            get { return this.CommonPath.ToLowerInvariant(); }
        }

        private bool IsSameSize
        {
            get { return this.Local.Size == this.Remote.Size; }
        }

        private bool IsLastModifiedSimilarEnough
        {
            get
            {
                // Filetime seems to be in exact seconds
                long ticksPerSecond = 10000000;
                var localTicks = this.Local.LastModified.Ticks / ticksPerSecond;
                var remoteTicks = this.Remote.ClientModified.Ticks / ticksPerSecond;
                return localTicks == remoteTicks;
            }
        }

        public bool IsSimilarEnough
        {
            get { return this.IsSameSize && this.IsLastModifiedSimilarEnough; }
        }
    }
}
