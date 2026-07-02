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
    private DateTime activeChargeStartedAt = DateTime.MinValue;
    private DateTime activeChargeUntil = DateTime.MinValue;

    private string activeGrandCrossText = string.Empty;
    private DateTime activeGrandCrossStartedAt = DateTime.MinValue;
    private DateTime activeGrandCrossUntil = DateTime.MinValue;

    private HashSet<string> previousCastKeys = new();

    private bool magicChargeActive;
    private MagicChargeStatusState chargeThunderState = MagicChargeStatusState.Unknown;
    private MagicChargeStatusState chargeBlizzardState = MagicChargeStatusState.Unknown;

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
        this.SetMagicChargeScreenText(text);

        this.LastMagicChargeEvent = "テスト";
        this.LastMagicChargeResult = text;
    }

    internal void TestGrandCrossText(string text)
    {
        this.SetGrandCrossText(text, resetFade: true);

        this.LastGrandCrossEvent = "テスト";
        this.LastGrandCrossResult = text;
    }

    internal void ResetMagicChargeState()
    {
        this.magicChargeActive = false;
        this.chargeThunderState = MagicChargeStatusState.Unknown;
        this.chargeBlizzardState = MagicChargeStatusState.Unknown;

        this.activeChargeText = string.Empty;
        this.activeChargeStartedAt = DateTime.MinValue;
        this.activeChargeUntil = DateTime.MinValue;

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
        this.activeGrandCrossStartedAt = DateTime.MinValue;
        this.activeGrandCrossUntil = DateTime.MinValue;

        this.LastGrandCrossEvent = "リセット";
        this.LastGrandCrossResult = string.Empty;
    }

    internal IReadOnlyList<string> GetMagicChargeStatusLines()
    {
        var result = new List<string>();

        result.Add($"チャージ監視状態: {(this.magicChargeActive ? "監視中" : "待機なし")}");
        result.Add("対象Status: 1485 / チャージ：サンダガ, 1484 / チャージ：ブリザガ");
        result.Add("判定条件: Param 0=真, Param 0以外=偽");
        result.Add($"チャージ：サンダガ: {this.GetMagicChargeStatusStateDisplayName(this.chargeThunderState)}");
        result.Add($"チャージ：ブリザガ: {this.GetMagicChargeStatusStateDisplayName(this.chargeBlizzardState)}");

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
                result.Add($"{i + 1}: {this.FormatGrandCrossDisplayItem(item)}");
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

        this.DrawScreenText(
            this.activeChargeText,
            this.activeChargeStartedAt,
            this.activeChargeUntil,
            this.configuration.MagicChargeScreenPositionX,
            this.configuration.MagicChargeScreenPositionY,
            this.GetMagicChargePresetFontSize(),
            this.configuration.MagicChargeDrawBackground,
            new Vector4(
                this.configuration.MagicChargeTextColorR,
                this.configuration.MagicChargeTextColorG,
                this.configuration.MagicChargeTextColorB,
                this.configuration.MagicChargeTextColorA
            ),
            this.configuration.MagicChargeFadeInSeconds,
            this.configuration.MagicChargeUseOutline,
            this.GetMagicChargePresetOutlineThickness(),
            new Vector4(
                this.configuration.MagicChargeOutlineColorR,
                this.configuration.MagicChargeOutlineColorG,
                this.configuration.MagicChargeOutlineColorB,
                this.configuration.MagicChargeOutlineColorA
            ),
            this.configuration.MagicChargeFontPreset
        );

        if (this.configuration.GrandCrossDisplayMode == 1)
        {
            this.DrawScreenText(
                this.activeGrandCrossText,
                this.activeGrandCrossStartedAt,
                this.activeGrandCrossUntil,
                this.configuration.GrandCrossScreenPositionX,
                this.configuration.GrandCrossScreenPositionY,
                this.GetGrandCrossPresetFontSize(),

                // 画面固定位置表示では、文字が背景に埋もれるため強制的に背景OFF
                false,

                new Vector4(
                    this.configuration.GrandCrossTextColorR,
                    this.configuration.GrandCrossTextColorG,
                    this.configuration.GrandCrossTextColorB,
                    this.configuration.GrandCrossTextColorA
                ),

                // 画面固定位置表示ではフェードインを強制 0.15s
                0.15f,

                this.configuration.GrandCrossUseOutline,
                this.GetGrandCrossPresetOutlineThickness(),
                new Vector4(
                    this.configuration.GrandCrossOutlineColorR,
                    this.configuration.GrandCrossOutlineColorG,
                    this.configuration.GrandCrossOutlineColorB,
                    this.configuration.GrandCrossOutlineColorA
                ),
                this.configuration.GrandCrossFontPreset
            );
        }
        else
        {
            this.DrawOverheadText(
                this.activeGrandCrossText,
                this.activeGrandCrossUntil,
                this.configuration.GrandCrossWorldHeightOffset,
                this.configuration.GrandCrossScreenOffsetX,
                this.configuration.GrandCrossScreenOffsetY,
                this.configuration.GrandCrossFontSize,
                this.configuration.GrandCrossDrawBackground,
                new Vector4(
                    this.configuration.GrandCrossTextColorR,
                    this.configuration.GrandCrossTextColorG,
                    this.configuration.GrandCrossTextColorB,
                    this.configuration.GrandCrossTextColorA
                )
            );
        }
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
                this.OnMagicOutDeferred(cast);
                continue;
            }
        }

        if (this.magicChargeActive)
            this.UpdateMagicChargeStatusFromKefka();
    }

    private void OnMagicChargeStarted(CastEvent cast)
    {
        this.magicChargeActive = true;
        this.chargeThunderState = MagicChargeStatusState.Unknown;
        this.chargeBlizzardState = MagicChargeStatusState.Unknown;

        this.activeChargeText = string.Empty;
        this.activeChargeStartedAt = DateTime.MinValue;
        this.activeChargeUntil = DateTime.MinValue;

        this.LastMagicChargeEvent = $"マジックチャージ検出: {cast.CasterName} / 47780";
        this.LastMagicChargeResult = string.Empty;
    }

    private void OnMagicOutDeferred(CastEvent cast)
    {
        this.magicChargeActive = false;

        this.LastMagicChargeEvent = $"マジックアウト検出: {cast.CasterName} / 47781";
        this.LastMagicChargeResult = "マジックアウト後の判定仕様は保留中";

        this.SetMagicChargeScreenText(this.LastMagicChargeResult);
    }

    private void UpdateMagicChargeStatusFromKefka()
    {
        var kefka = this.FindMagicChargeKefka();
        if (kefka == null)
            return;

        var changed = false;

        foreach (var status in kefka.StatusList)
        {
            if (status.StatusId == 1485)
            {
                var state = status.Param == 0
                    ? MagicChargeStatusState.True
                    : MagicChargeStatusState.Fake;

                if (this.chargeThunderState != state)
                {
                    this.chargeThunderState = state;
                    changed = true;
                }
            }

            if (status.StatusId == 1484)
            {
                var state = status.Param == 0
                    ? MagicChargeStatusState.True
                    : MagicChargeStatusState.Fake;

                if (this.chargeBlizzardState != state)
                {
                    this.chargeBlizzardState = state;
                    changed = true;
                }
            }
        }

        if (!changed)
            return;

        this.LastMagicChargeResult =
            $"チャージ：サンダガ:{this.GetMagicChargeStatusStateDisplayName(this.chargeThunderState)} / " +
            $"チャージ：ブリザガ:{this.GetMagicChargeStatusStateDisplayName(this.chargeBlizzardState)}";

        this.SetMagicChargeScreenText(this.LastMagicChargeResult);
    }

    private IBattleChara? FindMagicChargeKefka()
    {
        foreach (var obj in this.objectTable)
        {
            if (obj is not IBattleChara battleChara)
                continue;

            var name = battleChara.Name.ToString();

            if (this.configuration.FilterByCasterName)
            {
                if (string.IsNullOrWhiteSpace(this.configuration.CasterNameKeyword))
                    continue;

                if (!name.Contains(this.configuration.CasterNameKeyword, StringComparison.OrdinalIgnoreCase))
                    continue;
            }

            foreach (var status in battleChara.StatusList)
            {
                if (status.StatusId == 1485 || status.StatusId == 1484)
                    return battleChara;
            }
        }

        return null;
    }

    private string GetMagicChargeStatusStateDisplayName(MagicChargeStatusState state)
    {
        return state switch
        {
            MagicChargeStatusState.True => "真",
            MagicChargeStatusState.Fake => "偽",
            _ => "未取得"
        };
    }

    private void SetMagicChargeScreenText(string text)
    {
        this.activeChargeText = text;
        this.activeChargeStartedAt = DateTime.Now;
        this.activeChargeUntil = DateTime.Now.AddSeconds(this.configuration.MagicChargeDisplaySeconds);
    }

    private float GetMagicChargePresetFontSize()
    {
        var baseSize = Math.Max(8.0f, this.configuration.MagicChargeFontSize);

        return this.configuration.MagicChargeFontPreset switch
        {
            1 => baseSize * 1.20f,
            2 => baseSize * 1.10f,
            3 => baseSize * 0.90f,
            _ => baseSize
        };
    }

    private float GetMagicChargePresetOutlineThickness()
    {
        var baseThickness = Math.Max(0.0f, this.configuration.MagicChargeOutlineThickness);

        return this.configuration.MagicChargeFontPreset switch
        {
            2 => baseThickness + 1.0f,
            _ => baseThickness
        };
    }

    private void SetGrandCrossText(string text, bool resetFade)
    {
        this.activeGrandCrossText = text;

        if (resetFade || this.activeGrandCrossStartedAt == DateTime.MinValue || DateTime.Now > this.activeGrandCrossUntil)
            this.activeGrandCrossStartedAt = DateTime.Now;

        this.activeGrandCrossUntil = DateTime.Now.AddSeconds(this.configuration.GrandCrossDisplaySeconds);
    }

    private float GetGrandCrossPresetFontSize()
    {
        var baseSize = Math.Max(8.0f, this.configuration.GrandCrossFontSize);

        return this.configuration.GrandCrossFontPreset switch
        {
            1 => baseSize * 1.20f,
            2 => baseSize * 1.10f,
            3 => baseSize * 0.90f,
            _ => baseSize
        };
    }

    private float GetGrandCrossPresetOutlineThickness()
    {
        var baseThickness = Math.Max(0.0f, this.configuration.GrandCrossOutlineThickness);

        return this.configuration.GrandCrossFontPreset switch
        {
            2 => baseThickness + 1.0f,
            _ => baseThickness
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
                this.activeGrandCrossStartedAt = DateTime.MinValue;
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
            this.GetActiveGrandCrossDisplayItemsSorted().Select(this.FormatGrandCrossDisplayItem)
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
        var capturedStatusIds = new List<uint>();

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

            capturedStatusIds.Add(status.StatusId);

            if (shouldLog)
                addedTexts.Add($"{displayText} / 残り {status.RemainingTime:0.0}秒");
        }

        if (capturedStatusIds.Count > 0)
            this.pendingChaosDebuffTexts.RemoveAll(item => capturedStatusIds.Contains(item.StatusId));

        if (addedTexts.Count == 0)
            return;

        this.LastGrandCrossEvent =
            $"カオスStatus保持: {string.Join(this.configuration.GrandCrossSeparator, addedTexts)}";

        this.LastGrandCrossResult = string.Join(
            this.configuration.GrandCrossSeparator,
            this.GetActiveGrandCrossDisplayItemsSorted().Select(this.FormatGrandCrossDisplayItem)
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
            this.activeGrandCrossStartedAt = DateTime.MinValue;
            this.activeGrandCrossUntil = DateTime.MinValue;
            this.LastGrandCrossResult = string.Empty;
            return;
        }

        var resultText = string.Join(
            this.configuration.GrandCrossSeparator,
            activeItems.Select(this.FormatGrandCrossDisplayItem)
        );

        this.SetGrandCrossText(resultText, resetFade: false);

        this.LastGrandCrossResult = resultText;
    }

    private string FormatGrandCrossDisplayItem(GrandCrossDisplayItem item)
    {
        return $"{item.DisplayText}｛{item.RemainingTime:0.0}s｝";
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

    private void DrawScreenText(
        string text,
        DateTime startedAt,
        DateTime until,
        float screenPositionX,
        float screenPositionY,
        float fontSize,
        bool drawBackground,
        Vector4 textColor,
        float fadeInSeconds,
        bool useOutline,
        float outlineThickness,
        Vector4 outlineColor,
        int fontPreset)
    {
        if (DateTime.Now > until)
            return;

        if (string.IsNullOrWhiteSpace(text))
            return;

        var actualStartedAt = startedAt == DateTime.MinValue
            ? DateTime.Now.AddSeconds(-fadeInSeconds)
            : startedAt;

        var elapsed = (float)(DateTime.Now - actualStartedAt).TotalSeconds;
        var alpha = fadeInSeconds <= 0.0f
            ? 1.0f
            : Math.Clamp(elapsed / fadeInSeconds, 0.0f, 1.0f);

        var finalTextColor = new Vector4(
            textColor.X,
            textColor.Y,
            textColor.Z,
            Math.Clamp(textColor.W * alpha, 0.0f, 1.0f)
        );

        var finalOutlineColor = new Vector4(
            outlineColor.X,
            outlineColor.Y,
            outlineColor.Z,
            Math.Clamp(outlineColor.W * alpha, 0.0f, 1.0f)
        );

        var drawList = ImGui.GetForegroundDrawList();

        var textSize = ImGui.CalcTextSize(text);
        var baseFontSize = MathF.Max(1.0f, ImGui.GetFontSize());
        var fontScale = fontSize / baseFontSize;
        textSize *= fontScale;

        var textPosition = new Vector2(
            MathF.Round(screenPositionX - textSize.X / 2.0f),
            MathF.Round(screenPositionY - textSize.Y / 2.0f)
        );

        if (drawBackground)
        {
            var padding = fontPreset switch
            {
                2 => new Vector2(10.0f, 6.0f),
                _ => new Vector2(8.0f, 5.0f)
            };

            var bgMin = new Vector2(
                MathF.Round(textPosition.X - padding.X),
                MathF.Round(textPosition.Y - padding.Y)
            );

            var bgMax = new Vector2(
                MathF.Round(textPosition.X + textSize.X + padding.X),
                MathF.Round(textPosition.Y + textSize.Y + padding.Y)
            );

            drawList.AddRectFilled(
                bgMin,
                bgMax,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f * alpha)),
                6.0f
            );
        }

        if (useOutline && outlineThickness > 0.0f)
        {
            var crispOutlineThickness = MathF.Max(1.0f, MathF.Round(outlineThickness));

            this.DrawTextOutline(
                drawList,
                text,
                textPosition,
                fontSize,
                finalOutlineColor,
                crispOutlineThickness
            );
        }

        drawList.AddText(
            ImGui.GetFont(),
            fontSize,
            textPosition,
            ImGui.ColorConvertFloat4ToU32(finalTextColor),
            text
        );
    }

    private void DrawTextOutline(
        ImDrawListPtr drawList,
        string text,
        Vector2 textPosition,
        float fontSize,
        Vector4 outlineColor,
        float thickness)
    {
        var color = ImGui.ColorConvertFloat4ToU32(outlineColor);

        var crispThickness = MathF.Max(1.0f, MathF.Round(thickness));

        var offsets = new[]
        {
            new Vector2(-crispThickness, 0.0f),
            new Vector2(crispThickness, 0.0f),
            new Vector2(0.0f, -crispThickness),
            new Vector2(0.0f, crispThickness),
            new Vector2(-crispThickness, -crispThickness),
            new Vector2(crispThickness, -crispThickness),
            new Vector2(-crispThickness, crispThickness),
            new Vector2(crispThickness, crispThickness),
        };

        foreach (var offset in offsets)
        {
            drawList.AddText(
                ImGui.GetFont(),
                fontSize,
                textPosition + offset,
                color,
                text
            );
        }
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

    private enum MagicChargeStatusState
    {
        Unknown = 0,
        True = 1,
        Fake = 2
    }
}