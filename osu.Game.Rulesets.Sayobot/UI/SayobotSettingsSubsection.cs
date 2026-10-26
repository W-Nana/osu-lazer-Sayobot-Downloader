using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Game.Graphics.UserInterfaceV2;
using osu.Game.Overlays;
using osu.Game.Overlays.Notifications;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Sayobot.Configuration;
using osu.Game.Rulesets.Sayobot.Download;
using osu.Game.Rulesets.Sayobot.Localisation;
using static osu.Game.Rulesets.Sayobot.Localisation.SayobotStrings;

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
                Caption = Get(SayobotString.EnabledCaption),
                HintText = Get(SayobotString.EnabledHint),
                Current = config.GetBindable<bool>(SayobotSetting.Enabled),
            },
            new FormCheckBox
            {
                Caption = Get(SayobotString.NoVideoCaption),
                HintText = Get(SayobotString.NoVideoHint),
                Current = config.GetBindable<bool>(SayobotSetting.NoVideo),
            },
            new FormCheckBox
            {
                Caption = Get(SayobotString.FallbackCaption),
                HintText = Get(SayobotString.FallbackHint),
                Current = config.GetBindable<bool>(SayobotSetting.FallbackToOfficial),
            },
            new FormCheckBox
            {
                Caption = Get(SayobotString.ProxyCaption),
                HintText = Get(SayobotString.ProxyHint),
                Current = config.GetBindable<bool>(SayobotSetting.UseSystemProxy),
            },
            new FormTextBox
            {
                Caption = Get(SayobotString.ManualCaption),
                HintText = Get(SayobotString.ManualHint),
                PlaceholderText = Get(SayobotString.ManualPlaceholder),
                Current = manualSetId,
            },
            new SettingsButtonV2
            {
                Text = Get(SayobotString.DownloadButton),
                Action = () =>
                {
                    if (!BeatmapSetIdParser.TryParse(manualSetId.Value, out int setId))
                    {
                        notifications?.Post(new SimpleNotification { Text = Get(SayobotString.InvalidSetId) });
                        return;
                    }
                    SayobotDownloadComponent.EnsureInstalled(game, ruleset).DownloadManually(setId);
                },
            },
        ];
    }
}
