using System.Reflection;
using System.Runtime.Loader;

namespace Corvus.Text.Json.AsyncApi.Playground.Services;

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
                return new ExecutionResult
                {
                    Success = false,
                    Output = outputWriter.ToString(),
                    ErrorMessage = "No entry point found.",
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            // The C# compiler generates a sync Main wrapper that internally calls
            // .GetAwaiter().GetResult() on the async state machine. In WASM
            // (single-threaded), that blocks and throws "Cannot wait on monitors".
            // We must find the actual async <Main>$ method and await it directly.
            MethodInfo invokeTarget = entryPoint;
            if (entryPoint.ReturnType == typeof(void))
            {
                MethodInfo? asyncMain = entryPoint.DeclaringType?.GetMethod(
                    "<Main>$",
                    BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public,
                    null,
                    [typeof(string[])],
                    null);

                if (asyncMain is not null && typeof(Task).IsAssignableFrom(asyncMain.ReturnType))
                {
                    invokeTarget = asyncMain;
                }
            }

            ParameterInfo[] parameters = invokeTarget.GetParameters();
            object?[] args = parameters.Length > 0
                ? [Array.Empty<string>()]
                : [];

            object? result = invokeTarget.Invoke(null, args);

            // Await the task if the entry point is async.
            if (result is Task task)
            {
                await task;
            }

            context.Unload();

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
        catch (TargetInvocationException ex) when (ex.InnerException is not null)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = outputWriter.ToString(),
                ErrorMessage = $"Runtime error: {ex.InnerException.Message}\n{ex.InnerException.StackTrace}",
            };
        }
        catch (Exception ex)
        {
            return new ExecutionResult
            {
                Success = false,
                Output = outputWriter.ToString(),
                ErrorMessage = $"Runtime error: {ex.Message}\n{ex.StackTrace}",
            };
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