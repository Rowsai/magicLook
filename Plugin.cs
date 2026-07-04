using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using Dalamud.Hooking;
using Dalamud.Bindings.ImGui;
using Dalamud.Game.ClientState.Objects.Types;
using Dalamud.Game.Command;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace MagicLook;

public sealed class Plugin : IDalamudPlugin
{
    private const string CommandName = "/magiclook";
    private const uint MagicLookNazoNazoMagicActionId = 47764;
    private const double MagicLookNazoNazoMagicWindowSeconds = 8.0;

    private readonly IDalamudPluginInterface pluginInterface;
    private readonly ICommandManager commandManager;
    private readonly IClientState clientState;
    private readonly IFramework framework;
    private readonly IGameGui gameGui;
    private readonly IObjectTable objectTable;
    private readonly IGameInteropProvider gameInteropProvider;
    private readonly IPluginLog log;

    private readonly WindowSystem windowSystem = new("magicLook");
    private readonly ConfigWindow configWindow;

    private readonly Configuration configuration;

    private readonly Hook<ActorVfxCreateDelegate>?[] actorVfxCreateHooks = new Hook<ActorVfxCreateDelegate>?[5];
    private Hook<ProcessObjectEffectDelegate>? processObjectEffectHook;
    private int actorVfxHookSuccessCount;
    private long actorVfxDetourCallCount;
    private long processObjectEffectDetourCallCount;
    private DateTime lastActorVfxDetourLogAt = DateTime.MinValue;
    private DateTime lastActorVfxPathFailLogAt = DateTime.MinValue;
    private DateTime lastObjectEffectLogAt = DateTime.MinValue;
    private const string MagicLookVfxFakeFiregaTriggerPath = "vfx/lockon/eff/m0462trg_c01c.avfx";
    private const string MagicLookVfxTrueFiregaTriggerPath = "vfx/lockon/eff/m0462trg_c02c.avfx";
    private const string MagicLookVfxSpreadPath = "vfx/lockon/eff/m0462trg_a0c.avfx";
    private const string MagicLookVfxStackPath = "vfx/lockon/eff/m0462trg_b0c.avfx";

    private readonly List<RecentMagicLookActionEvent> recentMagicLookActionEvents = new();
    private readonly List<RecentVfxEvent> recentVfxEvents = new();
    private readonly List<RecentObjectEffectFallbackEvent> recentObjectEffectFallbackEvents = new();
    private readonly Dictionary<nint, DateTime> recentTrueFireObjectEffectTriggers = new();

    private DateTime magicLookNazoNazoMagicWindowUntil = DateTime.MinValue;
    private string magicLookNazoNazoMagicCasterName = string.Empty;
    private bool magicLookPhase1DisplayedInCurrentNazoNazoWindow;

    private string activeLockText = string.Empty;
    private DateTime activeLockStartedAt = DateTime.MinValue;
    private DateTime activeLockUntil = DateTime.MinValue;

    private string activeChargeText = string.Empty;
    private DateTime activeChargeStartedAt = DateTime.MinValue;
    private DateTime activeChargeUntil = DateTime.MinValue;

    private string activeGrandCrossText = string.Empty;
    private DateTime activeGrandCrossStartedAt = DateTime.MinValue;
    private DateTime activeGrandCrossUntil = DateTime.MinValue;
    private int activeGrandCrossDisplayMode = -1;

    private readonly List<string> magicLookLogLines = new();
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

    private bool chaosCasting;
    private uint currentChaosActionId;
    private uint currentChaosInternalParam;
    private string currentChaosCasterName = string.Empty;
    private bool wasLocalPlayerDead;

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
        IGameInteropProvider gameInteropProvider,
        IPluginLog log)
    {
        this.pluginInterface = pluginInterface;
        this.commandManager = commandManager;
        this.clientState = clientState;
        this.framework = framework;
        this.gameGui = gameGui;
        this.objectTable = objectTable;
        this.gameInteropProvider = gameInteropProvider;
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

        this.InitializeActorVfxHook();
        this.InitializeObjectEffectHook();
    }

    public void Dispose()
    {
        this.framework.Update -= this.OnFrameworkUpdate;
        this.pluginInterface.UiBuilder.Draw -= this.Draw;
        this.pluginInterface.UiBuilder.OpenConfigUi -= this.OpenConfigUi;
        this.pluginInterface.UiBuilder.OpenMainUi -= this.OpenConfigUi;

        foreach (var hook in this.actorVfxCreateHooks)
        {
            hook?.Disable();
            hook?.Dispose();
        }

        this.processObjectEffectHook?.Disable();
        this.processObjectEffectHook?.Dispose();

        this.commandManager.RemoveHandler(CommandName);
        this.windowSystem.RemoveAllWindows();
    }

    internal void SaveConfig()
    {
        this.configuration.Save(this.pluginInterface);
    }

    internal IReadOnlyList<string> GetMagicLookLogLines() => this.magicLookLogLines;

    internal string GetMagicLookLogText()
    {
        if (this.magicLookLogLines.Count == 0)
            return "・・・\n・・・\n・・・";

        return string.Join("\n", this.magicLookLogLines);
    }

    internal void TestLockText(string text)
    {
        this.SetMagicLookText(text);

        this.LastCasterName = "テスト";
        this.LastActionId = 0;
        this.LastMatchedLabel = "magicLook テスト表示";
        this.AddMagicLookLog("テスト", 0, text);
    }

    internal void TestChargeText(string text)
    {
        this.SetMagicChargeText(text, resetFade: true);

        this.LastMagicChargeEvent = "テスト";
        this.LastMagicChargeResult = text;
    }

    internal void TestGrandCrossText(string text)
    {
        this.SetGrandCrossText(text, resetFade: true);

        this.LastGrandCrossEvent = "表示位置テスト";
        this.LastGrandCrossResult = text;
    }

    internal void TestGrandCrossConfiguredTrueText()
    {
        var text = string.Join(
            this.configuration.GrandCrossSeparator,
            new[]
            {
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossAllaganFieldTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossLivingWoundTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossCurseShriekTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossWaterCompressionTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossAccelerationBombTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossFireTrueText),
                this.FormatGrandCrossTestDisplayText(this.configuration.GrandCrossTsunamiTrueText),
            }.Where(item => !string.IsNullOrWhiteSpace(item))
        );

        this.SetGrandCrossText(text, resetFade: true);

        this.LastGrandCrossEvent = "表示テスト";
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

        this.chaosCasting = false;
        this.currentChaosActionId = 0;
        this.currentChaosInternalParam = 0;
        this.currentChaosCasterName = string.Empty;

        this.grandCrossHeldDebuffs.Clear();
        this.pendingChaosDebuffTexts.Clear();
        this.grandCrossStatusSnapshot.Clear();
        this.grandCrossCapturedCastCount = 0;

        this.activeGrandCrossText = string.Empty;
        this.activeGrandCrossStartedAt = DateTime.MinValue;
        this.activeGrandCrossUntil = DateTime.MinValue;
        this.activeGrandCrossDisplayMode = -1;

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

        result.Add("●グランドクロス");
        result.Add($"・詠唱中: {(this.grandCrossCasting ? "はい" : "いいえ")}");
        result.Add($"・ステータス取得待ち: {(this.pendingGrandCrossStatusCapture ? "はい" : "いいえ")}");
        result.Add("・デバフ取得対象: 自分のみ");
        result.Add($"・グランドクロス真偽: {this.GetGrandCrossTruthDisplayName()}");
        result.Add($"・グランドクロス取得回数: {this.grandCrossCapturedCastCount}/3");
        result.Add($"・グランドクロスデバフ保持数: {this.grandCrossHeldDebuffs.Count}");
        result.Add($"・保持中のデバフ情報: {this.FormatHeldDebuffSummary(activeItems)}");

        result.Add("●ほのお or つなみ");
        result.Add($"・カオス待機数: {this.pendingChaosDebuffTexts.Count}");
        result.Add($"・現在表示中のカオスStatus数: {this.GetActiveGrandCrossHeldDebuffsSorted().Count(item => item.StatusId == 5547 || item.StatusId == 5548)}");
        result.Add($"・保持中のデバフ情報: {this.FormatChaosHeldDebuffSummary()}");

        result.Add($"Phase4保持中デバフ情報: {this.FormatHeldDebuffSummary(activeItems)}");

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

        return result;
    }

    private void InitializeObjectEffectHook()
    {
        this.AddMagicLookLog("ObjectEffect Hook", 0, "ObjectEffect hook 初期化開始");

        try
        {
            var address = EventObject.Addresses.PlayAnimation.Value;
            if (address == nint.Zero)
            {
                this.AddMagicLookLog("ObjectEffect Hook", 0, "PlayAnimation address is zero / ObjectEffect取得は動作しません");
                return;
            }

            this.processObjectEffectHook = this.gameInteropProvider.HookFromAddress<ProcessObjectEffectDelegate>(
                address,
                this.ProcessObjectEffectDetour);

            this.processObjectEffectHook.Enable();
            this.AddMagicLookLog("ObjectEffect Hook", 0, $"ObjectEffect hook initialized: {FormatPointer(address)}");
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "magicLook ObjectEffect hook initialization failed.");
            this.AddMagicLookLog("ObjectEffect Hook", 0, $"ObjectEffect hook failed: {ex.GetType().Name} / {ex.Message}");
        }
    }

    private void ProcessObjectEffectDetour(nint thisPtr, uint entityId, uint actionId, ulong a4)
    {
        try
        {
            this.processObjectEffectDetourCallCount++;

            var now = DateTime.Now;
            var shouldLog = this.processObjectEffectDetourCallCount <= 80 ||
                            (now - this.lastObjectEffectLogAt).TotalSeconds >= 1.0;

            if (shouldLog)
            {
                this.lastObjectEffectLogAt = now;
                this.AddMagicLookLog(
                    "ObjectEffect",
                    actionId,
                    $"Detour called #{this.processObjectEffectDetourCallCount} / this:{FormatPointer(thisPtr)} data1:{entityId} data2:{actionId} a4:{a4}");
            }

            this.TrackMagicLookObjectEffectFallback(thisPtr, entityId, actionId, a4);

            // Splatoonのログタブが拾っている ObjectEffect 系の情報です。
            // ActorVFX Detourが呼ばれない環境では、暫定的に data2 を Phase1 のVFX相当として扱います。
            // 既知ログ: data2=2 => c02c候補, data2=32 => a0c候補, data2=128 => b0c候補。
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "ObjectEffect detour failed.");
            this.AddMagicLookLog("ObjectEffect", 0, $"detour failed: {ex.GetType().Name} / {ex.Message}");
        }

        this.processObjectEffectHook?.Original(thisPtr, entityId, actionId, a4);
    }

    private void InitializeActorVfxHook()
    {
        this.AddMagicLookLog("VFX Hook", 0, "ActorVFX hook 初期化開始");
        this.AddMagicLookLog("VFX Hook", 0, "detour呼び出し確認ログ + 複数hook候補同時監視モードで起動");

        var signatures = new[]
        {
            // Actor VFX create 候補。signature だけ一致しても、実際に呼ばれない近縁関数を拾うことがあるため、
            // 成功した候補を1つで止めず、同時に保持して Detour called が出る候補を探します。
            "40 53 48 83 EC ?? 48 8B D9 48 8B 0D ?? ?? ?? ?? 48 85 C9 74 ?? 48 8B 01 FF 50 ?? 84 C0 74 ?? 33 D2 48 8B CB E8 ?? ?? ?? ?? 48 8B C3 48 83 C4 ?? 5B C3 33 C0 48 83 C4 ?? 5B C3",
            "40 53 48 83 EC ?? 48 8B D9 48 8B 0D ?? ?? ?? ?? 48 85 C9 74",
            "40 53 55 56 57 41 54 41 55 41 56 48 81 EC ?? ?? ?? ?? 48 8B 05 ?? ?? ?? ?? 48 33 C4 48 89 84 24 ?? ?? ?? ?? 4D 8B E0",
            "40 53 55 56 57 41 56 48 81 EC ?? ?? ?? ?? 48 8B E9",
            "40 53 48 83 EC ?? 48 8B D9 48 8B 0D ?? ?? ?? ?? 48 85 C9 74"
        };

        var detours = new ActorVfxCreateDelegate[]
        {
            this.ActorVfxCreateDetour0,
            this.ActorVfxCreateDetour1,
            this.ActorVfxCreateDetour2,
            this.ActorVfxCreateDetour3,
            this.ActorVfxCreateDetour4,
        };

        var seenSignatures = new HashSet<string>();

        for (var i = 0; i < signatures.Length; i++)
        {
            var signature = signatures[i];

            if (!seenSignatures.Add(signature))
            {
                this.AddMagicLookLog("VFX Hook", 0, $"signature {i + 1}/{signatures.Length} skipped: duplicate");
                continue;
            }

            try
            {
                this.AddMagicLookLog("VFX Hook", 0, $"signature {i + 1}/{signatures.Length} 初期化試行");

                var hook = this.gameInteropProvider.HookFromSignature<ActorVfxCreateDelegate>(
                    signature,
                    detours[i]);

                hook.Enable();
                this.actorVfxCreateHooks[i] = hook;
                this.actorVfxHookSuccessCount++;

                var message = $"ActorVFX hook initialized: signature {i + 1}";
                this.log.Information($"magicLook {message}: {signature}");
                this.AddMagicLookLog("VFX Hook", 0, message);
            }
            catch (Exception ex)
            {
                this.log.Debug(ex, $"magicLook ActorVFX hook signature failed: {signature}");
                this.AddMagicLookLog("VFX Hook", 0, $"signature {i + 1} failed: {ex.GetType().Name}");
            }
        }

        if (this.actorVfxHookSuccessCount == 0)
        {
            const string failedMessage = "ActorVFX hook could not be initialized. Phase1 Firega VFX判定は動作しません。";
            this.log.Warning($"magicLook {failedMessage}");
            this.AddMagicLookLog("VFX Hook", 0, failedMessage);
            return;
        }

        this.AddMagicLookLog("VFX Hook", 0, $"ActorVFX hook initialized count: {this.actorVfxHookSuccessCount}");
        this.AddMagicLookLog("VFX Hook", 0, "この後、VFX発生時に Detour called #n / signature:x が出る候補が実際に呼ばれているhookです。");
    }

    private nint ActorVfxCreateDetour0(nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        return this.ActorVfxCreateDetourCommon(0, pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private nint ActorVfxCreateDetour1(nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        return this.ActorVfxCreateDetourCommon(1, pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private nint ActorVfxCreateDetour2(nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        return this.ActorVfxCreateDetourCommon(2, pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private nint ActorVfxCreateDetour3(nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        return this.ActorVfxCreateDetourCommon(3, pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private nint ActorVfxCreateDetour4(nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        return this.ActorVfxCreateDetourCommon(4, pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private nint ActorVfxCreateDetourCommon(int hookIndex, nint pathPtr, nint targetActor, nint sourceActor, float a4, byte a5, ushort a6, byte a7)
    {
        try
        {
            this.actorVfxDetourCallCount++;

            var now = DateTime.Now;
            var shouldLogCall = this.actorVfxDetourCallCount <= 60 ||
                                (now - this.lastActorVfxDetourLogAt).TotalSeconds >= 3.0;

            if (shouldLogCall)
            {
                this.lastActorVfxDetourLogAt = now;
                this.AddMagicLookLog(
                    "VFX Hook",
                    0,
                    $"Detour called #{this.actorVfxDetourCallCount} / signature:{hookIndex + 1} / arg1:{FormatPointer(pathPtr)} arg2:{FormatPointer(targetActor)} arg3:{FormatPointer(sourceActor)}");
            }

            var path = this.ResolveVfxPathFromDetourArguments(
                shouldLogCall,
                ("arg1/path", pathPtr),
                ("arg2/target", targetActor),
                ("arg3/source", sourceActor));

            if (!string.IsNullOrWhiteSpace(path))
            {
                this.OnActorVfxCreated(path);
            }
            else if (shouldLogCall || (now - this.lastActorVfxPathFailLogAt).TotalSeconds >= 3.0)
            {
                this.lastActorVfxPathFailLogAt = now;
                this.AddMagicLookLog("VFX Hook", 0, $"path read failed / signature:{hookIndex + 1} / arg1-arg3 にVFXパス候補なし");
            }
        }
        catch (Exception ex)
        {
            this.log.Debug(ex, "ActorVFX detour failed.");
            this.AddMagicLookLog("VFX Hook", 0, $"detour failed / signature:{hookIndex + 1}: {ex.GetType().Name} / {ex.Message}");
        }

        var hook = hookIndex >= 0 && hookIndex < this.actorVfxCreateHooks.Length
            ? this.actorVfxCreateHooks[hookIndex]
            : null;

        if (hook == null)
        {
            this.AddMagicLookLog("VFX Hook", 0, $"original call skipped / signature:{hookIndex + 1} hook is null");
            return nint.Zero;
        }

        return hook.Original(pathPtr, targetActor, sourceActor, a4, a5, a6, a7);
    }

    private string ResolveVfxPathFromDetourArguments(bool verboseLog, params (string Name, nint Pointer)[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var direct = this.TryReadPossibleVfxPath(candidate.Pointer);
            if (!string.IsNullOrWhiteSpace(direct))
            {
                if (verboseLog)
                    this.AddMagicLookLog("VFX Hook", 0, $"path candidate direct {candidate.Name}: {NormalizeVfxPath(direct)}");

                return direct;
            }

            // 引数そのものではなく、引数が指す構造体内に char* が入っているケースの保険。
            // 読み取り失敗は握りつぶして次候補へ進む。
            for (var offset = 0; offset <= 0x40; offset += 0x8)
            {
                var nestedPointer = this.TryReadPointer(candidate.Pointer, offset);
                if (nestedPointer == nint.Zero)
                    continue;

                var nested = this.TryReadPossibleVfxPath(nestedPointer);
                if (string.IsNullOrWhiteSpace(nested))
                    continue;

                if (verboseLog)
                {
                    this.AddMagicLookLog(
                        "VFX Hook",
                        0,
                        $"path candidate nested {candidate.Name}+0x{offset:X}: {NormalizeVfxPath(nested)}");
                }

                return nested;
            }
        }

        return string.Empty;
    }

    private string TryReadPossibleVfxPath(nint pointer)
    {
        if (pointer == nint.Zero)
            return string.Empty;

        try
        {
            var utf8 = Marshal.PtrToStringUTF8(pointer, 512);
            if (LooksLikeVfxPath(utf8))
                return utf8 ?? string.Empty;
        }
        catch
        {
            // ignored
        }

        try
        {
            var ansi = Marshal.PtrToStringAnsi(pointer, 512);
            if (LooksLikeVfxPath(ansi))
                return ansi ?? string.Empty;
        }
        catch
        {
            // ignored
        }

        return string.Empty;
    }

    private nint TryReadPointer(nint basePointer, int offset)
    {
        if (basePointer == nint.Zero)
            return nint.Zero;

        try
        {
            return Marshal.ReadIntPtr(basePointer, offset);
        }
        catch
        {
            return nint.Zero;
        }
    }

    private static bool LooksLikeVfxPath(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var normalized = NormalizeVfxPath(value);

        return normalized.Contains("vfx/", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains(".avfx", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("lockon/eff", StringComparison.OrdinalIgnoreCase) ||
               normalized.Contains("m0462trg", StringComparison.OrdinalIgnoreCase);
    }

    private static string FormatPointer(nint pointer)
    {
        return $"0x{pointer.ToInt64():X}";
    }

    private void OnActorVfxCreated(string path)
    {
        var normalized = NormalizeVfxPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var isWatched = this.IsMagicLookWatchedVfx(normalized);
        var isRelated = normalized.Contains("m0462trg", StringComparison.OrdinalIgnoreCase) ||
                        normalized.Contains("vfx/lockon/eff", StringComparison.OrdinalIgnoreCase);

        if (!isWatched)
        {
            // デバッグ用。全VFXを出すとログが埋まるため、関連しそうな lockon / m0462 系だけ表示します。
            if (isRelated)
                this.AddMagicLookLog("VFX raw", 0, normalized);

            return;
        }

        this.AddRecentMagicLookVfxEvent(normalized, "ActorVFX");

        this.AddMagicLookLog("VFX hit", 0, normalized);
    }

    private bool IsMagicLookWatchedVfx(string normalizedPath)
    {
        return normalizedPath is
            MagicLookVfxFakeFiregaTriggerPath or
            MagicLookVfxTrueFiregaTriggerPath or
            MagicLookVfxSpreadPath or
            MagicLookVfxStackPath;
    }

    private void TrackMagicLookObjectEffectFallback(nint thisPtr, uint entityId, uint actionId, ulong a4)
    {
        // 現状の環境では ActorVFX Detour が呼ばれていないため、
        // ObjectEffect.PlayAnimation の data2(actionId引数) を Phase1 VFX の代替情報として一時採用します。
        // 重要: data2:32/128 は他人のロックオン演出も流れてくるため、data2:2(c02c相当) と同じ thisPtr のものだけ採用します。
        var now = DateTime.Now;

        if (actionId == 2)
        {
            this.recentTrueFireObjectEffectTriggers[thisPtr] = now;
            this.AddObjectEffectFallbackPseudoVfx(thisPtr, entityId, actionId, a4, MagicLookVfxTrueFiregaTriggerPath, now, "c02c trigger");
            return;
        }

        var pseudoVfxPath = actionId switch
        {
            32 => MagicLookVfxSpreadPath,
            128 => MagicLookVfxStackPath,
            _ => string.Empty
        };

        if (string.IsNullOrWhiteSpace(pseudoVfxPath))
            return;

        if (!this.recentTrueFireObjectEffectTriggers.TryGetValue(thisPtr, out var triggerAt) ||
            (now - triggerAt).TotalSeconds > 7.0)
        {
            this.AddMagicLookLog(
                "ObjectEffectFallback",
                47764,
                $"ignored unrelated data1:{entityId} data2:{actionId} a4:{a4} this:{FormatPointer(thisPtr)} => pseudo:{pseudoVfxPath} / same-thisPtr c02c trigger not found");
            return;
        }

        this.AddObjectEffectFallbackPseudoVfx(thisPtr, entityId, actionId, a4, pseudoVfxPath, now, "same-thisPtr matched");
    }

    private void AddObjectEffectFallbackPseudoVfx(nint thisPtr, uint entityId, uint actionId, ulong a4, string pseudoVfxPath, DateTime now, string reason)
    {
        if (this.recentObjectEffectFallbackEvents.Any(item =>
                item.ThisPtr == thisPtr &&
                item.Data1 == entityId &&
                item.Data2 == actionId &&
                item.A4 == a4 &&
                (now - item.OccurredAt).TotalMilliseconds < 300))
        {
            return;
        }

        this.recentObjectEffectFallbackEvents.Add(new RecentObjectEffectFallbackEvent
        {
            ThisPtr = thisPtr,
            Data1 = entityId,
            Data2 = actionId,
            A4 = a4,
            PseudoVfxPath = pseudoVfxPath,
            OccurredAt = now
        });

        this.AddRecentMagicLookVfxEvent(pseudoVfxPath, $"ObjectEffect data2:{actionId}");

        this.AddMagicLookLog(
            "ObjectEffectFallback",
            47764,
            $"data1:{entityId} data2:{actionId} a4:{a4} this:{FormatPointer(thisPtr)} => pseudo:{pseudoVfxPath} / {reason}");
    }

    private void AddRecentMagicLookVfxEvent(string path, string source)
    {
        var normalized = NormalizeVfxPath(path);
        if (string.IsNullOrWhiteSpace(normalized))
            return;

        var now = DateTime.Now;

        if (this.recentVfxEvents.Any(item =>
                item.Path == normalized &&
                item.Source == source &&
                (now - item.OccurredAt).TotalMilliseconds < 300))
        {
            return;
        }

        this.recentVfxEvents.Add(new RecentVfxEvent
        {
            Path = normalized,
            Source = source,
            OccurredAt = now
        });
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

        this.ClearGrandCrossLikeNonGrandCrossTexts();

        if (this.configuration.MagicLookDisplayMode == 1)
        {
            this.DrawScreenText(
                this.activeLockText,
                this.activeLockStartedAt,
                this.activeLockUntil,
                this.configuration.MagicLookFixedScreenPositionX,
                this.configuration.MagicLookFixedScreenPositionY,
                this.configuration.MagicLookFontSize,
                this.configuration.MagicLookDrawBackground,
                new Vector4(1.0f, 0.95f, 0.25f, 1.0f),
                this.configuration.MagicLookFadeInSeconds,
                this.configuration.MagicLookUseOutline,
                this.configuration.MagicLookOutlineThickness,
                new Vector4(
                    this.configuration.MagicLookOutlineColorR,
                    this.configuration.MagicLookOutlineColorG,
                    this.configuration.MagicLookOutlineColorB,
                    this.configuration.MagicLookOutlineColorA),
                0);
        }
        else
        {
            this.DrawOverheadText(
                this.activeLockText,
                this.activeLockUntil,
                this.configuration.MagicLookWorldHeightOffset,
                this.configuration.MagicLookScreenOffsetX,
                this.configuration.MagicLookScreenOffsetY,
                this.configuration.MagicLookFontSize,
                this.configuration.MagicLookDrawBackground,
                new Vector4(1.0f, 0.95f, 0.25f, 1.0f));
        }

        if (this.configuration.MagicChargeDisplayMode == 0)
        {
            this.DrawOverheadText(
                this.activeChargeText,
                this.activeChargeUntil,
                this.configuration.MagicChargeWorldHeightOffset,
                this.configuration.MagicChargeScreenOffsetX,
                this.configuration.MagicChargeScreenOffsetY,
                this.configuration.MagicChargeFontSize,
                this.configuration.MagicChargeDrawBackground,
                new Vector4(
                    this.configuration.MagicChargeTextColorR,
                    this.configuration.MagicChargeTextColorG,
                    this.configuration.MagicChargeTextColorB,
                    this.configuration.MagicChargeTextColorA));
        }
        else
        {
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
                    this.configuration.MagicChargeTextColorA),
                this.configuration.MagicChargeFadeInSeconds,
                this.configuration.MagicChargeUseOutline,
                this.GetMagicChargePresetOutlineThickness(),
                new Vector4(
                    this.configuration.MagicChargeOutlineColorR,
                    this.configuration.MagicChargeOutlineColorG,
                    this.configuration.MagicChargeOutlineColorB,
                    this.configuration.MagicChargeOutlineColorA),
                this.configuration.MagicChargeFontPreset);
        }

        this.DrawGrandCrossTextOnlySelectedMode();
    }

    private void DrawGrandCrossTextOnlySelectedMode()
    {
        if (string.IsNullOrWhiteSpace(this.activeGrandCrossText))
            return;

        if (DateTime.Now > this.activeGrandCrossUntil)
            return;

        var displayMode = this.GetGrandCrossDisplayMode();

        if (this.activeGrandCrossDisplayMode != displayMode)
            this.activeGrandCrossDisplayMode = displayMode;

        if (displayMode == 1)
        {
            this.DrawScreenText(
                this.activeGrandCrossText,
                this.activeGrandCrossStartedAt,
                this.activeGrandCrossUntil,
                this.configuration.GrandCrossScreenPositionX,
                this.configuration.GrandCrossScreenPositionY,
                this.GetGrandCrossPresetFontSize(),
                false,
                new Vector4(
                    this.configuration.GrandCrossTextColorR,
                    this.configuration.GrandCrossTextColorG,
                    this.configuration.GrandCrossTextColorB,
                    this.configuration.GrandCrossTextColorA),
                0.15f,
                this.configuration.GrandCrossUseOutline,
                this.GetGrandCrossPresetOutlineThickness(),
                new Vector4(
                    this.configuration.GrandCrossOutlineColorR,
                    this.configuration.GrandCrossOutlineColorG,
                    this.configuration.GrandCrossOutlineColorB,
                    this.configuration.GrandCrossOutlineColorA),
                this.configuration.GrandCrossFontPreset);

            return;
        }

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
                this.configuration.GrandCrossTextColorA));
    }

    private int GetGrandCrossDisplayMode()
    {
        return this.configuration.GrandCrossDisplayMode == 1 ? 1 : 0;
    }

    private void ClearGrandCrossLikeNonGrandCrossTexts()
    {
        if (this.IsGrandCrossStyleText(this.activeLockText))
        {
            this.activeLockText = string.Empty;
            this.activeLockStartedAt = DateTime.MinValue;
            this.activeLockUntil = DateTime.MinValue;
        }

        if (this.IsGrandCrossStyleText(this.activeChargeText))
        {
            this.activeChargeText = string.Empty;
            this.activeChargeStartedAt = DateTime.MinValue;
            this.activeChargeUntil = DateTime.MinValue;
        }
    }

    private bool IsGrandCrossStyleText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return false;

        if (text.Contains("｛", StringComparison.Ordinal) && text.Contains("s｝", StringComparison.Ordinal))
            return true;

        if (text.Contains("{", StringComparison.Ordinal) && text.Contains("s}", StringComparison.Ordinal))
            return true;

        return false;
    }

    private bool ResetGrandCrossStateOnLocalPlayerDeath()
    {
        var localPlayer = this.GetLocalPlayerAsBattleChara();
        var isDead = localPlayer != null && localPlayer.CurrentHp <= 0;

        if (isDead && !this.wasLocalPlayerDead)
        {
            this.ResetGrandCrossState();
            this.previousCastKeys.Clear();
            this.LastGrandCrossEvent = "ローカルプレイヤー戦闘不能検出によりGrandCross状態をリセット";
        }

        this.wasLocalPlayerDead = isDead;
        return isDead;
    }

    private void OnFrameworkUpdate(IFramework _)
    {
        if (!this.configuration.Enabled)
            return;

        if (this.configuration.UseTerritoryFilter &&
            this.clientState.TerritoryType != this.configuration.TerritoryType)
        {
            this.previousCastKeys.Clear();
            this.ResetMagicLookPhase1TriggerState();
            this.ResetGrandCrossState();
            return;
        }

        if (this.ResetGrandCrossStateOnLocalPlayerDeath())
            return;

        var currentCasts = new List<CastEvent>();
        var newlyStartedCasts = new List<CastEvent>();

        this.CollectCastEvents(currentCasts, newlyStartedCasts);
        this.TrackMagicLookNazoNazoMagicCast(currentCasts, newlyStartedCasts);
        this.TrackMagicLookActionEvents(currentCasts);
        this.CleanupRecentMagicLookEvents();

        this.ProcessMagicLook(currentCasts);
        this.ProcessMagicCharge(newlyStartedCasts);
        this.ProcessGrandCross(currentCasts, newlyStartedCasts);
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

    private void TrackMagicLookNazoNazoMagicCast(List<CastEvent> currentCasts, List<CastEvent> newlyStartedCasts)
    {
        var newlyStarted = newlyStartedCasts.FirstOrDefault(this.IsMagicLookNazoNazoMagicCast);
        var current = newlyStarted.Caster != null
            ? newlyStarted
            : currentCasts.FirstOrDefault(this.IsMagicLookNazoNazoMagicCast);

        if (current.Caster == null)
            return;

        var now = DateTime.Now;
        var isNewWindow = newlyStarted.Caster != null || now > this.magicLookNazoNazoMagicWindowUntil;

        if (isNewWindow)
        {
            // 重要:
            // ObjectEffectFallback は、なぞなぞマジック(47764)の詠唱検出より先に
            // c02c相当(data2:2)が流れてくることがあります。
            // ここで recentVfxEvents / recentTrueFireObjectEffectTriggers を消すと、
            // 後続の a0c/b0c 相当(data2:32/128)が「same-thisPtr c02c trigger not found」になり、
            // Phase1テキストが表示されません。
            // そのため、新しいなぞなぞマジックウィンドウ開始時は表示済みフラグだけリセットし、
            // 直前数秒のVFX/ObjectEffectFallback履歴は CleanupRecentMagicLookEvents の期限管理に任せます。
            this.magicLookPhase1DisplayedInCurrentNazoNazoWindow = false;

            this.AddMagicLookLog(
                current.CasterName,
                MagicLookNazoNazoMagicActionId,
                "なぞなぞマジック詠唱検出 / Phase1判定待機開始 / 直前VFX履歴保持");
        }

        this.magicLookNazoNazoMagicWindowUntil = now.AddSeconds(MagicLookNazoNazoMagicWindowSeconds);
        this.magicLookNazoNazoMagicCasterName = current.CasterName;
    }

    private bool IsMagicLookNazoNazoMagicCast(CastEvent cast)
    {
        if (cast.Caster == null)
            return false;

        if (cast.ActionId != MagicLookNazoNazoMagicActionId)
            return false;

        if (!this.configuration.FilterByCasterName)
            return true;

        if (string.IsNullOrWhiteSpace(this.configuration.CasterNameKeyword))
            return true;

        return cast.CasterName.Contains(this.configuration.CasterNameKeyword, StringComparison.OrdinalIgnoreCase);
    }

    private bool IsMagicLookNazoNazoMagicWindowActive()
    {
        return DateTime.Now <= this.magicLookNazoNazoMagicWindowUntil;
    }

    private void ResetMagicLookPhase1TriggerState()
    {
        this.magicLookNazoNazoMagicWindowUntil = DateTime.MinValue;
        this.magicLookNazoNazoMagicCasterName = string.Empty;
        this.magicLookPhase1DisplayedInCurrentNazoNazoWindow = false;
        this.recentMagicLookActionEvents.Clear();
        this.recentVfxEvents.Clear();
        this.recentObjectEffectFallbackEvents.Clear();
        this.recentTrueFireObjectEffectTriggers.Clear();
    }

    private void ProcessMagicLook(List<CastEvent> currentCasts)
    {
        if (!this.configuration.MagicLookEnabled)
            return;

        var phase1Text = this.GetMagicLookPhase1TextFromRecentEvents();
        if (!string.IsNullOrWhiteSpace(phase1Text))
        {
            this.SetMagicLookText(phase1Text);

            var latestAction = this.recentMagicLookActionEvents
                .OrderByDescending(item => item.OccurredAt)
                .FirstOrDefault();

            this.LastCasterName = !string.IsNullOrWhiteSpace(this.magicLookNazoNazoMagicCasterName)
                ? this.magicLookNazoNazoMagicCasterName
                : latestAction?.CasterName ?? "VFX判定";
            this.LastActionId = MagicLookNazoNazoMagicActionId;
            this.LastMatchedLabel = phase1Text;

            if (!this.magicLookPhase1DisplayedInCurrentNazoNazoWindow)
            {
                this.magicLookPhase1DisplayedInCurrentNazoNazoWindow = true;
                this.AddMagicLookLog(this.LastCasterName, this.LastActionId, $"なぞなぞマジック基準 Phase1判定: {phase1Text}");
            }

            return;
        }

        var matches = this.FindMagicLookMatches(currentCasts);
        if (matches.Count == 0)
            return;

        var comboText = this.GetMagicLookComboText(matches);

        // Phase4 のブリザガ + サンダガ合成は、Phase1の単体表示抑制より先に表示します。
        // 以前は抑制判定を先に行っていたため、ブリザガ/サンダガの組み合わせ表示まで止まっていました。
        if (!string.IsNullOrWhiteSpace(comboText))
        {
            this.SetMagicLookText(comboText);

            var latestCombo = matches[^1];
            this.LastCasterName = string.Join(" / ", matches.Select(match => match.CasterName).Distinct());
            this.LastActionId = latestCombo.ActionId;
            this.LastMatchedLabel = comboText;
            this.AddMagicLookLog(this.LastCasterName, this.LastActionId, this.LastMatchedLabel);
            return;
        }

        // Phase1ではブリザガ/サンダガ系ActionId単体の表示（例: 踏む＜扇＞、真サンダガ）を先に出さず、
        // c01c/c02c + a0c/b0c または ブリザガ+サンダガ のPhase1判定が揃うまで待機します。
        if (this.ShouldSuppressMagicLookSingleActionTextWhileWaitingPhase1(currentCasts))
            return;

        var displayText = string.Join("\n", matches.Select(match => match.Text));

        this.SetMagicLookText(displayText);

        var latest = matches[^1];

        this.LastCasterName = string.Join(" / ", matches.Select(match => match.CasterName).Distinct());
        this.LastActionId = latest.ActionId;
        this.LastMatchedLabel = string.Join(" / ", matches.Select(match => match.Label).Distinct());

        this.AddMagicLookLog(this.LastCasterName, this.LastActionId, this.LastMatchedLabel);
    }

    private bool ShouldSuppressMagicLookSingleActionTextWhileWaitingPhase1(List<CastEvent> currentCasts)
    {
        if (!this.IsMagicLookNazoNazoMagicWindowActive())
            return false;

        var hasPhase1Cast = currentCasts.Any(cast => cast.ActionId is 47768 or 47771 or 47774 or 47775 or 47776 or 47777);

        if (!hasPhase1Cast)
            return false;

        // なぞなぞマジック詠唱中のPhase1は、ブリザガ/サンダガ単体やPhase4風の合成を先に出さず、
        // c01c/c02c + a0c/b0c のVFX/ObjectEffectFallback判定が揃うまで待機します。
        return this.configuration.MagicLookPhaseTextSettings.Any(setting =>
            setting.Enabled &&
            string.Equals(setting.Phase, "Phase1", StringComparison.OrdinalIgnoreCase) &&
            setting.RequiredVfxPaths.Count > 0);
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
                    setting.Text));
            }
        }

        return matches
            .GroupBy(match => match.Text)
            .Select(group => group.First())
            .ToList();
    }

    private string GetMagicLookComboText(List<CastMatch> matches)
    {
        var actionIds = matches.Select(match => match.ActionId).ToHashSet();

        var phaseMatch = this.configuration.MagicLookPhaseTextSettings.FirstOrDefault(setting =>
            setting.Enabled &&
            string.Equals(setting.Phase, "Phase4", StringComparison.OrdinalIgnoreCase) &&
            setting.RequiredVfxPaths.Count == 0 &&
            this.IsMagicLookPhaseActionMatch(setting, actionIds));

        if (phaseMatch != null)
            return phaseMatch.Text;

        // Phase1 のファイガ/頭割り/散開は VFX パス比較が必要です。
        // 現在の描画ロジックでは ActionId だけで誤判定しないよう、RequiredVfxPaths を持つ設定は
        // VFX収集ロジック側から参照するまでここではスキップします。

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

    private void TrackMagicLookActionEvents(List<CastEvent> currentCasts)
    {
        foreach (var cast in currentCasts)
        {
            if (cast.ActionId is not (47768 or 47771 or 47774 or 47775 or 47776 or 47777))
                continue;

            if (this.recentMagicLookActionEvents.Any(item =>
                    item.ActionId == cast.ActionId &&
                    item.CasterName == cast.CasterName &&
                    (DateTime.Now - item.OccurredAt).TotalMilliseconds < 500))
                continue;

            this.recentMagicLookActionEvents.Add(new RecentMagicLookActionEvent
            {
                ActionId = cast.ActionId,
                CasterName = cast.CasterName,
                OccurredAt = DateTime.Now
            });
        }
    }

    private void CleanupRecentMagicLookEvents()
    {
        var now = DateTime.Now;
        this.recentMagicLookActionEvents.RemoveAll(item => (now - item.OccurredAt).TotalSeconds > 8.0);
        this.recentVfxEvents.RemoveAll(item => (now - item.OccurredAt).TotalSeconds > 8.0);
        this.recentObjectEffectFallbackEvents.RemoveAll(item => (now - item.OccurredAt).TotalSeconds > 8.0);

        foreach (var key in this.recentTrueFireObjectEffectTriggers
                     .Where(item => (now - item.Value).TotalSeconds > 8.0)
                     .Select(item => item.Key)
                     .ToList())
        {
            this.recentTrueFireObjectEffectTriggers.Remove(key);
        }
    }

    private string GetMagicLookPhase1TextFromRecentEvents()
    {
        if (!this.IsMagicLookNazoNazoMagicWindowActive())
            return string.Empty;

        var phaseActionEvents = this.recentMagicLookActionEvents
            .Where(item => item.ActionId is 47768 or 47771 or 47774 or 47775 or 47776 or 47777)
            .OrderByDescending(item => item.OccurredAt)
            .ToList();

        if (phaseActionEvents.Count == 0)
            return string.Empty;

        var activeActionIds = phaseActionEvents
            .Select(item => item.ActionId)
            .ToHashSet();

        var latestAction = phaseActionEvents[0];

        // 1. ファイガVFXを使う Phase1 設定を優先します。
        //    ブリザガ/サンダガの両方が履歴に残るケースでも、直近Action側の設定だけを見ることで誤一致を避けます。
        foreach (var setting in this.configuration.MagicLookPhaseTextSettings)
        {
            if (!setting.Enabled)
                continue;

            if (!string.Equals(setting.Phase, "Phase1", StringComparison.OrdinalIgnoreCase))
                continue;

            if (setting.RequiredVfxPaths.Count == 0)
                continue;

            if (setting.ActionIds.Count == 0)
                continue;

            if (!setting.ActionIds.Contains(latestAction.ActionId))
                continue;

            if (!setting.RequiredVfxPaths.All(this.HasRecentVfxPath))
                continue;

            return setting.Text;
        }

        // 2. ブリザガ + サンダガの Phase1 組み合わせ。
        //    以前はこの組み合わせ設定がPhase1側になかった/表示前に抑制されていたため、表示されませんでした。
        foreach (var setting in this.configuration.MagicLookPhaseTextSettings)
        {
            if (!setting.Enabled)
                continue;

            if (!string.Equals(setting.Phase, "Phase1", StringComparison.OrdinalIgnoreCase))
                continue;

            if (setting.RequiredVfxPaths.Count != 0)
                continue;

            if (!this.IsMagicLookPhaseActionMatch(setting, activeActionIds))
                continue;

            return setting.Text;
        }

        return string.Empty;
    }

    private bool HasRecentVfxPath(string vfxPath)
    {
        var normalized = NormalizeVfxPath(vfxPath);
        return this.recentVfxEvents.Any(item => item.Path == normalized);
    }

    private static string NormalizeVfxPath(string path)
    {
        return (path ?? string.Empty)
            .Replace('\\', '/')
            .Trim()
            .ToLowerInvariant();
    }

    private bool IsMagicLookPhaseActionMatch(MagicLookPhaseTextSetting setting, HashSet<uint> activeActionIds)
    {
        if (setting.ActionIds.Count == 0)
            return false;

        // Phase4 の設定は、真/偽を1行に表すため ActionIds に候補をまとめています。
        // 必要な組み合わせを明示的に判定して、似た行に誤一致しないようにします。
        return setting.Label switch
        {
            "真ブリザガ / 真サンダガ" => activeActionIds.Contains(47768) && activeActionIds.Contains(47775),
            "真ブリザガ / 偽サンダガ" => activeActionIds.Contains(47768) && (activeActionIds.Contains(47776) || activeActionIds.Contains(47777)),
            "偽ブリザガ / 真サンダガ" => (activeActionIds.Contains(47771) || activeActionIds.Contains(47774)) && activeActionIds.Contains(47775),
            "偽ブリザガ / 偽サンダガ" => (activeActionIds.Contains(47771) || activeActionIds.Contains(47774)) && (activeActionIds.Contains(47776) || activeActionIds.Contains(47777)),
            _ => setting.ActionIds.All(activeActionIds.Contains),
        };
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

        this.SetMagicChargeText(this.LastMagicChargeResult, resetFade: true);
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

        this.SetMagicChargeText(this.LastMagicChargeResult, resetFade: false);
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

    private void SetMagicLookText(string text)
    {
        var now = DateTime.Now;

        if (!string.Equals(this.activeLockText, text, StringComparison.Ordinal) || now > this.activeLockUntil)
            this.activeLockStartedAt = now;

        this.activeLockText = text;
        this.activeLockUntil = now.AddSeconds(this.configuration.MagicLookDisplaySeconds);
    }

    private void SetMagicChargeText(string text, bool resetFade)
    {
        this.activeChargeText = text;

        if (resetFade || this.activeChargeStartedAt == DateTime.MinValue || DateTime.Now > this.activeChargeUntil)
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
        this.activeGrandCrossDisplayMode = this.GetGrandCrossDisplayMode();

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

    private bool TryResetGrandCrossCountOnOchokuriSoul(List<CastEvent> newlyStartedCasts)
    {
        var ochokuriSoulCast = newlyStartedCasts.FirstOrDefault(cast => cast.ActionId == 49884);
        if (ochokuriSoulCast.Caster == null)
            return false;

        this.ResetGrandCrossState();
        this.previousCastKeys.Clear();

        this.LastGrandCrossEvent =
            $"おちょくりソウル検出によりGrandCross取得回数をリセット: {ochokuriSoulCast.CasterName} / 49884";
        this.LastGrandCrossResult = string.Empty;

        return true;
    }

    private void ProcessGrandCross(List<CastEvent> currentCasts, List<CastEvent> newlyStartedCasts)
    {
        if (!this.configuration.GrandCrossEnabled)
            return;

        // ActionId:49884 / おちょくりソウル の後からGrandCross取得を開始するため、
        // この詠唱開始を新しいGrandCrossサイクルの起点として取得回数と保持状態をリセットする。
        if (this.TryResetGrandCrossCountOnOchokuriSoul(newlyStartedCasts))
            return;

        this.ProcessGrandCrossChaosCasts(currentCasts);
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
                this.activeGrandCrossDisplayMode = -1;
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

        // 3回目のグランドクロス直後に即表示してしまうと、最後に付与されるデバフを拾い漏らすことがあるため、
        // 詠唱完了後は GrandCrossStatusCaptureSeconds が経過するまで待ってからまとめて取得する。
        if (DateTime.Now < this.pendingGrandCrossStatusCaptureUntil)
            return;

        var newDebuffs = this.FindGrandCrossDebuffs(preferNewStatusOnly: true);

        if (newDebuffs.Count == 0)
            newDebuffs = this.FindGrandCrossDebuffs(preferNewStatusOnly: false);

        if (newDebuffs.Count == 0)
        {
            this.pendingGrandCrossStatusCapture = false;
            this.LastGrandCrossEvent = "グランドクロス付与ステータス取得タイムアウト";
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
                isFake);

            addedTexts.Add(displayText);
        }

        this.grandCrossCapturedCastCount++;

        this.pendingGrandCrossStatusCapture = false;
        this.pendingGrandCrossIsFake = false;

        var addedText = string.Join(this.configuration.GrandCrossSeparator, addedTexts);

        this.LastGrandCrossEvent = $"グランドクロス保持: {addedText}";
        this.LastGrandCrossResult = string.Join(
            this.configuration.GrandCrossSeparator,
            this.GetActiveGrandCrossDisplayItemsSorted().Select(this.FormatGrandCrossDisplayItem));

        if (this.grandCrossCapturedCastCount >= 3)
        {
            this.grandCrossCycleCompleted = true;
            this.UpdateGrandCrossDisplayFromHeldItems();

            this.LastGrandCrossEvent = "グランドクロス3回分表示開始";
        }
    }

    private void ProcessGrandCrossChaosCasts(List<CastEvent> currentCasts)
    {
        var chaosCast = currentCasts.FirstOrDefault(cast =>
            (cast.ActionId == 47902 || cast.ActionId == 47903) &&
            this.IsChaosCasterNameMatched(cast.CasterName));

        if (chaosCast.Caster != null)
        {
            if (!this.chaosCasting || this.currentChaosActionId != chaosCast.ActionId)
            {
                this.chaosCasting = true;
                this.currentChaosActionId = chaosCast.ActionId;
                this.currentChaosInternalParam = 0;
                this.currentChaosCasterName = chaosCast.CasterName;

                this.LastGrandCrossEvent =
                    $"カオスAction詠唱開始: {chaosCast.CasterName} / {chaosCast.ActionId}";
            }

            var param = this.GetChaosGrandCrossInternalParam(chaosCast.Caster);
            if (param == 1119 || param == 1120)
                this.currentChaosInternalParam = param;

            this.LastGrandCrossEvent =
                $"カオスAction詠唱中: {chaosCast.CasterName} / {chaosCast.ActionId} / Param:{this.currentChaosInternalParam}";

            return;
        }

        if (!this.chaosCasting)
            return;

        var completedActionId = this.currentChaosActionId;
        var completedParam = this.currentChaosInternalParam;
        var completedCasterName = this.currentChaosCasterName;

        this.chaosCasting = false;
        this.currentChaosActionId = 0;
        this.currentChaosInternalParam = 0;
        this.currentChaosCasterName = string.Empty;

        if (completedParam != 1119 && completedParam != 1120)
        {
            this.LastGrandCrossEvent =
                $"カオスAction詠唱完了: {completedCasterName} / {completedActionId} / Param未検出";

            return;
        }

        var statusId = completedActionId switch
        {
            47902 => 5547u,
            47903 => 5548u,
            _ => 0u
        };

        var text = this.GetChaosGrandCrossDisplayText(completedActionId, completedParam);
        if (statusId == 0 || string.IsNullOrWhiteSpace(text))
            return;

        this.AddOrUpdatePendingChaosDebuffText(statusId, text);

        this.LastGrandCrossEvent =
            $"カオスAction待機: {completedCasterName} / {completedActionId} / Param:{completedParam} / StatusId:{statusId} / {text}";
    }

    private bool IsChaosCasterNameMatched(string casterName)
    {
        if (!this.configuration.GrandCrossChaosFilterByEnemyName)
            return true;

        if (string.IsNullOrWhiteSpace(this.configuration.GrandCrossChaosEnemyNameKeyword))
            return false;

        return casterName.Contains(this.configuration.GrandCrossChaosEnemyNameKeyword, StringComparison.OrdinalIgnoreCase);
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
                false);

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
            this.GetActiveGrandCrossDisplayItemsSorted().Select(this.FormatGrandCrossDisplayItem));

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
                status.RemainingTime));
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
            this.activeGrandCrossDisplayMode = -1;
            this.LastGrandCrossResult = string.Empty;
            return;
        }

        var resultText = string.Join(
            this.configuration.GrandCrossSeparator,
            activeItems.Select(this.FormatGrandCrossDisplayItem));

        this.SetGrandCrossText(resultText, resetFade: false);

        this.LastGrandCrossResult = resultText;
    }

    private string FormatGrandCrossDisplayItem(GrandCrossDisplayItem item)
    {
        return $"{item.DisplayText}｛{item.RemainingTime:0.0}s｝";
    }

    private string FormatGrandCrossTestDisplayText(string displayText)
    {
        if (string.IsNullOrWhiteSpace(displayText))
            return string.Empty;

        return $"{displayText}｛0.0s｝";
    }

    private string FormatHeldDebuffSummary(List<GrandCrossDisplayItem> activeItems)
    {
        if (activeItems.Count == 0)
            return "＊＊＊＊";

        return string.Join("_", activeItems.Select(this.FormatGrandCrossDisplayItem));
    }

    private string FormatChaosHeldDebuffSummary()
    {
        var chaosItems = this.GetActiveGrandCrossHeldDebuffsSorted()
            .Where(item => item.StatusId == 5547 || item.StatusId == 5548)
            .Select(item => new GrandCrossDisplayItem
            {
                DisplayText = item.DisplayText,
                RemainingTime = item.RemainingTime,
                SortKey = item.StatusId
            })
            .ToList();

        return this.FormatHeldDebuffSummary(chaosItems);
    }

    private string GetGrandCrossTruthDisplayName()
    {
        return this.currentGrandCrossInternalParam switch
        {
            1121 => "偽",
            1122 => "真",
            _ => "真 or 偽"
        };
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
            Math.Clamp(textColor.W * alpha, 0.0f, 1.0f));

        var finalOutlineColor = new Vector4(
            outlineColor.X,
            outlineColor.Y,
            outlineColor.Z,
            Math.Clamp(outlineColor.W * alpha, 0.0f, 1.0f));

        var drawList = ImGui.GetForegroundDrawList();

        var textSize = ImGui.CalcTextSize(text);
        var baseFontSize = MathF.Max(1.0f, ImGui.GetFontSize());
        var fontScale = fontSize / baseFontSize;
        textSize *= fontScale;

        var textPosition = new Vector2(
            MathF.Round(screenPositionX - textSize.X / 2.0f),
            MathF.Round(screenPositionY - textSize.Y / 2.0f));

        if (drawBackground)
        {
            var padding = fontPreset switch
            {
                2 => new Vector2(10.0f, 6.0f),
                _ => new Vector2(8.0f, 5.0f)
            };

            var bgMin = new Vector2(
                MathF.Round(textPosition.X - padding.X),
                MathF.Round(textPosition.Y - padding.Y));

            var bgMax = new Vector2(
                MathF.Round(textPosition.X + textSize.X + padding.X),
                MathF.Round(textPosition.Y + textSize.Y + padding.Y));

            drawList.AddRectFilled(
                bgMin,
                bgMax,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f * alpha)),
                6.0f);
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
                crispOutlineThickness);
        }

        drawList.AddText(
            ImGui.GetFont(),
            fontSize,
            textPosition,
            ImGui.ColorConvertFloat4ToU32(finalTextColor),
            text);
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
                text);
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
            screenPosition.Y - textSize.Y / 2.0f);

        if (drawBackground)
        {
            var padding = new Vector2(8.0f, 5.0f);
            var bgMin = textPosition - padding;
            var bgMax = textPosition + textSize + padding;

            drawList.AddRectFilled(
                bgMin,
                bgMax,
                ImGui.ColorConvertFloat4ToU32(new Vector4(0f, 0f, 0f, 0.55f)),
                6.0f);
        }

        drawList.AddText(
            ImGui.GetFont(),
            fontSize,
            textPosition,
            ImGui.ColorConvertFloat4ToU32(textColor),
            text);
    }

    private void AddMagicLookLog(string casterName, uint actionId, string result)
    {
        var line = $"{DateTime.Now:HH:mm:ss} / {casterName} / ActionId:{actionId} / {result}";

        if (this.magicLookLogLines.Count > 0 && this.magicLookLogLines[0] == line)
            return;

        this.magicLookLogLines.Insert(0, line);

        while (this.magicLookLogLines.Count > 80)
            this.magicLookLogLines.RemoveAt(this.magicLookLogLines.Count - 1);
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

    private delegate void ProcessObjectEffectDelegate(
        nint thisPtr,
        uint entityId,
        uint actionId,
        ulong a4);

    private delegate nint ActorVfxCreateDelegate(
        nint path,
        nint targetActor,
        nint sourceActor,
        float a4,
        byte a5,
        ushort a6,
        byte a7);

    private sealed class RecentMagicLookActionEvent
    {
        public uint ActionId { get; init; }

        public string CasterName { get; init; } = string.Empty;

        public DateTime OccurredAt { get; init; }
    }

    private sealed class RecentVfxEvent
    {
        public string Path { get; init; } = string.Empty;

        public string Source { get; init; } = string.Empty;

        public DateTime OccurredAt { get; init; }
    }

    private sealed class RecentObjectEffectFallbackEvent
    {
        public nint ThisPtr { get; init; }

        public uint Data1 { get; init; }

        public uint Data2 { get; init; }

        public ulong A4 { get; init; }

        public string PseudoVfxPath { get; init; } = string.Empty;

        public DateTime OccurredAt { get; init; }
    }

    private readonly record struct CastEvent(
        string CasterName,
        uint ActionId,
        string Key,
        IBattleChara Caster);

    private readonly record struct CastMatch(
        string CasterName,
        uint ActionId,
        string Label,
        string Text);

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
        float RemainingTime);

    private enum MagicChargeStatusState
    {
        Unknown = 0,
        True = 1,
        Fake = 2
    }
}
