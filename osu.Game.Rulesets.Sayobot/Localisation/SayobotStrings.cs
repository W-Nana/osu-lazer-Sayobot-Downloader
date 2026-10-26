using System.Globalization;
using osu.Framework.Localisation;

namespace osu.Game.Rulesets.Sayobot.Localisation;

public enum SayobotString
{
    EnabledCaption,
    EnabledHint,
    NoVideoCaption,
    NoVideoHint,
    FallbackCaption,
    FallbackHint,
    ProxyCaption,
    ProxyHint,
    ManualCaption,
    ManualHint,
    ManualPlaceholder,
    DownloadButton,
    InvalidSetId,
    Loaded,
    AlreadyQueued,
    Queued,
    Completed,
    Downloading,
    DownloadProgress,
    DownloadProgressWithTotal,
    Importing,
    Cancelled,
    Failed,
    FallingBack,
    FailureDetails,
    OfficialCleanupPending,
    OfficialStartFailed,
    MirrorMissing,
    ServerError,
    NetworkError,
    Timeout,
    InvalidArchive,
    FileError,
    ImportError,
    UnknownError,
}

/// <summary>
/// Compiled translations evaluated by lazer's localisation manager. No resource loading,
/// language registration or changes to the game's localisation stores are needed.
/// </summary>
public static class SayobotStrings
{
    private sealed record Translation(string English, string SimplifiedChinese, string TraditionalChinese, string Japanese);

    private static readonly IReadOnlyDictionary<SayobotString, Translation> translations = new Dictionary<SayobotString, Translation>
    {
        [SayobotString.EnabledCaption] = new(
            "Sayobot: use mirror for native downloads",
            "Sayobot：接管原生谱面下载",
            "Sayobot：接管原生譜面下載",
            "Sayobot：標準のダウンロードにミラーを使用"),
        [SayobotString.EnabledHint] = new(
            "Download and import from Sayobot. Stays active when you switch game modes; downloads up to two beatmap sets at once.",
            "使用 Sayobot 镜像下载并导入。切换到其他游戏模式后仍有效；最多同时下载两个谱面集。",
            "使用 Sayobot 鏡像下載並匯入。切換至其他遊戲模式後仍有效；最多同時下載兩個譜面集。",
            "Sayobot からダウンロードしてインポートします。ゲームモードを切り替えても有効です。同時にダウンロードできるビートマップセットは 2 つまでです。"),
        [SayobotString.NoVideoCaption] = new(
            "Sayobot: download without video",
            "Sayobot：下载无视频版本",
            "Sayobot：下載無影片版本",
            "Sayobot：動画なしでダウンロード"),
        [SayobotString.NoVideoHint] = new(
            "Use Sayobot's novideo archives. This overrides the video option on the native download button.",
            "使用 Sayobot 的 novideo 谱面包。此设置取代原生下载按钮的视频选项。",
            "使用 Sayobot 的 novideo 譜面包。這個設定取代原生下載按鈕的影片選項。",
            "Sayobot の novideo アーカイブを使用します。標準のダウンロードボタンの動画設定よりも、この設定が優先されます。"),
        [SayobotString.FallbackCaption] = new(
            "Sayobot: fall back to official downloads",
            "Sayobot：失败时回退官方下载",
            "Sayobot：失敗時回退官方下載",
            "Sayobot：失敗時は公式サーバーからダウンロード"),
        [SayobotString.FallbackHint] = new(
            "Use the official download if the mirror is missing the set, checksums differ or the transfer fails. Cancelling does not trigger a fallback.",
            "镜像缺图、校验码不同或传输失败时，改用官方下载。手动取消不会触发回退。",
            "鏡像缺圖、校驗碼不同或傳輸失敗時，改用官方下載。手動取消不會回退。",
            "ミラーにセットがない場合、チェックサムが異なる場合、または転送に失敗した場合に公式サーバーを使用します。手動キャンセル時は切り替えません。"),
        [SayobotString.ProxyCaption] = new(
            "Sayobot: use system proxy",
            "Sayobot：使用系统代理",
            "Sayobot：使用系統代理",
            "Sayobot：システムのプロキシを使用"),
        [SayobotString.ProxyHint] = new(
            "Connect directly by default. Enable to use the proxy configured by your system or environment variables.",
            "默认直接连接 Sayobot；开启后使用系统或环境变量中的代理设置。",
            "預設直接連線 Sayobot；開啟後使用系統／環境變數的代理設定。",
            "既定では Sayobot に直接接続します。有効にすると、システムまたは環境変数のプロキシ設定を使用します。"),
        [SayobotString.ManualCaption] = new(
            "Sayobot: manually download a beatmap set",
            "Sayobot：手动下载谱面集",
            "Sayobot：手動下載譜面集",
            "Sayobot：ビートマップセットを手動でダウンロード"),
        [SayobotString.ManualHint] = new(
            "Enter a beatmap set ID or osu! beatmap set link, such as 41823. Use this to test downloading and importing.",
            "输入谱面集 ID 或 osu! 谱面集链接，例如 41823。可用此入口测试下载和导入。",
            "輸入譜面集 ID 或 osu! 譜面集連結，例如 41823。這個入口可用來測試下載及匯入。",
            "ビートマップセット ID または osu! のセットリンク（例：41823）を入力してください。ダウンロードとインポートの確認に使えます。"),
        [SayobotString.ManualPlaceholder] = new(
            "41823 or https://osu.ppy.sh/beatmapsets/41823",
            "41823 或 https://osu.ppy.sh/beatmapsets/41823",
            "41823 或 https://osu.ppy.sh/beatmapsets/41823",
            "41823 または https://osu.ppy.sh/beatmapsets/41823"),
        [SayobotString.DownloadButton] = new(
            "Sayobot: download and import",
            "Sayobot：下载并导入",
            "Sayobot：下載並匯入",
            "Sayobot：ダウンロードしてインポート"),
        [SayobotString.InvalidSetId] = new(
            "Enter a valid positive beatmap set ID or osu! beatmap set link.",
            "请输入有效的正整数谱面集 ID 或 osu! 谱面集链接。",
            "請輸入有效的正整數譜面集 ID 或 osu! 譜面集連結。",
            "有効な正の整数のビートマップセット ID または osu! のセットリンクを入力してください。"),
        [SayobotString.Loaded] = new(
            "osu!lazer Sayobot Downloader loaded. Switch back to osu! / taiko / catch / mania and download as usual.",
            "osu!lazer Sayobot Downloader 已加载。可切回 osu!／taiko／catch／mania，照常点击下载。",
            "osu!lazer Sayobot Downloader 已載入。可切回 osu!／taiko／catch／mania，照常按下載。",
            "osu!lazer Sayobot Downloader を読み込みました。osu! / taiko / catch / mania に戻り、通常どおりダウンロードできます。"),
        [SayobotString.AlreadyQueued] = new(
            "Sayobot: beatmap set {0} is already queued.",
            "Sayobot：谱面集 {0} 已在下载队列中。",
            "Sayobot：譜面集 {0} 已在下載佇列中。",
            "Sayobot：ビートマップセット {0} はすでにキューに登録されています。"),
        [SayobotString.Queued] = new(
            "Sayobot: waiting to download beatmap set {0}",
            "Sayobot：等待下载谱面集 {0}",
            "Sayobot：等待下載譜面集 {0}",
            "Sayobot：ビートマップセット {0} のダウンロードを待機中"),
        [SayobotString.Completed] = new(
            "Sayobot: beatmap set {0} imported",
            "Sayobot：谱面集 {0} 已导入",
            "Sayobot：譜面集 {0} 已匯入",
            "Sayobot：ビートマップセット {0} をインポートしました"),
        [SayobotString.Downloading] = new(
            "Sayobot: downloading beatmap set {0}",
            "Sayobot：正在下载谱面集 {0}",
            "Sayobot：正在下載譜面集 {0}",
            "Sayobot：ビートマップセット {0} をダウンロード中"),
        [SayobotString.DownloadProgress] = new(
            "Sayobot: beatmap set {0} ({1:0.0} MiB)",
            "Sayobot：谱面集 {0}（{1:0.0} MiB）",
            "Sayobot：譜面集 {0}（{1:0.0} MiB）",
            "Sayobot：ビートマップセット {0}（{1:0.0} MiB）"),
        [SayobotString.DownloadProgressWithTotal] = new(
            "Sayobot: beatmap set {0} ({1:0.0} / {2:0.0} MiB)",
            "Sayobot：谱面集 {0}（{1:0.0} / {2:0.0} MiB）",
            "Sayobot：譜面集 {0}（{1:0.0} / {2:0.0} MiB）",
            "Sayobot：ビートマップセット {0}（{1:0.0} / {2:0.0} MiB）"),
        [SayobotString.Importing] = new(
            "Sayobot: importing beatmap set {0}",
            "Sayobot：正在导入谱面集 {0}",
            "Sayobot：正在匯入譜面集 {0}",
            "Sayobot：ビートマップセット {0} をインポート中"),
        [SayobotString.Cancelled] = new(
            "Sayobot: beatmap set {0} cancelled",
            "Sayobot：已取消谱面集 {0}",
            "Sayobot：已取消譜面集 {0}",
            "Sayobot：ビートマップセット {0} をキャンセルしました"),
        [SayobotString.Failed] = new(
            "Sayobot: beatmap set {0} failed ({1})",
            "Sayobot：谱面集 {0} 失败（{1}）",
            "Sayobot：譜面集 {0} 失敗（{1}）",
            "Sayobot：ビートマップセット {0} の処理に失敗しました（{1}）"),
        [SayobotString.FallingBack] = new(
            "Sayobot download failed; switching to the official download: {0}",
            "Sayobot 下载失败，改用官方下载：{0}",
            "Sayobot 下載失敗，改用官方下載：{0}",
            "Sayobot のダウンロードに失敗しました。公式サーバーに切り替えます：{0}"),
        [SayobotString.FailureDetails] = new(
            "Sayobot download or import failed: {0}. See the runtime log for details.",
            "Sayobot 下载或导入失败：{0}。详情请查看 runtime 日志。",
            "Sayobot 下載或匯入失敗：{0}。詳見 runtime 日誌。",
            "Sayobot のダウンロードまたはインポートに失敗しました：{0}。詳細は runtime ログを確認してください。"),
        [SayobotString.OfficialCleanupPending] = new(
            "The official download is still being cleaned up. Retry beatmap set {0} manually.",
            "官方下载仍在清理，请手动重试谱面集 {0}。",
            "官方下載仍在清理，請手動重試譜面集 {0}。",
            "公式ダウンロードの後処理中です。ビートマップセット {0} を手動で再試行してください。"),
        [SayobotString.OfficialStartFailed] = new(
            "Could not start the official download. Retry beatmap set {0} manually.",
            "无法启动官方下载，请手动重试谱面集 {0}。",
            "無法啟動官方下載，請手動重試譜面集 {0}。",
            "公式ダウンロードを開始できませんでした。ビートマップセット {0} を手動で再試行してください。"),
        [SayobotString.MirrorMissing] = new(
            "This beatmap set is not available on the mirror.",
            "镜像中没有此谱面集。",
            "鏡像中沒有此譜面集。",
            "ミラーにこのビートマップセットがありません。"),
        [SayobotString.ServerError] = new(
            "The mirror returned HTTP {0}.",
            "镜像返回 HTTP {0}。",
            "鏡像回傳 HTTP {0}。",
            "ミラーから HTTP {0} が返されました。"),
        [SayobotString.NetworkError] = new(
            "Could not connect to the mirror or follow its download redirect.",
            "无法连接镜像或跟随其下载重定向。",
            "無法連線鏡像或跟隨其下載重新導向。",
            "ミラーへの接続、またはダウンロード先へのリダイレクトに失敗しました。"),
        [SayobotString.Timeout] = new(
            "The mirror did not respond in time.",
            "镜像响应超时。",
            "鏡像回應逾時。",
            "ミラーからの応答がタイムアウトしました。"),
        [SayobotString.InvalidArchive] = new(
            "The mirror archive is invalid, incomplete or does not match the requested beatmap version.",
            "镜像谱面包无效、不完整，或与请求的谱面版本不符。",
            "鏡像譜面包無效、不完整，或與請求的譜面版本不符。",
            "ミラーのアーカイブが無効、不完全、または要求したビートマップのバージョンと一致しません。"),
        [SayobotString.FileError] = new(
            "Could not read or write the temporary beatmap archive.",
            "无法读取或写入临时谱面包。",
            "無法讀取或寫入暫存譜面包。",
            "一時ビートマップアーカイブの読み書きに失敗しました。"),
        [SayobotString.ImportError] = new(
            "lazer could not import the beatmap set.",
            "lazer 无法导入此谱面集。",
            "lazer 無法匯入此譜面集。",
            "lazer でビートマップセットをインポートできませんでした。"),
        [SayobotString.UnknownError] = new(
            "An unexpected error occurred. See the runtime log for details.",
            "发生意外错误。详情请查看 runtime 日志。",
            "發生未預期的錯誤。詳見 runtime 日誌。",
            "予期しないエラーが発生しました。詳細は runtime ログを確認してください。"),
    };

    public static LocalisableString Get(SayobotString key, params object?[] arguments)
        => new(new TranslatedString(translations[key], arguments));

    private sealed class TranslatedString(Translation translation, object?[] arguments)
        : LocalisableFormattableString(translation.English, arguments)
    {
        protected override string FormatString(string format, object?[] args, LocalisationParameters parameters)
        {
            var culture = parameters.Store?.EffectiveCulture ?? CultureInfo.InvariantCulture;
            string translatedFormat = translation.English;

            // Follow the culture's script before its region (for example zh-Hant-CN).
            for (var parent = culture; !string.IsNullOrEmpty(parent.Name); parent = parent.Parent)
            {
                if (parent.Name.Equals("zh-Hant", StringComparison.OrdinalIgnoreCase))
                {
                    translatedFormat = translation.TraditionalChinese;
                    break;
                }
                if (parent.Name.Equals("zh-Hans", StringComparison.OrdinalIgnoreCase))
                {
                    translatedFormat = translation.SimplifiedChinese;
                    break;
                }
            }

            if (translatedFormat == translation.English)
            {
                translatedFormat = culture.TwoLetterISOLanguageName switch
                {
                    "zh" when culture.Name.Equals("zh-TW", StringComparison.OrdinalIgnoreCase)
                              || culture.Name.Equals("zh-HK", StringComparison.OrdinalIgnoreCase)
                              || culture.Name.Equals("zh-MO", StringComparison.OrdinalIgnoreCase) => translation.TraditionalChinese,
                    "zh" => translation.SimplifiedChinese,
                    "ja" => translation.Japanese,
                    _ => translation.English,
                };
            }

            return base.FormatString(translatedFormat, args, parameters);
        }
    }
}
