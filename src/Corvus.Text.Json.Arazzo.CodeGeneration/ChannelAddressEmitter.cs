// <copyright file="ChannelAddressEmitter.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text;
using Corvus.Text.Json.AsyncApi.CodeGeneration;

namespace Corvus.Text.Json.Arazzo.CodeGeneration;

/// <summary>
/// Resolves an AsyncAPI channel's address parameters (the <c>{name}</c> placeholders in a parameterised
/// channel address) from a step's arguments. A send step passes the resolved values to the generated
/// producer's channel-parameter arguments; a receive step substitutes them into the address template to
/// build the runtime subscription address.
/// </summary>
internal static class ChannelAddressEmitter
{
    /// <summary>
    /// Emits the statements that resolve each channel parameter (in declaration order) to a
    /// <see cref="string"/> local and returns the local names, keyed by parameter name.
    /// </summary>
    public static IReadOnlyList<(string Name, string Local)> ResolveParameters(
        IReadOnlyList<string> channelParameters,
        IReadOnlyList<StepArgument> arguments,
        StringBuilder fields,
        StringBuilder statements,
        string prefix,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors)
    {
        var resolved = new List<(string Name, string Local)>(channelParameters.Count);
        foreach (string name in channelParameters)
        {
            StepArgument? match = null;
            foreach (StepArgument argument in arguments)
            {
                if (argument.Name == name)
                {
                    match = argument;
                    break;
                }
            }

            if (match is not { } parameter)
            {
                throw new NotSupportedException($"Channel address parameter '{name}' has no matching step parameter; a parameterised channel address needs a step parameter for each placeholder.");
            }

            string identifier = EmitText.SanitizeIdentifier(name);
            string local = $"{prefix}{identifier}Param";
            switch (parameter.Kind)
            {
                case ArgumentValueKind.LiteralString:
                    statements.Append("string ").Append(local).Append(" = ").Append(EmitText.Quote(parameter.Value)).AppendLine(";");
                    break;

                case ArgumentValueKind.Expression:
                    string elementLocal = $"{local}Element";
                    ValueResolution.Emit(fields, statements, parameter.Value, elementLocal, "context", stepOutputLocals, $"{prefix}{identifier}", inputsVariable, inputAccessors);
                    statements.Append("string ").Append(local).Append(" = ").Append(elementLocal).AppendLine(".GetString();");
                    break;

                default:
                    throw new NotSupportedException($"Channel address parameter '{name}' binds a {parameter.Kind} value; only expression and string-literal channel parameters are supported.");
            }

            resolved.Add((name, local));
        }

        return resolved;
    }

    /// <summary>
    /// Emits the address argument for a receive step's transport call: a constant <c>"addr"u8.ToArray()</c>
    /// for a static channel, or a runtime-built <see cref="ReadOnlyMemory{T}"/> for a parameterised one
    /// (substituting the resolved parameter locals into the address template).
    /// </summary>
    public static string EmitReceiveAddress(
        in AsyncApiChannelDescriptor descriptor,
        IReadOnlyList<StepArgument> arguments,
        StringBuilder fields,
        StringBuilder statements,
        string prefix,
        IReadOnlyDictionary<string, string> stepOutputLocals,
        string inputsVariable,
        IReadOnlyDictionary<string, string>? inputAccessors,
        out string addressText)
    {
        if (descriptor.ChannelParameters.Count == 0)
        {
            // A static address: the channel text is the literal; the subscription wants its UTF-8 bytes.
            addressText = EmitText.Quote(descriptor.ChannelAddress);
            return $"{EmitText.Quote(descriptor.ChannelAddress)}u8.ToArray()";
        }

        IReadOnlyList<(string Name, string Local)> resolved = ResolveParameters(
            descriptor.ChannelParameters, arguments, fields, statements, prefix, stepOutputLocals, inputsVariable, inputAccessors);

        var locals = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach ((string name, string local) in resolved)
        {
            locals[name] = local;
        }

        // The resolved address is built once as a string (so the durable wait can persist the concrete
        // channel a parameterised receive is suspended on) and reused as the subscription's UTF-8 bytes.
        string addressTextLocal = $"{prefix}AddressText";
        string addressLocal = $"{prefix}Address";
        statements.Append("string ").Append(addressTextLocal)
            .Append(" = ").Append(BuildInterpolatedAddress(descriptor.ChannelAddress, locals)).AppendLine(";");
        statements.Append("ReadOnlyMemory<byte> ").Append(addressLocal)
            .Append(" = System.Text.Encoding.UTF8.GetBytes(").Append(addressTextLocal).AppendLine(");");
        addressText = addressTextLocal;
        return addressLocal;
    }

    /// <summary>
    /// Builds a C# interpolated-string expression for a channel address template: literal segments are
    /// emitted verbatim (with interpolation/quote escaping) and each <c>{name}</c> placeholder is replaced
    /// by its resolved string local.
    /// </summary>
    private static string BuildInterpolatedAddress(string template, IReadOnlyDictionary<string, string> parameterLocals)
    {
        var builder = new StringBuilder("$\"");
        int i = 0;
        while (i < template.Length)
        {
            char c = template[i];
            if (c == '{')
            {
                int close = template.IndexOf('}', i + 1);
                if (close > i)
                {
                    string name = template[(i + 1)..close];
                    if (parameterLocals.TryGetValue(name, out string? local))
                    {
                        builder.Append('{').Append(local).Append('}');
                        i = close + 1;
                        continue;
                    }
                }
            }

            // Literal character: escape interpolation braces and string-literal specials.
            switch (c)
            {
                case '{':
                    builder.Append("{{");
                    break;
                case '}':
                    builder.Append("}}");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                default:
                    builder.Append(c);
                    break;
            }

            i++;
        }

        builder.Append('"');
        return builder.ToString();
    }
}