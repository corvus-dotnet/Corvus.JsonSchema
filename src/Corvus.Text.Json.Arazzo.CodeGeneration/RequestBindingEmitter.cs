// <copyright file="RequestBindingEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.OpenApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Emits the code that constructs a step's generated request, binding each Arazzo argument to the
/// matching generated request property via <c>TTarget.From(source)</c> (plan §3.1).
/// </summary>
/// <remarks>
/// Each argument's runtime expression is parsed once into a <c>static readonly</c>
/// <c>ArazzoExpression</c> field (no per-execution parse/allocation); at run time the value is
/// resolved to a <see cref="JsonElement"/> via <c>WorkflowExecutionContext.TryResolveValue</c> and
/// bound to the property's generated type with <c>From</c>. Required parameters become constructor
/// arguments (in the generator's parameter order); optional parameters become object-initializer
/// assignments.
/// </remarks>
public static class RequestBindingEmitter
{
    /// <summary>
    /// Emits the request-construction code for an operation step.
    /// </summary>
    /// <param name="operation">The resolved operation the step targets.</param>
    /// <param name="arguments">The step's arguments (parameter name → runtime-expression value).</param>
    /// <param name="contextVariable">The name of the in-scope <c>WorkflowExecutionContext</c> variable.</param>
    /// <param name="requestVariable">The name of the local the constructed request is assigned to.</param>
    /// <param name="fieldPrefix">A unique prefix (e.g. the step id) for the emitted static expression fields.</param>
    /// <returns>The emitted static field declarations and the in-method construction statements.</returns>
    /// <exception cref="InvalidOperationException">A required parameter has no argument.</exception>
    public static RequestBindingCode Emit(
        in ResolvedOperation operation,
        IReadOnlyList<StepArgument> arguments,
        string contextVariable,
        string requestVariable,
        string fieldPrefix)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentException.ThrowIfNullOrEmpty(contextVariable);
        ArgumentException.ThrowIfNullOrEmpty(requestVariable);
        ArgumentException.ThrowIfNullOrEmpty(fieldPrefix);

        var argumentsByName = new Dictionary<string, StepArgument>(StringComparer.Ordinal);
        foreach (StepArgument argument in arguments)
        {
            argumentsByName[argument.Name] = argument;
        }

        var fields = new StringBuilder();
        var statements = new StringBuilder();
        var constructorArguments = new List<string>();
        var initializers = new List<string>();

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

            fields.Append("private static readonly ArazzoExpression ")
                .Append(field)
                .Append(" = ArazzoExpression.Parse(")
                .Append(EmitText.Quote(argument.Expression))
                .AppendLine(");");

            statements.Append(contextVariable)
                .Append(".TryResolveValue(")
                .Append(field)
                .Append(", out JsonElement ")
                .Append(local)
                .AppendLine(");");

            string bound = $"{parameter.TypeName}.From({local})";
            if (parameter.IsRequired)
            {
                constructorArguments.Add(bound);
            }
            else
            {
                initializers.Add($"{parameter.PropertyName} = {bound},");
            }
        }

        statements.Append("var ")
            .Append(requestVariable)
            .Append(" = new ")
            .Append(operation.Operation.RequestTypeName)
            .Append('(')
            .Append(string.Join(", ", constructorArguments))
            .Append(')');

        if (initializers.Count > 0)
        {
            statements.AppendLine();
            statements.AppendLine("{");
            foreach (string initializer in initializers)
            {
                statements.Append("    ").AppendLine(initializer);
            }

            statements.AppendLine("};");
        }
        else
        {
            statements.AppendLine(";");
        }

        return new RequestBindingCode(fields.ToString(), statements.ToString());
    }
}

/// <summary>
/// An argument a step passes to an operation parameter (plan §3.1).
/// </summary>
/// <param name="Name">The operation parameter name to bind.</param>
/// <param name="Expression">The runtime expression that produces the value (e.g. <c>$inputs.petId</c>).</param>
public readonly record struct StepArgument(string Name, string Expression);

/// <summary>
/// The code emitted for a step's request construction (plan §3.1).
/// </summary>
/// <param name="Fields">The <c>static readonly</c> compiled-expression field declarations to place on the executor class.</param>
/// <param name="Statements">The in-method statements that resolve the arguments and construct the request.</param>
public readonly record struct RequestBindingCode(string Fields, string Statements);