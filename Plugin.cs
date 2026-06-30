using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;

namespace MagicLock;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/magiclock";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("magicLock");
    private readonly ConfigWindow configWindow;

    private readonly Configuration configuration;

    private string activeLockText = string.Empty;
    private DateTime activeLockUntil = DateTime.MinValue;

    private string activeChargeText = string.Empty;
    private DateTime activeChargeUntil = DateTime.MinValue;

    private string activeGrandCrossText = string.Empty;
    private DateTime activeGrandCrossUntil = DateTime.MinValue;

    private HashSet<string> previousCastKeys = new();

    private bool waitingForMagicChargeResult;
    private bool magicChargeCycleCompleted;
    private readonly List<MagicChargeKind> magicChargeSlots = new();

    private bool grandCrossCasting;
    private bool grandCrossCycleCompleted;
    private bool pendingGrandCrossStatusCapture;
    private DateTime pendingGrandCrossStatusCaptureUntil = DateTime.MinValue;
    private bool pendingGrandCrossIsFake;
    private uint currentGrandCrossInternalParam;
    private string currentGrandCrossCasterName = string.Empty;

    private readonly List<string> grandCrossHeldTexts = new();

    internal string LastCasterName { get; private set; } = string.Empty;
    internal uint LastActionId { get; private set; }
    internal string LastMatchedLabel { get; private set; } = string.Empty;

    internal string LastMagicChargeEvent { get; private set; } = string.Empty;
    internal string LastMagicChargeResult { get; private set; } = string.Empty;

    internal string LastGrandCrossEvent { get; private set; } = string.Empty;
    internal string LastGrandCrossResult { get; private set; } = string.Empty;

    public Plugin(
        IDalamudPluginInterface pluginInterface,
        ICommandManager commandManager,
        IClientState clientState,
        IFramework framework,
        IGameGui gameGui,
        IObjectTable objectTable,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.clientState = clientState;
        this.framework = framework;
        this.gameGui = gameGui;
        this.objectTable = objectTable;
        this.log = log;

        this.configuration =
            this.pluginInterface.GetPluginConfig() as Configuration
            ?? new Configuration();

        this.configuration.EnsureDefaults();
        this.configuration.Save(this.pluginInterface);

        this.configWindow = new ConfigWindow(this, this.configuration);
        this.windowSystem.AddWindow(this.configWindow);

        this.commandManager.AddHandler(CommandName, new CommandInfo(this.OnCommand)
        {
            HelpMessage = "magicLock の設定画面を開きます。"
        });

        this.framework.Update += this.OnFrameworkUpdate;
        this.pluginInterface.UiBuilder.Draw += this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi += this.OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi += this.OpenConfigUi;
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        this.pluginInterface.UiBuilder.Draw -= this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenConfigUi;

        this.commandManager.RemoveHandler(CommandName);
        this.windowSystem.RemoveAllWindows();
    }

    internal void SaveConfig()
    {
        this.configuration.Save(this.pluginInterface);
    }

    internal void TestLockText(string text)
    {
        this.activeLockText = text;
        this.activeLockUntil = DateTime.Now.AddSeconds(this.configuration.MagicLockDisplaySeconds);

        this.LastCasterName = "テスト";
        this.LastActionId = 0;
        this.LastMatchedLabel = "magicLock テスト表示";
    }

    internal void TestChargeText(string text)
    {
        this.activeChargeText = text;
        this.activeChargeUntil = DateTime.Now.AddSeconds(this.configuration.MagicChargeDisplaySeconds);

        this.LastMagicChargeEvent = "テスト";
        this.LastMagicChargeResult = text;
    }

    internal void TestGrandCrossText(string text)
    {
        this.activeGrandCrossText = text;
        this.activeGrandCrossUntil = DateTime.Now.AddSeconds(this.configuration.GrandCrossDisplaySeconds);

        this.LastGrandCrossEvent = "テスト";
        this.LastGrandCrossResult = text;
    }

    internal void ResetMagicChargeState()
    {
        this.waitingForMagicChargeResult = false;
        this.magicChargeCycleCompleted = false;
        this.magicChargeSlots.Clear();

        this.LastMagicChargeEvent = "リセット";
        this.LastMagicChargeResult = string.Empty;
    }

    internal void ResetGrandCrossState()
    {
        this.grandCrossCasting = false;
        this.grandCrossCycleCompleted = false;
        this.pendingGrandCrossStatusCapture = false;
        this.pendingGrandCrossStatusCaptureUntil = DateTime.MinValue;
        this.pendingGrandCrossIsFake = false;
        this.currentGrandCrossInternalParam = 0;
        this.currentGrandCrossCasterName = string.Empty;
        this.grandCrossHeldTexts.Clear();

        this.LastGrandCrossEvent = "リセット";
        this.LastGrandCrossResult = string.Empty;
    }

    internal IReadOnlyList<string> GetMagicChargeStatusLines()
    {
        var result = new List<string>();

        result.Add($"待機状態: {(this.waitingForMagicChargeResult ? "マジックチャージ後のAction待ち" : "待機なし")}");
        result.Add($"取得数: {this.magicChargeSlots.Count} / 2");

        var slot1 = this.magicChargeSlots.Count >= 1
            ? this.GetMagicChargeKindDisplayName(this.magicChargeSlots[0])
            : "未取得";

        var slot2 = this.magicChargeSlots.Count >= 2
            ? this.GetMagicChargeKindDisplayName(this.magicChargeSlots[1])
            : "未取得";

        result.Add($"1回目: {slot1}");
        result.Add($"2回目: {slot2}");

        if (!string.IsNullOrWhiteSpace(this.LastMagicChargeEvent))
            result.Add($"直近イベント: {this.LastMagicChargeEvent}");

        if (!string.IsNullOrWhiteSpace(this.LastMagicChargeResult))
            result.Add($"直近結果: {this.LastMagicChargeResult}");

        return result;
    }

    internal IReadOnlyList<string> GetGrandCrossStatusLines()
    {
        var result = new List<string>();

        result.Add($"詠唱中: {(this.grandCrossCasting ? "はい" : "いいえ")}");
        result.Add($"ステータス取得待ち: {(this.pendingGrandCrossStatusCapture ? "はい" : "いいえ")}");
        result.Add($"取得数: {this.grandCrossHeldTexts.Count} / 3");

        result.Add($"1回目: {(this.grandCrossHeldTexts.Count >= 1 ? this.grandCrossHeldTexts[0] : "未取得")}");
        result.Add($"2回目: {(this.grandCrossHeldTexts.Count >= 2 ? this.grandCrossHeldTexts[1] : "未取得")}");
        result.Add($"3回目: {(this.grandCrossHeldTexts.Count >= 3 ? this.grandCrossHeldTexts[2] : "未取得")}");

        if (this.currentGrandCrossInternalParam != 0)
            result.Add($"直近内部Param: {this.currentGrandCrossInternalParam}");

        if (!string.IsNullOrWhiteSpace(this.LastGrandCrossEvent))
            result.Add($"直近イベント: {this.LastGrandCrossEvent}");

        if (!string.IsNullOrWhiteSpace(this.LastGrandCrossResult))
            result.Add($"直近結果: {this.LastGrandCrossResult}");

        return result;
    }

    private void OnCommand(string command, string args)
    {
        this.configWindow.IsOpen = true;
    }

    private void OpenConfigUi()
    {
        this.configWindow.IsOpen = true;
    }

    private void Draw()
    {
        this.windowSystem.Draw();

        this.DrawOverheadText(
            this.activeLockText,
            this.activeLockUntil,
            this.configuration.MagicLockWorldHeightOffset,
            this.configuration.MagicLockScreenOffsetX,
            this.configuration.MagicLockScreenOffsetY,
            this.configuration.MagicLockFontSize,
            this.configuration.MagicLockDrawBackground,
            new Vector4(1.0f, 0.95f, 0.25f, 1.0f)
        );

        this.DrawOverheadText(
            this.activeChargeText,
            this.activeChargeUntil,
            this.configuration.MagicChargeWorldHeightOffset,
            this.configuration.MagicChargeScreenOffsetX,
            this.configuration.MagicChargeScreenOffsetY,
            this.configuration.MagicChargeFontSize,
            this.configuration.MagicChargeDrawBackground,
            new Vector4(0.35f, 0.9f, 1.0f, 1.0f)
        );

        this.DrawOverheadText(
            this.activeGrandCrossText,
            this.activeGrandCrossUntil,
            this.configuration.GrandCrossWorldHeightOffset,
            this.configuration.GrandCrossScreenOffsetX,
            this.configuration.GrandCrossScreenOffsetY,
            this.configuration.GrandCrossFontSize,
            this.configuration.GrandCrossDrawBackground,
            new Vector4(1.0f, 0.45f, 0.95f, 1.0f)
        );
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!this.configuration.Enabled)
            return;

        if (this.configuration.UseTerritoryFilter &&
            this.clientState.TerritoryType != this.configuration.TerritoryType)
        {
            this.previousCastKeys.Clear();
            return;
        }

        var currentCasts = new List<CastEvent>();
        var newlyStartedCasts = new List<CastEvent>();

        this.CollectCastEvents(currentCasts, newlyStartedCasts);

        this.ProcessMagicLock(currentCasts);
        this.ProcessMagicCharge(newlyStartedCasts);
        this.ProcessGrandCross();
    }

    private void CollectCastEvents(List<CastEvent> currentCasts, List<CastEvent> newlyStartedCasts)
    {
        var currentCastKeys = new HashSet<string>();

        foreach (var obj in this.objectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            if (!battleChara.IsCasting)
                continue;

            var actionId = battleChara.CastActionId;
            if (actionId == 0)
                continue;

            var casterName = battleChara.Name.ToString();
            var key = $"{casterName}:{actionId}";

            currentCastKeys.Add(key);

            var castEvent = new CastEvent(casterName, actionId, key);
            currentCasts.Add(castEvent);

            if (!this.previousCastKeys.Contains(key))
                newlyStartedCasts.Add(castEvent);
        }

        this.previousCastKeys = currentCastKeys;
    }

    private void ProcessMagicLock(List<CastEvent> currentCasts)
    {
        if (!this.configuration.MagicLockEnabled)
            return;

        var matches = this.FindMagicLockMatches(currentCasts);
        if (matches.Count == 0)
            return;

        var comboText = this.GetMagicLockComboText(matches);

        this.activeLockText = !string.IsNullOrWhiteSpace(comboText)
            ? comboText
            : string.Join("\n", matches.Select(match => match.Text));

        this.activeLockUntil = DateTime.Now.AddSeconds(this.configuration.MagicLockDisplaySeconds);

        var latest = matches[^1];

        this.LastCasterName = string.Join(" / ", matches.Select(match => match.CasterName).Distinct());
        this.LastActionId = latest.ActionId;
        this.LastMatchedLabel = !string.IsNullOrWhiteSpace(comboText)
            ? comboText
            : string.Join(" / ", matches.Select(match => match.Label).Distinct());
    }

    private List<CastMatch> FindMagicLockMatches(List<CastEvent> currentCasts)
    {
        var matches = new List<CastMatch>();

        foreach (var cast in currentCasts)
        {
            if (this.configuration.FilterByCasterName)
            {
                if (string.IsNullOrWhiteSpace(this.configuration.CasterNameKeyword))
                    continue;

                if (!cast.CasterName.Contains(this.configuration.CasterNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            foreach (var setting in this.configuration.ActionTextSettings)
            {
                if (!setting.Enabled)
                    continue;

                if (setting.ActionIds.Count == 0)
                    continue;

                if (!setting.ActionIds.Contains(cast.ActionId))
                    continue;

                matches.Add(new CastMatch(
                    cast.CasterName,
                    cast.ActionId,
                    setting.Label,
                    setting.Text
                ));
            }
        }

        return matches
            .GroupBy(match => match.Text)
            .Select(group => group.First())
            .ToList();
    }

    private string GetMagicLockComboText(List<CastMatch> matches)
    {
        var actionIds = matches
            .Select(match => match.ActionId)
            .ToHashSet();

        var hasFanNoStep = actionIds.Contains(47768);
        var hasFanStep = actionIds.Contains(47771) || actionIds.Contains(47774);

        var hasLineNoStep = actionIds.Contains(47775);
        var hasLineStep = actionIds.Contains(47776) || actionIds.Contains(47777);

        if (hasFanNoStep && hasLineNoStep)
            return this.configuration.MagicLockBothNoStepText;

        if (hasFanStep && hasLineStep)
            return this.configuration.MagicLockBothStepText;

        if (hasFanNoStep && hasLineStep)
            return this.configuration.MagicLockLineOnlyStepText;

        if (hasFanStep && hasLineNoStep)
            return this.configuration.MagicLockFanOnlyStepText;

        return string.Empty;
    }

    private void ProcessMagicCharge(List<CastEvent> newlyStartedCasts)
    {
        if (!this.configuration.MagicChargeEnabled)
            return;

        foreach (var cast in newlyStartedCasts)
        {
            if (this.configuration.FilterByCasterName)
            {
                if (string.IsNullOrWhiteSpace(this.configuration.CasterNameKeyword))
                    continue;

                if (!cast.CasterName.Contains(this.configuration.CasterNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            if (cast.ActionId == 47780)
            {
                this.OnMagicChargeStarted(cast);
                continue;
            }

            if (cast.ActionId == 47781)
            {
                this.OnMagicOut(cast);
                continue;
            }

            var chargeKind = this.ToMagicChargeKind(cast.ActionId);
            if (chargeKind == MagicChargeKind.None)
                continue;

            this.OnMagicChargeResultAction(cast, chargeKind);
        }
    }

    private void OnMagicChargeStarted(CastEvent cast)
    {
        if (this.magicChargeCycleCompleted || this.magicChargeSlots.Count >= 2)
        {
            this.magicChargeSlots.Clear();
            this.magicChargeCycleCompleted = false;
        }

        this.waitingForMagicChargeResult = true;
        this.LastMagicChargeEvent = $"マジックチャージ検出: {cast.CasterName} / 47780";
    }

    private void OnMagicChargeResultAction(CastEvent cast, MagicChargeKind chargeKind)
    {
        if (!this.waitingForMagicChargeResult)
            return;

        if (this.magicChargeSlots.Count < 2)
        {
            this.magicChargeSlots.Add(chargeKind);
        }
        else
        {
            this.magicChargeSlots.RemoveAt(0);
            this.magicChargeSlots.Add(chargeKind);
        }

        this.waitingForMagicChargeResult = false;

        this.LastMagicChargeEvent =
            $"チャージAction保持: {cast.CasterName} / {cast.ActionId} / {this.GetMagicChargeKindDisplayName(chargeKind)}";
    }

    private void OnMagicOut(CastEvent cast)
    {
        var resultText = this.GetMagicChargeResultText();

        if (string.IsNullOrWhiteSpace(resultText))
            resultText = this.configuration.MagicChargeUnknownText;

        this.activeChargeText = resultText;
        this.activeChargeUntil = DateTime.Now.AddSeconds(this.configuration.MagicChargeDisplaySeconds);

        this.waitingForMagicChargeResult = false;
        this.magicChargeCycleCompleted = true;

        this.LastMagicChargeEvent = $"マジックアウト検出: {cast.CasterName} / 47781";
        this.LastMagicChargeResult = resultText;
    }

    private MagicChargeKind ToMagicChargeKind(uint actionId)
    {
        return actionId switch
        {
            47768 => MagicChargeKind.FanNoStep,
            47771 => MagicChargeKind.FanStep,
            47774 => MagicChargeKind.FanStep,
            47775 => MagicChargeKind.LineNoStep,
            47776 => MagicChargeKind.LineStep,
            47777 => MagicChargeKind.LineStep,
            _ => MagicChargeKind.None
        };
    }

    private string GetMagicChargeResultText()
    {
        if (this.magicChargeSlots.Count < 2)
            return string.Empty;

        var hasFanNoStep = this.magicChargeSlots.Contains(MagicChargeKind.FanNoStep);
        var hasFanStep = this.magicChargeSlots.Contains(MagicChargeKind.FanStep);

        var hasLineNoStep = this.magicChargeSlots.Contains(MagicChargeKind.LineNoStep);
        var hasLineStep = this.magicChargeSlots.Contains(MagicChargeKind.LineStep);

        if (hasFanNoStep && hasLineNoStep)
            return this.configuration.MagicChargeFanNoStepLineNoStepText;

        if (hasFanNoStep && hasLineStep)
            return this.configuration.MagicChargeFanNoStepLineStepText;

        if (hasFanStep && hasLineNoStep)
            return this.configuration.MagicChargeFanStepLineNoStepText;

        if (hasFanStep && hasLineStep)
            return this.configuration.MagicChargeFanStepLineStepText;

        return string.Empty;
    }

    private string GetMagicChargeKindDisplayName(MagicChargeKind kind)
    {
        return kind switch
        {
            MagicChargeKind.FanNoStep => "47768 / ひろがるブリザガ",
            MagicChargeKind.FanStep => "47771 or 47774 / ひろがるブリザガ",
            MagicChargeKind.LineNoStep => "47775 / もりもりサンダガ",
            MagicChargeKind.LineStep => "47776 or 47777 / もりもりサンダガ",
            _ => "未取得"
        };
    }

    private void ProcessGrandCross()
    {
        if (!this.configuration.GrandCrossEnabled)
            return;

        var grandCrossCaster = this.FindGrandCrossCaster();

        if (grandCrossCaster != null)
        {
            if (this.grandCrossCycleCompleted || this.grandCrossHeldTexts.Count >= 3)
            {
                this.grandCrossHeldTexts.Clear();
                this.grandCrossCycleCompleted = false;
            }

            this.grandCrossCasting = true;
            this.pendingGrandCrossStatusCapture = false;
            this.currentGrandCrossCasterName = grandCrossCaster.Name.ToString();

            var param = this.GetGrandCrossInternalParam(grandCrossCaster);
            if (param == 1121 || param == 1122)
                this.currentGrandCrossInternalParam = param;

            this.LastGrandCrossEvent =
                $"グランドクロス詠唱中: {this.currentGrandCrossCasterName} / Param:{this.currentGrandCrossInternalParam}";

            return;
        }

        if (this.grandCrossCasting)
        {
            this.grandCrossCasting = false;
            this.pendingGrandCrossStatusCapture = true;
            this.pendingGrandCrossStatusCaptureUntil = DateTime.Now.AddSeconds(this.configuration.GrandCrossStatusCaptureSeconds);
            this.pendingGrandCrossIsFake = this.currentGrandCrossInternalParam == 1121;

            this.LastGrandCrossEvent =
                $"グランドクロス詠唱完了: {this.currentGrandCrossCasterName} / Param:{this.currentGrandCrossInternalParam}";

            return;
        }

        if (!this.pendingGrandCrossStatusCapture)
            return;

        if (DateTime.Now > this.pendingGrandCrossStatusCaptureUntil)
        {
            this.pendingGrandCrossStatusCapture = false;
            this.LastGrandCrossEvent = "グランドクロス付与ステータス取得タイムアウト";
            return;
        }

        var statusText = this.FindGrandCrossStatusText();
        if (string.IsNullOrWhiteSpace(statusText))
            return;

        if (this.pendingGrandCrossIsFake)
            statusText = $"{this.configuration.GrandCrossFakePrefix}{statusText}";

        this.AddGrandCrossHeldText(statusText);

        this.pendingGrandCrossStatusCapture = false;
        this.pendingGrandCrossIsFake = false;

        this.LastGrandCrossEvent = $"グランドクロス保持: {statusText}";
        this.LastGrandCrossResult = string.Join(this.configuration.GrandCrossSeparator, this.grandCrossHeldTexts);

        if (this.grandCrossHeldTexts.Count >= 3)
        {
            var resultText = string.Join(this.configuration.GrandCrossSeparator, this.grandCrossHeldTexts);

            this.activeGrandCrossText = resultText;
            this.activeGrandCrossUntil = DateTime.Now.AddSeconds(this.configuration.GrandCrossDisplaySeconds);

            this.grandCrossCycleCompleted = true;
            this.LastGrandCrossEvent = "グランドクロス3回分表示";
            this.LastGrandCrossResult = resultText;
        }
    }

    private IBattleChara? FindGrandCrossCaster()
    {
        foreach (var obj in this.objectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            if (!battleChara.IsCasting)
                continue;

            if (battleChara.CastActionId != 47892)
                continue;

            if (this.configuration.GrandCrossFilterByEnemyName)
            {
                var name = battleChara.Name.ToString();

                if (string.IsNullOrWhiteSpace(this.configuration.GrandCrossEnemyNameKeyword))
                    continue;

                if (!name.Contains(this.configuration.GrandCrossEnemyNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            return battleChara;
        }

        return null;
    }

    private uint GetGrandCrossInternalParam(IBattleChara battleChara)
    {
        foreach (var status in battleChara.StatusList)
        {
            if (status.StatusId != 2056)
                continue;

            if (status.Param == 1121 || status.Param == 1122)
                return status.Param;
        }

        return this.currentGrandCrossInternalParam;
    }

    private string FindGrandCrossStatusText()
    {
        foreach (var obj in this.objectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            if (this.configuration.GrandCrossFilterByEnemyName)
            {
                var name = battleChara.Name.ToString();

                if (string.IsNullOrWhiteSpace(this.configuration.GrandCrossEnemyNameKeyword))
                    continue;

                if (!name.Contains(this.configuration.GrandCrossEnemyNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            foreach (var status in battleChara.StatusList)
            {
                var text = this.GetGrandCrossStatusDisplayText(status.StatusId);
                if (!string.IsNullOrWhiteSpace(text))
                    return text;
            }
        }

        return string.Empty;
    }

    private string GetGrandCrossStatusDisplayText(uint statusId)
    {
        return statusId switch
        {
            454 => this.configuration.GrandCrossAllaganFieldText,
            5464 => this.configuration.GrandCrossDeathBeyondText,
            4887 => this.configuration.GrandCrossLivingWoundText,
            4888 => this.configuration.GrandCrossDeadWoundText,
            5543 => this.configuration.GrandCrossCurseShriekText,
            5544 => this.configuration.GrandCrossForkedLightningText,
            5545 => this.configuration.GrandCrossWaterCompressionText,
            _ => string.Empty
        };
    }

    private void AddGrandCrossHeldText(string text)
    {
        if (this.grandCrossHeldTexts.Count < 3)
        {
            this.grandCrossHeldTexts.Add(text);
            return;
        }

        this.grandCrossHeldTexts.RemoveAt(0);
        this.grandCrossHeldTexts.Add(text);
    }

    private void DrawOverheadText(
        string text,
        DateTime until,
        float worldHeightOffset,
        float screenOffsetX,
        float screenOffsetY,
        float fontSize,
        bool drawBackground,
        Vector4 textColor)
    {
        if (DateTime.Now > until)
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var localPlayer = this.GetLocalPlayerFromObjectTable();
        if (localPlayer == null)
            return;

        var worldPosition = localPlayer.Position;
        worldPosition.Y += worldHeightOffset;

        if (!this.gameGui.WorldToScreen(worldPosition, out var screenPosition))
            return;

        screenPosition.X += screenOffsetX;
        screenPosition.Y += screenOffsetY;

        var drawList = ImGui.GetForegroundDrawList();

        var textSize = ImGui.CalcTextSize(text);
        var baseFontSize = MathF.Max(1.0f, ImGui.GetFontSize());
        var fontScale = fontSize / baseFontSize;
        textSize *= fontScale;

        var textPosition = new Vector2(
            screenPosition.X - textSize.X / 2.0f,
            screenPosition.Y - textSize.Y / 2.0f
        );

        if (drawBackground)
        {
            var padding = new Vector2(8.0f, 5.0f);
            var bgMin = textPosition - padding;
            var bgMax = textPosition + textSize + padding;

            drawList.AddRectFilled(
                bgMin,
                bgMax,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)),
                6.0f
            );
        }

        drawList.AddText(
            ImGui.GetFont(),
            fontSize,
            textPosition,
            ImGui.ColorConvertFloat4ToU32(textColor),
            text
        );
    }

    private IGameObject? GetLocalPlayerFromObjectTable()
    {
        try
        {
            return this.objectTable[0];
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "ObjectTable[0] からローカルプレイヤーを取得できませんでした。");
            return null;
        }
    }

    private readonly record struct CastEvent(
        string CasterName,
        uint ActionId,
        string Key
    );

    private readonly record struct CastMatch(
        string CasterName,
        uint ActionId,
        string Label,
        string Text
    );

    private enum MagicChargeKind
    {
        None = 0,
        FanNoStep = 1,
        FanStep = 2,
        LineNoStep = 3,
        LineStep = 4
    }
}
