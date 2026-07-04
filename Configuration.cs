using System;
using System.Collections.Generic;
using System.Linq;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace MagicLook;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool UseTerritoryFilter { get; set; } = true;

    public ushort TerritoryType { get; set; } = 1363;

    public bool FilterByCasterName { get; set; } = false;

    public string CasterNameKeyword { get; set; } = "ケフカ";

    public bool MagicLookEnabled { get; set; } = true;

    public int MagicLookDisplayMode { get; set; } = 0;

    public float MagicLookWorldHeightOffset { get; set; } = 2.35f;

    public float MagicLookScreenOffsetX { get; set; } = 0.0f;

    public float MagicLookScreenOffsetY { get; set; } = -20.0f;

    public float MagicLookFixedScreenPositionX { get; set; } = 960.0f;

    public float MagicLookFixedScreenPositionY { get; set; } = 260.0f;

    public float MagicLookFontSize { get; set; } = 28.0f;

    public float MagicLookDisplaySeconds { get; set; } = 2.0f;

    public float MagicLookFadeInSeconds { get; set; } = 0.15f;

    public bool MagicLookDrawBackground { get; set; } = true;

    public bool MagicLookUseOutline { get; set; } = true;

    public float MagicLookOutlineThickness { get; set; } = 2.0f;

    public float MagicLookOutlineColorR { get; set; } = 0.0f;

    public float MagicLookOutlineColorG { get; set; } = 0.0f;

    public float MagicLookOutlineColorB { get; set; } = 0.0f;

    public float MagicLookOutlineColorA { get; set; } = 1.0f;

    public string MagicLookTestText { get; set; } = "テキスト表示位置テスト文字列";

    public string MagicLookBothNoStepText { get; set; } = "両方踏まない";

    public string MagicLookBothStepText { get; set; } = "両方踏む";

    public string MagicLookLineOnlyStepText { get; set; } = "直線だけ踏む";

    public string MagicLookFanOnlyStepText { get; set; } = "扇だけ踏む";

    public bool MagicChargeEnabled { get; set; } = true;

    public int MagicChargeDisplayMode { get; set; } = 1;

    public float MagicChargeWorldHeightOffset { get; set; } = 2.85f;

    public float MagicChargeScreenOffsetX { get; set; } = 0.0f;

    public float MagicChargeScreenOffsetY { get; set; } = -75.0f;

    public float MagicChargeScreenPositionX { get; set; } = 960.0f;

    public float MagicChargeScreenPositionY { get; set; } = 300.0f;

    public float MagicChargeFontSize { get; set; } = 28.0f;

    public float MagicChargeDisplaySeconds { get; set; } = 3.0f;

    public float MagicChargeFadeInSeconds { get; set; } = 0.5f;

    public bool MagicChargeDrawBackground { get; set; } = true;

    public float MagicChargeTextColorR { get; set; } = 0.35f;

    public float MagicChargeTextColorG { get; set; } = 0.90f;

    public float MagicChargeTextColorB { get; set; } = 1.00f;

    public float MagicChargeTextColorA { get; set; } = 1.00f;

    public bool MagicChargeUseOutline { get; set; } = true;

    public float MagicChargeOutlineThickness { get; set; } = 2.0f;

    public float MagicChargeOutlineColorR { get; set; } = 0.0f;

    public float MagicChargeOutlineColorG { get; set; } = 0.0f;

    public float MagicChargeOutlineColorB { get; set; } = 0.0f;

    public float MagicChargeOutlineColorA { get; set; } = 1.0f;

    public int MagicChargeFontPreset { get; set; } = 0;

    public string MagicChargeTestText { get; set; } = "テキスト表示位置テスト文字列";

    public string MagicChargeUnknownText { get; set; } = "magicCharge 判定不能";

    public string MagicChargeFanNoStepLineNoStepText { get; set; } = "真ブリザガ / 真サンダガ";

    public string MagicChargeFanNoStepLineStepText { get; set; } = "偽ブリザガ / 真サンダガ";

    public string MagicChargeFanStepLineNoStepText { get; set; } = "真ブリザガ / 偽サンダガ";

    public string MagicChargeFanStepLineStepText { get; set; } = "偽ブリザガ / 偽サンダガ";

    public bool GrandCrossEnabled { get; set; } = true;

    public bool GrandCrossFilterByEnemyName { get; set; } = true;

    public string GrandCrossEnemyNameKeyword { get; set; } = "ネオエクスデス";

    public bool GrandCrossChaosFilterByEnemyName { get; set; } = true;

    public string GrandCrossChaosEnemyNameKeyword { get; set; } = "カオス";

    public int GrandCrossDisplayMode { get; set; } = 0;

    public float GrandCrossWorldHeightOffset { get; set; } = 3.25f;

    public float GrandCrossScreenOffsetX { get; set; } = 0.0f;

    public float GrandCrossScreenOffsetY { get; set; } = -130.0f;

    public float GrandCrossScreenPositionX { get; set; } = 960.0f;

    public float GrandCrossScreenPositionY { get; set; } = 360.0f;

    public float GrandCrossFontSize { get; set; } = 28.0f;

    public float GrandCrossDisplaySeconds { get; set; } = 5.0f;

    public float GrandCrossFadeInSeconds { get; set; } = 0.5f;

    public bool GrandCrossDrawBackground { get; set; } = true;

    public bool GrandCrossShowRemainingSeconds { get; set; } = true;

    public float GrandCrossTextColorR { get; set; } = 1.00f;

    public float GrandCrossTextColorG { get; set; } = 0.95f;

    public float GrandCrossTextColorB { get; set; } = 0.25f;

    public float GrandCrossTextColorA { get; set; } = 1.00f;

    public bool GrandCrossUseOutline { get; set; } = true;

    public float GrandCrossOutlineThickness { get; set; } = 3.0f;

    public float GrandCrossOutlineColorR { get; set; } = 0.0f;

    public float GrandCrossOutlineColorG { get; set; } = 0.0f;

    public float GrandCrossOutlineColorB { get; set; } = 0.0f;

    public float GrandCrossOutlineColorA { get; set; } = 1.0f;

    public int GrandCrossFontPreset { get; set; } = 2;

    public float GrandCrossStatusCaptureSeconds { get; set; } = 3.0f;

    public string GrandCrossSeparator { get; set; } = "_";

    public string GrandCrossFakePrefix { get; set; } = "偽";

    public string GrandCrossTestText { get; set; } = "テキスト表示位置テスト文字列";

    public string GrandCrossAllaganFieldTrueText { get; set; } = "アラガンフィールド";
    public string GrandCrossAllaganFieldFakeText { get; set; } = "アラガンフィールド";
    public string GrandCrossDeathBeyondTrueText { get; set; } = "死の超越";
    public string GrandCrossDeathBeyondFakeText { get; set; } = "死の超越";
    public string GrandCrossLivingWoundTrueText { get; set; } = "生者の傷";
    public string GrandCrossLivingWoundFakeText { get; set; } = "生者の傷";
    public string GrandCrossDeadWoundTrueText { get; set; } = "死者の傷";
    public string GrandCrossDeadWoundFakeText { get; set; } = "死者の傷";
    public string GrandCrossCurseShriekTrueText { get; set; } = "呪詛の叫声";
    public string GrandCrossCurseShriekFakeText { get; set; } = "呪詛の叫声";
    public string GrandCrossForkedLightningTrueText { get; set; } = "フォークライトニング";
    public string GrandCrossForkedLightningFakeText { get; set; } = "フォークライトニング";
    public string GrandCrossWaterCompressionTrueText { get; set; } = "水属性圧縮";
    public string GrandCrossWaterCompressionFakeText { get; set; } = "水属性圧縮";
    public string GrandCrossAccelerationBombTrueText { get; set; } = "加速度爆弾";
    public string GrandCrossAccelerationBombFakeText { get; set; } = "加速度爆弾";
    public string GrandCrossFireFakeText { get; set; } = "ほのお";
    public string GrandCrossFireTrueText { get; set; } = "ほのお";
    public string GrandCrossTsunamiFakeText { get; set; } = "つなみ";
    public string GrandCrossTsunamiTrueText { get; set; } = "つなみ";

    public List<ActionTextSetting> ActionTextSettings { get; set; } = new();

    public List<MagicLookPhaseTextSetting> MagicLookPhaseTextSettings { get; set; } = new();

    public void EnsureDefaults()
    {
        this.ActionTextSettings ??= new List<ActionTextSetting>();
        this.MagicLookPhaseTextSettings ??= new List<MagicLookPhaseTextSetting>();

        this.EnsureActionTextSetting("真ブリザガ / ActionId: 47768", new List<uint> { 47768 }, "真ブリザガ");
        this.EnsureActionTextSetting("偽ブリザガ / ActionId: 47771, 47774", new List<uint> { 47771, 47774 }, "偽ブリザガ");
        this.EnsureActionTextSetting("真サンダガ / ActionId: 47775", new List<uint> { 47775 }, "真サンダガ");
        this.EnsureActionTextSetting("偽サンダガ / ActionId: 47776, 47777", new List<uint> { 47776, 47777 }, "偽サンダガ");

        // magicLook Phase1 は、ブリザガ/サンダガActionId + ファイガVFX + 頭割り/散開VFX の組み合わせで表示内容を決めます。
        // 現在のソースではVFXフック自体は別途実装が必要なため、比較対象としてVFXパスを設定へ保持します。
        this.RebuildMagicLookPhaseSettingsToCurrentSpec();
    }

    private void EnsureActionTextSetting(string label, List<uint> actionIds, string defaultText)
    {
        var existing = this.ActionTextSettings.FirstOrDefault(setting =>
            setting.ActionIds.Count == actionIds.Count &&
            actionIds.All(id => setting.ActionIds.Contains(id)));

        if (existing != null)
        {
            if (string.IsNullOrWhiteSpace(existing.Label))
                existing.Label = label;

            if (existing.ActionIds.Count == 0)
                existing.ActionIds = actionIds;

            if (string.IsNullOrWhiteSpace(existing.Text))
                existing.Text = defaultText;

            return;
        }

        this.ActionTextSettings.Add(new ActionTextSetting
        {
            Enabled = true,
            Label = label,
            ActionIds = actionIds,
            Text = defaultText
        });
    }

    private void RebuildMagicLookPhaseSettingsToCurrentSpec()
    {
        var existingTextByLabel = this.MagicLookPhaseTextSettings
            .Where(setting => !string.IsNullOrWhiteSpace(setting.Label))
            .GroupBy(setting => this.CreatePhaseTextKey(setting.Phase, setting.Label))
            .ToDictionary(group => group.Key, group => group.First().Text);

        this.MagicLookPhaseTextSettings.Clear();

        // Phase1 / ファイガ + 頭割り・散開
        this.AddPhaseTextSetting(
            "Phase1",
            "真ブリザガ / 偽ファイガ（パターン1）",
            new List<uint> { 47768 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏まない_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真ブリザガ / 偽ファイガ（パターン2）",
            new List<uint> { 47768 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏まない_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真ブリザガ / 真ファイガ（パターン3）",
            new List<uint> { 47768 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏まない_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真ブリザガ / 真ファイガ（パターン4）",
            new List<uint> { 47768 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏まない_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽ブリザガ / 偽ファイガ（パターン1）",
            new List<uint> { 47771, 47774 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏む_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽ブリザガ / 偽ファイガ（パターン2）",
            new List<uint> { 47771, 47774 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏む_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽ブリザガ / 真ファイガ（パターン3）",
            new List<uint> { 47771, 47774 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏む_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽ブリザガ / 真ファイガ（パターン4）",
            new List<uint> { 47771, 47774 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏む_散開", existingTextByLabel);


        // Phase1 / ブリザガ + サンダガ
        this.AddPhaseTextSetting("Phase1", "真ブリザガ / 真サンダガ", new List<uint> { 47768, 47775 }, new List<string>(), "踏まない", existingTextByLabel);
        this.AddPhaseTextSetting("Phase1", "真ブリザガ / 偽サンダガ", new List<uint> { 47768, 47776, 47777 }, new List<string>(), "直線だけ", existingTextByLabel);
        this.AddPhaseTextSetting("Phase1", "偽ブリザガ / 真サンダガ", new List<uint> { 47771, 47774, 47775 }, new List<string>(), "扇だけ", existingTextByLabel);
        this.AddPhaseTextSetting("Phase1", "偽ブリザガ / 偽サンダガ", new List<uint> { 47771, 47774, 47776, 47777 }, new List<string>(), "両方踏む", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真サンダガ / 偽ファイガ（パターン1）",
            new List<uint> { 47775 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏まない_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真サンダガ / 偽ファイガ（パターン2）",
            new List<uint> { 47775 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏まない_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真サンダガ / 真ファイガ（パターン3）",
            new List<uint> { 47775 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏まない_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "真サンダガ / 真ファイガ（パターン4）",
            new List<uint> { 47775 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏まない_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽サンダガ / 偽ファイガ（パターン1）",
            new List<uint> { 47776, 47777 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏む_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽サンダガ / 偽ファイガ（パターン2）",
            new List<uint> { 47776, 47777 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c01c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏む_散開", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽サンダガ / 真ファイガ（パターン3）",
            new List<uint> { 47776, 47777 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_a0c.avfx"
            },
            "踏む_頭割り", existingTextByLabel);

        this.AddPhaseTextSetting(
            "Phase1",
            "偽サンダガ / 真ファイガ（パターン4）",
            new List<uint> { 47776, 47777 },
            new List<string>
            {
                "vfx/lockon/eff/m0462trg_c02c.avfx",
                "vfx/lockon/eff/m0462trg_b0c.avfx"
            },
            "踏む_散開", existingTextByLabel);

        // Phase4 / ブリザガ + サンダガ
        this.AddPhaseTextSetting("Phase4", "真ブリザガ / 真サンダガ", new List<uint> { 47768, 47775 }, new List<string>(), "踏まない", existingTextByLabel);
        this.AddPhaseTextSetting("Phase4", "真ブリザガ / 偽サンダガ", new List<uint> { 47768, 47776, 47777 }, new List<string>(), "直線だけ", existingTextByLabel);
        this.AddPhaseTextSetting("Phase4", "偽ブリザガ / 真サンダガ", new List<uint> { 47771, 47774, 47775 }, new List<string>(), "扇だけ", existingTextByLabel);
        this.AddPhaseTextSetting("Phase4", "偽ブリザガ / 偽サンダガ", new List<uint> { 47771, 47774, 47776, 47777 }, new List<string>(), "両方踏む", existingTextByLabel);
    }

    private void AddPhaseTextSetting(string phase, string label, List<uint> actionIds, List<string> requiredVfxPaths, string defaultText)
    {
        this.AddPhaseTextSetting(phase, label, actionIds, requiredVfxPaths, defaultText, null);
    }

    private void AddPhaseTextSetting(
        string phase,
        string label,
        List<uint> actionIds,
        List<string> requiredVfxPaths,
        string defaultText,
        Dictionary<string, string>? existingTextByLabel)
    {
        var text = this.ResolvePhaseText(phase, label, defaultText, existingTextByLabel);

        this.MagicLookPhaseTextSettings.Add(new MagicLookPhaseTextSetting
        {
            Enabled = true,
            Phase = phase,
            Label = label,
            ActionIds = actionIds,
            RequiredVfxPaths = requiredVfxPaths,
            Text = text
        });
    }

    private string ResolvePhaseText(string phase, string label, string defaultText, Dictionary<string, string>? existingTextByLabel)
    {
        if (existingTextByLabel == null)
            return defaultText;

        var key = this.CreatePhaseTextKey(phase, label);
        if (!existingTextByLabel.TryGetValue(key, out var existingText))
            return defaultText;

        if (string.IsNullOrWhiteSpace(existingText))
            return defaultText;

        // 旧版の初期値はラベルと同じ文字列だったため、新しい初期値へ差し替える。
        // ユーザーが任意テキストへ変更している場合は、その値を保持する。
        if (string.Equals(existingText, label, StringComparison.Ordinal))
            return defaultText;

        return existingText;
    }

    private string CreatePhaseTextKey(string phase, string label)
    {
        return $"{phase ?? string.Empty}|{label ?? string.Empty}";
    }

    public void Save(IDalamudPluginInterface pluginInterface)
    {
        pluginInterface.SavePluginConfig(this);
    }
}

[Serializable]
public sealed class ActionTextSetting
{
    public bool Enabled { get; set; } = true;

    public string Label { get; set; } = string.Empty;

    public List<uint> ActionIds { get; set; } = new();

    public List<string> RequiredVfxPaths { get; set; } = new();

    public string Text { get; set; } = string.Empty;
}

[Serializable]
public sealed class MagicLookPhaseTextSetting
{
    public bool Enabled { get; set; } = true;

    public string Phase { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    public List<uint> ActionIds { get; set; } = new();

    public List<string> RequiredVfxPaths { get; set; } = new();

    public string Text { get; set; } = string.Empty;
}
