// <copyright file="RequestBindingEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that resolves a step's arguments and binds each to the matching generated
/// client-method parameter via <c>TTarget.From(source)</c>, ready to pass as named arguments to the
/// generated client method (plan §3.1).
/// </summary>
/// <remarks>
/// Each argument's runtime expression is parsed once into a <c>static readonly</c>
/// <c>ArazzoExpression</c> field (no per-execution parse/allocation); at run time the value is
/// resolved to a <see cref="JsonElement"/> via <c>WorkflowExecutionContext.TryResolveValue</c> and
/// bound to the parameter's generated type with <c>From</c>. The binding is surfaced as a list of
/// <c>paramName: Type.From(local)</c> named-argument fragments — the generated client method (not this
/// generator) owns request construction, validation, and the protocol, so we never build the request
/// type directly.
/// </remarks>
public static class RequestBindingEmitter
{
    /// <summary>
    /// Emits the argument-resolution code and the named-argument fragments for a client-method call.
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="arguments">The step's arguments (parameter name → runtime-expression value).</param>
    /// <param name="contextVariable">The name of the in-scope <c>WorkflowExecutionContext</c> variable.</param>
    /// <param name="fieldPrefix">A unique prefix (e.g. the step id) for the emitted static expression fields.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="requestBodyExpression">
    /// The runtime expression that produces the request body (e.g. <c>$inputs.pet</c>), or
    /// <see langword="null"/> when the step declares no request body (or a body the current generator
    /// does not yet support — a literal, interpolated, or replacement payload, which is a later phase).
    /// </param>
    /// <returns>The emitted static field declarations, the in-method resolution statements, and the named-argument fragments.</returns>
    /// <exception cref="InvalidOperationException">A required parameter has no argument.</exception>
    public static RequestBindingCode Emit(
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        string contextVariable,
        string fieldPrefix,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string? requestBodyExpression = null)
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

            string field = $"{fieldPrefix}{parameter.PropertyName}";
            string local = $"{EmitText.ToCamelCase(parameter.PropertyName)}Value";

            if (argument.IsLiteral)
            {
                LiteralValueEmitter.Emit(fields, statements, argument.Expression, local, $"{field}Literal");
            }
            else
            {
                ValueResolution.Emit(fields, statements, argument.Expression, local, contextVariable, stepOutputLocals, field);
            }

            namedArguments.Add($"{parameter.ParameterName}: {parameter.TypeName}.From({local})");
        }

        // Bind the request body, resolving its runtime expression and From()-binding it to the client
        // method's `body` parameter. The generated client owns serialization; we only supply the value.
        if (requestBodyExpression is { } bodyExpression && operation.Operation.RequestBodyTypeName is { } bodyType)
        {
            const string bodyLocal = "bodyValue";
            ValueResolution.Emit(fields, statements, bodyExpression, bodyLocal, contextVariable, stepOutputLocals, $"{fieldPrefix}Body");
            namedArguments.Add($"body: {bodyType}.From({bodyLocal})");
        }

        return new RequestBindingCode(fields.ToString(), statements.ToString(), namedArguments);
    }
}

/// <summary>
/// An argument a step passes to an operation parameter (plan §3.1).
/// </summary>
/// <param name="Name">The operation parameter name to bind.</param>
/// <param name="Expression">
/// When <paramref name="IsLiteral"/> is <see langword="false"/>, the runtime expression that produces
/// the value (e.g. <c>$inputs.petId</c>); when <see langword="true"/>, the literal value's JSON text
/// (e.g. <c>"electronic"</c> or <c>42</c>).
/// </param>
/// <param name="IsLiteral">Whether the argument is a constant JSON value rather than a runtime expression.</param>
public readonly record struct StepArgument(string Name, string Expression, bool IsLiteral = false);

/// <summary>
/// The code emitted for a step's request binding (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> compiled-expression field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that resolve each argument into a local <see cref="JsonElement"/>.</param>
/// <param name="NamedArguments">The <c>paramName: Type.From(local)</c> fragments to pass to the generated client method.</param>
public readonly record struct RequestBindingCode(string Fields, string Statements, IReadOnlyList<string> NamedArguments);