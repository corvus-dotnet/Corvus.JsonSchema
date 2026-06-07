// <copyright file="StepBodyEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the body of a single operation step of a generated workflow executor (plan §3.1): construct
/// the request (<see cref="RequestBindingEmitter"/>), send it, record the step metric, feed the
/// response into the workflow context, and gate on the step's success criteria.
/// </summary>
/// <remarks>
/// Success criteria are compiled once into <c>static readonly</c> <c>CompiledCriterion</c> fields; a
/// step with no <c>successCriteria</c> defaults to requiring an HTTP success status. Output
/// extraction is layered on by a later stage.
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
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <param name="cancellationTokenVariable">The in-scope <c>CancellationToken</c> variable name.</param>
    /// <returns>The emitted static field declarations and the in-method statements.</returns>
    public static StepBodyCode Emit(
        string stepId,
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        IReadOnlyList<StepCriterion> successCriteria,
        string transportVariable,
        string contextVariable,
        string cancellationTokenVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals)
    {
        ArgumentException.ThrowIfNullOrEmpty(stepId);
        ArgumentNullException.ThrowIfNull(successCriteria);
        ArgumentNullException.ThrowIfNull(stepOutputLocals);

        string identifier = EmitText.SanitizeIdentifier(stepId);
        string prefix = $"{identifier}_";
        string requestVar = $"{EmitText.ToCamelCase(identifier)}Request";
        string responseVar = $"{EmitText.ToCamelCase(identifier)}Response";

        RequestBindingCode request = RequestBindingEmitter.Emit(operation, arguments, contextVariable, requestVar, prefix, stepOutputLocals);

        var fields = new StringBuilder(request.Fields);
        var body = new StringBuilder(request.Statements);

        body.Append("var ").Append(responseVar).Append(" = await ").Append(transportVariable)
            .Append(".SendAsync<").Append(operation.Operation.RequestTypeName).Append(", ")
            .Append(operation.Operation.ResponseTypeName).Append(">(").Append(requestVar)
            .Append(", ").Append(cancellationTokenVariable).AppendLine(").ConfigureAwait(false);");

        body.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        body.Append(contextVariable).Append(".SetResponseStatusCode(").Append(responseVar).AppendLine(".StatusCode);");

        // Feed each declared JSON body into the context, guarded by the matching status, so
        // $response.body resolves for criteria and outputs.
        foreach (ResponseDescriptor response in operation.Operation.Responses)
        {
            if (response.BodyPropertyName is { } bodyProperty
                && int.TryParse(response.StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
            {
                body.Append("if (").Append(responseVar).Append(".StatusCode == ")
                    .Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(") { ")
                    .Append(contextVariable).Append(".SetResponseBody(").Append(responseVar).Append('.').Append(bodyProperty)
                    .AppendLine("); }");
            }
        }

        EmitSuccessGate(fields, body, successCriteria, prefix, contextVariable, responseVar, stepId);

        return new StepBodyCode(fields.ToString(), body.ToString());
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
            body.Append("    throw new InvalidOperationException(")
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
        body.Append("    throw new InvalidOperationException(")
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