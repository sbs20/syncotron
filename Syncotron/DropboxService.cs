using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Users;
using log4net;
using Sbs20.Extensions;
using Sbs20.Syncotron.Diagnostics;
using System;
using System.IO;
using System.Threading.Tasks;

namespace Sbs20.Syncotron
{
    public class DropboxService : ICloudService
    {
        private const string DropboxErrorRestrictedContent = "path/restricted_content";

        private static readonly ILog log = LogManager.GetLogger(typeof(DropboxService));
        private FullAccount currentAccount = null;
        private DropboxClient client = null;
        private ReplicatorContext context = null;

        public Uri StartAuthorisation()
        {
            string clientId = this.context.Settings.Dropbox_ClientId;

            // The user should go to this URI, login and get a code... and enter that into FinishAuthorisation
            return DropboxOAuth2Helper.GetAuthorizeUri(clientId);
        }

        public void FinishAuthorisation(string code)
        {
            string clientId = this.context.Settings.Dropbox_ClientId;
            string secret = this.context.Settings.Dropbox_Secret;

            // There's no point in making this async - it blocks everything anyway
            Task<OAuth2Response> task = DropboxOAuth2Helper.ProcessCodeFlowAsync(code, clientId, secret);
            task.Wait();
            string accessToken = task.Result.AccessToken;
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
                    return this.Client != null;
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
            const string logstem = "ForEachAsync():{0}";
            Action<Metadata> handleEntry = (entry) =>
            {
                var item = FileItem.Create(entry);
                if (item.Path != this.context.RemotePath)
                {
                    action(item);
                }
            };

            var args0 = new ListFolderArg(path, recursive, false, deleted);
            ListFolderResult result = await this.Client.Files
                .ListFolderAsync(args0)
                .WithTimeout(TimeSpan.FromSeconds(this.context.HttpReadTimeoutInSeconds));

            // These logging calls are very expensive so check we're enabled first
            if (log.IsDebugEnabled)
            {
                log.DebugFormat(logstem, "Request:" + Json.ToString(args0));
                log.DebugFormat(logstem, "Result:" + Json.ToString(result));
            }

            foreach (var entry in result.Entries)
            {
                handleEntry(entry);
            }

            while (result.HasMore)
            {
                var args1 = new ListFolderContinueArg(result.Cursor);
                result = await this.Client.Files
                    .ListFolderContinueAsync(args1)
                    .WithTimeout(TimeSpan.FromSeconds(this.context.HttpReadTimeoutInSeconds));

                if (log.IsDebugEnabled)
                {
                    log.DebugFormat(logstem, "Request:" + Json.ToString(args1));
                    log.DebugFormat(logstem, "Result:" + Json.ToString(result));
                }

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
            log.Debug("MoveAsync():Start");
            FileMetadata remoteFile = (FileMetadata)file.Object;

            if (remoteFile != null)
            {
                await this.Client.Files.MoveAsync(remoteFile.PathLower, desiredPath);
                log.Debug("MoveAsync():done");
            }
        }

        public async Task UploadAsync(FileItem file)
        {
            log.Debug("UploadAsync():Start");
            FileInfo localFile = (FileInfo)file.Object;

            if (localFile == null)
            {
                throw new InvalidOperationException("Cannot upload null file");
            }

            // Note - this is not ensuring the name is a valid dropbox file name
            string remoteFileName = this.context.ToOppositePath(file);

            CommitInfo commitInfo = new CommitInfo(
                remoteFileName,
                WriteMode.Overwrite.Instance,
                false,
                file.LastModified);

            // One meg
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
                                var result = await this.Client.Files.UploadSessionStartAsync(body: memoryStream);
                                sessionId = result.SessionId;
                            }
                            else
                            {
                                UploadSessionCursor cursor = new UploadSessionCursor(sessionId, (ulong)(chunkSize * index));

                                if (index == numChunks - 1)
                                {
                                    var result = await this.Client.Files.UploadSessionFinishAsync(cursor, commitInfo, memoryStream);
                                    file.ServerRev = result.Rev;
                                }
                                else
                                {
                                    await this.Client.Files.UploadSessionAppendV2Async(cursor, body: memoryStream);
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
                    var result = await this.Client.Files.UploadAsync(commitInfo, fileStream);
                    file.ServerRev = result.Rev;
                }

                log.Debug("UploadAsync():done");
            }
        }

        public async Task DownloadAsync(FileItem fileItem, String localName)
        {
            log.Debug("DownloadAsync():Start");

            if (fileItem.IsFolder)
            {
                this.context.LocalFilesystem.CreateDirectory(localName);
            }
            else
            {
                FileMetadata remoteFile = (FileMetadata)fileItem.Object;

                if (remoteFile != null)
                {
                    try
                    {
                        var response = await this.Client.Files.DownloadAsync(new DownloadArg(remoteFile.PathDisplay));

                        using (var downloadStream = await response.GetContentAsStreamAsync())
                        {
                            await this.context.LocalFilesystem.WriteAsync(localName, downloadStream, remoteFile.ClientModified);
                        }
                    }
                    catch (ApiException<DownloadError> ex)
                    {
                        if (ex.Message.StartsWith(DropboxErrorRestrictedContent))
                        {
                            log.WarnFormat("Unable to download {0} [{1}]", fileItem.Path, ex.Message);
                        }
                        else
                        {
                            throw;
                        }
                    }

                    log.Debug("DownloadAsync():done");
                }
            }
        }

        public async Task DownloadAsync(FileItem file)
        {
            string localFileName = this.context.ToOppositePath(file);
            await this.DownloadAsync(file, localFileName);
        }

        public async Task DeleteAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new InvalidOperationException("Path is empty.");
            }

            try
            {
                log.Debug("DeleteAsync():Start");
                await this.Client.Files.DeleteAsync(new DeleteArg(path));
                log.Debug("DeleteAsync():done");
            }
            catch
            {
                log.Debug("DeleteAsync(" + path + "):fail");
            }
        }

        public async Task<string> LatestCursorAsync(string path, bool recursive, bool deleted)
        {
            var result = await this.Client.Files.ListFolderGetLatestCursorAsync(new ListFolderArg(
                path, recursive, false, deleted));

            return result.Cursor;
        }

        public async Task<FileItem> FileSelectAsync(string path)
        {
            try
            {
                var result = await this.Client.Files.GetMetadataAsync(new GetMetadataArg(path));
                return FileItem.Create(result);
            }
            catch
            {
                return null;
            }
        }

        private FullAccount CurrentAccount
        {
            get
            {
                if (this.currentAccount == null)
                {
                    // This is a one off, so we're going to block
                    Task<FullAccount> task = this.Client.Users.GetCurrentAccountAsync();
                    task.Wait();
                    this.currentAccount = task.Result;
                }

                return this.currentAccount;
            }
        }

        public string CurrentAccountEmail
        {
            get { return this.CurrentAccount.Email; }
        }
    }
}