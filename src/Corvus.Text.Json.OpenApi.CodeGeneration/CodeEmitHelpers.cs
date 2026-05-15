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
    /// Emits path parameter serialization (simple style, no explode).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitPathParamWrite(
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

            case ParameterSerializationKind.Object:
            case ParameterSerializationKind.Array:
                EmitJsonWriterFallback(w, valueExpr, uid);
                break;
        }
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

    /// <summary>
    /// Emits a query parameter write with the <c>name=</c> prefix and separator handling.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name as it appears in the query string.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitQueryParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
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
    /// Emits a cookie parameter write with the <c>name=</c> prefix and separator handling.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name as it appears in the cookie.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    public static void EmitCookieParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind)
    {
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