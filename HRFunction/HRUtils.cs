using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace HRFunction
{
    static class HRUtils
    {
        /// <summary>
        /// 1- Extract id from fullimageURl 
        /// 2- Notify
        /// </summary>
        /// <param name="blobPath"></param>
        public static void NotifyBackEnd(
            string thumbnailValue,
            string fullimageValue, ILogger log,
            String envUpdateThumbnailEndpointKey)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var data = new
                    {
                        Id = Path.GetFileNameWithoutExtension(fullimageValue),
                        ThumbnailImageURL = thumbnailValue
                    };
                    var company = JsonSerializer.Serialize(data);

                    var requestContent = new StringContent(company, Encoding.UTF8, "application/json");
                    log.LogInformation("Contenu : " + company);

                    string endPoint = Environment.GetEnvironmentVariable(envUpdateThumbnailEndpointKey);
                    log.LogInformation("Endpoint : " + endPoint);

                    var response = client.PutAsync(endPoint, requestContent);
                    response.Wait();
                    if (response.IsCompletedSuccessfully)
                    {
                        log.LogInformation("tout va bien : " + response.Result.StatusCode.ToString());
                    }
                    else
                    {
                        log.LogInformation("Ca a chié: " + response.Result.StatusCode.ToString());
                    }
                }
                catch (Exception ex)
                {
                    log.LogInformation("RASTOS3");
                    log.LogInformation(ex.Message);
                }
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="data"></param>
        /// <param name="endpoint"></param>
        /// <param name="log"></param>
        public static void NotifyBackend<T>(T data, String endpoint, ILogger log)
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
    }
}
