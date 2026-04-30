// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Collections.Concurrent;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Validator;

namespace TestUtilities;

/// <summary>
/// Generates and compiles standalone schema evaluator code from JSON schemas.
/// </summary>
public class TestEvaluatorHelper
{
    private static readonly ConcurrentDictionary<string, CompiledEvaluator> s_cache = new();

    private readonly IDocumentResolver _documentResolver;

    private readonly VocabularyRegistry _vocabularyRegistry;

    private readonly JsonSchemaTypeBuilder _jsonSchemaTypeBuilder;

    private readonly IVocabulary _defaultVocabulary;

    private readonly string? _remotesBaseDirectory;

    private readonly bool _validateFormat;

    private TestEvaluatorHelper(
        string remotesBaseDirectory,
        IVocabulary? defaultVocabulary = null,
        bool validateFormat = true)
    {
        _remotesBaseDirectory = remotesBaseDirectory;
        _documentResolver =
            new CompoundDocumentResolver(
                new FakeWebDocumentResolver(_remotesBaseDirectory!),
                new FileSystemDocumentResolver())
                    .AddMetaschema();

        _vocabularyRegistry = new();
        RegisterVocabularies();

        _jsonSchemaTypeBuilder = new(_documentResolver, _vocabularyRegistry);
        _validateFormat = validateFormat;
        _defaultVocabulary = defaultVocabulary ?? Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
    }

    private TestEvaluatorHelper(
        string remotesBaseDirectory,
        string defaultVocabulary,
        bool validateFormat = true)
    {
        _remotesBaseDirectory = remotesBaseDirectory;
        _documentResolver =
            new CompoundDocumentResolver(
                new FakeWebDocumentResolver(_remotesBaseDirectory!),
                new FileSystemDocumentResolver())
                    .AddMetaschema();

        _vocabularyRegistry = new();
        RegisterVocabularies();

        _jsonSchemaTypeBuilder = new(_documentResolver, _vocabularyRegistry);
        _validateFormat = validateFormat;

        if (!_vocabularyRegistry.TryGetSchemaDialect(defaultVocabulary, out _defaultVocabulary))
        {
            _defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
        }
    }

    /// <summary>
    /// Generate a compiled evaluator for the given virtual file.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary for the test run.</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>A <see cref="CompiledEvaluator"/> for the schema.</returns>
    public static async ValueTask<CompiledEvaluator> GenerateEvaluatorForVirtualFileAsync(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        IVocabulary defaultVocabulary,
        bool validateFormat,
        Assembly hostAssembly)
    {
        string key = $"eval_{schemaText}_{validateFormat}";
        if (s_cache.TryGetValue(key, out CompiledEvaluator? value))
        {
            return value;
        }

        var helper = new TestEvaluatorHelper(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat);

        CompiledEvaluator result = await helper.GenerateAndCompileAsync(virtualFilename, schemaText, TestJsonSchemaCodeGenerator.ToPascalCase(defaultNamespace), hostAssembly);
        return s_cache.GetOrAdd(key, _ => result);
    }

    /// <summary>
    /// Generate a compiled evaluator for the given virtual file.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary URI string (e.g. <c>"https://json-schema.org/draft/2020-12/schema"</c>).</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>A <see cref="CompiledEvaluator"/> for the schema.</returns>
    public static async ValueTask<CompiledEvaluator> GenerateEvaluatorForVirtualFileAsync(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        string defaultVocabulary,
        bool validateFormat,
        Assembly hostAssembly)
    {
        string key = $"eval_{schemaText}_{defaultVocabulary}_{validateFormat}";
        if (s_cache.TryGetValue(key, out CompiledEvaluator? value))
        {
            return value;
        }

        var helper = new TestEvaluatorHelper(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat);

        CompiledEvaluator result = await helper.GenerateAndCompileAsync(virtualFilename, schemaText, TestJsonSchemaCodeGenerator.ToPascalCase(defaultNamespace), hostAssembly);
        return s_cache.GetOrAdd(key, _ => result);
    }

    private void RegisterVocabularies()
    {
        Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.RegisterAnalyser(_documentResolver, _vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft201909.VocabularyAnalyser.RegisterAnalyser(_documentResolver, _vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft7.VocabularyAnalyser.RegisterAnalyser(_vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft6.VocabularyAnalyser.RegisterAnalyser(_vocabularyRegistry);
        Corvus.Json.CodeGeneration.Draft4.VocabularyAnalyser.RegisterAnalyser(_vocabularyRegistry);
        Corvus.Json.CodeGeneration.OpenApi30.VocabularyAnalyser.RegisterAnalyser(_vocabularyRegistry);
    }

    private async ValueTask<CompiledEvaluator> GenerateAndCompileAsync(string virtualFileName, string jsonSchema, string defaultNamespace, Assembly hostAssembly)
    {
        string path = Path.Combine(_remotesBaseDirectory!, virtualFileName);
        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(path, out string? normalizedPath))
        {
            path = normalizedPath;
        }

        var options = new CSharpLanguageProvider.Options(
            defaultNamespace,
            alwaysAssertFormat: _validateFormat,
            addExplicitUsings: true,
            codeGenerationMode: CodeGenerationMode.SchemaEvaluationOnly);

        _jsonSchemaTypeBuilder.AddDocument(path, System.Text.Json.JsonDocument.Parse(jsonSchema));

        CSharpLanguageProvider languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);

        TypeDeclaration rootType = await _jsonSchemaTypeBuilder.AddTypeDeclarationsAsync(new JsonReference(path), _defaultVocabulary, true);

        // Store the original (unreduced) root type before the pipeline reduces it.
        // The pipeline's GetCandidateTypesToGenerate replaces annotation-only schemas
        // with their reduced target (e.g., JsonAny), which loses the keyword information
        // needed by the evaluator.
        languageProvider.SetEvaluatorRootTypes(rootType);

        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
            _jsonSchemaTypeBuilder.GenerateCodeUsing(
                languageProvider,
                CancellationToken.None,
                rootType);

        // Find the evaluator class name from the generated code files.
        // The evaluator file follows the naming pattern {EvaluatorClassName}.Evaluator.cs
        GeneratedCodeFile? evaluatorFile = null;
        foreach (GeneratedCodeFile f in generatedCode)
        {
            if (f.FileName.Contains(".Evaluator."))
            {
                evaluatorFile = f;
                break;
            }
        }

        if (evaluatorFile is null)
        {
            throw new InvalidOperationException(
                "No evaluator file found in generated code. Files: " +
                string.Join(", ", generatedCode.Select(f => f.FileName)));
        }

        // Extract the class name: "FooEvaluator.Evaluator.cs" → "FooEvaluator"
        string evaluatorClassName = evaluatorFile.FileName;
        int dotEvaluator = evaluatorClassName.IndexOf(".Evaluator.");
        if (dotEvaluator >= 0)
        {
            evaluatorClassName = evaluatorClassName[..dotEvaluator];
        }

        string evaluatorTypeName = $"{defaultNamespace}.{evaluatorClassName}";

        Type evaluatorType = DynamicCompiler.CompileGeneratedType(evaluatorTypeName, generatedCode, hostAssembly);

        return new CompiledEvaluator(evaluatorType, evaluatorFile.FileContent);
    }
}

/// <summary>
/// Wraps a dynamically compiled standalone schema evaluator and provides
/// reflection-based invocation of its <c>Evaluate&lt;TElement&gt;</c> method.
/// </summary>
public class CompiledEvaluator
{
    private readonly Type _evaluatorType;

    private readonly MethodInfo _evaluateMethodDef;

    internal CompiledEvaluator(Type evaluatorType, string? generatedCode = null)
    {
        _evaluatorType = evaluatorType;
        GeneratedCode = generatedCode;

        _evaluateMethodDef = evaluatorType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(m => m.Name == "Evaluate" && m.IsGenericMethodDefinition);
    }

    /// <summary>
    /// Gets the compiled evaluator <see cref="Type"/>.
    /// </summary>
    public Type EvaluatorType => _evaluatorType;

    /// <summary>
    /// Gets the generated source code of the evaluator (if available).
    /// </summary>
    public string? GeneratedCode { get; }

    /// <summary>
    /// Evaluates a <see cref="JsonElement"/> instance against the compiled schema evaluator.
    /// </summary>
    /// <param name="instance">The JSON element to evaluate.</param>
    /// <param name="resultsCollector">An optional results collector.</param>
    /// <returns><see langword="true"/> if the instance is valid against the schema.</returns>
    public bool Evaluate(JsonElement instance, IJsonSchemaResultsCollector? resultsCollector = null)
    {
        MethodInfo evaluateMethod = _evaluateMethodDef.MakeGenericMethod(typeof(JsonElement));
        object? result = evaluateMethod.Invoke(null, [instance, resultsCollector]);
        return (bool)result!;
    }
}