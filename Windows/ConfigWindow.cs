using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MagicLock;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin, Configuration configuration)
        : base("magicLock 設定###magicLockConfig")
    {
        this.plugin = plugin;
        this.configuration = configuration;

        this.Size = new Vector2(720, 720);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    public override void Draw()
    {
        var changed = false;

        changed |= this.DrawCommonSettings();

        ImGui.Separator();

        if (ImGui.BeginTabBar("magicLockTabBar"))
        {
            if (ImGui.BeginTabItem("magicLock"))
            {
                this.DrawMagicLockTab(ref changed);
                ImGui.EndTabItem();
            }

            if (ImGui.BeginTabItem("magicCharge"))
            {
                this.DrawMagicChargeTab(ref changed);
                ImGui.EndTabItem();
            }

            ImGui.EndTabBar();
        }

        if (changed)
            this.plugin.SaveConfig();
    }

    private bool DrawCommonSettings()
    {
        var changed = false;

        if (ImGui.CollapsingHeader("共通設定", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var enabled = this.configuration.Enabled;
            if (ImGui.Checkbox("プラグイン全体を有効化", ref enabled))
            {
                this.configuration.Enabled = enabled;
                changed = true;
            }

            var useTerritoryFilter = this.configuration.UseTerritoryFilter;
            if (ImGui.Checkbox("TerritoryTypeで絞り込む", ref useTerritoryFilter))
            {
                this.configuration.UseTerritoryFilter = useTerritoryFilter;
                changed = true;
            }

            var territory = (int)this.configuration.TerritoryType;
            if (ImGui.InputInt("TerritoryType", ref territory))
            {
                territory = Math.Clamp(territory, 0, ushort.MaxValue);
                this.configuration.TerritoryType = (ushort)territory;
                changed = true;
            }

            var filterByCasterName = this.configuration.FilterByCasterName;
            if (ImGui.Checkbox("詠唱者名で絞り込む", ref filterByCasterName))
            {
                this.configuration.FilterByCasterName = filterByCasterName;
                changed = true;
            }

            var casterKeyword = this.configuration.CasterNameKeyword;
            if (ImGui.InputText("詠唱者名キーワード", ref casterKeyword, 128))
            {
                this.configuration.CasterNameKeyword = casterKeyword;
                changed = true;
            }

            ImGui.TextUnformatted("※ 検出しない場合は、詠唱者名絞り込みをOFFにしてください。");
        }

        return changed;
    }

    private void DrawMagicLockTab(ref bool changed)
    {
        var magicLockEnabled = this.configuration.MagicLockEnabled;
        if (ImGui.Checkbox("magicLockを有効化", ref magicLockEnabled))
        {
            this.configuration.MagicLockEnabled = magicLockEnabled;
            changed = true;
        }

        if (ImGui.CollapsingHeader("magicLock 表示位置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var worldHeightOffset = this.configuration.MagicLockWorldHeightOffset;
            if (DrawFloat("高さ補正 / World Y", ref worldHeightOffset, 0.05f, 0.25f))
            {
                this.configuration.MagicLockWorldHeightOffset = worldHeightOffset;
                changed = true;
            }

            var screenOffsetX = this.configuration.MagicLockScreenOffsetX;
            if (DrawFloat("横位置補正 / Screen X", ref screenOffsetX, 1.0f, 10.0f))
            {
                this.configuration.MagicLockScreenOffsetX = screenOffsetX;
                changed = true;
            }

            var screenOffsetY = this.configuration.MagicLockScreenOffsetY;
            if (DrawFloat("縦位置補正 / Screen Y", ref screenOffsetY, 1.0f, 10.0f))
            {
                this.configuration.MagicLockScreenOffsetY = screenOffsetY;
                changed = true;
            }

            var fontSize = this.configuration.MagicLockFontSize;
            if (DrawFloat("文字サイズ", ref fontSize, 1.0f, 5.0f))
            {
                this.configuration.MagicLockFontSize = Math.Max(8.0f, fontSize);
                changed = true;
            }

            var displaySeconds = this.configuration.MagicLockDisplaySeconds;
            if (DrawFloat("表示秒数", ref displaySeconds, 0.1f, 0.5f))
            {
                this.configuration.MagicLockDisplaySeconds = Math.Max(0.1f, displaySeconds);
                changed = true;
            }

            var drawBackground = this.configuration.MagicLockDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicLockBg", ref drawBackground))
            {
                this.configuration.MagicLockDrawBackground = drawBackground;
                changed = true;
            }

            if (ImGui.Button("magicLock テスト表示"))
                this.plugin.TestLockText("magicLock テスト表示");
        }

        if (ImGui.CollapsingHeader("magicLock 組み合わせ判定 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var bothNoStepText = this.configuration.MagicLockBothNoStepText;
            if (ImGui.InputText("47768 + 47775", ref bothNoStepText, 256))
            {
                this.configuration.MagicLockBothNoStepText = bothNoStepText;
                changed = true;
            }

            var bothStepText = this.configuration.MagicLockBothStepText;
            if (ImGui.InputText("47771/47774 + 47776/47777", ref bothStepText, 256))
            {
                this.configuration.MagicLockBothStepText = bothStepText;
                changed = true;
            }

            var lineOnlyStepText = this.configuration.MagicLockLineOnlyStepText;
            if (ImGui.InputText("47768 + 47776/47777", ref lineOnlyStepText, 256))
            {
                this.configuration.MagicLockLineOnlyStepText = lineOnlyStepText;
                changed = true;
            }

            var fanOnlyStepText = this.configuration.MagicLockFanOnlyStepText;
            if (ImGui.InputText("47771/47774 + 47775", ref fanOnlyStepText, 256))
            {
                this.configuration.MagicLockFanOnlyStepText = fanOnlyStepText;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("magicLock アクション別 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            for (var i = 0; i < this.configuration.ActionTextSettings.Count; i++)
            {
                var setting = this.configuration.ActionTextSettings[i];

                ImGui.PushID(i);

                var settingEnabled = setting.Enabled;
                if (ImGui.Checkbox("有効", ref settingEnabled))
                {
                    setting.Enabled = settingEnabled;
                    changed = true;
                }

                ImGui.SameLine();
                ImGui.TextUnformatted(setting.Label);

                var text = setting.Text;
                if (ImGui.InputText("表示内容", ref text, 256))
                {
                    setting.Text = text;
                    changed = true;
                }

                if (ImGui.Button("このテキストをテスト表示"))
                    this.plugin.TestLockText(setting.Text);

                ImGui.Separator();
                ImGui.PopID();
            }
        }

        if (ImGui.CollapsingHeader("magicLock 直近検出情報", ImGuiTreeNodeFlags.DefaultOpen))
        {
            ImGui.TextUnformatted($"詠唱者: {this.plugin.LastCasterName}");
            ImGui.TextUnformatted($"ActionId: {this.plugin.LastActionId}");
            ImGui.TextUnformatted($"一致設定: {this.plugin.LastMatchedLabel}");
        }
    }

    private void DrawMagicChargeTab(ref bool changed)
    {
        var magicChargeEnabled = this.configuration.MagicChargeEnabled;
        if (ImGui.Checkbox("magicChargeを有効化", ref magicChargeEnabled))
        {
            this.configuration.MagicChargeEnabled = magicChargeEnabled;
            changed = true;
        }

        if (ImGui.CollapsingHeader("magicCharge 表示位置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var worldHeightOffset = this.configuration.MagicChargeWorldHeightOffset;
            if (DrawFloat("高さ補正 / World Y##Charge", ref worldHeightOffset, 0.05f, 0.25f))
            {
                this.configuration.MagicChargeWorldHeightOffset = worldHeightOffset;
                changed = true;
            }

            var screenOffsetX = this.configuration.MagicChargeScreenOffsetX;
            if (DrawFloat("横位置補正 / Screen X##Charge", ref screenOffsetX, 1.0f, 10.0f))
            {
                this.configuration.MagicChargeScreenOffsetX = screenOffsetX;
                changed = true;
            }

            var screenOffsetY = this.configuration.MagicChargeScreenOffsetY;
            if (DrawFloat("縦位置補正 / Screen Y##Charge", ref screenOffsetY, 1.0f, 10.0f))
            {
                this.configuration.MagicChargeScreenOffsetY = screenOffsetY;
                changed = true;
            }

            var fontSize = this.configuration.MagicChargeFontSize;
            if (DrawFloat("文字サイズ##Charge", ref fontSize, 1.0f, 5.0f))
            {
                this.configuration.MagicChargeFontSize = Math.Max(8.0f, fontSize);
                changed = true;
            }

            var displaySeconds = this.configuration.MagicChargeDisplaySeconds;
            if (DrawFloat("表示秒数##Charge", ref displaySeconds, 0.1f, 0.5f))
            {
                this.configuration.MagicChargeDisplaySeconds = Math.Max(0.1f, displaySeconds);
                changed = true;
            }

            var drawBackground = this.configuration.MagicChargeDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicChargeBg", ref drawBackground))
            {
                this.configuration.MagicChargeDrawBackground = drawBackground;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("magicCharge 判定結果 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var text1 = this.configuration.MagicChargeFanNoStepLineNoStepText;
            if (ImGui.InputText("47768 + 47775", ref text1, 256))
            {
                this.configuration.MagicChargeFanNoStepLineNoStepText = text1;
                changed = true;
            }

            var text2 = this.configuration.MagicChargeFanNoStepLineStepText;
            if (ImGui.InputText("47768 + 47776/47777", ref text2, 256))
            {
                this.configuration.MagicChargeFanNoStepLineStepText = text2;
                changed = true;
            }

            var text3 = this.configuration.MagicChargeFanStepLineNoStepText;
            if (ImGui.InputText("47771/47774 + 47775", ref text3, 256))
            {
                this.configuration.MagicChargeFanStepLineNoStepText = text3;
                changed = true;
            }

            var text4 = this.configuration.MagicChargeFanStepLineStepText;
            if (ImGui.InputText("47771/47774 + 47776/47777", ref text4, 256))
            {
                this.configuration.MagicChargeFanStepLineStepText = text4;
                changed = true;
            }

            var unknownText = this.configuration.MagicChargeUnknownText;
            if (ImGui.InputText("判定不能時", ref unknownText, 256))
            {
                this.configuration.MagicChargeUnknownText = unknownText;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("magicCharge テストモード", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var testText = this.configuration.MagicChargeTestText;
            if (ImGui.InputText("テスト表示文字", ref testText, 256))
            {
                this.configuration.MagicChargeTestText = testText;
                changed = true;
            }

            if (ImGui.Button("magicCharge位置にテスト表示"))
                this.plugin.TestChargeText(this.configuration.MagicChargeTestText);
        }

        if (ImGui.CollapsingHeader("magicCharge チャージ状況リスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var line in this.plugin.GetMagicChargeStatusLines())
            {
                ImGui.TextUnformatted($"・{line}");
            }

            if (ImGui.Button("チャージ状況をリセット"))
                this.plugin.ResetMagicChargeState();
        }
    }

    private static bool DrawFloat(string label, ref float value, float step, float stepFast)
    {
        var before = value;
        ImGui.InputFloat(label, ref value, step, stepFast, "%.2f");
        return Math.Abs(before - value) > 0.0001f;
    }
}
