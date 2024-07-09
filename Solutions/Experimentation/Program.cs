using System.Text.Json;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Sandbox;

using PrepopulatedDocumentResolver documentResolver = new();

using var testDoc = JsonDocument.Parse(
    """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "test:/test-doc.json",
        "type": "object",
        "required": ["foo"],
        "properties": {
            "someIri" : {"type": "string", "format": "iri", "pattern": "https://(.*)" },
            "greeting": {"$ref": "#/$defs/HelloWorld"},
            "content": {"type": "string", "contentEncoding": "base64" },
            "foo": { "type": "string" },
            "baz": {"type": "number", "format": "int64" },
            "bar": { "$ref": "#/$defs/TwoLevels" },
            "myTree": {"$ref": "#/$defs/Tree" },
            "tensor": {"$ref": "#/$defs/Tensor" },
            "tuple": {"$ref": "#/$defs/SomeTuple" },
            "anonymousArray": {"type": "array"}
        },
        "$defs": {
            "HelloWorld": {"const": "Hello, World!" },
            "SomeTuple": {"type": "array", "prefixItems": [{"type": "string"}, {"type": "number"}], "items": false },
            "Tensor": {"type": "array", "items": {"$ref": "#/$defs/Rank2"}, "minItems": 10, "maxItems": 10},
            "Rank2": {"type": "array", "items": {"$ref": "#/$defs/Rank3"}, "minItems": 5, "maxItems": 5},
            "Rank3": {"type": "array", "items": {"type": "number", "format": "int32" }, "minItems": 8, "maxItems": 8},
            "Tree": {"type": "array", "items": {"$ref": "#/$defs/Tree"} },
            "TwoLevels": {"$ref": "#/$defs/PositiveInteger" },
            "PositiveInteger": { "type": "number", "format": "int32", "exclusiveMinimum": 0 }
        }
    }
    """);

using var testDoc2 = JsonDocument.Parse(
    """
    {
        "$schema": "https://json-schema.org/draft/2020-12/schema",
        "$id": "test:/test-doc-2.json",
        "type": "object",
        "required": ["baz"],
        "properties": {
            "baz": { "$ref": "test:/test-doc.json#/$defs/TwoLevels" }
        }
    }
    """);

documentResolver.AddDocument("test:/test-doc.json", testDoc);
documentResolver.AddDocument("test:/test-doc-2.json", testDoc2);
Metaschema.AddMetaschema(documentResolver);

VocabularyRegistry vocabularyRegistry = new();

// Add support for the vocabularies we are interested in.
Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
// This will be our fallback vocabulary
IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);

// We can add one or more type declarations to build, and it will efficiently
// generate the types we haven't previously seen.
TypeDeclaration typeDeclaration1 =
    await typeBuilder.AddTypeDeclarationsAsync(
        new("test:/test-doc.json"),
        defaultVocabulary).ConfigureAwait(false);

TypeDeclaration typeDeclaration2 =
    await typeBuilder.AddTypeDeclarationsAsync(
        new("test:/test-doc-2.json"),
        defaultVocabulary).ConfigureAwait(false);

TypeDeclaration draft202012MetaSchema =
    await typeBuilder.AddTypeDeclarationsAsync(
        new("https://json-schema.org/draft/2020-12/schema"),
        defaultVocabulary).ConfigureAwait(false);


// And then generate code for each of the root types in which we are interested, using the language providers concerned
IReadOnlyCollection<GeneratedCodeFile> generatedCode =
    typeBuilder.GenerateCodeUsing(
        CSharpLanguageProvider.DefaultWithOptions(new("Corvus.Json.JsonSchema.Draft202012")),
        typeDeclaration1,
        typeDeclaration2,
        draft202012MetaSchema);

Console.ReadKey();