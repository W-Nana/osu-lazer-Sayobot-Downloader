using System.Runtime.CompilerServices;
using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Logging;
using osu.Framework.Localisation;
using osu.Game.Beatmaps;
using osu.Game.Database;
using osu.Game.Extensions;
using osu.Game.Online.API;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Rulesets.Sayobot.Configuration;
using osu.Game.Rulesets.Sayobot.Download;
using osu.Game.Rulesets.Sayobot.Localisation;
using static osu.Game.Rulesets.Sayobot.Localisation.SayobotStrings;
using NativeNotificationsStrings = osu.Game.Localisation.NotificationsStrings;

namespace osu.Game.Rulesets.Sayobot.UI;

/// <summary>Lives under the game, so changing back to another ruleset does not stop the service.</summary>
internal sealed partial class SayobotDownloadComponent(SayobotRuleset ruleset) : Component
{
    private static readonly ConditionalWeakTable<OsuGameBase, SayobotDownloadComponent> installations = new();
    private static readonly object installationLock = new();
    private readonly Dictionary<int, Job> jobs = [];
    private readonly HashSet<int> officialFallbacks = [];
    private readonly List<NativeCancellation> nativeCancellations = [];
    private readonly SemaphoreSlim downloadSlots = new(2);
    private readonly CancellationTokenSource lifetime = new();
    private SayobotConfigManager config = null!;
    private bool disposed;

    [Resolved]
    private BeatmapModelDownloader downloader { get; set; } = null!;

    [Resolved]
    private BeatmapManager importer { get; set; } = null!;

    [Resolved]
    private IRulesetConfigCache configCache { get; set; } = null!;

    [Resolved(CanBeNull = true)]
    private INotificationOverlay? notifications { get; set; }

    public static SayobotDownloadComponent EnsureInstalled(OsuGameBase game, SayobotRuleset ruleset)
    {
        lock (installationLock)
        {
            if (installations.TryGetValue(game, out var existing) && !existing.disposed)
                return existing;

            var component = new SayobotDownloadComponent(ruleset) { AlwaysPresent = true };
            installations.Remove(game);
            installations.Add(game, component);
            game.Add(component);
            return component;
        }
    }

    protected override void LoadComplete()
    {
        base.LoadComplete();
        config = (SayobotConfigManager)configCache.GetConfigFor(ruleset)!;
        downloader.DownloadBegan += downloadBegan;
        notify(Get(SayobotString.Loaded));
        Logger.Log("Sayobot Download: subscribed to the public BeatmapModelDownloader.DownloadBegan event.");
    }

    public void DownloadManually(int setId) => Schedule(() =>
    {
        if (!disposed)
            start(new BeatmapSetInfo { OnlineID = setId }, null);
    });

    private void downloadBegan(ArchiveDownloadRequest<IBeatmapSetInfo> request) => Schedule(() =>
    {
        // DownloadBegan is a notification after the original request has been launched, not a pre-request hook.
        if (disposed)
            return;
        int setId = request.Model.OnlineID;
        if (officialFallbacks.Remove(setId) || setId <= 0 || !config.Get<bool>(SayobotSetting.Enabled))
            return;
        if (request.CompletionState != APIRequestCompletionState.Waiting)
            return;

        if (!jobs.ContainsKey(setId))
            start(request.Model, request);
        else
            cancelNativeDownload(request);
    });

    private void cancelNativeDownload(ArchiveDownloadRequest<IBeatmapSetInfo> request)
    {
        if (notifications != null)
        {
            // The native downloader does not expose its notification on the request. Match its
            // public localisable caption, preserving identity across UI language changes.
            var caption = NativeNotificationsStrings.Downloading(request.Model.GetDisplayString());
            var existing = notifications.AllNotifications.OfType<ProgressNotification>()
                                        .Where(n => !n.WasClosed && n.Text == caption).ToArray();
            var ongoing = existing.Where(n => n.Ongoing).ToArray();
            // Identical display titles can be ambiguous. Leave those notifications alone.
            if (ongoing.Length <= 1)
                nativeCancellations.Add(new NativeCancellation(caption, ongoing.SingleOrDefault(), existing.ToHashSet()));
        }

        request.Cancel();
        dismissNativeCancellations();
    }

    protected override void Update()
    {
        base.Update();
        dismissNativeCancellations();
    }

    private void dismissNativeCancellations()
    {
        if (notifications == null || nativeCancellations.Count == 0)
            return;

        // NotificationOverlay.Post() can be deferred, for example during gameplay. Keep tracking
        // until the cancelled original appears, even if the mirror job has already finished.
        var displayed = notifications.AllNotifications.OfType<ProgressNotification>().ToArray();
        foreach (var cancellation in nativeCancellations.ToArray())
        {
            var notification = cancellation.Notification;
            if (notification == null)
            {
                var candidates = displayed.Where(n => !n.WasClosed
                    && n.State == ProgressNotificationState.Cancelled && n.Text == cancellation.Caption
                    && !cancellation.PreviousNotifications.Contains(n)).ToArray();
                if (candidates.Length > 1)
                {
                    nativeCancellations.Remove(cancellation);
                    continue;
                }
                notification = candidates.SingleOrDefault();
            }

            if (notification == null)
                continue;
            if (notification.State == ProgressNotificationState.Cancelled)
            {
                notification.Read = true;
                notification.Close(false);
            }
            if (notification.WasClosed || notification.State == ProgressNotificationState.Completed)
                nativeCancellations.Remove(cancellation);
        }
    }

    private void start(IBeatmapSetInfo model, ArchiveDownloadRequest<IBeatmapSetInfo>? originalRequest)
    {
        int setId = model.OnlineID;
        if (jobs.ContainsKey(setId))
        {
            notify(Get(SayobotString.AlreadyQueued, setId));
            return;
        }

        // Realm-managed fields are captured on the update thread. Only detached values go to the worker.
        BeatmapSetInfo? updateSnapshot = model is BeatmapSetInfo { IsManaged: true } original
            ? new BeatmapSetInfo { ID = original.ID, DateAdded = original.DateAdded }
            : null;
        string[] hashes = updateSnapshot == null
            ? model.Beatmaps.Select(b => b.MD5Hash).Where(isMd5).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
            : [];
        var beatmapIds = model.Beatmaps.Where(b => b.OnlineID > 0 && isMd5(b.MD5Hash))
                              .GroupBy(b => b.MD5Hash, StringComparer.OrdinalIgnoreCase)
                              .ToDictionary(group => group.Key, group => group.First().OnlineID, StringComparer.OrdinalIgnoreCase);

        var job = new Job(
            model,
            updateSnapshot,
            new DownloadSpecification(setId, config.Get<bool>(SayobotSetting.NoVideo), hashes),
            beatmapIds,
            config.Get<bool>(SayobotSetting.UseSystemProxy),
            config.Get<bool>(SayobotSetting.FallbackToOfficial),
            CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token));
        var notification = job.Notification;
        notification.Text = Get(SayobotString.Queued, setId);
        notification.CompletionText = Get(SayobotString.Completed, setId);
        notification.CancelRequested = () =>
        {
            // ImportAsUpdate has no cancellation-token parameter. Once import begins, let it finish.
            if (Volatile.Read(ref job.ImportStarted) != 0)
                return false;
            job.Cancellation.Cancel();
            return true;
        };

        jobs.Add(setId, job);
        if (originalRequest != null)
            cancelNativeDownload(originalRequest);
        notifications?.Post(notification);
        _ = Task.Run(() => runAsync(job));
    }

    private async Task runAsync(Job job)
    {
        int setId = job.Specification.SetId;
        string path = Path.Combine(Path.GetTempPath(), $"sayobot-{setId}-{Guid.NewGuid():N}.osz");
        bool slotTaken = false;
        bool needsOfficialFallback = false;
        try
        {
            await downloadSlots.WaitAsync(job.Cancellation.Token).ConfigureAwait(false);
            slotTaken = true;
            post(() =>
            {
                job.Notification.State = ProgressNotificationState.Active;
                job.Notification.Text = Get(SayobotString.Downloading, setId);
            });

            using var client = new SayobotClient(job.UseSystemProxy);
            await client.DownloadAsync(job.Specification, path, new InlineProgress(p => post(() =>
            {
                if (!job.Notification.Ongoing)
                    return;
                job.Notification.Progress = p.TotalBytes is > 0 ? (float)p.Bytes / p.TotalBytes.Value : 0;
                job.Notification.Text = p.TotalBytes is > 0
                    ? Get(SayobotString.DownloadProgressWithTotal, setId, p.Bytes / 1048576d, p.TotalBytes.Value / 1048576d)
                    : Get(SayobotString.DownloadProgress, setId, p.Bytes / 1048576d);
            })), job.Cancellation.Token).ConfigureAwait(false);

            Interlocked.Exchange(ref job.ImportStarted, 1);
            job.Cancellation.Token.ThrowIfCancellationRequested();
            post(() => job.Notification.Text = Get(SayobotString.Importing, setId));

            List<Live<BeatmapSetInfo>> importedModels;
            if (job.UpdateSnapshot != null)
            {
                var updated = await importer.ImportAsUpdate(job.Notification, new ImportTask(path), job.UpdateSnapshot).ConfigureAwait(false);
                importedModels = updated == null ? [] : [updated];
            }
            else
                importedModels = (await importer.Import(job.Notification, [new ImportTask(path)]).ConfigureAwait(false)).ToList();

            if (importedModels.Count == 0)
                throw new InvalidDataException("lazer did not import any beatmaps from the downloaded archive.");

            // Some old .osu files omit online IDs. Supply known IDs through the public Realm wrapper
            // so native availability trackers can observe the set without waiting for a metadata lookup.
            foreach (var imported in importedModels)
            {
                imported.PerformWrite(set =>
                {
                    if (set.OnlineID <= 0)
                        set.OnlineID = setId;
                    foreach (var beatmap in set.Beatmaps)
                        if (beatmap.OnlineID <= 0 && job.BeatmapIds.TryGetValue(beatmap.MD5Hash, out int id))
                            beatmap.OnlineID = id;
                });
            }

            Logger.Log($"Sayobot Download: imported beatmap set {setId}.");
        }
        catch (OperationCanceledException) when (job.Cancellation.IsCancellationRequested)
        {
            post(() =>
            {
                job.Notification.Text = Get(SayobotString.Cancelled, setId);
                job.Notification.State = ProgressNotificationState.Cancelled;
            });
        }
        catch (Exception e)
        {
            needsOfficialFallback = !lifetime.IsCancellationRequested && job.FallbackToOfficial
                                    && Volatile.Read(ref job.ImportStarted) == 0;
            logLocalFailure(e, $"Sayobot Download: beatmap set {setId} failed.");
            bool fallback = needsOfficialFallback;
            post(() =>
            {
                job.Notification.Text = Get(SayobotString.Failed, setId,
                    SayobotErrors.Describe(e, Volatile.Read(ref job.ImportStarted) != 0));
                job.Notification.State = ProgressNotificationState.Cancelled;
                notify(Get(fallback ? SayobotString.FallingBack : SayobotString.FailureDetails, setId));
            });
        }
        finally
        {
            SayobotClient.TryDelete(path);
            if (slotTaken)
                downloadSlots.Release();
            bool fallback = needsOfficialFallback;
            post(() =>
            {
                jobs.Remove(setId);
                job.Notification.CancelRequested = null;
                job.Cancellation.Dispose();
                if (fallback)
                    queueOfficialFallback(job, 0);
            });
        }
    }

    private void queueOfficialFallback(Job job, int attempt)
    {
        int setId = job.Specification.SetId;
        if (disposed || jobs.ContainsKey(setId))
            return;

        // The original cancellation is delivered on the API scheduler. Wait for its bookkeeping to finish.
        if (downloader.GetExistingDownload(job.OriginalModel) != null)
        {
            if (attempt < 20)
                Scheduler.AddDelayed(() => queueOfficialFallback(job, attempt + 1), 100);
            else
                notify(Get(SayobotString.OfficialCleanupPending, setId));
            return;
        }

        officialFallbacks.Add(setId);
        try
        {
            if (job.UpdateSnapshot != null && job.OriginalModel is BeatmapSetInfo original)
                downloader.DownloadAsUpdate(original, job.Specification.NoVideo);
            else if (!downloader.Download(job.OriginalModel, job.Specification.NoVideo))
                officialFallbacks.Remove(setId);
        }
        catch (Exception e)
        {
            officialFallbacks.Remove(setId);
            logLocalFailure(e, $"Sayobot Download: could not start official fallback for {setId}.");
            notify(Get(SayobotString.OfficialStartFailed, setId));
        }
    }

    private void post(Action action)
    {
        if (!lifetime.IsCancellationRequested)
            Schedule(() => { if (!disposed) action(); });
    }

    private void notify(LocalisableString message) => notifications?.Post(new SimpleNotification { Text = message });

    // Logger.Error() forwards exceptions to lazer's notification and Sentry listeners.
    // Retain diagnostics in runtime.log without emitting an event to those listeners.
    private static void logLocalFailure(Exception exception, string description)
        => Logger.Log($"{description}\n{exception}", level: LogLevel.Verbose, outputToListeners: false);

    private static bool isMd5(string? hash)
        => hash is { Length: 32 } && hash.All(Uri.IsHexDigit);

    protected override void Dispose(bool isDisposing)
    {
        if (isDisposing && !disposed)
        {
            disposed = true;
            if (downloader != null)
                downloader.DownloadBegan -= downloadBegan;
            lifetime.Cancel();
            nativeCancellations.Clear();
        }
        base.Dispose(isDisposing);
    }

    private sealed class InlineProgress(Action<DownloadProgress> report) : IProgress<DownloadProgress>
    {
        public void Report(DownloadProgress value) => report(value);
    }

    private sealed record NativeCancellation(LocalisableString Caption, ProgressNotification? Notification,
                                              HashSet<ProgressNotification> PreviousNotifications);

    private sealed class Job(IBeatmapSetInfo originalModel, BeatmapSetInfo? updateSnapshot,
                             DownloadSpecification specification, IReadOnlyDictionary<string, int> beatmapIds, bool useSystemProxy,
                             bool fallbackToOfficial, CancellationTokenSource cancellation)
    {
        public IBeatmapSetInfo OriginalModel { get; } = originalModel;
        public BeatmapSetInfo? UpdateSnapshot { get; } = updateSnapshot;
        public DownloadSpecification Specification { get; } = specification;
        public IReadOnlyDictionary<string, int> BeatmapIds { get; } = beatmapIds;
        public bool UseSystemProxy { get; } = useSystemProxy;
        public bool FallbackToOfficial { get; } = fallbackToOfficial;
        public CancellationTokenSource Cancellation { get; } = cancellation;
        public ProgressNotification Notification { get; } = new();
        public int ImportStarted;
    }
}
