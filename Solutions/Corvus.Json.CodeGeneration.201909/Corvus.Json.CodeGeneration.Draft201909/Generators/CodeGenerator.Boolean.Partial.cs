// <copyright file="CodeGenerator.Boolean.Partial.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json.CodeGeneration.Draft201909;
using Corvus.Json.JsonSchema.Draft201909;

namespace Corvus.Json.CodeGeneration.Generators.Draft201909;

/// <summary>
/// Services for the code generation t4 templates.
/// </summary>
public partial class CodeGeneratorBoolean
{
    /// <summary>
    /// Initializes a new instance of the <see cref="CodeGeneratorBoolean"/> class.
    /// </summary>
    /// <param name="jsonSchemaBuilder">The current <see cref="JsonSchemaBuilder"/>.</param>
    /// <param name="declarationToGenerate">The <see cref="TypeDeclaration"/> to generate in this file.</param>
    public CodeGeneratorBoolean(JsonSchemaBuilder jsonSchemaBuilder, TypeDeclaration declarationToGenerate)
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
            return this.TypeDeclaration.Properties;
        }
    }

    /// <summary>
    /// Gets the array of required <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> RequiredProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => p.IsDefinedInLocalScope && p.IsRequired).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the array of optional <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> OptionalProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => p.IsDefinedInLocalScope && !p.IsRequired).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the array of required <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> RequiredAllOfAndRefProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => p.IsRequired).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the array of optional <see cref="PropertyDeclaration"/>s.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> OptionalAllOfAndRefProperties
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => !p.IsRequired).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the array of properties with default values.
    /// </summary>
    public ImmutableArray<PropertyDeclaration> Defaults
    {
        get
        {
            return this.TypeDeclaration.Properties.Where(p => p.HasDefaultValue).ToImmutableArray();
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
                return this.TypeDeclaration.Schema().Pattern;
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
            return this.TypeDeclaration.Schema().DependentRequired.IsNotUndefined() || this.HasDependenciesDependentRequired;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a DependentSchema constraint.
    /// </summary>
    public bool HasDependentSchemas
    {
        get
        {
            return this.TypeDeclaration.Schema().DependentSchemas.IsNotUndefined() || this.HasDependenciesDependentSchemas;
        }
    }

        /// <summary>
    /// Gets a value indicating whether this has a DependentRequired constraint.
    /// </summary>
    public bool HasDependenciesDependentRequired
    {
        get
        {
            return this.TypeDeclaration.Schema().Dependencies.IsNotUndefined() && this.TypeDeclaration.Schema().Dependencies.EnumerateObject().Any(t => t.Value.ValueKind == JsonValueKind.Array);
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a DependentSchema constraint.
    /// </summary>
    public bool HasDependenciesDependentSchemas
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
    public int MaxItems
    {
        get
        {
            return this.HasMaxItems ? this.TypeDeclaration.Schema().MaxItems : default;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minItems constraint.
    /// </summary>
    public int MinItems
    {
        get
        {
            return this.HasMinItems ? this.TypeDeclaration.Schema().MinItems : default;
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
                return this.TypeDeclaration.Schema().MultipleOf.AsJsonElement.GetRawText();
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
                return this.TypeDeclaration.Schema().ExclusiveMaximum.AsJsonElement.GetRawText();
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
                return this.TypeDeclaration.Schema().Maximum.AsJsonElement.GetRawText();
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
                return this.TypeDeclaration.Schema().ExclusiveMinimum.AsJsonElement.GetRawText();
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
                return this.TypeDeclaration.Schema().Minimum.AsJsonElement.GetRawText();
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
                return ((int)this.TypeDeclaration.Schema().MaxLength).ToString();
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
                return ((int)this.TypeDeclaration.Schema().MinLength).ToString();
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
    /// Gets the dotnet typename for the non-naked reference.
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
    /// Gets a value indicating whether this object has explicit unevaluatedProperties.
    /// </summary>
    public bool HasUnevaluatedProperties
    {
        get
        {
            if (this.TypeDeclaration.Schema().UnevaluatedProperties.IsNullOrUndefined())
            {
                return false;
            }

            TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "unevaluatedProperties");
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
    /// Gets a value indicating whether this object has explicit unevaluatedProperties as a schema object.
    /// </summary>
    public bool HasUnevaluatedPropertiesObject
    {
        get
        {
            if (this.TypeDeclaration.Schema().UnevaluatedProperties.IsNullOrUndefined())
            {
                return false;
            }

            TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "unevaluatedProperties");
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
            return this.HasMultipleItemsType && (this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.Object ||
                 this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.True ||
                 this.TypeDeclaration.Schema().AdditionalItems.ValueKind == JsonValueKind.False);
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
                    TypeDeclaration typeDeclaration = this.Builder.GetTypeDeclarationForPatternProperty(this.TypeDeclaration, property.Name);
                    builder.Add(new PatternProperty(property.Name, typeDeclaration.FullyQualifiedDotnetTypeName!));
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
            if (this.HasDependenciesDependentSchemas)
            {
                return this.DependenciesDependentSchemas;
            }

            ImmutableArray<DependentSchema>.Builder builder = ImmutableArray.CreateBuilder<DependentSchema>();
            if (this.TypeDeclaration.Schema().DependentSchemas.IsNotUndefined())
            {
                foreach (JsonObjectProperty property in this.TypeDeclaration.Schema().DependentSchemas.EnumerateObject())
                {
                    builder.Add(new DependentSchema(property.Name, this.Builder.GetTypeDeclarationForDependentSchema(this.TypeDeclaration, property.Name).FullyQualifiedDotnetTypeName!));
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
            if (this.HasDependenciesDependentRequired)
            {
                return this.DependenciesDependentRequired;
            }

            ImmutableArray<DependentRequiredValue>.Builder builder = ImmutableArray.CreateBuilder<DependentRequiredValue>();
            if (this.TypeDeclaration.Schema().DependentRequired.IsNotUndefined())
            {
                foreach (JsonObjectProperty property in this.TypeDeclaration.Schema().DependentRequired.EnumerateObject())
                {
                    ImmutableArray<string>.Builder innerBuilder = ImmutableArray.CreateBuilder<string>();
                    foreach (JsonAny item in property.Value.EnumerateArray())
                    {
                        innerBuilder.Add((string)item);
                    }

                    builder.Add(new DependentRequiredValue(property.Name, innerBuilder.ToImmutable()));
                }
            }

            return builder.ToImmutable();
        }
    }

        /// <summary>
    /// Gets the dependent schema for the type.
    /// </summary>
    public ImmutableArray<DependentSchema> DependenciesDependentSchemas
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
                        builder.Add(new DependentSchema(property.Name, this.Builder.GetTypeDeclarationForDependentSchema(this.TypeDeclaration, property.Name).FullyQualifiedDotnetTypeName!));
                    }
                }
            }

            return builder.ToImmutable();
        }
    }

    /// <summary>
    /// Gets the dependent required values for the type.
    /// </summary>
    public ImmutableArray<DependentRequiredValue> DependenciesDependentRequired
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
                        foreach (JsonAny item in property.Value.EnumerateArray())
                        {
                            innerBuilder.Add((string)item);
                        }

                        builder.Add(new DependentRequiredValue(property.Name, innerBuilder.ToImmutable()));
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
    /// Gets a value indicating whether this has the maxContains constraint.
    /// </summary>
    public bool HasMaxContains
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxContains.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the maxContains constraint.
    /// </summary>
    public int MaxContains
    {
        get
        {
            if (this.HasMaxContains)
            {
                return this.TypeDeclaration.Schema().MaxContains;
            }

            return default;
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
            return (this.HasSingleItemsType || (this.TypeDeclaration.Schema().Items.IsUndefined() && this.TypeDeclaration.Schema().UnevaluatedItems.IsNotUndefined())) && this.SingleItemsDotnetTypeName != $"{BuiltInTypes.AnyTypeDeclaration.Ns}.{BuiltInTypes.AnyTypeDeclaration.Type}";
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
    /// Gets a value indicating whether this object has explicit validation for unevaluated items.
    /// </summary>
    public bool HasUnevaluatedItems
    {
        get
        {
            return this.TypeDeclaration.Schema().UnevaluatedItems.ValueKind == JsonValueKind.Object ||
                 this.TypeDeclaration.Schema().UnevaluatedItems.ValueKind == JsonValueKind.True ||
                 this.TypeDeclaration.Schema().UnevaluatedItems.ValueKind == JsonValueKind.False;
        }
    }

    /// <summary>
    /// Gets the unevaluated items dotnet type name.
    /// </summary>
    public string UnevaluatedItemsDotnetTypeName
    {
        get
        {
            if (this.TypeDeclaration.Schema().UnevaluatedItems.IsNotUndefined())
            {
                TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "unevaluatedItems");
                return td.FullyQualifiedDotnetTypeName ?? string.Empty;
            }

            return string.Empty;
        }
    }

    /// <summary>
    /// Gets a value indicating whether this has a minContains constraint.
    /// </summary>
    public bool HasMinContains
    {
        get
        {
            return this.TypeDeclaration.Schema().MinContains.IsNotUndefined();
        }
    }

    /// <summary>
    /// Gets the minContains constraint.
    /// </summary>
    public int MinContains
    {
        get
        {
            return this.HasMinContains ? this.TypeDeclaration.Schema().MinContains : default;
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
    /// Gets the unevaluatedProperties schema dotnet type name.
    /// </summary>
    public string UnevaluatedPropertiesDotnetTypeName
    {
        get
        {
            return this.Builder.GetTypeDeclarationForProperty(this.TypeDeclaration, "unevaluatedProperties").FullyQualifiedDotnetTypeName ?? string.Empty;
        }
    }

    /// <summary>
    /// Gets the maxProperties constraint.
    /// </summary>
    public int MaxProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MaxProperties.IsNotUndefined() ? this.TypeDeclaration.Schema().MaxProperties : default;
        }
    }

    /// <summary>
    /// Gets the minProperties constraint.
    /// </summary>
    public int MinProperties
    {
        get
        {
            return this.TypeDeclaration.Schema().MinProperties.IsNotUndefined() ? this.TypeDeclaration.Schema().MinProperties : default;
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
            var conversions = new Dictionary<TypeDeclaration, Conversion>();

            this.AddConversionsFor(this.TypeDeclaration, conversions, null);

            // Get the conversions that require a constructor
            return conversions.Where(t => t.Value.IsDirect).Select(t => t.Value).ToImmutableArray();
        }
    }

    /// <summary>
    /// Gets the implicit conversions appropriate for this type that require a cast.
    /// </summary>
    public ImmutableArray<Conversion> Conversions
    {
        get
        {
            var conversions = new Dictionary<TypeDeclaration, Conversion>();

            this.AddConversionsFor(this.TypeDeclaration, conversions, null);

            // Select the conversions that require a cast through another type
            return conversions.Select(t => t.Value).ToImmutableArray();
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
            if (this.TypeDeclaration.Schema().Format.IsNotUndefined())
            {
                return this.TypeDeclaration.Schema().Format == "integer";
            }

            return false;
        }
    }

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

    private static string GetRawTextAsQuotedString(JsonAny? value)
    {
        if (value is JsonAny actualValue)
        {
            return Formatting.FormatLiteralOrNull(actualValue.AsJsonElement.GetRawText(), true);
        }

        throw new ArgumentNullException(nameof(value));
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

                if (!td.IsBuiltInType && !conversions.ContainsKey(td))
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

                if (!td.IsBuiltInType && !conversions.ContainsKey(td))
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

                if (!td.IsBuiltInType && !conversions.ContainsKey(td))
                {
                    conversions.Add(td, new Conversion(td, parent is null));
                    this.AddConversionsFor(td, conversions, typeDeclaration);
                }
            }
        }

        if (typeDeclaration.Schema().Ref.IsNotUndefined() && !(typeDeclaration.Schema().IsNakedReference() || typeDeclaration.Schema().IsNakedRecursiveReference()))
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "$ref");

            if (!td.IsBuiltInType && !conversions.ContainsKey(td))
            {
                conversions.Add(td, new Conversion(td, parent is null));
                this.AddConversionsFor(td, conversions, typeDeclaration);
            }
        }

        if (typeDeclaration.Schema().Then.IsNotUndefined() && !(typeDeclaration.Schema().IsNakedReference() || typeDeclaration.Schema().IsNakedRecursiveReference()))
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "then");

            if (!td.IsBuiltInType && !conversions.ContainsKey(td))
            {
                conversions.Add(td, new Conversion(td, parent is null));
                this.AddConversionsFor(td, conversions, typeDeclaration);
            }
        }

        if (typeDeclaration.Schema().Else.IsNotUndefined() && !(typeDeclaration.Schema().IsNakedReference() || typeDeclaration.Schema().IsNakedRecursiveReference()))
        {
            TypeDeclaration td = this.Builder.GetTypeDeclarationForProperty(typeDeclaration, "else");

            if (!td.IsBuiltInType && !conversions.ContainsKey(td))
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
        /// Gets the serialized value as a property name.
        /// </summary>
        public object AsPropertyName { get; }
    }
}