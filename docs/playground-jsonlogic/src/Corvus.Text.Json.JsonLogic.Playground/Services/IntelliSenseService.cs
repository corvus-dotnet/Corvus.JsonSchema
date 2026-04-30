using System.Reflection;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.JsonLogic.Playground.Services;

/// <summary>
/// Provides Roslyn-backed IntelliSense (completions and signature help)
/// for the custom operators C# editor.
/// </summary>
public class IntelliSenseService
{
    private static readonly MefHostServices HostServices = MefHostServices.Create(
        MefHostServices.DefaultAssemblies.Concat(
        [
            Assembly.Load("Microsoft.CodeAnalysis.Features"),
            Assembly.Load("Microsoft.CodeAnalysis.CSharp.Features"),
        ]).Distinct().ToList());

    private readonly WorkspaceService workspaceService;
    private AdhocWorkspace? workspace;
    private ProjectId? projectId;
    private DocumentId? userDocId;
    private int prefixLength;

    private const int MaxCompletionItems = 100;

    public IntelliSenseService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Get completion items at the given position in the user code.
    /// </summary>
    public async Task<IReadOnlyList<CompletionItemInfo>> GetCompletionsAsync(
        string userCode,
        int lineNumber,
        int column)
    {
        try
        {
            await this.workspaceService.EnsureInitializedAsync();
            Document document = this.EnsureDocument(userCode);

            CompletionService? completionService = CompletionService.GetService(document);
            if (completionService is null)
            {
                return [];
            }

            SourceText sourceText = await document.GetTextAsync();
            int userCodeStartLine = this.GetUserCodeStartLine(sourceText);

            int absoluteLine = userCodeStartLine + lineNumber - 1;
            if (absoluteLine < 0 || absoluteLine >= sourceText.Lines.Count)
            {
                return [];
            }

            int position = sourceText.Lines[absoluteLine].Start + column - 1;
            if (position < 0 || position > sourceText.Length)
            {
                return [];
            }

            CompletionList? completions = await completionService.GetCompletionsAsync(
                document,
                position);

            if (completions is null)
            {
                return [];
            }

            return completions.ItemsList
                .Take(MaxCompletionItems)
                .Select(item => new CompletionItemInfo(
                    item.DisplayText,
                    item.InlineDescription ?? string.Empty,
                    item.SortText,
                    item.FilterText,
                    MapKind(item.Tags)))
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Get signature help at the given position in the user code.
    /// </summary>
    public async Task<SignatureHelpResult?> GetSignatureHelpAsync(
        string userCode,
        int lineNumber,
        int column)
    {
        try
        {
            await this.workspaceService.EnsureInitializedAsync();
            Document document = this.EnsureDocument(userCode);

            SourceText sourceText = await document.GetTextAsync();
            int userCodeStartLine = this.GetUserCodeStartLine(sourceText);

            int absoluteLine = userCodeStartLine + lineNumber - 1;
            if (absoluteLine < 0 || absoluteLine >= sourceText.Lines.Count)
            {
                return null;
            }

            int position = sourceText.Lines[absoluteLine].Start + column - 1;
            if (position < 0 || position > sourceText.Length)
            {
                return null;
            }

            SemanticModel? semanticModel = await document.GetSemanticModelAsync();
            SyntaxTree? syntaxTree = await document.GetSyntaxTreeAsync();
            if (semanticModel is null || syntaxTree is null)
            {
                return null;
            }

            SyntaxNode root = await syntaxTree.GetRootAsync();
            SyntaxToken token = root.FindToken(position);
            SyntaxNode? node = token.Parent;

            ArgumentListSyntax? argList = null;
            SyntaxNode? invocation = null;

            for (SyntaxNode? current = node; current is not null; current = current.Parent)
            {
                if (current is ArgumentListSyntax als)
                {
                    argList = als;
                    invocation = als.Parent;
                    break;
                }

                if (current is BracketedArgumentListSyntax)
                {
                    return null;
                }
            }

            if (argList is null || invocation is null)
            {
                return null;
            }

            int activeParameter = 0;
            foreach (SyntaxToken separator in argList.Arguments.GetSeparators())
            {
                if (position > separator.SpanStart)
                {
                    activeParameter++;
                }
            }

            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(invocation);
            var candidateSymbols = new List<IMethodSymbol>();

            if (symbolInfo.Symbol is IMethodSymbol method)
            {
                candidateSymbols.Add(method);
            }

            foreach (ISymbol candidate in symbolInfo.CandidateSymbols)
            {
                if (candidate is IMethodSymbol candidateMethod)
                {
                    candidateSymbols.Add(candidateMethod);
                }
            }

            if (invocation is InvocationExpressionSyntax invExpr)
            {
                var memberGroup = semanticModel.GetMemberGroup(invExpr.Expression);
                foreach (ISymbol member in memberGroup)
                {
                    if (member is IMethodSymbol overload && !candidateSymbols.Contains(overload, SymbolEqualityComparer.Default))
                    {
                        candidateSymbols.Add(overload);
                    }
                }
            }
            else if (invocation is ObjectCreationExpressionSyntax objCreation)
            {
                Microsoft.CodeAnalysis.TypeInfo typeInfo = semanticModel.GetTypeInfo(objCreation);
                if (typeInfo.Type is INamedTypeSymbol namedType)
                {
                    foreach (IMethodSymbol ctor in namedType.Constructors)
                    {
                        if (!ctor.IsImplicitlyDeclared && !candidateSymbols.Contains(ctor, SymbolEqualityComparer.Default))
                        {
                            candidateSymbols.Add(ctor);
                        }
                    }
                }
            }

            if (candidateSymbols.Count == 0)
            {
                return null;
            }

            var signatures = new List<SignatureInfo>();
            int activeSignature = 0;

            for (int i = 0; i < candidateSymbols.Count; i++)
            {
                IMethodSymbol m = candidateSymbols[i];

                var parameters = m.Parameters.Select(p =>
                    new SignatureParameterInfo(
                        $"{p.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)} {p.Name}",
                        ExtractParameterDoc(m.GetDocumentationCommentXml(), p.Name)))
                    .ToList();

                string label = m.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
                string doc = FormatXmlDoc(m.GetDocumentationCommentXml());

                signatures.Add(new SignatureInfo(label, doc, parameters));

                if (SymbolEqualityComparer.Default.Equals(m, symbolInfo.Symbol))
                {
                    activeSignature = i;
                }
            }

            return new SignatureHelpResult(signatures, activeSignature, activeParameter);
        }
        catch
        {
            return null;
        }
    }

    private Document EnsureDocument(string userCode)
    {
        if (this.workspace is null || this.projectId is null || this.userDocId is null)
        {
            this.BuildWorkspace(userCode);
        }
        else
        {
            Solution solution = this.workspace.CurrentSolution;
            string userSource = WorkspaceService.GlobalUsings + "\n" + userCode;
            SourceText newText = SourceText.From(userSource);
            solution = solution.WithDocumentText(this.userDocId, newText);
            this.workspace.TryApplyChanges(solution);
        }

        return this.workspace!.CurrentSolution.GetDocument(this.userDocId!)!;
    }

    private void BuildWorkspace(string userCode)
    {
        this.workspace?.Dispose();

        this.workspace = new AdhocWorkspace(HostServices);

        this.projectId = ProjectId.CreateNewId();
        var projectInfo = ProjectInfo.Create(
            this.projectId,
            VersionStamp.Default,
            "OperatorsProject",
            "OperatorsProject",
            LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
                .WithPreprocessorSymbols("NET", "NET10_0", "NET10_0_OR_GREATER",
                    "NET9_0_OR_GREATER", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER",
                    "DYNAMIC_BUILD"),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary),
            metadataReferences: this.workspaceService.References);

        Solution solution = this.workspace.CurrentSolution.AddProject(projectInfo);

        this.userDocId = DocumentId.CreateNewId(this.projectId);
        string userSource = WorkspaceService.GlobalUsings + "\n" + userCode;
        this.prefixLength = WorkspaceService.GlobalUsings.Length + 1;

        solution = solution.AddDocument(
            this.userDocId,
            "CustomOperators.cs",
            SourceText.From(userSource));

        this.workspace.TryApplyChanges(solution);
    }

    private int GetUserCodeStartLine(SourceText sourceText)
    {
        if (this.prefixLength <= 0)
        {
            return 0;
        }

        for (int i = 0; i < sourceText.Lines.Count; i++)
        {
            if (sourceText.Lines[i].Start >= this.prefixLength)
            {
                return i;
            }
        }

        return sourceText.Lines.Count;
    }

    private static string MapKind(System.Collections.Immutable.ImmutableArray<string> tags)
    {
        if (tags.IsDefaultOrEmpty)
        {
            return "Text";
        }

        string first = tags[0];
        return first switch
        {
            "Class" or "Structure" or "Struct" => "Class",
            "Interface" => "Interface",
            "Enum" => "Enum",
            "EnumMember" => "EnumMember",
            "Method" or "ExtensionMethod" => "Method",
            "Property" => "Property",
            "Field" => "Field",
            "Event" => "Event",
            "Local" or "Parameter" => "Variable",
            "Namespace" => "Module",
            "Keyword" => "Keyword",
            "Snippet" => "Snippet",
            "Constant" => "Constant",
            "Delegate" => "Function",
            "TypeParameter" => "TypeParameter",
            _ => "Text",
        };
    }

    internal static string FormatXmlDoc(string? xml)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            XDocument doc = XDocument.Parse(xml);
            var parts = new List<string>();

            string? summary = GetElementText(doc, "summary");
            if (!string.IsNullOrWhiteSpace(summary))
            {
                parts.Add(summary);
            }

            string? returns = GetElementText(doc, "returns");
            if (!string.IsNullOrWhiteSpace(returns))
            {
                parts.Add($"**Returns:** {returns}");
            }

            string? remarks = GetElementText(doc, "remarks");
            if (!string.IsNullOrWhiteSpace(remarks))
            {
                parts.Add($"*{remarks}*");
            }

            return string.Join("\n\n", parts);
        }
        catch
        {
            return string.Empty;
        }
    }

    internal static string ExtractParameterDoc(string? xml, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(xml))
        {
            return string.Empty;
        }

        try
        {
            XDocument doc = XDocument.Parse(xml);
            XElement? paramElement = doc.Descendants("param")
                .FirstOrDefault(e => e.Attribute("name")?.Value == parameterName);

            if (paramElement is null)
            {
                return string.Empty;
            }

            return NormalizeText(GetInnerText(paramElement));
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string? GetElementText(XDocument doc, string elementName)
    {
        XElement? element = doc.Descendants(elementName).FirstOrDefault();
        if (element is null)
        {
            return null;
        }

        return NormalizeText(GetInnerText(element));
    }

    private static string GetInnerText(XElement element)
    {
        var sb = new System.Text.StringBuilder();

        foreach (XNode node in element.Nodes())
        {
            if (node is XText text)
            {
                sb.Append(text.Value);
            }
            else if (node is XElement child)
            {
                switch (child.Name.LocalName)
                {
                    case "see":
                    case "seealso":
                        sb.Append(child.Attribute("cref")?.Value?.Split('.').Last()
                            ?? child.Attribute("langword")?.Value
                            ?? child.Value);
                        break;
                    case "paramref":
                    case "typeparamref":
                        sb.Append(child.Attribute("name")?.Value ?? child.Value);
                        break;
                    case "c":
                        sb.Append($"`{child.Value}`");
                        break;
                    case "para":
                        sb.Append($"\n\n{GetInnerText(child)}");
                        break;
                    default:
                        sb.Append(child.Value);
                        break;
                }
            }
        }

        return sb.ToString();
    }

    private static string NormalizeText(string text)
    {
        string[] lines = text.Split('\n');
        var trimmed = lines.Select(l => l.Trim()).Where(l => l.Length > 0);
        return string.Join(" ", trimmed);
    }
}

/// <summary>
/// A completion item to be sent to the Monaco editor.
/// </summary>
public record CompletionItemInfo(
    string Label,
    string Detail,
    string SortText,
    string FilterText,
    string Kind);

/// <summary>
/// Signature help result to be sent to the Monaco editor.
/// </summary>
public record SignatureHelpResult(
    IReadOnlyList<SignatureInfo> Signatures,
    int ActiveSignature,
    int ActiveParameter);

/// <summary>
/// A single method signature.
/// </summary>
public record SignatureInfo(
    string Label,
    string Documentation,
    IReadOnlyList<SignatureParameterInfo> Parameters);

/// <summary>
/// A single method parameter in a signature.
/// </summary>
public record SignatureParameterInfo(
    string Label,
    string Documentation);
