#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Controls whether dotted TOON keys are expanded into nested JSON objects when decoding.
/// </summary>
public enum ToonPathExpansion
{
    /// <summary>
    /// Preserve dotted keys as literal property names.
    /// </summary>
    Off,

    /// <summary>
    /// Expand dotted keys only when doing so is unambiguous.
    /// </summary>
    Safe,
}