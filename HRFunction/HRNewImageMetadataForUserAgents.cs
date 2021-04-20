using System;
using System.Text;
using HRFunction.DuplicatedDtoWaitingForNugetSharing;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace HRFunction
{
    public static class HRNewImageMetadataForUserAgents
    {
        private static readonly string ENV_NEW_IMAGE_SIGNALR_ENDPOINT_KEY = "NEW_IMAGE_SIGNALR_ENDPOINT";

        /// <summary>
        /// 1- Decode raw string from Base64
        /// 2- Deserialize input in json business object
        /// 3- Notifiy SignalR Web service
        /// </summary>
        /// <param name="myQueueItem"></param>
        /// <param name="log"></param>
        [FunctionName("HRNewImageMetadataForUserAgents")]
        public static void Run([QueueTrigger("queue-for-useragents-on-new-image-metadata", Connection = "ConnectionStrings:HR_IMAGE_QUEUE_CX")]string myQueueItem, ILogger log)
        {
            try
            {
                if (!String.IsNullOrEmpty(myQueueItem))
                {
                    log.LogInformation($"C# Queue trigger function processed original value: {myQueueItem}");
                    //1- 
                    var base64EncodedBytes = Convert.FromBase64String(myQueueItem);
                    String convertedString = Encoding.UTF8.GetString(base64EncodedBytes);
                    //2-
                    log.LogInformation($"C# Queue trigger function processed converted value: {convertedString}");
                    HRSubmitPictureInputDto data = JsonConvert.DeserializeObject<HRSubmitPictureInputDto>(convertedString);
                    //3- 
                    String endpoint = Environment.GetEnvironmentVariable(ENV_NEW_IMAGE_SIGNALR_ENDPOINT_KEY);
                    log.LogInformation("Endpoint : " + endpoint);
                    HRUtils.NotifyBackend<HRSubmitPictureInputDto>(data, endpoint, log);
                    log.LogInformation("HRNewImageMetadataForUserAgents ended successfully.");
                }
                else
                {
                    log.LogInformation("HRNewImageMetadataForUserAgents dummy on null entry.");
                }
            }
            catch(Exception ex)
            {
                log.LogError("HRNewImageMetadataForUserAgents exception : " + ex.Message);
            }
        }
    }
}
