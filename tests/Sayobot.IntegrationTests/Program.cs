using System.Collections.Concurrent;
using System.Diagnostics;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Logging;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Extensions;
using osu.Game.Localisation;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Sayobot;

using var host = new TestRunHeadlessGameHost($"sayobot-integration-{Guid.NewGuid():N}", realtime: true);
using var game = new IntegrationGame();
var forwardedLogs = new ConcurrentQueue<LogEntry>();
Logger.NewEntry += forwardedLogs.Enqueue;
Console.WriteLine("Starting isolated headless lazer 2026.1005.0; no user data or login is used.");
try
{
    host.Run(game);
    if (game.Success)
    {
        Logger.Flush();
        string failureMessage = $"{SayobotRuleset.ProjectName}: beatmap set {int.MaxValue} failed.";
        string log = File.ReadAllText(Logger.Storage.GetFullPath(Logger.GetLogger(LoggingTarget.Runtime).Filename));
        if (!log.Contains(failureMessage) || forwardedLogs.Any(e => e.Message.Contains(failureMessage)))
            throw new Exception("Mirror failure must remain in the local runtime log without reaching Logger.NewEntry / Sentry listeners.");
        Console.WriteLine("PASS: mirror failure retained in runtime.log without a notification / reporting log event.");
    }
    Console.WriteLine(game.Success ? "PASS: ruleset bootstrap, native cancellation cleanup, live Sayobot download, Realm import and official fallback without a loop." : $"FAIL: {game.Failure}");
    return game.Success ? 0 : 1;
}
catch (Exception e)
{
    Console.WriteLine(e);
    return 1;
}
finally
{
    Logger.NewEntry -= forwardedLogs.Enqueue;
}

sealed partial class IntegrationGame : OsuGameBase
{
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly DummyAPIAccess dummyApi = new();
    private readonly RecordingNotificationOverlay notifications = new();
    private readonly List<ProgressNotification> nativeNotifications = [];
    private ProgressNotification? previousCancellation;
    private readonly ProgressNotification unrelatedCancellation = new() { Text = "Another component's cancelled task", State = ProgressNotificationState.Cancelled };
    private APIRequest? originalRequest;
    private readonly List<APIRequest> fallbackRequests = [];
    private bool checkedImport;
    private bool checkingFallback;
    private bool fallbackResultScheduled;
    public bool Success { get; private set; }
    public string Failure { get; private set; } = "The host stopped before import completed.";

    public IntegrationGame()
    {
        API = dummyApi;
        dummyApi.HandleRequest = request =>
        {
            if (request is DownloadBeatmapSetRequest)
            {
                if (checkingFallback)
                    fallbackRequests.Add(request);
                else
                    originalRequest = request;
            }
            return true;
        };
    }

    protected override IReadOnlyDependencyContainer CreateChildDependencies(IReadOnlyDependencyContainer parent)
    {
        var dependencies = new DependencyContainer(base.CreateChildDependencies(parent));
        dependencies.CacheAs<INotificationOverlay>(notifications);
        return dependencies;
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Add(dummyApi);
        BeatmapManager.ProcessBeatmap = null;
        BeatmapDownloader.PostNotification = notification =>
        {
            nativeNotifications.Add((ProgressNotification)notification);
            // Keep the first native notification hidden from AllNotifications until after
            // cancellation, exercising NotificationOverlay's deferred-post behaviour.
            if (nativeNotifications.Count == 1)
                Scheduler.AddDelayed(() => notifications.Post(notification), 250);
            else
                notifications.Post(notification);
        };
        notifications.Post(unrelatedCancellation);
        Add(new SayobotRuleset().CreateIcon());

        Scheduler.AddDelayed(() =>
        {
            Console.WriteLine("Triggering the real native BeatmapModelDownloader.Download() API.");
            var model = new APIBeatmapSet
            {
                OnlineID = 41823,
                Artist = "The Quick Brown Fox",
                Title = "The Big Black",
                Beatmaps = [],
            };
            previousCancellation = new ProgressNotification
            {
                Text = NotificationsStrings.Downloading(model.GetDisplayString()),
                State = ProgressNotificationState.Cancelled,
            };
            notifications.Post(previousCancellation);
            bool started = BeatmapDownloader.Download(model, false);
            if (!started)
            {
                Failure = "The native downloader refused to start.";
                Host.Exit();
            }
        }, 1000);
    }

    protected override void Update()
    {
        base.Update();
        if (elapsed.Elapsed > TimeSpan.FromSeconds(90))
        {
            Failure = $"Timed out; original request state: {originalRequest?.CompletionState}.";
            Host.Exit();
            return;
        }

        if (checkingFallback && fallbackRequests.Count >= 2 && !fallbackResultScheduled)
        {
            fallbackResultScheduled = true;
            Scheduler.AddDelayed(() =>
            {
                Success = fallbackRequests.Count == 2
                          && fallbackRequests[0].CompletionState == APIRequestCompletionState.Failed
                          && fallbackRequests[1].CompletionState == APIRequestCompletionState.Waiting
                          && nativeNotifications.Count == 3
                          && nativeNotifications[0].WasClosed && nativeNotifications[0].Read
                          && nativeNotifications[1].WasClosed && nativeNotifications[1].Read
                          && !nativeNotifications[2].WasClosed && nativeNotifications[2].Ongoing
                          && previousCancellation is { WasClosed: false, Read: false }
                          && !unrelatedCancellation.WasClosed;
                Failure = $"Official fallback or notification cleanup failed; native calls: {fallbackRequests.Count}; "
                          + $"native notifications closed: {string.Join(", ", nativeNotifications.Select(n => n.WasClosed))}.";
                Console.WriteLine($"Missing mirror set: {fallbackRequests.Count} native requests; "
                                  + $"states: {string.Join(", ", fallbackRequests.Select(r => r.CompletionState))}.");
                Console.WriteLine($"Native notifications closed: {string.Join(", ", nativeNotifications.Select(n => n.WasClosed))}; "
                                  + $"old matching notification closed: {previousCancellation?.WasClosed}; unrelated closed: {unrelatedCancellation.WasClosed}.");
                foreach (var request in fallbackRequests)
                    request.Cancel();
                Host.Exit();
            }, 500);
        }

        if (checkedImport || originalRequest == null)
            return;

        var imported = BeatmapManager.QueryBeatmapSet(s => s.OnlineID == 41823 && !s.DeletePending);
        if (imported == null)
            return;

        int difficulties = imported.PerformRead(s => s.Beatmaps.Count);
        if (difficulties < 2)
            return;

        checkedImport = true;
        Console.WriteLine($"Realm import found {difficulties} difficulties; original request state: {originalRequest.CompletionState}.");
        Scheduler.AddDelayed(() =>
        {
            if (originalRequest.CompletionState != APIRequestCompletionState.Failed)
            {
                Failure = "The original native request was not cancelled.";
                Host.Exit();
                return;
            }
            checkingFallback = true;
            Console.WriteLine("Testing a missing mirror set and a single official fallback; official API remains mocked.");
            BeatmapDownloader.Download(new APIBeatmapSet
            {
                OnlineID = int.MaxValue,
                Artist = "test",
                Title = "missing mirror set",
                Beatmaps = [],
            }, false);
        }, 500);
    }
}

sealed class RecordingNotificationOverlay : INotificationOverlay
{
    private readonly List<Notification> notifications = [];
    public IEnumerable<Notification> AllNotifications => notifications;
    public IBindable<int> UnreadCount { get; } = new Bindable<int>();
    public void Post(Notification notification) => notifications.Add(notification);
    public void Hide() { }
}
