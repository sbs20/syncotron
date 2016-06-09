using log4net;
using System.Collections.Generic;
using System.Linq;

namespace Sbs20.Syncotron
{
    public class FileItemIndex
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FileItemIndex));
        private IDictionary<string, FileItem> index;

        public FileItemIndex(IEnumerable<FileItem> files)
        {
            log.Info("Build local index : start");
            this.index = files.ToDictionary(i => i.Path.ToLower(), i => i);
            log.Info("Build local index : finish");
        }

        public FileItem this[string path]
        {
            get
            {
                try
                {
                    return this.index[path.ToLower()];
                }
                catch
                {
                    return null;
                }
            }
        }
    }
}
