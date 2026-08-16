namespace SunBloom.SharedKernel.Time;

/// <summary>The production <see cref="IClock" />. Tests substitute a fixed clock.</summary>
public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
