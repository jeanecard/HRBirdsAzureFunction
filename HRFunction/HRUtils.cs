using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Gif;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HRFunction
{
    static class HRUtils
    {
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="endpoint"></param>
        /// <param name="log"></param>
        public static void NotifyPutBackend<T>(T data, String endpoint, ILogger log)
        {
            if (data != null && !String.IsNullOrEmpty(endpoint) && log != null)
            {
                using (var client = new HttpClient())
                {
                    try
                    {
                        var jsonData = JsonSerializer.Serialize(data);

                        var requestContent = new StringContent(jsonData, Encoding.UTF8, "application/json");
                        log.LogInformation("Content : " + jsonData);

                        var response = client.PutAsync(endpoint, requestContent);
                        response.Wait();
                        if (response.IsCompletedSuccessfully)
                        {
                            log.LogInformation("Successful call : " + response.Result.StatusCode.ToString());
                        }
                        else
                        {
                            log.LogError("client.PutAsync fail" + response.Result.StatusCode.ToString());
                        }
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex.Message);
                    }
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="createdEvent"></param>
        /// <returns></returns>
        public static byte[] GetRawImageFromUrl(string url, ILogger log)
        {
            HttpClient http = new HttpClient();
            using var response = http.GetAsync(url);
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
        /// <param name="extension"></param>
        /// <returns></returns>
        public static IImageEncoder GetEncoder(string extension)
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
        /// <param name="imageData"></param>
        /// <param name="output"></param>
        public static void UploadImageInMemoryStream(
            byte[] imageData, 
            MemoryStream output, 
            IImageEncoder encoder, 
            String thumbContainerName,
            int thumbnailWidth)
        {
            using (Image<Rgba32> image = Image.Load(imageData))
            {
                var divisor = image.Width / thumbnailWidth;
                var height = Convert.ToInt32(Math.Round((decimal)(image.Height / divisor)));

                image.Mutate(x => x.Resize(thumbnailWidth, height));
                image.Save(output, encoder);
            }
        }
    }
}
