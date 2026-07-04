using System;

namespace magicLook.TrueFirePatch;

/// <summary>
/// Splatoon layout based VFX matcher for Kefka True Fire / spread.
/// </summary>
public static class TrueFireVfxMatcher
{
    public const uint NazoNazoMagicActionId = 47764;

    public const string KefkaTrueFireGateVfx = "vfx/lockon/eff/m0462trg_c02c.avfx";
    public const string PlayerSpreadVfx = "vfx/lockon/eff/m0462trg_a0c.avfx";

    // Existing Kefka puzzle candidates. Keep these in the important log list.
    public const string HeadShareVfxC03C = "vfx/lockon/eff/m0462trg_c03c.avfx";
    public const string LineStepVfxC05C = "vfx/lockon/eff/m0462trg_c05c.avfx";
    public const string LineDoNotStepVfxC06C = "vfx/lockon/eff/m0462trg_c06c.avfx";

    public static bool IsKefkaTrueFireGate(string? path)
        => ContainsPath(path, KefkaTrueFireGateVfx);

    public static bool IsPlayerSpread(string? path)
        => ContainsPath(path, PlayerSpreadVfx);

    public static bool IsImportantKefkaVfx(string? path)
    {
        return IsKefkaTrueFireGate(path)
            || IsPlayerSpread(path)
            || ContainsPath(path, HeadShareVfxC03C)
            || ContainsPath(path, LineStepVfxC05C)
            || ContainsPath(path, LineDoNotStepVfxC06C);
    }

    public static string NormalizePath(string? path)
    {
        return string.IsNullOrWhiteSpace(path)
            ? string.Empty
            : path.Replace('\\', '/').Trim().ToLowerInvariant();
    }

    private static bool ContainsPath(string? actualPath, string expectedPath)
    {
        var actual = NormalizePath(actualPath);
        var expected = NormalizePath(expectedPath);

        return actual.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }
}
