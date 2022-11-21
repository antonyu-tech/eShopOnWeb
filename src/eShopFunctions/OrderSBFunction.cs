using System;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Storage.Blobs;
using System.IO;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace eShopFunctions
{
    public static class OrderSBFunction
    {
        [FunctionName("OrderSBFunction")]
        public static async Task Run([ServiceBusTrigger("ordermessages1", Connection = "servicebusconnectionstr")]string myQueueItem, ILogger log)
        {
            log.LogInformation($"C# ServiceBus queue trigger function processed message: {myQueueItem}");

            try
            {
                CosmosOrder data = JsonConvert.DeserializeObject<CosmosOrder>(myQueueItem);
                log.LogInformation($"Order {data.Id} Processing Started");
                var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnection);
                BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("orders");
                await containerClient.CreateIfNotExistsAsync();
                BlobClient blobClient = containerClient.GetBlobClient(data.Id.ToString());
                await blobClient.UploadAsync(new BinaryData(myQueueItem));

                log.LogInformation($"Order {data.Id} Processed");
            }
            catch (Exception ex)
            {
                log.LogError(ex, $"Something went wrong while processing a message", myQueueItem);
                throw;
            }

        }
    }
}
