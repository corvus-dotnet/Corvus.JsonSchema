// <copyright file="StepBodyEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the body of a single operation step of a generated workflow executor (plan §3.1): resolve
/// the arguments (<see cref="RequestBindingEmitter"/>), invoke the operation through the
/// <em>generated client method</em> — which builds and validates the request, sends it, and validates
/// the response, so the OpenAPI protocol is managed for us — record the step metric, feed the response
/// body into the workflow context, gate on the step's success criteria, and dispose the response.
/// </summary>
/// <remarks>
/// <para>
/// The response is an <see cref="System.IAsyncDisposable"/> whose JSON values are backed by its own
/// transport buffer, independent of the request. Each declared JSON body is therefore <em>cloned into
/// the run's <c>JsonWorkspace</c></em> (<c>CloneAsBuilder</c>) before being fed to the context — so the
/// step's outputs, which reference (not copy) those values, remain valid for the whole run — and the
/// response is then disposed in a <c>finally</c>, leaking nothing.
/// </para>
/// <para>
/// Success criteria are compiled once into <c>static readonly</c> <c>CompiledCriterion</c> fields; a
/// step with no <c>successCriteria</c> defaults to requiring an HTTP success status.
/// </para>
/// </remarks>
public static class StepBodyEmitter
{
    /// <summary>
    /// Emits the field declarations and in-method statements for an operation step.
    /// </summary>
    /// <param name="stepId">The step id (used for variable/field names, telemetry, and diagnostics).</param>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="arguments">The step's arguments (parameter name → runtime-expression value).</param>
    /// <param name="successCriteria">The step's success criteria, in document order.</param>
    /// <param name="transportVariable">The in-scope <c>IApiTransport</c> variable name.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name (owns the cloned response bodies).</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <param name="cancellationTokenVariable">The in-scope <c>CancellationToken</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="requestBodyExpression">The runtime expression that produces the request body, or <see langword="null"/> when the step declares no (supported) request body.</param>
    /// <returns>The emitted static field declarations and the in-method statements.</returns>
    public static StepBodyCode Emit(
        string stepId,
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        IReadOnlyList<StepCriterion> successCriteria,
        string transportVariable,
        string workspaceVariable,
        string contextVariable,
        string cancellationTokenVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string? requestBodyExpression = null)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentNullException.ThrowIfNull(successCriteria);
        ArgumentNullException.ThrowIfNull(stepOutputLocals);
        ArgumentException.ThrowIfNullOrEmpty(workspaceVariable);

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string prefix = $"{identifier}_";
        string camel = EmitText.ToCamelCase(identifier);
        string clientVar = $"{camel}Client";
        string responseVar = $"{camel}Response";

        RequestBindingCode request = RequestBindingEmitter.Emit(operation, arguments, contextVariable, prefix, stepOutputLocals, requestBodyExpression);

        var fields = new StringBuilder(request.Fields);
        var body = new StringBuilder(request.Statements);

        // Invoke through the generated client: it constructs and validates the request, sends it via
        // the transport, and validates the response — we never touch the request type or the raw
        // transport, so the protocol stays in the generated code.
        var callArguments = new List<string>(request.NamedArguments)
        {
            $"cancellationToken: {cancellationTokenVariable}",
        };

        body.Append("var ").Append(clientVar).Append(" = new ").Append(operation.Operation.ClientTypeName)
            .Append('(').Append(transportVariable).AppendLine(");");
        body.Append("var ").Append(responseVar).Append(" = await ").Append(clientVar).Append('.')
            .Append(operation.Operation.ClientMethodName).Append('(')
            .Append(string.Join(", ", callArguments)).AppendLine(").ConfigureAwait(false);");

        // The response owns a transport buffer; clone any extracted body into the workspace, then
        // always dispose it.
        body.AppendLine("try");
        body.AppendLine("{");

        var inner = new StringBuilder();
        inner.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        inner.Append(contextVariable).Append(".SetResponseStatusCode(").Append(responseVar).AppendLine(".StatusCode);");

        // Feed each declared JSON body into the context, guarded by the matching status, so
        // $response.body resolves for criteria and outputs. The body is cloned into the workspace so it
        // outlives the response (disposed below) and can be safely referenced by the step's outputs.
        foreach (ResponseDescriptor response in operation.Operation.Responses)
        {
            if (response.BodyPropertyName is { } bodyProperty
                && int.TryParse(response.StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
            {
                inner.Append("if (").Append(responseVar).Append(".StatusCode == ")
                    .Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(") { ")
                    .Append(contextVariable).Append(".SetResponseBody(((JsonElement)").Append(responseVar).Append('.').Append(bodyProperty)
                    .Append(").CloneAsBuilder(").Append(workspaceVariable).AppendLine(").RootElement); }");
            }
        }

        EmitSuccessGate(fields, inner, successCriteria, prefix, contextVariable, responseVar, stepId);

        AppendIndented(body, inner.ToString(), 4);

        body.AppendLine("}");
        body.AppendLine("finally");
        body.AppendLine("{");
        body.Append("    await ").Append(responseVar).AppendLine(".DisposeAsync().ConfigureAwait(false);");
        body.AppendLine("}");

        return new StepBodyCode(fields.ToString(), body.ToString());
    }

    private static void AppendIndented(StringBuilder target, string text, int indent)
    {
        if (text.Length == 0)
        {
            return;
        }

        string pad = new(' ', indent);
        foreach (string line in text.Split('\n'))
        {
            string trimmed = line.TrimEnd('\r');
            if (trimmed.Length == 0)
            {
                target.AppendLine();
            }
            else
            {
                target.Append(pad).AppendLine(trimmed);
            }
        }
    }

    private static void EmitSuccessGate(
        StringBuilder fields,
        StringBuilder body,
        IReadOnlyList<StepCriterion> successCriteria,
        string prefix,
        string contextVariable,
        string responseVar,
        string stepId)
    {
        if (successCriteria.Count == 0)
        {
            // No criteria: per the engine's default, the step succeeds on an HTTP success status.
            body.Append("if (!").Append(responseVar).AppendLine(".IsSuccess)");
            body.AppendLine("{");
            body.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
                .Append(EmitText.Quote($"Step '{stepId}' returned an unsuccessful status."))
                .AppendLine(");");
            body.AppendLine("}");
            return;
        }

        var checks = new List<string>(successCriteria.Count);
        for (int i = 0; i < successCriteria.Count; i++)
        {
            StepCriterion criterion = successCriteria[i];
            string field = $"{prefix}SuccessCriterion{i.ToString(CultureInfo.InvariantCulture)}";
            string contextArgument = criterion.Context is null ? string.Empty : $", {EmitText.Quote(criterion.Context)}";

            fields.Append("private static readonly CompiledCriterion ").Append(field)
                .Append(" = CompiledCriterion.Compile(CriterionType.").Append(MapCriterionType(criterion.Type))
                .Append(", ").Append(EmitText.Quote(criterion.Condition)).Append(contextArgument).AppendLine(");");

            checks.Add($"{field}.Evaluate({contextVariable})");
        }

        body.Append("if (!(").Append(string.Join(" && ", checks)).AppendLine("))");
        body.AppendLine("{");
        body.Append("    throw new WorkflowStepFailedException(").Append(EmitText.Quote(stepId)).Append(", ")
            .Append(EmitText.Quote($"Step '{stepId}' did not satisfy its success criteria."))
            .AppendLine(");");
        body.AppendLine("}");
    }

    private static string MapCriterionType(string type)
        => type.ToLowerInvariant() switch
        {
            "regex" => "Regex",
            "jsonpath" => "JsonPath",
            _ => "Simple",
        };
}

/// <summary>
/// A success/failure criterion declared on a step (plan §3.1).
/// </summary>
/// <param name="Type">The criterion type (<c>simple</c>, <c>regex</c>, or <c>jsonpath</c>).</param>
/// <param name="Condition">The condition expression.</param>
/// <param name="Context">The runtime-expression context, or <see langword="null"/> for a <c>simple</c> criterion.</param>
public readonly record struct StepCriterion(string Type, string Condition, string? Context);

/// <summary>
/// The code emitted for a step body (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements implementing the step.</param>
public readonly record struct StepBodyCode(string Fields, string Statements);