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

namespace MagicLook;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/magiclook";

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IObjectTable objectTable;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("magicLook");
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

    private readonly List<GrandCrossHeldDebuff> grandCrossHeldDebuffs = new();
    private readonly List<PendingChaosDebuffText> pendingChaosDebuffTexts = new();
    private HashSet<string> grandCrossStatusSnapshot = new();
    private int grandCrossCapturedCastCount;

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
            HelpMessage = "magicLook の設定画面を開きます。"
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
        this.activeLockUntil = DateTime.Now.AddSeconds(this.configuration.MagicLookDisplaySeconds);

        this.LastCasterName = "テスト";
        this.LastActionId = 0;
        this.LastMatchedLabel = "magicLook テスト表示";
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

        this.grandCrossHeldDebuffs.Clear();
        this.pendingChaosDebuffTexts.Clear();
        this.grandCrossStatusSnapshot.Clear();
        this.grandCrossCapturedCastCount = 0;

        this.activeGrandCrossText = string.Empty;
        this.activeGrandCrossUntil = DateTime.MinValue;

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
        var activeItems = this.GetActiveGrandCrossDisplayItemsSorted();

        result.Add($"詠唱中: {(this.grandCrossCasting ? "はい" : "いいえ")}");
        result.Add($"ステータス取得待ち: {(this.pendingGrandCrossStatusCapture ? "はい" : "いいえ")}");
        result.Add("デバフ取得対象: 自分のみ");
        result.Add("GrandCross真偽: Param 1121=偽, 1122=真");
        result.Add("カオスAction対象: カオス / 47902 ほのお / 47903 つなみ");
        result.Add("カオスStatus対象: 5547 混沌の炎 / 5548 混沌の水");
        result.Add($"グランドクロス取得回数: {this.grandCrossCapturedCastCount} / 3");
        result.Add($"保持デバフ数: {this.grandCrossHeldDebuffs.Count}");
        result.Add($"カオス待機数: {this.pendingChaosDebuffTexts.Count}");
        result.Add($"現在表示対象: {activeItems.Count}");

        if (activeItems.Count == 0)
        {
            result.Add("表示対象: なし");
        }
        else
        {
            for (var i = 0; i < activeItems.Count; i++)
            {
                var item = activeItems[i];
                result.Add($"{i + 1}: {item.DisplayText} / 残り {item.RemainingTime:0.0}秒");
            }
        }

        if (this.currentGrandCrossInternalParam != 0)
            result.Add($"直近内部Param: {this.currentGrandCrossInternalParam}");

        if (!string.IsNullOrWhiteSpace(this.LastGrandCrossEvent))
            result.Add($"直近イベント: {this.LastGrandCrossEvent}");

        if (!string.IsNullOrWhiteSpace(this.LastGrandCrossResult))
            result.Add($"直近結果: {this.LastGrandCrossResult}");

        return result;
    }

    internal IReadOnlyList<string> GetChaosStatusLines()
    {
        var result = new List<string>();

        result.Add("対象Action: 47902 / ほのお, 47903 / つなみ");
        result.Add("対象Status: 5547 / 混沌の炎, 5548 / 混沌の水");
        result.Add("Param: 1119=偽, 1120=真");
        result.Add($"待機テキスト数: {this.pendingChaosDebuffTexts.Count}");

        if (this.pendingChaosDebuffTexts.Count == 0)
        {
            result.Add("待機テキスト: なし");
        }
        else
        {
            foreach (var pending in this.pendingChaosDebuffTexts)
            {
                var statusName = pending.StatusId switch
                {
                    5547 => "5547 / 混沌の炎",
                    5548 => "5548 / 混沌の水",
                    _ => pending.StatusId.ToString()
                };

                result.Add($"待機: {statusName} => {pending.DisplayText}");
            }
        }

        var activeChaosDebuffs = this.GetActiveGrandCrossHeldDebuffsSorted()
            .Where(item => item.StatusId == 5547 || item.StatusId == 5548)
            .ToList();

        result.Add($"現在表示中のカオスStatus数: {activeChaosDebuffs.Count}");

        if (activeChaosDebuffs.Count == 0)
        {
            result.Add("現在表示中: なし");
        }
        else
        {
            foreach (var debuff in activeChaosDebuffs)
            {
                var statusName = debuff.StatusId switch
                {
                    5547 => "5547 / 混沌の炎",
                    5548 => "5548 / 混沌の水",
                    _ => debuff.StatusId.ToString()
                };

                result.Add($"表示中: {statusName} => {debuff.DisplayText} / 残り {debuff.RemainingTime:0.0}秒");
            }
        }

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
            this.configuration.MagicLookWorldHeightOffset,
            this.configuration.MagicLookScreenOffsetX,
            this.configuration.MagicLookScreenOffsetY,
            this.configuration.MagicLookFontSize,
            this.configuration.MagicLookDrawBackground,
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

        this.ProcessMagicLook(currentCasts);
        this.ProcessMagicCharge(newlyStartedCasts);
        this.ProcessGrandCross(newlyStartedCasts);
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

            var castEvent = new CastEvent(casterName, actionId, key, battleChara);
            currentCasts.Add(castEvent);

            if (!this.previousCastKeys.Contains(key))
                newlyStartedCasts.Add(castEvent);
        }

        this.previousCastKeys = currentCastKeys;
    }

    private void ProcessMagicLook(List<CastEvent> currentCasts)
    {
        if (!this.configuration.MagicLookEnabled)
            return;

        var matches = this.FindMagicLookMatches(currentCasts);
        if (matches.Count == 0)
            return;

        var comboText = this.GetMagicLookComboText(matches);

        this.activeLockText = !string.IsNullOrWhiteSpace(comboText)
            ? comboText
            : string.Join("\n", matches.Select(match => match.Text));

        this.activeLockUntil = DateTime.Now.AddSeconds(this.configuration.MagicLookDisplaySeconds);

        var latest = matches[^1];

        this.LastCasterName = string.Join(" / ", matches.Select(match => match.CasterName).Distinct());
        this.LastActionId = latest.ActionId;
        this.LastMatchedLabel = !string.IsNullOrWhiteSpace(comboText)
            ? comboText
            : string.Join(" / ", matches.Select(match => match.Label).Distinct());
    }

    private List<CastMatch> FindMagicLookMatches(List<CastEvent> currentCasts)
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

    private string GetMagicLookComboText(List<CastMatch> matches)
    {
        var actionIds = matches
            .Select(match => match.ActionId)
            .ToHashSet();

        var hasFanNoStep = actionIds.Contains(47768);
        var hasFanStep = actionIds.Contains(47771) || actionIds.Contains(47774);

        var hasLineNoStep = actionIds.Contains(47775);
        var hasLineStep = actionIds.Contains(47776) || actionIds.Contains(47777);

        if (hasFanNoStep && hasLineNoStep)
            return this.configuration.MagicLookBothNoStepText;

        if (hasFanStep && hasLineStep)
            return this.configuration.MagicLookBothStepText;

        if (hasFanNoStep && hasLineStep)
            return this.configuration.MagicLookLineOnlyStepText;

        if (hasFanStep && hasLineNoStep)
            return this.configuration.MagicLookFanOnlyStepText;

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

    private void ProcessGrandCross(List<CastEvent> newlyStartedCasts)
    {
        if (!this.configuration.GrandCrossEnabled)
            return;

        this.ProcessGrandCrossChaosActions(newlyStartedCasts);
        this.ProcessPendingChaosDebuffs();

        if (this.grandCrossCycleCompleted)
            this.UpdateGrandCrossDisplayFromHeldItems();

        var grandCrossCaster = this.FindGrandCrossCaster();

        if (grandCrossCaster != null)
        {
            if (this.grandCrossCycleCompleted)
            {
                this.grandCrossHeldDebuffs.Clear();
                this.pendingChaosDebuffTexts.Clear();
                this.grandCrossStatusSnapshot.Clear();
                this.grandCrossCapturedCastCount = 0;
                this.grandCrossCycleCompleted = false;
                this.activeGrandCrossText = string.Empty;
                this.activeGrandCrossUntil = DateTime.MinValue;
            }

            if (!this.grandCrossCasting)
            {
                this.grandCrossStatusSnapshot = this.CreateGrandCrossStatusSnapshot();

                this.LastGrandCrossEvent =
                    $"グランドクロス詠唱開始: {grandCrossCaster.Name} / 47892";
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

        var newDebuffs = this.FindGrandCrossDebuffs(preferNewStatusOnly: true);

        if (newDebuffs.Count == 0 && DateTime.Now > this.pendingGrandCrossStatusCaptureUntil)
        {
            newDebuffs = this.FindGrandCrossDebuffs(preferNewStatusOnly: false);
        }

        if (newDebuffs.Count == 0)
        {
            if (DateTime.Now > this.pendingGrandCrossStatusCaptureUntil)
            {
                this.pendingGrandCrossStatusCapture = false;
                this.LastGrandCrossEvent = "グランドクロス付与ステータス取得タイムアウト";
            }

            return;
        }

        var isFake = this.pendingGrandCrossIsFake;
        var addedTexts = new List<string>();

        foreach (var debuff in newDebuffs)
        {
            var displayText = this.GetGrandCrossStatusDisplayText(debuff.StatusId, isFake);

            if (debuff.StatusId == 5547 || debuff.StatusId == 5548)
                displayText = debuff.Text;

            if (string.IsNullOrWhiteSpace(displayText))
                continue;

            this.AddOrUpdateGrandCrossHeldDebuff(
                debuff.StatusId,
                displayText,
                isFake
            );

            addedTexts.Add(displayText);
        }

        this.grandCrossCapturedCastCount++;

        this.pendingGrandCrossStatusCapture = false;
        this.pendingGrandCrossIsFake = false;

        var addedText = string.Join(this.configuration.GrandCrossSeparator, addedTexts);

        this.LastGrandCrossEvent = $"グランドクロス保持: {addedText}";
        this.LastGrandCrossResult = string.Join(
            this.configuration.GrandCrossSeparator,
            this.GetActiveGrandCrossDisplayItemsSorted().Select(item => item.DisplayText)
        );

        if (this.grandCrossCapturedCastCount >= 3)
        {
            this.grandCrossCycleCompleted = true;
            this.UpdateGrandCrossDisplayFromHeldItems();

            this.LastGrandCrossEvent = "グランドクロス3回分表示開始";
        }
    }

    private void ProcessGrandCrossChaosActions(List<CastEvent> newlyStartedCasts)
    {
        foreach (var cast in newlyStartedCasts)
        {
            if (cast.ActionId != 47902 && cast.ActionId != 47903)
                continue;

            if (this.configuration.GrandCrossChaosFilterByEnemyName)
            {
                if (string.IsNullOrWhiteSpace(this.configuration.GrandCrossChaosEnemyNameKeyword))
                    continue;

                if (!cast.CasterName.Contains(this.configuration.GrandCrossChaosEnemyNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            var param = this.GetChaosGrandCrossInternalParam(cast.Caster);

            if (param != 1119 && param != 1120)
            {
                this.LastGrandCrossEvent =
                    $"カオスAction検出: {cast.CasterName} / {cast.ActionId} / Param未検出";

                continue;
            }

            var statusId = cast.ActionId switch
            {
                47902 => 5547u,
                47903 => 5548u,
                _ => 0u
            };

            var text = this.GetChaosGrandCrossDisplayText(cast.ActionId, param);
            if (statusId == 0 || string.IsNullOrWhiteSpace(text))
                continue;

            this.AddOrUpdatePendingChaosDebuffText(statusId, text);

            this.LastGrandCrossEvent =
                $"カオスAction待機: {cast.CasterName} / {cast.ActionId} / Param:{param} / StatusId:{statusId} / {text}";
        }
    }

    private void ProcessPendingChaosDebuffs()
    {
        if (this.pendingChaosDebuffTexts.Count == 0)
            return;

        var localPlayer = this.GetLocalPlayerAsBattleChara();
        if (localPlayer == null)
            return;

        var addedTexts = new List<string>();

        foreach (var status in localPlayer.StatusList)
        {
            if (status.StatusId != 5547 && status.StatusId != 5548)
                continue;

            var displayText = this.GetPendingChaosDebuffDisplayText(status.StatusId);
            if (string.IsNullOrWhiteSpace(displayText))
                continue;

            var existing = this.grandCrossHeldDebuffs.FirstOrDefault(item => item.StatusId == status.StatusId);
            var shouldLog = existing == null || existing.DisplayText != displayText;

            this.AddOrUpdateGrandCrossHeldDebuff(
                status.StatusId,
                displayText,
                false
            );

            if (shouldLog)
                addedTexts.Add($"{displayText} / 残り {status.RemainingTime:0.0}秒");
        }

        if (addedTexts.Count == 0)
            return;

        this.LastGrandCrossEvent =
            $"カオスStatus保持: {string.Join(this.configuration.GrandCrossSeparator, addedTexts)}";

        this.LastGrandCrossResult = string.Join(
            this.configuration.GrandCrossSeparator,
            this.GetActiveGrandCrossDisplayItemsSorted().Select(item => item.DisplayText)
        );

        if (this.grandCrossCycleCompleted)
            this.UpdateGrandCrossDisplayFromHeldItems();
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

    private uint GetChaosGrandCrossInternalParam(IBattleChara battleChara)
    {
        foreach (var status in battleChara.StatusList)
        {
            if (status.StatusId != 2056)
                continue;

            if (status.Param == 1119 || status.Param == 1120)
                return status.Param;
        }

        return 0;
    }

    private string GetChaosGrandCrossDisplayText(uint actionId, uint param)
    {
        return actionId switch
        {
            47902 when param == 1119 => this.configuration.GrandCrossFireFakeText,
            47902 when param == 1120 => this.configuration.GrandCrossFireTrueText,

            47903 when param == 1119 => this.configuration.GrandCrossTsunamiFakeText,
            47903 when param == 1120 => this.configuration.GrandCrossTsunamiTrueText,

            _ => string.Empty
        };
    }

    private List<GrandCrossDetectedDebuff> FindGrandCrossDebuffs(bool preferNewStatusOnly)
    {
        var result = new List<GrandCrossDetectedDebuff>();

        var localPlayer = this.GetLocalPlayerAsBattleChara();
        if (localPlayer == null)
            return result;

        foreach (var status in localPlayer.StatusList)
        {
            if (!this.IsGrandCrossTargetStatus(status.StatusId))
                continue;

            var text = status.StatusId switch
            {
                5547 => this.GetPendingChaosDebuffDisplayText(5547),
                5548 => this.GetPendingChaosDebuffDisplayText(5548),
                _ => this.GetGrandCrossStatusDisplayText(status.StatusId, isFake: false)
            };

            if (string.IsNullOrWhiteSpace(text))
                continue;

            var key = this.CreateGrandCrossStatusKey(localPlayer, status.StatusId);

            if (preferNewStatusOnly && this.grandCrossStatusSnapshot.Contains(key))
                continue;

            if (result.Any(item => item.StatusId == status.StatusId))
                continue;

            result.Add(new GrandCrossDetectedDebuff(
                status.StatusId,
                text,
                status.RemainingTime
            ));
        }

        return result;
    }

    private void AddOrUpdateGrandCrossHeldDebuff(uint statusId, string displayText, bool isFake)
    {
        var existing = this.grandCrossHeldDebuffs.FirstOrDefault(item => item.StatusId == statusId);

        if (existing != null)
        {
            existing.DisplayText = displayText;
            existing.IsFake = isFake;
            return;
        }

        this.grandCrossHeldDebuffs.Add(new GrandCrossHeldDebuff
        {
            StatusId = statusId,
            DisplayText = displayText,
            IsFake = isFake
        });
    }

    private void AddOrUpdatePendingChaosDebuffText(uint statusId, string displayText)
    {
        var existing = this.pendingChaosDebuffTexts.FirstOrDefault(item => item.StatusId == statusId);

        if (existing != null)
        {
            existing.DisplayText = displayText;
            return;
        }

        this.pendingChaosDebuffTexts.Add(new PendingChaosDebuffText
        {
            StatusId = statusId,
            DisplayText = displayText
        });
    }

    private List<GrandCrossHeldDebuff> GetActiveGrandCrossHeldDebuffsSorted()
    {
        var localPlayer = this.GetLocalPlayerAsBattleChara();
        if (localPlayer == null)
            return new List<GrandCrossHeldDebuff>();

        var activeStatusMap = new Dictionary<uint, float>();

        foreach (var status in localPlayer.StatusList)
        {
            if (!this.IsGrandCrossTargetStatus(status.StatusId))
                continue;

            activeStatusMap[status.StatusId] = status.RemainingTime;
        }

        var activeDebuffs = new List<GrandCrossHeldDebuff>();

        foreach (var held in this.grandCrossHeldDebuffs)
        {
            if (!activeStatusMap.TryGetValue(held.StatusId, out var remainingTime))
                continue;

            held.RemainingTime = remainingTime;
            activeDebuffs.Add(held);
        }

        return activeDebuffs
            .OrderBy(item => item.RemainingTime)
            .ThenBy(item => item.StatusId)
            .ToList();
    }

    private List<GrandCrossDisplayItem> GetActiveGrandCrossDisplayItemsSorted()
    {
        var activeDebuffs = this.GetActiveGrandCrossHeldDebuffsSorted();

        var result = new List<GrandCrossDisplayItem>();

        foreach (var debuff in activeDebuffs)
        {
            result.Add(new GrandCrossDisplayItem
            {
                DisplayText = debuff.DisplayText,
                RemainingTime = debuff.RemainingTime,
                SortKey = debuff.StatusId
            });
        }

        return result
            .OrderBy(item => item.RemainingTime)
            .ThenBy(item => item.SortKey)
            .ToList();
    }

    private void UpdateGrandCrossDisplayFromHeldItems()
    {
        var activeItems = this.GetActiveGrandCrossDisplayItemsSorted();

        if (activeItems.Count == 0)
        {
            this.activeGrandCrossText = string.Empty;
            this.activeGrandCrossUntil = DateTime.MinValue;
            this.LastGrandCrossResult = string.Empty;
            return;
        }

        var resultText = string.Join(
            this.configuration.GrandCrossSeparator,
            activeItems.Select(item => item.DisplayText)
        );

        this.activeGrandCrossText = resultText;

        this.activeGrandCrossUntil = DateTime.Now.AddSeconds(this.configuration.GrandCrossDisplaySeconds);

        this.LastGrandCrossResult = resultText;
    }

    private HashSet<string> CreateGrandCrossStatusSnapshot()
    {
        var result = new HashSet<string>();

        var localPlayer = this.GetLocalPlayerAsBattleChara();
        if (localPlayer == null)
            return result;

        foreach (var status in localPlayer.StatusList)
        {
            if (!this.IsGrandCrossTargetStatus(status.StatusId))
                continue;

            result.Add(this.CreateGrandCrossStatusKey(localPlayer, status.StatusId));
        }

        return result;
    }

    private string CreateGrandCrossStatusKey(IBattleChara battleChara, uint statusId)
    {
        return $"{battleChara.EntityId}:{statusId}";
    }

    private bool IsGrandCrossTargetStatus(uint statusId)
    {
        return statusId is
            454 or
            5464 or
            4887 or
            4888 or
            5543 or
            5544 or
            5545 or
            5546 or
            5547 or
            5548;
    }

    private string GetGrandCrossStatusDisplayText(uint statusId, bool isFake)
    {
        return statusId switch
        {
            454 => isFake ? this.configuration.GrandCrossAllaganFieldFakeText : this.configuration.GrandCrossAllaganFieldTrueText,
            5464 => isFake ? this.configuration.GrandCrossDeathBeyondFakeText : this.configuration.GrandCrossDeathBeyondTrueText,
            4887 => isFake ? this.configuration.GrandCrossLivingWoundFakeText : this.configuration.GrandCrossLivingWoundTrueText,
            4888 => isFake ? this.configuration.GrandCrossDeadWoundFakeText : this.configuration.GrandCrossDeadWoundTrueText,
            5543 => isFake ? this.configuration.GrandCrossCurseShriekFakeText : this.configuration.GrandCrossCurseShriekTrueText,
            5544 => isFake ? this.configuration.GrandCrossForkedLightningFakeText : this.configuration.GrandCrossForkedLightningTrueText,
            5545 => isFake ? this.configuration.GrandCrossWaterCompressionFakeText : this.configuration.GrandCrossWaterCompressionTrueText,
            5546 => isFake ? this.configuration.GrandCrossAccelerationBombFakeText : this.configuration.GrandCrossAccelerationBombTrueText,
            5547 => this.GetPendingChaosDebuffDisplayText(5547),
            5548 => this.GetPendingChaosDebuffDisplayText(5548),
            _ => string.Empty
        };
    }

    private string GetPendingChaosDebuffDisplayText(uint statusId)
    {
        var pending = this.pendingChaosDebuffTexts.FirstOrDefault(item => item.StatusId == statusId);
        if (pending != null && !string.IsNullOrWhiteSpace(pending.DisplayText))
            return pending.DisplayText;

        return statusId switch
        {
            5547 => this.configuration.GrandCrossFireTrueText,
            5548 => this.configuration.GrandCrossTsunamiTrueText,
            _ => string.Empty
        };
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

    private IBattleChara? GetLocalPlayerAsBattleChara()
    {
        try
        {
            return this.objectTable[0] as IBattleChara;
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "ObjectTable[0] からローカルプレイヤーをIBattleCharaとして取得できませんでした。");
            return null;
        }
    }

    private readonly record struct CastEvent(
        string CasterName,
        uint ActionId,
        string Key,
        IBattleChara Caster
    );

    private readonly record struct CastMatch(
        string CasterName,
        uint ActionId,
        string Label,
        string Text
    );

    private sealed class GrandCrossHeldDebuff
    {
        public uint StatusId { get; init; }

        public string DisplayText { get; set; } = string.Empty;

        public bool IsFake { get; set; }

        public float RemainingTime { get; set; }
    }

    private sealed class PendingChaosDebuffText
    {
        public uint StatusId { get; init; }

        public string DisplayText { get; set; } = string.Empty;
    }

    private sealed class GrandCrossDisplayItem
    {
        public string DisplayText { get; init; } = string.Empty;

        public float RemainingTime { get; init; }

        public uint SortKey { get; init; }
    }

    private readonly record struct GrandCrossDetectedDebuff(
        uint StatusId,
        string Text,
        float RemainingTime
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