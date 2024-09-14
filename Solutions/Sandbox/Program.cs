using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.CSharp;
using Corvus.Json.CodeGeneration.CSharp.QuickStart;

var options = new CSharpLanguageProvider.Options("Test");

var generator = CSharpGenerator.Create(options: options);

IReadOnlyCollection<GeneratedCodeFile> generatedFiles =
    await generator.GenerateFilesAsync(new JsonReference("./CombinedDocumentSchema.json"));

Console.ReadLine();