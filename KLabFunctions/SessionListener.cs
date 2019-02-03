using System.Threading.Tasks;
using KLabFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace KLabFunctions
{
    public static class SessionListener
    {
        [FunctionName("SessionListener")]
        public static async Task Run([QueueTrigger("sessions", Connection = "AzureWebJobsStorage")]Session session,
            [Blob("session-files", Connection = "AzureWebJobsStorage")]CloudBlobContainer container,
            ILogger log)
        {
            await container.CreateIfNotExistsAsync();
            var blob = container.GetBlockBlobReference($"{session.Id}.txt");
            await blob.UploadTextAsync($"Created a new session: {session.Description}");

            log.LogInformation($"C# Queue trigger function processed: {session.Description}");
        }
    }
}
