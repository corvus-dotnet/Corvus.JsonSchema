
namespace Corvus.Json.CodeGenerator;

/// <summary>
/// Determines whether optional properties are emitted as .NET nullable values.
/// </summary>
public enum OptionalAsNullable
{
    /// <summary>
    /// Optional properties are not emitted as nullable
    /// </summary>
    None,

    /// <summary>
    /// Properties that are optional are emitted as nullable, and <c>JsonValueKind.Null</c> or 
    /// <c>JsonValueKind.Undefined</c> values produce <see langword="null"/>.
    /// </summary>
    NullOrUndefined,
}
