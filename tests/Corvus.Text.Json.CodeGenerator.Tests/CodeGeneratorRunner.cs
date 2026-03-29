using System.Diagnostics;
using System.Text;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Helper for invoking the code generator CLI tool as a process.
/// </summary>
internal static class CodeGeneratorRunner
{
    private static readonly string CodeGeneratorProjectPath = Path.GetFullPath(
        Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "src", "Corvus.Json.CodeGenerator", "Corvus.Json.CodeGenerator.csproj"));

    // Derive the build configuration (Debug/Release) from the test output path.
    // AppContext.BaseDirectory is e.g. .../bin/Debug/net10.0/
    private static readonly string BuildConfiguration =
        new DirectoryInfo(AppContext.BaseDirectory).Parent!.Name;

    /// <summary>
    /// Runs the code generator with the specified arguments and returns the result.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(string arguments, string workingDirectory = null, int timeoutSeconds = 120)
    {
        string effectiveWorkingDirectory = workingDirectory ?? AppContext.BaseDirectory;

        ProcessStartInfo psi = new()
        {
            FileName = "dotnet",
            Arguments = $"run --no-build --no-launch-profile -c {BuildConfiguration} --framework net10.0 --project \"{CodeGeneratorProjectPath}\" -- {arguments}",
            WorkingDirectory = effectiveWorkingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using Process process = new() { StartInfo = psi };

        StringBuilder stdout = new();
        StringBuilder stderr = new();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stdout.AppendLine(e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null)
            {
                stderr.AppendLine(e.Data);
            }
        };

        process.Start();
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using CancellationTokenSource cts = new(TimeSpan.FromSeconds(timeoutSeconds));

        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException(
                $"Code generator process timed out after {timeoutSeconds}s. Stdout: {stdout} Stderr: {stderr}");
        }

        return new ProcessResult(process.ExitCode, stdout.ToString(), stderr.ToString());
    }

    /// <summary>
    /// Gets the path to a test fixture file relative to the output directory.
    /// </summary>
    public static string GetFixturePath(params string[] pathSegments)
    {
        return Path.Combine([AppContext.BaseDirectory, "TestFixtures", .. pathSegments]);
    }

    /// <summary>
    /// Creates a temporary directory for test output and returns its path.
    /// </summary>
    public static string CreateTempOutputDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "CodeGenTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    /// <summary>
    /// Cleans up a temporary output directory.
    /// </summary>
    public static void CleanupTempDirectory(string path)
    {
        if (Directory.Exists(path))
        {
            try
            {
                Directory.Delete(path, recursive: true);
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}

/// <summary>
/// Result of running the code generator process.
/// </summary>
internal record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    /// <summary>
    /// Gets the combined output (stdout + stderr).
    /// </summary>
    public string CombinedOutput => StandardOutput + StandardError;
}