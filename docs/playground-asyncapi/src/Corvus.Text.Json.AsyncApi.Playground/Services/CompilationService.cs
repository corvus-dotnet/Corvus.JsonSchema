using Microsoft.CodeAnalysis;
using Corvus.Text.Json.AsyncApi.Playground.Models;

namespace Corvus.Text.Json.AsyncApi.Playground.Services;

/// <summary>
/// Compiles generated C# code + user code into a runnable assembly using Roslyn.
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
        IReadOnlyCollection<GeneratedFile> generatedFiles,
        string userCode)
    {
        await this.workspaceService.EnsureInitializedAsync();

        IEnumerable<string> generatedSources = generatedFiles.Select(f => f.Content);
        Microsoft.CodeAnalysis.CSharp.CSharpCompilation compilation =
            this.workspaceService.CreateCompilation(generatedSources, userCode);

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