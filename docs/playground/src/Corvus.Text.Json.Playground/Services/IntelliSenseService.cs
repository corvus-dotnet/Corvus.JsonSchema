using System.Reflection;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.Text;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Provides Roslyn-backed IntelliSense (completions) for the user code editor.
/// Maintains an AdhocWorkspace with a Document that includes global usings,
/// generated code, and user code — then delegates to Roslyn's CompletionService.
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
    private IReadOnlyCollection<Corvus.Json.CodeGeneration.GeneratedCodeFile>? generatedFiles;

    // Track the prefix length (global usings + generated code) so we can map
    // the user's cursor position to the correct offset in the combined document.
    private int prefixLength;

    public IntelliSenseService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Update the workspace with newly generated code files.
    /// Called after each successful code generation.
    /// </summary>
    public void UpdateGeneratedCode(IReadOnlyCollection<Corvus.Json.CodeGeneration.GeneratedCodeFile> files)
    {
        this.generatedFiles = files;
        this.workspace?.Dispose();
        this.workspace = null;
        this.projectId = null;
        this.userDocId = null;
    }

    /// <summary>
    /// Get completion items at the given position in the user code.
    /// </summary>
    private const int MaxCompletionItems = 100;

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

            // Map the user's editor line/column to absolute position in the document
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
        catch (Exception)
        {
            return [];
        }
    }

    /// <summary>
    /// Get signature help at the given position in the user code.
    /// Uses the Roslyn semantic model to find method overloads and parameter info.
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

            // Walk up to find the ArgumentList and its parent invocation
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
                    // Indexer — not supported for now
                    return null;
                }
            }

            if (argList is null || invocation is null)
            {
                return null;
            }

            // Determine which parameter index the cursor is at
            int activeParameter = 0;
            foreach (SyntaxToken separator in argList.Arguments.GetSeparators())
            {
                if (position > separator.SpanStart)
                {
                    activeParameter++;
                }
            }

            // Get the method symbol(s)
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

            // Also look for overloads via the method group
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

                // Mark the best match as active
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
            // Update just the user code document text
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
            "PlaygroundProject",
            "PlaygroundProject",
            LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest)
                .WithPreprocessorSymbols("NET", "NET10_0", "NET10_0_OR_GREATER",
                    "NET9_0_OR_GREATER", "NET8_0_OR_GREATER", "NET7_0_OR_GREATER"),
            compilationOptions: new CSharpCompilationOptions(OutputKind.ConsoleApplication),
            metadataReferences: this.workspaceService.References);

        Solution solution = this.workspace.CurrentSolution.AddProject(projectInfo);

        // Add each generated file as a separate document
        if (this.generatedFiles is not null)
        {
            int i = 0;
            foreach (var file in this.generatedFiles)
            {
                var docId = DocumentId.CreateNewId(this.projectId);
                solution = solution.AddDocument(
                    docId,
                    $"Generated_{i}.cs",
                    SourceText.From(file.FileContent));
                i++;
            }
        }

        // Add user code as its own document (with global usings prepended)
        this.userDocId = DocumentId.CreateNewId(this.projectId);
        string userSource = WorkspaceService.GlobalUsings + "\n" + userCode;

        // Track the prefix (global usings) so we can map cursor positions
        this.prefixLength = WorkspaceService.GlobalUsings.Length + 1; // +1 for newline

        solution = solution.AddDocument(
            this.userDocId,
            "Program.cs",
            SourceText.From(userSource));

        this.workspace.TryApplyChanges(solution);
    }

    private int GetUserCodeStartLine(SourceText sourceText)
    {
        // Find which line the user code starts at (after prefix)
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
        // Map Roslyn tags to Monaco CompletionItemKind names
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

    /// <summary>
    /// Extracts human-readable text from a Roslyn XML documentation comment.
    /// Parses summary, returns, and remarks elements into clean text.
    /// </summary>
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

    /// <summary>
    /// Extracts the documentation text for a specific parameter from an XML doc comment.
    /// </summary>
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
        // Process child nodes to handle <see>, <paramref>, <typeparamref>, <c> etc.
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
        // Collapse whitespace and trim
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
