using Microsoft.CodeAnalysis;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Compiles generated C# code (from V5 codegen) + user code into a runnable assembly
/// using Roslyn's CSharpCompilation, entirely in the browser.
/// </summary>
public class CompilationService
{
    private readonly WorkspaceService workspaceService;

    public CompilationService(WorkspaceService workspaceService)
    {
        this.workspaceService = workspaceService;
    }

    /// <summary>
    /// Compile the generated code files together with user code into an in-memory assembly.
    /// </summary>
    public async Task<CompilationResult> CompileAsync(
        IReadOnlyCollection<GeneratedCodeFile> generatedFiles,
        string userCode)
    {
        await this.workspaceService.EnsureInitializedAsync();

        // Extract the C# source text from each generated code file
        IEnumerable<string> generatedSources = generatedFiles.Select(f => f.FileContent);

        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation =
            this.workspaceService.CreateCompilation(generatedSources, userCode);

        // Yield before the heavy Emit() call so the browser can process
        // pending UI updates (Blazor WASM is single-threaded).
        await Task.Yield();

        using var ms = new MemoryStream();
        Microsoft.CodeAnalysis.Emit.EmitResult result = compilation.Emit(ms);

        if (!result.Success)
        {
            return new CompilationResult
            {
                Success = false,
                Diagnostics = result.Diagnostics
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList(),
            };
        }

        ms.Seek(0, SeekOrigin.Begin);

        return new CompilationResult
        {
            Success = true,
            Assembly = ms.ToArray(),
            Diagnostics = result.Diagnostics
                .Where(d => d.Severity >= DiagnosticSeverity.Warning)
                .ToList(),
        };
    }
}

/// <summary>
/// Result of compiling generated + user code.
/// </summary>
public class CompilationResult
{
    /// <summary>
    /// Gets or sets a value indicating whether compilation succeeded.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the compiled assembly bytes (null if compilation failed).
    /// </summary>
    public byte[]? Assembly { get; set; }

    /// <summary>
    /// Gets or sets the compiler diagnostics (errors, warnings).
    /// </summary>
    public List<Diagnostic> Diagnostics { get; set; } = [];
}
