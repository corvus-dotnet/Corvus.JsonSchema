// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Corvus.Json;
using Corvus.Json.CodeGeneration;
using Corvus.Json.CodeGeneration.DocumentResolvers;
using Corvus.Text.Json;
using Corvus.Text.Json.CodeGeneration;
using Corvus.Text.Json.Internal;
using Corvus.Text.Json.Validator;

namespace TestUtilities;

public class TestJsonSchemaCodeGenerator
{
    private static int s_compilationCount;

    private readonly IDocumentResolver _documentResolver;

    private readonly VocabularyRegistry _vocabularyRegistry;

    private readonly JsonSchemaTypeBuilder _jsonSchemaTypeBuilder;

    private readonly IVocabulary _defaultVocabulary;

    private readonly string? _remotesBaseDirectory;

    private readonly bool _validateFormat;

    private readonly bool _optionalAsNullable;

    private readonly bool _useImplicitOperatorString;

    private readonly bool _addExplicitUsings;

    private TestJsonSchemaCodeGenerator(
        string remotesBaseDirectory,
        IVocabulary? defaultVocabulary = null,
        bool validateFormat = true,
        bool optionalAsNullable = false,
        bool useImplicitOperatorString = false,
        bool addExplicitUsings = true)
        : this(remotesBaseDirectory, null, defaultVocabulary, validateFormat, optionalAsNullable, useImplicitOperatorString, addExplicitUsings)
    {
    }

    private TestJsonSchemaCodeGenerator(
        string remotesBaseDirectory,
        string defaultVocabulary,
        bool validateFormat = true,
        bool optionalAsNullable = false,
        bool useImplicitOperatorString = false,
        bool addExplicitUsings = true)
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
        _optionalAsNullable = optionalAsNullable;
        _useImplicitOperatorString = useImplicitOperatorString;
        _addExplicitUsings = addExplicitUsings;

        if (!_vocabularyRegistry.TryGetSchemaDialect(defaultVocabulary, out _defaultVocabulary))
        {
            _defaultVocabulary = Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
        }
    }

    private TestJsonSchemaCodeGenerator(
        string remotesBaseDirectory,
        IDocumentResolver? resolver,
        IVocabulary? defaultVocabulary = null,
        bool validateFormat = true,
        bool optionalAsNullable = false,
        bool useImplicitOperatorString = false,
        bool addExplicitUsings = true)
    {
        _remotesBaseDirectory = remotesBaseDirectory;
        _documentResolver =
            resolver?.AddMetaschema() ??
            new CompoundDocumentResolver(
                new FakeWebDocumentResolver(_remotesBaseDirectory!),
                new FileSystemDocumentResolver())
                    .AddMetaschema();

        _vocabularyRegistry = new();
        RegisterVocabularies();

        _jsonSchemaTypeBuilder = new(_documentResolver, _vocabularyRegistry);
        _validateFormat = validateFormat;
        _optionalAsNullable = optionalAsNullable;
        _useImplicitOperatorString = useImplicitOperatorString;
        _addExplicitUsings = addExplicitUsings;

        _defaultVocabulary = defaultVocabulary ?? Corvus.Json.CodeGeneration.Draft202012.VocabularyAnalyser.DefaultVocabulary;
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

    /// <summary>
    /// Generate the JSON type for the given virtual file.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary for the test run.</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="optionalAsNullable">Whether to treat optional as nullable.</param>
    /// <param name="useImplicitOperatorString">Whether to generate implicit conversions to string.</param>
    /// <param name="addExplicitUsings">Whether to add explicit usings for the generated code, or rely on the global usings.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>A task which, when complete, provides the <see cref="DynamicJsonType"/> for the schema.</returns>
    public static async ValueTask<DynamicJsonType> GenerateTypeForVirtualFile(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        IVocabulary defaultVocabulary,
        bool validateFormat,
        bool optionalAsNullable,
        bool useImplicitOperatorString,
        bool addExplicitUsings,
        Assembly hostAssembly)
    {
        var generator = new TestJsonSchemaCodeGenerator(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat,
            optionalAsNullable: optionalAsNullable,
            useImplicitOperatorString: useImplicitOperatorString,
            addExplicitUsings: addExplicitUsings);

        GeneratedCode generatedCode = await generator.GenerateCodeAsync(virtualFilename, schemaText, defaultNamespace: ToPascalCase(defaultNamespace));
        return Compile(generatedCode, hostAssembly);
    }

    /// <summary>
    /// Generate the JSON type for the given virtual file.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary for the test run.</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="optionalAsNullable">Whether to treat optional as nullable.</param>
    /// <param name="useImplicitOperatorString">Whether to generate implicit conversions to string.</param>
    /// <param name="addExplicitUsings">Whether to add explicit usings for the generated code, or rely on the global usings.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>A task which, when complete, provides the <see cref="DynamicJsonType"/> for the schema.</returns>
    public static async ValueTask<DynamicJsonType> GenerateTypeForVirtualFile(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        string defaultVocabulary,
        bool validateFormat,
        bool optionalAsNullable,
        bool useImplicitOperatorString,
        bool addExplicitUsings,
        Assembly hostAssembly)
    {
        var generator = new TestJsonSchemaCodeGenerator(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat,
            optionalAsNullable: optionalAsNullable,
            useImplicitOperatorString: useImplicitOperatorString,
            addExplicitUsings: addExplicitUsings);

        GeneratedCode generatedCode = await generator.GenerateCodeAsync(virtualFilename, schemaText, defaultNamespace: ToPascalCase(defaultNamespace));
        return Compile(generatedCode, hostAssembly);
    }

    /// <summary>
    /// Generate the JSON type for the given test suite entry.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary for the test run.</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="optionalAsNullable">Whether to treat optional as nullable.</param>
    /// <param name="useImplicitOperatorString">Whether to generate implicit conversions to string.</param>
    /// <param name="addExplicitUsings">Whether to add explicit usings for the generated code, or rely on the global usings.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>The <see cref="DynamicJsonType"/> for the schema.</returns>

    public static DynamicJsonType SynchronouslyGenerateTypeForVirtualFile(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        IVocabulary defaultVocabulary,
        bool validateFormat,
        bool optionalAsNullable,
        bool useImplicitOperatorString,
        bool addExplicitUsings,
        Assembly hostAssembly)
    {
        var generator = new TestJsonSchemaCodeGenerator(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat,
            optionalAsNullable: optionalAsNullable,
            useImplicitOperatorString: useImplicitOperatorString,
            addExplicitUsings: addExplicitUsings);

        GeneratedCode generatedCode = generator.GenerateCodeSync(virtualFilename, schemaText, defaultNamespace: ToPascalCase(defaultNamespace));
        return Compile(generatedCode, hostAssembly);
    }

    /// <summary>
    /// Generate the JSON type for the given test suite entry.
    /// </summary>
    /// <param name="virtualFilename">The virtual file name.</param>
    /// <param name="schemaText">The text of the virtual schema file.</param>
    /// <param name="defaultNamespace">The default namespace for code generation.</param>
    /// <param name="remotesBaseDirectory">The remotes base directory for the test suite.</param>
    /// <param name="defaultVocabulary">The default vocabulary for the test run.</param>
    /// <param name="validateFormat">Whether to enforce format validation rather than just evaluation.</param>
    /// <param name="optionalAsNullable">Whether to treat optional as nullable.</param>
    /// <param name="useImplicitOperatorString">Whether to generate implicit conversions to string.</param>
    /// <param name="addExplicitUsings">Whether to add explicit usings for the generated code, or rely on the global usings.</param>
    /// <param name="hostAssembly">The host assembly with preserved compilation context.</param>
    /// <returns>The <see cref="DynamicJsonType"/> for the schema.</returns>

    public static DynamicJsonType SynchronouslyGenerateTypeForVirtualFile(
        string virtualFilename,
        string schemaText,
        string defaultNamespace,
        string remotesBaseDirectory,
        string defaultVocabulary,
        bool validateFormat,
        bool optionalAsNullable,
        bool useImplicitOperatorString,
        bool addExplicitUsings,
        Assembly hostAssembly)
    {
        var generator = new TestJsonSchemaCodeGenerator(
            remotesBaseDirectory,
            defaultVocabulary: defaultVocabulary,
            validateFormat: validateFormat,
            optionalAsNullable: optionalAsNullable,
            useImplicitOperatorString: useImplicitOperatorString,
            addExplicitUsings: addExplicitUsings);

        GeneratedCode generatedCode = generator.GenerateCodeSync(virtualFilename, schemaText, defaultNamespace: ToPascalCase(defaultNamespace));
        return Compile(generatedCode, hostAssembly);
    }

    /// <summary>
    /// Compiles the generated code.
    /// </summary>
    /// <param name="code">The generated code.</param>
    /// <param name="hostAssembly">The assembly hosting the generator to use for build metadata context.</param>
    /// <returns>A <see cref="Task"/>, which, when complete, provides the <see cref="Type"/> of the instance.</returns>
    private static DynamicJsonType Compile(GeneratedCode code, Assembly hostAssembly)
    {
        // Need to establish if the output type is JsonElement
        if (code.RootType.IsBuiltInJsonAnyType())
        {
            return new(typeof(JsonElement));
        }

        if (code.RootType.IsBuiltInJsonNotAnyType())
        {
            return new(typeof(JsonElementForBooleanFalseSchema));
        }

        int count = Interlocked.Increment(ref s_compilationCount);
        string rootTypeName = code.RootType.FullyQualifiedDotnetTypeName()!;

        // Force GC every 10 compilations to allow collectible ALCs to unload (net8.0+)
        // and to free Roslyn temporary objects. Without this, memory grows faster than
        // the background GC can reclaim, causing OOM on CI runners with 7 GB RAM.
        if (count % 10 == 0)
        {
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true);
        }

        long memMB = System.Diagnostics.Process.GetCurrentProcess().WorkingSet64 / (1024 * 1024);

        // Write diagnostics to a log file that survives even if the process is
        // SIGKILL'd (OOM), since the OS flushes file buffers on process exit.
        string diagLine = $"[DIAG] Compilation #{count} | Mem={memMB}MB | Type={rootTypeName}";
        Console.Error.WriteLine(diagLine);
        Console.Error.Flush();
        WriteDiagnosticLog(diagLine);

        Type generatedType = Corvus.Text.Json.Validator.DynamicCompiler.CompileGeneratedType(
            rootTypeName,
            code.GeneratedFiles,
            hostAssembly);

        return new(generatedType);
    }

    private static void WriteDiagnosticLog(string message)
    {
        try
        {
            string logDir = Path.Combine(
                Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? ".",
                "TestResults");
            Directory.CreateDirectory(logDir);
            string logPath = Path.Combine(logDir, "compilation-diagnostics.log");
            using var writer = new StreamWriter(logPath, append: true);
            writer.WriteLine($"{System.DateTime.UtcNow:HH:mm:ss.fff} {message}");
            writer.Flush();
        }
        catch
        {
            // Best-effort diagnostics — don't fail the test if logging fails
        }
    }

    /// <summary>
    /// Generates code for the given schema with the specified virutal file name.
    /// </summary>
    /// <param name="virtualFileName">The virtual file name.</param>
    /// <param name="jsonSchema">The schema to compile.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the generated code for the schema, with the root type declaration for that schema.</returns>
    private async Task<GeneratedCode> GenerateCodeAsync(string virtualFileName, string jsonSchema, string defaultNamespace = "Test")
    {
        ConfigureGeneration(virtualFileName, jsonSchema, defaultNamespace, out string path, out CSharpLanguageProvider languageProvider);

        TypeDeclaration rootType = await _jsonSchemaTypeBuilder.AddTypeDeclarationsAsync(new Corvus.Json.JsonReference(path), _defaultVocabulary, true);

        return GenerateCodeForRootType(languageProvider, rootType);
    }

    /// <summary>
    /// Generates code for the given schema with the specified virutal file name.
    /// </summary>
    /// <param name="virtualFileName">The virtual file name.</param>
    /// <param name="jsonSchema">The schema to compile.</param>
    /// <returns>A <see cref="Task"/> which, when complete, provides the generated code for the schema, with the root type declaration for that schema.</returns>
    private GeneratedCode GenerateCodeSync(string virtualFileName, string jsonSchema, string defaultNamespace = "Test")
    {
        ConfigureGeneration(virtualFileName, jsonSchema, defaultNamespace, out string path, out CSharpLanguageProvider languageProvider);

        TypeDeclaration rootType = _jsonSchemaTypeBuilder.AddTypeDeclarations(new Corvus.Json.JsonReference(path), _defaultVocabulary, true);

        return GenerateCodeForRootType(languageProvider, rootType);
    }

    private GeneratedCode GenerateCodeForRootType(CSharpLanguageProvider languageProvider, TypeDeclaration rootType)
    {
        IReadOnlyCollection<GeneratedCodeFile> generatedCode =
                        _jsonSchemaTypeBuilder.GenerateCodeUsing(
                        languageProvider,
                        CancellationToken.None,
                        rootType);

        // Return the fully reduced type declaration
        return new(rootType.ReducedTypeDeclaration().ReducedType, generatedCode);
    }

    private void ConfigureGeneration(string virtualFileName, string jsonSchema, string defaultNamespace, out string path, out CSharpLanguageProvider languageProvider)
    {
        path = Path.Combine(_remotesBaseDirectory!, virtualFileName);
        if (SchemaReferenceNormalization.TryNormalizeSchemaReference(path, out string? result))
        {
            path = result;
        }

        var options = new CSharpLanguageProvider.Options(
            defaultNamespace,
            alwaysAssertFormat: _validateFormat,
            optionalAsNullable: _optionalAsNullable,
            useImplicitOperatorString: _useImplicitOperatorString,
            addExplicitUsings: _addExplicitUsings);

        _jsonSchemaTypeBuilder.AddDocument(path, System.Text.Json.JsonDocument.Parse(jsonSchema));

        languageProvider = CSharpLanguageProvider.DefaultWithOptions(options);
    }

    public static string ToPascalCase(string name)
    {
        Span<char> buffer = stackalloc char[name.Length];
        name.AsSpan().CopyTo(buffer);
        int length = Formatting.ToPascalCase(buffer);
        return buffer.Slice(0, length).ToString();
    }
}

/// <summary>
/// Creates an instance of the <see cref="GeneratedCode"/>.
/// </summary>
/// <param name="rootType">The root type.</param>
/// <param name="generatedFiles">The generated files.</param>
public readonly struct GeneratedCode(TypeDeclaration rootType, IReadOnlyCollection<GeneratedCodeFile> generatedFiles)
{
    public TypeDeclaration RootType { get; } = rootType;

    public IReadOnlyCollection<GeneratedCodeFile> GeneratedFiles { get; } = generatedFiles;
}

/// <summary>
/// Extension methods for <see cref="DynamicJsonElement"/> providing test-specific helpers.
/// </summary>
public static class DynamicJsonElementExtensions
{
    public static DynamicJsonElement CastFrom<TSource>(TSource s, DynamicJsonType target)
    {
        ParameterExpression p = Expression.Parameter(typeof(TSource));
        UnaryExpression c = Expression.ConvertChecked(p, target.Type);
        UnaryExpression toIJsonElement = Expression.ConvertChecked(c, typeof(IJsonElement));
        return new(target.Type, Expression.Lambda<Func<TSource, IJsonElement>>(toIJsonElement, p).Compile()(s));
    }

    public static bool HasDotnetPropertyValue(this DynamicJsonElement element, string propertyName)
    {
        PropertyInfo property = element.Type.GetProperty(propertyName) ?? throw new InvalidOperationException($"Property {propertyName} of type {element.Type.FullName} is not a JSON element");
        object? value = property.GetValue(element.Element);

        return value is IJsonElement v && v.TokenType == JsonTokenType.None;
    }

    public static bool CompareDotnetPropertyStringValue(this DynamicJsonElement element, string propertyName, string expectedValue)
    {
        PropertyInfo? property = element.Type.GetProperty(propertyName);
        if (property is null)
        {
            return false;
        }

        object? value = property.GetValue(element.Element);

        if (value is not IJsonElement v)
        {
            if (value is null)
            {
                return expectedValue == "null";
            }

            throw new InvalidOperationException($"Property {propertyName} of type {element.Type.FullName} is not a JSON element");
        }

        if (v.TokenType != JsonTokenType.String)
        {
            return false;
        }

        string? actualValue = v.ParentDocument.GetString(v.ParentDocumentIndex, JsonTokenType.String);

        return expectedValue.Equals(actualValue);
    }

    public static bool CompareNullableDotnetPropertyStringValue(this DynamicJsonElement element, string propertyName, string expectedValue)
    {
        PropertyInfo? property = element.Type.GetProperty(propertyName);
        if (property is null)
        {
            return false;
        }

        object? value = property.GetValue(element.Element);

        if (value is not IJsonElement v)
        {
            if (value is null)
            {
                return expectedValue == "null";
            }

            throw new InvalidOperationException($"Property {propertyName} of type {element.Type.FullName} is not a JSON element");
        }

        if (expectedValue == "Null")
        {
            return v.TokenType == JsonTokenType.Null;
        }

        if (expectedValue == "Undefined")
        {
            return v.TokenType == JsonTokenType.None;
        }

        if (v.TokenType != JsonTokenType.String)
        {
            return false;
        }

        string? actualValue = v.ParentDocument.GetString(v.ParentDocumentIndex, JsonTokenType.String);

        return expectedValue.Equals(actualValue);
    }
}