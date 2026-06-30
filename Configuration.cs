using System;
using System.Collections.Generic;
using Dalamud.Configuration;
using Dalamud.Plugin;

namespace MagicLock;

[Serializable]
public sealed class Configuration : IPluginConfiguration
{
    public int Version { get; set; } = 1;

    public bool Enabled { get; set; } = true;

    public bool UseTerritoryFilter { get; set; } = true;

    public ushort TerritoryType { get; set; } = 1363;

    public bool FilterByCasterName { get; set; } = false;

    public string CasterNameKeyword { get; set; } = "ケフカ";

    public bool MagicLockEnabled { get; set; } = true;

    public float MagicLockWorldHeightOffset { get; set; } = 2.35f;

    public float MagicLockScreenOffsetX { get; set; } = 0.0f;

    public float MagicLockScreenOffsetY { get; set; } = -20.0f;

    public float MagicLockFontSize { get; set; } = 28.0f;

    public float MagicLockDisplaySeconds { get; set; } = 2.0f;

    public bool MagicLockDrawBackground { get; set; } = true;

    public string MagicLockBothNoStepText { get; set; } = "両方踏まない";

    public string MagicLockBothStepText { get; set; } = "両方踏む";

    public string MagicLockLineOnlyStepText { get; set; } = "直線だけ踏む";

    public string MagicLockFanOnlyStepText { get; set; } = "扇だけ踏む";

    public bool MagicChargeEnabled { get; set; } = true;

    public float MagicChargeWorldHeightOffset { get; set; } = 2.85f;

    public float MagicChargeScreenOffsetX { get; set; } = 0.0f;

    public float MagicChargeScreenOffsetY { get; set; } = -75.0f;

    public float MagicChargeFontSize { get; set; } = 28.0f;

    public float MagicChargeDisplaySeconds { get; set; } = 3.0f;

    public bool MagicChargeDrawBackground { get; set; } = true;

    public string MagicChargeTestText { get; set; } = "magicCharge テスト表示";

    public string MagicChargeUnknownText { get; set; } = "magicCharge 判定不能";

    // 47768 + 47775
    public string MagicChargeFanNoStepLineNoStepText { get; set; } = "直線　踏む／扇　踏む";

    // 47768 + 47776/47777
    public string MagicChargeFanNoStepLineStepText { get; set; } = "直線　踏む／扇　踏まない";

    // 47771/47774 + 47775
    public string MagicChargeFanStepLineNoStepText { get; set; } = "直線　踏まない／扇　踏む";

    // 47771/47774 + 47776/47777
    public string MagicChargeFanStepLineStepText { get; set; } = "直線　踏まない／扇　踏まない";

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
