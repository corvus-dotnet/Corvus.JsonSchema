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

namespace JsonSchemaSample.CustomerApi;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct ApiCustomer
{
    /// <summary>
    /// The well-known property names in the JSON object.
    /// </summary>
    public static class JsonPropertyNames
    {
        /// <summary>
        /// JSON property name for <see cref = "CustomerId"/>.
        /// </summary>
        public static ReadOnlySpan<byte> CustomerIdUtf8 => "customerId"u8;

        /// <summary>
        /// JSON property name for <see cref = "CustomerId"/>.
        /// </summary>
        public const string CustomerId = "customerId";
        /// <summary>
        /// JSON property name for <see cref = "FamilyName"/>.
        /// </summary>
        public static ReadOnlySpan<byte> FamilyNameUtf8 => "familyName"u8;

        /// <summary>
        /// JSON property name for <see cref = "FamilyName"/>.
        /// </summary>
        public const string FamilyName = "familyName";
        /// <summary>
        /// JSON property name for <see cref = "GivenName"/>.
        /// </summary>
        public static ReadOnlySpan<byte> GivenNameUtf8 => "givenName"u8;

        /// <summary>
        /// JSON property name for <see cref = "GivenName"/>.
        /// </summary>
        public const string GivenName = "givenName";
    }

    /// <summary>
    /// Gets the <c>customerId</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
    /// </summary>
    public Corvus.Json.JsonUuid CustomerId
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.CustomerIdUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonUuid(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.CustomerId, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonUuid>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the <c>familyName</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
    /// </summary>
    public Corvus.Json.JsonString FamilyName
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.FamilyNameUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.FamilyName, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the <c>givenName</c> property. If the instance is valid, this property will be not be <c>undefined</c>.
    /// </summary>
    public Corvus.Json.JsonString GivenName
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.GivenNameUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.GivenName, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Creates an instance of a <see cref = "ApiCustomer"/>.
    /// </summary>
    public static ApiCustomer Create(Corvus.Json.JsonUuid customerId, Corvus.Json.JsonString familyName, Corvus.Json.JsonString givenName)
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        builder.Add(JsonPropertyNames.CustomerId, customerId.AsAny);
        builder.Add(JsonPropertyNames.FamilyName, familyName.AsAny);
        builder.Add(JsonPropertyNames.GivenName, givenName.AsAny);
        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Sets customerId.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public ApiCustomer WithCustomerId(in Corvus.Json.JsonUuid value)
    {
        return this.SetProperty(JsonPropertyNames.CustomerId, value);
    }

    /// <summary>
    /// Sets familyName.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public ApiCustomer WithFamilyName(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.FamilyName, value);
    }

    /// <summary>
    /// Sets givenName.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public ApiCustomer WithGivenName(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.GivenName, value);
    }

    private static ValidationContext __CorvusValidateFamilyName(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateGivenName(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateCustomerId(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonUuid>().Validate(validationContext, level);
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
            if (property.NameEquals(JsonPropertyNames.FamilyNameUtf8))
            {
                propertyValidator = __CorvusValidateFamilyName;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.GivenNameUtf8))
            {
                propertyValidator = __CorvusValidateGivenName;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.CustomerIdUtf8))
            {
                propertyValidator = __CorvusValidateCustomerId;
                return true;
            }
        }
        else
        {
            if (property.NameEquals(JsonPropertyNames.FamilyName))
            {
                propertyValidator = __CorvusValidateFamilyName;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.GivenName))
            {
                propertyValidator = __CorvusValidateGivenName;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.CustomerId))
            {
                propertyValidator = __CorvusValidateCustomerId;
                return true;
            }
        }

        propertyValidator = null;
        return false;
    }
}