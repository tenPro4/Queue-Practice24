namespace Queue_Practice24.BackgroundServices
{
    public class TaskItem
    {
        public string Name { get; set; }
        public int Priority { get; set; } // Lower number = Higher priority
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class LowPriorityTaskGenerator : BackgroundService
    {
        private readonly PriorityTaskService _taskService;
        private int _taskCount = 1;

        public LowPriorityTaskGenerator(PriorityTaskService taskService)
        {
            _taskService = taskService;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var task = new TaskItem
                {
                    Name = $"LowPriorityTask-{_taskCount++}",
                    Priority = 10 // Lowest priority
                };

                _taskService.EnqueueTask(task);
                await Task.Delay(5000, stoppingToken); // Generate every 5 seconds
            }
        }
    }

    public class PriorityTaskService: BackgroundService
    {
        private readonly PriorityQueue<TaskItem, int> _taskQueue;
        private readonly ILogger<PriorityTaskService> _logger;
        private readonly SemaphoreSlim _semaphore;

        public PriorityTaskService(ILogger<PriorityTaskService> logger)
        {
            _taskQueue = new PriorityQueue<TaskItem, int>();
            _logger = logger;
            _semaphore = new SemaphoreSlim(1, 1); // Only one task processed at a time
        }

        public void EnqueueTask(TaskItem task)
        {
            lock (_taskQueue)
            {
                _taskQueue.Enqueue(task, task.Priority);
                _logger.LogInformation($"Task '{task.Name}' added with priority {task.Priority}");
            }
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                TaskItem? task = null;

                lock (_taskQueue)
                {
                    if (_taskQueue.Count > 0)
                    {
                        task = _taskQueue.Dequeue();
                    }
                }

                if (task != null)
                {
                    await _semaphore.WaitAsync(stoppingToken);
                    try
                    {
                        _logger.LogInformation($"Processing Task: {task.Name} (Priority {task.Priority})");
                        await Task.Delay(2000, stoppingToken); // Simulate work
                        _logger.LogInformation($"Task Completed: {task.Name}");
                    }
                    finally
                    {
                        _semaphore.Release();
                    }
                }
                else
                {
                    await Task.Delay(1000, stoppingToken); // Polling interval
                }
            }
        }
    }
}
