using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;

namespace MagicLook;

internal static class ConfigWindowPhaseTextRenderer
{
    private const float TextBoxWidth = 500.0f;
    private const float ColumnWidth = 820.0f;

    internal static void Draw(Configuration configuration, Action saveConfig)
    {
        if (!ImGui.CollapsingHeader("magicLook 詠唱組み合わせ設定", ImGuiTreeNodeFlags.DefaultOpen))
            return;

        ImGui.TextUnformatted("Phase1");

        DrawTwoColumnRows(
            configuration,
            saveConfig,
            new[]
            {
                "真ブリザガ / 真ファイガ（パターン3）",
                "真ブリザガ / 偽ファイガ（パターン1）",
                "偽ブリザガ / 真ファイガ（パターン3）",
                "偽ブリザガ / 偽ファイガ（パターン1）",
            },
            new[]
            {
                "真ブリザガ / 真サンダガ",
                "真ブリザガ / 偽サンダガ",
                "偽ブリザガ / 真サンダガ",
                "偽ブリザガ / 偽サンダガ",
            });

        ImGui.Spacing();

        DrawSingleColumnRows(
            configuration,
            saveConfig,
            "Phase1",
            new[]
            {
                "真サンダガ / 真ファイガ（パターン3）",
                "偽サンダガ / 真ファイガ（パターン3）",
                "真サンダガ / 偽ファイガ（パターン1）",
                "偽サンダガ / 偽ファイガ（パターン1）",
            });

        ImGui.TextUnformatted("Phase4");

        DrawSingleColumnRows(
            configuration,
            saveConfig,
            "Phase4",
            new[]
            {
                "真ブリザガ / 真サンダガ",
                "真ブリザガ / 偽サンダガ",
                "偽ブリザガ / 真サンダガ",
                "偽ブリザガ / 偽サンダガ",
            });
    }

    private static void DrawTwoColumnRows(
        Configuration configuration,
        Action saveConfig,
        IReadOnlyList<string> leftLabels,
        IReadOnlyList<string> rightLabels)
    {
        var rows = Math.Max(leftLabels.Count, rightLabels.Count);

        for (var i = 0; i < rows; i++)
        {
            if (i < leftLabels.Count)
                DrawPhaseTextRow(configuration, saveConfig, "Phase1", leftLabels[i]);

            ImGui.SameLine(ColumnWidth);

            if (i < rightLabels.Count)
                DrawPhaseTextRow(configuration, saveConfig, "Phase1", rightLabels[i]);
        }
    }

    private static void DrawSingleColumnRows(
        Configuration configuration,
        Action saveConfig,
        string phase,
        IReadOnlyList<string> labels)
    {
        foreach (var label in labels)
            DrawPhaseTextRow(configuration, saveConfig, phase, label);
    }

    private static void DrawPhaseTextRow(
        Configuration configuration,
        Action saveConfig,
        string phase,
        string label)
    {
        var setting = FindSetting(configuration, phase, label);
        if (setting == null)
        {
            ImGui.BeginDisabled();
            var missing = string.Empty;
            ImGui.SetNextItemWidth(TextBoxWidth);
            ImGui.InputText($"##missing_{phase}_{label}", ref missing, 256);
            ImGui.SameLine();
            ImGui.TextUnformatted($"{label} / 未設定");
            ImGui.EndDisabled();
            return;
        }

        var text = setting.Text ?? string.Empty;
        ImGui.SetNextItemWidth(TextBoxWidth);
        if (ImGui.InputText($"##phase_text_{phase}_{label}", ref text, 256))
        {
            setting.Text = text;
            saveConfig();
        }

        ImGui.SameLine();
        ImGui.TextUnformatted(label);
    }

    private static MagicLookPhaseTextSetting? FindSetting(Configuration configuration, string phase, string label)
    {
        return configuration.MagicLookPhaseTextSettings.FirstOrDefault(setting =>
            setting.Enabled &&
            string.Equals(setting.Phase, phase, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(setting.Label, label, StringComparison.Ordinal));
    }
}
