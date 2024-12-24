
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using System.Diagnostics;
using System.Threading.Channels;

namespace Queue_Practice24.BackgroundServices
{
    public class TriggerEmail
    {
        public string Subject { get; set; }
    }

    public class ChannelTaskService : BackgroundService
    {
        private readonly SemaphoreSlim _semaphore;
        private readonly Channel<TriggerEmail> _channel;
        private readonly ILogger<ChannelTaskService> _logger;

        public ChannelTaskService(Channel<TriggerEmail> channel, ILogger<ChannelTaskService> logger)
        {
            _channel = channel;
            _logger = logger;
            _semaphore = new SemaphoreSlim(2); //optional semaphore to limit the number of concurrent tasks
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            // Use the Reader property of the injected channel
            var channelReader = _channel.Reader;

            while (await channelReader.WaitToReadAsync(stoppingToken))
            {
                //process one email at a time sequentially
                //await keywords make it wait for each operation to finish before moving to the next iteration
                var email = await channelReader.ReadAsync(stoppingToken);

                await Task.Delay(5000); // Simulate some work

                _logger.LogInformation($"Email {email.Subject} Sent!");
            }
        }
    }
}
