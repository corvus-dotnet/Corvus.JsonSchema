// Analyzer probe for update-generated-code-analyzer-config.ps1. Loads the analyzer assemblies given as arguments,
// runs every C# DiagnosticAnalyzer's Initialize against a recording context and prints one JSON document describing
// each analyzer: its generated-code flags (an analyzer that never calls ConfigureGeneratedCodeAnalysis gets the
// compiler's default, Analyze | ReportDiagnostics), whether it registers symbol-start actions, and its descriptors.
// It is a file-based app so that it hosts the analyzers on the Roslyn version they were built against, whatever
// PowerShell bundles. Run: dotnet run --file analyzers/AnalyzerProbe.cs -- <assembly.dll> ...
#:package Microsoft.CodeAnalysis.CSharp@5.3.0
#:property ManagePackageVersionsCentrally=false
#:property RunAnalyzers=false
#:property JsonSerializerIsReflectionEnabledByDefault=true
#:property Nullable=enable
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

List<AnalyzerReport> reports = [];

// Each package folder gets its own load context that shares the Roslyn and framework assemblies of the host and
// resolves everything else by assembly name from the folder (a package may ship a dependency under another file
// name, and two packages, or two versions of one, may ship assemblies of the same name).
Dictionary<string, PackageLoadContext> contexts = new(StringComparer.Ordinal);

foreach (string path in args)
{
    string directory = Path.GetDirectoryName(Path.GetFullPath(path))!;
    if (!contexts.TryGetValue(directory, out PackageLoadContext? context))
    {
        context = new PackageLoadContext(directory);
        contexts.Add(directory, context);
    }

    Assembly assembly = context.LoadFromAssemblyPath(Path.GetFullPath(path));
    Type[] types;
    try
    {
        types = assembly.GetTypes();
    }
    catch (ReflectionTypeLoadException e)
    {
        types = [.. e.Types.Where(t => t is not null)!];
    }

    foreach (Type type in types)
    {
        if (type.IsAbstract || !typeof(DiagnosticAnalyzer).IsAssignableFrom(type))
        {
            continue;
        }

        DiagnosticAnalyzerAttribute? attribute = type.GetCustomAttribute<DiagnosticAnalyzerAttribute>();
        if (attribute is null || !attribute.Languages.Contains(LanguageNames.CSharp))
        {
            continue;
        }

        DiagnosticAnalyzer analyzer = (DiagnosticAnalyzer)Activator.CreateInstance(type)!;
        RecordingAnalysisContext recorder = new();
        string? error = null;
        try
        {
            analyzer.Initialize(recorder);
        }
        catch (Exception e)
        {
            error = e.GetType().Name + ": " + e.Message;
        }

        reports.Add(new AnalyzerReport(
            Path.GetFileName(path),
            assembly.GetName().Version?.ToString() ?? string.Empty,
            type.FullName!,
            (recorder.Flags ?? (GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics)).ToString(),
            recorder.Flags is null,
            recorder.SymbolStart,
            recorder.RegistersActions,
            error,
            [.. analyzer.SupportedDiagnostics.Select(d => new DescriptorReport(
                d.Id,
                d.DefaultSeverity.ToString(),
                d.IsEnabledByDefault,
                d.CustomTags.Contains(WellKnownDiagnosticTags.NotConfigurable),
                d.CustomTags.Contains(WellKnownDiagnosticTags.CustomSeverityConfigurable),
                d.CustomTags.Contains(WellKnownDiagnosticTags.CompilationEnd)))]));
    }
}

Console.WriteLine(JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = false }));

sealed record AnalyzerReport(
    string Assembly,
    string AssemblyVersion,
    string Type,
    string GeneratedCodeFlags,
    bool FlagsAreDefault,
    bool RegistersSymbolStart,
    bool RegistersActions,
    string? InitializeError,
    DescriptorReport[] Descriptors);

sealed record DescriptorReport(string Id, string DefaultSeverity, bool IsEnabledByDefault, bool NotConfigurable, bool CustomSeverityConfigurable, bool CompilationEnd);

sealed class PackageLoadContext : AssemblyLoadContext
{
    private readonly string directory;

    public PackageLoadContext(string directory)
        : base(directory, isCollectible: false)
    {
        this.directory = directory;
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Roslyn, the framework and the probe itself come from the host; everything else from the package folder.
        if (assemblyName.Name is null ||
            assemblyName.Name.StartsWith("Microsoft.CodeAnalysis", StringComparison.Ordinal) ||
            assemblyName.Name.StartsWith("System.", StringComparison.Ordinal) ||
            assemblyName.Name is "System" or "netstandard" or "mscorlib")
        {
            return null;
        }

        foreach (string candidate in Directory.EnumerateFiles(this.directory, "*.dll"))
        {
            try
            {
                if (string.Equals(AssemblyName.GetAssemblyName(candidate).Name, assemblyName.Name, StringComparison.OrdinalIgnoreCase))
                {
                    return this.LoadFromAssemblyPath(candidate);
                }
            }
            catch (BadImageFormatException)
            {
            }
        }

        return null;
    }
}

sealed class RecordingAnalysisContext : AnalysisContext
{
    public GeneratedCodeAnalysisFlags? Flags { get; private set; }

    public bool SymbolStart { get; private set; }

    public bool RegistersActions { get; private set; }

    public override void ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags analysisMode) => this.Flags = analysisMode;

    public override void EnableConcurrentExecution()
    {
    }

    public override void RegisterCodeBlockAction(Action<CodeBlockAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterCodeBlockStartAction<TLanguageKindEnum>(Action<CodeBlockStartAnalysisContext<TLanguageKindEnum>> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterCompilationAction(Action<CompilationAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterCompilationStartAction(Action<CompilationStartAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterSemanticModelAction(Action<SemanticModelAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterSymbolAction(Action<SymbolAnalysisContext> action, ImmutableArray<SymbolKind> symbolKinds)
    {
        this.RegistersActions = true;
    }

    public override void RegisterSymbolStartAction(Action<SymbolStartAnalysisContext> action, SymbolKind symbolKind)
    {
        this.SymbolStart = true;
        this.RegistersActions = true;
    }

    public override void RegisterSyntaxNodeAction<TLanguageKindEnum>(Action<SyntaxNodeAnalysisContext> action, ImmutableArray<TLanguageKindEnum> syntaxKinds)
    {
        this.RegistersActions = true;
    }

    public override void RegisterSyntaxTreeAction(Action<SyntaxTreeAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterOperationAction(Action<OperationAnalysisContext> action, ImmutableArray<OperationKind> operationKinds)
    {
        this.RegistersActions = true;
    }

    public override void RegisterOperationBlockAction(Action<OperationBlockAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterOperationBlockStartAction(Action<OperationBlockStartAnalysisContext> action)
    {
        this.RegistersActions = true;
    }

    public override void RegisterAdditionalFileAction(Action<AdditionalFileAnalysisContext> action)
    {
        this.RegistersActions = true;
    }
}