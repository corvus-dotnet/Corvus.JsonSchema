using System.Reflection;
using System.Runtime.Loader;

namespace Corvus.Text.Json.Playground.Services;

/// <summary>
/// Executes a compiled assembly on a background thread, capturing Console.Out output.
/// Uses a CollectibleAssemblyLoadContext so assemblies can be unloaded after execution.
/// </summary>
public class ExecutionService
{
    /// <summary>
    /// Execute the compiled assembly and return the console output.
    /// </summary>
    public async Task<ExecutionResult> ExecuteAsync(
        byte[] assemblyBytes,
        CancellationToken cancellationToken = default)
    {
        var outputWriter = new StringWriter();

        try
        {
            await Task.Run(
                () => ExecuteOnBackgroundThread(assemblyBytes, outputWriter, cancellationToken),
                cancellationToken);

            return new ExecutionResult
            {
                Success = true,
                Output = outputWriter.ToString(),
            };
        }
        catch (OperationCanceledException)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = outputWriter.ToString(),
                ErrorMessage = "Execution cancelled.",
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = outputWriter.ToString(),
                ErrorMessage = ex.Message,
            };
        }
    }

    private static void ExecuteOnBackgroundThread(
        byte[] assemblyBytes,
        StringWriter outputWriter,
        CancellationToken cancellationToken)
    {
        // Redirect Console.Out to our writer
        TextWriter originalOut = Console.Out;
        TextWriter originalError = Console.Error;
        Console.SetOut(outputWriter);
        Console.SetError(outputWriter);

        try
        {
            using var ms = new MemoryStream(assemblyBytes);
            var context = new CollectibleAssemblyLoadContext();
            Assembly assembly = context.LoadFromStream(ms);

            MethodInfo? entryPoint = assembly.EntryPoint;
            if (entryPoint is null)
            {
                outputWriter.WriteLine("Error: No entry point found in the compiled assembly.");
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();

            ParameterInfo[] parameters = entryPoint.GetParameters();
            object?[] args = parameters.Length > 0
                ? [Array.Empty<string>()]
                : [];

            object? result = entryPoint.Invoke(null, args);

            // Handle async entry points
            if (result is Task task)
            {
                task.GetAwaiter().GetResult();
            }

            context.Unload();
        }
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            outputWriter.WriteLine($"Runtime error: {ex.InnerException.Message}");
            if (ex.InnerException.StackTrace is not null)
            {
                outputWriter.WriteLine(ex.InnerException.StackTrace);
            }
        }
        catch (OperationCanceledException)
        {
            outputWriter.WriteLine("Execution cancelled.");
            throw;
        }
        catch (Exception ex)
        {
            outputWriter.WriteLine($"Runtime error: {ex.Message}");
            if (ex.StackTrace is not null)
            {
                outputWriter.WriteLine(ex.StackTrace);
            }
        }
        finally
        {
            Console.SetOut(originalOut);
            Console.SetError(originalError);
        }
    }

    /// <summary>
    /// An assembly load context that can be unloaded to free memory.
    /// </summary>
    private sealed class CollectibleAssemblyLoadContext : AssemblyLoadContext
    {
        public CollectibleAssemblyLoadContext()
            : base(isCollectible: true)
        {
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            // Return null to fall back to the default context
            return null;
        }
    }
}

/// <summary>
/// Result of executing compiled code.
/// </summary>
public class ExecutionResult
{
    /// <summary>
    /// Gets or sets a value indicating whether execution completed without errors.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets the captured console output.
    /// </summary>
    public string Output { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the error message if execution failed.
    /// </summary>
    public string? ErrorMessage { get; set; }
}
