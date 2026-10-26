using System.Buffers;
using System.Globalization;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;

namespace osu.Game.Rulesets.Sayobot.Download;

internal sealed record DownloadProgress(long Bytes, long? TotalBytes);

internal sealed record DownloadSpecification(int SetId, bool NoVideo, IReadOnlyCollection<string> ExpectedMd5Hashes);

/// <summary>
/// Owns its HTTP stack. No osu! API credentials, cookies or shared HTTP handlers are used.
/// </summary>
internal sealed class SayobotClient : IDisposable
{
    private const long maxArchiveBytes = 2L * 1024 * 1024 * 1024;
    private const int maxBeatmapBytes = 16 * 1024 * 1024;
    private static readonly TimeSpan idleTimeout = TimeSpan.FromSeconds(45);
    private readonly HttpClient client;

    public SayobotClient(bool useSystemProxy)
        : this(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = useSystemProxy,
            ConnectTimeout = TimeSpan.FromSeconds(15),
            AutomaticDecompression = DecompressionMethods.All,
            MaxConnectionsPerServer = 2,
        })
    {
    }

    internal SayobotClient(HttpMessageHandler handler)
    {
        client = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("SayobotRuleset/0.1");
    }

    public static Uri CreateDownloadUri(int setId, bool noVideo)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(setId);
        return new Uri($"https://dl.sayobot.cn/beatmaps/download/{(noVideo ? "novideo" : "full")}/{setId.ToString(CultureInfo.InvariantCulture)}");
    }

    public async Task DownloadAsync(DownloadSpecification specification, string destination,
                                    IProgress<DownloadProgress>? progress, CancellationToken cancellationToken)
    {
        bool ownsDestination = false;
        try
        {
            using var ioCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var uri = CreateDownloadUri(specification.SetId, specification.NoVideo);
            HttpResponseMessage? response = null;

            try
            {
                for (int redirects = 0; ; redirects++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    ioCancellation.CancelAfter(idleTimeout);
                    using var request = new HttpRequestMessage(HttpMethod.Get, uri);
                    response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ioCancellation.Token).ConfigureAwait(false);

                    if (!isRedirect(response.StatusCode))
                        break;

                    var location = response.Headers.Location;
                    response.Dispose();
                    response = null;
                    if (redirects >= 5 || location == null)
                        throw new HttpRequestException("Sayobot returned too many redirects or a redirect without a destination.");

                    uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    if (!IsAllowedDownloadUri(uri))
                        throw new HttpRequestException("Sayobot redirected outside its HTTPS download service.");
                }

                response.EnsureSuccessStatusCode();
                long? total = response.Content.Headers.ContentLength;
                if (total > maxArchiveBytes)
                    throw new InvalidDataException("The download exceeds the 2 GiB archive limit.");

                var mediaType = response.Content.Headers.ContentType?.MediaType;
                if (mediaType is "text/html" or "application/json" or "text/plain")
                    throw new InvalidDataException("The mirror returned an error page instead of an .osz archive.");

                await using (var source = await response.Content.ReadAsStreamAsync(ioCancellation.Token).ConfigureAwait(false))
                await using (var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None,
                                                        81920, FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    ownsDestination = true;
                    byte[] buffer = ArrayPool<byte>.Shared.Rent(81920);
                    try
                    {
                        long received = 0;
                        long lastReported = Environment.TickCount64;
                        while (true)
                        {
                            ioCancellation.CancelAfter(idleTimeout);
                            int count = await source.ReadAsync(buffer.AsMemory(), ioCancellation.Token).ConfigureAwait(false);
                            if (count == 0)
                                break;

                            received += count;
                            if (received > maxArchiveBytes)
                                throw new InvalidDataException("The download exceeds the 2 GiB archive limit.");

                            await output.WriteAsync(buffer.AsMemory(0, count), cancellationToken).ConfigureAwait(false);
                            if (Environment.TickCount64 - lastReported >= 100)
                            {
                                progress?.Report(new DownloadProgress(received, total));
                                lastReported = Environment.TickCount64;
                            }
                        }

                        if (received == 0 || (total.HasValue && received != total.Value))
                            throw new InvalidDataException("The mirror returned an empty or incomplete archive.");

                        progress?.Report(new DownloadProgress(received, total));
                    }
                    finally
                    {
                        ArrayPool<byte>.Shared.Return(buffer);
                    }
                }

                ioCancellation.CancelAfter(Timeout.InfiniteTimeSpan);
            }
            catch (OperationCanceledException e) when (!cancellationToken.IsCancellationRequested)
            {
                throw new TimeoutException("The Sayobot download stopped responding for 45 seconds.", e);
            }
            finally
            {
                response?.Dispose();
            }

            await ValidateArchiveAsync(destination, specification, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            if (ownsDestination)
                TryDelete(destination);
            throw;
        }
    }

    internal static bool IsAllowedDownloadUri(Uri uri)
        => uri.Scheme == Uri.UriSchemeHttps && string.IsNullOrEmpty(uri.UserInfo)
           && (uri.Host.Equals("sayobot.cn", StringComparison.OrdinalIgnoreCase)
               || uri.Host.EndsWith(".sayobot.cn", StringComparison.OrdinalIgnoreCase));

    internal static async Task ValidateArchiveAsync(string filename, DownloadSpecification specification,
                                                     CancellationToken cancellationToken)
    {
        using var archive = ZipFile.OpenRead(filename);
        var foundHashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        int beatmapCount = 0;

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!entry.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))
                continue;
            if (entry.Length == 0 || entry.Length > maxBeatmapBytes)
                throw new InvalidDataException("The archive contains an empty or excessively large beatmap file.");

            await using var input = entry.Open();
            using var data = new MemoryStream((int)entry.Length);
            byte[] scratch = ArrayPool<byte>.Shared.Rent(81920);
            try
            {
                while (true)
                {
                    int count = await input.ReadAsync(scratch.AsMemory(), cancellationToken).ConfigureAwait(false);
                    if (count == 0)
                        break;
                    if (data.Length + count > maxBeatmapBytes)
                        throw new InvalidDataException("The archive contains an excessively large beatmap file.");
                    data.Write(scratch, 0, count);
                }
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(scratch);
            }

            byte[] bytes = data.ToArray();
            foundHashes.Add(Convert.ToHexString(MD5.HashData(bytes)));
            data.Position = 0;
            using var reader = new StreamReader(data, leaveOpen: true);
            string? firstLine = await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false);
            if (firstLine == null || !firstLine.StartsWith("osu file format v", StringComparison.Ordinal))
                throw new InvalidDataException("The archive contains an invalid beatmap file.");

            while (await reader.ReadLineAsync(cancellationToken).ConfigureAwait(false) is { } line)
            {
                if (line.Trim().Equals("[HitObjects]", StringComparison.OrdinalIgnoreCase))
                    break;
                const string field = "BeatmapSetID:";
                var trimmed = line.Trim();
                if (trimmed.StartsWith(field, StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(trimmed.AsSpan(field.Length).Trim(), NumberStyles.Integer,
                                    CultureInfo.InvariantCulture, out int id)
                    && id > 0 && id != specification.SetId)
                    throw new InvalidDataException($"Expected beatmap set {specification.SetId}, but the mirror supplied set {id}.");
            }

            beatmapCount++;
        }

        if (beatmapCount == 0)
            throw new InvalidDataException("The archive contains no .osu beatmap files.");

        if (specification.ExpectedMd5Hashes.Any(hash => !foundHashes.Contains(hash)))
            throw new InvalidDataException("The mirror's beatmap version does not match the requested beatmap checksums.");
    }

    public static void TryDelete(string filename)
    {
        try { File.Delete(filename); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    private static bool isRedirect(HttpStatusCode status)
        => status is HttpStatusCode.MovedPermanently or HttpStatusCode.Redirect or HttpStatusCode.SeeOther
                     or HttpStatusCode.TemporaryRedirect or HttpStatusCode.PermanentRedirect;

    public void Dispose() => client.Dispose();
}
