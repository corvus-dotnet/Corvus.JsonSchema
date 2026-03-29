// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Xunit;

namespace Corvus.Text.Json.Tests;
public static class JsonSchemaCodeGeneratorTests
{
    private const string ArrayType =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": {0}
        }}
        """;

    private const string TupleType =
    """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "title": "PureTuple",
        "description": "A fixed-length tuple array with positional types and no additional items.",
        "type": "array",
        "prefixItems": [
        { "type": "string" },
        { "type": "integer", "format": "int32" },
        { "type": "boolean" }
        ],
        "items": false
    }
    """;

    private const string TupleWithAdditionalItemsType =
    """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "TupleWithAdditionalItems",
      "description": "A tuple array with positional types and additional items of a specific type.",
      "type": "array",
      "prefixItems": [
        { "type": "string" },
        { "type": "integer", "format": "int32" }
      ],
      "items": { "type": "boolean" }
    }
    """;

    private const string AllOfInlineTupleWithUnevaluated =
    """
    {
      "$schema": "https://json-schema.org/draft/2020-12/schema",
      "title": "AllOfInlineTupleWithUnevaluated",
      "description": "An inline tuple within allOf with unevaluatedItems at the top level.",
      "allOf": [
        {
          "prefixItems": [
            { "type": "string" },
            { "type": "number" }
          ]
        }
      ],
      "unevaluatedItems": { "type": "boolean" }
    }        
    """;

    private const string ArrayTypeWithItemsConstraint =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "{0}": {{ "type": "{1}" }}
        }}
        """;

    private const string ArrayTypeWithItemsConstraintAndFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "{0}": {{ "type": "{1}", "format": "{2}" }}
        }}
        """;

    private const string ComplexComposedObjectWithProperties =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                {
                    "type": "object",
                    "properties": {
                        "eitherOr": {
                            "type": "boolean"
                        }
                    }
                },
                {
                    "type": "object",
                    "properties": {
                        "wholeNumber": {
                            "type": "integer"
                        }
                    }
                }
                        ],
            "allOf": [
                {
                    "type": "object",
                    "properties": {
                        "name": {
                            "type": "string"
                        }
                    }
                },
                {
                    "type": "object",
                    "properties": {
                        "otherName": {
                            "type": "number"
                        }
                    }
                }
            ]
        }
        """;

    private const string ComposedArrayMultiItemType =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "oneOf": [
                { "items": {"type": "number" } },
                { "items": {"type": "string" } }
            ],
            "minItems": 10,
            "maxItems": 10
        }
        """;

    private const string ComposedMultiFormatNumericWithAdditionalConstraint =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                    {{"type": "{0}", "format": "{1}"}},
                    {{"type": "{2}", "format": "{3}"}}
                ],
            "minimum": 30
        }}
        """;

    private const string ComposedMultiFormatType =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "anyOf": [
                    {{"type": "{0}", "format": "{1}"}},
                    {{"type": "{2}", "format": "{3}"}}
                ]
        }}
        """;

    private const string ComposedObjectWithProperties =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {
                    "type": "object",
                    "properties": {
                        "name": {
                            "type": "string"
                        }
                    }
                },
                {
                    "type": "object",
                    "properties": {
                        "otherName": {
                            "type": "number"
                        }
                    }
                }
            ]
        }
        """;

    private const string ComposedObjectWithRequiredProperties =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "allOf": [
                {
                    "required": ["name"]
                },
                {
                    "type": "object",
                    "properties": {
                        "name": {
                            "type": "string"
                        },
                        "otherName": {
                            "type": "number"
                        }
                    }
                }
            ]
        }
        """;

    private const string FixedSizeNumericArrayFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "items": {{
                "type": "number",
                "format": "{0}"
            }},
            "minItems": 10,
            "maxItems": 10
        }}
        """;

    private const string MultiDimensionFixedSizeNumericArrayFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "items": {{
                "type": "array",
                "items": {{
                    "type": "number",
                    "format": "{0}"
                }},
                "minItems": 5,
                "maxItems": 5
            }},
            "minItems": 2,
            "maxItems": 2
        }}
        """;

    private const string MultiDimensionHigherRankFixedSizeNumericArrayFormat =
"""
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "items": {{
                "type": "array",
                "items": {{
                    "type": "array",
                    "items": {{
                        "type": "number",
                        "format": "{0}"
                    }},
                    "minItems": 5,
                    "maxItems": 5
                }},
                "minItems": 2,
                "maxItems": 2
            }},
            "minItems": 3,
            "maxItems": 3
        }}
        """;

    private const string NumericArrayFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "array",
            "items": {{
                "type": "number",
                "format": "{0}"
            }}
        }}
        """;

    private const string NumericFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "number",
            "format": "{0}"
        }}
        """;

    private const string Person =
                                                                                                                        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "title": "JSON Schema for a Person entity coming back from a 3rd party API (e.g. a storage format in a database)",
            "type": "object",

            "required": [ "name" ],
            "properties": {
                "name": { "$ref": "#/$defs/PersonName" },
                "age": { "$ref": "#/$defs/Age" },
                "competedInYears": { "$ref": "#/$defs/CompetedInYears" }
            },
            "unevaluatedProperties": false,
            "$defs": {
                "PersonArray": {
                    "type": "array",
                    "items": {
                        "$ref": "#/$defs/Person"
                    }
                },
                "HeightRangeDouble": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 3.0
                },
                "PersonName": {
                    "type": "object",
                    "description": "A name of a person.",
                    "required": [ "firstName" ],
                    "properties": {
                        "firstName": {
                            "$ref": "#/$defs/NameComponent",
                            "description": "The person's first name."
                        },
                        "lastName": {
                            "$ref": "#/$defs/NameComponent",
                            "description": "The person's last name."
                        },
                        "otherNames": {
                            "$ref": "#/$defs/OtherNames",
                            "description": "Other (middle) names for the person"
                        }
                    }
                },
                "OtherNames": {
                    "oneOf": [
                        { "$ref": "#/$defs/NameComponent" },
                        { "$ref": "#/$defs/NameComponentArray" }
                    ]
                },
                "NameComponentArray": {
                    "type": "array",
                    "items": {
                        "$ref": "#/$defs/NameComponent"
                    }
                },
                "NameComponent": {
                    "type": "string",
                    "minLength": 1,
                    "maxLength": 256
                },
                "CompetedInYears": {
                    "type": "array",
                    "items": { "$ref": "#/$defs/Year" }
                },
                "Year": {
                    "type": "number",
                    "format": "int32"
                },
                "Age": {
                    "type": "number",
                    "minimum": 0,
                    "maximum": 130
                }
            }
        }
        """;

    private const string SimpleObjectWithProperties =
        """
        {
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "object",
            "properties": {
                "name": {
                    "type": "string"
                },
                "otherName": {
                    "type": "number"
                }
            }
        }
        """;

    private const string SimpleType =
                """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "{0}"
        }}
        """;

    private const string StringFormat =
        """
        {{
            "$schema": "https://json-schema.org/draft/2020-12/schema",
            "type": "string",
            "format": "{0}"
        }}
        """;

    [Theory]
    [InlineData("[\"object\", \"array\"]")]
    [InlineData("[\"object\", \"string\"]")]
    [InlineData("[\"object\", \"number\"]")]
    [InlineData("[\"object\", \"boolean\"]")]
    [InlineData("[\"object\", \"null\"]")]
    [InlineData("[\"array\", \"string\"]")]
    [InlineData("[\"array\", \"number\"]")]
    [InlineData("[\"array\", \"boolean\"]")]
    [InlineData("[\"array\", \"null\"]")]
    [InlineData("[\"string\", \"boolean\"]")]
    [InlineData("[\"string\", \"number\"]")]
    [InlineData("[\"string\", \"null\"]")]
    [InlineData("[\"number\", \"boolean\"]")]
    [InlineData("[\"number\", \"null\"]")]
    [InlineData("[\"boolean\", \"null\"]")]
    public static async Task GenerateCode_Emits_ArrayTypes(string type)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"types_{GetNameFor(type)}.json",
            string.Format(ArrayType, type),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );

        static string GetNameFor(string type)
        {
            StringBuilder s = new(type);
            s.Replace("\"", "");
            s.Replace(" ", "");
            s.Replace(",", "");
            s.Replace("[", "");
            s.Replace("]", "");
            return s.ToString();
        }
    }

    [Fact]
    public static async Task GenerateCode_Emits_TupleType ()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"types_tupletype.json",
            TupleType,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_TupleWithAdditionalItemsType()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"types_tuplewithadditionalitemstype.json",
            TupleWithAdditionalItemsType,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_AllOfInlineTupleWithUnevaluated()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"types_allofinlinetuplewithunevaluated.json",
            AllOfInlineTupleWithUnevaluated,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("items", "number", "int128")]
    [InlineData("items", "number", "int64")]
    [InlineData("items", "number", "int32")]
    [InlineData("items", "number", "int16")]
    [InlineData("items", "number", "sbyte")]
    [InlineData("items", "number", "uint128")]
    [InlineData("items", "number", "uint64")]
    [InlineData("items", "number", "uint32")]
    [InlineData("items", "number", "uint16")]
    [InlineData("items", "number", "byte")]
    [InlineData("items", "number", "double")]
    [InlineData("items", "number", "single")]
    [InlineData("items", "number", "decimal")]
    [InlineData("items", "string", "date")]
    [InlineData("items", "string", "date-time")]
    [InlineData("items", "string", "time")]
    [InlineData("items", "string", "duration")]
    [InlineData("items", "string", "ipv4")]
    [InlineData("items", "string", "ipv6")]
    [InlineData("items", "string", "uuid")]
    [InlineData("items", "string", "uri")]
    [InlineData("items", "string", "uri-reference")]
    [InlineData("items", "string", "iri")]
    [InlineData("items", "string", "iri-reference")]
    [InlineData("items", "string", "regex")]
    [InlineData("unevaluatedItems", "number", "int128")]
    [InlineData("unevaluatedItems", "number", "int64")]
    [InlineData("unevaluatedItems", "number", "int32")]
    [InlineData("unevaluatedItems", "number", "int16")]
    [InlineData("unevaluatedItems", "number", "sbyte")]
    [InlineData("unevaluatedItems", "number", "uint128")]
    [InlineData("unevaluatedItems", "number", "uint64")]
    [InlineData("unevaluatedItems", "number", "uint32")]
    [InlineData("unevaluatedItems", "number", "uint16")]
    [InlineData("unevaluatedItems", "number", "byte")]
    [InlineData("unevaluatedItems", "number", "double")]
    [InlineData("unevaluatedItems", "number", "single")]
    [InlineData("unevaluatedItems", "number", "decimal")]
    [InlineData("unevaluatedItems", "string", "date")]
    [InlineData("unevaluatedItems", "string", "date-time")]
    [InlineData("unevaluatedItems", "string", "time")]
    [InlineData("unevaluatedItems", "string", "duration")]
    [InlineData("unevaluatedItems", "string", "ipv4")]
    [InlineData("unevaluatedItems", "string", "ipv6")]
    [InlineData("unevaluatedItems", "string", "uuid")]
    [InlineData("unevaluatedItems", "string", "uri")]
    [InlineData("unevaluatedItems", "string", "uri-reference")]
    [InlineData("unevaluatedItems", "string", "iri")]
    [InlineData("unevaluatedItems", "string", "iri-reference")]
    [InlineData("unevaluatedItems", "string", "regex")]
    public static async Task GenerateCode_Emits_ArrayTypeWithItemsConstraintAndFormat(string keyword, string type, string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"arrayTypeWithItemsAndFormat_{keyword}_{type}_{format}.json",
            string.Format(ArrayTypeWithItemsConstraintAndFormat, keyword, type, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_ComplexComposedObjectWithProperties()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "complexComposedObjectWithProperties.json",
            ComplexComposedObjectWithProperties,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_ComposedArrayMultiItemType()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "composedArrayMultiItemType.json",
            ComposedArrayMultiItemType,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("string", "date", "string", "date-time")]
    [InlineData("string", "date", "string", "date")]
    [InlineData("string", "date", "number", "int32")]
    [InlineData("string", "uuid", "string", "iri")]
    [InlineData("number", "int64", "number", "int128")]
    public static async Task GenerateCode_Emits_ComposedFormatTypes(string type1, string format1, string type2, string format2)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"composedFormat_{type1}_{format1}_{type2}_{format2}.json",
            string.Format(ComposedMultiFormatType, type1, format1, type2, format2),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("number", "int64", "number", "int128")]
    [InlineData("number", "int64", "string", "date")]
    public static async Task GenerateCode_Emits_ComposedMultiFormatNumericWithAdditionalConstraint(string type1, string format1, string type2, string format2)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"composedNumericFormatWithConstraint_{type1}_{format1}_{type2}_{format2}.json",
            string.Format(ComposedMultiFormatNumericWithAdditionalConstraint, type1, format1, type2, format2),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_ComposedObjectWithProperties()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "composedObjectWithProperties.json",
            ComposedObjectWithProperties,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_ComposedObjectWithRequiredProperties()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "composedObjectWithRequiredProperties.json",
            ComposedObjectWithRequiredProperties,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("int128")]
    [InlineData("byte")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    [InlineData("uint128")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("single")]
    [InlineData("half")]
    public static async Task GenerateCode_Emits_FixedSizeNumericArrayTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"fixedSizeNumericArray_{format}.json",
            string.Format(FixedSizeNumericArrayFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("int128")]
    [InlineData("byte")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    [InlineData("uint128")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("single")]
    [InlineData("half")]
    public static async Task GenerateCode_Emits_MultiDimensionFixedSizeNumericArrayTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"multiDimensionFixedSizeNumericArray_{format}.json",
            string.Format(MultiDimensionFixedSizeNumericArrayFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("int128")]
    [InlineData("byte")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    [InlineData("uint128")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("single")]
    [InlineData("half")]
    public static async Task GenerateCode_Emits_MultiDimensionHigherRankFixedSizeNumericArrayTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"multiDimensionHigherRankFixedSizeNumericArray_{format}.json",
            string.Format(MultiDimensionHigherRankFixedSizeNumericArrayFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("int128")]
    [InlineData("byte")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    [InlineData("uint128")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("single")]
    [InlineData("half")]
    public static async Task GenerateCode_Emits_NumericArrayTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"numericArray_{format}.json",
            string.Format(NumericArrayFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("sbyte")]
    [InlineData("int16")]
    [InlineData("int32")]
    [InlineData("int64")]
    [InlineData("int128")]
    [InlineData("byte")]
    [InlineData("uint16")]
    [InlineData("uint32")]
    [InlineData("uint64")]
    [InlineData("uint128")]
    [InlineData("decimal")]
    [InlineData("double")]
    [InlineData("single")]
    [InlineData("half")]
    public static async Task GenerateCode_Emits_NumericFormatTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"numericFormat_{format}.json",
            string.Format(NumericFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_Person()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "person.json",
            Person,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Fact]
    public static async Task GenerateCode_Emits_SimpleObjectWithProperties()
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            "simpleObjectWithProperties.json",
            SimpleObjectWithProperties,
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("object")]
    [InlineData("array")]
    [InlineData("string")]
    [InlineData("number")]
    [InlineData("boolean")]
    [InlineData("null")]
    public static async Task GenerateCode_Emits_SimpleTypes(string type)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"type_{type}.json",
            string.Format(SimpleType, type),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }

    [Theory]
    [InlineData("date")]
    [InlineData("date-time")]
    [InlineData("time")]
    [InlineData("duration")]
    [InlineData("ipv4")]
    [InlineData("ipv6")]
    [InlineData("uuid")]
    [InlineData("uri")]
    [InlineData("uri-reference")]
    [InlineData("iri")]
    [InlineData("iri-reference")]
    [InlineData("regex")]
    public static async Task GenerateCode_Emits_StringFormatTypes(string format)
    {
        _ = await TestJsonSchemaCodeGenerator.GenerateTypeForVirtualFile(
            $"stringFormat_{format}.json",
            string.Format(StringFormat, format),
            $"{MethodBase.GetCurrentMethod().DeclaringType.FullName}.{MethodBase.GetCurrentMethod().Name}",
            "./someFakePath",
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary,
            validateFormat: true,
            optionalAsNullable: false,
            useImplicitOperatorString: false,
            addExplicitUsings: false,
            hostAssembly: Assembly.GetExecutingAssembly()
            );
    }
}
