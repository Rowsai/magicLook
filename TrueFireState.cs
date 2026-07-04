using System;

namespace magicLook.TrueFirePatch;

/// <summary>
/// Conditional state equivalent to the Splatoon layout:
/// Kefka 47764 cast + c02c VFX opens a 5000ms window,
/// then player a0c VFX inside that window means spread.
/// </summary>
public sealed class TrueFireState
{
    private readonly TimeSpan windowDuration;
    private DateTime windowUntilUtc = DateTime.MinValue;
    private DateTime lastSelfSpreadUtc = DateTime.MinValue;

    public TrueFireState(TimeSpan? windowDuration = null)
    {
        this.windowDuration = windowDuration ?? TimeSpan.FromMilliseconds(5000);
    }

    public bool IsWindowActive => DateTime.UtcNow <= this.windowUntilUtc;

    public DateTime WindowUntilUtc => this.windowUntilUtc;

    public void OpenWindow()
    {
        this.windowUntilUtc = DateTime.UtcNow.Add(this.windowDuration);
    }

    public void Reset()
    {
        this.windowUntilUtc = DateTime.MinValue;
        this.lastSelfSpreadUtc = DateTime.MinValue;
    }

    /// <summary>
    /// Returns true once per short cooldown to avoid repeated overhead text spam.
    /// </summary>
    public bool TryConsumeSelfSpread(TimeSpan? cooldown = null)
    {
        if (!this.IsWindowActive)
            return false;

        var now = DateTime.UtcNow;
        var cd = cooldown ?? TimeSpan.FromMilliseconds(1500);

        if (now - this.lastSelfSpreadUtc < cd)
            return false;

        this.lastSelfSpreadUtc = now;
        return true;
    }
}
