namespace SunBloom.SharedKernel.Time;

/// <summary>
/// The only sanctioned source of the current time.
/// </summary>
/// <remarks>
/// Competency scoring applies exponential time decay to evidence, so "now" is an
/// input to the domain rather than an ambient fact. Code that calls
/// <c>DateTime.UtcNow</c> directly cannot be tested at a chosen point in time, which
/// would make the scoring model unverifiable. Enforced by <c>NoAmbientTimeTests</c>.
/// </remarks>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
