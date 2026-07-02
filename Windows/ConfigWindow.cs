using System;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MagicLook;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin, Configuration configuration)
        : base("magicLook 設定###magicLookConfig")
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

        if (ImGui.BeginTabBar("magicLookTabBar"))
        {
            if (ImGui.BeginTabItem("magicLook"))
            {
                this.DrawMagicLookTab(ref changed);
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

            ImGui.TextUnformatted("※ magicLook / magicCharge の検出に使います。検出しない場合はOFFにしてください。");
        }

        return changed;
    }

    private void DrawMagicLookTab(ref bool changed)
    {
        var magicLookEnabled = this.configuration.MagicLookEnabled;
        if (ImGui.Checkbox("magicLookを有効化", ref magicLookEnabled))
        {
            this.configuration.MagicLookEnabled = magicLookEnabled;
            changed = true;
        }

        if (ImGui.CollapsingHeader("magicLook 表示位置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawFloatAndAssign("高さ補正 / World Y", this.configuration.MagicLookWorldHeightOffset, v => this.configuration.MagicLookWorldHeightOffset = v, 0.05f, 0.25f);
            changed |= DrawFloatAndAssign("横位置補正 / Screen X", this.configuration.MagicLookScreenOffsetX, v => this.configuration.MagicLookScreenOffsetX = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("縦位置補正 / Screen Y", this.configuration.MagicLookScreenOffsetY, v => this.configuration.MagicLookScreenOffsetY = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("文字サイズ", this.configuration.MagicLookFontSize, v => this.configuration.MagicLookFontSize = Math.Max(8.0f, v), 1.0f, 5.0f);
            changed |= DrawFloatAndAssign("表示秒数", this.configuration.MagicLookDisplaySeconds, v => this.configuration.MagicLookDisplaySeconds = Math.Max(0.1f, v), 0.1f, 0.5f);

            var drawBackground = this.configuration.MagicLookDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicLookBg", ref drawBackground))
            {
                this.configuration.MagicLookDrawBackground = drawBackground;
                changed = true;
            }

            if (ImGui.Button("magicLook テスト表示"))
                this.plugin.TestLockText("magicLook テスト表示");
        }

        if (ImGui.CollapsingHeader("magicLook 組み合わせ判定 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("47768 + 47775", this.configuration.MagicLookBothNoStepText, v => this.configuration.MagicLookBothNoStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47776/47777", this.configuration.MagicLookBothStepText, v => this.configuration.MagicLookBothStepText = v);
            changed |= DrawInputTextAndAssign("47768 + 47776/47777", this.configuration.MagicLookLineOnlyStepText, v => this.configuration.MagicLookLineOnlyStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47775", this.configuration.MagicLookFanOnlyStepText, v => this.configuration.MagicLookFanOnlyStepText = v);
        }

        if (ImGui.CollapsingHeader("magicLook アクション別 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            if (this.configuration.ActionTextSettings.Count == 0)
            {
                ImGui.TextUnformatted("magicLookのActionTextSettingsが空です。設定初期化に失敗している可能性があります。");
            }

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

        if (ImGui.CollapsingHeader("magicLook 直近検出情報", ImGuiTreeNodeFlags.DefaultOpen))
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

        if (ImGui.CollapsingHeader("magicCharge 表示設定", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawFloatAndAssign("画面位置 X##ChargeScreenPosX", this.configuration.MagicChargeScreenPositionX, v => this.configuration.MagicChargeScreenPositionX = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("画面位置 Y##ChargeScreenPosY", this.configuration.MagicChargeScreenPositionY, v => this.configuration.MagicChargeScreenPositionY = v, 1.0f, 10.0f);
            changed |= DrawFloatAndAssign("文字サイズ##Charge", this.configuration.MagicChargeFontSize, v => this.configuration.MagicChargeFontSize = Math.Max(8.0f, v), 1.0f, 5.0f);
            changed |= DrawFloatAndAssign("表示秒数##Charge", this.configuration.MagicChargeDisplaySeconds, v => this.configuration.MagicChargeDisplaySeconds = Math.Max(0.1f, v), 0.1f, 0.5f);
            changed |= DrawFloatAndAssign("フェードイン秒数##ChargeFadeIn", this.configuration.MagicChargeFadeInSeconds, v => this.configuration.MagicChargeFadeInSeconds = Math.Max(0.0f, v), 0.05f, 0.25f);

            var drawBackground = this.configuration.MagicChargeDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicChargeBg", ref drawBackground))
            {
                this.configuration.MagicChargeDrawBackground = drawBackground;
                changed = true;
            }

            var color = new Vector4(
                this.configuration.MagicChargeTextColorR,
                this.configuration.MagicChargeTextColorG,
                this.configuration.MagicChargeTextColorB,
                this.configuration.MagicChargeTextColorA
            );

            if (ImGui.ColorEdit4("文字色##MagicChargeTextColor", ref color))
            {
                this.configuration.MagicChargeTextColorR = color.X;
                this.configuration.MagicChargeTextColorG = color.Y;
                this.configuration.MagicChargeTextColorB = color.Z;
                this.configuration.MagicChargeTextColorA = color.W;
                changed = true;
            }

            ImGui.Separator();

            var useOutline = this.configuration.MagicChargeUseOutline;
            if (ImGui.Checkbox("文字の縁取りを有効化##MagicChargeOutline", ref useOutline))
            {
                this.configuration.MagicChargeUseOutline = useOutline;
                changed = true;
            }

            changed |= DrawFloatAndAssign(
                "縁取り太さ##MagicChargeOutlineThickness",
                this.configuration.MagicChargeOutlineThickness,
                v => this.configuration.MagicChargeOutlineThickness = Math.Max(0.0f, v),
                0.1f,
                0.5f
            );

            var outlineColor = new Vector4(
                this.configuration.MagicChargeOutlineColorR,
                this.configuration.MagicChargeOutlineColorG,
                this.configuration.MagicChargeOutlineColorB,
                this.configuration.MagicChargeOutlineColorA
            );

            if (ImGui.ColorEdit4("縁取り色##MagicChargeOutlineColor", ref outlineColor))
            {
                this.configuration.MagicChargeOutlineColorR = outlineColor.X;
                this.configuration.MagicChargeOutlineColorG = outlineColor.Y;
                this.configuration.MagicChargeOutlineColorB = outlineColor.Z;
                this.configuration.MagicChargeOutlineColorA = outlineColor.W;
                changed = true;
            }

            var fontPreset = this.configuration.MagicChargeFontPreset;
            var fontItems = new[]
            {
                "デフォルトくっきり",
                "大きめくっきり",
                "強調くっきり",
                "小さめくっきり"
            };

            if (ImGui.Combo("表示プリセット##MagicChargeFontPreset", ref fontPreset, fontItems, fontItems.Length))
            {
                this.configuration.MagicChargeFontPreset = Math.Clamp(fontPreset, 0, fontItems.Length - 1);
                changed = true;
            }

            ImGui.TextUnformatted("※ 実フォント切替ではなく、にじみを抑える安全な表示プリセットです。");
        }

        if (ImGui.CollapsingHeader("magicCharge 判定結果 表示テキスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("47768 + 47775", this.configuration.MagicChargeFanNoStepLineNoStepText, v => this.configuration.MagicChargeFanNoStepLineNoStepText = v);
            changed |= DrawInputTextAndAssign("47768 + 47776/47777", this.configuration.MagicChargeFanNoStepLineStepText, v => this.configuration.MagicChargeFanNoStepLineStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47775", this.configuration.MagicChargeFanStepLineNoStepText, v => this.configuration.MagicChargeFanStepLineNoStepText = v);
            changed |= DrawInputTextAndAssign("47771/47774 + 47776/47777", this.configuration.MagicChargeFanStepLineStepText, v => this.configuration.MagicChargeFanStepLineStepText = v);
            changed |= DrawInputTextAndAssign("判定不能時", this.configuration.MagicChargeUnknownText, v => this.configuration.MagicChargeUnknownText = v);

            ImGui.TextUnformatted("※ 現在、マジックアウト後の最終判定仕様は保留中です。");
        }

        if (ImGui.CollapsingHeader("magicCharge テストモード", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign(
                "テスト表示文字",
                this.configuration.MagicChargeTestText,
                v => this.configuration.MagicChargeTestText = v
            );

            if (ImGui.Button("画面固定位置にテスト表示"))
                this.plugin.TestChargeText(this.configuration.MagicChargeTestText);

            ImGui.TextUnformatted("※ 画面位置・文字色・縁取り・表示プリセット・フェードイン秒数・背景設定を反映します。");
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
            if (ImGui.Checkbox("ネオエクスデス名で絞り込む", ref filterByEnemyName))
            {
                this.configuration.GrandCrossFilterByEnemyName = filterByEnemyName;
                changed = true;
            }

            changed |= DrawInputTextAndAssign("ネオエクスデス名キーワード", this.configuration.GrandCrossEnemyNameKeyword, v => this.configuration.GrandCrossEnemyNameKeyword = v);

            ImGui.TextUnformatted("対象ActionId: 47892 / グランドクロス");
            ImGui.TextUnformatted("内部ステータス: StatusId 2056 / Param 1121=偽, 1122=真");
            ImGui.TextUnformatted("デバフ取得対象: 自分のみ");

            ImGui.Separator();

            var chaosFilterByEnemyName = this.configuration.GrandCrossChaosFilterByEnemyName;
            if (ImGui.Checkbox("カオス名で絞り込む", ref chaosFilterByEnemyName))
            {
                this.configuration.GrandCrossChaosFilterByEnemyName = chaosFilterByEnemyName;
                changed = true;
            }

            changed |= DrawInputTextAndAssign("カオス名キーワード", this.configuration.GrandCrossChaosEnemyNameKeyword, v => this.configuration.GrandCrossChaosEnemyNameKeyword = v);

            ImGui.TextUnformatted("対象ActionId: 47902 / ほのお");
            ImGui.TextUnformatted("対象ActionId: 47903 / つなみ");
            ImGui.TextUnformatted("内部ステータス: StatusId 2056 / Param 1119=偽, 1120=真");
            ImGui.TextUnformatted("ほのお詠唱後Status: 5547 / 混沌の炎");
            ImGui.TextUnformatted("つなみ詠唱後Status: 5548 / 混沌の水");
        }

        if (ImGui.CollapsingHeader("GrandCross 表示位置", ImGuiTreeNodeFlags.DefaultOpen))
        {
            var displayMode = this.configuration.GrandCrossDisplayMode;
            var displayModeItems = new[]
            {
                "プレイヤー頭上に表示",
                "画面固定位置に表示"
            };

            if (ImGui.Combo("表示先##GrandCrossDisplayMode", ref displayMode, displayModeItems, displayModeItems.Length))
            {
                this.configuration.GrandCrossDisplayMode = Math.Clamp(displayMode, 0, displayModeItems.Length - 1);
                changed = true;
            }

            ImGui.Separator();

            if (this.configuration.GrandCrossDisplayMode == 0)
            {
                ImGui.TextUnformatted("プレイヤー頭上に表示する設定");

                changed |= DrawFloatAndAssign(
                    "高さ補正 / World Y##GrandCross",
                    this.configuration.GrandCrossWorldHeightOffset,
                    v => this.configuration.GrandCrossWorldHeightOffset = v,
                    0.05f,
                    0.25f
                );

                changed |= DrawFloatAndAssign(
                    "横位置補正 / Screen X##GrandCross",
                    this.configuration.GrandCrossScreenOffsetX,
                    v => this.configuration.GrandCrossScreenOffsetX = v,
                    1.0f,
                    10.0f
                );

                changed |= DrawFloatAndAssign(
                    "縦位置補正 / Screen Y##GrandCross",
                    this.configuration.GrandCrossScreenOffsetY,
                    v => this.configuration.GrandCrossScreenOffsetY = v,
                    1.0f,
                    10.0f
                );

                changed |= DrawFloatAndAssign(
                    "文字サイズ##GrandCross",
                    this.configuration.GrandCrossFontSize,
                    v => this.configuration.GrandCrossFontSize = Math.Max(8.0f, v),
                    1.0f,
                    5.0f
                );

                changed |= DrawFloatAndAssign(
                    "表示秒数##GrandCross",
                    this.configuration.GrandCrossDisplaySeconds,
                    v => this.configuration.GrandCrossDisplaySeconds = Math.Max(0.1f, v),
                    0.1f,
                    0.5f
                );

                ImGui.TextUnformatted("※ 現在と同じ、プレイヤー頭上への表示です。");
            }
            else
            {
                ImGui.TextUnformatted("画面固定位置に表示する設定");

                changed |= DrawFloatAndAssign(
                    "画面位置 X##GrandCrossScreenPosX",
                    this.configuration.GrandCrossScreenPositionX,
                    v => this.configuration.GrandCrossScreenPositionX = v,
                    1.0f,
                    10.0f
                );

                changed |= DrawFloatAndAssign(
                    "画面位置 Y##GrandCrossScreenPosY",
                    this.configuration.GrandCrossScreenPositionY,
                    v => this.configuration.GrandCrossScreenPositionY = v,
                    1.0f,
                    10.0f
                );

                changed |= DrawFloatAndAssign(
                    "文字サイズ##GrandCrossFixed",
                    this.configuration.GrandCrossFontSize,
                    v => this.configuration.GrandCrossFontSize = Math.Max(8.0f, v),
                    1.0f,
                    5.0f
                );

                changed |= DrawFloatAndAssign(
                    "表示秒数##GrandCrossFixed",
                    this.configuration.GrandCrossDisplaySeconds,
                    v => this.configuration.GrandCrossDisplaySeconds = Math.Max(0.1f, v),
                    0.1f,
                    0.5f
                );

                changed |= DrawFloatAndAssign(
                    "フェードイン秒数##GrandCrossFadeIn",
                    this.configuration.GrandCrossFadeInSeconds,
                    v => this.configuration.GrandCrossFadeInSeconds = Math.Max(0.0f, v),
                    0.05f,
                    0.25f
                );

                var useOutline = this.configuration.GrandCrossUseOutline;
                if (ImGui.Checkbox("文字の縁取りを有効化##GrandCrossOutline", ref useOutline))
                {
                    this.configuration.GrandCrossUseOutline = useOutline;
                    changed = true;
                }

                changed |= DrawFloatAndAssign(
                    "縁取り太さ##GrandCrossOutlineThickness",
                    this.configuration.GrandCrossOutlineThickness,
                    v => this.configuration.GrandCrossOutlineThickness = Math.Max(0.0f, v),
                    0.1f,
                    0.5f
                );

                var outlineColor = new Vector4(
                    this.configuration.GrandCrossOutlineColorR,
                    this.configuration.GrandCrossOutlineColorG,
                    this.configuration.GrandCrossOutlineColorB,
                    this.configuration.GrandCrossOutlineColorA
                );

                if (ImGui.ColorEdit4("縁取り色##GrandCrossOutlineColor", ref outlineColor))
                {
                    this.configuration.GrandCrossOutlineColorR = outlineColor.X;
                    this.configuration.GrandCrossOutlineColorG = outlineColor.Y;
                    this.configuration.GrandCrossOutlineColorB = outlineColor.Z;
                    this.configuration.GrandCrossOutlineColorA = outlineColor.W;
                    changed = true;
                }

                var fontPreset = this.configuration.GrandCrossFontPreset;
                var fontItems = new[]
                {
                    "デフォルトくっきり",
                    "大きめくっきり",
                    "強調くっきり",
                    "小さめくっきり"
                };

                if (ImGui.Combo("表示プリセット##GrandCrossFontPreset", ref fontPreset, fontItems, fontItems.Length))
                {
                    this.configuration.GrandCrossFontPreset = Math.Clamp(fontPreset, 0, fontItems.Length - 1);
                    changed = true;
                }

                ImGui.TextUnformatted("※ magicChargeと同じ、画面固定位置への表示です。");
            }

            ImGui.Separator();

            var drawBackground = this.configuration.GrandCrossDrawBackground;
            if (ImGui.Checkbox("背景を表示する##GrandCrossBg", ref drawBackground))
            {
                this.configuration.GrandCrossDrawBackground = drawBackground;
                changed = true;
            }

            var grandCrossTextColor = new Vector4(
                this.configuration.GrandCrossTextColorR,
                this.configuration.GrandCrossTextColorG,
                this.configuration.GrandCrossTextColorB,
                this.configuration.GrandCrossTextColorA
            );

            if (ImGui.ColorEdit4("文字色##GrandCrossTextColor", ref grandCrossTextColor))
            {
                this.configuration.GrandCrossTextColorR = grandCrossTextColor.X;
                this.configuration.GrandCrossTextColorG = grandCrossTextColor.Y;
                this.configuration.GrandCrossTextColorB = grandCrossTextColor.Z;
                this.configuration.GrandCrossTextColorA = grandCrossTextColor.W;
                changed = true;
            }

            changed |= DrawFloatAndAssign(
                "ステータス取得待機秒数##GrandCross",
                this.configuration.GrandCrossStatusCaptureSeconds,
                v => this.configuration.GrandCrossStatusCaptureSeconds = Math.Max(0.1f, v),
                0.1f,
                0.5f
            );
        }

        if (ImGui.CollapsingHeader("GrandCross 表示テキスト設定", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("区切り文字", this.configuration.GrandCrossSeparator, v => this.configuration.GrandCrossSeparator = v);

            ImGui.Separator();
            ImGui.TextUnformatted("GrandCross デバフ表示テキスト / 真偽別");

            changed |= DrawInputTextAndAssign("454 / アラガンフィールド / 真", this.configuration.GrandCrossAllaganFieldTrueText, v => this.configuration.GrandCrossAllaganFieldTrueText = v);
            changed |= DrawInputTextAndAssign("454 / アラガンフィールド / 偽", this.configuration.GrandCrossAllaganFieldFakeText, v => this.configuration.GrandCrossAllaganFieldFakeText = v);

            changed |= DrawInputTextAndAssign("5464 / 死の超越 / 真", this.configuration.GrandCrossDeathBeyondTrueText, v => this.configuration.GrandCrossDeathBeyondTrueText = v);
            changed |= DrawInputTextAndAssign("5464 / 死の超越 / 偽", this.configuration.GrandCrossDeathBeyondFakeText, v => this.configuration.GrandCrossDeathBeyondFakeText = v);

            changed |= DrawInputTextAndAssign("4887 / 生者の傷 / 真", this.configuration.GrandCrossLivingWoundTrueText, v => this.configuration.GrandCrossLivingWoundTrueText = v);
            changed |= DrawInputTextAndAssign("4887 / 生者の傷 / 偽", this.configuration.GrandCrossLivingWoundFakeText, v => this.configuration.GrandCrossLivingWoundFakeText = v);

            changed |= DrawInputTextAndAssign("4888 / 死者の傷 / 真", this.configuration.GrandCrossDeadWoundTrueText, v => this.configuration.GrandCrossDeadWoundTrueText = v);
            changed |= DrawInputTextAndAssign("4888 / 死者の傷 / 偽", this.configuration.GrandCrossDeadWoundFakeText, v => this.configuration.GrandCrossDeadWoundFakeText = v);

            changed |= DrawInputTextAndAssign("5543 / 呪詛の叫声 / 真", this.configuration.GrandCrossCurseShriekTrueText, v => this.configuration.GrandCrossCurseShriekTrueText = v);
            changed |= DrawInputTextAndAssign("5543 / 呪詛の叫声 / 偽", this.configuration.GrandCrossCurseShriekFakeText, v => this.configuration.GrandCrossCurseShriekFakeText = v);

            changed |= DrawInputTextAndAssign("5544 / フォークライトニング / 真", this.configuration.GrandCrossForkedLightningTrueText, v => this.configuration.GrandCrossForkedLightningTrueText = v);
            changed |= DrawInputTextAndAssign("5544 / フォークライトニング / 偽", this.configuration.GrandCrossForkedLightningFakeText, v => this.configuration.GrandCrossForkedLightningFakeText = v);

            changed |= DrawInputTextAndAssign("5545 / 水属性圧縮 / 真", this.configuration.GrandCrossWaterCompressionTrueText, v => this.configuration.GrandCrossWaterCompressionTrueText = v);
            changed |= DrawInputTextAndAssign("5545 / 水属性圧縮 / 偽", this.configuration.GrandCrossWaterCompressionFakeText, v => this.configuration.GrandCrossWaterCompressionFakeText = v);

            changed |= DrawInputTextAndAssign("5546 / 加速度爆弾 / 真", this.configuration.GrandCrossAccelerationBombTrueText, v => this.configuration.GrandCrossAccelerationBombTrueText = v);
            changed |= DrawInputTextAndAssign("5546 / 加速度爆弾 / 偽", this.configuration.GrandCrossAccelerationBombFakeText, v => this.configuration.GrandCrossAccelerationBombFakeText = v);

            ImGui.Separator();
            ImGui.TextUnformatted("カオス ほのお / つなみ 表示テキスト");

            changed |= DrawInputTextAndAssign("47902 / ほのお / Param1119 偽 / Status5547", this.configuration.GrandCrossFireFakeText, v => this.configuration.GrandCrossFireFakeText = v);
            changed |= DrawInputTextAndAssign("47902 / ほのお / Param1120 真 / Status5547", this.configuration.GrandCrossFireTrueText, v => this.configuration.GrandCrossFireTrueText = v);
            changed |= DrawInputTextAndAssign("47903 / つなみ / Param1119 偽 / Status5548", this.configuration.GrandCrossTsunamiFakeText, v => this.configuration.GrandCrossTsunamiFakeText = v);
            changed |= DrawInputTextAndAssign("47903 / つなみ / Param1120 真 / Status5548", this.configuration.GrandCrossTsunamiTrueText, v => this.configuration.GrandCrossTsunamiTrueText = v);
        }

        if (ImGui.CollapsingHeader("GrandCross テストモード", ImGuiTreeNodeFlags.DefaultOpen))
        {
            changed |= DrawInputTextAndAssign("テスト表示文字", this.configuration.GrandCrossTestText, v => this.configuration.GrandCrossTestText = v);

            if (ImGui.Button("GrandCross位置にテスト表示"))
                this.plugin.TestGrandCrossText(this.configuration.GrandCrossTestText);

            ImGui.TextUnformatted("※ 表示先・画面位置・文字色・縁取り・表示プリセット・フェードイン設定を反映します。");
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

        if (ImGui.CollapsingHeader("カオス保持状況リスト", ImGuiTreeNodeFlags.DefaultOpen))
        {
            foreach (var line in this.plugin.GetChaosStatusLines())
            {
                ImGui.TextUnformatted($"・{line}");
            }
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