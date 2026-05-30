#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Controls whether JSON object paths are folded when encoding TOON.
/// </summary>
public enum ToonKeyFolding
{
    /// <summary>
    /// Do not fold nested object paths.
    /// </summary>
    Off,

    /// <summary>
    /// Fold paths only when doing so is unambiguous.
    /// </summary>
    Safe,
}