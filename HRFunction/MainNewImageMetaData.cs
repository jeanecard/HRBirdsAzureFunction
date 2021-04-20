using Azure.Storage.Queues;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using System;
using System.Text;

namespace HRFunction
{
    public static class MainNewImageMetaData
    {
        private static String _QUEUE_STORAGE_CONNEXION_KEY = "HR_IMAGE_QUEUE_CX";
        private static String _QUEUE_FOR_USER_AGENTS_NAME_KEY = "QUEUE_STORAGE_NEW_IMAGE_METADATA_FOR_USER_AGENTS";
        [FunctionName("MainNewImageMetaData")]
        public static void Run([QueueTrigger("hr-main-new-image-metadata", Connection = "ConnectionStrings:HR_IMAGE_QUEUE_CX")]string myQueueItem, ILogger log)
        {
            if (!String.IsNullOrEmpty(myQueueItem))
            {
                //1- Get env variable
                String queueStorageCx = Environment.GetEnvironmentVariable(_QUEUE_STORAGE_CONNEXION_KEY);
                String queueNewImageMetaDataForUserAgents = Environment.GetEnvironmentVariable(_QUEUE_FOR_USER_AGENTS_NAME_KEY);


                //2- Log received info
                var base64EncodedBytes = Convert.FromBase64String(myQueueItem);
                String convertedString = Encoding.UTF8.GetString(base64EncodedBytes);
                log.LogInformation($"C# Queue trigger function processed original value: {myQueueItem}");
                log.LogInformation($"C# Queue trigger function processed converted value: {convertedString}");

                //3- get or create queueFor user agents
                // Get the connection string from app settings
                // Instantiate a QueueClient which will be used to create and manipulate the queue
                QueueClientOptions queueClientOptions = new QueueClientOptions()
                {
                    MessageEncoding = QueueMessageEncoding.Base64
                };

                QueueClient queueClient = new QueueClient(
                    queueStorageCx,
                    queueNewImageMetaDataForUserAgents,
                    queueClientOptions);
                queueClient.CreateIfNotExists();

                //4- Send message
                if (queueClient.Exists())
                {
                    var queueResponse = queueClient.SendMessage(myQueueItem);
                }
                else
                {
                    log.LogInformation($"Error when creating queue");
                }
            }
            else
            {
                log.LogInformation($"Empty entry");
            }
        }
    }
}
