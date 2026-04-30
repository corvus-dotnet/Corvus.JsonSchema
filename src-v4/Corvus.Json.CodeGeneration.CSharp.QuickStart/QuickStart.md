# Embedding C# code generation

You can easily embed a C# code generator in your application by using the
`Corvus.Json.CodeGeneration.CSharp.QuickStart.CSharpGenerator` class.

This class is part of the `Corvus.Json.CodeGeneration.CSharp.QuickStart` nuget package.

Here is an example of how to use it:

```csharp
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.CSharp.QuickStart;

var options = new CSharpLanguageProvider.Options("My.Test.Namespace");

var generator = CSharpGenerator.Create(options: options);

IReadOnlyCollection<GeneratedCodeFile> generatedFiles =
    await generator.GenerateFilesAsync(new JsonReference("./CombinedDocumentSchema.json"));
```

This uses the default schema vocabularies, will be able to the base schema files using the local FileSystem or via HTTP.

## Customize document resolution

There are overloads of `Create()` that will allow you to provide your own `PrepopulatedDocumentResolver`,
or a callback delegate to resolve the files.

## Referencing the schema to generate

You can provide a complete reference to a schema, including a JSON Pointer in the fragment
e.g. `"SomeSchemaFile.json#/$defs/SomeEmbeddedSchema"` or `"SomeOpenApiDoc.json#/components/schema/MySchema"`.

You only need provide "root"" schema - all the directly referenced subschema from that root will be discovered
and generated in the usual way.

Note that this *does not* include unreferenced schema in e.g. a `$defs` section.

There is also an overload that allows you to pass mutliple references to generate multiple schemas in a single batch.

This will be more efficient than calling generate multiple times, especially if there are shared subschema between references.