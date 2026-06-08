// <copyright file="StepBodyEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
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
/// Success criteria are inlined where possible: a pure <c>$statusCode &lt;op&gt; &lt;int&gt;</c>
/// comparison becomes a direct status-code comparison, and a single-comparison / lone-truthy
/// <c>simple</c> condition is inlined (via <see cref="SimpleCriterionInliner"/>) to evaluate directly
/// against the live response / inputs / prior-step outputs, reusing <see cref="Corvus.Text.Json.Arazzo.Comparand"/>
/// for the operand semantics — no <c>CompiledCriterion</c>, no context. Anything else (compound/negated/
/// grouped <c>simple</c>, <c>regex</c>, <c>jsonpath</c>) is compiled once into a <c>static readonly</c>
/// <c>CompiledCriterion</c> field. A step with no <c>successCriteria</c> defaults to requiring an HTTP
/// success status.
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
    /// <param name="outputs">The step's outputs, projected into the step-outputs product inside the step while the response is alive.</param>
    /// <param name="transportVariable">The in-scope <c>IApiTransport</c> variable name.</param>
    /// <param name="workspaceVariable">The in-scope <c>JsonWorkspace</c> variable name (owns the cloned response bodies).</param>
    /// <param name="contextVariable">The in-scope <c>WorkflowExecutionContext</c> variable name.</param>
    /// <param name="cancellationTokenVariable">The in-scope <c>CancellationToken</c> variable name.</param>
    /// <param name="stepOutputLocals">Map of step id → the local holding that step's outputs object.</param>
    /// <param name="inputsVariable">The in-scope workflow inputs variable name (for static <c>$inputs</c> navigation).</param>
    /// <param name="requestBody">The step's request body (expression or literal), or <see langword="null"/> when the step declares no (supported) request body.</param>
    /// <param name="bindResponseBody">
    /// Whether to feed the response body into the context. Set <see langword="false"/> when nothing in
    /// the step references <c>$response.body</c> — the body is then never cloned into the workspace,
    /// saving the clone (a document allocation) for status-only steps.
    /// </param>
    /// <returns>The emitted static field declarations and the in-method statements.</returns>
    public static StepBodyCode Emit(
        string stepId,
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        IReadOnlyList<StepCriterion> successCriteria,
        IReadOnlyList<OutputMapping> outputs,
        string transportVariable,
        string workspaceVariable,
        string contextVariable,
        string cancellationTokenVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        StepBody? requestBody = null,
        bool bindResponseBody = true)
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

        RequestBindingCode request = RequestBindingEmitter.Emit(operation, arguments, contextVariable, prefix, stepOutputLocals, inputsVariable, requestBody);

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

        // The step-outputs product is declared BEFORE the try so later steps can reference it, but
        // built INSIDE the try while the response is still alive — so its $response.body values are
        // projected (and only those copied) without cloning the whole body. The response is then
        // disposed in the finally.
        string responseBodyLocal = $"{camel}ResponseBody";
        bool hasOutputs = outputs.Count > 0;
        if (hasOutputs)
        {
            body.Append("JsonElement ").Append(EmitText.StepOutputsElementLocal(stepId)).AppendLine(" = default;");
        }

        body.AppendLine("try");
        body.AppendLine("{");

        var inner = new StringBuilder();
        inner.AppendLine("ArazzoTelemetry.StepsExecuted.Add(1);");
        inner.Append(contextVariable).Append(".SetResponseStatusCode(").Append(responseVar).AppendLine(".StatusCode);");

        // Bind the matched-status body as a LIVE reference (no clone) — used by criteria and projected
        // into outputs below, all while the response is alive.
        if (bindResponseBody)
        {
            inner.Append("JsonElement ").Append(responseBodyLocal).AppendLine(" = default;");
            foreach (ResponseDescriptor response in operation.Operation.Responses)
            {
                if (response.BodyPropertyName is { } bodyProperty
                    && int.TryParse(response.StatusCode, NumberStyles.Integer, CultureInfo.InvariantCulture, out int statusCode))
                {
                    inner.Append("if (").Append(responseVar).Append(".StatusCode == ")
                        .Append(statusCode.ToString(CultureInfo.InvariantCulture)).Append(") { ")
                        .Append(responseBodyLocal).Append(" = (JsonElement)").Append(responseVar).Append('.').Append(bodyProperty)
                        .AppendLine("; }");
                }
            }

            inner.Append(contextVariable).Append(".SetResponseBody(").Append(responseBodyLocal).AppendLine(");");
        }

        EmitSuccessGate(
            fields, inner, successCriteria, prefix, contextVariable, responseVar,
            bindResponseBody ? responseBodyLocal : null, inputsVariable, stepOutputLocals, stepId);

        if (hasOutputs)
        {
            OutputExtractionCode outputCode = OutputExtractionEmitter.Emit(
                stepId, outputs, workspaceVariable, contextVariable, stepOutputLocals, inputsVariable,
                bindResponseBody ? responseBodyLocal : null);
            fields.Append(outputCode.Fields);
            inner.Append(outputCode.Statements);
        }

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
        string? responseBodyLocal,
        string inputsVariable,
        IReadOnlyDictionary<string, string> stepOutputLocals,
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
            string index = i.ToString(CultureInfo.InvariantCulture);

            // A pure `$statusCode <op> <int>` simple criterion is evaluated at generation time into a
            // direct comparison against the response's status code — no CompiledCriterion field, no
            // context, no allocation.
            if (TryEmitStatusCodeCheck(criterion, responseVar, out string inlineCheck))
            {
                checks.Add(inlineCheck);
                continue;
            }

            // A single-comparison / lone-truthy simple criterion is inlined to evaluate directly
            // against the live response / inputs / prior-step outputs (reusing Comparand for operand
            // semantics) — its operand-resolution statements are emitted before the gate.
            if (MapCriterionType(criterion.Type) == "Simple"
                && SimpleCriterionInliner.TryEmit(
                    criterion.Condition, responseVar, responseBodyLocal, inputsVariable, stepOutputLocals,
                    $"{prefix}C{index}", fields, out string inlineStatements, out string inlineExpression))
            {
                body.Append(inlineStatements);
                checks.Add(inlineExpression);
                continue;
            }

            // Everything else (compound/negated/grouped simple, regex, jsonpath) falls back to the
            // compiled-criterion runtime.
            string field = $"{prefix}SuccessCriterion{index}";
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

    /// <summary>
    /// Recognises a pure <c>$statusCode &lt;op&gt; &lt;int&gt;</c> <c>simple</c> criterion and emits it as
    /// a direct comparison against the response's status code, evaluated entirely at generation time.
    /// </summary>
    /// <param name="criterion">The criterion to inspect.</param>
    /// <param name="responseVar">The in-scope response variable name.</param>
    /// <param name="check">The emitted inline boolean expression when recognised; otherwise empty.</param>
    /// <returns><see langword="true"/> when the criterion was emitted inline.</returns>
    private static bool TryEmitStatusCodeCheck(in StepCriterion criterion, string responseVar, out string check)
    {
        check = string.Empty;

        // Only a context-free simple criterion can be a bare $statusCode comparison.
        if (criterion.Context is not null || MapCriterionType(criterion.Type) != "Simple")
        {
            return false;
        }

        Match match = StatusCodeCriterion.Match(criterion.Condition);
        if (!match.Success)
        {
            return false;
        }

        string op = match.Groups[1].Value;
        string number = match.Groups[2].Value;
        check = $"{responseVar}.StatusCode {op} {number}";
        return true;
    }

    private static readonly Regex StatusCodeCriterion = new(
        @"^\s*\$statusCode\s*(==|!=|<=|>=|<|>)\s*(\d+)\s*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
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