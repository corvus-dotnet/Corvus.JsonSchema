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

namespace JsonSchemaSample.Api;
/// <summary>
/// The person schema https://schema.org/Person
/// </summary>
public readonly partial struct Person
{
    /// <summary>
    /// The well-known property names in the JSON object.
    /// </summary>
    public static class JsonPropertyNames
    {
        /// <summary>
        /// JSON property name for <see cref = "BirthDate"/>.
        /// </summary>
        public static ReadOnlySpan<byte> BirthDateUtf8 => "birthDate"u8;

        /// <summary>
        /// JSON property name for <see cref = "BirthDate"/>.
        /// </summary>
        public const string BirthDate = "birthDate";
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
        /// <summary>
        /// JSON property name for <see cref = "Height"/>.
        /// </summary>
        public static ReadOnlySpan<byte> HeightUtf8 => "height"u8;

        /// <summary>
        /// JSON property name for <see cref = "Height"/>.
        /// </summary>
        public const string Height = "height";
        /// <summary>
        /// JSON property name for <see cref = "OtherNames"/>.
        /// </summary>
        public static ReadOnlySpan<byte> OtherNamesUtf8 => "otherNames"u8;

        /// <summary>
        /// JSON property name for <see cref = "OtherNames"/>.
        /// </summary>
        public const string OtherNames = "otherNames";
    }

    /// <summary>
    /// Gets the (optional) <c>birthDate</c> property.
    /// </summary>
    public Corvus.Json.JsonDate BirthDate
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.BirthDateUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonDate(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.BirthDate, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonDate>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>familyName</c> property.
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
    /// Gets the (optional) <c>givenName</c> property.
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
    /// Gets the (optional) <c>height</c> property.
    /// </summary>
    public Corvus.Json.JsonNumber Height
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.HeightUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonNumber(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.Height, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonNumber>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Gets the (optional) <c>otherNames</c> property.
    /// </summary>
    public Corvus.Json.JsonString OtherNames
    {
        get
        {
            if ((this.backing & Backing.JsonElement) != 0)
            {
                if (this.jsonElementBacking.ValueKind != JsonValueKind.Object)
                {
                    return default;
                }

                if (this.jsonElementBacking.TryGetProperty(JsonPropertyNames.OtherNamesUtf8, out JsonElement result))
                {
                    return new Corvus.Json.JsonString(result);
                }
            }

            if ((this.backing & Backing.Object) != 0)
            {
                if (this.objectBacking.TryGetValue(JsonPropertyNames.OtherNames, out JsonAny result))
                {
                    return result.As<Corvus.Json.JsonString>();
                }
            }

            return default;
        }
    }

    /// <summary>
    /// Creates an instance of a <see cref = "Person"/>.
    /// </summary>
    public static Person Create(Corvus.Json.JsonDate? birthDate = null, Corvus.Json.JsonString? familyName = null, Corvus.Json.JsonString? givenName = null, Corvus.Json.JsonNumber? height = null, Corvus.Json.JsonString? otherNames = null)
    {
        var builder = ImmutableList.CreateBuilder<JsonObjectProperty>();
        if (birthDate is Corvus.Json.JsonDate birthDate__)
        {
            builder.Add(JsonPropertyNames.BirthDate, birthDate__.AsAny);
        }

        if (familyName is Corvus.Json.JsonString familyName__)
        {
            builder.Add(JsonPropertyNames.FamilyName, familyName__.AsAny);
        }

        if (givenName is Corvus.Json.JsonString givenName__)
        {
            builder.Add(JsonPropertyNames.GivenName, givenName__.AsAny);
        }

        if (height is Corvus.Json.JsonNumber height__)
        {
            builder.Add(JsonPropertyNames.Height, height__.AsAny);
        }

        if (otherNames is Corvus.Json.JsonString otherNames__)
        {
            builder.Add(JsonPropertyNames.OtherNames, otherNames__.AsAny);
        }

        return new(builder.ToImmutable());
    }

    /// <summary>
    /// Sets birthDate.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Person WithBirthDate(in Corvus.Json.JsonDate value)
    {
        return this.SetProperty(JsonPropertyNames.BirthDate, value);
    }

    /// <summary>
    /// Sets familyName.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Person WithFamilyName(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.FamilyName, value);
    }

    /// <summary>
    /// Sets givenName.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Person WithGivenName(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.GivenName, value);
    }

    /// <summary>
    /// Sets height.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Person WithHeight(in Corvus.Json.JsonNumber value)
    {
        return this.SetProperty(JsonPropertyNames.Height, value);
    }

    /// <summary>
    /// Sets otherNames.
    /// </summary>
    /// <param name = "value">The value to set.</param>
    /// <returns>The entity with the updated property.</returns>
    public Person WithOtherNames(in Corvus.Json.JsonString value)
    {
        return this.SetProperty(JsonPropertyNames.OtherNames, value);
    }

    private static ValidationContext __CorvusValidateFamilyName(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateGivenName(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateOtherNames(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonString>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateBirthDate(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonDate>().Validate(validationContext, level);
    }

    private static ValidationContext __CorvusValidateHeight(in JsonObjectProperty property, in ValidationContext validationContext, ValidationLevel level)
    {
        return property.ValueAs<Corvus.Json.JsonNumber>().Validate(validationContext, level);
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
            else if (property.NameEquals(JsonPropertyNames.OtherNamesUtf8))
            {
                propertyValidator = __CorvusValidateOtherNames;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.BirthDateUtf8))
            {
                propertyValidator = __CorvusValidateBirthDate;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.HeightUtf8))
            {
                propertyValidator = __CorvusValidateHeight;
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
            else if (property.NameEquals(JsonPropertyNames.OtherNames))
            {
                propertyValidator = __CorvusValidateOtherNames;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.BirthDate))
            {
                propertyValidator = __CorvusValidateBirthDate;
                return true;
            }
            else if (property.NameEquals(JsonPropertyNames.Height))
            {
                propertyValidator = __CorvusValidateHeight;
                return true;
            }
        }

        propertyValidator = null;
        return false;
    }
}