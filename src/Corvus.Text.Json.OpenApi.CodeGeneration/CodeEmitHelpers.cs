// <copyright file="CodeEmitHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.OpenApi;

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Shared helper methods for emitting C# source code from OpenAPI specifications.
/// </summary>
/// <remarks>
/// <para>
/// These are version-independent utilities that operate on primitive types
/// (strings, enums, <see cref="IndentedWriter"/>). They are consumed by
/// version-specific code generators in the <c>OpenApi31</c> and <c>OpenApi30</c>
/// projects, and by the shared <see cref="ClientCodeEmitter"/>.
/// </para>
/// </remarks>
public static class CodeEmitHelpers
{
    /// <summary>
    /// Converts a name to a PascalCase C# identifier, stripping invalid characters.
    /// </summary>
    /// <param name="name">The name to sanitize.</param>
    /// <returns>A PascalCase identifier.</returns>
    public static string SanitizeIdentifier(string name)
    {
        char[] chars = new char[name.Length];
        bool capitalizeNext = true;
        int written = 0;

        foreach (char c in name)
        {
            if (c is ' ' or '_' or '-' or '.')
            {
                capitalizeNext = true;
                continue;
            }

            if (!char.IsLetterOrDigit(c))
            {
                continue;
            }

            chars[written++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
            capitalizeNext = false;
        }

        return new string(chars, 0, written);
    }

    /// <summary>
    /// Converts a name to a camelCase C# parameter identifier.
    /// </summary>
    /// <param name="name">The parameter name from the spec.</param>
    /// <returns>A camelCase identifier.</returns>
    public static string SanitizeParameterName(string name)
    {
        bool needsSanitizing = false;
        if (name.Length == 0 || (!char.IsLetter(name[0]) && name[0] != '_'))
        {
            needsSanitizing = true;
        }
        else
        {
            foreach (char c in name)
            {
                if (!char.IsLetterOrDigit(c) && c != '_')
                {
                    needsSanitizing = true;
                    break;
                }
            }
        }

        if (!needsSanitizing)
        {
            return name;
        }

        string pascal = SanitizeIdentifier(name);
        if (pascal.Length == 0)
        {
            return "_param";
        }

        return string.Concat(char.ToLowerInvariant(pascal[0]).ToString(), pascal.AsSpan(1));
    }

    /// <summary>
    /// Escapes a C# keyword by prefixing it with <c>@</c>.
    /// </summary>
    /// <param name="name">The identifier to escape.</param>
    /// <returns>The escaped identifier, or the original if not a keyword.</returns>
    public static string EscapeCSharpKeyword(string name) =>
        name switch
        {
            "abstract" or "as" or "base" or "bool" or "break" or "byte" or "case" or
            "catch" or "char" or "checked" or "class" or "const" or "continue" or
            "decimal" or "default" or "delegate" or "do" or "double" or "else" or
            "enum" or "event" or "explicit" or "extern" or "false" or "finally" or
            "fixed" or "float" or "for" or "foreach" or "goto" or "if" or "implicit" or
            "in" or "int" or "interface" or "internal" or "is" or "lock" or "long" or
            "namespace" or "new" or "null" or "object" or "operator" or "out" or
            "override" or "params" or "private" or "protected" or "public" or "readonly" or
            "ref" or "return" or "sbyte" or "sealed" or "short" or "sizeof" or
            "stackalloc" or "static" or "string" or "struct" or "switch" or "this" or
            "throw" or "true" or "try" or "typeof" or "uint" or "ulong" or "unchecked" or
            "unsafe" or "ushort" or "using" or "virtual" or "void" or "volatile" or "while"
                => $"@{name}",
            _ => name,
        };

    /// <summary>
    /// Escapes text for inclusion in an XML doc comment.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The XML-safe text.</returns>
    public static string EscapeXml(string text) =>
        text.Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal);

    /// <summary>
    /// Escapes text for inclusion in a C# string literal.
    /// </summary>
    /// <param name="text">The text to escape.</param>
    /// <returns>The escaped text.</returns>
    public static string EscapeStringLiteral(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);

    /// <summary>
    /// Gets the C# expression for an <see cref="OperationMethod"/> value.
    /// </summary>
    /// <param name="method">The operation method.</param>
    /// <returns>A C# expression string.</returns>
    public static string OperationMethodExpression(OperationMethod method) =>
        method switch
        {
            OperationMethod.Get => "OperationMethod.Get",
            OperationMethod.Post => "OperationMethod.Post",
            OperationMethod.Put => "OperationMethod.Put",
            OperationMethod.Delete => "OperationMethod.Delete",
            OperationMethod.Patch => "OperationMethod.Patch",
            OperationMethod.Head => "OperationMethod.Head",
            OperationMethod.Options => "OperationMethod.Options",
            OperationMethod.Trace => "OperationMethod.Trace",
            _ => $"(OperationMethod){(int)method}",
        };

    /// <summary>
    /// Emits the standard auto-generated file header with <c>using</c> directives.
    /// </summary>
    /// <param name="w">The writer.</param>
    public static void EmitHeader(IndentedWriter w)
    {
        w.WriteLine("// <auto-generated>");
        w.WriteLine("// This code was generated by the Corvus.Text.Json OpenAPI code generator.");
        w.WriteLine("// Do not edit this file directly.");
        w.WriteLine("// </auto-generated>");
        w.WriteLine();
        w.WriteLine("#nullable enable");
        w.WriteLine();
        w.WriteLine("using System.Buffers;");
        w.WriteLine("using Corvus.Runtime.InteropServices;");
        w.WriteLine("using Corvus.Text.Json;");
        w.WriteLine("using Corvus.Text.Json.Internal;");
        w.WriteLine("using Corvus.Text.Json.OpenApi;");
        w.WriteLine();
    }

    /// <summary>
    /// Converts a status code string to a descriptive name for use in identifiers.
    /// </summary>
    /// <param name="statusCode">The status code (e.g. <c>200</c>, <c>default</c>).</param>
    /// <returns>A PascalCase name.</returns>
    public static string StatusCodeToName(string statusCode) =>
        statusCode switch
        {
            "200" => "Ok",
            "201" => "Created",
            "202" => "Accepted",
            "204" => "NoContent",
            "301" => "MovedPermanently",
            "304" => "NotModified",
            "400" => "BadRequest",
            "401" => "Unauthorized",
            "403" => "Forbidden",
            "404" => "NotFound",
            "405" => "MethodNotAllowed",
            "409" => "Conflict",
            "422" => "UnprocessableEntity",
            "429" => "TooManyRequests",
            "500" => "InternalServerError",
            "502" => "BadGateway",
            "503" => "ServiceUnavailable",
            "default" => "Default",
            _ when statusCode.Length == 3 && statusCode[1] == 'X' && statusCode[2] == 'X' =>
                $"Status{statusCode[0]}xx",
            _ => $"Status{statusCode}",
        };

    /// <summary>
    /// Converts a header name (e.g. <c>X-Rate-Limit</c>) to a PascalCase property name.
    /// </summary>
    /// <param name="headerName">The HTTP header name.</param>
    /// <returns>A PascalCase property name.</returns>
    public static string HeaderNameToPropertyName(string headerName)
    {
        Span<char> buffer = stackalloc char[headerName.Length];
        int written = 0;
        bool capitalizeNext = true;

        for (int i = 0; i < headerName.Length; i++)
        {
            char c = headerName[i];

            if (c == '-' || c == '_' || c == '.')
            {
                capitalizeNext = true;
                continue;
            }

            buffer[written++] = capitalizeNext ? char.ToUpperInvariant(c) : c;
            capitalizeNext = false;
        }

        return new string(buffer[..written]);
    }

    /// <summary>
    /// Converts a PascalCase name to camelCase.
    /// </summary>
    /// <param name="name">The PascalCase name.</param>
    /// <returns>The camelCase name.</returns>
    public static string ToCamelCase(string name)
    {
        if (name.Length == 0)
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }

    /// <summary>
    /// Converts a string to PascalCase by splitting on spaces, underscores, and hyphens.
    /// </summary>
    /// <param name="input">The input string.</param>
    /// <returns>The PascalCase string.</returns>
    public static string ToPascalCase(string input)
    {
        var sb = new System.Text.StringBuilder(input.Length);
        bool capitalizeNext = true;

        foreach (char c in input)
        {
            if (c is ' ' or '_' or '-')
            {
                capitalizeNext = true;
                continue;
            }

            sb.Append(capitalizeNext ? char.ToUpperInvariant(c) : c);
            capitalizeNext = false;
        }

        return sb.ToString();
    }

    // ── Scalar UTF-8 value emission helpers ─────────────────────────────

    /// <summary>
    /// Emits code that gets the UTF-8 bytes for a string value via
    /// <c>GetUtf8String()</c> and writes them to <c>writer</c>.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitStringWrite(
        IndentedWriter w,
        string valueExpr,
        string uid)
    {
        w.WriteLine(
            $"using UnescapedUtf8JsonString utf8{uid} = " +
            $"((JsonElement){valueExpr}).GetUtf8String();");
    }

    /// <summary>
    /// Emits a <c>TryFormat</c> into a correctly-sized stackalloc buffer.
    /// After this, <c>buf{uid}[..bw{uid}]</c> holds the UTF-8 bytes.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="bufferSize">The buffer size for stackalloc.</param>
    public static void EmitTryFormatToLocal(
        IndentedWriter w,
        string valueExpr,
        string uid,
        int bufferSize)
    {
        w.WriteLine($"Span<byte> buf{uid} = stackalloc byte[{bufferSize}];");
        w.WriteLine(
            $"{valueExpr}.TryFormat(buf{uid}, " +
            $"out int bw{uid}, default, default);");
    }

    /// <summary>
    /// Emits code that gets the raw UTF-8 JSON bytes for an unbounded
    /// number via <c>JsonMarshal.GetRawUtf8Value</c>.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitUnboundedNumberToLocal(
        IndentedWriter w,
        string valueExpr,
        string uid)
    {
        w.WriteLine(
            $"using RawUtf8JsonString raw{uid} = " +
            $"JsonMarshal.GetRawUtf8Value({valueExpr});");
    }

    /// <summary>
    /// Returns the stackalloc buffer size for a bounded type, or -1 if
    /// the type doesn't use <c>TryFormat</c>.
    /// </summary>
    /// <param name="kind">The serialization kind.</param>
    /// <returns>The buffer size, or -1.</returns>
    public static int TryFormatBufferSize(ParameterSerializationKind kind)
    {
        int size = SchemaClassifier.GetMaxFormattedSize(kind);
        return size > 0 ? size : -1;
    }

    /// <summary>
    /// Emits a ternary that writes <c>"true"u8</c> or <c>"false"u8</c>
    /// based on the boolean JSON value.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the boolean value.</param>
    public static void EmitBooleanWrite(IndentedWriter w, string valueExpr)
    {
        w.WriteLine(
            $"writer.Write((bool){valueExpr} ? \"true\"u8 : \"false\"u8);");
    }

    /// <summary>
    /// Emits a boolean write with a <c>totalWritten</c> counter update.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the boolean value.</param>
    public static void EmitBooleanWriteCounted(IndentedWriter w, string valueExpr)
    {
        w.WriteLine(
            $"bool bv = (bool){valueExpr};");
        w.WriteLine(
            $"writer.Write(bv ? \"true\"u8 : \"false\"u8);");
        w.WriteLine(
            $"totalWritten += bv ? 4 : 5;");
    }

    /// <summary>
    /// Emits a boolean header write via the callback.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the boolean value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitBooleanHeaderWrite(IndentedWriter w, string valueExpr, string uid)
    {
        w.WriteLine(
            $"callback(nameUtf8{uid}, (bool){valueExpr} ? \"true\"u8 : \"false\"u8, state);");
    }

    /// <summary>
    /// Emits URI escaping and writing of a UTF-8 span to a buffer writer.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="spanExpr">The C# expression for the UTF-8 span.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitUriEscapeAndWrite(
        IndentedWriter w,
        string spanExpr,
        string uid)
    {
        w.WriteLine($"Span<byte> esc{uid} = stackalloc byte[{spanExpr}.Length * 3];");
        w.WriteLine(
            $"if (Utf8Uri.TryEscapeDataString({spanExpr}, esc{uid}, out int ew{uid}))");
        w.OpenBrace();
        w.WriteLine($"writer.Write(esc{uid}[..ew{uid}]);");
        w.CloseBrace();
    }

    /// <summary>
    /// Emits URI escaping and writing with a <c>totalWritten</c> counter update.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="spanExpr">The C# expression for the UTF-8 span.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitUriEscapeAndWriteCounted(
        IndentedWriter w,
        string spanExpr,
        string uid)
    {
        w.WriteLine($"Span<byte> esc{uid} = stackalloc byte[{spanExpr}.Length * 3];");
        w.WriteLine(
            $"if (Utf8Uri.TryEscapeDataString({spanExpr}, esc{uid}, out int ew{uid}))");
        w.OpenBrace();
        w.WriteLine($"writer.Write(esc{uid}[..ew{uid}]);");
        w.WriteLine($"totalWritten += ew{uid};");
        w.CloseBrace();
    }

    /// <summary>
    /// Emits a JSON writer fallback for object/array serialization.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitJsonWriterFallback(
        IndentedWriter w,
        string valueExpr,
        string uid)
    {
        w.WriteLine($"using Utf8JsonWriter jw{uid} = new(writer);");
        w.WriteLine($"((JsonElement){valueExpr}).WriteTo(jw{uid});");
        w.WriteLine($"jw{uid}.Flush();");
    }

    /// <summary>
    /// Emits a JSON writer fallback with a <c>totalWritten</c> counter update.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitJsonWriterFallbackCounted(
        IndentedWriter w,
        string valueExpr,
        string uid)
    {
        w.WriteLine($"using Utf8JsonWriter jw{uid} = new(writer);");
        w.WriteLine($"((JsonElement){valueExpr}).WriteTo(jw{uid});");
        w.WriteLine($"jw{uid}.Flush();");
        w.WriteLine($"totalWritten += (int)jw{uid}.BytesCommitted;");
    }

    // ── Compound parameter serialization per location ───────────────────

    /// <summary>
    /// Emits path parameter serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name (used for matrix style).</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitPathParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode)
    {
        if (kind == ParameterSerializationKind.Array)
        {
            EmitPathArrayWrite(w, paramName, valueExpr, uid, style, explode);
            return;
        }

        if (kind == ParameterSerializationKind.Object)
        {
            EmitPathObjectWrite(w, paramName, valueExpr, uid, style, explode);
            return;
        }

        // For scalar types, emit the style prefix, then the value.
        EmitPathStylePrefix(w, paramName, style);
        EmitPathScalarValue(w, valueExpr, uid, kind);
    }

    /// <summary>
    /// Emits query parameter serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name as it appears in the query string.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitQueryParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode)
    {
        if (kind == ParameterSerializationKind.Array)
        {
            EmitQueryArrayWrite(w, paramName, valueExpr, uid, style, explode);
            return;
        }

        if (kind == ParameterSerializationKind.Object)
        {
            EmitQueryObjectWrite(w, paramName, valueExpr, uid, style, explode);
            return;
        }

        // For scalar types, form style: name=value with & separator.
        w.WriteLine("if (!first)");
        w.OpenBrace();
        w.WriteLine("writer.Write(\"&\"u8);");
        w.WriteLine("totalWritten++;");
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
        w.WriteLine($"totalWritten += {paramName.Length + 1};");

        EmitQueryScalarWrite(w, valueExpr, uid, kind);

        w.WriteLine();
        w.WriteLine("first = false;");
    }

    /// <summary>
    /// Emits header parameter serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style (always Simple for headers).</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitHeaderParamWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode)
    {
        if (kind == ParameterSerializationKind.Array)
        {
            EmitHeaderArrayWrite(w, valueExpr, uid, explode);
            return;
        }

        if (kind == ParameterSerializationKind.Object)
        {
            EmitHeaderObjectWrite(w, valueExpr, uid, explode);
            return;
        }

        // For scalar types, headers always use simple style.
        EmitHeaderScalarWrite(w, valueExpr, uid, kind);
    }

    /// <summary>
    /// Emits cookie parameter serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name as it appears in the cookie.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style (always Form for cookies).</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitCookieParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode)
    {
        if (kind == ParameterSerializationKind.Array)
        {
            EmitCookieArrayWrite(w, paramName, valueExpr, uid, explode);
            return;
        }

        if (kind == ParameterSerializationKind.Object)
        {
            EmitCookieObjectWrite(w, paramName, valueExpr, uid, explode);
            return;
        }

        // For scalar types, cookies always use form style: name=value with "; " separator.
        w.WriteLine("if (!first)");
        w.OpenBrace();
        w.WriteLine("writer.Write(\"; \"u8);");
        w.WriteLine("totalWritten += 2;");
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
        w.WriteLine($"totalWritten += {paramName.Length + 1};");

        EmitCookieScalarWrite(w, valueExpr, uid, kind);

        w.WriteLine();
        w.WriteLine("first = false;");
    }

    /// <summary>
    /// Emits query parameter scalar serialization (form style).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitQueryScalarWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
        switch (kind)
        {
            case ParameterSerializationKind.Boolean:
                EmitBooleanWriteCounted(w, valueExpr);
                break;

            case ParameterSerializationKind.Byte:
            case ParameterSerializationKind.UInt16:
            case ParameterSerializationKind.UInt32:
            case ParameterSerializationKind.UInt64:
            case ParameterSerializationKind.UInt128:
            case ParameterSerializationKind.SByte:
            case ParameterSerializationKind.Int16:
            case ParameterSerializationKind.Int32:
            case ParameterSerializationKind.Int64:
            case ParameterSerializationKind.Int128:
            case ParameterSerializationKind.Half:
            case ParameterSerializationKind.Single:
            case ParameterSerializationKind.Double:
            case ParameterSerializationKind.Decimal:
                EmitTryFormatToLocal(w, valueExpr, uid, TryFormatBufferSize(kind));
                w.WriteLine($"writer.Write(buf{uid}[..bw{uid}]);");
                w.WriteLine($"totalWritten += bw{uid};");
                break;

            case ParameterSerializationKind.UnboundedNumber:
                EmitUnboundedNumberToLocal(w, valueExpr, uid);
                w.WriteLine($"writer.Write(raw{uid}.Span);");
                w.WriteLine($"totalWritten += raw{uid}.Span.Length;");
                break;

            case ParameterSerializationKind.String:
                EmitStringWrite(w, valueExpr, uid);
                EmitUriEscapeAndWriteCounted(w, $"utf8{uid}.Span", uid);
                break;

            case ParameterSerializationKind.Object:
            case ParameterSerializationKind.Array:
                EmitJsonWriterFallbackCounted(w, valueExpr, uid);
                break;
        }
    }

    /// <summary>
    /// Emits header parameter scalar serialization (simple style, no escaping).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitHeaderScalarWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
        switch (kind)
        {
            case ParameterSerializationKind.String:
                EmitStringWrite(w, valueExpr, uid);
                w.WriteLine(
                    $"callback(nameUtf8{uid}, utf8{uid}.Span, state);");
                break;

            case ParameterSerializationKind.Boolean:
                EmitBooleanHeaderWrite(w, valueExpr, uid);
                break;

            case ParameterSerializationKind.Byte:
            case ParameterSerializationKind.UInt16:
            case ParameterSerializationKind.UInt32:
            case ParameterSerializationKind.UInt64:
            case ParameterSerializationKind.UInt128:
            case ParameterSerializationKind.SByte:
            case ParameterSerializationKind.Int16:
            case ParameterSerializationKind.Int32:
            case ParameterSerializationKind.Int64:
            case ParameterSerializationKind.Int128:
            case ParameterSerializationKind.Half:
            case ParameterSerializationKind.Single:
            case ParameterSerializationKind.Double:
            case ParameterSerializationKind.Decimal:
                EmitTryFormatToLocal(w, valueExpr, uid, TryFormatBufferSize(kind));
                w.WriteLine(
                    $"callback(nameUtf8{uid}, buf{uid}[..bw{uid}], state);");
                break;

            case ParameterSerializationKind.UnboundedNumber:
                EmitUnboundedNumberToLocal(w, valueExpr, uid);
                w.WriteLine(
                    $"callback(nameUtf8{uid}, raw{uid}.Span, state);");
                break;

            case ParameterSerializationKind.Object:
            case ParameterSerializationKind.Array:
                EmitJsonWriterFallback(w, valueExpr, uid);
                break;
        }
    }

    /// <summary>
    /// Emits cookie parameter scalar serialization (form style, no URI escaping).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitCookieScalarWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
        switch (kind)
        {
            case ParameterSerializationKind.Boolean:
                EmitBooleanWriteCounted(w, valueExpr);
                break;

            case ParameterSerializationKind.Byte:
            case ParameterSerializationKind.UInt16:
            case ParameterSerializationKind.UInt32:
            case ParameterSerializationKind.UInt64:
            case ParameterSerializationKind.UInt128:
            case ParameterSerializationKind.SByte:
            case ParameterSerializationKind.Int16:
            case ParameterSerializationKind.Int32:
            case ParameterSerializationKind.Int64:
            case ParameterSerializationKind.Int128:
            case ParameterSerializationKind.Half:
            case ParameterSerializationKind.Single:
            case ParameterSerializationKind.Double:
            case ParameterSerializationKind.Decimal:
                EmitTryFormatToLocal(w, valueExpr, uid, TryFormatBufferSize(kind));
                w.WriteLine($"writer.Write(buf{uid}[..bw{uid}]);");
                w.WriteLine($"totalWritten += bw{uid};");
                break;

            case ParameterSerializationKind.UnboundedNumber:
                EmitUnboundedNumberToLocal(w, valueExpr, uid);
                w.WriteLine($"writer.Write(raw{uid}.Span);");
                w.WriteLine($"totalWritten += raw{uid}.Span.Length;");
                break;

            case ParameterSerializationKind.String:
                EmitStringWrite(w, valueExpr, uid);
                w.WriteLine($"writer.Write(utf8{uid}.Span);");
                w.WriteLine($"totalWritten += utf8{uid}.Span.Length;");
                break;

            case ParameterSerializationKind.Object:
            case ParameterSerializationKind.Array:
                EmitJsonWriterFallbackCounted(w, valueExpr, uid);
                break;
        }
    }

    // ── Style-aware path parameter helpers ───────────────────────────────

    /// <summary>
    /// Emits the prefix for a path parameter based on style.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="style">The parameter style.</param>
    public static void EmitPathStylePrefix(
        IndentedWriter w,
        string paramName,
        ParameterStyle style)
    {
        switch (style)
        {
            case ParameterStyle.Label:
                w.WriteLine("writer.Write(\".\"u8);");
                break;

            case ParameterStyle.Matrix:
                w.WriteLine($"writer.Write(\";{paramName}=\"u8);");
                break;

            default:
                break;
        }
    }

    /// <summary>
    /// Emits a single scalar value for path parameters (no prefix, no counting).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitPathScalarValue(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
        switch (kind)
        {
            case ParameterSerializationKind.String:
                EmitStringWrite(w, valueExpr, uid);
                EmitUriEscapeAndWrite(w, $"utf8{uid}.Span", uid);
                break;

            case ParameterSerializationKind.Boolean:
                EmitBooleanWrite(w, valueExpr);
                break;

            case ParameterSerializationKind.Byte:
            case ParameterSerializationKind.UInt16:
            case ParameterSerializationKind.UInt32:
            case ParameterSerializationKind.UInt64:
            case ParameterSerializationKind.UInt128:
            case ParameterSerializationKind.SByte:
            case ParameterSerializationKind.Int16:
            case ParameterSerializationKind.Int32:
            case ParameterSerializationKind.Int64:
            case ParameterSerializationKind.Int128:
            case ParameterSerializationKind.Half:
            case ParameterSerializationKind.Single:
            case ParameterSerializationKind.Double:
            case ParameterSerializationKind.Decimal:
                EmitTryFormatToLocal(w, valueExpr, uid, TryFormatBufferSize(kind));
                w.WriteLine($"writer.Write(buf{uid}[..bw{uid}]);");
                break;

            case ParameterSerializationKind.UnboundedNumber:
                EmitUnboundedNumberToLocal(w, valueExpr, uid);
                w.WriteLine($"writer.Write(raw{uid}.Span);");
                break;
        }
    }

    /// <summary>
    /// Emits path array serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the array value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitPathArrayWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterStyle style,
        bool explode)
    {
        // Determine prefix and separator based on style + explode.
        // simple: prefix=none, sep=","
        // label+!explode: prefix=".", sep=","
        // label+explode: prefix=".", sep="."
        // matrix+!explode: prefix=";name=", sep=","
        // matrix+explode: prefix=";", sep=";name="  (each element gets ;name=value)
        string prefix = style switch
        {
            ParameterStyle.Label => ".",
            ParameterStyle.Matrix when !explode => $";{paramName}=",
            ParameterStyle.Matrix => ";",
            _ => string.Empty,
        };

        string separator = style switch
        {
            ParameterStyle.Label when explode => ".",
            ParameterStyle.Matrix when explode => $";{paramName}=",
            _ => ",",
        };

        if (prefix.Length > 0)
        {
            w.WriteLine($"writer.Write(\"{prefix}\"u8);");
        }

        // If matrix+explode, the first element needs "name=" prefix
        // (subsequent ones get it as part of the separator).
        if (style == ParameterStyle.Matrix && explode)
        {
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
        }

        w.WriteLine($"bool firstItem{uid} = true;");
        w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
        w.OpenBrace();
        w.WriteLine($"if (!firstItem{uid})");
        w.OpenBrace();
        w.WriteLine($"writer.Write(\"{separator}\"u8);");
        w.CloseBrace();
        w.WriteLine();
        EmitElementScalarWrite(w, $"item{uid}", uid);
        w.WriteLine($"firstItem{uid} = false;");
        w.CloseBrace();
    }

    /// <summary>
    /// Emits path object serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the object value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitPathObjectWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterStyle style,
        bool explode)
    {
        // For objects:
        // simple+!explode: key,value,key,value (sep=",", kvSep=",")
        // simple+explode: key=value,key=value (sep=",", kvSep="=")
        // label+!explode: .key,value,key,value (prefix=".", sep=",", kvSep=",")
        // label+explode: .key=value.key=value (prefix=".", sep=".", kvSep="=")
        // matrix+!explode: ;name=key,value,key,value (prefix=";name=", sep=",", kvSep=",")
        // matrix+explode: ;key=value;key=value (prefix=";", sep=";", kvSep="=")
        string prefix = style switch
        {
            ParameterStyle.Label => ".",
            ParameterStyle.Matrix when !explode => $";{paramName}=",
            ParameterStyle.Matrix => ";",
            _ => string.Empty,
        };

        string separator = style switch
        {
            ParameterStyle.Label when explode => ".",
            ParameterStyle.Matrix when explode => ";",
            _ => ",",
        };

        string kvSeparator = explode ? "=" : ",";

        if (prefix.Length > 0)
        {
            w.WriteLine($"writer.Write(\"{prefix}\"u8);");
        }

        w.WriteLine($"bool firstProp{uid} = true;");
        w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
        w.OpenBrace();
        w.WriteLine($"if (!firstProp{uid})");
        w.OpenBrace();
        w.WriteLine($"writer.Write(\"{separator}\"u8);");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine($"writer.Write(System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name));");
        w.WriteLine($"writer.Write(\"{kvSeparator}\"u8);");
        EmitElementScalarWrite(w, $"prop{uid}.Value", uid);
        w.WriteLine($"firstProp{uid} = false;");
        w.CloseBrace();
    }

    // ── Style-aware query parameter helpers ──────────────────────────────

    /// <summary>
    /// Emits query array serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the array value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitQueryArrayWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterStyle style,
        bool explode)
    {
        // form+!explode: name=val1,val2,val3
        // form+explode: name=val1&name=val2&name=val3
        // spaceDelimited+!explode: name=val1%20val2%20val3
        // pipeDelimited+!explode: name=val1|val2|val3 (pipe gets URI-encoded to %7C)
        if (style == ParameterStyle.Form && explode)
        {
            // Each element becomes a separate name=value pair.
            w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
            w.OpenBrace();
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"&\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");
            EmitElementScalarWriteCounted(w, $"item{uid}", uid);
            w.WriteLine("first = false;");
            w.CloseBrace();
        }
        else
        {
            // Non-exploded: name=val1<sep>val2<sep>val3
            string itemSep = style switch
            {
                ParameterStyle.SpaceDelimited => "%20",
                ParameterStyle.PipeDelimited => "%7C",
                _ => ",",
            };

            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"&\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");

            w.WriteLine($"bool firstItem{uid} = true;");
            w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
            w.OpenBrace();
            w.WriteLine($"if (!firstItem{uid})");
            w.OpenBrace();
            w.WriteLine($"writer.Write(\"{itemSep}\"u8);");
            w.WriteLine($"totalWritten += {itemSep.Length};");
            w.CloseBrace();
            w.WriteLine();
            EmitElementScalarWriteCounted(w, $"item{uid}", uid);
            w.WriteLine($"firstItem{uid} = false;");
            w.CloseBrace();

            w.WriteLine();
            w.WriteLine("first = false;");
        }
    }

    /// <summary>
    /// Emits query object serialization with style and explode support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the object value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitQueryObjectWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterStyle style,
        bool explode)
    {
        if (style == ParameterStyle.DeepObject)
        {
            // deepObject+explode: name[key1]=val1&name[key2]=val2
            // The brackets get percent-encoded: name%5Bkey1%5D=val1&name%5Bkey2%5D=val2
            w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
            w.OpenBrace();
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"&\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}%5B\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 3};");
            w.WriteLine($"byte[] keyBytes{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name);");
            w.WriteLine($"writer.Write(keyBytes{uid});");
            w.WriteLine($"totalWritten += keyBytes{uid}.Length;");
            w.WriteLine("writer.Write(\"%5D=\"u8);");
            w.WriteLine("totalWritten += 4;");
            EmitElementScalarWriteCounted(w, $"prop{uid}.Value", uid);
            w.WriteLine("first = false;");
            w.CloseBrace();
        }
        else if (explode)
        {
            // form+explode: key1=val1&key2=val2
            // spaceDelimited/pipeDelimited+explode: same as form+explode
            w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
            w.OpenBrace();
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"&\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"byte[] keyBytes{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name);");
            w.WriteLine($"writer.Write(keyBytes{uid});");
            w.WriteLine($"totalWritten += keyBytes{uid}.Length;");
            w.WriteLine("writer.Write(\"=\"u8);");
            w.WriteLine("totalWritten++;");
            EmitElementScalarWriteCounted(w, $"prop{uid}.Value", uid);
            w.WriteLine("first = false;");
            w.CloseBrace();
        }
        else
        {
            // form+!explode: name=key1,val1,key2,val2
            // spaceDelimited+!explode: name=key1%20val1%20key2%20val2
            // pipeDelimited+!explode: name=key1%7Cval1%7Ckey2%7Cval2
            string kvSep = style switch
            {
                ParameterStyle.SpaceDelimited => "%20",
                ParameterStyle.PipeDelimited => "%7C",
                _ => ",",
            };

            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"&\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");

            w.WriteLine($"bool firstProp{uid} = true;");
            w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
            w.OpenBrace();
            w.WriteLine($"if (!firstProp{uid})");
            w.OpenBrace();
            w.WriteLine($"writer.Write(\"{kvSep}\"u8);");
            w.WriteLine($"totalWritten += {kvSep.Length};");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"byte[] keyBytes{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name);");
            w.WriteLine($"writer.Write(keyBytes{uid});");
            w.WriteLine($"totalWritten += keyBytes{uid}.Length;");
            w.WriteLine($"writer.Write(\"{kvSep}\"u8);");
            w.WriteLine($"totalWritten += {kvSep.Length};");
            EmitElementScalarWriteCounted(w, $"prop{uid}.Value", uid);
            w.WriteLine($"firstProp{uid} = false;");
            w.CloseBrace();

            w.WriteLine();
            w.WriteLine("first = false;");
        }
    }

    // ── Style-aware header parameter helpers ─────────────────────────────

    /// <summary>
    /// Emits header array serialization (always simple style).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the array value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitHeaderArrayWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        bool explode)
    {
        // Headers always use simple style. simple+!explode = comma-separated, simple+explode = same.
        w.WriteLine("Span<byte> headerBuf = stackalloc byte[512];");
        w.WriteLine("int headerLen = 0;");
        w.WriteLine($"bool firstItem{uid} = true;");
        w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
        w.OpenBrace();
        w.WriteLine($"if (!firstItem{uid})");
        w.OpenBrace();
        w.WriteLine("headerBuf[headerLen++] = (byte)',';");
        w.CloseBrace();
        w.WriteLine();
        EmitElementToHeaderBuffer(w, $"item{uid}", uid);
        w.WriteLine($"firstItem{uid} = false;");
        w.CloseBrace();
        w.WriteLine($"callback(nameUtf8{uid}, headerBuf[..headerLen], state);");
    }

    /// <summary>
    /// Emits header object serialization (always simple style).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the object value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitHeaderObjectWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        bool explode)
    {
        // simple+!explode: key,value,key,value
        // simple+explode: key=value,key=value
        string kvSep = explode ? "=" : ",";

        w.WriteLine("Span<byte> headerBuf = stackalloc byte[512];");
        w.WriteLine("int headerLen = 0;");
        w.WriteLine($"bool firstProp{uid} = true;");
        w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
        w.OpenBrace();
        w.WriteLine($"if (!firstProp{uid})");
        w.OpenBrace();
        w.WriteLine("headerBuf[headerLen++] = (byte)',';");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine($"int keyLen{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name, headerBuf[headerLen..]);");
        w.WriteLine($"headerLen += keyLen{uid};");
        w.WriteLine($"headerBuf[headerLen++] = (byte)'{kvSep}';");
        EmitElementToHeaderBuffer(w, $"prop{uid}.Value", uid);
        w.WriteLine($"firstProp{uid} = false;");
        w.CloseBrace();
        w.WriteLine($"callback(nameUtf8{uid}, headerBuf[..headerLen], state);");
    }

    // ── Style-aware cookie parameter helpers ─────────────────────────────

    /// <summary>
    /// Emits cookie array serialization (always form style).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the array value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitCookieArrayWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        bool explode)
    {
        if (explode)
        {
            // form+explode: name=val1; name=val2; name=val3
            w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
            w.OpenBrace();
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"; \"u8);");
            w.WriteLine("totalWritten += 2;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");
            EmitElementScalarWriteCounted(w, $"item{uid}", uid);
            w.WriteLine("first = false;");
            w.CloseBrace();
        }
        else
        {
            // form+!explode: name=val1,val2,val3
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"; \"u8);");
            w.WriteLine("totalWritten += 2;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");

            w.WriteLine($"bool firstItem{uid} = true;");
            w.WriteLine($"foreach (var item{uid} in ((JsonElement){valueExpr}).EnumerateArray())");
            w.OpenBrace();
            w.WriteLine($"if (!firstItem{uid})");
            w.OpenBrace();
            w.WriteLine("writer.Write(\",\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            EmitElementScalarWriteCounted(w, $"item{uid}", uid);
            w.WriteLine($"firstItem{uid} = false;");
            w.CloseBrace();

            w.WriteLine();
            w.WriteLine("first = false;");
        }
    }

    /// <summary>
    /// Emits cookie object serialization (always form style).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name.</param>
    /// <param name="valueExpr">The C# expression for the object value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    public static void EmitCookieObjectWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        bool explode)
    {
        if (explode)
        {
            // form+explode: key1=val1; key2=val2
            w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
            w.OpenBrace();
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"; \"u8);");
            w.WriteLine("totalWritten += 2;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"byte[] keyBytes{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name);");
            w.WriteLine($"writer.Write(keyBytes{uid});");
            w.WriteLine($"totalWritten += keyBytes{uid}.Length;");
            w.WriteLine("writer.Write(\"=\"u8);");
            w.WriteLine("totalWritten++;");
            EmitElementScalarWriteCounted(w, $"prop{uid}.Value", uid);
            w.WriteLine("first = false;");
            w.CloseBrace();
        }
        else
        {
            // form+!explode: name=key1,val1,key2,val2
            w.WriteLine("if (!first)");
            w.OpenBrace();
            w.WriteLine("writer.Write(\"; \"u8);");
            w.WriteLine("totalWritten += 2;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
            w.WriteLine($"totalWritten += {paramName.Length + 1};");

            w.WriteLine($"bool firstProp{uid} = true;");
            w.WriteLine($"foreach (var prop{uid} in ((JsonElement){valueExpr}).EnumerateObject())");
            w.OpenBrace();
            w.WriteLine($"if (!firstProp{uid})");
            w.OpenBrace();
            w.WriteLine("writer.Write(\",\"u8);");
            w.WriteLine("totalWritten++;");
            w.CloseBrace();
            w.WriteLine();
            w.WriteLine($"byte[] keyBytes{uid} = System.Text.Encoding.UTF8.GetBytes(prop{uid}.Name);");
            w.WriteLine($"writer.Write(keyBytes{uid});");
            w.WriteLine($"totalWritten += keyBytes{uid}.Length;");
            w.WriteLine("writer.Write(\",\"u8);");
            w.WriteLine("totalWritten++;");
            EmitElementScalarWriteCounted(w, $"prop{uid}.Value", uid);
            w.WriteLine($"firstProp{uid} = false;");
            w.CloseBrace();

            w.WriteLine();
            w.WriteLine("first = false;");
        }
    }

    // ── Element-level serialization helpers ──────────────────────────────

    /// <summary>
    /// Emits code to write a JSON element's scalar value to a <c>writer</c> (path-style, no counting).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="itemExpr">The C# expression for the JSON element.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitElementScalarWrite(
        IndentedWriter w,
        string itemExpr,
        string uid)
    {
        // Write the element's raw value as UTF-8 text.
        w.WriteLine($"writer.Write(System.Text.Encoding.UTF8.GetBytes({itemExpr}.ToString()));");
    }

    /// <summary>
    /// Emits code to write a JSON element's scalar value to a <c>writer</c> with <c>totalWritten</c> tracking.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="itemExpr">The C# expression for the JSON element.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitElementScalarWriteCounted(
        IndentedWriter w,
        string itemExpr,
        string uid)
    {
        w.WriteLine($"byte[] elBytes{uid} = System.Text.Encoding.UTF8.GetBytes({itemExpr}.ToString());");
        w.WriteLine($"writer.Write(elBytes{uid});");
        w.WriteLine($"totalWritten += elBytes{uid}.Length;");
    }

    /// <summary>
    /// Emits code to write a JSON element's scalar value to a header buffer span.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="itemExpr">The C# expression for the JSON element.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    public static void EmitElementToHeaderBuffer(
        IndentedWriter w,
        string itemExpr,
        string uid)
    {
        w.WriteLine($"int elLen{uid} = System.Text.Encoding.UTF8.GetBytes({itemExpr}.ToString(), headerBuf[headerLen..]);");
        w.WriteLine($"headerLen += elLen{uid};");
    }

    /// <summary>
    /// Emits the body of an async response body parse into a typed element.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="typeName">The fully qualified .NET type name.</param>
    /// <param name="accessorName">The property name on the response struct.</param>
    public static void EmitParseResponseBody(
        IndentedWriter w,
        string typeName,
        string accessorName)
    {
        w.OpenBrace();
        w.WriteLine(
            $"var doc = await ParsedJsonDocument<{typeName}>" +
            ".ParseAsync(contentStream, default, cancellationToken).ConfigureAwait(false);");
        w.WriteLine("response.parsedDocument = doc;");
        w.WriteLine($"response.{accessorName}Body = doc.RootElement;");
        w.CloseBrace();
    }

    /// <summary>
    /// Emits a lazy response header property that parses the raw header string
    /// into a typed value on first access.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="propertyName">The PascalCase property name (e.g. "XRequestId").</param>
    /// <param name="fieldName">The camelCase field name (e.g. "xRequestId").</param>
    /// <param name="headerName">The raw HTTP header name (e.g. "X-Request-Id").</param>
    /// <param name="typeName">The fully qualified .NET type name for the header value.</param>
    /// <param name="explode">Whether the header uses explode serialization.</param>
    /// <param name="kind">The serialization kind of the header schema.</param>
    public static void EmitResponseHeaderLazyProperty(
        IndentedWriter w,
        string propertyName,
        string fieldName,
        string headerName,
        string typeName,
        bool explode,
        ParameterSerializationKind kind,
        bool hasDeepNesting = false)
    {
        // Backing fields: cached parsed value and a flag to avoid re-parsing.
        w.WriteLine($"private {typeName}? {fieldName}HeaderValue;");
        w.WriteLine($"private bool {fieldName}HeaderParsed;");
        w.WriteLine();

        // Property getter: lazily parse the raw header string on first access.
        w.WriteLine($"public {typeName}? {propertyName}Header");
        w.OpenBrace();
        w.WriteLine("get");
        w.OpenBrace();

        // If already parsed, return cached value.
        w.WriteLine($"if (this.{fieldName}HeaderParsed)");
        w.OpenBrace();
        w.WriteLine($"return this.{fieldName}HeaderValue;");
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine($"this.{fieldName}HeaderParsed = true;");
        w.WriteLine();

        // Try to get the raw header string.
        w.WriteLine($"if (this.responseHeaders is not null");
        w.WriteLine($"    && this.responseHeaders.TryGetValue(\"{headerName}\", out string? rawValue)");
        w.WriteLine("    && rawValue is not null)");
        w.OpenBrace();

        // Parse based on serialization kind.
        // Headers always use style: simple.
        // Scalar: parse the raw string directly.
        // Array: split on comma, parse each element.
        // Object: split on comma, interpret as key,value pairs (or key=value with explode).
        if (kind is ParameterSerializationKind.Array)
        {
            // TODO: Pass the actual element schema kind when available.
            EmitResponseHeaderArrayParse(w, fieldName, typeName, explode, ParameterSerializationKind.String, hasDeepNesting);
        }
        else if (kind is ParameterSerializationKind.Object)
        {
            // TODO: Pass the actual value schema kind when available.
            EmitResponseHeaderObjectParse(w, fieldName, typeName, explode, ParameterSerializationKind.String, hasDeepNesting);
        }
        else
        {
            // Scalar: wrap the raw string in a JSON value and parse it.
            EmitResponseHeaderScalarParse(w, fieldName, typeName, kind);
        }

        w.CloseBrace();
        w.WriteLine();

        w.WriteLine($"return this.{fieldName}HeaderValue;");
        w.CloseBrace();
        w.CloseBrace();
    }

    private static void EmitResponseHeaderScalarParse(
        IndentedWriter w,
        string fieldName,
        string typeName,
        ParameterSerializationKind kind)
    {
        if (kind is ParameterSerializationKind.String)
        {
            // String: use FixedJsonValueDocument via HeaderValueParser (zero-alloc, handles escaping).
            w.WriteLine($"this.{fieldName}HeaderValue = Corvus.Text.Json.OpenApi.HeaderValueParser.ParseString<{typeName}>(rawValue, this.workspace);");
        }
        else if (IsNumericKind(kind))
        {
            // Numeric: use FixedJsonValueDocument via HeaderValueParser (zero-alloc).
            w.WriteLine($"this.{fieldName}HeaderValue = Corvus.Text.Json.OpenApi.HeaderValueParser.ParseNumber<{typeName}>(rawValue, this.workspace);");
        }
        else
        {
            // Boolean and other non-numeric/non-string: use CreateBuilder with Source implicit conversion.
            string sourceExpr = GetScalarSourceExpression(kind);
            w.WriteLine($"this.{fieldName}HeaderValue = {typeName}.CreateBuilder(this.workspace, {sourceExpr}).RootElement;");
        }
    }

    private static bool IsNumericKind(ParameterSerializationKind kind)
    {
        return kind is ParameterSerializationKind.Byte
            or ParameterSerializationKind.UInt16
            or ParameterSerializationKind.UInt32
            or ParameterSerializationKind.UInt64
            or ParameterSerializationKind.UInt128
            or ParameterSerializationKind.SByte
            or ParameterSerializationKind.Int16
            or ParameterSerializationKind.Int32
            or ParameterSerializationKind.Int64
            or ParameterSerializationKind.Int128
            or ParameterSerializationKind.Half
            or ParameterSerializationKind.Single
            or ParameterSerializationKind.Double
            or ParameterSerializationKind.Decimal
            or ParameterSerializationKind.UnboundedNumber;
    }

    private static string GetScalarSourceExpression(ParameterSerializationKind kind)
    {
        return kind switch
        {
            ParameterSerializationKind.Boolean => "bool.Parse(rawValue)",
            _ => "rawValue",
        };
    }

    private static void EmitResponseHeaderArrayParse(
        IndentedWriter w,
        string fieldName,
        string typeName,
        bool explode,
        ParameterSerializationKind elementKind,
        bool hasDeepNesting)
    {
        // style: simple arrays are comma-separated (explode has no effect on separator for simple).
        // Use CreateBuilder (empty) + RootElement.AddItem in a loop.
        _ = explode;
        string elementSourceExpr = GetElementSourceExpression(elementKind, "element");

        if (hasDeepNesting)
        {
            w.WriteLine("#warning Deep nesting detected in array header schema. Nested object/array values are treated as JSON-encoded strings.");
        }

        // When hasDeepNesting is true, elements may contain JSON-encoded values with
        // commas inside braces/brackets/strings. Use StyleValueSplitter.NextSeparator
        // to split only at depth-zero commas.
        string separatorExpr = hasDeepNesting
            ? "Corvus.Text.Json.OpenApi.StyleValueSplitter.NextSeparator(remaining)"
            : "remaining.IndexOf(',')";

        // Build an empty array, then add items via the Mutable root element.
        // The builder is not disposed — the workspace owns the document lifetime.
        w.OpenBrace();
        w.WriteLine($"var builder = {typeName}.CreateBuilder(this.workspace);");
        w.WriteLine($"{typeName}.Mutable root = builder.RootElement;");
        w.WriteLine("System.ReadOnlySpan<char> remaining = rawValue;");
        w.WriteLine("while (!remaining.IsEmpty)");
        w.OpenBrace();
        w.WriteLine($"int commaIdx = {separatorExpr};");
        w.WriteLine("System.ReadOnlySpan<char> element = commaIdx >= 0 ? remaining.Slice(0, commaIdx).Trim() : remaining.Trim();");
        w.WriteLine($"root.AddItem({elementSourceExpr});");
        w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
        w.CloseBrace();
        w.WriteLine($"this.{fieldName}HeaderValue = root;");
        w.CloseBrace();
    }

    private static void EmitResponseHeaderObjectParse(
        IndentedWriter w,
        string fieldName,
        string typeName,
        bool explode,
        ParameterSerializationKind valueKind,
        bool hasDeepNesting)
    {
        // style: simple objects:
        // explode=false: comma-separated alternating key,value: "R,100,G,200,B,150"
        // explode=true: comma-separated key=value: "R=100,G=200,B=150"
        // Use CreateBuilder<TContext> with rawValue as context — static lambda, zero captures.
        string valueSourceExpr = GetElementSourceExpression(valueKind, "value");

        if (hasDeepNesting)
        {
            w.WriteLine("#warning Deep nesting detected in object header schema. Nested object/array values are treated as JSON-encoded strings.");
        }

        // When hasDeepNesting is true, values may contain JSON-encoded content with
        // commas inside braces/brackets/strings. Use StyleValueSplitter.NextSeparator
        // to split only at depth-zero commas.
        string separatorExpr = hasDeepNesting
            ? "Corvus.Text.Json.OpenApi.StyleValueSplitter.NextSeparator(remaining)"
            : "remaining.IndexOf(',')";

        w.Write($"this.{fieldName}HeaderValue = {typeName}.CreateBuilder<string>(this.workspace, rawValue, static (in string ctx, ref {typeName}.Builder objectBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("System.ReadOnlySpan<char> remaining = ctx;");

        if (explode)
        {
            w.WriteLine("while (!remaining.IsEmpty)");
            w.OpenBrace();
            w.WriteLine($"int commaIdx = {separatorExpr};");
            w.WriteLine("System.ReadOnlySpan<char> pair = commaIdx >= 0 ? remaining.Slice(0, commaIdx) : remaining;");
            w.WriteLine("int eqIdx = pair.IndexOf('=');");
            w.WriteLine("if (eqIdx >= 0)");
            w.OpenBrace();
            w.WriteLine("System.ReadOnlySpan<char> key = pair.Slice(0, eqIdx).Trim();");
            w.WriteLine("System.ReadOnlySpan<char> value = pair.Slice(eqIdx + 1).Trim();");
            w.WriteLine($"objectBuilder.AddProperty(key, {valueSourceExpr});");
            w.CloseBrace();
            w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
            w.CloseBrace();
        }
        else
        {
            // For non-explode with deep nesting, keys are always simple strings so the first
            // comma is always a depth-zero separator. But the value may be JSON-encoded, so
            // the second comma must use depth-aware splitting.
            w.WriteLine("while (!remaining.IsEmpty)");
            w.OpenBrace();

            // Key separator is always simple (keys are simple strings).
            w.WriteLine("int commaIdx = remaining.IndexOf(',');");
            w.WriteLine("System.ReadOnlySpan<char> key = commaIdx >= 0 ? remaining.Slice(0, commaIdx).Trim() : remaining.Trim();");
            w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
            w.WriteLine("if (remaining.IsEmpty) break;");

            // Value separator uses depth-aware splitting when deep nesting is present.
            w.WriteLine($"commaIdx = {separatorExpr};");
            w.WriteLine("System.ReadOnlySpan<char> value = commaIdx >= 0 ? remaining.Slice(0, commaIdx).Trim() : remaining.Trim();");
            w.WriteLine($"objectBuilder.AddProperty(key, {valueSourceExpr});");
            w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
            w.CloseBrace();
        }

        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    private static string GetElementSourceExpression(ParameterSerializationKind kind, string varName)
    {
        return kind switch
        {
            ParameterSerializationKind.String => varName,
            ParameterSerializationKind.Boolean => $"bool.Parse({varName})",
            ParameterSerializationKind.Int32 => $"int.Parse({varName})",
            ParameterSerializationKind.Int64 => $"long.Parse({varName})",
            ParameterSerializationKind.Single => $"float.Parse({varName})",
            ParameterSerializationKind.Double => $"double.Parse({varName})",
            _ => varName,
        };
    }

    /// <summary>
    /// Emits the <c>SendAsyncCore</c> and <c>SendWithBodyAsyncCore</c> helper methods.
    /// </summary>
    /// <param name="w">The writer.</param>
    public static void EmitSendAsyncCoreHelpers(IndentedWriter w)
    {
        w.WriteLine("private async ValueTask<TResponse> SendAsyncCore<TRequest, TResponse>(");
        w.PushIndent();
        w.WriteLine("JsonWorkspace workspace,");
        w.WriteLine("TRequest request,");
        w.WriteLine("CancellationToken cancellationToken)");
        w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
        w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
        w.PopIndent();
        w.OpenBrace();
        w.WriteLine("try");
        w.OpenBrace();
        w.WriteLine(
            "return await this.transport.SendAsync<TRequest, TResponse>(in request, cancellationToken).ConfigureAwait(false);");
        w.CloseBrace();
        w.WriteLine("finally");
        w.OpenBrace();
        w.WriteLine("workspace.Dispose();");
        w.CloseBrace();
        w.CloseBrace();

        w.WriteLine();

        w.WriteLine("private async ValueTask<TResponse> SendWithBodyAsyncCore<TRequest, TBody, TResponse>(");
        w.PushIndent();
        w.WriteLine("JsonWorkspace workspace,");
        w.WriteLine("TRequest request,");
        w.WriteLine("TBody body,");
        w.WriteLine("CancellationToken cancellationToken)");
        w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
        w.WriteLine("where TBody : struct, IJsonElement<TBody>");
        w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
        w.PopIndent();
        w.OpenBrace();
        w.WriteLine("try");
        w.OpenBrace();
        w.WriteLine(
            "return await this.transport.SendAsync<TRequest, TBody, TResponse>(in request, in body, cancellationToken).ConfigureAwait(false);");
        w.CloseBrace();
        w.WriteLine("finally");
        w.OpenBrace();
        w.WriteLine("workspace.Dispose();");
        w.CloseBrace();
        w.CloseBrace();
    }
}