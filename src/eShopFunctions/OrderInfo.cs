using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using System.Collections.Concurrent;

namespace eShopFunctions
{
    public static class OrderInfo
    {
        private static CosmosClient cosmosClient;

        static OrderInfo()
        {
            if (cosmosClient == null)
            {
                string cosmosDBConnectionString = Environment.GetEnvironmentVariable("AzureCosmosDBConnectionString");
                cosmosClient = new CosmosClient(cosmosDBConnectionString);
            }
        }

        [FunctionName("OrderInfo")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string orderId = req.Query["orderId"];

            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            //dynamic data = JsonConvert.DeserializeObject(requestBody);
            //name = name ?? data?.name;
            
            /*var storageConnection = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
            BlobServiceClient blobServiceClient = new BlobServiceClient(storageConnection);
            BlobContainerClient containerClient = blobServiceClient.GetBlobContainerClient("orders");
            await containerClient.CreateIfNotExistsAsync();
            BlobClient blobClient = containerClient.GetBlobClient(orderId);
            await blobClient.UploadAsync(new BinaryData(requestBody));*/


            //CosmosDB save
            var container = cosmosClient.GetContainer("eShopOrderDB", "OrderInfo");
            CosmosOrder data = JsonConvert.DeserializeObject<CosmosOrder>(requestBody);
            CosmosOrderExt dataExt = new CosmosOrderExt(data);

            try
            {
                var result = await container.CreateItemAsync(dataExt, new PartitionKey(dataExt.Id));
                return new OkObjectResult(result.Resource.id);
            }
            catch (CosmosException cosmosException)
            {
                log.LogError("Creating item failed with error {0}", cosmosException.ToString());
                return new BadRequestObjectResult($"Failed to create item. Cosmos Status Code {cosmosException.StatusCode}, Sub Status Code {cosmosException.SubStatusCode}: {cosmosException.Message}.");
            }



            string responseMessage = string.IsNullOrEmpty(orderId)
                ? "This HTTP triggered function executed successfully. Pass a name in the query string or in the request body for a personalized response."
                : $"Hello, {orderId}. This HTTP triggered function executed successfully.";

            return new OkObjectResult(responseMessage);
        }
    }
}
