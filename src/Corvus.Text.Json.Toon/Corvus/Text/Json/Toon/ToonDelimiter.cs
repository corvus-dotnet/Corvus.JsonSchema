#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Identifies the delimiter used in TOON inline arrays, tabular rows, and field lists.
/// </summary>
public enum ToonDelimiter
{
    /// <summary>
    /// Comma-delimited TOON.
    /// </summary>
    Comma,

    /// <summary>
    /// Tab-delimited TOON.
    /// </summary>
    Tab,

    /// <summary>
    /// Pipe-delimited TOON.
    /// </summary>
    Pipe,
}