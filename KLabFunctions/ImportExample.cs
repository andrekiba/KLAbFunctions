using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using ExcelDataReader;
using KLabFunctions.Models;
using Microsoft.Azure.WebJobs;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Table;

namespace KLabFunctions
{
    public static class ImportExample
    {
        [FunctionName("ImportNetflix")]
        public static async Task ImportNetflix(
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

                var tempShows = GetTempShows(file, filename, partitionKey, log);

                if (tempShows.Any())
                {
                    var query = new TableQuery<Show>().Where(TableQuery.GenerateFilterCondition("PartitionKey", QueryComparisons.Equal, partitionKey));

                    var oldShows = await shows.ExecuteQuerySegmentedAsync(query, null);

                    if(oldShows.Any())
                        await shows.ExecuteParallelBatchAsync(TableOperationType.Delete, oldShows.Cast<ITableEntity>().ToList());

                    await shows.ExecuteParallelBatchAsync(TableOperationType.Insert, tempShows.Cast<ITableEntity>().ToList());
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
        public static async Task ExecuteParallelBatchAsync(this CloudTable table, TableOperationType oType, IList<ITableEntity> entities)
        {
            var taskCount = 0;
            const int taskThreshold = 200;
            const int maxBatchSize = 100;
            var batchTasks = new List<Task<IList<TableResult>>>();

            for (var i = 0; i < entities.Count; i += maxBatchSize)
            {
                taskCount++;

                var batchItems = entities.Skip(i)
                    .Take(maxBatchSize)
                    .ToList();

                var batch = new TableBatchOperation();

                switch (oType)
                {
                    case TableOperationType.Insert:
                        batchItems.ForEach(e => batch.Insert(e));
                        break;
                    case TableOperationType.Delete:
                        batchItems.ForEach(e => batch.Delete(e));
                        break;
                    case TableOperationType.Replace:
                        batchItems.ForEach(e => batch.Replace(e));
                        break;
                    case TableOperationType.Merge:
                        batchItems.ForEach(e => batch.Merge(e));
                        break;
                    case TableOperationType.InsertOrReplace:
                        batchItems.ForEach(e => batch.InsertOrReplace(e));
                        break;
                    case TableOperationType.InsertOrMerge:
                        batchItems.ForEach(e => batch.InsertOrMerge(e));
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(oType), oType, null);
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
