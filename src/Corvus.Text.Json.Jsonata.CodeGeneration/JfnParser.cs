// <copyright file="JfnParser.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Generic;

namespace Corvus.Text.Json.Jsonata.CodeGeneration;

/// <summary>
/// Parses <c>.jfn</c> files into <see cref="CustomFunction"/> definitions.
/// </summary>
/// <remarks>
/// <para>
/// The <c>.jfn</c> format supports two forms:
/// </para>
/// <para>
/// <b>Expression form</b> (single-line):
/// <code>fn name(param1, param2) =&gt; expression;</code>
/// </para>
/// <para>
/// <b>Block form</b> (multi-line):
/// <code>
/// fn name(param1, param2)
/// {
///     statements;
///     return result;
/// }
/// </code>
/// </para>
/// <para>
/// Lines starting with <c>//</c> are comments. Blank lines are ignored.
/// </para>
/// </remarks>
public static class JfnParser
{
    /// <summary>
    /// Parses the content of a <c>.jfn</c> file into a list of custom function definitions.
    /// </summary>
    /// <param name="content">The full text content of the <c>.jfn</c> file.</param>
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

            // Parse "fn name(params) => expr;" or "fn name(params)"
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
            string[] parameters = paramStr.Length == 0
                ? Array.Empty<string>()
                : ParseParameters(paramStr, i + 1);

            string afterParams = rest.Substring(parenClose + 1).Trim();

            if (afterParams.StartsWith("=>", StringComparison.Ordinal))
            {
                // Expression form: fn name(params) => expression;
                string expr = afterParams.Substring(2).Trim();
                if (expr.EndsWith(";", StringComparison.Ordinal))
                {
                    expr = expr.Substring(0, expr.Length - 1).Trim();
                }

                if (expr.Length == 0)
                {
                    throw new FormatException($"Empty expression body at line {i + 1}: {line}");
                }

                functions.Add(new CustomFunction(name, parameters, expr, isExpression: true));
                i++;
            }
            else if (afterParams.Length == 0 || afterParams == "{")
            {
                // Block form: fn name(params) { ... } or fn name(params)\n{ ... }
                if (afterParams == "{")
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
                functions.Add(new CustomFunction(name, parameters, body, isExpression: false));
            }
            else
            {
                throw new FormatException(
                    $"Expected '=>' or '{{' after function signature at line {i + 1}: {line}");
            }
        }

        return functions;
    }

    private static string[] ParseParameters(string paramStr, int lineNumber)
    {
        string[] parts = paramStr.Split(',');
        string[] result = new string[parts.Length];

        for (int j = 0; j < parts.Length; j++)
        {
            string p = parts[j].Trim();
            if (p.Length == 0)
            {
                throw new FormatException($"Empty parameter name at line {lineNumber}");
            }

            result[j] = p;
        }

        return result;
    }
}