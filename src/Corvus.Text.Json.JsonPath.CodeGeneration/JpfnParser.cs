// <copyright file="JpfnParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.JsonPath.CodeGeneration;

/// <summary>
/// Parses <c>.jpfn</c> files into <see cref="CustomFunction"/> definitions.
/// </summary>
/// <remarks>
/// <para>
/// The <c>.jpfn</c> format supports two forms:
/// </para>
/// <para>
/// <b>Expression form</b> (single-line):
/// <code>fn name(value p1, nodes p2) : value =&gt; expression;</code>
/// </para>
/// <para>
/// <b>Block form</b> (multi-line):
/// <code>
/// fn name(value p1) : logical
/// {
///     statements;
///     return result;
/// }
/// </code>
/// </para>
/// <para>
/// Parameter types: <c>value</c> (JsonElement), <c>logical</c> (bool), <c>nodes</c>
/// (ReadOnlySpan&lt;JsonElement&gt;). Return types: <c>value</c> or <c>logical</c>.
/// Lines starting with <c>//</c> are comments. Blank lines are ignored.
/// </para>
/// </remarks>
public static class JpfnParser
{
    /// <summary>
    /// Parses the content of a <c>.jpfn</c> file into a list of custom function definitions.
    /// </summary>
    /// <param name="content">The full text content of the <c>.jpfn</c> file.</param>
    /// <returns>A list of parsed <see cref="CustomFunction"/> definitions.</returns>
    /// <exception cref="FormatException">
    /// Thrown when the content contains invalid syntax.
    /// </exception>
    public static IReadOnlyList<CustomFunction> Parse(string content)
    {
        List<CustomFunction> functions = new();
        string[] lines = content.Split('\n');
        int i = 0;

        while (i < lines.Length)
        {
            string line = lines[i].TrimEnd('\r').Trim();

            // Skip blank lines and comments
            if (line.Length == 0 || line.StartsWith("//", StringComparison.Ordinal))
            {
                i++;
                continue;
            }

            if (!line.StartsWith("fn ", StringComparison.Ordinal))
            {
                throw new FormatException($"Expected 'fn' keyword at line {i + 1}: {line}");
            }

            // Parse "fn name(type param, ...) : returnType => expr;" or block form
            string rest = line.Substring(3).Trim();

            int parenOpen = rest.IndexOf('(');
            if (parenOpen < 0)
            {
                throw new FormatException($"Expected '(' in function definition at line {i + 1}: {line}");
            }

            string name = rest.Substring(0, parenOpen).Trim();
            if (name.Length == 0)
            {
                throw new FormatException($"Missing function name at line {i + 1}: {line}");
            }

            int parenClose = rest.IndexOf(')', parenOpen);
            if (parenClose < 0)
            {
                throw new FormatException($"Expected ')' in function definition at line {i + 1}: {line}");
            }

            string paramStr = rest.Substring(parenOpen + 1, parenClose - parenOpen - 1).Trim();
            FunctionParameter[] parameters = paramStr.Length == 0
                ? Array.Empty<FunctionParameter>()
                : ParseParameters(paramStr, i + 1);

            string afterParams = rest.Substring(parenClose + 1).Trim();

            // Parse return type: ": value" or ": logical"
            if (!afterParams.StartsWith(":", StringComparison.Ordinal))
            {
                throw new FormatException(
                    $"Expected ':' followed by return type after parameters at line {i + 1}: {line}");
            }

            string afterColon = afterParams.Substring(1).Trim();
            FunctionParamType returnType;
            string afterReturnType;

            if (afterColon.StartsWith("value", StringComparison.Ordinal))
            {
                returnType = FunctionParamType.Value;
                afterReturnType = afterColon.Substring(5).Trim();
            }
            else if (afterColon.StartsWith("logical", StringComparison.Ordinal))
            {
                returnType = FunctionParamType.Logical;
                afterReturnType = afterColon.Substring(7).Trim();
            }
            else
            {
                throw new FormatException(
                    $"Expected return type 'value' or 'logical' at line {i + 1}: {line}");
            }

            if (afterReturnType.StartsWith("=>", StringComparison.Ordinal))
            {
                // Expression form
                string expr = afterReturnType.Substring(2).Trim();
                if (expr.EndsWith(";", StringComparison.Ordinal))
                {
                    expr = expr.Substring(0, expr.Length - 1).Trim();
                }

                if (expr.Length == 0)
                {
                    throw new FormatException($"Empty expression body at line {i + 1}: {line}");
                }

                functions.Add(new CustomFunction(name, parameters, returnType, expr, isExpression: true));
                i++;
            }
            else if (afterReturnType.Length == 0 || afterReturnType == "{")
            {
                // Block form
                if (afterReturnType == "{")
                {
                    i++;
                }
                else
                {
                    i++;

                    // Find opening brace
                    while (i < lines.Length)
                    {
                        string nextLine = lines[i].TrimEnd('\r').Trim();
                        if (nextLine.Length == 0 || nextLine.StartsWith("//", StringComparison.Ordinal))
                        {
                            i++;
                            continue;
                        }

                        if (nextLine.StartsWith("{", StringComparison.Ordinal))
                        {
                            i++;
                            break;
                        }

                        throw new FormatException(
                            $"Expected '{{' or '=>' after function signature at line {i + 1}: {nextLine}");
                    }

                    if (i >= lines.Length)
                    {
                        throw new FormatException(
                            $"Unexpected end of file after function signature for '{name}'");
                    }
                }

                // Collect block body until matching closing brace
                int braceDepth = 1;
                List<string> bodyLines = new();

                while (i < lines.Length && braceDepth > 0)
                {
                    string bodyLine = lines[i].TrimEnd('\r');
                    foreach (char c in bodyLine)
                    {
                        if (c == '{')
                        {
                            braceDepth++;
                        }
                        else if (c == '}')
                        {
                            braceDepth--;
                        }
                    }

                    if (braceDepth > 0)
                    {
                        bodyLines.Add(bodyLine);
                    }

                    i++;
                }

                if (braceDepth != 0)
                {
                    throw new FormatException($"Unmatched '{{' in block body for function '{name}'");
                }

                string body = string.Join("\n", bodyLines);
                functions.Add(new CustomFunction(name, parameters, returnType, body, isExpression: false));
            }
            else
            {
                throw new FormatException(
                    $"Expected '=>' or '{{' after return type at line {i + 1}: {line}");
            }
        }

        return functions;
    }

    private static FunctionParameter[] ParseParameters(string paramStr, int lineNumber)
    {
        string[] parts = paramStr.Split(',');
        FunctionParameter[] result = new FunctionParameter[parts.Length];

        for (int j = 0; j < parts.Length; j++)
        {
            string p = parts[j].Trim();
            if (p.Length == 0)
            {
                throw new FormatException($"Empty parameter at line {lineNumber}");
            }

            // Split "type name" — type is optional, defaults to "value"
            int spaceIdx = p.IndexOf(' ');
            if (spaceIdx < 0)
            {
                // No type specified: default to value
                result[j] = new FunctionParameter(FunctionParamType.Value, p);
            }
            else
            {
                string typePart = p.Substring(0, spaceIdx).Trim();
                string namePart = p.Substring(spaceIdx + 1).Trim();

                if (namePart.Length == 0)
                {
                    throw new FormatException($"Missing parameter name at line {lineNumber}");
                }

                FunctionParamType type = typePart switch
                {
                    "value" => FunctionParamType.Value,
                    "logical" => FunctionParamType.Logical,
                    "nodes" => FunctionParamType.Nodes,
                    _ => throw new FormatException(
                        $"Unknown parameter type '{typePart}' at line {lineNumber}. Expected 'value', 'logical', or 'nodes'."),
                };

                result[j] = new FunctionParameter(type, namePart);
            }
        }

        return result;
    }
}