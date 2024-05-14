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
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Internal;

namespace JsonSchemaSample.DatabaseApi;
/// <summary>
/// Generated from JSON Schema.
/// </summary>
public readonly partial struct DbCustomer
{
    private ValidationContext ValidateObject(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
    {
        ValidationContext result = validationContext;
        if (valueKind != JsonValueKind.Object)
        {
            return result;
        }

        int propertyCount = 0;
        bool foundFamilyName = false;
        bool foundGivenName = false;
        bool foundId = false;
        bool foundIdDescriptors = false;
        foreach (JsonObjectProperty property in this.EnumerateObject())
        {
            if (__TryGetCorvusLocalPropertiesValidator(property, this.HasJsonElementBacking, out ObjectPropertyValidator? propertyValidator))
            {
                result = result.WithLocalProperty(propertyCount);
                if (level > ValidationLevel.Basic)
                {
                    result = result.PushDocumentProperty("properties", property.Name.GetString());
                }

                var propertyResult = propertyValidator(property, result.CreateChildContext(), level);
                result = result.MergeResults(propertyResult.IsValid, level, propertyResult);
                if (level > ValidationLevel.Basic)
                {
                    result = result.PopLocation(); // property name
                }

                if (level == ValidationLevel.Flag && !result.IsValid)
                {
                    return result;
                }

                if ((this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.FamilyNameUtf8)) || (!this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.FamilyName)))
                {
                    foundFamilyName = true;
                }
                else if ((this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.GivenNameUtf8)) || (!this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.GivenName)))
                {
                    foundGivenName = true;
                }
                else if ((this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.IdUtf8)) || (!this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.Id)))
                {
                    foundId = true;
                }
                else if ((this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.IdDescriptorsUtf8)) || (!this.HasJsonElementBacking && property.NameEquals(JsonPropertyNames.IdDescriptors)))
                {
                    foundIdDescriptors = true;
                }
            }

            propertyCount++;
        }

        if (!foundFamilyName)
        {
            if (level >= ValidationLevel.Detailed)
            {
                result = result.WithResult(isValid: false, $"6.5.3. required - required property \"familyName\" not present.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, "6.5.3. required - required property not present.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

        if (!foundGivenName)
        {
            if (level >= ValidationLevel.Detailed)
            {
                result = result.WithResult(isValid: false, $"6.5.3. required - required property \"givenName\" not present.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, "6.5.3. required - required property not present.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

        if (!foundId)
        {
            if (level >= ValidationLevel.Detailed)
            {
                result = result.WithResult(isValid: false, $"6.5.3. required - required property \"id\" not present.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, "6.5.3. required - required property not present.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

        if (!foundIdDescriptors)
        {
            if (level >= ValidationLevel.Detailed)
            {
                result = result.WithResult(isValid: false, $"6.5.3. required - required property \"idDescriptors\" not present.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                result = result.WithResult(isValid: false, "6.5.3. required - required property not present.");
            }
            else
            {
                return result.WithResult(isValid: false);
            }
        }

        return result;
    }
}