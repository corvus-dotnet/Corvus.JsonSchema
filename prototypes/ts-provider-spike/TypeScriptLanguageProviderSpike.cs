using System.Diagnostics.CodeAnalysis;
using System.Text;
using Corvus.Json.CodeGeneration;

namespace TsProviderSpike;

// Minimal feasibility spike: a real ILanguageProvider that plugs into the existing
// language-neutral core (JsonSchemaTypeBuilder.GenerateCodeUsing). It does NOT do
// validation or real naming heuristics — it only proves the seam works end to end:
// the core accepts the provider, runs the pipeline, hands us the reduced/named type
// graph, and we emit a TypeScript interface per type from the real model.
public sealed class TypeScriptLanguageProviderSpike : ILanguageProvider
{
    private const string TsNameKey = "Ts_TypeName";

    public ILanguageProvider RegisterCodeFileBuilders(params ICodeFileBuilder[] builders) => this;

    public ILanguageProvider RegisterValidationHandlers(params IKeywordValidationHandler[] handlers) => this;

    public ILanguageProvider RegisterNameHeuristics(params INameHeuristic[] heuristics) => this;

    public bool TryGetValidationHandlersFor(IKeyword keyword, [NotNullWhen(true)] out IReadOnlyCollection<IKeywordValidationHandler>? validationHandlers)
    {
        validationHandlers = null;
        return false;
    }

    public bool ShouldGenerate(TypeDeclaration type) => true;

    public void IdentifyNonGeneratedType(TypeDeclaration typeDeclaration, CancellationToken cancellationToken)
    {
        // No built-ins recognised in the spike.
    }

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
        var files = new List<GeneratedCodeFile>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (TypeDeclaration td in typeDeclarations)
        {
            string baseName = td.TryGetMetadata<string>(TsNameKey, out string? n) && !string.IsNullOrEmpty(n)
                ? n!
                : "GeneratedType";

            string name = baseName;
            int suffix = 2;
            while (!used.Add(name))
            {
                name = baseName + suffix++;
            }

            var sb = new StringBuilder();
            sb.Append("export interface ").Append(name).Append(" {\n");
            foreach (PropertyDeclaration property in td.PropertyDeclarations)
            {
                sb.Append("  readonly ").Append(property.JsonPropertyName).Append("?: unknown;\n");
            }

            sb.Append("}\n");

            files.Add(new GeneratedCodeFile(name + ".ts", sb.ToString(), td));
        }

        return files;
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
