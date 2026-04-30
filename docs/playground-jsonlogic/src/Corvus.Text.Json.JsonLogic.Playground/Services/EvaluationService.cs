using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Text.Json;
using Corvus.Text.Json.JsonLogic;

namespace Corvus.Text.Json.JsonLogic.Playground.Services;

/// <summary>
/// Identifies which editor an evaluation error originated from.
/// </summary>
public enum ErrorSource
{
    None,
    Rule,
    Data,
}

/// <summary>
/// Result of a JsonLogic evaluation.
/// </summary>
public sealed class EvaluationResult
{
    public bool Success { get; init; }

    public string? ResultJson { get; init; }

    public string? ErrorMessage { get; init; }

    public double ElapsedMs { get; init; }

    public ErrorSource ErrorSource { get; init; }

    /// <summary>
    /// When an error is caused by a custom operator, the operator name (e.g. "clamp").
    /// </summary>
    public string? ErrorOperatorName { get; init; }

    /// <summary>
    /// JSON Pointer paths to the error-causing operator in the rule tree (e.g. ["/clamp", "/if/1/clamp"]).
    /// </summary>
    public List<string>? ErrorPaths { get; init; }
}

/// <summary>
/// Wraps <see cref="JsonLogicEvaluator"/> for use in the Blazor playground.
/// </summary>
public sealed partial class EvaluationService
{
    // Fresh evaluator per call — avoids cache holding references to disposed documents
    private readonly SemaphoreSlim evalLock = new(1, 1);

    private IReadOnlyDictionary<string, IOperatorCompiler> customOperators =
        new Dictionary<string, IOperatorCompiler>();

    /// <summary>
    /// Updates the custom operators used during evaluation.
    /// Each operator is wrapped to attribute errors to the operator name.
    /// </summary>
    public void SetCustomOperators(IReadOnlyDictionary<string, IOperatorCompiler> operators)
    {
        this.customOperators = operators.ToDictionary(
            kv => kv.Key,
            kv => (IOperatorCompiler)new ErrorAttributingCompiler(kv.Key, kv.Value));
    }

    public async Task<EvaluationResult> EvaluateAsync(string ruleJson, string dataJson)
    {
        if (string.IsNullOrWhiteSpace(ruleJson))
        {
            return new EvaluationResult
            {
                Success = true,
                ResultJson = string.Empty,
                ElapsedMs = 0,
            };
        }

        await evalLock.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                var sw = Stopwatch.StartNew();

                // Parse rule
                ParsedJsonDocument<JsonElement> ruleDoc;
                try
                {
                    ruleDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes(ruleJson));
                }
                catch (JsonException ex)
                {
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Rule JSON: " + FixBrokenSRFormat(ex.Message),
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }

                // Parse data
                ParsedJsonDocument<JsonElement> dataDoc;
                try
                {
                    string data = string.IsNullOrWhiteSpace(dataJson) ? "{}" : dataJson;
                    dataDoc = ParsedJsonDocument<JsonElement>.Parse(
                        Encoding.UTF8.GetBytes(data));
                }
                catch (JsonException ex)
                {
                    ruleDoc.Dispose();
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Data JSON: " + FixBrokenSRFormat(ex.Message),
                        ErrorSource = ErrorSource.Data,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }

                try
                {
                    // Reset instance counters so depth-first indices align with the rule tree
                    foreach (IOperatorCompiler compiler in this.customOperators.Values)
                    {
                        if (compiler is ErrorAttributingCompiler eac)
                        {
                            eac.ResetInstanceCounter();
                        }
                    }

                    // Use a fresh evaluator each time so no cached references
                    // to previously-disposed documents survive across calls.
                    var eval = new JsonLogicEvaluator(
                        new Dictionary<string, IOperatorCompiler>(this.customOperators));

                    var rule = new JsonLogicRule(ruleDoc.RootElement);
                    JsonElement result = eval.Evaluate(in rule, dataDoc.RootElement);

                    // Format result BEFORE disposing documents
                    string resultText;
                    try
                    {
                        resultText = result.ValueKind == JsonValueKind.Undefined
                            ? "/* no result */"
                            : FormatJson(result);
                    }
                    catch (ObjectDisposedException)
                    {
                        resultText = "/* result unavailable — backing document was disposed */";
                    }

                    sw.Stop();

                    return new EvaluationResult
                    {
                        Success = true,
                        ResultJson = resultText,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                catch (Exception ex) when (ex is ObjectDisposedException)
                {
                    sw.Stop();
                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = "Cannot access a disposed object (JsonDocument). Try again.",
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                    };
                }
                catch (Exception ex)
                {
                    sw.Stop();

                    // Extract operator name and find its path(s) in the rule tree
                    string? opName = null;
                    List<string>? errorPaths = null;
                    Exception inner = ex;
                    while (inner is System.Reflection.TargetInvocationException or AggregateException
                           && inner.InnerException is not null)
                    {
                        inner = inner.InnerException;
                    }

                    if (inner is CustomOperatorException coe)
                    {
                        opName = coe.OperatorName;
                        var allPaths = FindOperatorPaths(ruleDoc.RootElement, coe.OperatorName);
                        if (coe.InstanceIndex < allPaths.Count)
                        {
                            // Pick the specific instance that threw
                            errorPaths = [allPaths[coe.InstanceIndex]];
                        }
                        else
                        {
                            // Fallback: highlight all instances
                            errorPaths = allPaths;
                        }
                    }

                    return new EvaluationResult
                    {
                        Success = false,
                        ErrorMessage = FormatRuntimeError(ex),
                        ErrorSource = ErrorSource.Rule,
                        ElapsedMs = sw.Elapsed.TotalMilliseconds,
                        ErrorOperatorName = opName,
                        ErrorPaths = errorPaths,
                    };
                }
                finally
                {
                    ruleDoc.Dispose();
                    dataDoc.Dispose();
                }
            });
        }
        finally
        {
            evalLock.Release();
        }
    }

    private static string FormatRuntimeError(Exception ex)
    {
        // Unwrap TargetInvocationException / AggregateException to get the real error
        Exception inner = ex;
        while (inner is System.Reflection.TargetInvocationException or AggregateException
               && inner.InnerException is not null)
        {
            inner = inner.InnerException;
        }

        // Custom operator errors carry the operator name
        if (inner is CustomOperatorException coe)
        {
            string innerMsg = FormatRuntimeError(coe.InnerException!);
            return $"Operator '{coe.OperatorName}': {innerMsg}";
        }

        // Produce friendly messages for common runtime exceptions
        return inner switch
        {
            ArgumentOutOfRangeException argEx =>
                $"{argEx.ParamName ?? "Argument"} out of range: {FixBrokenSRFormat(argEx.Message)}",
            DivideByZeroException =>
                "Division by zero",
            OverflowException =>
                "Arithmetic overflow",
            InvalidOperationException ioe =>
                FixBrokenSRFormat(ioe.Message),
            _ => FixBrokenSRFormat(inner.Message),
        };
    }

    /// <summary>
    /// Well-known .NET SR resource keys that appear as raw strings in Blazor WASM
    /// because the IL linker trims the resource files. Maps key → format string.
    /// </summary>
    private static readonly Dictionary<string, string> KnownSRKeys = new(StringComparer.Ordinal)
    {
        ["Argument_MinMaxValue"] = "'{0}' must not be greater than '{1}'.",
        ["Arg_ParamName_Name"] = "Parameter name: {0}",
        ["ArgumentOutOfRange_Index"] = "Index was out of range. Must be non-negative and less than the size of the collection.",
        ["ArgumentOutOfRange_NeedNonNegNum"] = "Non-negative number required.",
        ["Arg_ArrayPlusOffTooSmall"] = "Destination array is not long enough.",
        ["InvalidOperation_EnumFailedVersion"] = "Collection was modified; enumeration may not complete.",
        ["NotSupported_ReadOnlyCollection"] = "Collection is read-only.",
        ["Arg_KeyNotFoundWithKey"] = "The given key '{0}' was not present in the dictionary.",
        ["Arg_DuplicateKey"] = "An item with the same key has already been added. Key: {0}",
    };

    private static string FixBrokenSRFormat(string message)
    {
        // Detect unresolved SR key patterns (Blazor WASM trims resource strings)
        if (message.Contains("ObjectDisposed_"))
        {
            return "Cannot access a disposed object.";
        }

        // Pattern 1: Raw SR key with comma-separated args (no placeholders)
        // e.g. "Argument_MinMaxValue, 10, 0"
        if (SrKeyPattern().IsMatch(message))
        {
            return ResolveKnownSRKey(message);
        }

        if (!message.Contains("{0}"))
        {
            return message;
        }

        // Pattern 2: SR format string with {0}, {1} placeholders and trailing args
        string suffix = string.Empty;
        Match posMatch = Regex.Match(message, @"\s*LineNumber: \d+ \| BytePositionInLine: \d+\.\s*$");
        string body = message;
        if (posMatch.Success)
        {
            suffix = posMatch.Value;
            body = message[..posMatch.Index];
        }

        int maxPlaceholder = -1;
        for (int i = 9; i >= 0; i--)
        {
            if (body.Contains($"{{{i}}}"))
            {
                maxPlaceholder = i;
                break;
            }
        }

        if (maxPlaceholder < 0)
        {
            return message;
        }

        int argCount = maxPlaceholder + 1;
        string remaining = body;
        string[] args = new string[argCount];
        for (int i = argCount - 1; i >= 0; i--)
        {
            int lastComma = remaining.LastIndexOf(", ", StringComparison.Ordinal);
            if (lastComma < 0)
            {
                return message;
            }

            args[i] = remaining[(lastComma + 2)..];
            remaining = remaining[..lastComma];
        }

        try
        {
            return string.Format(remaining, args) + suffix;
        }
        catch
        {
            return message;
        }
    }

    /// <summary>
    /// Resolves an unresolved SR key like "Argument_MinMaxValue, 10, 0" using
    /// the known keys table, or falls back to showing the key and arguments.
    /// </summary>
    private static string ResolveKnownSRKey(string message)
    {
        int firstComma = message.IndexOf(", ", StringComparison.Ordinal);
        string key = firstComma >= 0 ? message[..firstComma] : message;
        string[] args = firstComma >= 0
            ? message[(firstComma + 2)..].Split(", ")
            : [];

        if (KnownSRKeys.TryGetValue(key, out string? format))
        {
            try
            {
                return string.Format(format, args.Cast<object>().ToArray());
            }
            catch
            {
                // If format fails, fall through
            }
        }

        // Fallback: show key and args
        return firstComma >= 0 ? $"{key}: {message[(firstComma + 2)..]}" : key;
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"^[A-Z][a-zA-Z]+_[A-Z][a-zA-Z]")]
    private static partial System.Text.RegularExpressions.Regex SrKeyPattern();

    private static string FormatJson(JsonElement element)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = true }))
        {
            element.WriteTo(writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Recursively finds all JSON Pointer paths where the given operator name
    /// appears as an object key in the JSON Logic rule tree.
    /// </summary>
    private static List<string> FindOperatorPaths(JsonElement element, string operatorName)
    {
        var paths = new List<string>();
        FindOperatorPathsRecursive(element, "", operatorName, paths);
        return paths;
    }

    private static void FindOperatorPathsRecursive(
        JsonElement element, string currentPath, string operatorName, List<string> paths)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var prop in element.EnumerateObject())
                {
                    string propPath = currentPath + "/" + prop.Name;
                    if (prop.Name == operatorName)
                    {
                        paths.Add(propPath);
                    }

                    FindOperatorPathsRecursive(prop.Value, propPath, operatorName, paths);
                }

                break;

            case JsonValueKind.Array:
                int index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    FindOperatorPathsRecursive(item, currentPath + "/" + index, operatorName, paths);
                    index++;
                }

                break;
        }
    }

    /// <summary>
    /// Wraps an <see cref="IOperatorCompiler"/> so that any exception thrown during
    /// evaluation is re-thrown as a <see cref="CustomOperatorException"/> tagged with
    /// the operator name and the depth-first instance index (0-based).
    /// </summary>
    private sealed class ErrorAttributingCompiler(string operatorName, IOperatorCompiler inner) : IOperatorCompiler
    {
        private int instanceCounter;

        public RuleEvaluator Compile(RuleEvaluator[] operands)
        {
            int instance = this.instanceCounter++;
            RuleEvaluator compiled = inner.Compile(operands);
            return (in JsonElement data, JsonWorkspace workspace) =>
            {
                try
                {
                    return compiled(in data, workspace);
                }
                catch (CustomOperatorException)
                {
                    throw; // Already attributed
                }
                catch (Exception ex)
                {
                    throw new CustomOperatorException(operatorName, instance, ex);
                }
            };
        }

        /// <summary>
        /// Resets the instance counter. Call before each evaluation so that
        /// instance indices align with the depth-first traversal of the rule tree.
        /// </summary>
        public void ResetInstanceCounter() => this.instanceCounter = 0;
    }
}

/// <summary>
/// Exception thrown by a custom operator, tagged with the operator name and
/// depth-first instance index for error attribution.
/// </summary>
public sealed class CustomOperatorException(string operatorName, int instanceIndex, Exception inner)
    : Exception($"Custom operator '{operatorName}': {inner.Message}", inner)
{
    public string OperatorName => operatorName;

    /// <summary>
    /// The 0-based index of this operator instance in depth-first compilation order.
    /// </summary>
    public int InstanceIndex => instanceIndex;
}
