// <copyright file="RequestBindingEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that binds a step's argument values to a generated client method's
/// <c>{Type}.Source</c> parameters (plan §3.1).
/// </summary>
/// <remarks>
/// The generated client method takes each value as a <c>{Type}.Source</c> and builds it into its own
/// workspace, so the executor passes the cheapest source and never reifies a throwaway document:
/// a runtime-expression result flows in as the resolved <see cref="JsonElement"/> (an
/// implicit <c>Source</c>); a literal scalar is the C# literal itself (<c>true</c>, <c>10</c>,
/// <c>"x"u8</c>); a literal object/array is a parse-once constant; an interpolation is built into a
/// pooled UTF-8 builder. There is no <c>{Type}.From(...)</c> — the implicit <c>Source</c> conversions
/// do the binding.
/// </remarks>
public static class RequestBindingEmitter
{
    /// <summary>
    /// Emits the argument-resolution code and the named-argument fragments for a client-method call.
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="arguments">The step's arguments.</param>
    /// <param name="contextVariable">The name of the in-scope <c>WorkflowExecutionContext</c> variable.</param>
    /// <param name="fieldPrefix">A unique prefix (e.g. the step id) for the emitted static fields.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable name (for static <c>$inputs</c> navigation).</param>
    /// <param name="requestBody">The step's request body, or <see langword="null"/> when there is none (or one not yet supported).</param>
    /// <returns>The emitted static field declarations, the in-method resolution statements, and the named-argument fragments.</returns>
    /// <exception cref="InvalidOperationException">A required parameter has no argument.</exception>
    public static RequestBindingCode Emit(
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        string contextVariable,
        string fieldPrefix,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        StepBody? requestBody = null)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(stepOutputLocals);
        ArgumentException.ThrowIfNullOrEmpty(contextVariable);
        ArgumentException.ThrowIfNullOrEmpty(fieldPrefix);

        var argumentsByName = new Dictionary<string, StepArgument>(StringComparer.Ordinal);
        foreach (StepArgument argument in arguments)
        {
            argumentsByName[argument.Name] = argument;
        }

        var fields = new StringBuilder();
        var statements = new StringBuilder();
        var cleanup = new StringBuilder();
        var namedArguments = new List<string>();

        foreach (RequestParameterInfo parameter in operation.Operation.RequestParameters)
        {
            if (!argumentsByName.TryGetValue(parameter.Name, out StepArgument argument))
            {
                if (parameter.IsRequired)
                {
                    throw new InvalidOperationException(
                        $"Step provides no value for required parameter '{parameter.Name}' of operation '{operation.Operation.OperationId ?? operation.Operation.MethodName}'.");
                }

                continue;
            }

            string source = EmitValue(
                fields, statements, cleanup, argument.Kind, argument.Value, contextVariable, stepOutputLocals, inputsVariable, inputAccessors,
                $"{fieldPrefix}{parameter.PropertyName}", parameter.PropertyName, parameter.TypeName);
            namedArguments.Add($"{parameter.ParameterName}: {source}");
        }

        if (requestBody is { } body && operation.Operation.RequestBodyTypeName is { } bodyType)
        {
            string source = EmitValue(
                fields, statements, cleanup, body.Kind, body.Value, contextVariable, stepOutputLocals, inputsVariable, inputAccessors,
                $"{fieldPrefix}Body", "Body", bodyType);
            namedArguments.Add($"body: {source}");
        }

        return new RequestBindingCode(fields.ToString(), statements.ToString(), namedArguments, cleanup.ToString());
    }

    /// <summary>
    /// Emits any fields/statements needed to produce a value and returns the C# expression to pass as
    /// the client parameter's <c>{Type}.Source</c> (relying on its implicit conversions).
    /// </summary>
    private static string EmitValue(
        StringBuilder fields,
        StringBuilder statements,
        StringBuilder cleanup,
        ArgumentValueKind kind,
        string value,
        string contextVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string fieldName,
        string propertyName,
        string typeName)
    {
        string local = $"{EmitText.ToCamelCase(propertyName)}Value";

        switch (kind)
        {
            case ArgumentValueKind.Expression:
                // Runtime expression → resolve to a JsonElement (a reference into an existing document),
                // passed straight in as a Source.
                ValueResolution.Emit(fields, statements, value, local, contextVariable, stepOutputLocals, fieldName, inputsVariable, inputAccessors);
                return local;

            case ArgumentValueKind.Interpolation:
                // Inline the template (literal segments + statically-navigated $inputs/$steps fragments
                // into a pooled buffer, no context) when possible: the buffer's span is passed straight
                // to the client as its Source and returned after the call. Otherwise fall back to the
                // runtime interpreter. Request-side interpolation binds no response body, so pass none.
                if (InterpolationInliner.TryEmit(
                    value, responseBodyLocal: null, inputsVariable, stepOutputLocals, inputAccessors, $"{fieldName}Interp", out InterpolationInlineCode inlined))
                {
                    statements.Append(inlined.Statements);
                    cleanup.Append(inlined.Cleanup);
                    return inlined.SourceExpression;
                }

                InterpolationEmitter.Emit(fields, statements, value, local, contextVariable, $"{fieldName}Template");
                return local;

            case ArgumentValueKind.LiteralString:
                // The unescaped content as a UTF-8 span → Source string. No document.
                return $"{EmitText.Quote(value)}u8";

            case ArgumentValueKind.LiteralNumber:
                // The raw JSON number text is a valid C# numeric literal → Source number. No document.
                return value;

            case ArgumentValueKind.LiteralBoolean:
                return value;

            case ArgumentValueKind.LiteralNull:
                return $"{typeName}.Source.Null()";

            case ArgumentValueKind.CompositeTemplate:
                // A request body that embeds runtime expressions in an object/array template: build it into
                // the run workspace (resolving $inputs/$steps; there is no message/response source for a
                // request) and pass the resulting JsonElement as the client method's Source. The generated
                // client materialises the Source synchronously, so the workspace-built value is safe.
                return JsonTemplateEmitter.EmitComposite(
                    propertyName, value, "workspace", default, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{fieldName}Tmpl");

            default:
                // Object/array constant: parsed once into a standalone document, passed as a Source.
                fields.Append("private static readonly ParsedJsonDocument<JsonElement> ").Append(fieldName)
                    .Append(" = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(")
                    .Append(EmitText.Quote(value)).AppendLine("));");
                return $"{fieldName}.RootElement";
        }
    }
}

/// <summary>
/// How a step argument or request body produces its value.
/// </summary>
public enum ArgumentValueKind
{
    /// <summary>A runtime expression (e.g. <c>$inputs.petId</c>), resolved via the context.</summary>
    Expression,

    /// <summary>An interpolation template containing embedded <c>{$…}</c> expressions.</summary>
    Interpolation,

    /// <summary>A constant JSON string; the value is its unescaped content.</summary>
    LiteralString,

    /// <summary>A constant JSON number; the value is its raw number text.</summary>
    LiteralNumber,

    /// <summary>A constant JSON boolean; the value is <c>true</c> or <c>false</c>.</summary>
    LiteralBoolean,

    /// <summary>A constant JSON null.</summary>
    LiteralNull,

    /// <summary>A constant JSON object or array; the value is its raw JSON text.</summary>
    LiteralComposite,

    /// <summary>A JSON object or array that embeds runtime expressions (a template to substitute); the value is its raw JSON text.</summary>
    CompositeTemplate,
}

/// <summary>
/// An argument a step passes to an operation parameter (plan §3.1).
/// </summary>
/// <param name="Name">The operation parameter name to bind.</param>
/// <param name="Value">The expression, interpolation template, or literal value text (see <paramref name="Kind"/>).</param>
/// <param name="Kind">How the value is produced.</param>
public readonly record struct StepArgument(string Name, string Value, ArgumentValueKind Kind = ArgumentValueKind.Expression);

/// <summary>
/// A step's request body (plan §3.1).
/// </summary>
/// <param name="Value">The expression, interpolation template, or literal value text (see <paramref name="Kind"/>).</param>
/// <param name="Kind">How the value is produced.</param>
public readonly record struct StepBody(string Value, ArgumentValueKind Kind);

/// <summary>
/// The request-side values of a step, used to resolve <c>$method</c> and <c>$request.*</c> criterion
/// operands — <c>$method</c> is the operation's HTTP method (a compile-time constant), and
/// <c>$request.&lt;location&gt;.&lt;name&gt;</c>/<c>$request.body</c> resolve to the value the step binds
/// to that request parameter/body.
/// </summary>
/// <param name="Method">The operation's HTTP method, upper-cased (e.g. <c>GET</c>).</param>
/// <param name="Arguments">The step's parameter arguments, keyed by name.</param>
/// <param name="Body">The step's request body, or <see langword="null"/>.</param>
public readonly record struct StepRequestContext(
    string Method,
    IReadOnlyList<StepArgument> Arguments,
    StepBody? Body);

/// <summary>
/// The code emitted for a step's request binding (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that resolve each argument, emitted before the client call.</param>
/// <param name="NamedArguments">The <c>paramName: source</c> fragments to pass to the generated client method.</param>
/// <param name="Cleanup">Statements to emit immediately after the client call — e.g. returning a pooled interpolation buffer whose span was passed as a <c>Source</c> (the client consumes it synchronously, so it is safe to return once the call has been made).</param>
public readonly record struct RequestBindingCode(string Fields, string Statements, IReadOnlyList<string> NamedArguments, string Cleanup = "");