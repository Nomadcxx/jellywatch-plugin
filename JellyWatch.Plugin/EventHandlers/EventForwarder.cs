using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using JellyWatch.Plugin.Configuration;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using MediaBrowser.Controller.Tasks;
using MediaBrowser.Model.Activity;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Session;
using Microsoft.Extensions.Logging;

namespace JellyWatch.Plugin.EventHandlers;

/// <summary>
/// Forwards Jellyfin events to JellyWatch via HTTP POST.
/// Provides richer payloads than the standard Webhook Plugin.
/// </summary>
public class EventForwarder : IServerEntryPoint, IDisposable
{
    private readonly ILibraryManager _libraryManager;
    private readonly ISessionManager _sessionManager;
    private readonly ITaskManager _taskManager;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<EventForwarder> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="EventForwarder"/> class.
    /// </summary>
    public EventForwarder(
        ILibraryManager libraryManager,
        ISessionManager sessionManager,
        ITaskManager taskManager,
        IHttpClientFactory httpClientFactory,
        ILogger<EventForwarder> logger)
    {
        _libraryManager = libraryManager;
        _sessionManager = sessionManager;
        _taskManager = taskManager;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Runs when the server starts. Subscribes to all events.
    /// </summary>
    public Task RunAsync()
    {
        var config = JellyWatchPlugin.Instance?.Configuration;
        if (config?.EnableEventForwarding != true)
        {
            _logger.LogInformation("Event forwarding is disabled");
            return Task.CompletedTask;
        }

        if (config.ForwardLibraryEvents)
        {
            _libraryManager.ItemAdded += OnItemAdded;
            _libraryManager.ItemRemoved += OnItemRemoved;
            _libraryManager.ItemUpdated += OnItemUpdated;
        }

        if (config.ForwardPlaybackEvents)
        {
            _sessionManager.PlaybackStart += OnPlaybackStart;
            _sessionManager.PlaybackStopped += OnPlaybackStopped;
            _sessionManager.PlaybackProgress += OnPlaybackProgress;
        }

        _taskManager.TaskCompleted += OnTaskCompleted;

        _logger.LogInformation("EventForwarder initialized");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Disposes resources and unsubscribes from events.
    /// </summary>
    public void Dispose()
    {
        _libraryManager.ItemAdded -= OnItemAdded;
        _libraryManager.ItemRemoved -= OnItemRemoved;
        _libraryManager.ItemUpdated -= OnItemUpdated;
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _sessionManager.PlaybackProgress -= OnPlaybackProgress;
        _taskManager.TaskCompleted -= OnTaskCompleted;
    }

    private async void OnItemAdded(object? sender, ItemChangeEventArgs e)
    {
        if (!ShouldForwardEvent("ItemAdded")) return;
        await ForwardEvent("ItemAdded", BuildItemPayload(e.Item));
    }

    private async void OnItemRemoved(object? sender, ItemChangeEventArgs e)
    {
        if (!ShouldForwardEvent("ItemRemoved")) return;
        await ForwardEvent("ItemRemoved", BuildItemPayload(e.Item));
    }

    private async void OnItemUpdated(object? sender, ItemChangeEventArgs e)
    {
        if (!ShouldForwardEvent("ItemUpdated")) return;
        await ForwardEvent("ItemUpdated", BuildItemPayload(e.Item));
    }

    private async void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        if (!ShouldForwardEvent("PlaybackStart")) return;
        await ForwardEvent("PlaybackStart", BuildPlaybackPayload(e));
    }

    private async void OnPlaybackStopped(object? sender, PlaybackProgressEventArgs e)
    {
        if (!ShouldForwardEvent("PlaybackStopped")) return;
        await ForwardEvent("PlaybackStopped", BuildPlaybackPayload(e));
    }

    private async void OnPlaybackProgress(object? sender, PlaybackProgressEventArgs e)
    {
        // Only forward progress events every 30 seconds to avoid spam
        if (DateTime.UtcNow.Second % 30 != 0) return;
        if (!ShouldForwardEvent("PlaybackProgress")) return;
        await ForwardEvent("PlaybackProgress", BuildPlaybackPayload(e));
    }

    private async void OnTaskCompleted(object? sender, TaskCompletionEventArgs e)
    {
        if (!ShouldForwardEvent("TaskCompleted")) return;
        await ForwardEvent("TaskCompleted", BuildTaskCompletedPayload(e));
    }

    private bool ShouldForwardEvent(string eventType)
    {
        var config = JellyWatchPlugin.Instance?.Configuration;
        return config?.EnableEventForwarding == true;
    }

    private object BuildItemPayload(BaseItem item)
    {
        var payload = new
        {
            EventType = "ItemChanged",
            Timestamp = DateTime.UtcNow.ToString("O"),
            Item = new
            {
                Id = item.Id.ToString(),
                Name = item.Name,
                Path = item.Path,
                Type = item.GetType().Name,
                ProviderIds = item.ProviderIds,
                IsIdentified = item.ProviderIds.Count > 0,
                LibraryName = item.GetParent()?.Name,
                ParentId = item.ParentId?.ToString(),
                HasLocalTrailer = item.LocalTrailerIds.Count > 0,
                HasSubtitles = item.GetMediaStreams().Any(s => s.Type == MediaStreamType.Subtitle),
                PrimaryImagePath = item.GetImagePath(ImageType.Primary),
                DateCreated = item.DateCreated?.ToString("O"),
                DateModified = item.DateModified?.ToString("O")
            }
        };
        return payload;
    }

    private object BuildPlaybackPayload(PlaybackProgressEventArgs e)
    {
        var item = e.Item;
        var user = e.Session.User;

        var payload = new
        {
            EventType = "Playback",
            Timestamp = DateTime.UtcNow.ToString("O"),
            Session = new
            {
                Id = e.Session.Id,
                DeviceId = e.Session.DeviceId,
                DeviceName = e.Session.DeviceName,
                Client = e.Session.Client,
                UserId = user?.Id.ToString(),
                UserName = user?.Name
            },
            Item = item != null ? new
            {
                Id = item.Id.ToString(),
                Name = item.Name,
                Path = item.Path,
                Type = item.GetType().Name
            } : null,
            Playback = new
            {
                PositionTicks = e.PlaybackPositionTicks,
                DurationTicks = item?.RunTimeTicks,
                IsPaused = e.IsPaused,
                AudioStreamIndex = e.AudioStreamIndex,
                SubtitleStreamIndex = e.SubtitleStreamIndex
            }
        };
        return payload;
    }

    private object BuildTaskCompletedPayload(TaskCompletionEventArgs e)
    {
        var task = ReadPropertyValue(e, "Task");
        var result = ReadPropertyValue(e, "Result");

        return new
        {
            EventType = "TaskCompleted",
            Timestamp = DateTime.UtcNow.ToString("O"),
            Task = new
            {
                Id = ReadPropertyValue(task, "Id")?.ToString(),
                Name = ReadPropertyValue(task, "Name")?.ToString()
                    ?? ReadPropertyValue(result, "Name")?.ToString(),
                Key = ReadPropertyValue(task, "Key")?.ToString(),
                Category = ReadPropertyValue(task, "Category")?.ToString()
            },
            Result = new
            {
                Status = ReadPropertyValue(result, "Status")?.ToString()
                    ?? ReadPropertyValue(result, "State")?.ToString(),
                StartTimeUtc = FormatDate(ReadPropertyValue(result, "StartTimeUtc")
                    ?? ReadPropertyValue(result, "StartTime")),
                EndTimeUtc = FormatDate(ReadPropertyValue(result, "EndTimeUtc")
                    ?? ReadPropertyValue(result, "EndTime")),
                ErrorMessage = ReadPropertyValue(result, "ErrorMessage")?.ToString(),
                LongErrorMessage = ReadPropertyValue(result, "LongErrorMessage")?.ToString()
            }
        };
    }

    private static object? ReadPropertyValue(object? source, string propertyName)
    {
        return source?.GetType().GetProperty(propertyName)?.GetValue(source);
    }

    private static string? FormatDate(object? value)
    {
        return value switch
        {
            DateTime dateTime => dateTime.ToString("O"),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToString("O"),
            _ => value?.ToString()
        };
    }

    private async Task ForwardEvent(string eventType, object payload)
    {
        var config = JellyWatchPlugin.Instance?.Configuration;
        if (config == null) return;

        var url = $"{config.JellyWatchUrl.TrimEnd('/')}/api/v1/webhooks/jellyfin";
        var maxRetries = config.RetryCount;
        var timeout = TimeSpan.FromSeconds(config.RequestTimeoutSeconds);

        var requestPayload = new
        {
            EventType = eventType,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Payload = payload
        };

        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = timeout;

                var json = JsonSerializer.Serialize(requestPayload, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                var request = new HttpRequestMessage(HttpMethod.Post, url)
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                request.Headers.Add("X-JellyWatch-Secret", config.SharedSecret);
                request.Headers.Add("X-Jellyfin-Event", eventType);

                var response = await client.SendAsync(request);
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Forwarded {EventType} event to JellyWatch", eventType);
                    return;
                }

                _logger.LogWarning("Failed to forward {EventType} event: {StatusCode}", 
                    eventType, response.StatusCode);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error forwarding {EventType} event (attempt {Attempt}/{Max})",
                    eventType, attempt + 1, maxRetries);
            }

            if (attempt < maxRetries - 1)
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
            }
        }

        _logger.LogError("Failed to forward {EventType} event after {MaxRetries} attempts", 
            eventType, maxRetries);
    }
}
