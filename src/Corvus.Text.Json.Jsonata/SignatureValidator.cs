// <copyright file="SignatureValidator.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Jsonata;

/// <summary>
/// Validates JSONata function signatures at compile-time (syntax) and at invoke-time (type checking).
/// </summary>
/// <remarks>
/// <para>
/// Signature syntax: <c>&lt;params:return&gt;</c> where params are type characters:
/// <list type="bullet">
/// <item><c>n</c> — number</item>
/// <item><c>s</c> — string</item>
/// <item><c>b</c> — boolean</item>
/// <item><c>o</c> — object</item>
/// <item><c>a</c> — array (optionally followed by <c>&lt;subtype&gt;</c>)</item>
/// <item><c>f</c> — function</item>
/// <item><c>x</c> — any type (expression)</item>
/// <item><c>j</c> — any JSON value</item>
/// <item><c>l</c> — null</item>
/// </list>
/// </para>
/// <para>
/// Modifiers: <c>?</c> (optional), <c>+</c> (one-or-more variadic).
/// <c>(types)</c> = union type, <c>-</c> = context separator, <c>:</c> = return type separator.
/// </para>
/// </remarks>
internal static class SignatureValidator
{
    /// <summary>
    /// Validates the syntax of a signature string at compile time.
    /// </summary>
    /// <param name="signature">The raw signature string including angle brackets.</param>
    /// <param name="position">The source position for error reporting.</param>
    /// <exception cref="JsonataException">
    /// Thrown with code S0401 for invalid type characters or invalid sub-type on non-array types,
    /// or S0402 for malformed/unbalanced brackets.
    /// </exception>
    public static void ValidateSyntax(string signature, int position)
    {
        if (signature.Length < 2 || signature[0] != '<')
        {
            throw new JsonataException("S0402", $"The function signature \"{signature}\" is not valid. Unrecognized signature structure", position);
        }

        int i = 1; // Skip leading '<'
        int angleBracketDepth = 1;
        char prevTypeChar = '\0';

        while (i < signature.Length)
        {
            char c = signature[i];

            if (c == '<')
            {
                angleBracketDepth++;

                // Sub-type specifier is valid after 'a' (array) or 'f' (function with signature)
                if (prevTypeChar != 'a' && prevTypeChar != 'f' && prevTypeChar != '\0')
                {
                    throw new JsonataException("S0401", $"The \"{prevTypeChar}\" type in the function signature \"{signature}\" does not support sub-types", position);
                }

                i++;
                continue;
            }

            if (c == '>')
            {
                angleBracketDepth--;
                if (angleBracketDepth < 0)
                {
                    throw new JsonataException("S0402", $"The function signature \"{signature}\" is not valid. Unexpected '>'", position);
                }

                prevTypeChar = '\0';
                i++;
                continue;
            }

            if (c == '(')
            {
                // Union type — scan until matching ')'
                i++;
                int parenDepth = 1;
                while (i < signature.Length && parenDepth > 0)
                {
                    if (signature[i] == '(')
                    {
                        parenDepth++;
                    }
                    else if (signature[i] == ')')
                    {
                        parenDepth--;
                    }

                    i++;
                }

                if (parenDepth != 0)
                {
                    throw new JsonataException("S0402", $"The function signature \"{signature}\" is not valid. Unmatched '('", position);
                }

                prevTypeChar = 'x'; // Union is like "any"
                continue;
            }

            if (c == ':')
            {
                prevTypeChar = '\0';
                i++;
                continue;
            }

            if (c == '-' || c == '?' || c == '+')
            {
                prevTypeChar = '\0';
                i++;
                continue;
            }

            if (c == ' ')
            {
                i++;
                continue;
            }

            // Type character
            if (IsTypeChar(c))
            {
                prevTypeChar = c;
                i++;
                continue;
            }

            // Unknown character
            throw new JsonataException("S0401", $"The \"{c}\" type in the function signature \"{signature}\" is not valid", position);
        }

        if (angleBracketDepth != 0)
        {
            throw new JsonataException("S0402", $"The function signature \"{signature}\" is not valid. Unmatched '<'", position);
        }
    }

    /// <summary>
    /// Validates argument types at invoke time against a parsed signature.
    /// </summary>
    /// <param name="signature">The raw signature string.</param>
    /// <param name="args">The evaluated arguments.</param>
    /// <param name="position">Source position for error reporting.</param>
    /// <exception cref="JsonataException">
    /// Thrown with code T0410 for argument type mismatch or T0412 for array element type mismatch.
    /// </exception>
    public static void ValidateArgs(string signature, Sequence[] args, int position)
    {
        // Parse the parameter type specs from the signature
        var paramSpecs = ParseParamSpecs(signature);

        int argIdx = 0;
        for (int paramIdx = 0; paramIdx < paramSpecs.Count; paramIdx++)
        {
            var spec = paramSpecs[paramIdx];

            if (spec.IsContextSeparator)
            {
                // The '-' separator doesn't stop validation — it just marks which params
                // can be context-filled. Continue to the next param spec.
                continue;
            }

            if (spec.IsVariadic)
            {
                // Consume args that match this type spec. Stop when we hit one that
                // doesn't match (so the next param spec can pick it up), or when there
                // are just enough remaining args for the remaining non-variadic params.
                int remainingRequiredParams = 0;
                for (int k = paramIdx + 1; k < paramSpecs.Count; k++)
                {
                    if (!paramSpecs[k].IsOptional && !paramSpecs[k].IsContextSeparator && !paramSpecs[k].IsVariadic)
                    {
                        remainingRequiredParams++;
                    }
                }

                while (argIdx < args.Length - remainingRequiredParams)
                {
                    if (!args[argIdx].IsUndefined)
                    {
                        var element = args[argIdx].FirstOrDefault;
                        if (!MatchesTypeForVariadic(element, spec, args[argIdx]))
                        {
                            break;
                        }

                        ValidateSingleArg(signature, spec, args[argIdx], argIdx + 1, position);
                    }

                    argIdx++;
                }

                continue;
            }

            if (argIdx >= args.Length)
            {
                // Remaining params are unfilled — acceptable (context or optional)
                break;
            }

            var arg = args[argIdx];

            // For optional params, if the arg doesn't match the type, skip the
            // param (don't consume the arg) so the next param spec can try it.
            if (spec.IsOptional && !arg.IsUndefined)
            {
                var element = arg.FirstOrDefault;
                if (!MatchesType(element, spec, arg))
                {
                    // Optional param doesn't match — skip it without consuming arg
                    continue;
                }
            }

            // Skip undefined args (optional)
            if (!arg.IsUndefined)
            {
                ValidateSingleArg(signature, spec, arg, argIdx + 1, position);
            }

            argIdx++;
        }

        // Check for extra arguments beyond what the signature expects
        if (argIdx < args.Length)
        {
            throw new JsonataException("T0410", $"Too many arguments for function {signature}", position);
        }
    }

    private static void ValidateSingleArg(string signature, ParamSpec spec, Sequence arg, int argNumber, int position)
    {
        if (arg.IsUndefined)
        {
            return;
        }

        var element = arg.FirstOrDefault;

        if (!MatchesType(element, spec, arg))
        {
            if (spec.SubType is not null && (spec.Types.Contains('a') || spec.Types.Count == 0))
            {
                throw new JsonataException("T0412", $"Argument {argNumber} of function {signature} expected array of {DescribeType(spec.SubType)} but got {DescribeValue(element)}", position);
            }

            throw new JsonataException("T0410", $"Argument {argNumber} of function {signature} expected {DescribeTypes(spec)} but got {DescribeValue(element)}", position);
        }

        // Check array element sub-type
        if (spec.SubType is not null && spec.Types.Contains('a'))
        {
            if (element.ValueKind == JsonValueKind.Array)
            {
                for (int i = 0; i < element.GetArrayLength(); i++)
                {
                    if (!MatchesSingleType(element[i], spec.SubType))
                    {
                        throw new JsonataException("T0412", $"Argument {argNumber} of function {signature} expected array of {DescribeType(spec.SubType)} but got array containing {DescribeValue(element[i])}", position);
                    }
                }
            }
            else
            {
                // Non-array passed to a<subtype> — the auto-wrapped value must match sub-type
                if (!MatchesSingleType(element, spec.SubType))
                {
                    throw new JsonataException("T0412", $"Argument {argNumber} of function {signature} expected array of {DescribeType(spec.SubType)} but got {DescribeValue(element)}", position);
                }
            }
        }
    }

    private static bool MatchesType(in JsonElement element, ParamSpec spec, Sequence seq)
    {
        foreach (char type in spec.Types)
        {
            if (MatchesSingleTypeChar(element, type, seq))
            {
                return true;
            }
        }

        return spec.Types.Count == 0; // Empty = any
    }

    private static bool MatchesTypeForVariadic(in JsonElement element, ParamSpec spec, Sequence seq)
    {
        // For variadic matching, check if the element matches without throwing.
        // This is used to determine when to stop consuming args for a variadic param.
        return MatchesType(element, spec, seq);
    }

    private static bool MatchesSingleTypeChar(in JsonElement element, char type, Sequence seq)
    {
        return type switch
        {
            'n' => element.ValueKind == JsonValueKind.Number,
            's' => element.ValueKind == JsonValueKind.String,
            'b' => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
            'o' => element.ValueKind == JsonValueKind.Object,
            'a' => true, // JSONata auto-wraps non-arrays; accept any value
            'f' => seq.IsLambda,
            'l' => element.ValueKind == JsonValueKind.Null,
            'x' or 'j' => true,
            _ => false,
        };
    }

    private static bool MatchesSingleType(in JsonElement element, ParamSpec spec)
    {
        foreach (char type in spec.Types)
        {
            if (type switch
            {
                'n' => element.ValueKind == JsonValueKind.Number,
                's' => element.ValueKind == JsonValueKind.String,
                'b' => element.ValueKind is JsonValueKind.True or JsonValueKind.False,
                'o' => element.ValueKind == JsonValueKind.Object,
                'a' => element.ValueKind == JsonValueKind.Array,
                'l' => element.ValueKind == JsonValueKind.Null,
                'x' or 'j' => true,
                _ => false,
            })
            {
                return true;
            }
        }

        return false;
    }

    private static List<ParamSpec> ParseParamSpecs(string signature)
    {
        var specs = new List<ParamSpec>();
        int i = 1; // Skip '<'
        int depth = 0;

        while (i < signature.Length - 1) // Stop before final '>'
        {
            char c = signature[i];

            if (c == ':')
            {
                // Past return type separator — stop parsing params
                break;
            }

            if (c == '-')
            {
                specs.Add(new ParamSpec { IsContextSeparator = true });
                i++;
                continue;
            }

            if (c == '?' || c == '+' || c == ' ')
            {
                if (c == '?' && specs.Count > 0)
                {
                    specs[specs.Count - 1].IsOptional = true;
                }

                if (c == '+' && specs.Count > 0)
                {
                    specs[specs.Count - 1].IsVariadic = true;
                }

                i++;
                continue;
            }

            if (c == '(')
            {
                // Union type
                var spec = new ParamSpec();
                i++; // Skip '('
                while (i < signature.Length && signature[i] != ')')
                {
                    char tc = signature[i];
                    if (IsTypeChar(tc))
                    {
                        spec.Types.Add(tc);

                        // Check for sub-type after 'a'
                        if (tc == 'a' && i + 1 < signature.Length && signature[i + 1] == '<')
                        {
                            i += 2; // Skip 'a<'
                            var subSpec = new ParamSpec();
                            while (i < signature.Length && signature[i] != '>')
                            {
                                if (IsTypeChar(signature[i]))
                                {
                                    subSpec.Types.Add(signature[i]);
                                }

                                i++;
                            }

                            if (i < signature.Length)
                            {
                                i++; // Skip '>'
                            }

                            spec.SubType = subSpec;
                            continue;
                        }
                    }

                    i++;
                }

                if (i < signature.Length)
                {
                    i++; // Skip ')'
                }

                specs.Add(spec);
                continue;
            }

            if (c == '<')
            {
                // Sub-type — should follow 'a'
                depth++;
                i++;

                if (specs.Count > 0)
                {
                    var subSpec = new ParamSpec();
                    while (i < signature.Length && !(signature[i] == '>' && depth == 1))
                    {
                        if (signature[i] == '<')
                        {
                            depth++;
                        }
                        else if (signature[i] == '>')
                        {
                            depth--;
                            if (depth == 0)
                            {
                                break;
                            }
                        }
                        else if (IsTypeChar(signature[i]))
                        {
                            subSpec.Types.Add(signature[i]);
                        }

                        i++;
                    }

                    if (i < signature.Length)
                    {
                        i++; // Skip '>'
                        depth = 0;
                    }

                    specs[specs.Count - 1].SubType = subSpec;
                }

                continue;
            }

            if (c == '>')
            {
                depth--;
                i++;
                continue;
            }

            if (IsTypeChar(c))
            {
                specs.Add(new ParamSpec { Types = { c } });
                i++;
                continue;
            }

            i++;
        }

        return specs;
    }

    private static bool IsTypeChar(char c) => c is 'n' or 's' or 'b' or 'o' or 'a' or 'f' or 'x' or 'j' or 'l';

    private static string DescribeTypes(ParamSpec spec)
    {
        if (spec.Types.Count == 0)
        {
            return "any";
        }

        return string.Join("/", spec.Types.Select(DescribeTypeChar));
    }

    private static string DescribeType(ParamSpec spec)
    {
        return DescribeTypes(spec);
    }

    private static string DescribeTypeChar(char c)
    {
        return c switch
        {
            'n' => "number",
            's' => "string",
            'b' => "boolean",
            'o' => "object",
            'a' => "array",
            'f' => "function",
            'l' => "null",
            'x' or 'j' => "any",
            _ => c.ToString(),
        };
    }

    private static string DescribeValue(in JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Number => $"number ({element})",
            JsonValueKind.String => $"\"{element.GetString()}\"",
            JsonValueKind.True or JsonValueKind.False => element.GetRawText(),
            JsonValueKind.Object => "object",
            JsonValueKind.Array => "array",
            JsonValueKind.Null => "null",
            _ => "undefined",
        };
    }

    private sealed class ParamSpec
    {
        public List<char> Types { get; set; } = [];

        public ParamSpec? SubType { get; set; }

        public bool IsOptional { get; set; }

        public bool IsVariadic { get; set; }

        public bool IsContextSeparator { get; set; }
    }
}