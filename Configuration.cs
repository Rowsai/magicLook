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

    public float MagicLookWorldHeightOffset { get; set; } = 2.35f;

    public float MagicLookScreenOffsetX { get; set; } = 0.0f;

    public float MagicLookScreenOffsetY { get; set; } = -20.0f;

    public float MagicLookFontSize { get; set; } = 28.0f;

    public float MagicLookDisplaySeconds { get; set; } = 2.0f;

    public bool MagicLookDrawBackground { get; set; } = true;

    public string MagicLookBothNoStepText { get; set; } = "両方踏まない";

    public string MagicLookBothStepText { get; set; } = "両方踏む";

    public string MagicLookLineOnlyStepText { get; set; } = "直線だけ踏む";

    public string MagicLookFanOnlyStepText { get; set; } = "扇だけ踏む";

    public bool MagicChargeEnabled { get; set; } = true;

    // 旧仕様互換用。現在のmagicCharge画面固定表示では使用しません。
    public float MagicChargeWorldHeightOffset { get; set; } = 2.85f;

    // 旧仕様互換用。現在のmagicCharge画面固定表示では使用しません。
    public float MagicChargeScreenOffsetX { get; set; } = 0.0f;

    // 旧仕様互換用。現在のmagicCharge画面固定表示では使用しません。
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

    public string MagicChargeTestText { get; set; } = "magicCharge テスト表示";

    public string MagicChargeUnknownText { get; set; } = "magicCharge 判定不能";

    public string MagicChargeFanNoStepLineNoStepText { get; set; } = "直線　踏む／扇　踏む";

    public string MagicChargeFanNoStepLineStepText { get; set; } = "直線　踏む／扇　踏まない";

    public string MagicChargeFanStepLineNoStepText { get; set; } = "直線　踏まない／扇　踏む";

    public string MagicChargeFanStepLineStepText { get; set; } = "直線　踏まない／扇　踏まない";

    public bool GrandCrossEnabled { get; set; } = true;

    public bool GrandCrossFilterByEnemyName { get; set; } = true;

    public string GrandCrossEnemyNameKeyword { get; set; } = "ネオエクスデス";

    public bool GrandCrossChaosFilterByEnemyName { get; set; } = true;

    public string GrandCrossChaosEnemyNameKeyword { get; set; } = "カオス";

    // 0 = プレイヤー頭上, 1 = 画面固定位置
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

    public float GrandCrossTextColorR { get; set; } = 1.00f;

    public float GrandCrossTextColorG { get; set; } = 0.45f;

    public float GrandCrossTextColorB { get; set; } = 0.95f;

    public float GrandCrossTextColorA { get; set; } = 1.00f;

    public bool GrandCrossUseOutline { get; set; } = true;

    public float GrandCrossOutlineThickness { get; set; } = 2.0f;

    public float GrandCrossOutlineColorR { get; set; } = 0.0f;

    public float GrandCrossOutlineColorG { get; set; } = 0.0f;

    public float GrandCrossOutlineColorB { get; set; } = 0.0f;

    public float GrandCrossOutlineColorA { get; set; } = 1.0f;

    public int GrandCrossFontPreset { get; set; } = 0;

    public float GrandCrossStatusCaptureSeconds { get; set; } = 3.0f;

    public string GrandCrossSeparator { get; set; } = "_";

    // 旧仕様互換用。現在の表示ロジックでは使用しません。
    public string GrandCrossFakePrefix { get; set; } = "偽";

    public string GrandCrossTestText { get; set; } = "GrandCross テスト表示";

    public string GrandCrossAllaganFieldTrueText { get; set; } = "アラガンフィールド";

    public string GrandCrossAllaganFieldFakeText { get; set; } = "偽アラガンフィールド";

    public string GrandCrossDeathBeyondTrueText { get; set; } = "死の超越";

    public string GrandCrossDeathBeyondFakeText { get; set; } = "偽死の超越";

    public string GrandCrossLivingWoundTrueText { get; set; } = "生者の傷";

    public string GrandCrossLivingWoundFakeText { get; set; } = "偽生者の傷";

    public string GrandCrossDeadWoundTrueText { get; set; } = "死者の傷";

    public string GrandCrossDeadWoundFakeText { get; set; } = "偽死者の傷";

    public string GrandCrossCurseShriekTrueText { get; set; } = "呪詛の叫声";

    public string GrandCrossCurseShriekFakeText { get; set; } = "偽呪詛の叫声";

    public string GrandCrossForkedLightningTrueText { get; set; } = "フォークライトニング";

    public string GrandCrossForkedLightningFakeText { get; set; } = "偽フォークライトニング";

    public string GrandCrossWaterCompressionTrueText { get; set; } = "水属性圧縮";

    public string GrandCrossWaterCompressionFakeText { get; set; } = "偽水属性圧縮";

    public string GrandCrossAccelerationBombTrueText { get; set; } = "加速度爆弾";

    public string GrandCrossAccelerationBombFakeText { get; set; } = "偽加速度爆弾";

    public string GrandCrossFireFakeText { get; set; } = "偽ほのお";

    public string GrandCrossFireTrueText { get; set; } = "ほのお";

    public string GrandCrossTsunamiFakeText { get; set; } = "偽つなみ";

    public string GrandCrossTsunamiTrueText { get; set; } = "つなみ";

    public List<ActionTextSetting> ActionTextSettings { get; set; } = new();

    public void EnsureDefaults()
    {
        this.ActionTextSettings ??= new List<ActionTextSetting>();

        this.EnsureActionTextSetting(
            "ひろがるブリザガ / ActionId: 47768",
            new List<uint> { 47768 },
            "ひろがるブリザガ 47768"
        );

        this.EnsureActionTextSetting(
            "ひろがるブリザガ / ActionId: 47771, 47774",
            new List<uint> { 47771, 47774 },
            "ひろがるブリザガ 47771 / 47774"
        );

        this.EnsureActionTextSetting(
            "もりもりサンダガ / ActionId: 47775",
            new List<uint> { 47775 },
            "もりもりサンダガ 47775"
        );

        this.EnsureActionTextSetting(
            "もりもりサンダガ / ActionId: 47776, 47777",
            new List<uint> { 47776, 47777 },
            "もりもりサンダガ 47776 / 47777"
        );
    }

    private void EnsureActionTextSetting(string label, List<uint> actionIds, string defaultText)
    {
        var existing = this.ActionTextSettings.FirstOrDefault(setting =>
            setting.ActionIds.Count == actionIds.Count &&
            actionIds.All(id => setting.ActionIds.Contains(id))
        );

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

    public string Text { get; set; } = string.Empty;
}