#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Options for configuring TOON encoding.
/// </summary>
public readonly struct ToonWriterOptions
{
    /// <summary>
    /// Gets the default TOON writer options.
    /// </summary>
    public static readonly ToonWriterOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonWriterOptions"/> struct.
    /// </summary>
    public ToonWriterOptions()
    {
        this.IndentSize = 2;
        this.Delimiter = ToonDelimiter.Comma;
        this.KeyFolding = ToonKeyFolding.Off;
        this.FlattenDepth = int.MaxValue;
    }

    /// <summary>
    /// Gets the number of spaces in one indentation level.
    /// </summary>
    public int IndentSize { get; init; }

    /// <summary>
    /// Gets the delimiter to use for inline arrays and tabular rows.
    /// </summary>
    public ToonDelimiter Delimiter { get; init; }

    /// <summary>
    /// Gets the key folding mode.
    /// </summary>
    public ToonKeyFolding KeyFolding { get; init; }

    /// <summary>
    /// Gets the maximum number of path segments to fold when key folding is enabled.
    /// </summary>
    public int FlattenDepth { get; init; }

    internal int EffectiveIndentSize => this.IndentSize > 0 ? this.IndentSize : 2;
}