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

        this.Size = new Vector2(760, 760);
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

            if (ImGui.BeginTabItem("GrandCross"))
            {
                this.DrawGrandCrossTab(ref changed);
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
            if (ImGui.Checkbox("ケフカ系 詠唱者名で絞り込む", ref filterByCasterName))
            {
                this.configuration.FilterByCasterName = filterByCasterName;
                changed = true;
            }

            var casterKeyword = this.configuration.CasterNameKeyword;
            if (ImGui.InputText("ケフカ系 詠唱者名キーワード", ref casterKeyword, 128))
            {
                this.configuration.CasterNameKeyword = casterKeyword;
                changed = true;
            }

            ImGui.TextUnformatted("※ magicLock / magicCharge の検出に使います。検出しない場合はOFFにしてください。");
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
            changed |= DrawFloatAndAssign("高さ補正 / World Y", this.configuration.MagicLockWorldHeightOffset, v => this.configuration.MagicLockWorldHeightOffset = v, 0.05f, 0.25f);
            changed |= DrawFloatAndAssign("横位置補正 / Screen X", this.configuration.MagicLockScreenOffsetX, v => this.configuration.MagicLockScreenOffsetX = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("縦位置補正 / Screen Y", this.configuration.MagicLockScreenOffsetY, v => this.configuration.MagicLockScreenOffsetY = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("文字サイズ", this.configuration.MagicLockFontSize, v => this.configuration.MagicLockFontSize = Math.Max(8.0f, v), 1.0f, 5.0f);
            changed |= DrawFloatAndAssign("表示秒数", this.configuration.MagicLockDisplaySeconds, v => this.configuration.MagicLockDisplaySeconds = Math.Max(0.1f, v), 0.1f, 0.5f);

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
            changed |= DrawInputTextAndAssign("47768 + 47775", this.configuration.MagicLockBothNoStepText, v => this.configuration.MagicLockBothNoStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47776/47777", this.configuration.MagicLockBothStepText, v => this.configuration.MagicLockBothStepText = v);
            changed |= DrawInputTextAndAssign("47768 + 47776/47777", this.configuration.MagicLockLineOnlyStepText, v => this.configuration.MagicLockLineOnlyStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47775", this.configuration.MagicLockFanOnlyStepText, v => this.configuration.MagicLockFanOnlyStepText = v);
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
            changed |= DrawFloatAndAssign("高さ補正 / World Y##Charge", this.configuration.MagicChargeWorldHeightOffset, v => this.configuration.MagicChargeWorldHeightOffset = v, 0.05f, 0.25f);
            changed |= DrawFloatAndAssign("横位置補正 / Screen X##Charge", this.configuration.MagicChargeScreenOffsetX, v => this.configuration.MagicChargeScreenOffsetX = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("縦位置補正 / Screen Y##Charge", this.configuration.MagicChargeScreenOffsetY, v => this.configuration.MagicChargeScreenOffsetY = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("文字サイズ##Charge", this.configuration.MagicChargeFontSize, v => this.configuration.MagicChargeFontSize = Math.Max(8.0f, v), 1.0f, 5.0f);
            changed |= DrawFloatAndAssign("表示秒数##Charge", this.configuration.MagicChargeDisplaySeconds, v => this.configuration.MagicChargeDisplaySeconds = Math.Max(0.1f, v), 0.1f, 0.5f);

            var drawBackground = this.configuration.MagicChargeDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicChargeBg", ref drawBackground))
            {
                this.configuration.MagicChargeDrawBackground = drawBackground;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("magicCharge 判定結果 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("47768 + 47775", this.configuration.MagicChargeFanNoStepLineNoStepText, v => this.configuration.MagicChargeFanNoStepLineNoStepText = v);
            changed |= DrawInputTextAndAssign("47768 + 47776/47777", this.configuration.MagicChargeFanNoStepLineStepText, v => this.configuration.MagicChargeFanNoStepLineStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47775", this.configuration.MagicChargeFanStepLineNoStepText, v => this.configuration.MagicChargeFanStepLineNoStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47776/47777", this.configuration.MagicChargeFanStepLineStepText, v => this.configuration.MagicChargeFanStepLineStepText = v);
            changed |= DrawInputTextAndAssign("判定不能時", this.configuration.MagicChargeUnknownText, v => this.configuration.MagicChargeUnknownText = v);
        }

        if (ImGui.CollapsingHeader("magicCharge テストモード", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("テスト表示文字", this.configuration.MagicChargeTestText, v => this.configuration.MagicChargeTestText = v);

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

    private void DrawGrandCrossTab(ref bool changed)
    {
        var grandCrossEnabled = this.configuration.GrandCrossEnabled;
        if (ImGui.Checkbox("GrandCrossを有効化", ref grandCrossEnabled))
        {
            this.configuration.GrandCrossEnabled = grandCrossEnabled;
            changed = true;
        }

        if (ImGui.CollapsingHeader("GrandCross 対象設定", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var filterByEnemyName = this.configuration.GrandCrossFilterByEnemyName;
            if (ImGui.Checkbox("エネミー名で絞り込む", ref filterByEnemyName))
            {
                this.configuration.GrandCrossFilterByEnemyName = filterByEnemyName;
                changed = true;
            }

            changed |= DrawInputTextAndAssign("エネミー名キーワード", this.configuration.GrandCrossEnemyNameKeyword, v => this.configuration.GrandCrossEnemyNameKeyword = v);

            ImGui.TextUnformatted("対象ActionId: 47892 / グランドクロス");
            ImGui.TextUnformatted("内部ステータス: StatusId 2056 / Param 1121=偽, 1122=本物");
        }

        if (ImGui.CollapsingHeader("GrandCross 表示位置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawFloatAndAssign("高さ補正 / World Y##GrandCross", this.configuration.GrandCrossWorldHeightOffset, v => this.configuration.GrandCrossWorldHeightOffset = v, 0.05f, 0.25f);
            changed |= DrawFloatAndAssign("横位置補正 / Screen X##GrandCross", this.configuration.GrandCrossScreenOffsetX, v => this.configuration.GrandCrossScreenOffsetX = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("縦位置補正 / Screen Y##GrandCross", this.configuration.GrandCrossScreenOffsetY, v => this.configuration.GrandCrossScreenOffsetY = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("文字サイズ##GrandCross", this.configuration.GrandCrossFontSize, v => this.configuration.GrandCrossFontSize = Math.Max(8.0f, v), 1.0f, 5.0f);
            changed |= DrawFloatAndAssign("表示秒数##GrandCross", this.configuration.GrandCrossDisplaySeconds, v => this.configuration.GrandCrossDisplaySeconds = Math.Max(0.1f, v), 0.1f, 0.5f);
            changed |= DrawFloatAndAssign("ステータス取得待機秒数##GrandCross", this.configuration.GrandCrossStatusCaptureSeconds, v => this.configuration.GrandCrossStatusCaptureSeconds = Math.Max(0.1f, v), 0.1f, 0.5f);

            var drawBackground = this.configuration.GrandCrossDrawBackground;
            if (ImGui.Checkbox("背景を表示する##GrandCrossBg", ref drawBackground))
            {
                this.configuration.GrandCrossDrawBackground = drawBackground;
                changed = true;
            }
        }

        if (ImGui.CollapsingHeader("GrandCross 表示テキスト設定", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("区切り文字", this.configuration.GrandCrossSeparator, v => this.configuration.GrandCrossSeparator = v);
            changed |= DrawInputTextAndAssign("偽プレフィックス", this.configuration.GrandCrossFakePrefix, v => this.configuration.GrandCrossFakePrefix = v);

            ImGui.Separator();

            changed |= DrawInputTextAndAssign("454 / アラガンフィールド", this.configuration.GrandCrossAllaganFieldText, v => this.configuration.GrandCrossAllaganFieldText = v);
            changed |= DrawInputTextAndAssign("5464 / 死の超越", this.configuration.GrandCrossDeathBeyondText, v => this.configuration.GrandCrossDeathBeyondText = v);
            changed |= DrawInputTextAndAssign("4887 / 生者の傷", this.configuration.GrandCrossLivingWoundText, v => this.configuration.GrandCrossLivingWoundText = v);
            changed |= DrawInputTextAndAssign("4888 / 死者の傷", this.configuration.GrandCrossDeadWoundText, v => this.configuration.GrandCrossDeadWoundText = v);
            changed |= DrawInputTextAndAssign("5543 / 呪詛の叫声", this.configuration.GrandCrossCurseShriekText, v => this.configuration.GrandCrossCurseShriekText = v);
            changed |= DrawInputTextAndAssign("5544 / フォークライトニング", this.configuration.GrandCrossForkedLightningText, v => this.configuration.GrandCrossForkedLightningText = v);
            changed |= DrawInputTextAndAssign("5545 / 水属性圧縮", this.configuration.GrandCrossWaterCompressionText, v => this.configuration.GrandCrossWaterCompressionText = v);
        }

        if (ImGui.CollapsingHeader("GrandCross テストモード", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("テスト表示文字", this.configuration.GrandCrossTestText, v => this.configuration.GrandCrossTestText = v);

            if (ImGui.Button("GrandCross位置にテスト表示"))
                this.plugin.TestGrandCrossText(this.configuration.GrandCrossTestText);
        }

        if (ImGui.CollapsingHeader("GrandCross 保持状況リスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var line in this.plugin.GetGrandCrossStatusLines())
            {
                ImGui.TextUnformatted($"・{line}");
            }

            if (ImGui.Button("GrandCross保持状況をリセット"))
                this.plugin.ResetGrandCrossState();
        }
    }

    private static bool DrawInputTextAndAssign(string label, string currentValue, Action<string> assign)
    {
        var value = currentValue;
        if (!ImGui.InputText(label, ref value, 256))
            return false;

        assign(value);
        return true;
    }

    private static bool DrawFloatAndAssign(string label, float currentValue, Action<float> assign, float step, float stepFast)
    {
        var value = currentValue;
        var before = value;

        ImGui.InputFloat(label, ref value, step, stepFast, "%.2f");

        if (Math.Abs(before - value) <= 0.0001f)
            return false;

        assign(value);
        return true;
    }
}