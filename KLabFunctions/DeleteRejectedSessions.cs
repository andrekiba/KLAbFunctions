using System;
using System.Linq;
using System.Threading.Tasks;
using KLabFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions
{
    public static class DeleteRejectedSessions
    {
        [FunctionName("DeleteRejectedSessions")]
        public static async Task Run([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer,
            [Table("sessions", Connection = "AzureWebJobsStorage")] CloudTable sessionTable,
            ILogger log)
        {
            var query = new TableQuery<SessionEntity>();
            var segment = await sessionTable.ExecuteQuerySegmentedAsync(query, null);
            var deleted = 0;
            foreach (var session in segment.Where(s => s.Rejected))
            {
                await sessionTable.ExecuteAsync(TableOperation.Delete(session));
                deleted++;
            }
            log.LogInformation($"Deleted {deleted} items at {DateTime.Now}");
        }
    }
}
