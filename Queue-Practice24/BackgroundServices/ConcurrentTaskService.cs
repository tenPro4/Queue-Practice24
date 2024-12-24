
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
        public DateTime CompletedAt { get; set; } = DateTime.UtcNow;
    }

    public class ConcurrentTaskService : BackgroundService
    {
        private readonly ConcurrentQueue<VideoUpload> _queue;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly SemaphoreSlim _semaphore;
        private readonly ILogger<ConcurrentTaskService> _logger;
        private readonly ManualResetEventSlim _newItemEvent;

        public ConcurrentTaskService(
            ILogger<ConcurrentTaskService> logger,
            IServiceScopeFactory scopeFactory)
        {
            _queue = new ConcurrentQueue<VideoUpload>();
            _scopeFactory = scopeFactory;
            _semaphore = new SemaphoreSlim(2);
            _logger = logger;
            _newItemEvent = new ManualResetEventSlim(false);
        }

        public void EnqueueTask(VideoUpload video)
        {
            _queue.Enqueue(video);
            _logger.LogInformation("Video '{FileName}' queued for processing", video.FileName);
            _newItemEvent.Set(); // Signal that a new item is available
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    if (_queue.IsEmpty)
                    {
                        _newItemEvent.Reset();
                        await Task.Run(() => _newItemEvent.Wait(TimeSpan.FromSeconds(1)), stoppingToken);
                        continue;
                    }

                    if (_queue.TryDequeue(out var video))
                    {
                        // Start processing without awaiting to allow concurrent execution
                        _ = ProcessVideoAsync(video, stoppingToken)
                            .ContinueWith(
                                t => LogTaskCompletion(t, video),
                                TaskContinuationOptions.ExecuteSynchronously);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Service shutdown requested");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while processing queue");
            }
        }

        private async Task ProcessVideoAsync(VideoUpload video, CancellationToken stoppingToken)
        {
            await _semaphore.WaitAsync(stoppingToken);

            try
            {
                using var scope = _scopeFactory.CreateScope();
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
                cts.CancelAfter(TimeSpan.FromMinutes(3));

                var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                video.Status = "Processing";
                await dbContext.AddAsync(video, cts.Token);
                await dbContext.SaveChangesAsync(cts.Token);

                _logger.LogInformation("Started processing video: {FileName}", video.FileName);

                // Simulate video processing
                await Task.Delay(5000, cts.Token);

                video.Status = "Completed";
                video.CompletedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cts.Token);

                _logger.LogInformation("Completed processing video: {FileName}", video.FileName);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private void LogTaskCompletion(Task task, VideoUpload video)
        {
            if (task.IsFaulted)
            {
                _logger.LogError(
                    task.Exception?.InnerException,
                    "Failed to process video: {FileName}",
                    video.FileName);
            }
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            _newItemEvent.Set(); // Ensure the processing loop can exit
            await base.StopAsync(cancellationToken);
        }
    }
}
