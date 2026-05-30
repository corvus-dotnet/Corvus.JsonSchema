#if STJ && TOON
namespace Corvus.Toon;
#else
namespace Corvus.Text.Json.Toon;
#endif

/// <summary>
/// Options for configuring TOON decoding.
/// </summary>
public readonly struct ToonReaderOptions
{
    private readonly bool strict;
    private readonly bool strictSet;

    /// <summary>
    /// Gets the default TOON reader options.
    /// </summary>
    public static readonly ToonReaderOptions Default = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ToonReaderOptions"/> struct.
    /// </summary>
    public ToonReaderOptions()
    {
        this.strict = true;
        this.strictSet = true;
        this.IndentSize = 2;
        this.ExpandPaths = ToonPathExpansion.Off;
    }

    /// <summary>
    /// Gets a value indicating whether strict structural validation is enabled.
    /// </summary>
    public bool Strict
    {
        get => !this.strictSet || this.strict;
        init
        {
            this.strict = value;
            this.strictSet = true;
        }
    }

    /// <summary>
    /// Gets the number of spaces in one indentation level.
    /// </summary>
    public int IndentSize { get; init; }

    /// <summary>
    /// Gets the dotted-key path expansion mode.
    /// </summary>
    public ToonPathExpansion ExpandPaths { get; init; }

    internal int EffectiveIndentSize => this.IndentSize > 0 ? this.IndentSize : 2;
}