using Azure.Storage;
using Azure.Storage.Blobs;
using HRFunction.DuplicatedDtoWaitingForNugetSharing;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;

namespace HRFunction
{
    public static class MainNewImage
    {
        private static readonly string ENV_THUMBNAIL_WIDTH_NAME = "THUMBNAIL_WIDTH";
        private static readonly string ENV_THUMBNAIL_CONTAINER_NAME = "THUMBNAIL_CONTAINER_NAME";
        private static readonly string ENV_ROOT_THUMBNAIL_STORAGE_URL = "ROOT_THUMBNAIL_STORAGE_URL";//"https://hrbirdsblobstorage.blob.core.windows.net/thumbnails/";
        private static readonly string THUMBNAIL_EXTENSION = ".jpeg";
        private static readonly string ENV_STORAGE_KEY = "STORAGE_KEY";
        private static readonly string ENV_STORAGE_NAME = "STORAGE_NAME";
        private static readonly string ENV_UPDATE_THUMBNAIL_ENDPOINT = "UPDATE_THUMBNAIL_ENDPOINT";
        private static readonly string ENV_NEW_THUMBNAIL_SIGNALR_ENDPOINT_KEY = "NEW_THUMBNAIL_SIGNALR_ENDPOINT";

        /// <summary>
        /// 1- Get data converted from base64
        /// 2- Create thumbnail and add it into blob storage
        /// 3- Notify SubmittedImage backend with the new thumbnail value.
        /// 4- Notify User agents that a new humbnial s available. ConnectionStrings:HR_IMAGE_QUEUE_CX
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        [FunctionName("MainNewImage")]
        public static void Run([QueueTrigger("hr-main-new-image", Connection = "ConnectionStrings:HR_IMAGE_QUEUE_CX")] string myQueueItem, ILogger log)
        {
            //BONNE PRATIQUE TOUJLURS ENCADRE DE TRY CATH POUR S'Y RETROUVER DANS LES LOGS AZURE
            try
            {
                if (!String.IsNullOrEmpty(myQueueItem))
                {
                    //1 
                    var base64EncodedBytes = Convert.FromBase64String(myQueueItem);
                    String convertedString = Encoding.UTF8.GetString(base64EncodedBytes);
                    HRSubmitPictureListItemDto data = JsonConvert.DeserializeObject<HRSubmitPictureListItemDto>(convertedString);
                    String url = data?.FullImageUrl;
                    if(!String.IsNullOrEmpty(url))
                    {
                        log.LogInformation($"Data received : " + convertedString);

                        //2- 
                        String blobPath = CreateAndUploadThumbnail(url, log);

                        data.ThumbnailUrl = blobPath;
                        //3-
                        string backEndPoint = Environment.GetEnvironmentVariable(ENV_UPDATE_THUMBNAIL_ENDPOINT);
                        HRUtils.NotifyPutBackend<HRSubmitPictureListItemDto>(data, backEndPoint, log);
                        log.LogInformation($"Step 2: " + blobPath);

                        //4-
                        string userAgentsEndPoint = Environment.GetEnvironmentVariable(ENV_NEW_THUMBNAIL_SIGNALR_ENDPOINT_KEY);
                        HRUtils.NotifyPutBackend<HRSubmitPictureListItemDto>(data, userAgentsEndPoint, log);
                        log.LogInformation($"Step 3 : " + blobPath);
                    }
                    else
                    {
                        log.LogInformation($"No Fullimage URL supplied, can not process thumbnail.");

                    }

                }
                else
                {
                    log.LogInformation($"Empty entry");
                }
            }
            catch (Exception ex)
            {
                log.LogInformation($"HR Error !!! : " + ex.Message);
            }
        }
        /// <summary>
        /// 
        /// </summary>
        private static string CreateAndUploadThumbnail(string url, ILogger log)
        {
            byte[] imageData = HRUtils.GetRawImageFromUrl(url, log);
            if (imageData != null)
            {
                var extension = Path.GetExtension(url);
                var encoder = HRUtils.GetEncoder(extension);

                if (encoder != null)
                {
                    var uri = new Uri(url);
                    var tempBlobClient = new BlobClient(uri);
                    var blobName = tempBlobClient.Name;

                    using (var output = new MemoryStream())
                    {
                        var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable(ENV_THUMBNAIL_WIDTH_NAME));
                        var thumbContainerName = Environment.GetEnvironmentVariable(ENV_THUMBNAIL_CONTAINER_NAME);
                        //3-
                        HRUtils.UploadImageInMemoryStream(imageData, output, encoder, thumbContainerName, thumbnailWidth);
                        String blobPath =
                            Environment.GetEnvironmentVariable(ENV_ROOT_THUMBNAIL_STORAGE_URL)
                            + Path.GetFileNameWithoutExtension(url)
                            + THUMBNAIL_EXTENSION;
                        //4-
                        Uri blobUri = new Uri(blobPath);
                        //4.1-
                        StorageSharedKeyCredential storageCredentials =
                            new StorageSharedKeyCredential(
                                Environment.GetEnvironmentVariable(ENV_STORAGE_NAME),
                                Environment.GetEnvironmentVariable(ENV_STORAGE_KEY));
                        //4.2-
                        BlobClient blobClient = new BlobClient(blobUri, storageCredentials);
                        output.Position = 0;
                        // 4.3-
                        var azureResponse = blobClient.Upload(output);
                        log.LogInformation("Result id version of blob: " + azureResponse.Value?.VersionId);
                        return blobPath;
                    }
                }
            }
            return String.Empty;
        }
    }
}
