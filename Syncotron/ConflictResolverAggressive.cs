using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    class ConflictResolverAggressive : IConflictResolver
    {
        public async Task ResolveAsync(FileItemPair filePair)
        {
            // We have two "new" versions of a file. We have to take a brute force approach
            Logger.info(this, "resolveConflict(" + filePair.CommonPath + ")");

            await Task.Delay(1);
            // We're going to download an alternate version : <filename>.conflict
            //string tempFilepath = "";

            // Download the server version
            //await cloudService.download(filePair.Remote, tempFilepath);

            // Compare the files
            //FileSystemService fileSystemService = FileSystemService.getInstance();
            //java.io.File tempFile = fileSystemService.getFile(tempFilepath);
            //java.io.File localFile = (java.io.File)filePair.local.getFile();
            //boolean filesEqual = fileSystemService.filesEqual(localFile, tempFile);

            // Act
            //    if (filesEqual) {

            //        Logger.info(this, "resolveConflict(" + filePair.key() + "):files equal");

            //        // Everything is good. Just delete the tempfile
            //        fileSystemService.delete(tempFilepath);

            //    } else {

            //        Logger.info(this, "resolveConflict(" + filePair.key() + "):files not equal");

            //// Rename the server version to the conflict
            //String serverConflictPath = filePair.remote.getPath() +
            //                ServiceManager.getInstance().string(R.string.replication_conflict_extension);

            //cloudService.move(filePair.remote, serverConflictPath);

            //// Upload the local version
            //cloudService.upload(filePair.local);
            //}
        }
    }
}
