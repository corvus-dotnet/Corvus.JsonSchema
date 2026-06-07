// <copyright file="CodeEmitHelpers.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics;
using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Microsoft.CodeAnalysis.CSharp;

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
    /// Formats a C# string literal (including surrounding quotes), using the Roslyn
    /// <see cref="SymbolDisplay.FormatLiteral(string, bool)"/> method.
    /// </summary>
    /// <param name="text">The text to include in the literal.</param>
    /// <returns>A quoted and escaped C# string literal (e.g. <c>"hello\"world"</c>).</returns>
    public static string FormatStringLiteral(string text) =>
        SymbolDisplay.FormatLiteral(text, true);

    /// <summary>
    /// Formats a C# expression for a JSON Schema default value, using the
    /// same factory methods as the core JSON Schema code generator
    /// (<c>ParsedJsonDocument&lt;T&gt;.Null</c>, <c>.True</c>, <c>.False</c>,
    /// <c>.NumberConstant</c>, <c>.StringConstant</c>).
    /// </summary>
    /// <param name="typeName">The resolved C# type name (e.g. <c>Schema.Limit</c>).</param>
    /// <param name="rawJson">
    /// The raw JSON text of the default value (e.g. <c>100</c>, <c>"foo"</c>),
    /// or <see langword="null"/> if no default exists.
    /// </param>
    /// <param name="valueKind">
    /// The <see cref="JsonValueKind"/> of the default value, or
    /// <see cref="JsonValueKind.Undefined"/> if no default exists.
    /// </param>
    /// <returns>
    /// A C# expression string. Returns <c>"default"</c> when no default is available.
    /// </returns>
    public static string FormatDefaultValueExpression(
        string typeName, string? rawJson, JsonValueKind valueKind)
    {
        if (rawJson is null)
        {
            return "default";
        }

        string escaped = SymbolDisplay.FormatLiteral(rawJson, true);

        return valueKind switch
        {
            JsonValueKind.Null => $"ParsedJsonDocument<{typeName}>.Null",
            JsonValueKind.True => $"ParsedJsonDocument<{typeName}>.True",
            JsonValueKind.False => $"ParsedJsonDocument<{typeName}>.False",
            JsonValueKind.Number => $"ParsedJsonDocument<{typeName}>.NumberConstant([..{escaped}u8])",
            JsonValueKind.String => $"ParsedJsonDocument<{typeName}>.StringConstant([..{escaped}u8])",
            _ => "default", // Object/Array defaults are uncommon for parameters; fall back.
        };
    }

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
            OperationMethod.Query => "OperationMethod.Query",
            OperationMethod.Custom => "OperationMethod.Custom",
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
        w.WriteLine("using System.Diagnostics.CodeAnalysis;");
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
    /// Gets the name of the generated property that holds a response's typed JSON body for a status
    /// code (e.g. <c>200</c> → <c>OkBody</c>). The single source of truth for that convention, used
    /// both when emitting the response struct and when describing it to downstream generators.
    /// </summary>
    /// <param name="statusCode">The response status code, or <c>default</c>.</param>
    /// <returns>The generated body property name.</returns>
    public static string ResponseBodyPropertyName(string statusCode) => $"{StatusCodeToName(statusCode)}Body";

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
        EmitPathParamWrite(w, paramName, valueExpr, uid, kind, style, explode, allowReserved: false);
    }

    /// <summary>
    /// Emits path parameter serialization with style, explode, and allowReserved support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name (used for matrix style).</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    /// <param name="allowReserved">When <see langword="true"/>, reserved characters are not percent-encoded.</param>
    public static void EmitPathParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode,
        bool allowReserved)
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
        EmitPathScalarValue(w, valueExpr, uid, kind, allowReserved);
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
        EmitCookieParamWrite(w, paramName, valueExpr, uid, kind, style, explode, allowReserved: style == ParameterStyle.Cookie);
    }

    /// <summary>
    /// Emits cookie parameter serialization with style, explode, and allowReserved support.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="paramName">The parameter name as it appears in the cookie.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="style">The parameter style.</param>
    /// <param name="explode">Whether to use exploded serialization.</param>
    /// <param name="allowReserved">When <see langword="true"/>, reserved characters in strings are not percent-encoded.</param>
    public static void EmitCookieParamWrite(
        IndentedWriter w,
        string paramName,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        ParameterStyle style,
        bool explode,
        bool allowReserved)
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

        // For scalar types, cookies use name=value with "; " separator.
        w.WriteLine("if (!first)");
        w.OpenBrace();
        w.WriteLine("writer.Write(\"; \"u8);");
        w.WriteLine("totalWritten += 2;");
        w.CloseBrace();
        w.WriteLine();

        w.WriteLine($"writer.Write(\"{paramName}=\"u8);");
        w.WriteLine($"totalWritten += {paramName.Length + 1};");

        EmitCookieScalarWrite(w, valueExpr, uid, kind, allowReserved);

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
        EmitCookieScalarWrite(w, valueExpr, uid, kind, allowReserved: true);
    }

    /// <summary>
    /// Emits cookie parameter scalar serialization with optional percent-encoding.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="allowReserved">When <see langword="true"/>, string values are not percent-encoded.</param>
    public static void EmitCookieScalarWrite(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        bool allowReserved)
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
                if (allowReserved)
                {
                    w.WriteLine($"writer.Write(utf8{uid}.Span);");
                    w.WriteLine($"totalWritten += utf8{uid}.Span.Length;");
                }
                else
                {
                    EmitUriEscapeAndWriteCounted(w, $"utf8{uid}.Span", uid);
                }

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
        EmitPathScalarValue(w, valueExpr, uid, kind, allowReserved: false);
    }

    /// <summary>
    /// Emits a single scalar value for path parameters with optional allowReserved behavior.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="valueExpr">The C# expression for the value.</param>
    /// <param name="uid">A unique identifier for variable naming.</param>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="allowReserved">When <see langword="true"/>, reserved characters in strings are not percent-encoded.</param>
    public static void EmitPathScalarValue(
        IndentedWriter w,
        string valueExpr,
        string uid,
        ParameterSerializationKind kind,
        bool allowReserved)
    {
        switch (kind)
        {
            case ParameterSerializationKind.String:
                EmitStringWrite(w, valueExpr, uid);
                if (allowReserved)
                {
                    // When allowReserved is true, write the UTF-8 value directly without percent-encoding.
                    w.WriteLine($"writer.Write(utf8{uid}.Span);");
                }
                else
                {
                    EmitUriEscapeAndWrite(w, $"utf8{uid}.Span", uid);
                }

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
    /// <param name="accessorName">The property name on the response struct (e.g. "Ok", "Default").</param>
    public static void EmitParseResponseBody(
        IndentedWriter w,
        string typeName,
        string accessorName)
    {
        string docVar = $"{char.ToLowerInvariant(accessorName[0])}{accessorName.Substring(1)}Doc";
        w.WriteLine(
            $"var {docVar} = await ParsedJsonDocument<{typeName}>" +
            ".ParseAsync(contentStream, default, cancellationToken).ConfigureAwait(false);");
        w.WriteLine($"response.parsedDocument = {docVar};");
        w.WriteLine($"response.{accessorName}Body = {docVar}.RootElement;");
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
        ParameterSerializationKind elementKind,
        bool hasDeepNesting = false,
        string? elementTypeName = null)
    {
        // Backing fields: cached parsed value and a flag to avoid re-parsing.
        // The value is non-nullable; when the header is absent, it remains default
        // and consumers check via IsUndefined().
        w.WriteLine($"private {typeName} {fieldName}HeaderValue;");
        w.WriteLine($"private bool {fieldName}HeaderParsed;");
        w.WriteLine();

        // Property getter: lazily parse the raw header string on first access.
        // Returns default (undefined) when the header is not present.
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Gets the parsed value of the <c>{headerName}</c> response header,");
        w.WriteLine("/// or <see langword=\"default\"/> (undefined) if not present.");
        w.WriteLine("/// Use <c>IsUndefined()</c> to check for absence.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public {typeName} {propertyName}Header");
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
            EmitResponseHeaderArrayParse(w, fieldName, typeName, explode, elementKind, hasDeepNesting, elementTypeName);
        }
        else if (kind is ParameterSerializationKind.Object)
        {
            EmitResponseHeaderObjectParse(w, fieldName, typeName, explode, elementKind, hasDeepNesting, elementTypeName);
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
            // Boolean: use CreateBuilder with Source implicit conversion.
            // Only Boolean reaches this branch — String and all numeric kinds are handled above.
            Debug.Assert(kind is ParameterSerializationKind.Boolean, "Only Boolean should reach this branch.");
            w.WriteLine($"this.{fieldName}HeaderValue = {typeName}.CreateBuilder(this.workspace, bool.Parse(rawValue)).RootElement;");
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

    private static void EmitResponseHeaderArrayParse(
        IndentedWriter w,
        string fieldName,
        string typeName,
        bool explode,
        ParameterSerializationKind elementKind,
        bool hasDeepNesting,
        string? elementTypeName = null)
    {
        // style: simple arrays are comma-separated (explode has no effect on separator for simple).
        // Use CreateBuilder<TContext> with rawValue as context — static lambda, zero captures.
        _ = explode;

        // When hasDeepNesting is true, elements may contain JSON-encoded values with
        // commas inside braces/brackets/strings. Use StyleValueSplitter.NextSeparator
        // to split only at depth-zero commas.
        string separatorExpr = hasDeepNesting
            ? "Corvus.Text.Json.OpenApi.StyleValueSplitter.NextSeparator(remaining)"
            : "remaining.IndexOf(',')";

        w.Write($"this.{fieldName}HeaderValue = {typeName}.CreateBuilder<string>(this.workspace, rawValue, static (in string ctx, ref {typeName}.Builder arrayBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("System.ReadOnlySpan<char> remaining = ctx;");
        w.WriteLine("while (!remaining.IsEmpty)");
        w.OpenBrace();
        w.WriteLine($"int commaIdx = {separatorExpr};");
        w.WriteLine("System.ReadOnlySpan<char> element = commaIdx >= 0 ? remaining.Slice(0, commaIdx).Trim() : remaining.Trim();");

        if (hasDeepNesting && elementTypeName is not null)
        {
            // Elements are JSON-encoded objects/arrays — parse directly into the workspace.
            w.WriteLine("arrayBuilder._builder.AddItemFromJson(element);");
        }
        else
        {
            string elementSourceExpr = GetElementSourceExpression(elementKind, "element");
            w.WriteLine($"arrayBuilder.AddItem({elementSourceExpr});");
        }

        w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
        w.CloseBrace();
        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    private static void EmitResponseHeaderObjectParse(
        IndentedWriter w,
        string fieldName,
        string typeName,
        bool explode,
        ParameterSerializationKind valueKind,
        bool hasDeepNesting,
        string? valueTypeName = null)
    {
        // style: simple objects:
        // explode=false: comma-separated alternating key,value: "R,100,G,200,B,150"
        // explode=true: comma-separated key=value: "R=100,G=200,B=150"
        // Use CreateBuilder<TContext> with rawValue as context — static lambda, zero captures.

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
            EmitObjectAddProperty(w, hasDeepNesting, valueKind, valueTypeName);
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
            EmitObjectAddProperty(w, hasDeepNesting, valueKind, valueTypeName);
            w.WriteLine("remaining = commaIdx >= 0 ? remaining.Slice(commaIdx + 1) : default;");
            w.CloseBrace();
        }

        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    private static void EmitObjectAddProperty(
        IndentedWriter w,
        bool hasDeepNesting,
        ParameterSerializationKind valueKind,
        string? valueTypeName)
    {
        if (hasDeepNesting && valueTypeName is not null)
        {
            // Values are JSON-encoded objects/arrays — parse directly into the workspace.
            w.WriteLine("objectBuilder._builder.AddPropertyValueFromJson(key, value);");
        }
        else
        {
            string valueSourceExpr = GetElementSourceExpression(valueKind, "value");
            w.WriteLine($"objectBuilder.AddProperty(key, {valueSourceExpr});");
        }
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
            ParameterSerializationKind.Decimal => $"decimal.Parse({varName})",
            ParameterSerializationKind.Int16 => $"short.Parse({varName})",
            ParameterSerializationKind.Byte => $"byte.Parse({varName})",
            ParameterSerializationKind.SByte => $"sbyte.Parse({varName})",
            ParameterSerializationKind.UInt16 => $"ushort.Parse({varName})",
            ParameterSerializationKind.UInt32 => $"uint.Parse({varName})",
            ParameterSerializationKind.UInt64 => $"ulong.Parse({varName})",
            ParameterSerializationKind.Half => $"double.Parse({varName})",
            ParameterSerializationKind.UnboundedNumber => $"double.Parse({varName})",
            _ => varName,
        };
    }

    /// <summary>
    /// Gets the source expression for converting a span/string variable to the element type.
    /// Public wrapper for use by server parameter parsing.
    /// </summary>
    /// <param name="kind">The serialization kind.</param>
    /// <param name="varName">The variable name holding the value.</param>
    /// <returns>A C# expression string.</returns>
    public static string GetElementSourceExpressionPublic(ParameterSerializationKind kind, string varName)
    {
        return GetElementSourceExpression(kind, varName);
    }

    /// <summary>
    /// Emits code to parse a comma-separated string into a typed array using CreateBuilder.
    /// Used by server parameter parsing (style: simple, form non-explode, pipe, space).
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="targetVar">The variable to assign the result to (e.g. "fieldNameValue").</param>
    /// <param name="rawValueVar">The variable holding the raw string value (e.g. "rawValue").</param>
    /// <param name="workspaceExpr">The workspace expression (e.g. "workspace").</param>
    /// <param name="typeName">The array type name.</param>
    /// <param name="separator">The separator character expression (e.g. "',' " or "'|'").</param>
    /// <param name="elementKind">The serialization kind of array elements.</param>
    /// <param name="hasDeepNesting">Whether elements may contain nested JSON.</param>
    /// <param name="elementTypeName">The element type name for deep-nested elements.</param>
    public static void EmitArrayParseFromSeparatedString(
        IndentedWriter w,
        string targetVar,
        string rawValueVar,
        string workspaceExpr,
        string typeName,
        string separator,
        ParameterSerializationKind elementKind,
        bool hasDeepNesting,
        string? elementTypeName = null)
    {
        string separatorExpr = hasDeepNesting
            ? "Corvus.Text.Json.OpenApi.StyleValueSplitter.NextSeparator(remaining)"
            : $"remaining.IndexOf({separator})";

        w.Write($"{targetVar} = {typeName}.CreateBuilder<string>({workspaceExpr}, {rawValueVar}, static (in string ctx, ref {typeName}.Builder arrayBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("System.ReadOnlySpan<char> remaining = ctx;");
        w.WriteLine("while (!remaining.IsEmpty)");
        w.OpenBrace();
        w.WriteLine($"int sepIdx = {separatorExpr};");
        w.WriteLine("System.ReadOnlySpan<char> element = sepIdx >= 0 ? remaining.Slice(0, sepIdx).Trim() : remaining.Trim();");

        if (hasDeepNesting && elementTypeName is not null)
        {
            w.WriteLine("arrayBuilder._builder.AddItemFromJson(element);");
        }
        else
        {
            string elementSourceExpr = GetElementSourceExpression(elementKind, "element");
            w.WriteLine($"arrayBuilder.AddItem({elementSourceExpr});");
        }

        w.WriteLine("remaining = sepIdx >= 0 ? remaining.Slice(sepIdx + 1) : default;");
        w.CloseBrace();
        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    /// <summary>
    /// Emits code to parse a string of key/value pairs into a typed object using CreateBuilder.
    /// Used by server parameter parsing for object-type parameters.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="targetVar">The variable to assign the result to.</param>
    /// <param name="rawValueVar">The variable holding the raw string value.</param>
    /// <param name="workspaceExpr">The workspace expression.</param>
    /// <param name="typeName">The object type name.</param>
    /// <param name="separator">The separator character expression (e.g. "','").</param>
    /// <param name="explode">Whether explode is enabled (key=value vs key,value).</param>
    /// <param name="valueKind">The serialization kind of object values.</param>
    /// <param name="hasDeepNesting">Whether values may contain nested JSON.</param>
    /// <param name="valueTypeName">The value type name for deep-nested values.</param>
    public static void EmitObjectParseFromSeparatedString(
        IndentedWriter w,
        string targetVar,
        string rawValueVar,
        string workspaceExpr,
        string typeName,
        string separator,
        bool explode,
        ParameterSerializationKind valueKind,
        bool hasDeepNesting,
        string? valueTypeName = null)
    {
        string separatorExpr = hasDeepNesting
            ? "Corvus.Text.Json.OpenApi.StyleValueSplitter.NextSeparator(remaining)"
            : $"remaining.IndexOf({separator})";

        w.Write($"{targetVar} = {typeName}.CreateBuilder<string>({workspaceExpr}, {rawValueVar}, static (in string ctx, ref {typeName}.Builder objectBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("System.ReadOnlySpan<char> remaining = ctx;");

        if (explode)
        {
            // explode: key=value,key=value (separator between pairs)
            w.WriteLine("while (!remaining.IsEmpty)");
            w.OpenBrace();
            w.WriteLine($"int sepIdx = {separatorExpr};");
            w.WriteLine("System.ReadOnlySpan<char> pair = sepIdx >= 0 ? remaining.Slice(0, sepIdx) : remaining;");
            w.WriteLine("int eqIdx = pair.IndexOf('=');");
            w.WriteLine("if (eqIdx >= 0)");
            w.OpenBrace();
            w.WriteLine("System.ReadOnlySpan<char> key = pair.Slice(0, eqIdx).Trim();");
            w.WriteLine("System.ReadOnlySpan<char> value = pair.Slice(eqIdx + 1).Trim();");
            EmitObjectAddProperty(w, hasDeepNesting, valueKind, valueTypeName);
            w.CloseBrace();
            w.WriteLine("remaining = sepIdx >= 0 ? remaining.Slice(sepIdx + 1) : default;");
            w.CloseBrace();
        }
        else
        {
            // non-explode: key,value,key,value (alternating)
            w.WriteLine("while (!remaining.IsEmpty)");
            w.OpenBrace();
            w.WriteLine($"int sepIdx = remaining.IndexOf({separator});");
            w.WriteLine("System.ReadOnlySpan<char> key = sepIdx >= 0 ? remaining.Slice(0, sepIdx).Trim() : remaining.Trim();");
            w.WriteLine("remaining = sepIdx >= 0 ? remaining.Slice(sepIdx + 1) : default;");
            w.WriteLine("if (remaining.IsEmpty) break;");
            w.WriteLine($"sepIdx = {separatorExpr};");
            w.WriteLine("System.ReadOnlySpan<char> value = sepIdx >= 0 ? remaining.Slice(0, sepIdx).Trim() : remaining.Trim();");
            EmitObjectAddProperty(w, hasDeepNesting, valueKind, valueTypeName);
            w.WriteLine("remaining = sepIdx >= 0 ? remaining.Slice(sepIdx + 1) : default;");
            w.CloseBrace();
        }

        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    /// <summary>
    /// Emits code to parse a scalar string value into a typed element.
    /// Used by server parameter parsing for scalar parameters.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="targetVar">The variable to assign the result to.</param>
    /// <param name="rawValueVar">The variable holding the raw string value.</param>
    /// <param name="workspaceExpr">The workspace expression.</param>
    /// <param name="typeName">The target type name.</param>
    /// <param name="kind">The serialization kind of the scalar.</param>
    public static void EmitScalarParse(
        IndentedWriter w,
        string targetVar,
        string rawValueVar,
        string workspaceExpr,
        string typeName,
        ParameterSerializationKind kind)
    {
        if (kind is ParameterSerializationKind.String)
        {
            w.WriteLine($"{targetVar} = Corvus.Text.Json.OpenApi.HeaderValueParser.ParseString<{typeName}>({rawValueVar}, {workspaceExpr});");
        }
        else if (IsNumericKind(kind))
        {
            w.WriteLine($"{targetVar} = Corvus.Text.Json.OpenApi.HeaderValueParser.ParseNumber<{typeName}>({rawValueVar}, {workspaceExpr});");
        }
        else
        {
            w.WriteLine($"{targetVar} = Corvus.Text.Json.OpenApi.HeaderValueParser.ParseBoolean<{typeName}>({rawValueVar}, {workspaceExpr});");
        }
    }

    /// <summary>
    /// Emits code to parse form+explode=true query parameters where multiple values arrive as separate query keys.
    /// Builds an array from StringValues.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="targetVar">The variable to assign the result to.</param>
    /// <param name="valuesVar">The StringValues variable name.</param>
    /// <param name="workspaceExpr">The workspace expression.</param>
    /// <param name="typeName">The array type name.</param>
    /// <param name="elementKind">The serialization kind of array elements.</param>
    public static void EmitArrayParseFromMultipleValues(
        IndentedWriter w,
        string targetVar,
        string valuesVar,
        string workspaceExpr,
        string typeName,
        ParameterSerializationKind elementKind)
    {
        w.Write($"{targetVar} = {typeName}.CreateBuilder<Microsoft.Extensions.Primitives.StringValues>({workspaceExpr}, {valuesVar}, static (in Microsoft.Extensions.Primitives.StringValues ctx, ref {typeName}.Builder arrayBuilder) =>");
        w.WriteLine();
        w.OpenBrace();
        w.WriteLine("for (int i = 0; i < ctx.Count; i++)");
        w.OpenBrace();
        w.WriteLine("string? item = ctx[i];");
        w.WriteLine("if (item is null) continue;");
        string elementSourceExpr = GetElementSourceExpression(elementKind, "item.AsSpan()");
        w.WriteLine($"arrayBuilder.AddItem({elementSourceExpr});");
        w.CloseBrace();
        w.CloseBraceNoNewline();
        w.WriteLine(").RootElement;");
    }

    /// <summary>
    /// Emits the <c>SendAsyncCore</c> and <c>SendWithBodyAsyncCore</c> helper methods.
    /// Only emits the helpers that are actually used by the client's operations.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="needsSendAsync">Whether the client needs <c>SendAsyncCore</c> (no-body operations).</param>
    /// <param name="needsSendWithBody">Whether the client needs <c>SendWithBodyAsyncCore</c> (JSON body operations).</param>
    /// <param name="needsSendWithStreamBody">Whether the client needs <c>SendWithStreamBodyAsyncCore</c> (stream upload operations).</param>
    /// <param name="needsSendWithBodyWriter">Whether the client needs <c>SendWithBodyWriterAsyncCore</c> (form/multipart operations).</param>
    public static void EmitSendAsyncCoreHelpers(
        IndentedWriter w,
        bool needsSendAsync = true,
        bool needsSendWithBody = true,
        bool needsSendWithStreamBody = true,
        bool needsSendWithBodyWriter = true)
    {
        bool emittedAny = false;

        if (needsSendAsync)
        {
            w.WriteLine("private async ValueTask<TResponse> SendAsyncCore<TRequest, TResponse>(");
            w.PushIndent();
            w.WriteLine("JsonWorkspace workspace,");
            w.WriteLine("TRequest request,");
            w.WriteLine("ValidationMode responseValidationMode,");
            w.WriteLine("CancellationToken cancellationToken)");
            w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
            w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
            w.PopIndent();
            w.OpenBrace();
            w.WriteLine("try");
            w.OpenBrace();
            w.WriteLine(
                "TResponse response = await this.transport.SendAsync<TRequest, TResponse>(in request, cancellationToken).ConfigureAwait(false);");
            w.WriteLine("response.Validate(responseValidationMode);");
            w.WriteLine("return response;");
            w.CloseBrace();
            w.WriteLine("finally");
            w.OpenBrace();
            w.WriteLine("workspace.Dispose();");
            w.CloseBrace();
            w.CloseBrace();
            emittedAny = true;
        }

        if (needsSendWithBody)
        {
            if (emittedAny)
            {
                w.WriteLine();
            }

            w.WriteLine("private async ValueTask<TResponse> SendWithBodyAsyncCore<TRequest, TBody, TResponse>(");
            w.PushIndent();
            w.WriteLine("JsonWorkspace workspace,");
            w.WriteLine("TRequest request,");
            w.WriteLine("TBody body,");
            w.WriteLine("ValidationMode responseValidationMode,");
            w.WriteLine("CancellationToken cancellationToken)");
            w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
            w.WriteLine("where TBody : struct, IJsonElement<TBody>");
            w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
            w.PopIndent();
            w.OpenBrace();
            w.WriteLine("try");
            w.OpenBrace();
            w.WriteLine(
                "TResponse response = await this.transport.SendAsync<TRequest, TBody, TResponse>(in request, in body, cancellationToken).ConfigureAwait(false);");
            w.WriteLine("response.Validate(responseValidationMode);");
            w.WriteLine("return response;");
            w.CloseBrace();
            w.WriteLine("finally");
            w.OpenBrace();
            w.WriteLine("workspace.Dispose();");
            w.CloseBrace();
            w.CloseBrace();
            emittedAny = true;
        }

        if (needsSendWithStreamBody)
        {
            if (emittedAny)
            {
                w.WriteLine();
            }

            w.WriteLine("private async ValueTask<TResponse> SendWithStreamBodyAsyncCore<TRequest, TResponse>(");
            w.PushIndent();
            w.WriteLine("JsonWorkspace workspace,");
            w.WriteLine("TRequest request,");
            w.WriteLine("Stream body,");
            w.WriteLine("string contentType,");
            w.WriteLine("ValidationMode responseValidationMode,");
            w.WriteLine("CancellationToken cancellationToken)");
            w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
            w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
            w.PopIndent();
            w.OpenBrace();
            w.WriteLine("try");
            w.OpenBrace();
            w.WriteLine(
                "TResponse response = await this.transport.SendAsync<TRequest, TResponse>(in request, body, contentType, cancellationToken).ConfigureAwait(false);");
            w.WriteLine("response.Validate(responseValidationMode);");
            w.WriteLine("return response;");
            w.CloseBrace();
            w.WriteLine("finally");
            w.OpenBrace();
            w.WriteLine("workspace.Dispose();");
            w.CloseBrace();
            w.CloseBrace();
            emittedAny = true;
        }

        if (needsSendWithBodyWriter)
        {
            if (emittedAny)
            {
                w.WriteLine();
            }

            w.WriteLine("private async ValueTask<TResponse> SendWithBodyWriterAsyncCore<TRequest, TResponse>(");
            w.PushIndent();
            w.WriteLine("JsonWorkspace workspace,");
            w.WriteLine("TRequest request,");
            w.WriteLine("Func<Stream, CancellationToken, ValueTask> bodyWriter,");
            w.WriteLine("string contentType,");
            w.WriteLine("ValidationMode responseValidationMode,");
            w.WriteLine("CancellationToken cancellationToken)");
            w.WriteLine("where TRequest : struct, IApiRequest<TRequest>");
            w.WriteLine("where TResponse : struct, IApiResponse<TResponse>");
            w.PopIndent();
            w.OpenBrace();
            w.WriteLine("try");
            w.OpenBrace();
            w.WriteLine(
                "TResponse response = await this.transport.SendAsync<TRequest, TResponse>(in request, bodyWriter, contentType, cancellationToken).ConfigureAwait(false);");
            w.WriteLine("response.Validate(responseValidationMode);");
            w.WriteLine("return response;");
            w.CloseBrace();
            w.WriteLine("finally");
            w.OpenBrace();
            w.WriteLine("workspace.Dispose();");
            w.CloseBrace();
            w.CloseBrace();
        }
    }

    /// <summary>
    /// Emits the response body handling for an <c>application/octet-stream</c> response,
    /// storing the content stream directly without JSON parsing.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="accessorName">The accessor name (e.g. "Ok").</param>
    public static void EmitStreamResponseBody(
        IndentedWriter w,
        string accessorName)
    {
        w.WriteLine($"response.{accessorName}Stream = contentStream;");
    }

    /// <summary>
    /// Emits the text/plain response body handling in CreateAsync.
    /// Reads the stream into a rented buffer from ArrayPool.
    /// </summary>
    public static void EmitTextPlainResponseBody(
        IndentedWriter w,
        string accessorName)
    {
        string camelName = ToCamelCase(accessorName);
        w.WriteLine(
            $"response.{camelName}TextBuffer = ReadStreamToRentedBuffer(contentStream, out int {camelName}TextLen);");
        w.WriteLine($"response.{camelName}TextLength = {camelName}TextLen;");
    }

    /// <summary>
    /// Emits the private backing fields for a text/plain response.
    /// </summary>
    public static void EmitTextPlainFields(
        IndentedWriter w,
        string accessorName)
    {
        string camelName = ToCamelCase(accessorName);
        w.WriteLine($"private byte[]? {camelName}TextBuffer;");
        w.WriteLine($"private int {camelName}TextLength;");
        w.WriteLine($"private string? {camelName}TextCached;");
    }

    /// <summary>
    /// Emits the lazy <c>Text</c> property and zero-alloc <c>Utf8Bytes</c> property
    /// for a text/plain response.
    /// </summary>
    public static void EmitTextPlainProperties(
        IndentedWriter w,
        string accessorName)
    {
        string camelName = ToCamelCase(accessorName);

        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine($"/// Gets the text/plain response body as a string. The string is");
        w.WriteLine("/// realized lazily on first access.");
        w.WriteLine("/// </summary>");
        w.WriteLine($"public string? {accessorName}Text");
        w.OpenBrace();
        w.WriteLine("get");
        w.OpenBrace();
        w.WriteLine($"if (this.{camelName}TextBuffer is null) {{ return null; }}");
        w.WriteLine();
        w.WriteLine(
            $"return this.{camelName}TextCached ??= System.Text.Encoding.UTF8.GetString(" +
            $"this.{camelName}TextBuffer, 0, this.{camelName}TextLength);");
        w.CloseBrace();
        w.CloseBrace();

        w.WriteLine();
        w.WriteLine("/// <summary>");
        w.WriteLine("/// Gets the raw UTF-8 bytes of the text/plain response body.");
        w.WriteLine("/// </summary>");
        w.WriteLine(
            $"public ReadOnlySpan<byte> {accessorName}Utf8Bytes => this.{camelName}TextBuffer is not null" +
            $" ? new ReadOnlySpan<byte>(this.{camelName}TextBuffer, 0, this.{camelName}TextLength)" +
            " : ReadOnlySpan<byte>.Empty;");
    }

    /// <summary>
    /// Emits the ArrayPool return for a text/plain response buffer in DisposeAsync.
    /// </summary>
    public static void EmitTextPlainBufferReturn(
        IndentedWriter w,
        string accessorName)
    {
        string camelName = ToCamelCase(accessorName);
        w.WriteLine($"if (this.{camelName}TextBuffer is not null)");
        w.OpenBrace();
        w.WriteLine($"System.Buffers.ArrayPool<byte>.Shared.Return(this.{camelName}TextBuffer);");
        w.CloseBrace();
    }

    /// <summary>
    /// Emits the private static <c>ReadStreamToRentedBuffer</c> helper method.
    /// </summary>
    public static void EmitReadStreamToRentedBufferHelper(IndentedWriter w)
    {
        w.WriteLine();
        w.WriteLine(
            "private static byte[] ReadStreamToRentedBuffer(Stream stream, out int bytesRead)");
        w.OpenBrace();

        // Seekable path: known length, single rent.
        w.WriteLine("if (stream.CanSeek)");
        w.OpenBrace();
        w.WriteLine("int length = (int)stream.Length;");
        w.WriteLine("byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(length);");
        w.WriteLine("bytesRead = 0;");
        w.WriteLine("while (bytesRead < length)");
        w.OpenBrace();
        w.WriteLine("int n = stream.Read(buffer, bytesRead, length - bytesRead);");
        w.WriteLine("if (n == 0) { break; }");
        w.WriteLine("bytesRead += n;");
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return buffer;");
        w.CloseBrace();
        w.WriteLine();

        // Non-seekable fallback: grow by doubling.
        w.WriteLine("byte[] buf = System.Buffers.ArrayPool<byte>.Shared.Rent(4096);");
        w.WriteLine("bytesRead = 0;");
        w.WriteLine("int read;");
        w.WriteLine(
            "while ((read = stream.Read(buf, bytesRead, buf.Length - bytesRead)) > 0)");
        w.OpenBrace();
        w.WriteLine("bytesRead += read;");
        w.WriteLine("if (bytesRead == buf.Length)");
        w.OpenBrace();
        w.WriteLine(
            "byte[] larger = System.Buffers.ArrayPool<byte>.Shared.Rent(buf.Length * 2);");
        w.WriteLine("System.Array.Copy(buf, larger, bytesRead);");
        w.WriteLine("System.Buffers.ArrayPool<byte>.Shared.Return(buf);");
        w.WriteLine("buf = larger;");
        w.CloseBrace();
        w.CloseBrace();
        w.WriteLine();
        w.WriteLine("return buf;");

        w.CloseBrace();
    }

    /// <summary>
    /// Classifies a media type string into a <see cref="ContentCategory"/>.
    /// </summary>
    /// <param name="mediaType">The media type string (e.g. <c>application/json</c>,
    /// <c>text/plain</c>, <c>*/*</c>).</param>
    /// <returns>The content category for code generation purposes.</returns>
    public static ContentCategory ClassifyMediaType(string mediaType)
    {
        if (string.Equals(mediaType, "application/json", StringComparison.OrdinalIgnoreCase)
            || mediaType.EndsWith("+json", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.Json;
        }

        if (string.Equals(mediaType, "application/x-www-form-urlencoded", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.FormUrlEncoded;
        }

        if (string.Equals(mediaType, "multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.Multipart;
        }

        if (string.Equals(mediaType, "multipart/mixed", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.MultipartMixed;
        }

        if (string.Equals(mediaType, "application/octet-stream", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.OctetStream;
        }

        if (string.Equals(mediaType, "text/plain", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.TextPlain;
        }

        // Wildcard ranges: text/* → text, everything else → binary stream
        if (mediaType.StartsWith("text/", StringComparison.OrdinalIgnoreCase))
        {
            return ContentCategory.TextPlain;
        }

        if (string.Equals(mediaType, "*/*", StringComparison.Ordinal))
        {
            return ContentCategory.OctetStream;
        }

        // Remaining wildcards (application/*, image/*, audio/*, video/*, etc.)
        // and any other unknown types → binary stream
        return ContentCategory.OctetStream;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type should be treated as JSON.
    /// Matches <c>application/json</c> exactly, and any type with a <c>+json</c>
    /// structured syntax suffix (e.g. <c>application/vnd.api+json</c>).
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type represents JSON content.</returns>
    public static bool IsJsonMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.Json;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type represents a binary octet stream.
    /// This includes <c>application/octet-stream</c> and wildcard patterns like
    /// <c>*/*</c> and <c>application/*</c> that are treated as binary.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type should be treated as a raw binary stream.</returns>
    public static bool IsOctetStreamMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.OctetStream;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type should be treated as plain text.
    /// This includes <c>text/plain</c> and wildcard patterns like <c>text/*</c>.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type should be treated as a text string.</returns>
    public static bool IsTextPlainMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.TextPlain;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type represents
    /// <c>application/x-www-form-urlencoded</c> content.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type is form URL-encoded.</returns>
    public static bool IsFormUrlEncodedMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.FormUrlEncoded;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type represents
    /// <c>multipart/form-data</c> content.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type is multipart form data.</returns>
    public static bool IsMultipartMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.Multipart;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the given media type represents
    /// <c>multipart/mixed</c> content.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type is multipart mixed.</returns>
    public static bool IsMultipartMixedMediaType(string mediaType)
    {
        return ClassifyMediaType(mediaType) == ContentCategory.MultipartMixed;
    }

    /// <summary>
    /// Returns <see langword="true"/> if the media type is any non-JSON raw content type
    /// (octet-stream, text/plain, image/*, etc.) that should be represented as a <see cref="Stream"/>.
    /// </summary>
    /// <param name="mediaType">The media type string.</param>
    /// <returns><see langword="true"/> if the media type is a raw stream type.</returns>
    public static bool IsRawStreamMediaType(string mediaType)
    {
        ContentCategory category = ClassifyMediaType(mediaType);
        return category == ContentCategory.OctetStream || category == ContentCategory.TextPlain;
    }

    /// <summary>
    /// Collects distinct response media types from an operation's responses,
    /// ordered by preference (JSON first, then text, then stream).
    /// </summary>
    /// <param name="responses">
    /// An enumerable of <c>(string MediaType, ...)</c> tuples from the response content entries.
    /// </param>
    /// <returns>
    /// An array of distinct media type strings suitable for the <c>Accept</c> header.
    /// Wildcard types like <c>*/*</c> are excluded — they don't add value in Accept.
    /// Returns an empty array if there are no concrete media types.
    /// </returns>
    public static string[] GetAcceptMediaTypes(IEnumerable<(string MediaType, string? SchemaPointer)> responses)
    {
        return responses
            .Select(r => r.MediaType)
            .Where(m => !string.Equals(m, "*/*", StringComparison.Ordinal))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(m => ClassifyMediaType(m) switch
            {
                ContentCategory.Json => 0,
                ContentCategory.TextPlain => 1,
                ContentCategory.OctetStream => 2,
                ContentCategory.FormUrlEncoded => 3,
                ContentCategory.Multipart => 3,
                ContentCategory.MultipartMixed => 3,
                _ => throw new UnreachableException(),
            })
            .ToArray();
    }

    /// <summary>
    /// Emits code that writes the <c>Accept</c> header via the header callback.
    /// </summary>
    /// <param name="w">The writer.</param>
    /// <param name="mediaTypes">The ordered media types for the Accept header value.</param>
    public static void EmitAcceptHeader(IndentedWriter w, string[] mediaTypes)
    {
        string acceptValue = string.Join(", ", mediaTypes);
        w.WriteLine($"callback(\"Accept\"u8, \"{acceptValue}\"u8, state);");
    }

    /// <summary>
    /// Returns a C# condition expression that tests the <c>contentType</c> parameter
    /// against the media types for a given <see cref="ContentCategory"/>.
    /// </summary>
    /// <param name="category">The content category to match.</param>
    /// <returns>A C# boolean expression string.</returns>
    public static string ContentTypeCondition(ContentCategory category)
    {
        return category switch
        {
            ContentCategory.Json =>
                "contentType is not null && (string.Equals(contentType, \"application/json\", StringComparison.OrdinalIgnoreCase) || contentType.EndsWith(\"+json\", StringComparison.OrdinalIgnoreCase))",
            ContentCategory.TextPlain =>
                "contentType is not null && contentType.StartsWith(\"text/\", StringComparison.OrdinalIgnoreCase)",
            ContentCategory.OctetStream =>
                "contentType is null || !contentType.StartsWith(\"text/\", StringComparison.OrdinalIgnoreCase)",
            _ => "true",
        };
    }

    /// <summary>
    /// Gets the suffix appended to match parameter names for non-JSON content categories.
    /// </summary>
    /// <param name="category">The content category.</param>
    /// <returns>An empty string for JSON, "String" for text, "Stream" for binary.</returns>
    public static string MatchParamSuffix(ContentCategory category)
    {
        return category switch
        {
            ContentCategory.TextPlain => "String",
            ContentCategory.OctetStream => "Stream",
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Gets the member name on the response struct to pass to the match handler.
    /// </summary>
    /// <param name="accessorName">The PascalCase accessor name (e.g. "Ok").</param>
    /// <param name="category">The content category.</param>
    /// <returns>The member expression to use in the match body.</returns>
    public static string GetMemberNameForMatchResult(string accessorName, ContentCategory category)
    {
        return category switch
        {
            ContentCategory.OctetStream => $"{accessorName}Stream",
            ContentCategory.TextPlain => $"{accessorName}Text",
            _ => $"{accessorName}Body",
        };
    }

    /// <summary>
    /// Gets the type name for a match handler parameter.
    /// </summary>
    /// <param name="category">The content category.</param>
    /// <param name="jsonTypeName">The resolved JSON type name (only used for JSON category).</param>
    /// <returns>The C# type name for the match handler parameter.</returns>
    public static string GetMatchTypeName(ContentCategory category, string? jsonTypeName)
    {
        return category switch
        {
            ContentCategory.OctetStream => "Stream?",
            ContentCategory.TextPlain => "string?",
            _ => jsonTypeName ?? string.Empty,
        };
    }

    /// <summary>
    /// Gets the C# condition expression for detecting which content category
    /// was populated in a response struct at runtime, used for multi-category match dispatch.
    /// </summary>
    /// <param name="accessorName">The PascalCase accessor name (e.g. "Ok").</param>
    /// <param name="category">The content category to detect.</param>
    /// <returns>A C# boolean expression string, or <see langword="null"/> for the fallback category.</returns>
    public static string? ContentCategoryDetectionCondition(string accessorName, ContentCategory category)
    {
        string camelName = ToCamelCase(accessorName);

        return category switch
        {
            ContentCategory.TextPlain => $"this.{camelName}TextBuffer is not null",
            ContentCategory.OctetStream => $"this.{accessorName}Stream is not null",
            _ => null, // JSON is the fallback — no condition needed.
        };
    }
}