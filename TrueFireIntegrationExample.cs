using System;
using Dalamud.Game.ClientState.Objects.Types;
using FFXIVClientStructs.FFXIV.Client.Game.Character;

namespace magicLook.TrueFirePatch;

/// <summary>
/// Copy the relevant parts into your existing VfxLogCollector / CallTextManager.
/// This file is intentionally an integration example because the exact current source was not available.
/// </summary>
public sealed class TrueFireIntegrationExample
{
    private readonly TrueFireState trueFireState = new();

    /*
     * Call this from your VFX detour after you resolve:
     * - path: VFX path string
     * - source: source actor if available
     * - target: target actor if available
     * - isKefkaCasting47764: result of scanning ObjectTable for Kefka/DataId 19504 casting 47764
     * - isTargetSelf: target EntityId == ClientState.LocalPlayer.EntityId
     */
    public void OnVfxDetected(
        string? path,
        IGameObject? source,
        IGameObject? target,
        bool isKefkaCasting47764,
        bool isTargetSelf,
        Action<string> addDebugLog,
        Action<string> showCallText)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        if (TrueFireVfxMatcher.IsImportantKefkaVfx(path))
        {
            addDebugLog($"VFX hit / path:{path} source:{source?.Name} target:{target?.Name}");
        }

        // Splatoon: Kefta true = Kefka requires cast 47764 + c02c, Conditional true, reset true.
        if (TrueFireVfxMatcher.IsKefkaTrueFireGate(path))
        {
            if (isKefkaCasting47764)
            {
                this.trueFireState.OpenWindow();
                addDebugLog("TrueFire / Conditional opened / Kefka 47764 + c02c / 5000ms");
            }
            else
            {
                addDebugLog("TrueFire / c02c ignored because Kefka is not casting 47764");
            }

            return;
        }

        // Splatoon: Player spread = player ObjectKind 1 + a0c while Conditional is true.
        if (TrueFireVfxMatcher.IsPlayerSpread(path))
        {
            if (!this.trueFireState.IsWindowActive)
            {
                addDebugLog("TrueFire / a0c ignored because Conditional window is closed");
                return;
            }

            if (isTargetSelf && this.trueFireState.TryConsumeSelfSpread())
            {
                showCallText("<<< 散開 >>>");
                addDebugLog("TrueFire / SELF spread detected / a0c");
            }
            else
            {
                addDebugLog($"TrueFire / OTHER spread detected / a0c / target:{target?.Name}");
            }
        }
    }
}
