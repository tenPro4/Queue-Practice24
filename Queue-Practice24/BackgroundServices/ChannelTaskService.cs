
using Microsoft.AspNetCore.Mvc.RazorPages.Infrastructure;
using System.Threading.Channels;

namespace Queue_Practice24.BackgroundServices
{
    public class TriggerEmail
    {
        public string Subject { get; set; }
    }

    public class ChannelTaskService : BackgroundService
{
    private readonly Channel<TriggerEmail> _channel;  // Inject the entire Channel<TriggerEmail>
    private readonly ILogger<ChannelTaskService> _logger;

    public ChannelTaskService(Channel<TriggerEmail> channel, ILogger<ChannelTaskService> logger)
    {
        _channel = channel;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Use the Reader property of the injected channel
        var channelReader = _channel.Reader;

        while (await channelReader.WaitToReadAsync(stoppingToken))
        {
            var email = await channelReader.ReadAsync(stoppingToken);

            await Task.Delay(5000); // Simulate some work

            _logger.LogInformation($"Email {email.Subject} Sent!");
        }
    }
}
}
