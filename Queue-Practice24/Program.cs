using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Queue_Practice24.BackgroundServices;
using Queue_Practice24.Context;
using System.Collections.Concurrent;
using System.Threading.Channels;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseInMemoryDatabase("QueueDb"));

//builder.Services.AddHostedService<LowPriorityTaskGenerator>();
builder.Services.AddSingleton<PriorityTaskService>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<PriorityTaskService>());

builder.Services.AddSingleton<ConcurrentQueue<VideoUpload>>(); // Register ConcurrentQueue
builder.Services.AddSingleton<ConcurrentTaskService>(); // Register ConcurrentTaskService as a singleton
builder.Services.AddHostedService(provider => provider.GetRequiredService<ConcurrentTaskService>()); // Register ConcurrentTaskService as a hosted service

builder.Services.AddSingleton(_ => Channel.CreateUnbounded<TriggerEmail>()); // Register Channel
builder.Services.AddHostedService<ChannelTaskService>();

var app = builder.Build();

var priorityTaskService = app.Services.GetRequiredService<PriorityTaskService>();
var concurrentTaskService = app.Services.GetRequiredService<ConcurrentTaskService>();

var scope = app.Services.CreateScope();
var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
var channel = app.Services.GetService<Channel<TriggerEmail>>();

for (int i = 0; i < 5; i++)
{
    var email = new TriggerEmail
    {
        Subject = $"subject {i + 1}"
    };
    Console.WriteLine($"Channel Writer: Enqueuing Email with {email.Subject}");
    await channel.Writer.WriteAsync(email);
    await Task.Delay(100);
}

app.MapPost("/task", ([FromBody]TaskItem item) =>
{
    var task = new TaskItem
    {
        Name = item.Name,
        Priority = item.Priority
    };

    priorityTaskService.EnqueueTask(task);
    
    return Results.Ok(task);
});

app.MapPost("/video", async ([FromBody] VideoUpload item) =>
{
    var video = new VideoUpload
    {
        FileName = item.FileName,
    };

    dbContext.Add(video);
    await dbContext.SaveChangesAsync();

    concurrentTaskService.EnqueueTask(video);

    return Results.Ok(video);
});

app.MapGet("/video/{id:int}", async (int id) =>
{
    var video = await dbContext.VideoUploads.FindAsync(id);

    if (video == null)
    {
        return Results.NotFound(new { Message = "Video not found" });
    }

    return Results.Ok(video);
});

app.Run();