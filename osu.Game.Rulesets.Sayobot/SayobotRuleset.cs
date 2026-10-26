using osu.Framework.Allocation;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Game.Beatmaps;
using osu.Game.Configuration;
using osu.Game.Overlays.Settings;
using osu.Game.Rulesets.Configuration;
using osu.Game.Rulesets.Difficulty;
using osu.Game.Rulesets.Difficulty.Preprocessing;
using osu.Game.Rulesets.Difficulty.Skills;
using osu.Game.Rulesets.Filter;
using osu.Game.Rulesets.Mods;
using osu.Game.Rulesets.Objects;
using osu.Game.Rulesets.Sayobot.Configuration;
using osu.Game.Rulesets.Sayobot.UI;
using osu.Game.Rulesets.UI;
using osu.Game.Screens.Select;
using osu.Game.Screens.Select.Filter;
using osu.Framework.Bindables;

namespace osu.Game.Rulesets.Sayobot;

public sealed partial class SayobotRuleset : Ruleset
{
    public const string ProjectName = "osu!lazer Sayobot Downloader";

    public SayobotRuleset()
    {
        RulesetInfo.OnlineID = -1;
        RulesetInfo.ShortName = ShortName;
        RulesetInfo.Name = Description;
    }

    public override string ShortName => "sayobot-download";
    public override string Description => ProjectName;
    public override string RulesetAPIVersionSupported => CURRENT_RULESET_API_VERSION;
    public override IEnumerable<Mod> GetModsFor(ModType type) => [];
    public override DrawableRuleset CreateDrawableRulesetWith(IBeatmap beatmap, IReadOnlyList<Mod>? mods = null)
        => throw new NotSupportedException($"Choose osu!, taiko, catch or mania to play; {ProjectName} only provides downloads.");
    public override IBeatmapConverter CreateBeatmapConverter(IBeatmap beatmap) => new NoBeatmapConverter(beatmap, this);
    public override DifficultyCalculator CreateDifficultyCalculator(IWorkingBeatmap beatmap) => new NoDifficultyCalculator(RulesetInfo, beatmap);
    public override IRulesetFilterCriteria CreateRulesetFilterCriteria() => new NoBeatmapsFilter();
    public override IRulesetConfigManager CreateConfig(SettingsStore? settings) => new SayobotConfigManager(settings, RulesetInfo);
    public override RulesetSettingsSubsection CreateSettings() => new SayobotSettingsSubsection(this);
    public override Drawable CreateIcon() => new SayobotIcon(this);

    private sealed partial class SayobotIcon : CompositeDrawable
    {
        public SayobotIcon(SayobotRuleset ruleset)
        {
            AutoSizeAxes = Axes.Both;
            InternalChildren =
            [
                new SpriteIcon { Icon = FontAwesome.Solid.Download, Anchor = Anchor.Centre, Origin = Anchor.Centre },
                new Bootstrapper(ruleset)
            ];
        }
    }

    private sealed partial class Bootstrapper(SayobotRuleset ruleset) : Component
    {
        [Resolved(CanBeNull = true)]
        private OsuGameBase? game { get; set; }

        protected override void LoadComplete()
        {
            base.LoadComplete();
            if (game != null)
                SayobotDownloadComponent.EnsureInstalled(game, ruleset);
        }
    }

    private sealed class NoBeatmapsFilter : IRulesetFilterCriteria
    {
        public bool Matches(BeatmapInfo beatmapInfo, FilterCriteria criteria) => false;
        public bool TryParseCustomKeywordCriteria(string key, Operator op, string value) => false;
        public bool FilterMayChangeFromMods(FilterCriteria criteria, ValueChangedEvent<IReadOnlyList<Mod>> mods) => false;
    }

    private sealed class NoBeatmapConverter(IBeatmap beatmap, Ruleset ruleset) : BeatmapConverter<HitObject>(beatmap, ruleset)
    {
        public override bool CanConvert() => false;
    }

    private sealed class NoDifficultyCalculator(IRulesetInfo ruleset, IWorkingBeatmap beatmap) : DifficultyCalculator(ruleset, beatmap)
    {
        protected override DifficultyAttributes CreateDifficultyAttributes(IBeatmap beatmap, Mod[] mods, Skill[] skills) => new(mods, 0);
        protected override IEnumerable<DifficultyHitObject> CreateDifficultyHitObjects(IBeatmap beatmap, Mod[] mods) => [];
        protected override Skill[] CreateSkills(IBeatmap beatmap, Mod[] mods) => [];
    }
}
