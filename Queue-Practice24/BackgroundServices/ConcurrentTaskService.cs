
using Queue_Practice24.Context;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Queue_Practice24.BackgroundServices
{
    public class VideoUpload
    {
        public int Id { get; set; }
        public string FileName { get; set; }
        public string Status { get; set; } = "Pending"; // Pending, Processing, Completed, Abandoned
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class ConcurrentTaskService : BackgroundService
    {
        private readonly ConcurrentQueue<VideoUpload> _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<ConcurrentTaskService> _logger;

        public ConcurrentTaskService(ConcurrentQueue<VideoUpload> queue, ILogger<ConcurrentTaskService> logger, IServiceScopeFactory scopeFactory)
        {
            _queue = queue;
            _scopeFactory = scopeFactory;
            _semaphore = new SemaphoreSlim(2);
            _logger = logger;
        }

        public void EnqueueTask(VideoUpload video)
        {
            _queue.Enqueue(video);
            _logger.LogInformation($"Video '{video.FileName}'_{video.Id} added with status {video.Status}");
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                if (_queue.TryDequeue(out VideoUpload upload))
                {
                    await _semaphore.WaitAsync(stoppingToken);

                    using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(3));
                    using var scope = _scopeFactory.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    try
                    {
                        _logger.LogInformation($"Processing Task: {upload.FileName}_{upload.Id}");

                        upload.Status = "Processing";
                        await dbContext.SaveChangesAsync(cts.Token);

                        // Simulate video processing
                        await Task.Delay(5000, cts.Token);

                        // Proceed to mark the upload as completed
                        upload.Status = "Completed";
                        await dbContext.SaveChangesAsync(cts.Token);
                        _logger.LogInformation($"Task Completed: {upload.FileName}_{upload.Id}"); 
                    }
                    catch (OperationCanceledException)
                    {
                        upload.Status = "Failed";
                        await dbContext.SaveChangesAsync(cts.Token);
                        _logger.LogError($"Task Failed: {upload.FileName}_{upload.Id}");
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                else
                {
                    await Task.Delay(500); // Polling delay
                }
            }
        }
    }
}
