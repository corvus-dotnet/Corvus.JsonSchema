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
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;

namespace Corvus.Json.JsonSchema.OpenApi30;
public readonly partial struct OpenApiDocument
{
    /// <summary>
    /// Generated from JSON Schema.
    /// </summary>
    public readonly partial struct OAuthFlows
    {
        /// <summary>
        /// A pattern property matching ^x- producing a <see cref = "Corvus.Json.JsonAny"/>.
        /// </summary>
        public static Regex PatternPropertyJsonAny => new("^x-", RegexOptions.Compiled, TimeSpan.FromSeconds(1));

        private static readonly ImmutableDictionary<Regex, PatternPropertyValidator> __CorvusPatternProperties = CreatePatternPropertiesValidators();
        /// <summary>
        /// Determines if a property matches ^x- producing a <see cref = "Corvus.Json.JsonAny"/>.
        /// </summary>
        public bool MatchesPatternJsonAny(in JsonObjectProperty property)
        {
            return property.Name.IsMatch(PatternPropertyJsonAny);
        }

        /// <summary>
        /// Get a property value as the type matching the given pattern property ^x- as a <see cref = "Corvus.Json.JsonAny"/>.
        /// </summary>
        public bool TryAsPatternJsonAny(in JsonObjectProperty property, out Corvus.Json.JsonAny result)
        {
            if (property.Name.IsMatch(PatternPropertyJsonAny))
            {
                result = property.ValueAs<Corvus.Json.JsonAny>();
                return true;
            }
            else
            {
                result = Corvus.Json.JsonAny.Undefined;
                return false;
            }
        }

        /// <summary>
        /// Try to get a property value as the type matching the given pattern property ^x- as a <see cref = "Corvus.Json.JsonAny"/>.
        /// </summary>
        public Corvus.Json.JsonAny AsPatternJsonAny(in JsonObjectProperty property)
        {
            return property.ValueAs<Corvus.Json.JsonAny>();
        }

        private static ImmutableDictionary<Regex, PatternPropertyValidator> CreatePatternPropertiesValidators()
        {
            ImmutableDictionary<Regex, PatternPropertyValidator>.Builder builder = ImmutableDictionary.CreateBuilder<Regex, PatternPropertyValidator>();
            builder.Add(PatternPropertyJsonAny, __CorvusValidatePatternPropertyJsonAny);
            return builder.ToImmutable();
        }

        private static ValidationContext __CorvusValidatePatternPropertyJsonAny(in JsonObjectProperty that, in ValidationContext validationContext, ValidationLevel level)
        {
            return that.ValueAs<Corvus.Json.JsonAny>().Validate(validationContext, level);
        }
    }
}