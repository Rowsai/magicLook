using System;
using System.Linq;
using System.Reflection;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace MagicLook;

internal sealed class ConfigWindow : Window
{
    private readonly Plugin plugin;
    private readonly Configuration configuration;

    public ConfigWindow(Plugin plugin, Configuration configuration)
        : base($"magicLook version {GetAssemblyVersion()}###magicLookConfig")
    {
        this.plugin = plugin;
        this.configuration = configuration;

        this.Size = new Vector2(1280, 900);
        this.SizeCondition = ImGuiCond.FirstUseEver;
    }

    private static string GetAssemblyVersion()
    {
        return Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0.0";
    }

    public override void Draw()
    {
        var changed = false;

        this.PushTheme();

        if (ImGui.BeginTabBar("magicLookRootTabs"))
        {
            if (ImGui.BeginTabItem("General"))
            {
                this.DrawGeneralTab(ref changed);
                ImGui.EndTabItem();
            }

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

        this.PopTheme();

        if (changed)
            this.plugin.SaveConfig();
    }

    private void PushTheme()
    {
        ImGui.PushStyleColor(ImGuiCol.WindowBg, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ChildBg, new Vector4(0.0f, 0.0f, 0.0f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(1.0f, 1.0f, 1.0f, 1.0f));

        // ウィンドウタイトルバーもタブ帯と同じ水色に寄せる
        ImGui.PushStyleColor(ImGuiCol.TitleBg, new Vector4(0.00f, 0.55f, 0.78f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgActive, new Vector4(0.00f, 0.65f, 0.90f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TitleBgCollapsed, new Vector4(0.00f, 0.55f, 0.78f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.CheckMark, new Vector4(0.00f, 0.70f, 0.95f, 1.0f));

        ImGui.PushStyleColor(ImGuiCol.Header, new Vector4(0.50f, 0.50f, 0.50f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderHovered, new Vector4(0.62f, 0.62f, 0.62f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.HeaderActive, new Vector4(0.70f, 0.70f, 0.70f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Tab, new Vector4(0.00f, 0.55f, 0.78f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabHovered, new Vector4(0.10f, 0.70f, 0.95f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.TabActive, new Vector4(0.00f, 0.65f, 0.90f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBg, new Vector4(0.50f, 0.50f, 0.50f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgHovered, new Vector4(0.58f, 0.58f, 0.58f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.FrameBgActive, new Vector4(0.65f, 0.65f, 0.65f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.Button, new Vector4(0.50f, 0.50f, 0.50f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new Vector4(0.62f, 0.62f, 0.62f, 1.0f));
        ImGui.PushStyleColor(ImGuiCol.ButtonActive, new Vector4(0.70f, 0.70f, 0.70f, 1.0f));
    }

    private void PopTheme()
    {
        ImGui.PopStyleColor(19);
    }

    private static bool Section(string title, bool defaultOpen = true)
    {
        return ImGui.CollapsingHeader(title, defaultOpen ? ImGuiTreeNodeFlags.DefaultOpen : ImGuiTreeNodeFlags.None);
    }

    private static void LabelRight(string label)
    {
        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    private void DrawGeneralTab(ref bool changed)
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
        ImGui.SetNextItemWidth(240.0f);
        if (ImGui.InputInt("##TerritoryType", ref territory))
        {
            territory = Math.Clamp(territory, 0, ushort.MaxValue);
            this.configuration.TerritoryType = (ushort)territory;
            changed = true;
        }
        LabelRight("TerritoryType");

        var filterByCasterName = this.configuration.FilterByCasterName;
        if (ImGui.Checkbox("ケフカ系 詠唱者名で絞り込む", ref filterByCasterName))
        {
            this.configuration.FilterByCasterName = filterByCasterName;
            changed = true;
        }

        changed |= DrawInputTextLeft("##CasterNameKeyword", this.configuration.CasterNameKeyword, v => this.configuration.CasterNameKeyword = v, "詠唱者名キーワード", 240.0f);

        ImGui.TextUnformatted("※magicLook / magicCharge の検出に使用します。検出しない場合はOFFに設定してください。");
    }

    private void DrawMagicLookTab(ref bool changed)
    {
        var enabled = this.configuration.MagicLookEnabled;
        if (ImGui.Checkbox("magicLookを有効化", ref enabled))
        {
            this.configuration.MagicLookEnabled = enabled;
            changed = true;
        }

        if (Section("magicLook テキスト / 表示位置設定"))
        {
            this.DrawDisplayPositionSettings(
                ref changed,
                this.configuration.MagicLookDisplayMode,
                v => this.configuration.MagicLookDisplayMode = v,
                this.configuration.MagicLookScreenOffsetX,
                v => this.configuration.MagicLookScreenOffsetX = v,
                this.configuration.MagicLookScreenOffsetY,
                v => this.configuration.MagicLookScreenOffsetY = v,
                this.configuration.MagicLookFontSize,
                v => this.configuration.MagicLookFontSize = Math.Max(8.0f, v),
                this.configuration.MagicLookDisplaySeconds,
                v => this.configuration.MagicLookDisplaySeconds = Math.Max(0.1f, v),
                this.configuration.MagicLookFadeInSeconds,
                v => this.configuration.MagicLookFadeInSeconds = Math.Max(0.0f, v),
                this.configuration.MagicLookFixedScreenPositionX,
                v => this.configuration.MagicLookFixedScreenPositionX = v,
                this.configuration.MagicLookFixedScreenPositionY,
                v => this.configuration.MagicLookFixedScreenPositionY = v,
                this.configuration.MagicLookUseOutline,
                v => this.configuration.MagicLookUseOutline = v,
                this.configuration.MagicLookOutlineThickness,
                v => this.configuration.MagicLookOutlineThickness = Math.Max(0.0f, v),
                new Vector4(this.configuration.MagicLookOutlineColorR, this.configuration.MagicLookOutlineColorG, this.configuration.MagicLookOutlineColorB, this.configuration.MagicLookOutlineColorA),
                v =>
                {
                    this.configuration.MagicLookOutlineColorR = v.X;
                    this.configuration.MagicLookOutlineColorG = v.Y;
                    this.configuration.MagicLookOutlineColorB = v.Z;
                    this.configuration.MagicLookOutlineColorA = v.W;
                },
                "magicLook");

            var drawBackground = this.configuration.MagicLookDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicLookBg", ref drawBackground))
            {
                this.configuration.MagicLookDrawBackground = drawBackground;
                changed = true;
            }

            ImGui.Spacing();
            changed |= DrawInputTextLeft("##MagicLookTestText", this.configuration.MagicLookTestText, v => this.configuration.MagicLookTestText = v, "テキスト表示位置テスト文字列", 360.0f);
            if (ImGui.Button("表示位置テスト##MagicLook"))
                this.plugin.TestLockText(this.configuration.MagicLookTestText);
        }

        if (Section("magicLook 詠唱組み合わせ設定"))
        {
            this.DrawPhaseSettings("Phase1", ref changed);
            this.DrawPhaseSettings("Phase4", ref changed);
        }

        if (Section("magicLook 直近検出情報"))
        {
            ImGui.TextUnformatted($"詠唱者：{this.plugin.LastCasterName}");
            ImGui.TextUnformatted($"ひろげるブリザガ / ActionId：{this.plugin.LastActionId}");
            ImGui.TextUnformatted($"もりもりサンダガ / ActionId：{this.plugin.LastActionId}");
            ImGui.TextUnformatted($"めらめらファイガ / vfx：");
        }

        if (Section("magicLook ログ"))
        {
            ImGui.TextUnformatted("※ 下のログ欄はマウスで範囲選択してコピーできます。");

            var logText = this.plugin.GetMagicLookLogText();
            ImGui.InputTextMultiline(
                "##MagicLookCopyableLog",
                ref logText,
                20000,
                new Vector2(-1.0f, 180.0f),
                ImGuiInputTextFlags.ReadOnly);
        }
    }

    private void DrawPhaseSettings(string phase, ref bool changed)
    {
        ImGui.TextUnformatted(phase);

        var phaseSettings = this.configuration.MagicLookPhaseTextSettings
            .Where(setting => string.Equals(setting.Phase, phase, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (phaseSettings.Count == 0)
        {
            ImGui.TextUnformatted("設定がありません。");
            return;
        }

        if (string.Equals(phase, "Phase1", StringComparison.OrdinalIgnoreCase))
        {
            this.DrawPhase1ConfiguredLayout(ref changed);
            return;
        }

        this.DrawPhaseRowsByLabels(
            phase,
            new[]
            {
                "真ブリザガ / 真サンダガ",
                "真ブリザガ / 偽サンダガ",
                "偽ブリザガ / 真サンダガ",
                "偽ブリザガ / 偽サンダガ",
            },
            ref changed,
            "Phase4",
            500.0f);
    }

    private void DrawPhase1ConfiguredLayout(ref bool changed)
    {
        ImGui.TextUnformatted("ブリザガ / ファイガ");
        this.DrawTwoColumnPhaseRows(
            "Phase1",
            new[]
            {
                "真ブリザガ / 偽ファイガ（パターン1）",
                "真ブリザガ / 偽ファイガ（パターン2）",
                "真ブリザガ / 真ファイガ（パターン3）",
                "真ブリザガ / 真ファイガ（パターン4）",
            },
            new[]
            {
                "偽ブリザガ / 偽ファイガ（パターン1）",
                "偽ブリザガ / 偽ファイガ（パターン2）",
                "偽ブリザガ / 真ファイガ（パターン3）",
                "偽ブリザガ / 真ファイガ（パターン4）",
            },
            ref changed,
            "Phase1_BlizzardFirega");

        ImGui.Spacing();
        ImGui.TextUnformatted("ブリザガ / サンダガ");
        this.DrawTwoColumnPhaseRows(
            "Phase1",
            new[]
            {
                "真ブリザガ / 真サンダガ",
                "真ブリザガ / 偽サンダガ",
            },
            new[]
            {
                "偽ブリザガ / 真サンダガ",
                "偽ブリザガ / 偽サンダガ",
            },
            ref changed,
            "Phase1_BlizzardThunder");

        ImGui.Spacing();
        ImGui.TextUnformatted("サンダガ / ファイガ");
        this.DrawTwoColumnPhaseRows(
            "Phase1",
            new[]
            {
                "真サンダガ / 偽ファイガ（パターン1）",
                "真サンダガ / 偽ファイガ（パターン2）",
                "真サンダガ / 真ファイガ（パターン3）",
                "真サンダガ / 真ファイガ（パターン4）",
            },
            new[]
            {
                "偽サンダガ / 偽ファイガ（パターン1）",
                "偽サンダガ / 偽ファイガ（パターン2）",
                "偽サンダガ / 真ファイガ（パターン3）",
                "偽サンダガ / 真ファイガ（パターン4）",
            },
            ref changed,
            "Phase1_ThunderFirega");
    }

    private void DrawTwoColumnPhaseRows(string phase, string[] leftLabels, string[] rightLabels, ref bool changed, string tableId)
    {
        if (!ImGui.BeginTable(tableId, 2, ImGuiTableFlags.SizingStretchSame))
            return;

        ImGui.TableNextColumn();
        this.DrawPhaseRowsByLabels(phase, leftLabels, ref changed, tableId + "_Left", 500.0f);

        ImGui.TableNextColumn();
        this.DrawPhaseRowsByLabels(phase, rightLabels, ref changed, tableId + "_Right", 500.0f);

        ImGui.EndTable();
    }

    private void DrawPhaseRowsByLabels(string phase, string[] labels, ref bool changed, string idPrefix, float width)
    {
        for (var i = 0; i < labels.Length; i++)
        {
            var label = labels[i];
            var setting = this.FindPhaseSetting(phase, label);
            if (setting == null)
            {
                this.DrawMissingPhaseSettingRow(phase, label, $"{idPrefix}_Missing_{i}", width);
                continue;
            }

            this.DrawPhaseSettingRow(setting, $"{idPrefix}_{i}", ref changed, width);
        }
    }

    private MagicLookPhaseTextSetting? FindPhaseSetting(string phase, string label)
    {
        return this.configuration.MagicLookPhaseTextSettings.FirstOrDefault(setting =>
            setting.Enabled &&
            string.Equals(setting.Phase, phase, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(setting.Label, label, StringComparison.Ordinal));
    }

    private void DrawMissingPhaseSettingRow(string phase, string label, string id, float width)
    {
        ImGui.PushID(id);
        ImGui.BeginDisabled();
        var value = string.Empty;
        ImGui.SetNextItemWidth(width);
        ImGui.InputText("##PhaseTextMissing", ref value, 256);
        LabelRight($"{label} / 設定なし");
        ImGui.EndDisabled();
        ImGui.PopID();
    }

    private void DrawPhaseSettingRow(MagicLookPhaseTextSetting setting, string id, ref bool changed, float width)
    {
        ImGui.PushID(id);
        ImGui.SetNextItemWidth(width);
        var text = setting.Text ?? string.Empty;
        if (ImGui.InputText("##PhaseText", ref text, 256))
        {
            setting.Text = text;
            changed = true;
        }

        LabelRight(setting.Label);

        if (setting.RequiredVfxPaths.Count > 0 && ImGui.IsItemHovered())
        {
            ImGui.BeginTooltip();
            ImGui.TextUnformatted("比較VFX:");
            foreach (var vfx in setting.RequiredVfxPaths)
                ImGui.TextUnformatted(vfx);
            ImGui.EndTooltip();
        }

        ImGui.PopID();
    }

    private void DrawMagicChargeTab(ref bool changed)
    {
        var enabled = this.configuration.MagicChargeEnabled;
        if (ImGui.Checkbox("magicChargeを有効化", ref enabled))
        {
            this.configuration.MagicChargeEnabled = enabled;
            changed = true;
        }

        if (Section("magicLook テキスト / 表示位置設定"))
        {
            this.DrawDisplayPositionSettings(
                ref changed,
                this.configuration.MagicChargeDisplayMode,
                v => this.configuration.MagicChargeDisplayMode = v,
                this.configuration.MagicChargeScreenOffsetX,
                v => this.configuration.MagicChargeScreenOffsetX = v,
                this.configuration.MagicChargeScreenOffsetY,
                v => this.configuration.MagicChargeScreenOffsetY = v,
                this.configuration.MagicChargeFontSize,
                v => this.configuration.MagicChargeFontSize = Math.Max(8.0f, v),
                this.configuration.MagicChargeDisplaySeconds,
                v => this.configuration.MagicChargeDisplaySeconds = Math.Max(0.1f, v),
                this.configuration.MagicChargeFadeInSeconds,
                v => this.configuration.MagicChargeFadeInSeconds = Math.Max(0.0f, v),
                this.configuration.MagicChargeScreenPositionX,
                v => this.configuration.MagicChargeScreenPositionX = v,
                this.configuration.MagicChargeScreenPositionY,
                v => this.configuration.MagicChargeScreenPositionY = v,
                this.configuration.MagicChargeUseOutline,
                v => this.configuration.MagicChargeUseOutline = v,
                this.configuration.MagicChargeOutlineThickness,
                v => this.configuration.MagicChargeOutlineThickness = Math.Max(0.0f, v),
                new Vector4(this.configuration.MagicChargeOutlineColorR, this.configuration.MagicChargeOutlineColorG, this.configuration.MagicChargeOutlineColorB, this.configuration.MagicChargeOutlineColorA),
                v =>
                {
                    this.configuration.MagicChargeOutlineColorR = v.X;
                    this.configuration.MagicChargeOutlineColorG = v.Y;
                    this.configuration.MagicChargeOutlineColorB = v.Z;
                    this.configuration.MagicChargeOutlineColorA = v.W;
                },
                "magicCharge");

            var color = new Vector4(this.configuration.MagicChargeTextColorR, this.configuration.MagicChargeTextColorG, this.configuration.MagicChargeTextColorB, this.configuration.MagicChargeTextColorA);
            if (DrawColorEditLeft("##MagicChargeTextColor", ref color, "文字色"))
            {
                this.configuration.MagicChargeTextColorR = color.X;
                this.configuration.MagicChargeTextColorG = color.Y;
                this.configuration.MagicChargeTextColorB = color.Z;
                this.configuration.MagicChargeTextColorA = color.W;
                changed = true;
            }

            var drawBackground = this.configuration.MagicChargeDrawBackground;
            if (ImGui.Checkbox("背景を表示する##MagicChargeBg", ref drawBackground))
            {
                this.configuration.MagicChargeDrawBackground = drawBackground;
                changed = true;
            }

            ImGui.Spacing();
            changed |= DrawInputTextLeft("##MagicChargeTestText", this.configuration.MagicChargeTestText, v => this.configuration.MagicChargeTestText = v, "テキスト表示位置テスト文字列", 360.0f);
            if (ImGui.Button("表示位置テスト##MagicCharge"))
                this.plugin.TestChargeText(this.configuration.MagicChargeTestText);
        }

        if (Section("magicCharge 詠唱組み合わせ設定"))
        {
            changed |= DrawInputTextLeft("##Charge1", this.configuration.MagicChargeFanNoStepLineNoStepText, v => this.configuration.MagicChargeFanNoStepLineNoStepText = v, "真ブリザガ / 真サンダガ", 360.0f);
            changed |= DrawInputTextLeft("##Charge2", this.configuration.MagicChargeFanNoStepLineStepText, v => this.configuration.MagicChargeFanNoStepLineStepText = v, "偽ブリザガ / 真サンダガ", 360.0f);
            changed |= DrawInputTextLeft("##Charge3", this.configuration.MagicChargeFanStepLineNoStepText, v => this.configuration.MagicChargeFanStepLineNoStepText = v, "真ブリザガ / 偽サンダガ", 360.0f);
            changed |= DrawInputTextLeft("##Charge4", this.configuration.MagicChargeFanStepLineStepText, v => this.configuration.MagicChargeFanStepLineStepText = v, "偽ブリザガ / 偽サンダガ", 360.0f);
        }

        if (Section("magicLook 直近検出情報"))
        {
            foreach (var line in this.plugin.GetMagicChargeStatusLines())
                ImGui.TextUnformatted(line);
        }
    }

    private void DrawGrandCrossTab(ref bool changed)
    {
        var enabled = this.configuration.GrandCrossEnabled;
        if (ImGui.Checkbox("GrandCrossを有効化", ref enabled))
        {
            this.configuration.GrandCrossEnabled = enabled;
            changed = true;
        }

        if (Section("GrandCross テキスト / 表示位置設定"))
        {
            this.DrawDisplayPositionSettings(
                ref changed,
                this.configuration.GrandCrossDisplayMode,
                v => this.configuration.GrandCrossDisplayMode = v,
                this.configuration.GrandCrossScreenOffsetX,
                v => this.configuration.GrandCrossScreenOffsetX = v,
                this.configuration.GrandCrossScreenOffsetY,
                v => this.configuration.GrandCrossScreenOffsetY = v,
                this.configuration.GrandCrossFontSize,
                v => this.configuration.GrandCrossFontSize = Math.Max(8.0f, v),
                this.configuration.GrandCrossDisplaySeconds,
                v => this.configuration.GrandCrossDisplaySeconds = Math.Max(0.1f, v),
                this.configuration.GrandCrossFadeInSeconds,
                v => this.configuration.GrandCrossFadeInSeconds = Math.Max(0.0f, v),
                this.configuration.GrandCrossScreenPositionX,
                v => this.configuration.GrandCrossScreenPositionX = v,
                this.configuration.GrandCrossScreenPositionY,
                v => this.configuration.GrandCrossScreenPositionY = v,
                this.configuration.GrandCrossUseOutline,
                v => this.configuration.GrandCrossUseOutline = v,
                this.configuration.GrandCrossOutlineThickness,
                v => this.configuration.GrandCrossOutlineThickness = Math.Max(0.0f, v),
                new Vector4(this.configuration.GrandCrossOutlineColorR, this.configuration.GrandCrossOutlineColorG, this.configuration.GrandCrossOutlineColorB, this.configuration.GrandCrossOutlineColorA),
                v =>
                {
                    this.configuration.GrandCrossOutlineColorR = v.X;
                    this.configuration.GrandCrossOutlineColorG = v.Y;
                    this.configuration.GrandCrossOutlineColorB = v.Z;
                    this.configuration.GrandCrossOutlineColorA = v.W;
                },
                "GrandCross");

            var color = new Vector4(this.configuration.GrandCrossTextColorR, this.configuration.GrandCrossTextColorG, this.configuration.GrandCrossTextColorB, this.configuration.GrandCrossTextColorA);
            if (DrawColorEditLeft("##GrandCrossTextColor", ref color, "文字色"))
            {
                this.configuration.GrandCrossTextColorR = color.X;
                this.configuration.GrandCrossTextColorG = color.Y;
                this.configuration.GrandCrossTextColorB = color.Z;
                this.configuration.GrandCrossTextColorA = color.W;
                changed = true;
            }

            var showSeconds = this.configuration.GrandCrossShowRemainingSeconds;
            if (ImGui.Checkbox("デバフ秒数を表示する##GrandCrossSeconds", ref showSeconds))
            {
                this.configuration.GrandCrossShowRemainingSeconds = showSeconds;
                changed = true;
            }

            var drawBackground = this.configuration.GrandCrossDrawBackground;
            if (ImGui.Checkbox("背景を表示する##GrandCrossBg", ref drawBackground))
            {
                this.configuration.GrandCrossDrawBackground = drawBackground;
                changed = true;
            }
            ImGui.TextUnformatted("※画面固定位置の場合、実表示では背景OFF、フェードイン0.15秒固定です。");

            ImGui.Spacing();
            changed |= DrawInputTextLeft("##GrandCrossTestText", this.configuration.GrandCrossTestText, v => this.configuration.GrandCrossTestText = v, "テキスト表示位置テスト文字列", 360.0f);
            if (ImGui.Button("表示位置テスト##GrandCross"))
                this.plugin.TestGrandCrossText(this.configuration.GrandCrossTestText);
        }

        if (Section("GrandCross 対象設定"))
        {
            var filter = this.configuration.GrandCrossFilterByEnemyName;
            if (ImGui.Checkbox("ネオエクスデス名で絞り込む", ref filter))
            {
                this.configuration.GrandCrossFilterByEnemyName = filter;
                changed = true;
            }
            changed |= DrawInputTextLeft("##NeoExdeathKeyword", this.configuration.GrandCrossEnemyNameKeyword, v => this.configuration.GrandCrossEnemyNameKeyword = v, "ネオエクスデスキーワード", 280.0f);
            ImGui.TextUnformatted("対象ActionId：47892 / グランドクロス");
            ImGui.TextUnformatted("内部ステータス：StatusId：2056 / Param：1121＝偽、1122＝真");

            if (Section("デバフ取得対象", true))
            {
                ImGui.TextUnformatted("自分のみ");
            }

            var chaosFilter = this.configuration.GrandCrossChaosFilterByEnemyName;
            if (ImGui.Checkbox("カオス名で絞り込む", ref chaosFilter))
            {
                this.configuration.GrandCrossChaosFilterByEnemyName = chaosFilter;
                changed = true;
            }
            changed |= DrawInputTextLeft("##ChaosKeyword", this.configuration.GrandCrossChaosEnemyNameKeyword, v => this.configuration.GrandCrossChaosEnemyNameKeyword = v, "カオスキーワード", 280.0f);
            ImGui.TextUnformatted("対象ActionId：47902 / ほのお");
            ImGui.TextUnformatted("対象ActionId：47903 / つなみ");
            ImGui.TextUnformatted("内部ステータス：StatusId：2056 / Param：1119＝偽、1120＝真");
            ImGui.TextUnformatted("デバフ情報：");
            ImGui.TextUnformatted("ほのお＝StatusId：5547 / 混沌の炎");
            ImGui.TextUnformatted("つなみ＝StatusId：5548 / 混沌の水");
        }

        if (Section("GrandCross デバフ表示テキスト / 真偽別"))
        {
            this.DrawGrandCrossTextTable(ref changed);
            if (ImGui.Button("表示テスト##GrandCrossTextTable"))
                this.plugin.TestGrandCrossConfiguredTrueText();
        }

        if (Section("GrandCross 保持状況リスト"))
        {
            foreach (var line in this.plugin.GetGrandCrossStatusLines())
                ImGui.TextUnformatted(line);
        }
    }

    private void DrawDisplayPositionSettings(
        ref bool changed,
        int displayMode,
        Action<int> assignDisplayMode,
        float overheadX,
        Action<float> assignOverheadX,
        float overheadY,
        Action<float> assignOverheadY,
        float fontSize,
        Action<float> assignFontSize,
        float displaySeconds,
        Action<float> assignDisplaySeconds,
        float fadeSeconds,
        Action<float> assignFadeSeconds,
        float fixedX,
        Action<float> assignFixedX,
        float fixedY,
        Action<float> assignFixedY,
        bool useOutline,
        Action<bool> assignUseOutline,
        float outlineThickness,
        Action<float> assignOutlineThickness,
        Vector4 outlineColor,
        Action<Vector4> assignOutlineColor,
        string id)
    {
        var mode = displayMode;
        if (ImGui.RadioButton($"キャラクターの頭上に設定##{id}Overhead", mode == 0))
        {
            assignDisplayMode(0);
            changed = true;
        }

        if (mode == 0)
        {
            changed |= DrawFloatLeft($"##{id}OverheadX", overheadX, assignOverheadX, "横位置 X", 340.0f);
            changed |= DrawFloatLeft($"##{id}OverheadY", overheadY, assignOverheadY, "縦位置 Y", 340.0f);
            changed |= DrawFloatLeft($"##{id}FontSizeA", fontSize, assignFontSize, "文字サイズ", 340.0f);
            changed |= DrawFloatLeft($"##{id}DisplaySecA", displaySeconds, assignDisplaySeconds, "表示秒数", 340.0f);
            changed |= DrawFloatLeft($"##{id}FadeA", fadeSeconds, assignFadeSeconds, "フェードイン秒数", 340.0f);
            changed |= DrawBoolAndAssign($"縁取りを有効化##{id}OutlineA", useOutline, assignUseOutline);
            changed |= DrawFloatLeft($"##{id}OutlineThicknessA", outlineThickness, assignOutlineThickness, "縁取りの太さ", 340.0f);
            var color = outlineColor;
            if (DrawColorEditLeft($"##{id}OutlineColorA", ref color, "縁取りの色"))
            {
                assignOutlineColor(color);
                changed = true;
            }
        }

        var fixedMode = displayMode;
        if (ImGui.RadioButton($"画面固定位置に設定##{id}Fixed", fixedMode == 1))
        {
            assignDisplayMode(1);
            changed = true;
        }

        if (displayMode == 1)
        {
            changed |= DrawFloatLeft($"##{id}FixedX", fixedX, assignFixedX, "横位置 X", 340.0f);
            changed |= DrawFloatLeft($"##{id}FixedY", fixedY, assignFixedY, "縦位置 Y", 340.0f);
            changed |= DrawFloatLeft($"##{id}FontSizeB", fontSize, assignFontSize, "文字サイズ", 340.0f);
            changed |= DrawFloatLeft($"##{id}DisplaySecB", displaySeconds, assignDisplaySeconds, "表示秒数", 340.0f);
            changed |= DrawFloatLeft($"##{id}FadeB", fadeSeconds, assignFadeSeconds, "フェードイン秒数", 340.0f);
            changed |= DrawBoolAndAssign($"縁取りを有効化##{id}OutlineB", useOutline, assignUseOutline);
            changed |= DrawFloatLeft($"##{id}OutlineThicknessB", outlineThickness, assignOutlineThickness, "縁取りの太さ", 340.0f);
            var color = outlineColor;
            if (DrawColorEditLeft($"##{id}OutlineColorB", ref color, "縁取りの色"))
            {
                assignOutlineColor(color);
                changed = true;
            }
        }
    }

    private void DrawGrandCrossTextTable(ref bool changed)
    {
        var tableWidth = 460.0f;
        ImGui.BeginGroup();
        ImGui.TextUnformatted("真");
        changed |= DrawInputTextLeft("##GcAllaganTrue", this.configuration.GrandCrossAllaganFieldTrueText, v => this.configuration.GrandCrossAllaganFieldTrueText = v, "アラガンフィールド", 260.0f);
        changed |= DrawInputTextLeft("##GcDeathTrue", this.configuration.GrandCrossDeathBeyondTrueText, v => this.configuration.GrandCrossDeathBeyondTrueText = v, "死の超越", 260.0f);
        changed |= DrawInputTextLeft("##GcLivingTrue", this.configuration.GrandCrossLivingWoundTrueText, v => this.configuration.GrandCrossLivingWoundTrueText = v, "生者の傷", 260.0f);
        changed |= DrawInputTextLeft("##GcDeadTrue", this.configuration.GrandCrossDeadWoundTrueText, v => this.configuration.GrandCrossDeadWoundTrueText = v, "死者の傷", 260.0f);
        changed |= DrawInputTextLeft("##GcCurseTrue", this.configuration.GrandCrossCurseShriekTrueText, v => this.configuration.GrandCrossCurseShriekTrueText = v, "呪詛の叫声", 260.0f);
        changed |= DrawInputTextLeft("##GcForkTrue", this.configuration.GrandCrossForkedLightningTrueText, v => this.configuration.GrandCrossForkedLightningTrueText = v, "フォークライトニング", 260.0f);
        changed |= DrawInputTextLeft("##GcWaterTrue", this.configuration.GrandCrossWaterCompressionTrueText, v => this.configuration.GrandCrossWaterCompressionTrueText = v, "水属性圧縮", 260.0f);
        changed |= DrawInputTextLeft("##GcBombTrue", this.configuration.GrandCrossAccelerationBombTrueText, v => this.configuration.GrandCrossAccelerationBombTrueText = v, "加速度爆弾", 260.0f);
        changed |= DrawInputTextLeft("##GcFireTrue", this.configuration.GrandCrossFireTrueText, v => this.configuration.GrandCrossFireTrueText = v, "ほのお", 260.0f);
        changed |= DrawInputTextLeft("##GcTsunamiTrue", this.configuration.GrandCrossTsunamiTrueText, v => this.configuration.GrandCrossTsunamiTrueText = v, "つなみ", 260.0f);
        ImGui.EndGroup();

        ImGui.SameLine(tableWidth);

        ImGui.BeginGroup();
        ImGui.TextUnformatted("偽");
        changed |= DrawInputTextLeft("##GcAllaganFake", this.configuration.GrandCrossAllaganFieldFakeText, v => this.configuration.GrandCrossAllaganFieldFakeText = v, "アラガンフィールド", 260.0f);
        changed |= DrawInputTextLeft("##GcDeathFake", this.configuration.GrandCrossDeathBeyondFakeText, v => this.configuration.GrandCrossDeathBeyondFakeText = v, "死の超越", 260.0f);
        changed |= DrawInputTextLeft("##GcLivingFake", this.configuration.GrandCrossLivingWoundFakeText, v => this.configuration.GrandCrossLivingWoundFakeText = v, "生者の傷", 260.0f);
        changed |= DrawInputTextLeft("##GcDeadFake", this.configuration.GrandCrossDeadWoundFakeText, v => this.configuration.GrandCrossDeadWoundFakeText = v, "死者の傷", 260.0f);
        changed |= DrawInputTextLeft("##GcCurseFake", this.configuration.GrandCrossCurseShriekFakeText, v => this.configuration.GrandCrossCurseShriekFakeText = v, "呪詛の叫声", 260.0f);
        changed |= DrawInputTextLeft("##GcForkFake", this.configuration.GrandCrossForkedLightningFakeText, v => this.configuration.GrandCrossForkedLightningFakeText = v, "フォークライトニング", 260.0f);
        changed |= DrawInputTextLeft("##GcWaterFake", this.configuration.GrandCrossWaterCompressionFakeText, v => this.configuration.GrandCrossWaterCompressionFakeText = v, "水属性圧縮", 260.0f);
        changed |= DrawInputTextLeft("##GcBombFake", this.configuration.GrandCrossAccelerationBombFakeText, v => this.configuration.GrandCrossAccelerationBombFakeText = v, "加速度爆弾", 260.0f);
        changed |= DrawInputTextLeft("##GcFireFake", this.configuration.GrandCrossFireFakeText, v => this.configuration.GrandCrossFireFakeText = v, "ほのお", 260.0f);
        changed |= DrawInputTextLeft("##GcTsunamiFake", this.configuration.GrandCrossTsunamiFakeText, v => this.configuration.GrandCrossTsunamiFakeText = v, "つなみ", 260.0f);
        ImGui.EndGroup();
    }

    private static bool DrawBoolAndAssign(string label, bool currentValue, Action<bool> assign)
    {
        var value = currentValue;
        if (!ImGui.Checkbox(label, ref value))
            return false;

        assign(value);
        return true;
    }

    private static bool DrawInputTextLeft(string id, string currentValue, Action<string> assign, string label, float width)
    {
        var value = currentValue;
        ImGui.SetNextItemWidth(width);
        if (!ImGui.InputText(id, ref value, 256))
        {
            LabelRight(label);
            return false;
        }

        LabelRight(label);
        assign(value);
        return true;
    }

    private static bool DrawFloatLeft(string id, float currentValue, Action<float> assign, string label, float width)
    {
        var value = currentValue;
        var before = value;

        ImGui.SetNextItemWidth(width);
        ImGui.InputFloat(id, ref value, 1.0f, 10.0f, "%.2f");
        LabelRight(label);

        if (Math.Abs(before - value) <= 0.0001f)
            return false;

        assign(value);
        return true;
    }

    private static bool DrawColorEditLeft(string id, ref Vector4 color, string label)
    {
        ImGui.SetNextItemWidth(360.0f);
        var changed = ImGui.ColorEdit4(id, ref color);
        LabelRight(label);
        return changed;
    }
}
