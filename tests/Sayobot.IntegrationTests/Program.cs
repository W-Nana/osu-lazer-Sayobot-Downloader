using System.Diagnostics;
using osu.Framework.Platform;
using osu.Framework.Testing;
using osu.Game;
using osu.Game.Online.API;
using osu.Game.Online.API.Requests;
using osu.Game.Online.API.Requests.Responses;
using osu.Game.Rulesets.Sayobot;

using var host = new TestRunHeadlessGameHost($"sayobot-integration-{Guid.NewGuid():N}", realtime: true);
using var game = new IntegrationGame();
Console.WriteLine("Starting isolated headless lazer 2026.1005.0; no user data or login is used.");
try
{
    host.Run(game);
    Console.WriteLine(game.Success ? "PASS: ruleset bootstrap, native event cancellation, live Sayobot download, Realm import and official fallback without a loop." : $"FAIL: {game.Failure}");
    return game.Success ? 0 : 1;
}
catch (Exception e)
{
    Console.WriteLine(e);
    return 1;
}

sealed partial class IntegrationGame : OsuGameBase
{
    private readonly Stopwatch elapsed = Stopwatch.StartNew();
    private readonly DummyAPIAccess dummyApi = new();
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

    protected override void LoadComplete()
    {
        base.LoadComplete();
        Add(dummyApi);
        BeatmapManager.ProcessBeatmap = null;
        Add(new SayobotRuleset().CreateIcon());

        Scheduler.AddDelayed(() =>
        {
            Console.WriteLine("Triggering the real native BeatmapModelDownloader.Download() API.");
            bool started = BeatmapDownloader.Download(new APIBeatmapSet
            {
                OnlineID = 41823,
                Artist = "The Quick Brown Fox",
                Title = "The Big Black",
                Beatmaps = [],
            }, false);
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
                          && fallbackRequests[1].CompletionState == APIRequestCompletionState.Waiting;
                Failure = $"Official fallback did not bypass interception exactly once; native calls: {fallbackRequests.Count}.";
                Console.WriteLine($"Missing mirror set: {fallbackRequests.Count} native requests; "
                                  + $"states: {string.Join(", ", fallbackRequests.Select(r => r.CompletionState))}.");
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
