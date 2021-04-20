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


        [FunctionName("MainNewImage")]
        public static void Run([QueueTrigger("hr-main-new-image", Connection = "ConnectionStrings:HR_IMAGE_QUEUE_CX")] string myQueueItem, ILogger log)
        {
            if (!String.IsNullOrEmpty(myQueueItem))
            {
                //1 get data converted from base64
                var base64EncodedBytes = Convert.FromBase64String(myQueueItem);
                String convertedString = Encoding.UTF8.GetString(base64EncodedBytes);

                HRSubmitPictureInputDto data = JsonConvert.DeserializeObject<HRSubmitPictureInputDto>(convertedString);
                String url = data?.FullImageUrl;
                //2- 
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
                            //5-
                            HRUtils.NotifyBackEnd(blobPath, url, log, ENV_UPDATE_THUMBNAIL_ENDPOINT);
                        }
                    }
                }
            }
            else
            {
                log.LogInformation($"Empty entry");
            }
        }
    }
}
