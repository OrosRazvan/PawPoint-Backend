using PawPoint.DB;
using PawPoint.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ThreadingTask = System.Threading.Tasks.Task;

namespace PawPoint.Services.Jobs
{
    public class DataUpdateProcessingJob(ILogger<DataUpdateProcessingJob> logger, IDataUpdateProcessingService dataUpdateProcessingService, Context context)
    {
        public async ThreadingTask RunAsync()
        {
            var unprocessedFileNames = await context.ImportManifests
                .Where(m => !m.IsProcessed)
                .Select(m => m.FileName)
                .ToListAsync();

            if (!unprocessedFileNames.Any())
            {
                return;
            }

            logger.LogInformation("Found {Count} unprocessed files to process.", unprocessedFileNames.Count);

            foreach (var fileName in unprocessedFileNames)
            {
                try
                {
                    await dataUpdateProcessingService.ProcessDataUpdateBlobAsync(fileName);
                }
                catch (System.Exception ex)
                {
                    logger.LogError(ex, "Failed to process blob {FileName}. Continuing to the next file.", fileName);
                }
            }
        }
    }
}
