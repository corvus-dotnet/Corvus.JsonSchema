//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------
#nullable enable
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.Draft202012;
/// <summary>
/// Format vocabulary meta-schema for annotation results
/// </summary>
public readonly partial struct FormatAnnotation
{
    /// <summary>
    /// The well-known property names in the JSON object.
    /// </summary>
    public static class JsonPropertyNames
    {
        /// <summary>
        /// JSON property name for <see cref = "Format"/>.
        /// </summary>
        public static ReadOnlySpan<byte> FormatUtf8 => "format"u8;

        /// <summary>
        /// JSON property name for <see cref = "Format"/>.
        /// </summary>
        public const string Format = "format";
    }

    /// <summary>
    /// Gets the (optional) <c>format</c> property.
    /// </summary>
    public Corvus.Json.JsonString Format
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.FormatUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Format, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Creates an instance of a <see cref = "FormatAnnotation"/>.
    /// </summary>
    public static FormatAnnotation Create(Corvus.Json.JsonString? format = null)
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        if (format is Corvus.Json.JsonString format__)
        {
            builder.Add(JsonPropertyNames.Format, format__.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Sets format.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public FormatAnnotation WithFormat(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.Format, value);
    }

    private static ValidationContext __CorvusValidateFormat(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    /// <summary>
    /// Tries to get the validator for the given property.
    /// </summary>
    /// <param name = "property">The property for which to get the validator.</param>
    /// <param name = "hasJsonElementBacking"><c>True</c> if the object containing the property has a JsonElement backing.</param>
    /// <param name = "propertyValidator">The validator for the property, if provided by this schema.</param>
    /// <returns><c>True</c> if the validator was found.</returns>
    private bool __TryGetCorvusLocalPropertiesValidator(in JsonObjectProperty property, bool hasJsonElementBacking, [NotNullWhen(true)] out ObjectPropertyValidator? propertyValidator)
    {
        if (hasJsonElementBacking)
        {
            if (property.NameEquals(JsonPropertyNames.FormatUtf8))
            {
                propertyValidator = __CorvusValidateFormat;
                return true;
            }
        }
        else
        {
            if (property.NameEquals(JsonPropertyNames.Format))
            {
                propertyValidator = __CorvusValidateFormat;
                return true;
            }
        }

        propertyValidator = null;
        return false;
    }
}