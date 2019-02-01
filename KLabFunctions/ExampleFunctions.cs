using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using KLabFunctions.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;

namespace KLabFunctions
{
    public static class ExampleFunctions
    {
        [Disable]
        [FunctionName("HttpTriggerExample")]
        public static async Task<IActionResult> HttpTriggerExample(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = null)] HttpRequest req,
            ILogger log)
        {
            log.LogInformation("C# HTTP trigger function processed a request.");

            string name = req.Query["name"];

            var requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            dynamic data = JsonConvert.DeserializeObject(requestBody);
            name = name ?? data?.name;

            return name != null
                ? (ActionResult)new OkObjectResult($"Hello, {name}")
                : new BadRequestObjectResult("Please pass a name on the query string or in the request body");
        }

        [Disable]
        [FunctionName("TimerExample")]
        public static void TimerExample([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        //[Disable]
        [FunctionName("BlobExample")]
        public static async Task BlobExample(
            [BlobTrigger("netflix-files/{filename}.{ext}")]Stream file,
            string filename,
            string ext,
            [Table("shows")] CloudTable shows,
            ILogger log)
        {

            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            log.LogInformation($"C# Blob trigger function Processed blob\n Name:{filename} \n Size: {file.Length} Bytes");

            try
            {
                if (!ext.Equals("csv", StringComparison.InvariantCultureIgnoreCase))
                    return;

                var partitionKey = Path.GetFileName(filename);
                var insertBatch = new TableBatchOperation();
                var deleteBatch = new TableBatchOperation();

                var tempShows = GetTempShows(file, filename, partitionKey, log);

                if (tempShows.Any())
                {
                    var query = new TableQuery<Show>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

                    foreach (var oldShow in await shows.ExecuteQuerySegmentedAsync(query, null))
                    {
                        deleteBatch.Delete(oldShow);
                    }

                    if (deleteBatch.Any())
                        await shows.ExecuteBatchAsync(deleteBatch);

                    await shows.ExecuteInsertBatchAsync(tempShows.Cast<TableEntity>().ToList());

                    //tempShows.ForEach(s => insertBatch.Insert(s));

                    //if (insertBatch.Any())
                    //    await shows.ExecuteBatchAsync(insertBatch);

                }
            }
            catch (Exception e)
            {
                log.LogError("BlobExample failed", e);
            }
        }

        static List<Show> GetTempShows(Stream file, string filename, string partionKey, ILogger log)
        {
            var culture = new CultureInfo("it-IT");
            var tempShows = new List<Show>();
            
            using (var reader = ExcelReaderFactory.CreateCsvReader(file))
            {
                var index = 0;

                while (reader.Read())
                {
                    try
                    {
                        if (index == 0)
                        {
                            index++;
                            continue;
                        }

                        if (reader.IsDBNull(0))
                            continue;

                        var titleValue = reader.GetValue(0);
                        var title = titleValue?.ToString() ?? string.Empty;

                        var ratingValue = reader.GetValue(1);
                        var rating = ratingValue?.ToString() ?? string.Empty;

                        var ratingLevelValue = reader.GetValue(2);
                        var ratingLevel = ratingLevelValue?.ToString() ?? string.Empty;

                        var ratingDesclValue = reader.GetValue(3);
                        var ratingDesc = ratingDesclValue?.ToString() ?? string.Empty;

                        var releaseYearValue = reader.GetValue(4);
                        var releaseYear = releaseYearValue?.ToString() ?? string.Empty;

                        tempShows.Add(new Show
                        {
                            RowKey = Guid.NewGuid().ToString(),
                            PartitionKey = partionKey,
                            Title = title,
                            Rating = rating,
                            RatingLevel = ratingLevel,
                            RatingDescription = ratingDesc,
                            ReleaseYear = releaseYear
                        });

                        index++;
                    }
                    catch (Exception e)
                    {
                        log.LogError($"Problem parsing row {index} on file {filename}", e);
                    }
                }
            }

            return tempShows;
        }      
    }

    public static class CloudTableExtensions
    {
        public static async Task ExecuteInsertBatchAsync(this CloudTable table, List<TableEntity> entities)
        {
            var taskCount = 0;
            const int taskThreshold = 200;
            var batchTasks = new List<Task<IList<TableResult>>>();

            for (var i = 0; i < entities.Count; i += 100)
            {
                taskCount++;

                var batchItems = entities.Skip(i)
                    .Take(100)
                    .ToList();

                var batch = new TableBatchOperation();
                foreach (var item in batchItems)
                {
                    batch.Insert(item);
                }

                var task = table.ExecuteBatchAsync(batch);
                batchTasks.Add(task);

                if (taskCount < taskThreshold)
                    continue;

                await Task.WhenAll(batchTasks);
                taskCount = 0;
            }

            await Task.WhenAll(batchTasks);
        }
    }
}
