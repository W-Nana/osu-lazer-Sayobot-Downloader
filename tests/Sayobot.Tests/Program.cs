using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using osu.Game.Rulesets.Sayobot.Download;

int passed = 0;
int failed = 0;
var root = Path.Combine(Path.GetTempPath(), $"sayobot-tests-{Guid.NewGuid():N}");
Directory.CreateDirectory(root);

try
{
    await run("set ID and osu! link parsing", () =>
    {
        foreach (string input in new[] { "41823", " 41823 ", "https://osu.ppy.sh/beatmapsets/41823#osu/131891" })
            check(BeatmapSetIdParser.TryParse(input, out int id) && id == 41823, input);
        foreach (string input in new[] { "0", "-1", "2.5", "https://example.com/beatmapsets/41823", "https://osu.ppy.sh/beatmaps/131891" })
            check(!BeatmapSetIdParser.TryParse(input, out _), input);
        check(SayobotClient.CreateDownloadUri(41823, true).AbsoluteUri.EndsWith("/novideo/41823"), "no-video route");
        check(SayobotClient.CreateDownloadUri(41823, false).AbsoluteUri.EndsWith("/full/41823"), "full route");
        return Task.CompletedTask;
    });

    byte[] osu = Encoding.UTF8.GetBytes("osu file format v14\n\n[Metadata]\nTitle:Test\nArtist:Test\nCreator:Test\nVersion:Test\nBeatmapSetID:41823\n\n[HitObjects]\n");
    byte[] archive = createZip(("Test.osu", osu));
    var spec = new DownloadSpecification(41823, true, [Convert.ToHexString(MD5.HashData(osu)).ToLowerInvariant()]);

    await run("redirected .osz download keeps credentials absent", async () =>
    {
        int requests = 0;
        using var client = new SayobotClient(new FakeHandler((request, _) =>
        {
            requests++;
            check(request.Headers.Authorization == null, "Authorization must be absent");
            check(!request.Headers.Contains("Cookie"), "Cookie must be absent");
            if (requests == 1)
            {
                check(request.RequestUri == SayobotClient.CreateDownloadUri(41823, true), "correct request URL");
                var redirect = new HttpResponseMessage(HttpStatusCode.Redirect);
                redirect.Headers.Location = new Uri("https://tc1.sayobot.cn:25225/beatmaps/4/1823/novideo");
                return Task.FromResult(redirect);
            }
            return Task.FromResult(ok(archive));
        }));
        string path = nextPath();
        var progress = new List<DownloadProgress>();
        await client.DownloadAsync(spec, path, new CallbackProgress(progress.Add), CancellationToken.None);
        check(requests == 2 && File.ReadAllBytes(path).SequenceEqual(archive), "complete archive preserved");
        check(progress.Last().Bytes == archive.Length, "final progress");
    });

    await run("HTTPS and Sayobot-only redirect destinations", () =>
    {
        check(SayobotClient.IsAllowedDownloadUri(new Uri("https://tc1.sayobot.cn:25225/a")), "Sayobot node accepted");
        foreach (string uri in new[] { "http://tc1.sayobot.cn/a", "https://sayobot.cn.evil.test/a", "https://evil.test/a", "https://user:pass@dl.sayobot.cn/a" })
            check(!SayobotClient.IsAllowedDownloadUri(new Uri(uri)), uri);
        return Task.CompletedTask;
    });

    await run("external redirect is refused before a second request", async () =>
    {
        int count = 0;
        using var client = new SayobotClient(new FakeHandler((_, _) =>
        {
            count++;
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri("https://example.com/not-a-mirror");
            return Task.FromResult(response);
        }));
        string path = nextPath();
        await throws<HttpRequestException>(() => client.DownloadAsync(spec, path, null, CancellationToken.None));
        check(count == 1 && !File.Exists(path), "no external request or leftover file");
    });

    await run("HTML error page is refused", async () =>
    {
        using var client = new SayobotClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("<html>Error</html>", Encoding.UTF8, "text/html")
        })));
        string path = nextPath();
        await throws<InvalidDataException>(() => client.DownloadAsync(spec, path, null, CancellationToken.None));
        check(!File.Exists(path), "error file cleaned");
    });

    await run("truncated archive is refused and removed", async () =>
    {
        using var client = new SayobotClient(new FakeHandler((_, _) =>
        {
            var response = ok(archive[..(archive.Length / 2)]);
            response.Content.Headers.ContentLength = archive.Length;
            return Task.FromResult(response);
        }));
        string path = nextPath();
        await throws<InvalidDataException>(() => client.DownloadAsync(spec, path, null, CancellationToken.None));
        check(!File.Exists(path), "partial file cleaned");
    });

    await run("wrong set ID is refused", async () =>
    {
        byte[] wrong = createZip(("Wrong.osu", Encoding.UTF8.GetBytes("osu file format v14\n[Metadata]\nBeatmapSetID:999\n")));
        await invalidArchive(wrong, new DownloadSpecification(41823, true, []));
    });

    await run("stale mirror checksum is refused", () => invalidArchive(archive, spec with { ExpectedMd5Hashes = [new string('0', 32)] }));

    await run("ZIP without .osu files is refused", () => invalidArchive(createZip(("error.txt", Encoding.UTF8.GetBytes("error"))), spec));

    await run("user cancellation cleans partial file", async () =>
    {
        using var cancellation = new CancellationTokenSource();
        using var client = new SayobotClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new CancelAfterFirstReadStream(cancellation)),
        })));
        string path = nextPath();
        await throws<OperationCanceledException>(() => client.DownloadAsync(spec, path, null, cancellation.Token));
        check(!File.Exists(path), "cancelled partial file cleaned");
    });

    await run("missing mirror map produces a recoverable HTTP failure", async () =>
    {
        using var client = new SayobotClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound))));
        string path = nextPath();
        await throws<HttpRequestException>(() => client.DownloadAsync(spec, path, null, CancellationToken.None));
        check(!File.Exists(path), "no file after 404");
    });

    await run("existing destination files are never overwritten or deleted", async () =>
    {
        using var client = new SayobotClient(new FakeHandler((_, _) => Task.FromResult(ok(archive))));
        string path = nextPath();
        await File.WriteAllTextAsync(path, "keep existing contents");
        await throws<IOException>(() => client.DownloadAsync(spec, path, null, CancellationToken.None));
        check(await File.ReadAllTextAsync(path) == "keep existing contents", "existing file preserved");
    });

    if (args.Contains("--live"))
    {
        await run("LIVE: complete Sayobot .osz download and validation (41823)", async () =>
        {
            using var client = new SayobotClient(false);
            using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            string path = nextPath();
            await client.DownloadAsync(new DownloadSpecification(41823, true, []), path, null, timeout.Token);
            check(new FileInfo(path).Length > 1024, "real archive size");
            using var zip = ZipFile.OpenRead(path);
            Console.WriteLine($"  Real archive: {new FileInfo(path).Length:N0} bytes, {zip.Entries.Count} entries, "
                              + $"{zip.Entries.Count(e => e.FullName.EndsWith(".osu", StringComparison.OrdinalIgnoreCase))} difficulties.");
        });
    }
}
finally
{
    Directory.Delete(root, true);
}

Console.WriteLine($"{passed} passed; {failed} failed.");
return failed == 0 ? 0 : 1;

async Task run(string name, Func<Task> test)
{
    try { await test(); passed++; Console.WriteLine($"PASS {name}"); }
    catch (Exception e) { failed++; Console.WriteLine($"FAIL {name}: {e}"); }
}

string nextPath() => Path.Combine(root, $"{Guid.NewGuid():N}.osz");

async Task invalidArchive(byte[] bytes, DownloadSpecification specification)
{
    using var client = new SayobotClient(new FakeHandler((_, _) => Task.FromResult(ok(bytes))));
    string path = nextPath();
    await throws<InvalidDataException>(() => client.DownloadAsync(specification, path, null, CancellationToken.None));
    check(!File.Exists(path), "invalid archive cleaned");
}

static void check(bool condition, string reason)
{
    if (!condition) throw new Exception(reason);
}

static async Task throws<T>(Func<Task> action) where T : Exception
{
    try { await action(); }
    catch (T) { return; }
    throw new Exception($"Expected {typeof(T).Name}.");
}

static HttpResponseMessage ok(byte[] bytes)
    => new(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };

static byte[] createZip(params (string Name, byte[] Content)[] files)
{
    using var output = new MemoryStream();
    using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        foreach (var file in files)
            using (var stream = zip.CreateEntry(file.Name).Open()) stream.Write(file.Content);
    return output.ToArray();
}

sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        => send(request, cancellationToken);
}

sealed class CallbackProgress(Action<DownloadProgress> action) : IProgress<DownloadProgress>
{
    public void Report(DownloadProgress value) => action(value);
}

sealed class CancelAfterFirstReadStream(CancellationTokenSource cancellation) : Stream
{
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken token = default)
    {
        buffer.Span[0] = 1;
        cancellation.Cancel();
        token.ThrowIfCancellationRequested();
        return ValueTask.FromResult(1);
    }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
}
