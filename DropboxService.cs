using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class DropboxService : ICloudService
    {
        private static DropboxClient __client = null;
        private ReplicatorArgs replicatorArgs = null;

        public static Uri StartAuthorisation()
        {
            string clientId = Properties.Settings.Default.Dropbox_ClientId;

            // The user should go to this URI, login and get a code... and enter that into FinishAuthorisation
            return DropboxOAuth2Helper.GetAuthorizeUri(clientId);
        }

        public static async Task FinishAuthorisation(string code)
        {
            string clientId = Properties.Settings.Default.Dropbox_ClientId;
            string secret = Properties.Settings.Default.Dropbox_Secret;
            OAuth2Response oauthResponse = await DropboxOAuth2Helper.ProcessCodeFlowAsync(code, clientId, secret);
            string accessToken = oauthResponse.AccessToken;
            Properties.Settings.Default.Dropbox_AccessToken = accessToken;
        }

        public static DropboxClient Client()
        {
            if (__client == null)
            {
                string userAgent = "Sbs20.Syncotron";

                var config = new DropboxClientConfig
                {
                    UserAgent = userAgent
                };

                string accessToken = Properties.Settings.Default.Dropbox_AccessToken;
                if (string.IsNullOrEmpty(accessToken))
                {
                    throw new InvalidOperationException("Invalid access token");
                }

                __client = new DropboxClient(accessToken, config);
            }

            return __client;
        }

        public DropboxService(ReplicatorArgs definition)
        {
            this.replicatorArgs = definition;
        }

        public async Task ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            Action<Metadata> handleEntry = (entry) =>
            {
                var item = FileItem.Create(entry);
                if (item.Path != this.replicatorArgs.RemotePath)
                {
                    action(item);
                }
            };

            var client = Client();

            ListFolderResult result = await client.Files.ListFolderAsync(
                new ListFolderArg(path, recursive, false, deleted));

            foreach (var entry in result.Entries)
            {
                handleEntry(entry);
            }

            while (result.HasMore)
            {
                result = await client.Files.ListFolderContinueAsync(new ListFolderContinueArg(result.Cursor));

                foreach (var entry in result.Entries)
                {
                    handleEntry(entry);
                }
            }
        }

        public async Task MoveAsync(FileItem file, string desiredPath)
        {
            Logger.info(this, "move():Start");
            FileMetadata remoteFile = (FileMetadata)file.Object;

            if (remoteFile != null)
            {
                var client = Client();
                await client.Files.MoveAsync(remoteFile.PathLower, desiredPath);
                Logger.verbose(this, "move():done");
            }
        }

        public async Task UploadAsync(FileItem file)
        {
            Logger.info(this, "upload():Start");
            FileInfo localFile = (FileInfo)file.Object;

            if (localFile != null)
            {
                // Note - this is not ensuring the name is a valid dropbox file name
                string remoteFileName = this.replicatorArgs.ToRemotePath(file.Path);

                var client = Client();
                CommitInfo commitInfo = new CommitInfo(
                    remoteFileName,
                    WriteMode.Overwrite.Instance,
                    false,
                    file.LastModified);

                if (file.Size > 1 << 20)
                {
                    // Use chunked upload for larger files
                    using (Stream stream = localFile.OpenRead())
                    {
                        int chunkSize = 1024 * 128;
                        int numChunks = (int)Math.Ceiling((double)file.Size / chunkSize);

                        byte[] buffer = new byte[chunkSize];
                        string sessionId = null;

                        for (var index = 0; index < numChunks; index++)
                        {
                            var read = await stream.ReadAsync(buffer, 0, chunkSize);

                            using (MemoryStream memoryStream = new MemoryStream(buffer, 0, read))
                            {
                                if (index == 0)
                                {
                                    var result = await client.Files.UploadSessionStartAsync(memoryStream);
                                    sessionId = result.SessionId;
                                }
                                else
                                {
                                    UploadSessionCursor cursor = new UploadSessionCursor(sessionId, (ulong)(chunkSize * index));

                                    if (index == numChunks - 1)
                                    {
                                        await client.Files.UploadSessionFinishAsync(cursor, commitInfo, memoryStream);
                                    }
                                    else
                                    {
                                        await client.Files.UploadSessionAppendAsync(cursor, memoryStream);
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    // For smaller files, just upload
                    using (Stream fileStream = localFile.OpenRead())
                    {
                        await client.Files.UploadAsync(commitInfo, fileStream);
                    }

                    Logger.verbose(this, "upload():done");
                }
            }
        }

        public async Task DownloadAsync(FileItem fileItem, String localName)
        {
            Logger.info(this, "download():Start");

            if (fileItem.IsFolder)
            {
                new LocalFilesystemService().CreateDirectory(fileItem.Path);
            }
            else
            {
                FileMetadata remoteFile = (FileMetadata)fileItem.Object;

                if (remoteFile != null)
                {
                    var client = Client();
                    var response = await client.Files.DownloadAsync(new DownloadArg(remoteFile.PathDisplay));

                    FileInfo localFile = new FileInfo(localName);

                    // We need to wipe out the existing file
                    if (localFile.Exists)
                    {
                        localFile.Delete();
                    }

                    using (var downloadStream = await response.GetContentAsStreamAsync())
                    {
                        var fs = new LocalFilesystemService();
                        await fs.WriteAsync(localFile.FullName, downloadStream, remoteFile.Rev, remoteFile.ClientModified);
                    }

                    Logger.verbose(this, "download():done");
                }
            }
        }

        public async Task DownloadAsync(FileItem file)
        {
            string localFileName = this.replicatorArgs.ToLocalPath(file.Path);
            await this.DownloadAsync(file, localFileName);
        }

        public async Task DeleteAsync(FileItem file)
        {
            if (file.IsFolder)
            {
                throw new InvalidOperationException("Cannot delete a folder");
            }

            Logger.info(this, "delete():Start");
            FileMetadata remoteFile = (FileMetadata)file.Object;

            if (remoteFile != null)
            {
                var client = Client();
                await client.Files.DeleteAsync(new DeleteArg(remoteFile.PathLower));
                Logger.verbose(this, "delete():done");
            }
        }
    }
}