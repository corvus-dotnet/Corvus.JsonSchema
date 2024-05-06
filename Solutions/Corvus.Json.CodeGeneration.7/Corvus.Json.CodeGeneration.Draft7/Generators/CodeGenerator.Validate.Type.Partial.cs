// <copyright file="CodeGenerator.Validate.Type.Partial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Draft7;
using Corvus.Json.JsonSchema.Draft7;

namespace Corvus.Json.CodeGeneration.Generators.Draft7;

/// <summary>
/// Services for the code generation t4 templates.
/// </summary>
public partial class CodeGeneratorValidateType
{
    private Dictionary<TypeDeclaration, Conversion>? conversions;

    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGeneratorValidateType"/> class.
    /// </summary>
    /// <param name="jsonSchemaBuilder">The current <see cref="JsonSchemaBuilder"/>.</param>
    /// <param name="declarationToGenerate">The <see cref="TypeDeclaration"/> to generate in this file.</param>
    public CodeGeneratorValidateType(JsonSchemaBuilder jsonSchemaBuilder, TypeDeclaration declarationToGenerate)
    {
        this.Builder = jsonSchemaBuilder;
        this.TypeDeclaration = declarationToGenerate;
    }

    /// <summary>
    /// Gets or sets the current schema builder.
    /// </summary>
    public JsonSchemaBuilder Builder { get; set; }

    /// <summary>
    /// Gets or sets the <see cref="TypeDeclaration"/> we are building.
    /// </summary>
    public TypeDeclaration TypeDeclaration { get; set; }

    /// <summary>
    /// Gets the formatted documentation for the type.
    /// </summary>
    public string FormattedTypeDocumentation => this.TypeDeclaration.FormatTypeDocumentation();

    /// <summary>
    /// Gets a value indicating whether this is an object type.
    /// </summary>
    public string Namespace => this.TypeDeclaration.Namespace!;

    /// <summary>
    /// Gets a value indicating whether or not this is any specific type.
    /// </summary>
    public bool IsNotImplicitType
    {
        get
        {
            return !(this.IsImplicitObject || this.IsImplicitArray || this.IsImplicitNumber || this.IsImplicitString || this.IsImplicitBoolean || this.HasConstNull);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an object type.
    /// </summary>
    public bool IsImplicitObject
    {
        get
        {
            return this.TypeDeclaration.Schema().IsObjectType() || this.HasProperties || this.Conversions.Any(c => c.IsObject) || this.HasConstObject;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an array type.
    /// </summary>
    public bool IsImplicitArray
    {
        get
        {
            return this.TypeDeclaration.Schema().IsArrayType() || this.Conversions.Any(c => c.IsArray) || this.HasConstArray;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is implicitly a number type.
    /// </summary>
    public bool IsImplicitNumber
    {
        get
        {
            return this.TypeDeclaration.Schema().IsNumberType() || this.Conversions.Any(c => c.IsNumber) || this.HasConstNumber;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is implicitly a string type.
    /// </summary>
    public bool IsImplicitString
    {
        get
        {
            return this.TypeDeclaration.Schema().IsStringType() || this.Conversions.Any(c => c.IsString) || this.HasConstString;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is implicitly a boolean type.
    /// </summary>
    public bool IsImplicitBoolean
    {
        get
        {
            return this.TypeDeclaration.Schema().IsBooleanType() || this.Conversions.Any(c => c.IsBoolean) || this.HasConstBoolean;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has properties.
    /// </summary>
    public bool HasProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Length > 0;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has local properties.
    /// </summary>
    public bool HasLocalProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Any(p => p.IsDefinedInLocalScope);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has local properties.
    /// </summary>
    public bool HasDefaults
    {
        get
        {
            return this.TypeDeclaration.Properties.Any(p => p.HasDefaultValue);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default value itself.
    /// </summary>
    public bool HasDefault
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has conversions.
    /// </summary>
    public bool HasConversions
    {
        get
        {
            return this.Conversions.Length > 0;
        }
    }

    /// <summary>
    /// Gets the array of <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> Properties
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets the array of required <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> RequiredProperties
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.Where(p => p.IsDefinedInLocalScope && p.IsRequired).OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets the array of optional <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> OptionalProperties
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.Where(p => p.IsDefinedInLocalScope && !p.IsRequired).OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets the array of required <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> RequiredAllOfAndRefProperties
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.Where(p => p.IsRequired).OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets the array of optional <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> OptionalAllOfAndRefProperties
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.Where(p => !p.IsRequired).OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets the array of properties with default values.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> Defaults
    {
        get
        {
            return [.. this.TypeDeclaration.Properties.Where(p => p.HasDefaultValue).OrderBy(p => p.DotnetPropertyName)];
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant string value.
    /// </summary>
    public bool HasConstString
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.String;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant boolean value.
    /// </summary>
    public bool HasConstBoolean
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.True || this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant number value.
    /// </summary>
    public bool HasConstNumber
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.Number;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant object value.
    /// </summary>
    public bool HasConstObject
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant array value.
    /// </summary>
    public bool HasConstArray
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.Array;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a constant null value.
    /// </summary>
    public bool HasConstNull
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.ValueKind == JsonValueKind.Null;
        }
    }

    /// <summary>
    /// Gets a serialized string value.
    /// </summary>
    public string ConstString
    {
        get
        {
            if (this.HasConstString)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Const);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized boolean value.
    /// </summary>
    public string ConstBoolean
    {
        get
        {
            if (this.HasConstBoolean)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Const);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized number value.
    /// </summary>
    public string ConstNumber
    {
        get
        {
            if (this.HasConstNumber)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Const);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized object value.
    /// </summary>
    public string ConstObject
    {
        get
        {
            if (this.HasConstObject)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Const);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized array value.
    /// </summary>
    public string ConstArray
    {
        get
        {
            if (this.HasConstArray)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Const);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default string value.
    /// </summary>
    public bool HasDefaultString
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.String;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default boolean value.
    /// </summary>
    public bool HasDefaultBoolean
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.True || this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default number value.
    /// </summary>
    public bool HasDefaultNumber
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.Number;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default object value.
    /// </summary>
    public bool HasDefaultObject
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default array value.
    /// </summary>
    public bool HasDefaultArray
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.Array;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a default null value.
    /// </summary>
    public bool HasDefaultNull
    {
        get
        {
            return this.TypeDeclaration.Schema().Default.ValueKind == JsonValueKind.Null;
        }
    }

    /// <summary>
    /// Gets a serialized string value.
    /// </summary>
    public string DefaultString
    {
        get
        {
            if (this.HasDefaultString)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Default);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized boolean value.
    /// </summary>
    public string DefaultBoolean
    {
        get
        {
            if (this.HasDefaultBoolean)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Default);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized number value.
    /// </summary>
    public string DefaultNumber
    {
        get
        {
            if (this.HasDefaultNumber)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Default);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized object value.
    /// </summary>
    public string DefaultObject
    {
        get
        {
            if (this.HasDefaultObject)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Default);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a serialized array value.
    /// </summary>
    public string DefaultArray
    {
        get
        {
            if (this.HasDefaultArray)
            {
                return GetRawTextAsQuotedString(this.TypeDeclaration.Schema().Default);
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a pattern.
    /// </summary>
    public bool HasPattern
    {
        get
        {
            return this.TypeDeclaration.Schema().Pattern.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the Regex pattern.
    /// </summary>
    public string Pattern
    {
        get
        {
            if (this.HasPattern)
            {
                return (string)this.TypeDeclaration.Schema().Pattern;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a DependentRequired constraint.
    /// </summary>
    public bool HasDependentRequired
    {
        get
        {
            return this.TypeDeclaration.Schema().Dependencies.IsNotUndefined() && this.TypeDeclaration.Schema().Dependencies.EnumerateObject().Any(t => t.Value.ValueKind == JsonValueKind.Array);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a DependentSchema constraint.
    /// </summary>
    public bool HasDependentSchemas
    {
        get
        {
            return this.TypeDeclaration.Schema().Dependencies.IsNotUndefined() && this.TypeDeclaration.Schema().Dependencies.EnumerateObject().Any(t => t.Value.As<Schema>().IsValid());
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a PatternProperties constraint.
    /// </summary>
    public bool HasPatternProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().PatternProperties.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an explicit type.
    /// </summary>
    public bool HasExplicitType
    {
        get
        {
            return this.TypeDeclaration.Schema().Type.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a format.
    /// </summary>
    public bool HasFormat
    {
        get
        {
            return this.TypeDeclaration.Schema().Format.IsNotUndefined() || this.TypeDeclaration.Schema().ContentEncoding.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a media type or encoding.
    /// </summary>
    public bool HasMediaTypeOrEncoding
    {
        get
        {
            return this.TypeDeclaration.Schema().ContentMediaType.IsNotUndefined() || this.TypeDeclaration.Schema().ContentEncoding.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a const value.
    /// </summary>
    public bool HasConst
    {
        get
        {
            return this.TypeDeclaration.Schema().Const.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an enum value.
    /// </summary>
    public bool HasEnum
    {
        get
        {
            return this.TypeDeclaration.Schema().Enum.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the array of enum values.
    /// </summary>
    public ImmutableArray<EnumValue> EnumValues
    {
        get
        {
            ImmutableArray<EnumValue>.Builder builder = ImmutableArray.CreateBuilder<EnumValue>();
            if (this.TypeDeclaration.Schema().Enum.IsNotUndefined())
            {
                foreach (JsonAny value in this.TypeDeclaration.Schema().Enum.EnumerateArray())
                {
                    builder.Add(new EnumValue(value));
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a multipleOf constraint.
    /// </summary>
    public bool HasMultipleOf
    {
        get
        {
            return this.TypeDeclaration.Schema().MultipleOf.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an exclusiveMaximum constraint.
    /// </summary>
    public bool HasExclusiveMaximum
    {
        get
        {
            return this.TypeDeclaration.Schema().ExclusiveMaximum.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a maximum constraint.
    /// </summary>
    public bool HasMaximum
    {
        get
        {
            return this.TypeDeclaration.Schema().Maximum.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a maxItems constraint.
    /// </summary>
    public bool HasMaxItems
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxItems.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minItems constraint.
    /// </summary>
    public bool HasMinItems
    {
        get
        {
            return this.TypeDeclaration.Schema().MinItems.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a maxItems constraint.
    /// </summary>
    public long MaxItems
    {
        get
        {
            return this.HasMaxItems ? (long)this.TypeDeclaration.Schema().MaxItems : default;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minItems constraint.
    /// </summary>
    public long MinItems
    {
        get
        {
            return this.HasMinItems ? (long)this.TypeDeclaration.Schema().MinItems : default;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an exclusiveMinimum constraint.
    /// </summary>
    public bool HasExclusiveMinimum
    {
        get
        {
            return this.TypeDeclaration.Schema().ExclusiveMinimum.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minimum constraint.
    /// </summary>
    public bool HasMinimum
    {
        get
        {
            return this.TypeDeclaration.Schema().Minimum.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a multipleOf constraint.
    /// </summary>
    public string MultipleOf
    {
        get
        {
            if (this.TypeDeclaration.Schema().MultipleOf.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().MultipleOf.AsJsonElement.TryGetDouble(out double _))
                {
                    return this.TypeDeclaration.Schema().MultipleOf.AsJsonElement.GetRawText();
                }

                // Fall back to a decimal
                return $"{this.TypeDeclaration.Schema().MultipleOf.AsJsonElement.GetRawText()}M";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets an exclusiveMaximum constraint.
    /// </summary>
    public string ExclusiveMaximum
    {
        get
        {
            if (this.TypeDeclaration.Schema().ExclusiveMaximum.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().ExclusiveMaximum.AsJsonElement.TryGetDouble(out double _))
                {
                    return this.TypeDeclaration.Schema().ExclusiveMaximum.AsJsonElement.GetRawText();
                }

                // Fall back to a decimal
                return $"{this.TypeDeclaration.Schema().MultipleOf.AsJsonElement.GetRawText()}M";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a maximum constraint.
    /// </summary>
    public string Maximum
    {
        get
        {
            if (this.TypeDeclaration.Schema().Maximum.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().Maximum.AsJsonElement.TryGetDouble(out double _))
                {
                    return this.TypeDeclaration.Schema().Maximum.AsJsonElement.GetRawText();
                }

                // Fall back to a decimal
                return $"{this.TypeDeclaration.Schema().Maximum.AsJsonElement.GetRawText()}M";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets an exclusiveMinimum constraint.
    /// </summary>
    public string ExclusiveMinimum
    {
        get
        {
            if (this.TypeDeclaration.Schema().ExclusiveMinimum.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().ExclusiveMinimum.AsJsonElement.TryGetDouble(out double _))
                {
                    return this.TypeDeclaration.Schema().ExclusiveMinimum.AsJsonElement.GetRawText();
                }

                // Fall back to a decimal
                return $"{this.TypeDeclaration.Schema().ExclusiveMinimum.AsJsonElement.GetRawText()}M";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a minimum constraint.
    /// </summary>
    public string Minimum
    {
        get
        {
            if (this.TypeDeclaration.Schema().Minimum.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().Minimum.AsJsonElement.TryGetDouble(out double _))
                {
                    return this.TypeDeclaration.Schema().Minimum.AsJsonElement.GetRawText();
                }

                // Fall back to a decimal
                return $"{this.TypeDeclaration.Schema().Minimum.AsJsonElement.GetRawText()}M";
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a maxLength constraint.
    /// </summary>
    public bool HasMaxLength
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxLength.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minLength constraint.
    /// </summary>
    public bool HasMinLength
    {
        get
        {
            return this.TypeDeclaration.Schema().MinLength.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a maxLength constraint.
    /// </summary>
    public string MaxLength
    {
        get
        {
            if (this.TypeDeclaration.Schema().MaxLength.IsNotUndefined())
            {
                return ((long)this.TypeDeclaration.Schema().MaxLength).ToString();
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a minLength constraint.
    /// </summary>
    public string MinLength
    {
        get
        {
            if (this.TypeDeclaration.Schema().MinLength.IsNotUndefined())
            {
                return ((long)this.TypeDeclaration.Schema().MinLength).ToString();
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an if/then/else constraint.
    /// </summary>
    public bool HasIfThenElse
    {
        get
        {
            return this.TypeDeclaration.Schema().If.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a not constraint.
    /// </summary>
    public bool HasNot
    {
        get
        {
            return this.TypeDeclaration.Schema().Not.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an allOf constraint.
    /// </summary>
    public bool HasAllOf
    {
        get
        {
            return this.TypeDeclaration.Schema().AllOf.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an anyOf constraint.
    /// </summary>
    public bool HasAnyOf
    {
        get
        {
            return this.TypeDeclaration.Schema().AnyOf.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an oneOf constraint.
    /// </summary>
    public bool HasOneOf
    {
        get
        {
            return this.TypeDeclaration.Schema().OneOf.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a required constraint.
    /// </summary>
    public bool HasRequired
    {
        get
        {
            return this.TypeDeclaration.Schema().Required.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a non-naked reference.
    /// </summary>
    public bool HasRef
    {
        get
        {
            return this.TypeDeclaration.Schema().Ref.IsNotUndefined() && !this.TypeDeclaration.Schema().IsNakedReference();
        }
    }

    /// <summary>
    /// Gets the dotnet type name for the non-naked reference.
    /// </summary>
    public string RefDotnetTypeName
    {
        get
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "$ref");
            return td.FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a maxProperties constraint.
    /// </summary>
    public bool HasMaxProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxProperties.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minProperties constraint.
    /// </summary>
    public bool HasMinProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MinProperties.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a propertyNames constraint.
    /// </summary>
    public bool HasPropertyNames
    {
        get
        {
            return this.TypeDeclaration.Schema().PropertyNames.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object allows additional properties.
    /// </summary>
    public bool AllowsAdditionalProperties
    {
        get
        {
            if (this.TypeDeclaration.Schema().AdditionalProperties.IsNullOrUndefined())
            {
                return true;
            }

            TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "additionalProperties");
            return typeDeclaration.Schema().IsUndefined() ||
                typeDeclaration.Schema().ValueKind == JsonValueKind.True ||
                typeDeclaration.Schema().ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has explicit additionalProperties.
    /// </summary>
    public bool HasAdditionalProperties
    {
        get
        {
            if (this.TypeDeclaration.Schema().AdditionalProperties.IsNullOrUndefined())
            {
                return false;
            }

            TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "additionalProperties");
            return typeDeclaration.Schema().ValueKind == JsonValueKind.Object ||
                typeDeclaration.Schema().ValueKind == JsonValueKind.True ||
                typeDeclaration.Schema().ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has explicit additionalProperties as a schema object.
    /// </summary>
    public bool HasAdditionalPropertiesObject
    {
        get
        {
            if (this.TypeDeclaration.Schema().AdditionalProperties.IsNullOrUndefined())
            {
                return false;
            }

            TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "additionalProperties");

            return typeDeclaration.Schema().ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has an items constraint.
    /// </summary>
    public bool HasItems
    {
        get
        {
            return this.TypeDeclaration.Schema().Items.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has explicit validation for additional items.
    /// </summary>
    public bool HasAdditionalItems
    {
        get
        {
            return this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.Object ||
                 this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.True ||
                 this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object allows additional items.
    /// </summary>
    public bool AllowsAdditionalItems
    {
        get
        {
            return this.TypeDeclaration.Schema().AdditionalItems.IsUndefined() ||
                this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.True ||
                this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has explicit validation for additional items.
    /// </summary>
    public bool HasAdditionalItemsSchema
    {
        get
        {
            return this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.Object;
        }
    }

    /// <summary>
    /// Gets the additional items dotnet type name.
    /// </summary>
    public string AdditionalItemsDotnetTypeName
    {
        get
        {
            if (this.TypeDeclaration.Schema().AdditionalItems.IsNotUndefined())
            {
                TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "additionalItems");
                return td.FullyQualifiedDotnetTypeName ?? string.Empty;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has a uniqueItems constraint.
    /// </summary>
    public bool HasUniqueItems
    {
        get
        {
            return this.TypeDeclaration.Schema().UniqueItems.ValueKind == JsonValueKind.True;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has a contains constraint.
    /// </summary>
    public bool HasContains
    {
        get
        {
            return this.TypeDeclaration.Schema().Contains.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this object has a contains constraint.
    /// </summary>
    public string ContainsDotnetTypeName
    {
        get
        {
            if (this.TypeDeclaration.Schema().Contains.IsNotUndefined())
            {
                TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "contains");
                return typeDeclaration.FullyQualifiedDotnetTypeName!;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets the pattern properties for the type.
    /// </summary>
    public ImmutableArray<PatternProperty> PatternProperties
    {
        get
        {
            ImmutableArray<PatternProperty>.Builder builder = ImmutableArray.CreateBuilder<PatternProperty>();
            if (this.TypeDeclaration.Schema().PatternProperties.IsNotUndefined())
            {
                foreach (JsonObjectProperty property in this.TypeDeclaration.Schema().PatternProperties.EnumerateObject())
                {
                    string name = property.Name.GetString();
                    TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForPatternProperty(this.TypeDeclaration, name);
                    builder.Add(new PatternProperty(name, typeDeclaration.FullyQualifiedDotnetTypeName!));
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets the dependent schema for the type.
    /// </summary>
    public ImmutableArray<DependentSchema> DependentSchemas
    {
        get
        {
            ImmutableArray<DependentSchema>.Builder builder = ImmutableArray.CreateBuilder<DependentSchema>();
            if (this.TypeDeclaration.Schema().Dependencies.IsNotUndefined())
            {
                foreach (JsonObjectProperty property in this.TypeDeclaration.Schema().Dependencies.EnumerateObject())
                {
                    if (property.Value.As<Schema>().IsValid())
                    {
                        string name = property.Name.GetString();
                        builder.Add(new DependentSchema(name, this.Builder.GetTypeDeclarationForDependentSchema(this.TypeDeclaration, name).FullyQualifiedDotnetTypeName!));
                    }
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets the dependent required values for the type.
    /// </summary>
    public ImmutableArray<DependentRequiredValue> DependentRequired
    {
        get
        {
            ImmutableArray<DependentRequiredValue>.Builder builder = ImmutableArray.CreateBuilder<DependentRequiredValue>();
            if (this.TypeDeclaration.Schema().Dependencies.IsNotUndefined())
            {
                foreach (JsonObjectProperty property in this.TypeDeclaration.Schema().Dependencies.EnumerateObject())
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        ImmutableArray<string>.Builder innerBuilder = ImmutableArray.CreateBuilder<string>();
                        foreach (JsonAny item in property.Value.AsArray.EnumerateArray())
                        {
                            innerBuilder.Add((string)item.AsString);
                        }

                        builder.Add(new DependentRequiredValue(property.Name.GetString(), innerBuilder.ToImmutable()));
                    }
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets the collection of local properties.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> LocalProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => p.IsDefinedInLocalScope).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type has a single items type constraint.
    /// </summary>
    public bool HasSingleItemsType
    {
        get
        {
            return this.TypeDeclaration.Schema().Items.As<Schema>().IsValid();
        }
    }

    /// <summary>
    /// Gets a value indicating whether the type has multiple items type constraints.
    /// </summary>
    public bool HasMultipleItemsType
    {
        get
        {
            return this.TypeDeclaration.Schema().Items.IsSchemaArray;
        }
    }

    /// <summary>
    /// Gets the array of dotnet type names when the items constraint is an array.
    /// </summary>
    public ImmutableArray<string> PrefixItems => this.Items;

    /// <summary>
    /// Gets the array of dotnet type names when the items constraint is an array.
    /// </summary>
    public ImmutableArray<string> Items
    {
        get
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            if (this.TypeDeclaration.Schema().Items.IsSchemaArray)
            {
                for (int i = 0; i < this.TypeDeclaration.Schema().Items.GetArrayLength(); ++i)
                {
                    TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(this.TypeDeclaration, "items", i);
                    builder.Add(td.FullyQualifiedDotnetTypeName ?? string.Empty);
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets a value indicating whether we can enumerate this type as a single items type.
    /// </summary>
    public bool CanEnumerateAsSpecificType
    {
        get
        {
            return this.HasSingleItemsType && this.SingleItemsDotnetTypeName != $"{BuiltInTypes.AnyTypeDeclaration.Ns}.{BuiltInTypes.AnyTypeDeclaration.Type}";
        }
    }

    /// <summary>
    /// Gets a value indicating whether this array represents a tuple.
    /// </summary>
    public bool IsTuple
    {
        get
        {
            return this.TypeDeclaration.Schema().Items.IsSchemaArray && this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets the dotnet type name for the items constraint when it is a single schema.
    /// </summary>
    public string SingleItemsDotnetTypeName
    {
        get
        {
            if (this.TypeDeclaration.Schema().Items.IsNotUndefined())
            {
                if (this.TypeDeclaration.Schema().Items.ValueKind == JsonValueKind.Object)
                {
                    TypeDeclaration itemsType = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "items");
                    return itemsType.FullyQualifiedDotnetTypeName ?? string.Empty;
                }

                if (this.TypeDeclaration.Schema().Items.ValueKind == JsonValueKind.True)
                {
                    return $"{BuiltInTypes.AnyTypeDeclaration.Ns}.{BuiltInTypes.AnyTypeDeclaration.Type}";
                }

                if (this.TypeDeclaration.Schema().Items.ValueKind == JsonValueKind.False)
                {
                    return $"{BuiltInTypes.NotAnyTypeDeclaration.Ns}.{BuiltInTypes.NotAnyTypeDeclaration.Type}";
                }
            }

            return $"{BuiltInTypes.AnyTypeDeclaration.Ns}.{BuiltInTypes.AnyTypeDeclaration.Type}";
        }
    }

    /// <summary>
    /// Gets the dimension of the array. This will be zero if the type is not an array.
    /// </summary>
    public int ArrayDimension
    {
        get
        {
            return this.GetArrayDimension(this.TypeDeclaration);
        }
    }

    /// <summary>
    /// Gets the item type of the ultimate type of the item in a multi-dimensional array.
    /// </summary>
    public string MultiDimensionalArrayItemType
    {
        get
        {
            return this.GetMultiDimensionalArrayItemType(this.TypeDeclaration);
        }
    }

    /// <summary>
    /// Gets the propertyNames schema dotnet type name.
    /// </summary>
    public string PropertyNamesDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "propertyNames").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the additionalProperties schema dotnet type name.
    /// </summary>
    public string AdditionalPropertiesDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "additionalProperties").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the maxProperties constraint.
    /// </summary>
    public long MaxProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxProperties.IsNotUndefined() ? (long)this.TypeDeclaration.Schema().MaxProperties : default;
        }
    }

    /// <summary>
    /// Gets the minProperties constraint.
    /// </summary>
    public long MinProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MinProperties.IsNotUndefined() ? (long)this.TypeDeclaration.Schema().MinProperties : default;
        }
    }

    /// <summary>
    /// Gets an array of dotnet type names for the oneOf constraint.
    /// </summary>
    public ImmutableArray<string> OneOf
    {
        get
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            if (this.TypeDeclaration.Schema().OneOf.IsNotUndefined())
            {
                for (int i = 0; i < this.TypeDeclaration.Schema().OneOf.GetArrayLength(); ++i)
                {
                    TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(this.TypeDeclaration, "oneOf", i);
                    builder.Add(td.FullyQualifiedDotnetTypeName!);
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets an array of dotnet type names for the anyOf constraint.
    /// </summary>
    public ImmutableArray<string> AnyOf
    {
        get
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            if (this.TypeDeclaration.Schema().AnyOf.IsNotUndefined())
            {
                for (int i = 0; i < this.TypeDeclaration.Schema().AnyOf.GetArrayLength(); ++i)
                {
                    TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(this.TypeDeclaration, "anyOf", i);
                    builder.Add(td.FullyQualifiedDotnetTypeName!);
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets an array of dotnet type names for the allOf constraint.
    /// </summary>
    public ImmutableArray<string> AllOf
    {
        get
        {
            ImmutableArray<string>.Builder builder = ImmutableArray.CreateBuilder<string>();
            if (this.TypeDeclaration.Schema().AllOf.IsNotUndefined())
            {
                for (int i = 0; i < this.TypeDeclaration.Schema().AllOf.GetArrayLength(); ++i)
                {
                    TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(this.TypeDeclaration, "allOf", i);
                    builder.Add(td.FullyQualifiedDotnetTypeName!);
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets the implicit conversions appropriate for this type that require a constructor.
    /// </summary>
    public ImmutableArray<Conversion> DirectConversions
    {
        get
        {
            if (this.conversions is null)
            {
                this.conversions = new Dictionary<TypeDeclaration, Conversion>();
                this.AddConversionsFor(this.TypeDeclaration, this.conversions, null);
            }

            // Get the conversions that require a constructor
            return this.conversions.Where(t => t.Value.IsDirect).Select(t => t.Value).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the implicit conversions appropriate for this type that require a cast.
    /// </summary>
    public ImmutableArray<Conversion> Conversions
    {
        get
        {
            if (this.conversions is null)
            {
                this.conversions = new Dictionary<TypeDeclaration, Conversion>();
                this.AddConversionsFor(this.TypeDeclaration, this.conversions, null);
            }

            // Select the conversions that require a cast through another type
            return this.conversions.Select(t => t.Value).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the not schema dotnet type name.
    /// </summary>
    public string NotDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "not").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the if schema dotnet type name.
    /// </summary>
    public string IfFullyQualifiedDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "if").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the if schema dotnet type name.
    /// </summary>
    public string IfDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "if").DotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the schema has a then constraint.
    /// </summary>
    public bool HasThen
    {
        get
        {
            return this.TypeDeclaration.Schema().Then.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the fully qualified then schema dotnet type name.
    /// </summary>
    public string ThenFullyQualifiedDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "then").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the then schema dotnet type name.
    /// </summary>
    public string ThenDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "then").DotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether the schema has an else constraint.
    /// </summary>
    public bool HasElse
    {
        get
        {
            return this.TypeDeclaration.Schema().Else.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the fully qualified else schema dotnet type name.
    /// </summary>
    public string ElseFullyQualifiedDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "else").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the else schema dotnet type name.
    /// </summary>
    public string ElseDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "else").DotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is JsonBase64 encoded content.
    /// </summary>
    public bool IsJsonBase64Content
    {
        get
        {
            if (this.TypeDeclaration.Schema().ContentMediaType.IsNotUndefined() && this.TypeDeclaration.Schema().ContentEncoding.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().ContentMediaType == "application/json" && this.TypeDeclaration.Schema().ContentEncoding == "base64";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is JsonBase64 encoded content.
    /// </summary>
    public bool IsJsonBase64String
    {
        get
        {
            if (this.TypeDeclaration.Schema().ContentMediaType.IsUndefined() && this.TypeDeclaration.Schema().ContentEncoding.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().ContentEncoding == "base64";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is Json content.
    /// </summary>
    public bool IsJsonContent
    {
        get
        {
            if (this.TypeDeclaration.Schema().ContentMediaType.IsNotUndefined() && this.TypeDeclaration.Schema().ContentEncoding.IsUndefined())
            {
                return this.TypeDeclaration.Schema().ContentMediaType == "application/json";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is date.
    /// </summary>
    public bool IsJsonDate
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "date";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a date-time.
    /// </summary>
    public bool IsJsonDateTime
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "date-time";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a duration.
    /// </summary>
    public bool IsJsonDuration
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "duration";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a time.
    /// </summary>
    public bool IsJsonTime
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "time";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an email.
    /// </summary>
    public bool IsJsonEmail
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "email";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a duration.
    /// </summary>
    public bool IsJsonHostname
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "hostname";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an idn-email.
    /// </summary>
    public bool IsJsonIdnEmail
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "idn-email";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an idn-hostname.
    /// </summary>
    public bool IsJsonIdnHostname
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "idn-hostname";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an integer.
    /// </summary>
    public bool IsJsonInteger
    {
        get
        {
            return this.TypeDeclaration.Schema().IsJsonInteger() || BuiltInTypes.IsIntegerFormat(this.TypeDeclaration.Schema().Format.GetString());
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a byte.
    /// </summary>
    public bool IsJsonByte
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToByte(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an int16.
    /// </summary>
    public bool IsJsonInt16
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToInt16(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an int32.
    /// </summary>
    public bool IsJsonInt32
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToInt32(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an int64.
    /// </summary>
    public bool IsJsonInt64
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToInt64(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an int128.
    /// </summary>
    public bool IsJsonInt128
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToInt128(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an sbyte.
    /// </summary>
    public bool IsJsonSByte
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToSByte(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a uint16.
    /// </summary>
    public bool IsJsonUInt16
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToUInt16(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a uint32.
    /// </summary>
    public bool IsJsonUInt32
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToUInt32(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a uint64.
    /// </summary>
    public bool IsJsonUInt64
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToUInt64(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a uint128.
    /// </summary>
    public bool IsJsonUInt128
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToUInt128(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a double.
    /// </summary>
    public bool IsJsonDouble
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToDouble(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a decimal.
    /// </summary>
    public bool IsJsonDecimal
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToDecimal(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a half.
    /// </summary>
    public bool IsJsonHalf
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToHalf(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a single.
    /// </summary>
    public bool IsJsonSingle
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return BuiltInTypes.ImplicitConversionToSingle(this.TypeDeclaration.Schema().Format.GetString());
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the implied format for this type, following the reference hierarchy.
    /// </summary>
    public string? ImpliedFormat => this.GetImpliedFormat(this.TypeDeclaration);

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to a byte format.
    /// </summary>
    public string ConversionOperatorToByte => BuiltInTypes.ImplicitConversionToByte(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an int16 format.
    /// </summary>
    public string ConversionOperatorToInt16 => BuiltInTypes.ImplicitConversionToInt16(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an int32 format.
    /// </summary>
    public string ConversionOperatorToInt32 => BuiltInTypes.ImplicitConversionToInt32(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an int64 format.
    /// </summary>
    public string ConversionOperatorToInt64 => BuiltInTypes.ImplicitConversionToInt64(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an int128 format.
    /// </summary>
    public string ConversionOperatorToInt128 => BuiltInTypes.ImplicitConversionToInt128(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an sbyte format.
    /// </summary>
    public string ConversionOperatorToSByte => BuiltInTypes.ImplicitConversionToSByte(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an uint16 format.
    /// </summary>
    public string ConversionOperatorToUInt16 => BuiltInTypes.ImplicitConversionToUInt16(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an uint32 format.
    /// </summary>
    public string ConversionOperatorToUInt32 => BuiltInTypes.ImplicitConversionToUInt32(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an uint64 format.
    /// </summary>
    public string ConversionOperatorToUInt64 => BuiltInTypes.ImplicitConversionToUInt64(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an uint128 format.
    /// </summary>
    public string ConversionOperatorToUInt128 => BuiltInTypes.ImplicitConversionToUInt128(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an double format.
    /// </summary>
    public string ConversionOperatorToDouble => BuiltInTypes.ImplicitConversionToDouble(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to an decimal format.
    /// </summary>
    public string ConversionOperatorToDecimal => BuiltInTypes.ImplicitConversionToDecimal(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to a half format.
    /// </summary>
    public string ConversionOperatorToHalf => BuiltInTypes.ImplicitConversionToHalf(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this can implicitly convert to a single format.
    /// </summary>
    public string ConversionOperatorToSingle => BuiltInTypes.ImplicitConversionToSingle(this.ImpliedFormat) ? "implicit" : "explicit";

    /// <summary>
    /// Gets a value indicating whether this is an IPV4 address.
    /// </summary>
    public bool IsJsonIpV4
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "ipv4";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an IPV6 address.
    /// </summary>
    public bool IsJsonIpV6
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "ipv6";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an IRI.
    /// </summary>
    public bool IsJsonIri
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "iri";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is an IRI Reference.
    /// </summary>
    public bool IsJsonIriReference
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "iri-reference";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a Json Pointer.
    /// </summary>
    public bool IsJsonPointer
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "json-pointer";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a Regex.
    /// </summary>
    public bool IsJsonRegex
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "regex";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a Relative Json Pointer.
    /// </summary>
    public bool IsJsonRelativePointer
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "relative-json-pointer";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a URI.
    /// </summary>
    public bool IsJsonUri
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "uri";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a URI Reference.
    /// </summary>
    public bool IsJsonUriReference
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "uri-reference";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a URI template.
    /// </summary>
    public bool IsJsonUriTemplate
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "uri-template";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this is a UUID.
    /// </summary>
    public bool IsJsonUuid
    {
        get
        {
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "uuid";
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an explicit type constraint.
    /// </summary>
    public bool HasType
    {
        get
        {
            return this.TypeDeclaration.Schema().Type.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a string type.
    /// </summary>
    public bool HasStringType
    {
        get
        {
            return this.MatchType("string");
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an object type.
    /// </summary>
    public bool HasObjectType
    {
        get
        {
            return this.MatchType("object");
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an array type.
    /// </summary>
    public bool HasArrayType
    {
        get
        {
            return this.MatchType("array");
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a number type.
    /// </summary>
    public bool HasNumberType
    {
        get
        {
            return this.MatchType("number");
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has an integer type.
    /// </summary>
    public bool HasIntegerType
    {
        get
        {
            return this.MatchType("integer") || (this.MatchType("number") && (this.TypeDeclaration.Schema().Format == "int32" || this.TypeDeclaration.Schema().Format == "int64"));
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a boolean type.
    /// </summary>
    public bool HasBooleanType
    {
        get
        {
            return this.MatchType("boolean");
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a null type.
    /// </summary>
    public bool HasNullType
    {
        get
        {
            return this.MatchType("null");
        }
    }

    /// <summary>
    /// Emits code for the UTF8 encoded byte array for the given string.
    /// </summary>
    /// <param name="name">The string to encode.</param>
    /// <returns>The emitted code for a byte array representing the UTF8 encoded string.</returns>
    public static string GetEncodedBytes(string name)
    {
        return string.Join(", ", System.Text.Encoding.UTF8.GetBytes(name).Select(b => b.ToString()));
    }

    /// <summary>
    /// Determines whether a particular child type declaration is a constant value.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration to check.</param>
    /// <returns><c>True</c> if the type declaration represents a const value.</returns>
    public static bool IsConst(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.Schema().Const.IsNotUndefined();
    }

    /// <summary>
    /// Emits the nested-type header and sets up the tab indent.
    /// </summary>
    public void BeginNesting()
    {
        Stack<string> parentTypeNames = new();

        TypeDeclaration? current = this.TypeDeclaration.Parent;

        // We need to reverse the order, so we push them onto a stack...
        while (current is not null)
        {
            parentTypeNames.Push(current.DotnetTypeName!);
            current = current.Parent;
        }

        // ...and then pop them off again.
        while (parentTypeNames.Count > 0)
        {
            string name = parentTypeNames.Pop();
            this.WriteLine($"public readonly partial struct {name}");
            this.WriteLine("{");
            this.PushIndent("\r\n");
        }
    }

    /// <summary>
    /// Emits the nested-type header and sets up the tab indent.
    /// </summary>
    public void EndNesting()
    {
        TypeDeclaration? current = this.TypeDeclaration.Parent;
        while (current is not null)
        {
            this.PopIndent();
            this.WriteLine("}");
            current = current.Parent;
        }
    }

    /// <summary>
    /// Gets the suffix to use for the pattern property.
    /// </summary>
    /// <param name="patternProperty">The pattern property for which to determine the suffix.</param>
    /// <returns>The suffix for the given property.</returns>
    /// <remarks>
    /// The suffix will be the index + 1 if there are multiple patterns that resolve to the same type. Otherwise, it will be the simple type name to which it resolves.
    /// </remarks>
    public string PatternPropertySuffix(PatternProperty patternProperty)
    {
        if (this.PatternProperties.Count(p => p.DotnetTypeName == patternProperty.DotnetTypeName) == 1)
        {
            return patternProperty.DotnetTypeName[(patternProperty.DotnetTypeName.LastIndexOf('.') + 1)..];
        }

        return (this.PatternProperties.IndexOf(patternProperty) + 1).ToString();
    }

    /// <summary>
    /// Gets the implied format for this type, following the reference hierarchy.
    /// </summary>
    /// <param name="typeDeclaration">The type declaration for which to get the implied format.</param>
    /// <returns>The implied format, if any.</returns>
    public string? GetImpliedFormat(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.Schema().Format.IsNotUndefined())
        {
            return typeDeclaration.Schema().Format.GetString();
        }

        if (typeDeclaration.Schema().Ref.IsNotUndefined() && !typeDeclaration.Schema().IsNakedReference())
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "$ref");
            return this.GetImpliedFormat(td);
        }

        return string.Empty;
    }

    private static string GetRawTextAsQuotedString(JsonAny? value)
    {
        if (value is JsonAny actualValue)
        {
            return Formatting.FormatLiteralOrNull(actualValue.AsJsonElement.GetRawText(), true);
        }

        throw new ArgumentNullException(nameof(value));
    }

    private static string GetRawStringValueAsQuotedString(JsonAny? value)
    {
        if (value is JsonAny actualValue && actualValue.ValueKind == JsonValueKind.String)
        {
            return Formatting.FormatLiteralOrNull(actualValue.AsJsonElement.GetRawText()[1..^1], true);
        }

        throw new ArgumentNullException(nameof(value));
    }

    private int GetArrayDimension(TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.Schema().IsArrayType())
        {
            return 0;
        }

        if (typeDeclaration.Schema().Items.IsNotUndefined())
        {
            if (typeDeclaration.Schema().Items.ValueKind == JsonValueKind.Object)
            {
                // This could be an array, so we will recurse
                TypeDeclaration itemsType = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "items");
                return 1 + this.GetArrayDimension(itemsType);
            }
        }

        return 1;
    }

    private string GetMultiDimensionalArrayItemType(TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.Schema().IsArrayType())
        {
            return typeDeclaration.FullyQualifiedDotnetTypeName!;
        }

        if (typeDeclaration.Schema().Items.IsNotUndefined())
        {
            TypeDeclaration itemsType = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "items");
            return this.GetMultiDimensionalArrayItemType(itemsType);
        }

        return $"{BuiltInTypes.AnyTypeDeclaration.Ns}.{BuiltInTypes.AnyTypeDeclaration.Type}";
    }

    private bool MatchType(string typeToMatch)
    {
        if (this.TypeDeclaration.Schema().Type.IsNotUndefined())
        {
            if (this.TypeDeclaration.Schema().Type.IsSimpleTypes)
            {
                return this.TypeDeclaration.Schema().Type.AsSimpleTypes == typeToMatch;
            }
            else if (this.TypeDeclaration.Schema().Type.IsSimpleTypesArray)
            {
                return this.TypeDeclaration.Schema().Type.AsSimpleTypesArray.EnumerateArray().Any(t => t.AsString == typeToMatch);
            }
        }

        return false;
    }

    private void AddConversionsFor(TypeDeclaration typeDeclaration, Dictionary<TypeDeclaration, Conversion> conversions, TypeDeclaration? parent)
    {
        // First, look in the allOfs
        if (typeDeclaration.Schema().AllOf.IsNotUndefined())
        {
            for (int i = 0; i < typeDeclaration.Schema().AllOf.GetArrayLength(); ++i)
            {
                TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(typeDeclaration, "allOf", i);

                if (!conversions.ContainsKey(td))
                {
                    conversions.Add(td, new Conversion(td, parent is null));
                    this.AddConversionsFor(td, conversions, typeDeclaration);
                }
            }
        }

        if (typeDeclaration.Schema().AnyOf.IsNotUndefined())
        {
            for (int i = 0; i < typeDeclaration.Schema().AnyOf.GetArrayLength(); ++i)
            {
                TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(typeDeclaration, "anyOf", i);

                if (!conversions.ContainsKey(td))
                {
                    conversions.Add(td, new Conversion(td, parent is null));
                    this.AddConversionsFor(td, conversions, typeDeclaration);
                }
            }
        }

        if (typeDeclaration.Schema().OneOf.IsNotUndefined())
        {
            for (int i = 0; i < typeDeclaration.Schema().OneOf.GetArrayLength(); ++i)
            {
                TypeDeclaration td = this.Builder.GetTypeDeclarationForPropertyArrayIndex(typeDeclaration, "oneOf", i);

                if (!conversions.ContainsKey(td))
                {
                    conversions.Add(td, new Conversion(td, parent is null));
                    this.AddConversionsFor(td, conversions, typeDeclaration);
                }
            }
        }

        if (typeDeclaration.Schema().Ref.IsNotUndefined() && !typeDeclaration.Schema().IsNakedReference())
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "$ref");

            if (!conversions.ContainsKey(td))
            {
                conversions.Add(td, new Conversion(td, parent is null));
                this.AddConversionsFor(td, conversions, typeDeclaration);
            }
        }

        if (typeDeclaration.Schema().Then.IsNotUndefined() && !typeDeclaration.Schema().IsNakedReference())
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "then");

            if (!conversions.ContainsKey(td))
            {
                conversions.Add(td, new Conversion(td, parent is null));
                this.AddConversionsFor(td, conversions, typeDeclaration);
            }
        }

        if (typeDeclaration.Schema().Else.IsNotUndefined() && !typeDeclaration.Schema().IsNakedReference())
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "else");

            if (!conversions.ContainsKey(td))
            {
                conversions.Add(td, new Conversion(td, parent is null));
                this.AddConversionsFor(td, conversions, typeDeclaration);
            }
        }
    }

    /// <summary>
    /// Represents a dependent schema.
    /// </summary>
    public readonly struct DependentSchema
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependentSchema"/> struct.
        /// </summary>
        /// <param name="name">The <see cref="Name"/> that name of the dependent schema.</param>
        /// <param name="dotnetTypeName">The <see cref="DotnetTypeName"/> of the schema to match.</param>
        public DependentSchema(string name, string dotnetTypeName)
        {
            this.Name = name;
            this.DotnetTypeName = dotnetTypeName;
        }

        /// <summary>
        /// Gets the pattern that is applied.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the dotnet type name of the schema to match.
        /// </summary>
        public string DotnetTypeName { get; }
    }

    /// <summary>
    /// Represents a dependent required value.
    /// </summary>
    public readonly struct DependentRequiredValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="DependentRequiredValue"/> struct.
        /// </summary>
        /// <param name="name">The <see cref="Name"/> that name of the dependent schema.</param>
        /// <param name="requiredNames">The array of <see cref="RequiredNames"/> for the schema to match.</param>
        public DependentRequiredValue(string name, ImmutableArray<string> requiredNames)
        {
            this.Name = name;
            this.RequiredNames = requiredNames;
        }

        /// <summary>
        /// Gets the pattern that is applied.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Gets the array of properties required if the named property exists.
        /// </summary>
        public ImmutableArray<string> RequiredNames { get; }
    }

    /// <summary>
    /// Represents a pattern property.
    /// </summary>
    public readonly struct PatternProperty : IEquatable<PatternProperty>
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PatternProperty"/> struct.
        /// </summary>
        /// <param name="pattern">The <see cref="Pattern"/> that is applied.</param>
        /// <param name="dotnetTypeName">The <see cref="DotnetTypeName"/> of the schema to match.</param>
        public PatternProperty(string pattern, string dotnetTypeName)
        {
            this.Pattern = pattern;
            this.DotnetTypeName = dotnetTypeName;
        }

        /// <summary>
        /// Gets the pattern that is applied.
        /// </summary>
        public string Pattern { get; }

        /// <summary>
        /// Gets the dotnet type name of the schema to match.
        /// </summary>
        public string DotnetTypeName { get; }

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="left">LHS.</param>
        /// <param name="right">RHS.</param>
        /// <returns>True if the properties are equal.</returns>
        public static bool operator ==(PatternProperty left, PatternProperty right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Equality operator.
        /// </summary>
        /// <param name="left">LHS.</param>
        /// <param name="right">RHS.</param>
        /// <returns>True if the properties are not equal.</returns>
        public static bool operator !=(PatternProperty left, PatternProperty right)
        {
            return !(left == right);
        }

        /// <inheritdoc/>
        public override bool Equals(object? obj)
        {
            return obj is PatternProperty property && this.Equals(property);
        }

        /// <inheritdoc/>
        public bool Equals(PatternProperty other)
        {
            return this.Pattern == other.Pattern &&
                   this.DotnetTypeName == other.DotnetTypeName;
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return HashCode.Combine(this.Pattern, this.DotnetTypeName);
        }
    }

    /// <summary>
    /// Represents a conversion.
    /// </summary>
    public readonly struct Conversion
    {
        private readonly TypeDeclaration typeDeclaration;

        /// <summary>
        /// Initializes a new instance of the <see cref="Conversion"/> struct.
        /// </summary>
        /// <param name="typeDeclaration">The type declaration for the conversion.</param>
        /// <param name="isDirect">If this is a direct conversion, not via a type hierarchy.</param>
        public Conversion(TypeDeclaration typeDeclaration, bool isDirect)
        {
            this.typeDeclaration = typeDeclaration;
            this.IsDirect = isDirect;
        }

        /// <summary>
        /// Gets a value indicating whether this is a string value.
        /// </summary>
        public bool IsString => this.typeDeclaration.Schema().IsStringType();

        /// <summary>
        /// Gets a value indicating whether this is an object value.
        /// </summary>
        public bool IsObject => this.typeDeclaration.Schema().IsObjectType() || this.typeDeclaration.Properties.Length > 0;

        /// <summary>
        /// Gets a value indicating whether this is an array value.
        /// </summary>
        public bool IsArray => this.typeDeclaration.Schema().IsArrayType();

        /// <summary>
        /// Gets a value indicating whether this is a boolean value.
        /// </summary>
        public bool IsBoolean => this.typeDeclaration.Schema().IsBooleanType();

        /// <summary>
        /// Gets a value indicating whether this is a number value.
        /// </summary>
        public bool IsNumber => this.typeDeclaration.Schema().IsNumberType();

        /// <summary>
        /// Gets a value indicating whether this is a built-in type.
        /// </summary>
        public bool IsBuiltInType => this.typeDeclaration.Schema().IsBuiltInType();

        /// <summary>
        /// Gets a value indicating whether this is a built-in primitive type.
        /// </summary>
        public bool IsBuiltInPrimitiveType => this.typeDeclaration.Schema().IsBuiltInPrimitiveType();

        /// <summary>
        /// Gets the fully qualified dotnet type name.
        /// </summary>
        public string FullyQualifiedDotnetTypeName => this.typeDeclaration.FullyQualifiedDotnetTypeName ?? string.Empty;

        /// <summary>
        /// Gets the dotnet type name.
        /// </summary>
        public string DotnetTypeName => this.typeDeclaration.DotnetTypeName ?? string.Empty;

        /// <summary>
        /// Gets a value indicating whether this is a direct conversion, rather than coming through a type hierarchy.
        /// </summary>
        public bool IsDirect { get; }
    }

    /// <summary>
    /// Represents a value in an enumeration.
    /// </summary>
    public readonly struct EnumValue
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="EnumValue"/> struct.
        /// </summary>
        /// <param name="value">The instance of the enum value.</param>
        public EnumValue(JsonAny value)
        {
            this.IsString = value.ValueKind == JsonValueKind.String;
            this.IsBoolean = value.ValueKind == JsonValueKind.True || value.ValueKind == JsonValueKind.False;
            this.IsNumber = value.ValueKind == JsonValueKind.Number;
            this.IsObject = value.ValueKind == JsonValueKind.Object;
            this.IsArray = value.ValueKind == JsonValueKind.Array;
            this.IsNull = value.IsNull();
            this.SerializedValue = GetRawTextAsQuotedString(value);
            this.RawStringValue = value.ValueKind == JsonValueKind.String ? GetRawStringValueAsQuotedString(value) : null;
            this.AsPropertyName = Formatting.ToPascalCaseWithReservedWords(this.SerializedValue.Trim('"')).ToString();
        }

        /// <summary>
        /// Gets a value indicating whether this is a string value.
        /// </summary>
        public bool IsString { get; }

        /// <summary>
        /// Gets a value indicating whether this is an object value.
        /// </summary>
        public bool IsObject { get; }

        /// <summary>
        /// Gets a value indicating whether this is an array value.
        /// </summary>
        public bool IsArray { get; }

        /// <summary>
        /// Gets a value indicating whether this is a boolean value.
        /// </summary>
        public bool IsBoolean { get; }

        /// <summary>
        /// Gets a value indicating whether this is a number value.
        /// </summary>
        public bool IsNumber { get; }

        /// <summary>
        /// Gets a value indicating whether this is a null value.
        /// </summary>
        public bool IsNull { get; }

        /// <summary>
        /// Gets the serialized value. This will be quoted for strings, objects, and arrays, otherwise raw.
        /// </summary>
        public string SerializedValue { get; }

        /// <summary>
        /// Gets the raw value of a string value, as a quoted string. This will be null if the value was not a string.
        /// </summary>
        public string? RawStringValue { get; }

        /// <summary>
        /// Gets the serialized value as a property name.
        /// </summary>
        public object AsPropertyName { get; }
    }
}