// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Reflection;
using System.Threading.Tasks;
using Corvus.Text.Json.Validator;
using TestUtilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Tests;
[TestClass]
public class JsonSchemaCodeGeneratorTests
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

    [TestMethod]
    [DataRow("[\"object\", \"array\"]")]
    [DataRow("[\"object\", \"string\"]")]
    [DataRow("[\"object\", \"number\"]")]
    [DataRow("[\"object\", \"boolean\"]")]
    [DataRow("[\"object\", \"null\"]")]
    [DataRow("[\"array\", \"string\"]")]
    [DataRow("[\"array\", \"number\"]")]
    [DataRow("[\"array\", \"boolean\"]")]
    [DataRow("[\"array\", \"null\"]")]
    [DataRow("[\"string\", \"boolean\"]")]
    [DataRow("[\"string\", \"number\"]")]
    [DataRow("[\"string\", \"null\"]")]
    [DataRow("[\"number\", \"boolean\"]")]
    [DataRow("[\"number\", \"null\"]")]
    [DataRow("[\"boolean\", \"null\"]")]
    public async Task GenerateCode_Emits_ArrayTypes(string type)
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

    [TestMethod]
    public async Task GenerateCode_Emits_TupleType ()
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

    [TestMethod]
    public async Task GenerateCode_Emits_TupleWithAdditionalItemsType()
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

    [TestMethod]
    public async Task GenerateCode_Emits_AllOfInlineTupleWithUnevaluated()
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

    [TestMethod]
    [DataRow("items", "number", "int128")]
    [DataRow("items", "number", "int64")]
    [DataRow("items", "number", "int32")]
    [DataRow("items", "number", "int16")]
    [DataRow("items", "number", "sbyte")]
    [DataRow("items", "number", "uint128")]
    [DataRow("items", "number", "uint64")]
    [DataRow("items", "number", "uint32")]
    [DataRow("items", "number", "uint16")]
    [DataRow("items", "number", "byte")]
    [DataRow("items", "number", "double")]
    [DataRow("items", "number", "single")]
    [DataRow("items", "number", "decimal")]
    [DataRow("items", "string", "date")]
    [DataRow("items", "string", "date-time")]
    [DataRow("items", "string", "time")]
    [DataRow("items", "string", "duration")]
    [DataRow("items", "string", "ipv4")]
    [DataRow("items", "string", "ipv6")]
    [DataRow("items", "string", "uuid")]
    [DataRow("items", "string", "uri")]
    [DataRow("items", "string", "uri-reference")]
    [DataRow("items", "string", "iri")]
    [DataRow("items", "string", "iri-reference")]
    [DataRow("items", "string", "regex")]
    [DataRow("unevaluatedItems", "number", "int128")]
    [DataRow("unevaluatedItems", "number", "int64")]
    [DataRow("unevaluatedItems", "number", "int32")]
    [DataRow("unevaluatedItems", "number", "int16")]
    [DataRow("unevaluatedItems", "number", "sbyte")]
    [DataRow("unevaluatedItems", "number", "uint128")]
    [DataRow("unevaluatedItems", "number", "uint64")]
    [DataRow("unevaluatedItems", "number", "uint32")]
    [DataRow("unevaluatedItems", "number", "uint16")]
    [DataRow("unevaluatedItems", "number", "byte")]
    [DataRow("unevaluatedItems", "number", "double")]
    [DataRow("unevaluatedItems", "number", "single")]
    [DataRow("unevaluatedItems", "number", "decimal")]
    [DataRow("unevaluatedItems", "string", "date")]
    [DataRow("unevaluatedItems", "string", "date-time")]
    [DataRow("unevaluatedItems", "string", "time")]
    [DataRow("unevaluatedItems", "string", "duration")]
    [DataRow("unevaluatedItems", "string", "ipv4")]
    [DataRow("unevaluatedItems", "string", "ipv6")]
    [DataRow("unevaluatedItems", "string", "uuid")]
    [DataRow("unevaluatedItems", "string", "uri")]
    [DataRow("unevaluatedItems", "string", "uri-reference")]
    [DataRow("unevaluatedItems", "string", "iri")]
    [DataRow("unevaluatedItems", "string", "iri-reference")]
    [DataRow("unevaluatedItems", "string", "regex")]
    public async Task GenerateCode_Emits_ArrayTypeWithItemsConstraintAndFormat(string keyword, string type, string format)
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

    [TestMethod]
    public async Task GenerateCode_Emits_ComplexComposedObjectWithProperties()
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

    [TestMethod]
    public async Task GenerateCode_Emits_ComposedArrayMultiItemType()
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

    [TestMethod]
    [DataRow("string", "date", "string", "date-time")]
    [DataRow("string", "date", "string", "date")]
    [DataRow("string", "date", "number", "int32")]
    [DataRow("string", "uuid", "string", "iri")]
    [DataRow("number", "int64", "number", "int128")]
    public async Task GenerateCode_Emits_ComposedFormatTypes(string type1, string format1, string type2, string format2)
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

    [TestMethod]
    [DataRow("number", "int64", "number", "int128")]
    [DataRow("number", "int64", "string", "date")]
    public async Task GenerateCode_Emits_ComposedMultiFormatNumericWithAdditionalConstraint(string type1, string format1, string type2, string format2)
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

    [TestMethod]
    public async Task GenerateCode_Emits_ComposedObjectWithProperties()
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

    [TestMethod]
    public async Task GenerateCode_Emits_ComposedObjectWithRequiredProperties()
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

    [TestMethod]
    [DataRow("sbyte")]
    [DataRow("int16")]
    [DataRow("int32")]
    [DataRow("int64")]
    [DataRow("int128")]
    [DataRow("byte")]
    [DataRow("uint16")]
    [DataRow("uint32")]
    [DataRow("uint64")]
    [DataRow("uint128")]
    [DataRow("decimal")]
    [DataRow("double")]
    [DataRow("single")]
    [DataRow("half")]
    public async Task GenerateCode_Emits_FixedSizeNumericArrayTypes(string format)
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

    [TestMethod]
    [DataRow("sbyte")]
    [DataRow("int16")]
    [DataRow("int32")]
    [DataRow("int64")]
    [DataRow("int128")]
    [DataRow("byte")]
    [DataRow("uint16")]
    [DataRow("uint32")]
    [DataRow("uint64")]
    [DataRow("uint128")]
    [DataRow("decimal")]
    [DataRow("double")]
    [DataRow("single")]
    [DataRow("half")]
    public async Task GenerateCode_Emits_MultiDimensionFixedSizeNumericArrayTypes(string format)
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

    [TestMethod]
    [DataRow("sbyte")]
    [DataRow("int16")]
    [DataRow("int32")]
    [DataRow("int64")]
    [DataRow("int128")]
    [DataRow("byte")]
    [DataRow("uint16")]
    [DataRow("uint32")]
    [DataRow("uint64")]
    [DataRow("uint128")]
    [DataRow("decimal")]
    [DataRow("double")]
    [DataRow("single")]
    [DataRow("half")]
    public async Task GenerateCode_Emits_MultiDimensionHigherRankFixedSizeNumericArrayTypes(string format)
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

    [TestMethod]
    [DataRow("sbyte")]
    [DataRow("int16")]
    [DataRow("int32")]
    [DataRow("int64")]
    [DataRow("int128")]
    [DataRow("byte")]
    [DataRow("uint16")]
    [DataRow("uint32")]
    [DataRow("uint64")]
    [DataRow("uint128")]
    [DataRow("decimal")]
    [DataRow("double")]
    [DataRow("single")]
    [DataRow("half")]
    public async Task GenerateCode_Emits_NumericArrayTypes(string format)
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

    [TestMethod]
    [DataRow("sbyte")]
    [DataRow("int16")]
    [DataRow("int32")]
    [DataRow("int64")]
    [DataRow("int128")]
    [DataRow("byte")]
    [DataRow("uint16")]
    [DataRow("uint32")]
    [DataRow("uint64")]
    [DataRow("uint128")]
    [DataRow("decimal")]
    [DataRow("double")]
    [DataRow("single")]
    [DataRow("half")]
    public async Task GenerateCode_Emits_NumericFormatTypes(string format)
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

    [TestMethod]
    public async Task GenerateCode_Emits_Person()
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

    [TestMethod]
    public async Task GenerateCode_Emits_SimpleObjectWithProperties()
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

    [TestMethod]
    [DataRow("object")]
    [DataRow("array")]
    [DataRow("string")]
    [DataRow("number")]
    [DataRow("boolean")]
    [DataRow("null")]
    public async Task GenerateCode_Emits_SimpleTypes(string type)
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

    [TestMethod]
    [DataRow("date")]
    [DataRow("date-time")]
    [DataRow("time")]
    [DataRow("duration")]
    [DataRow("ipv4")]
    [DataRow("ipv6")]
    [DataRow("uuid")]
    [DataRow("uri")]
    [DataRow("uri-reference")]
    [DataRow("iri")]
    [DataRow("iri-reference")]
    [DataRow("regex")]
    public async Task GenerateCode_Emits_StringFormatTypes(string format)
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
