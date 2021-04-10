using Azure.Storage;
using Azure.Storage.Blobs;
using Microsoft.Azure.EventGrid.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.EventGrid;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.IO;
using System.Net.Http;
using System.Text.RegularExpressions;

namespace HRFunction
{
    public static class ThumbnailGenerator
    {
        private static readonly string THUMBNAIL_CONTAINER_NAME = "thumbnails";
        private static readonly string ENV_THUMBNAIL_WIDTH_NAME = "THUMBNAIL_WIDTH";
        private static readonly string ENV_THUMBNAIL_CONTAINER_NAME = "THUMBNAIL_CONTAINER_NAME";
        private static readonly string ENV_STORAGE_NAME = "STORAGE_NAME";//"hrbirdsblobstorage"
        private static readonly string ENV_ROOT_THUMBNAIL_STORAGE_URL = "ROOT_THUMBNAIL_STORAGE_URL";//"https://hrbirdsblobstorage.blob.core.windows.net/thumbnails/";
        private static readonly string ENV_STORAGE_KEY = "STORAGE_KEY";
        private static readonly string THUMBNAIL_EXTENSION = ".jpeg";
        /// <summary>
        /// 1- Process event only from FullImage
        /// 2- Load data from url 
        /// 3- Create memoryStream with uploaded data
        /// 4- Create BlobClient and upload it into Azure
        ///     4.1-Create StorageSharedKeyCredentials object by reading the values from the configuration (appsettings.json)
        ///     4.2- Create the blob client
        ///     4.3- Upmoad the file in Azure
        /// </summary>
        /// <param name="eventGridEvent"></param>
        /// <param name="log"></param>
        [FunctionName("createThumbnail")]
        public static void Run([EventGridTrigger] EventGridEvent eventGridEvent, ILogger log)
        {
            var createdEvent = ((JObject)eventGridEvent.Data).ToObject<StorageBlobCreatedEventData>();
            //1- 
            if (!IsEventProcessable(createdEvent))
            {
                return;
            }
            //2- 
            byte[] imageData = GetImageDataFromEvent(createdEvent, log);
            if (imageData != null)
            {
                var extension = Path.GetExtension(createdEvent.Url);
                var encoder = GetEncoder(extension);

                if (encoder != null)
                {
                    var blobName = GetBlobNameFromUrl(createdEvent.Url);

                    using (var output = new MemoryStream())
                    {
                        //3-
                        UploadImageInMemoryStream(imageData, output, encoder);
                        String blobPath = 
                            Environment.GetEnvironmentVariable(ENV_ROOT_THUMBNAIL_STORAGE_URL) 
                            + Guid.NewGuid().ToString() 
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
                    }
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="imageData"></param>
        /// <param name="output"></param>
        private static void UploadImageInMemoryStream(byte[] imageData, MemoryStream output, IImageEncoder encoder)
        {
            var thumbnailWidth = Convert.ToInt32(Environment.GetEnvironmentVariable(ENV_THUMBNAIL_WIDTH_NAME));
            var thumbContainerName = Environment.GetEnvironmentVariable(ENV_THUMBNAIL_CONTAINER_NAME);
            using (Image<Rgba32> image = Image.Load(imageData))
            {
                var divisor = image.Width / thumbnailWidth;
                var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                image.Mutate(x => x.Resize(thumbnailWidth, height));
                image.Save(output, encoder);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="createdEvent"></param>
        /// <returns></returns>
        private static byte[] GetImageDataFromEvent(StorageBlobCreatedEventData createdEvent, ILogger log)
        {
            HttpClient http = new HttpClient();
            using var response = http.GetAsync(createdEvent.Url);
            response.Wait();
            using var byteArrayTask = response.Result.Content.ReadAsByteArrayAsync();
            byteArrayTask.Wait();

            if (byteArrayTask.IsCompletedSuccessfully)
            {
                return byteArrayTask.Result;
            }
            else
            {
                log.LogError($"{response.Status} ");
            }
            return null;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="bloblUrl"></param>
        /// <returns></returns>
        private static string GetBlobNameFromUrl(string bloblUrl)
        {
            var uri = new Uri(bloblUrl);
            var blobClient = new BlobClient(uri);
            return blobClient.Name;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="extension"></param>
        /// <returns></returns>
        private static IImageEncoder GetEncoder(string extension)
        {
            IImageEncoder encoder = null;

            extension = extension.Replace(".", "");

            var isSupported = Regex.IsMatch(extension, "gif|png|jpe?g", RegexOptions.IgnoreCase);

            if (isSupported)
            {
                switch (extension.ToLower())
                {
                    case "png":
                        encoder = new PngEncoder();
                        break;
                    case "jpg":
                        encoder = new JpegEncoder();
                        break;
                    case "jpeg":
                        encoder = new JpegEncoder();
                        break;
                    case "gif":
                        encoder = new GifEncoder();
                        break;
                    default:
                        break;
                }
            }

            return encoder;
        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        private static bool IsEventProcessable(StorageBlobCreatedEventData data)
        {
            if (data != null)
            {
                if (data.Url != String.Empty && data.Url.ToUpper().Contains(THUMBNAIL_CONTAINER_NAME.ToUpper()))
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            return false;
        }
    }
}
