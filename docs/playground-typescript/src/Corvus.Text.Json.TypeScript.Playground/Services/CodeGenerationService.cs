using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json.TypeScript.CodeGeneration;
using Corvus.Text.Json.TypeScript.Playground.Models;

namespace Corvus.Text.Json.TypeScript.Playground.Services;

/// <summary>
/// Wraps the TypeScript code-generation engine (<see cref="TypeScriptLanguageProvider"/>) for use in a WASM
/// context: parse + register schemas, register the dialect vocabularies, build the type declarations, and emit
/// the TypeScript module (generated.ts + the shared corvus-runtime.ts), with a stable evaluateRoot entry point.
/// </summary>
public class CodeGenerationService
{
    private const string SchemaBaseUri = "schema://playground/";

    /// <summary>
    /// Generate a TypeScript module from one or more JSON Schema files.
    /// </summary>
    /// <param name="schemaFiles">The schema files (at least one marked as a root type).</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generation result (generated TypeScript + runtime, or errors).</returns>
    public async Task<GenerationResult> GenerateAsync(
        IReadOnlyList<SchemaFile> schemaFiles,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Prepopulated resolver (no file I/O, WASM-safe).
            using PrepopulatedDocumentResolver documentResolver = new();
            documentResolver.AddMetaschema();

            var parsedSchemas = new List<(SchemaFile File, string Uri)>();
            var schemaErrors = new List<SchemaError>();

            foreach (SchemaFile schemaFile in schemaFiles)
            {
                System.Text.Json.JsonDocument schemaDoc;
                try
                {
                    schemaDoc = System.Text.Json.JsonDocument.Parse(schemaFile.Content);
                }
                catch (System.Text.Json.JsonException ex)
                {
                    schemaErrors.Add(new SchemaError(schemaFile.Name, ex.Message, ex.LineNumber, ex.BytePositionInLine));
                    continue;
                }

                string uri = SchemaBaseUri + schemaFile.Name;
                documentResolver.AddDocument(uri, schemaDoc);

                // Also register by $id (critical for cross-file $ref resolution).
                if (schemaDoc.RootElement.TryGetProperty("$id", out System.Text.Json.JsonElement idElement) &&
                    idElement.ValueKind == System.Text.Json.JsonValueKind.String &&
                    idElement.GetString() is string id)
                {
                    documentResolver.AddDocument(id, schemaDoc);
                }

                parsedSchemas.Add((schemaFile, uri));
            }

            if (schemaErrors.Count > 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = $"JSON parse errors in {schemaErrors.Count} schema file(s)",
                    SchemaErrors = schemaErrors,
                };
            }

            // Register the supported dialect vocabularies (same set as the CLI / the C# playground).
            VocabularyRegistry vocabularyRegistry = new();
            Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(documentResolver, vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(vocabularyRegistry);
            vocabularyRegistry.RegisterVocabularies(
                Corvus.Json.CodeGeneration.CorvusVocabulary.SchemaVocabulary.DefaultInstance);

            IVocabulary defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;

            JsonSchemaTypeBuilder typeBuilder = new(documentResolver, vocabularyRegistry);
            var rootTypes = new List<TypeDeclaration>();

            foreach ((SchemaFile file, string uri) in parsedSchemas)
            {
                if (!file.IsRootType)
                {
                    continue;
                }

                JsonReference reference = new(uri);
                TypeDeclaration rootType = await typeBuilder.AddTypeDeclarationsAsync(reference, defaultVocabulary, rebaseAsRoot: false);
                rootTypes.Add(rootType);
            }

            if (rootTypes.Count == 0)
            {
                return new GenerationResult
                {
                    Success = false,
                    ErrorMessage = "No root types selected for generation. Mark at least one schema as a root type.",
                };
            }

            TypeScriptLanguageProvider provider = TypeScriptLanguageProvider.DefaultWithOptions(
                new TypeScriptLanguageProvider.Options(AlwaysAssertFormat: true));

            IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                typeBuilder.GenerateCodeUsing(provider, rootTypes, cancellationToken);

            // The generated types/validators module, plus the appended evaluateRoot entry point (the driver's
            // second step); the runtime module is emitted alongside it.
            string generatedTs = generatedCode.FirstOrDefault(f => f.FileName == "generated.ts")?.FileContent ?? string.Empty;
            generatedTs += provider.RootEvaluatorExport(rootTypes[0]);
            string runtimeTs = generatedCode.FirstOrDefault(f => f.FileName == "corvus-runtime.ts")?.FileContent ?? string.Empty;

            return new GenerationResult
            {
                Success = true,
                GeneratedFiles = generatedCode,
                GeneratedTypeScript = generatedTs,
                RuntimeTypeScript = runtimeTs,
                TypeMap = [],
            };
        }
        catch (Exception ex)
        {
            var rootSchemaNames = schemaFiles.Where(f => f.IsRootType).Select(f => f.Name).ToList();
            var errors = rootSchemaNames.Count > 0
                ? rootSchemaNames.Select(n => new SchemaError(n, ex.Message, null, null)).ToList()
                : [new SchemaError(schemaFiles.Count > 0 ? schemaFiles[0].Name : "schema.json", ex.Message, null, null)];

            return new GenerationResult
            {
                Success = false,
                ErrorMessage = $"Code generation failed: {ex.Message}",
                SchemaErrors = errors,
            };
        }
    }

    /// <summary>
    /// Single-schema convenience overload.
    /// </summary>
    /// <param name="schemaJson">The JSON Schema source.</param>
    /// <param name="cancellationToken">A cancellation token.</param>
    /// <returns>The generation result.</returns>
    public Task<GenerationResult> GenerateAsync(string schemaJson, CancellationToken cancellationToken = default)
        => this.GenerateAsync(
            [new SchemaFile { Name = "user-schema.json", Content = schemaJson, IsRootType = true }],
            cancellationToken);
}
