using osu.Game.Configuration;
using osu.Game.Rulesets.Configuration;

namespace osu.Game.Rulesets.Sayobot.Configuration;

public sealed class SayobotConfigManager(SettingsStore? settings, RulesetInfo ruleset)
    : RulesetConfigManager<SayobotSetting>(settings, ruleset)
{
    protected override void InitialiseDefaults()
    {
        base.InitialiseDefaults();
        SetDefault(SayobotSetting.Enabled, true);
        SetDefault(SayobotSetting.NoVideo, true);
        SetDefault(SayobotSetting.FallbackToOfficial, true);
        SetDefault(SayobotSetting.UseSystemProxy, false);
    }
}

public enum SayobotSetting
{
    Enabled,
    NoVideo,
    FallbackToOfficial,
    UseSystemProxy,
}
