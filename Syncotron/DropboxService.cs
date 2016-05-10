using Dropbox.Api;
using Dropbox.Api.Files;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class DropboxService : ICloudService
    {
        private DropboxClient client = null;
        private ReplicatorContext context = null;

        public Uri StartAuthorisation()
        {
            string clientId = this.context.Settings.Dropbox_ClientId;

            // The user should go to this URI, login and get a code... and enter that into FinishAuthorisation
            return DropboxOAuth2Helper.GetAuthorizeUri(clientId);
        }

        public async Task FinishAuthorisation(string code)
        {
            string clientId = this.context.Settings.Dropbox_ClientId;
            string secret = this.context.Settings.Dropbox_Secret;
            OAuth2Response oauthResponse = await DropboxOAuth2Helper.ProcessCodeFlowAsync(code, clientId, secret);
            string accessToken = oauthResponse.AccessToken;
            this.context.LocalStorage.SettingsWrite("Dropbox_AccessToken", accessToken);
        }

        public DropboxClient Client
        {
            get
            {
                if (client == null)
                {
                    string userAgent = "Sbs20.Syncotron";

                    var config = new DropboxClientConfig
                    {
                        UserAgent = userAgent
                    };

                    string accessToken = this.context.LocalStorage.SettingsRead<string>("Dropbox_AccessToken");
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new InvalidOperationException("Invalid access token");
                    }

                    client = new DropboxClient(accessToken, config);
                }

                return client;
            }
        }

        public bool IsAuthorised
        {
            get
            {
                try
                {
                    var client = this.Client;
                    return client != null;
                }
                catch
                {
                    return false;
                }
            }
        }

        public DropboxService(ReplicatorContext context)
        {
            this.context = context;
        }

        public async Task<string> ForEachAsync(string path, bool recursive, bool deleted, Action<FileItem> action)
        {
            Action<Metadata> handleEntry = (entry) =>
            {
                var item = FileItem.Create(entry);
                if (item.Path != this.context.RemotePath)
                {
                    action(item);
                }
            };

            ListFolderResult result = await this.Client.Files.ListFolderAsync(
                new ListFolderArg(path, recursive, false, deleted));

            foreach (var entry in result.Entries)
            {
                handleEntry(entry);
            }

            while (result.HasMore)
            {
                result = await this.Client.Files.ListFolderContinueAsync(new ListFolderContinueArg(result.Cursor));

                foreach (var entry in result.Entries)
                {
                    handleEntry(entry);
                }
            }

            return result.Cursor;
        }

        public async Task<string> ForEachContinueAsync(string cursor, Action<FileItem> action)
        {
            Action<Metadata> handleEntry = (entry) =>
            {
                var item = FileItem.Create(entry);
                if (item.Path != this.context.RemotePath)
                {
                    action(item);
                }
            };

            while (true)
            {
                var result = await this.Client.Files.ListFolderContinueAsync(new ListFolderContinueArg(cursor));

                foreach (var entry in result.Entries)
                {
                    handleEntry(entry);
                }

                if (!result.HasMore)
                {
                    return result.Cursor;
                }
                else
                {
                    cursor = result.Cursor;
                }
            }
        }

        public async Task MoveAsync(FileItem file, string desiredPath)
        {
            Logger.info(this, "move():Start");
            FileMetadata remoteFile = (FileMetadata)file.Object;

            if (remoteFile != null)
            {
                await this.Client.Files.MoveAsync(remoteFile.PathLower, desiredPath);
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
                string remoteFileName = this.context.ToRemotePath(file.Path);

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
                                    var result = await this.Client.Files.UploadSessionStartAsync(memoryStream);
                                    sessionId = result.SessionId;
                                }
                                else
                                {
                                    UploadSessionCursor cursor = new UploadSessionCursor(sessionId, (ulong)(chunkSize * index));

                                    if (index == numChunks - 1)
                                    {
                                        await this.Client.Files.UploadSessionFinishAsync(cursor, commitInfo, memoryStream);
                                    }
                                    else
                                    {
                                        await this.Client.Files.UploadSessionAppendAsync(cursor, memoryStream);
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
                        await this.Client.Files.UploadAsync(commitInfo, fileStream);
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
                this.context.LocalFilesystem.CreateDirectory(fileItem.Path);
            }
            else
            {
                FileMetadata remoteFile = (FileMetadata)fileItem.Object;

                if (remoteFile != null)
                {
                    var response = await this.Client.Files.DownloadAsync(new DownloadArg(remoteFile.PathDisplay));

                    FileInfo localFile = new FileInfo(localName);

                    // We need to wipe out the existing file
                    if (localFile.Exists)
                    {
                        localFile.Delete();
                    }

                    using (var downloadStream = await response.GetContentAsStreamAsync())
                    {
                        await this.context.LocalFilesystem.WriteAsync(localFile.FullName, downloadStream, remoteFile.Rev, remoteFile.ClientModified);
                    }

                    Logger.verbose(this, "download():done");
                }
            }
        }

        public async Task DownloadAsync(FileItem file)
        {
            string localFileName = this.context.ToLocalPath(file.Path);
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
                await this.Client.Files.DeleteAsync(new DeleteArg(remoteFile.PathLower));
                Logger.verbose(this, "delete():done");
            }
        }
    }
}