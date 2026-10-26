using System.Globalization;
using System.Net;
using osu.Framework.Configuration;
using osu.Framework.Localisation;
using osu.Game.Rulesets.Sayobot.Localisation;
using static osu.Game.Rulesets.Sayobot.Localisation.SayobotStrings;

using var config = new MemoryFrameworkConfigManager();
using var localisation = new LocalisationManager(config);
foreach (string locale in new[] { "en", "en-GB", "zh", "zh-Hans", "zh-Hant", "zh-TW", "zh-HK", "zh-MO", "zh-Hant-CN", "zh-Hans-TW", "ja", "ja-JP", "de-DE" })
    localisation.AddLanguage(locale, new EmptyLocalisationStore(CultureInfo.GetCultureInfo(locale)));

int passed = 0;
int failed = 0;

run("all messages have formatted translations in all four languages", () =>
{
    foreach (var key in Enum.GetValues<SayobotString>())
    {
        var text = Get(key, 41823, 1.5d, 3d);
        string english = localise("en", text);
        foreach (string locale in new[] { "en", "zh", "zh-Hant", "ja" })
        {
            string translated = localise(locale, text);
            check(!string.IsNullOrWhiteSpace(translated), $"{key}/{locale}: empty");
            check(!translated.Contains('{') && !translated.Contains('}'), $"{key}/{locale}: unformatted placeholder");
            if (locale != "en")
                check(translated != english, $"{key}/{locale}: missing translation or invalid format");
        }
    }
});

run("Chinese scripts and region variants select the correct translation", () =>
{
    var text = Get(SayobotString.DownloadButton);
    foreach (string locale in new[] { "zh-Hant", "zh-TW", "zh-HK", "zh-MO", "zh-Hant-CN" })
        check(localise(locale, text) == "Sayobot：下載並匯入", locale);
    foreach (string locale in new[] { "zh", "zh-Hans", "zh-Hans-TW" })
        check(localise(locale, text) == "Sayobot：下载并导入", locale);
    check(localise("ja-JP", text) == localise("ja", text), "Japanese region variant");
});

run("unsupported languages fall back to English with local number formatting", () =>
{
    var text = Get(SayobotString.DownloadProgressWithTotal, 41823, 1.5d, 3d);
    check(localise("en-GB", text) == "Sayobot: beatmap set 41823 (1.5 / 3.0 MiB)", "English region variant");
    check(localise("de-DE", text) == "Sayobot: beatmap set 41823 (1,5 / 3,0 MiB)", "English fallback with German decimals");
    check(Get(SayobotString.DownloadButton).ToString() == "Sayobot: download and import", "default string representation");
    using var emptyConfig = new MemoryFrameworkConfigManager();
    using var emptyManager = new LocalisationManager(emptyConfig);
    check(emptyManager.GetLocalisedString(text) == "Sayobot: beatmap set 41823 (1.5 / 3.0 MiB)", "no localisation store");
});

run("existing notification text and nested errors update when the language changes", () =>
{
    config.SetValue(FrameworkSetting.Locale, "en");
    var exception = new HttpRequestException("untranslated diagnostic", null, HttpStatusCode.NotFound);
    var text = Get(SayobotString.Failed, 41823, SayobotErrors.Describe(exception, false));
    var bound = localisation.GetLocalisedBindableString(text);
    int changes = 0;
    bound.ValueChanged += _ => changes++;
    foreach (var (locale, fragment) in new[] { ("zh", "镜像中没有"), ("zh-Hant", "鏡像中沒有"), ("ja", "ミラーにこの"), ("en", "not available on the mirror") })
    {
        config.SetValue(FrameworkSetting.Locale, locale);
        check(bound.Value == localisation.GetLocalisedString(text), "bound and direct localisation agree");
        check(bound.Value.Contains(fragment) && bound.Value.Contains("41823"), locale);
        check(!bound.Value.Contains(exception.Message), "raw diagnostic is not exposed");
    }
    check(changes == 4, $"expected four live updates, got {changes}");
});

run("download failures and import failures have localisable explanations", () =>
{
    (Exception Exception, SayobotString Key)[] cases =
    [
        (new HttpRequestException("diagnostic", null, HttpStatusCode.NotFound), SayobotString.MirrorMissing),
        (new HttpRequestException("diagnostic", null, HttpStatusCode.Gone), SayobotString.MirrorMissing),
        (new HttpRequestException("diagnostic"), SayobotString.NetworkError),
        (new TimeoutException("diagnostic"), SayobotString.Timeout),
        (new OperationCanceledException("diagnostic"), SayobotString.Timeout),
        (new InvalidDataException("diagnostic"), SayobotString.InvalidArchive),
        (new IOException("diagnostic"), SayobotString.FileError),
        (new UnauthorizedAccessException("diagnostic"), SayobotString.FileError),
        (new Exception("diagnostic"), SayobotString.UnknownError),
    ];
    foreach (string locale in new[] { "en", "zh", "zh-Hant", "ja" })
    {
        foreach (var (exception, key) in cases)
        {
            check(localise(locale, SayobotErrors.Describe(exception, false)) == localise(locale, Get(key)), $"{locale}/{key}");
            check(localise(locale, SayobotErrors.Describe(exception, true)) == localise(locale, Get(SayobotString.ImportError)), "import error");
        }
        var serverError = SayobotErrors.Describe(new HttpRequestException("diagnostic", null, HttpStatusCode.ServiceUnavailable), false);
        check(localise(locale, serverError) == localise(locale, Get(SayobotString.ServerError, 503)), "HTTP status preserved");
    }
});

run("translated descriptors preserve equality and argument changes", () =>
{
    var first = Get(SayobotString.DownloadProgress, 41823, 1.5d);
    var same = Get(SayobotString.DownloadProgress, 41823, 1.5d);
    var next = Get(SayobotString.DownloadProgress, 41823, 2d);
    check(first == same && first.GetHashCode() == same.GetHashCode(), "equal descriptors");
    check(first != next, "progress update is not suppressed");
    check(Get(SayobotString.Downloading, 41823) != Get(SayobotString.Importing, 41823), "stage update is not suppressed");
});

Console.WriteLine($"{passed} localisation tests passed; {failed} failed.");
return failed == 0 ? 0 : 1;

string localise(string locale, LocalisableString text)
{
    config.SetValue(FrameworkSetting.Locale, locale);
    return localisation.GetLocalisedString(text);
}

void run(string name, Action test)
{
    try
    {
        test();
        passed++;
        Console.WriteLine($"PASS: {name}");
    }
    catch (Exception e)
    {
        failed++;
        Console.WriteLine($"FAIL: {name}\n{e}");
    }
}

static void check(bool value, string message)
{
    if (!value)
        throw new Exception(message);
}

sealed class MemoryFrameworkConfigManager() : FrameworkConfigManager(null!)
{
    protected override string Filename => string.Empty;
}

sealed class EmptyLocalisationStore(CultureInfo culture) : ILocalisationStore
{
    public CultureInfo EffectiveCulture => culture;
    public string Get(string name) => null!;
    public Task<string> GetAsync(string name, CancellationToken cancellationToken = default) => Task.FromResult<string>(null!);
    public Stream GetStream(string name) => null!;
    public IEnumerable<string> GetAvailableResources() => [];
    public void Dispose() { }
}
