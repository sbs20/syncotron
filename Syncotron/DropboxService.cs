using Dropbox.Api;
using Dropbox.Api.Files;
using Dropbox.Api.Users;
using log4net;
using Sbs20.Common;
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
        private const string DropboxErrorPathNotFound = "path/not_found/";
        private const string DropboxErrorDisallowedName = "path/disallowed_name/";

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

        private async Task<string> ChunkUploadStreamAsync(Stream stream, 
            string filepath,
            ulong size,
            DateTime lastModified)
        {
            int chunkSize = this.context.HttpChunkSize;
            int chunkCount = (int)Math.Ceiling((double)size / chunkSize);
            string serverRev = null;

            byte[] buffer = new byte[chunkSize];
            string sessionId = null;

            var commitInfo = new CommitInfo(filepath,
                WriteMode.Overwrite.Instance,
                false,
                lastModified);

            DateTime start = DateTime.Now;

            for (var index = 0; index < chunkCount; index++)
            {
                var read = await stream
                    .ReadAsync(buffer, 0, chunkSize)
                    .WithTimeout(TimeSpan.FromSeconds(this.context.HttpReadTimeoutInSeconds));

                var offset = (ulong)(chunkSize * index);

                this.ProgressUpdate(filepath, size, offset, start);

                using (MemoryStream memoryStream = new MemoryStream(buffer, 0, read))
                {
                    if (chunkCount == 1)
                    {
                        var result = await this.Client.Files
                            .UploadAsync(commitInfo, memoryStream)
                            .WithTimeout(TimeSpan.FromSeconds(this.context.HttpWriteTimeoutInSeconds));

                        serverRev = result.Rev;
                    }
                    else if (index == 0)
                    {
                        var result = await this.Client.Files
                            .UploadSessionStartAsync(body: memoryStream)
                            .WithTimeout(TimeSpan.FromSeconds(this.context.HttpWriteTimeoutInSeconds));

                        sessionId = result.SessionId;
                    }
                    else
                    {
                        UploadSessionCursor cursor = new UploadSessionCursor(sessionId, offset);

                        bool isLastChunk = index == chunkCount - 1;
                        if (!isLastChunk)
                        {
                            await this.Client.Files
                                .UploadSessionAppendV2Async(cursor, body: memoryStream)
                                .WithTimeout(TimeSpan.FromSeconds(this.context.HttpWriteTimeoutInSeconds));
                        }
                        else
                        {
                            var result = await this.Client.Files
                                .UploadSessionFinishAsync(cursor, commitInfo, memoryStream)
                                .WithTimeout(TimeSpan.FromSeconds(this.context.HttpWriteTimeoutInSeconds));

                            serverRev = result.Rev;
                        }
                    }
                }
            }

            return serverRev;
        }

        public async Task UploadAsync(FileItem fileItem)
        {
            log.Debug("UploadAsync():Start");

            // Note - this is not ensuring the name is a valid dropbox file name
            string remotePath = this.context.ToOppositePath(fileItem);

            if (fileItem.IsFolder)
            {
                DirectoryInfo localFile = (DirectoryInfo)fileItem.Object;
                await this.Client.Files.CreateFolderAsync(remotePath);
            }
            else
            {
                FileInfo localFile = (FileInfo)fileItem.Object;

                if (localFile == null)
                {
                    throw new InvalidOperationException("Cannot upload null file");
                }

                try
                {
                    await Retry.On<TimeoutException>(async () =>
                    {
                        using (Stream stream = localFile.OpenRead())
                        {
                            fileItem.ServerRev = await this.ChunkUploadStreamAsync(stream,
                                remotePath, fileItem.Size, fileItem.LastModified);
                        }
                    }, 2);
                }
                catch (ApiException<UploadError> ex)
                {
                    if (ex.Message.StartsWith(DropboxErrorDisallowedName))
                    {
                        log.WarnFormat("Unable to upload {0} [{1}]", fileItem.Path, ex.Message);
                    }
                    else
                    {
                        throw;
                    }
                }
            }

            log.Debug("UploadAsync():done");
        }

        public async Task DownloadAsync(FileItem fileItem, string localPath)
        {
            log.Debug("DownloadAsync():Start");

            if (fileItem.IsFolder)
            {
                this.context.LocalFilesystem.CreateDirectory(localPath);
            }
            else
            {
                FileMetadata remoteFile = (FileMetadata)fileItem.Object;

                if (remoteFile != null)
                {
                    try
                    {
                        await Retry.On<TimeoutException>(async () =>
                        {
                            var response = await this.Client.Files.DownloadAsync(new DownloadArg(remoteFile.PathDisplay));

                            using (var downloadStream = await response.GetContentAsStreamAsync())
                            {
                                await this.context.LocalFilesystem.WriteAsync(localPath, remoteFile.Size, downloadStream, remoteFile.ClientModified);
                            }
                        }, 2);
                    }
                    catch (ApiException<DownloadError> ex)
                    {
                        if (ex.Message.StartsWith(DropboxErrorRestrictedContent))
                        {
                            log.WarnFormat("Unable to download {0} [{1}]", fileItem.Path, ex.Message);
                        }
                        else if (ex.Message.StartsWith(DropboxErrorPathNotFound))
                        {
                            // The file has been deleted before we got a chance to get it. Ignore
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

        public void ProgressUpdate(string filepath, ulong filesize, ulong bytes, DateTime start)
        {
            SyncotronMain.TransferProgressWrite(filepath, filesize, bytes, start);
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