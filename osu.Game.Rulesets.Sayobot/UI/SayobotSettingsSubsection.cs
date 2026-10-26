using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Sayobot.Configuration;
using osu.Game.Rulesets.Sayobot.Download;

namespace osu.Game.Rulesets.Sayobot.UI;

public sealed partial class SayobotSettingsSubsection(SayobotRuleset ruleset) : RulesetSettingsSubsection(ruleset)
{
    private readonly Bindable<string> manualSetId = new(string.Empty);

    [Resolved]
    private OsuGameBase game { get; set; } = null!;

    [Resolved(CanBeNull = true)]
    private INotificationOverlay? notifications { get; set; }

    [BackgroundDependencyLoader]
    private void load()
    {
        var config = (SayobotConfigManager)Config;
        Children =
        [
            new FormCheckBox
            {
                Caption = "Sayobot：接管原生譜面下載",
                HintText = "使用 Sayobot 鏡像下載並匯入。切換至其他遊戲模式後仍有效；最多同時下載兩個譜面集。",
                Current = config.GetBindable<bool>(SayobotSetting.Enabled),
            },
            new FormCheckBox
            {
                Caption = "Sayobot：下載無影片版本",
                HintText = "使用 Sayobot 的 novideo 譜面包。這個設定取代原生下載按鈕的影片選項。",
                Current = config.GetBindable<bool>(SayobotSetting.NoVideo),
            },
            new FormCheckBox
            {
                Caption = "Sayobot：失敗時回退官方下載",
                HintText = "鏡像缺圖、校驗碼不同或傳輸失敗時，改用官方下載。手動取消不會回退。",
                Current = config.GetBindable<bool>(SayobotSetting.FallbackToOfficial),
            },
            new FormCheckBox
            {
                Caption = "Sayobot：使用系統代理",
                HintText = "預設直接連線 Sayobot；開啟後使用系統／環境變數的代理設定。",
                Current = config.GetBindable<bool>(SayobotSetting.UseSystemProxy),
            },
            new FormTextBox
            {
                Caption = "Sayobot：手動下載譜面集",
                HintText = "輸入譜面集 ID 或 osu! 譜面集連結，例如 41823。這個入口可用來測試下載及匯入。",
                PlaceholderText = "41823 或 https://osu.ppy.sh/beatmapsets/41823",
                Current = manualSetId,
            },
            new SettingsButtonV2
            {
                Text = "Sayobot：下載並匯入",
                Action = () =>
                {
                    if (!BeatmapSetIdParser.TryParse(manualSetId.Value, out int setId))
                    {
                        notifications?.Post(new SimpleNotification { Text = "請輸入有效的正整數譜面集 ID 或 osu! 譜面集連結。" });
                        return;
                    }
                    SayobotDownloadComponent.EnsureInstalled(game, ruleset).DownloadManually(setId);
                },
            },
        ];
    }
}
