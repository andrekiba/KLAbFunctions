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

        [FunctionName("TimerExample")]
        public static void TimerExample([TimerTrigger("0 */2 * * * *")]TimerInfo myTimer, ILogger log)
        {
            log.LogInformation($"C# Timer trigger function executed at: {DateTime.Now}");
        }

        [FunctionName("BlobExample")]
        public static async Task BlobExample(
            [BlobTrigger("netflix-files/{filename}.{ext}")]Stream file,
            string filename,
            string ext,
            [Table("netflixShows")] CloudTable netflixShows,
            ILogger log)
        {

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
                    var query = new TableQuery<NetflixShow>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

                    foreach (var oldShow in await netflixShows.ExecuteQuerySegmentedAsync(query, null))
                    {
                        deleteBatch.Delete(oldShow);
                    }

                    if (deleteBatch.Any())
                        await netflixShows.ExecuteBatchAsync(deleteBatch);

                    tempShows.ForEach(s => insertBatch.Insert(s));

                    if (insertBatch.Any())
                        await netflixShows.ExecuteBatchAsync(insertBatch);
                }
            }
            catch (Exception e)
            {
                log.LogError("BlobExample failed", e);
            }
        }

        static List<NetflixShow> GetTempShows(Stream file, string filename, string partionKey, ILogger log)
        {
            var culture = new CultureInfo("it-IT");
            var tempShows = new List<NetflixShow>();

            using (var reader = ExcelReaderFactory.CreateReader(file))
            {
                var index = 1;

                while (reader.Read())
                {
                    try
                    {
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
                        var releaseDate = reader.GetDateTime(4);
                            
                        tempShows.Add(new NetflixShow
                        {
                            RowKey = Guid.NewGuid().ToString(),
                            PartitionKey = partionKey,
                            Title = title,
                            Rating = rating,
                            RatingLevel = ratingLevel,
                            RatingDescription = ratingDesc,
                            ReleaseYear = releaseDate
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
}
