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
/// workspace, so the executor passes the cheapest source: a runtime-expression result (a
/// <see cref="JsonElement"/>) and a parse-once object/array constant are re-wrapped to the parameter type
/// via <c>{Type}.From(...)</c> — a free re-interpretation of the same backing JSON, not a copy — which then
/// converts to <c>{Type}.Source</c>; a literal scalar is the C# literal itself (<c>true</c>, <c>10</c>,
/// <c>"x"u8</c>) and an interpolation is built into a pooled UTF-8 builder, each binding directly through a
/// <c>Source</c> implicit conversion. (A raw <see cref="JsonElement"/> cannot bind to <c>{Type}.Source</c>
/// directly: C# will not chain the two user-defined conversions <c>JsonElement → {Type} → Source</c>.)
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
        var parameterBindings = new List<RequestParameterBinding>();

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
            parameterBindings.Add(new RequestParameterBinding(parameter.Location, parameter.PropertyName, parameter.TypeName, source));
        }

        if (requestBody is { } body && operation.Operation.RequestBodyTypeName is { } bodyType)
        {
            string source = body.Replacements is { Count: > 0 } replacements
                ? EmitBodyWithReplacements(
                    fields, statements, cleanup, body, replacements, contextVariable, stepOutputLocals, inputsVariable, inputAccessors, $"{fieldPrefix}Body")
                : EmitValue(
                    fields, statements, cleanup, body.Kind, body.Value, contextVariable, stepOutputLocals, inputsVariable, inputAccessors,
                    $"{fieldPrefix}Body", "Body", bodyType);
            namedArguments.Add($"body: {source}");
        }

        return new RequestBindingCode(fields.ToString(), statements.ToString(), namedArguments, cleanup.ToString(), parameterBindings);
    }

    /// <summary>
    /// Converts a <see cref="JsonElement"/> expression to a generated model type with the model's
    /// <c>From</c> factory (a free re-wrap of the same backing JSON — see Corvus.Text.Json conversion
    /// semantics) so the model's single implicit conversion to <c>{Type}.Source</c> applies at the client
    /// call site (C# does not chain the two user-defined conversions <c>JsonElement → model → Source</c>).
    /// When the target is itself <see cref="JsonElement"/> (e.g. a request-body payload replacement that
    /// feeds <c>TryAdd</c> rather than a <c>Source</c>) the value is already correct, so it is passed through.
    /// </summary>
    internal static string ConvertToSourceType(string expression, string typeName)
        => typeName == "Corvus.Text.Json.JsonElement" ? expression : $"{typeName}.From({expression})";

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
        // Derive the value local from the step-unique field name (which embeds the step id), not the bare
        // property name — otherwise two straight-line steps that bind the same request property (e.g. both
        // call an operation with a 'petId' path parameter) would declare colliding locals in one method body.
        string local = $"{EmitText.ToCamelCase(fieldName)}Value";

        switch (kind)
        {
            case ArgumentValueKind.Expression:
                // Runtime expression → resolve to a JsonElement (a reference into an existing document).
                // Re-wrap to the parameter's model type with From so the single model → {Type}.Source implicit
                // conversion applies at the call site (C# will not chain JsonElement → model → Source itself).
                ValueResolution.Emit(fields, statements, value, local, contextVariable, stepOutputLocals, fieldName, inputsVariable, inputAccessors);
                return ConvertToSourceType(local, typeName);

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
                return ConvertToSourceType(
                    JsonTemplateEmitter.EmitComposite(
                        propertyName, value, "workspace", default, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{fieldName}Tmpl"),
                    typeName);

            default:
                // Object/array constant: parsed once into a standalone document, passed as a Source.
                fields.Append("private static readonly ParsedJsonDocument<JsonElement> ").Append(fieldName)
                    .Append(" = ParsedJsonDocument<JsonElement>.Parse(System.Text.Encoding.UTF8.GetBytes(")
                    .Append(EmitText.Quote(value)).AppendLine("));");
                return ConvertToSourceType($"{fieldName}.RootElement", typeName);
        }
    }

    /// <summary>
    /// Emits a request body built by overlaying payload replacements onto a base payload: resolves the base
    /// to a <see cref="JsonElement"/>, builds a mutable copy in the run workspace, sets each replacement's
    /// value at its JSON Pointer target (JSON Patch <c>add</c> semantics — create-or-replace), and returns
    /// the patched element (which converts to the client body's <c>Source</c>).
    /// </summary>
    internal static string EmitBodyWithReplacements(
        StringBuilder fields,
        StringBuilder statements,
        StringBuilder cleanup,
        in StepBody body,
        IReadOnlyList<PayloadReplacement> replacements,
        string contextVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string fieldName)
    {
        string prefix = EmitText.ToCamelCase(fieldName);
        string baseLocal = $"{prefix}Base";
        EmitBaseElement(fields, statements, cleanup, body, contextVariable, stepOutputLocals, inputsVariable, inputAccessors, fieldName, baseLocal);

        string builderLocal = $"{prefix}Builder";
        string mutableLocal = $"{prefix}Mutable";
        statements.Append("var ").Append(builderLocal).Append(" = ").Append(baseLocal).AppendLine(".CreateBuilder(workspace);");
        statements.Append("JsonElement.Mutable ").Append(mutableLocal).Append(" = ").Append(builderLocal).AppendLine(".RootElement;");

        for (int i = 0; i < replacements.Count; i++)
        {
            PayloadReplacement replacement = replacements[i];
            string valueSource = EmitValue(
                fields, statements, cleanup, replacement.Kind, replacement.Value, contextVariable, stepOutputLocals, inputsVariable, inputAccessors,
                $"{fieldName}Repl{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}", $"Repl{i.ToString(System.Globalization.CultureInfo.InvariantCulture)}", "Corvus.Text.Json.JsonElement");
            statements.Append(mutableLocal).Append(".TryAdd(").Append(EmitText.Quote(replacement.Target)).Append("u8, ").Append(valueSource).AppendLine(");");
        }

        string resultLocal = $"{prefix}Patched";
        statements.Append("JsonElement ").Append(resultLocal).Append(" = ").Append(mutableLocal).AppendLine(";");
        return resultLocal;
    }

    /// <summary>Resolves a request-body base payload (of any kind) to a <see cref="JsonElement"/> local.</summary>
    private static void EmitBaseElement(
        StringBuilder fields,
        StringBuilder statements,
        StringBuilder cleanup,
        in StepBody body,
        string contextVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        string fieldName,
        string baseLocal)
    {
        switch (body.Kind)
        {
            case ArgumentValueKind.Expression:
                // A runtime expression resolves to a reference; CreateBuilder copies it before mutation.
                ValueResolution.Emit(fields, statements, body.Value, baseLocal, contextVariable, stepOutputLocals, $"{fieldName}BaseExpr", inputsVariable, inputAccessors);
                break;

            case ArgumentValueKind.CompositeTemplate:
            {
                // EmitComposite appends its build statements as a side effect, so capture the result first.
                string built = JsonTemplateEmitter.EmitComposite(
                    fieldName, body.Value, "workspace", default, inputsVariable, stepOutputLocals, inputAccessors, fields, statements, $"{fieldName}BaseTmpl");
                statements.Append("JsonElement ").Append(baseLocal).Append(" = ").Append(built).AppendLine(";");
                break;
            }

            case ArgumentValueKind.Interpolation:
            {
                string built = JsonTemplateEmitter.EmitInterpolation(
                    fieldName, body.Value, "workspace", default, inputsVariable, stepOutputLocals, inputAccessors, statements, $"{fieldName}BaseInterp");
                statements.Append("JsonElement ").Append(baseLocal).Append(" = ").Append(built).AppendLine(";");
                break;
            }

            default:
            {
                string json = body.Kind switch
                {
                    ArgumentValueKind.LiteralComposite or ArgumentValueKind.LiteralNumber or ArgumentValueKind.LiteralBoolean => body.Value,
                    ArgumentValueKind.LiteralNull => "null",
                    ArgumentValueKind.LiteralString => System.Text.Json.JsonSerializer.Serialize(body.Value),
                    _ => throw new NotSupportedException($"Request body kind '{body.Kind}' is not supported as a base for payload replacements."),
                };
                string constant = JsonTemplateEmitter.EmitConstant(json, fields, $"{fieldName}BaseConst");
                statements.Append("JsonElement ").Append(baseLocal).Append(" = ").Append(constant).AppendLine(";");
                break;
            }
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
/// <param name="Replacements">Payload replacements to overlay onto the base payload at JSON Pointer targets, or <see langword="null"/> when there are none.</param>
public readonly record struct StepBody(string Value, ArgumentValueKind Kind, IReadOnlyList<PayloadReplacement>? Replacements = null);

/// <summary>
/// A single payload replacement (Arazzo <c>requestBody.replacements[]</c>): a value to set within the
/// payload at a JSON Pointer location.
/// </summary>
/// <param name="Target">The JSON Pointer (e.g. <c>/status</c>) at which to set the value.</param>
/// <param name="Value">The replacement value's expression/interpolation/literal text (see <paramref name="Kind"/>).</param>
/// <param name="Kind">How the replacement value is produced.</param>
public readonly record struct PayloadReplacement(string Target, string Value, ArgumentValueKind Kind);

/// <summary>
/// The request-side values of a step, used to resolve <c>$method</c> and <c>$request.*</c> criterion
/// operands — <c>$method</c> is the operation's HTTP method (a compile-time constant), and
/// <c>$request.&lt;location&gt;.&lt;name&gt;</c>/<c>$request.body</c> resolve to the value the step binds
/// to that request parameter/body.
/// </summary>
/// <param name="Method">The operation's HTTP method, upper-cased (e.g. <c>GET</c>).</param>
/// <param name="Arguments">The step's parameter arguments, keyed by name.</param>
/// <param name="Body">The step's request body, or <see langword="null"/>.</param>
/// <param name="UrlLocal">The executor-owned <c>byte[]</c> local holding the resolved relative request URL (for an inlined <c>$url</c> operand), or <see langword="null"/> when no step criterion references <c>$url</c>.</param>
public readonly record struct StepRequestContext(
    string Method,
    IReadOnlyList<StepArgument> Arguments,
    StepBody? Body,
    string? UrlLocal = null);

/// <summary>
/// The code emitted for a step's request binding (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that resolve each argument, emitted before the client call.</param>
/// <param name="NamedArguments">The <c>paramName: source</c> fragments to pass to the generated client method.</param>
/// <param name="Cleanup">Statements to emit immediately after the client call — e.g. returning a pooled interpolation buffer whose span was passed as a <c>Source</c> (the client consumes it synchronously, so it is safe to return once the call has been made).</param>
/// <param name="ParameterBindings">The per-parameter bindings (location, generated property/type, and the resolved <c>Source</c> expression) — used to reconstruct the request struct for a <c>$url</c> criterion without re-resolving the arguments.</param>
public readonly record struct RequestBindingCode(string Fields, string Statements, IReadOnlyList<string> NamedArguments, string Cleanup = "", IReadOnlyList<RequestParameterBinding>? ParameterBindings = null);

/// <summary>
/// One resolved request-parameter binding: where the parameter sits, the generated request property and
/// its model type, and the C# expression (a local, field, or literal already emitted by the request
/// binding) that yields the parameter's <c>Source</c>. The <c>$url</c> emitter reuses these to rebuild
/// the request struct and write its resolved path/query without resolving the arguments a second time.
/// </summary>
/// <param name="Location">The parameter location (path, query, header, or cookie).</param>
/// <param name="PropertyName">The generated request property name (e.g. <c>RunId</c>).</param>
/// <param name="TypeName">The fully-qualified type of that property (e.g. <c>…Models.JsonString</c>).</param>
/// <param name="Source">The C# expression that produces the parameter's <c>Source</c> value.</param>
public readonly record struct RequestParameterBinding(ParameterLocation Location, string PropertyName, string TypeName, string Source);