using System.IO;
using System.Linq;
using System.Threading.Tasks;
using KLabFunctions.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace KLabFunctions
{
    public static class SessionApi
    {
        const string TableName = "sessions";
        const string QueueName = "sessions";
        const string PartionKey = "SESSION";
        const string StorageConnection = "AzureWebJobsStorage";

        [FunctionName("CreateSession")]
        public static async Task<IActionResult> CreateSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "session")] HttpRequest req,
            [Table(TableName, Connection = StorageConnection)] IAsyncCollector<SessionEntity> sessionTable,
            [Queue(QueueName, Connection = StorageConnection)] IAsyncCollector<Session> sessionQueue,
            ILogger log)
        {
            log.LogInformation("Creating a new session");
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var input = JsonConvert.DeserializeObject<CreateSession>(requestBody);

            var session = new Session { Description = input.Description };
            await sessionTable.AddAsync(session.ToSessionEntity());
            await sessionQueue.AddAsync(session);
            return new OkObjectResult(session);
        }

        [FunctionName("GetSessions")]
        public static async Task<IActionResult> GetSessions(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "session")] HttpRequest req,
            [Table(TableName, Connection = StorageConnection)] CloudTable sessionTable,
            ILogger log)
        {
            log.LogInformation("Getting sessions");
            var query = new TableQuery<SessionEntity>();
            var segment = await sessionTable.ExecuteQuerySegmentedAsync(query, null);
            return new OkObjectResult(segment.Select(Mappings.ToSession));
        }

        [FunctionName("GetSessionById")]
        public static IActionResult GetSessionById(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "session/{id}")] HttpRequest req,
            [Table(TableName, PartionKey, "{id}", Connection = StorageConnection)] SessionEntity se,
            ILogger log,
            string id)
        {
            log.LogInformation("Getting session by id");
            if (se == null)
                return new NotFoundResult();
            
            return new OkObjectResult(se.ToSession());
        }

        [FunctionName("UpdateSession")]
        public static async Task<IActionResult> UpdateSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "session/{id}")]
            HttpRequest req,
            [Table(TableName, Connection = StorageConnection)] CloudTable sessionTable,
            ILogger log,
            string id)
        {
            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var updated = JsonConvert.DeserializeObject<UpdateSession>(requestBody);

            var findOperation = TableOperation.Retrieve<SessionEntity>(PartionKey, id);
            var findResult = await sessionTable.ExecuteAsync(findOperation);
            if (findResult.Result == null)
            {
                return new NotFoundResult();
            }

            var existingRow = (SessionEntity) findResult.Result;
            existingRow.Accepted = updated.Accepted;
            existingRow.Rejected = updated.Rejected;
            if (!string.IsNullOrEmpty(updated.Description))
                existingRow.Description = updated.Description;

            var replaceOperation = TableOperation.Replace(existingRow);
            await sessionTable.ExecuteAsync(replaceOperation);
            return new OkObjectResult(existingRow.ToSession());
        }

        [FunctionName("DeleteSession")]
        public static async Task<IActionResult> DeleteSession(
            [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "session/{id}")]HttpRequest req,
            [Table(TableName, Connection = StorageConnection)] CloudTable sessionTable,
            ILogger log,
            string id)
        {
            var deleteOperation = TableOperation.Delete(new TableEntity
                { PartitionKey = PartionKey, RowKey = id, ETag = "*" });
            try
            {
                var deleteResult = await sessionTable.ExecuteAsync(deleteOperation);
            }
            catch (StorageException e) when (e.RequestInformation.HttpStatusCode == 404)
            {
                return new NotFoundResult();
            }
            return new OkResult();
        }
    }
}
