using System;
using System.Collections.Generic;
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

    public float MagicChargeWorldHeightOffset { get; set; } = 2.85f;

    public float MagicChargeScreenOffsetX { get; set; } = 0.0f;

    public float MagicChargeScreenOffsetY { get; set; } = -75.0f;

    public float MagicChargeFontSize { get; set; } = 28.0f;

    public float MagicChargeDisplaySeconds { get; set; } = 3.0f;

    public bool MagicChargeDrawBackground { get; set; } = true;

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

    public float GrandCrossWorldHeightOffset { get; set; } = 3.25f;

    public float GrandCrossScreenOffsetX { get; set; } = 0.0f;

    public float GrandCrossScreenOffsetY { get; set; } = -130.0f;

    public float GrandCrossFontSize { get; set; } = 28.0f;

    public float GrandCrossDisplaySeconds { get; set; } = 5.0f;

    public bool GrandCrossDrawBackground { get; set; } = true;

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
        if (this.ActionTextSettings.Count > 0)
            return;

        this.ActionTextSettings.Add(new ActionTextSetting
        {
            Enabled = true,
            Label = "ひろがるブリザガ / ActionId: 47768",
            ActionIds = new List<uint> { 47768 },
            Text = "ひろがるブリザガ 47768"
        });

        this.ActionTextSettings.Add(new ActionTextSetting
        {
            Enabled = true,
            Label = "ひろがるブリザガ / ActionId: 47771, 47774",
            ActionIds = new List<uint> { 47771, 47774 },
            Text = "ひろがるブリザガ 47771 / 47774"
        });

        this.ActionTextSettings.Add(new ActionTextSetting
        {
            Enabled = true,
            Label = "もりもりサンダガ / ActionId: 47775",
            ActionIds = new List<uint> { 47775 },
            Text = "もりもりサンダガ 47775"
        });

        this.ActionTextSettings.Add(new ActionTextSetting
        {
            Enabled = true,
            Label = "もりもりサンダガ / ActionId: 47776, 47777",
            ActionIds = new List<uint> { 47776, 47777 },
            Text = "もりもりサンダガ 47776 / 47777"
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