using System.Diagnostics.CodeAnalysis;
using System.Text;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Feasibility spike #3: emit validators through the CORE handler-composition framework, so that
// user/third-party extensions plug in via RegisterValidationHandlers (the extensibility seam the C#
// provider, OpenApi and AsyncApi all use) — not a hard-coded walk-the-model emitter.
//
// Dispatch is the REAL core registry: KeywordValidationHandlerRegistry.RegisterValidationHandlers /
// TryGetHandlersFor, keyed on IKeywordValidationHandler.HandlesKeyword + ValidationHandlerPriority.
// Each handler additionally implements ITsKeywordEmitter to emit TS. For each type, the validator
// body is composed purely from the handlers the registry dispatches for that type's keywords.
public sealed class TypeScriptLanguageProviderSpike : IHierarchicalLanguageProvider
{
    private const string TsNameKey = "Ts_TypeName";
    private const string TsFinalKey = "Ts_FinalName";

    private readonly KeywordValidationHandlerRegistry validationHandlers = new();

    // The default provider registers the BASE handler set. Note multipleOf is intentionally NOT here
    // — it is registered later as an extension (see Program), to prove the registry-driven seam.
    public static TypeScriptLanguageProviderSpike CreateDefault()
    {
        var p = new TypeScriptLanguageProviderSpike();
        p.RegisterValidationHandlers(
            new TsTypeHandler(),
            new TsPropertiesHandler(),
            new TsStringLengthHandler(),
            new TsNumberRangeHandler(),
            new TsPatternHandler(),
            new TsEnumHandler());
        return p;
    }

    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers)
    {
        this.validationHandlers.RegisterValidationHandlers(handlers);
        return this;
    }

    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders) => this;

    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics) => this;

    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
        => this.validationHandlers.TryGetHandlersFor(keyword, out validationHandlers);

    public bool ShouldGenerate(TypeDeclaration type) => true;

    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
    }

    public void SetParent(TypeDeclaration child, TypeDeclaration? parent)
    {
    }

    public TypeDeclaration? GetParent(TypeDeclaration child) => null;

    public IReadOnlyCollection<TypeDeclaration> GetChildren(TypeDeclaration typeDeclaration) => [];

    public void SetNamesBeforeSubschema(TypeDeclaration typeDeclaration, string fallbackName, CancellationToken cancellationToken)
    {
        if (!typeDeclaration.TryGetMetadata<string>(TsNameKey, out string? existing) || string.IsNullOrEmpty(existing))
        {
            typeDeclaration.SetMetadata(TsNameKey, Sanitize(fallbackName));
        }
    }

    public void SetNamesAfterSubschema(TypeDeclaration typeDeclaration, IEnumerable<TypeDeclaration> existingTypeDeclarations, CancellationToken cancellationToken)
    {
        if (!typeDeclaration.TryGetMetadata<string>(TsNameKey, out string? existing) || string.IsNullOrEmpty(existing))
        {
            typeDeclaration.SetMetadata(TsNameKey, "GeneratedType");
        }
    }

    public IReadOnlyCollection<GeneratedCodeFile> GenerateCodeFor(IEnumerable<TypeDeclaration> typeDeclarations, CancellationToken cancellationToken)
    {
        List<TypeDeclaration> types = typeDeclarations.ToList();

        // Pass 1: assign final, unique TS names so cross-references resolve.
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (TypeDeclaration td in types)
        {
            string baseName = td.TryGetMetadata<string>(TsNameKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "GeneratedType";
            string name = baseName;
            int suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix++;
            }

            td.SetMetadata(TsFinalKey, name);
        }

        // Pass 2: emit interface + registry-composed validator per type.
        var sb = new StringBuilder();
        sb.Append("// AUTO-GENERATED: interfaces + validators composed from registered handlers.\n\n");
        foreach (TypeDeclaration td in types)
        {
            EmitInterface(sb, td);
            EmitValidator(sb, td);
        }

        return new[] { new GeneratedCodeFile("generated.ts", sb.ToString()) };
    }

    private static string FinalName(TypeDeclaration td)
        => td.TryGetMetadata<string>(TsFinalKey, out string? n) && !string.IsNullOrEmpty(n) ? n! : "GeneratedType";

    private static void EmitInterface(StringBuilder sb, TypeDeclaration td)
    {
        sb.Append("export interface ").Append(FinalName(td)).Append(" {\n");
        foreach (PropertyDeclaration p in td.PropertyDeclarations)
        {
            string opt = p.RequiredOrOptional == RequiredOrOptional.Optional ? "?" : string.Empty;
            sb.Append("  readonly ").Append(TsEmit.Str(p.JsonPropertyName)).Append(opt).Append(": unknown;\n");
        }

        sb.Append("}\n\n");
    }

    // The validator body is composed entirely from handlers the core registry dispatches for this
    // type's keywords, ordered by ValidationHandlerPriority. Registering an extra handler (e.g. the
    // multipleOf extension) adds a step here without any change to this method.
    private void EmitValidator(StringBuilder sb, TypeDeclaration td)
    {
        sb.Append("export function evaluate").Append(FinalName(td)).Append("(value: unknown): boolean {\n");

        var steps = new List<(uint Priority, ITsKeywordEmitter Emitter, IKeyword Keyword)>();
        foreach (IKeyword keyword in td.Keywords())
        {
            if (this.validationHandlers.TryGetHandlersFor(keyword, out IReadOnlyCollection<IKeywordValidationHandler>? handlers))
            {
                foreach (IKeywordValidationHandler handler in handlers)
                {
                    if (handler is ITsKeywordEmitter emitter)
                    {
                        steps.Add((handler.ValidationHandlerPriority, emitter, keyword));
                    }
                }
            }
        }

        foreach ((uint _, ITsKeywordEmitter emitter, IKeyword keyword) in steps.OrderBy(s => s.Priority))
        {
            emitter.Emit(sb, td, keyword);
        }

        sb.Append("  return true;\n}\n\n");
    }

    private static string Sanitize(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "GeneratedType";
        }

        var sb = new StringBuilder();
        bool upperNext = true;
        foreach (char c in value)
        {
            if (char.IsLetterOrDigit(c))
            {
                sb.Append(upperNext ? char.ToUpperInvariant(c) : c);
                upperNext = false;
            }
            else
            {
                upperNext = true;
            }
        }

        if (sb.Length == 0)
        {
            return "GeneratedType";
        }

        if (char.IsDigit(sb[0]))
        {
            sb.Insert(0, '_');
        }

        return sb.ToString();
    }
}
