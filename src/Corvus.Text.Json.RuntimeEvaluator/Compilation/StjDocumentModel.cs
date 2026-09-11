// <copyright file="StjDocumentModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
using System.Text;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// The slice of the Corvus document model the compiler and image writer use, over <see cref="System.Text.Json"/>.
/// </summary>
/// <remarks>
/// <para>
/// The source generator links the compiler in as source and builds against <c>System.Text.Json</c>, which has no
/// element identity. This model assigns one: every element the loader or compiler reaches is interned by its JSON
/// pointer within its document, and the intern ordinal is the element index the rest of the compiler keys on. The
/// pointer doubles as the node's schema location.
/// </para>
/// <para>
/// Only the members the compile path uses are provided; the evaluator itself is not built in this configuration.
/// </para>
/// </remarks>
internal interface IJsonDocument
{
    /// <summary>Gets the raw JSON text of a simple value (its number text, or the quoted string).</summary>
    ReadOnlyMemory<byte> GetRawSimpleValue(int index);

    /// <summary>Gets the token type of the value at an index.</summary>
    JsonTokenType GetJsonTokenType(int index);

    /// <summary>Gets the element at an intern ordinal.</summary>
    JsonElement GetElement(int index);

    /// <summary>Gets the ordinal of an element by pointer, assigning one on first sight.</summary>
    int Intern(string pointer, System.Text.Json.JsonElement element);
}

/// <summary>
/// A parsed document whose elements are addressed by intern ordinal.
/// </summary>
/// <typeparam name="T">Unused; keeps the name of the Corvus type the compiler is written against.</typeparam>
internal sealed class ParsedJsonDocument<T> : IJsonDocument, IDisposable
{
    private readonly System.Text.Json.JsonDocument document;
    private readonly List<(string Pointer, System.Text.Json.JsonElement Element)> elements = [];
    private readonly Dictionary<string, int> indexByPointer = new(StringComparer.Ordinal);

    private ParsedJsonDocument(System.Text.Json.JsonDocument document)
    {
        this.document = document;
    }

    /// <summary>Gets the root element.</summary>
    public JsonElement RootElement => this.GetElement(this.Intern(string.Empty, this.document.RootElement));

    /// <summary>Parses UTF-8 JSON.</summary>
    public static ParsedJsonDocument<T> Parse(ReadOnlyMemory<byte> utf8Json)
    {
        return new ParsedJsonDocument<T>(System.Text.Json.JsonDocument.Parse(utf8Json));
    }

    /// <inheritdoc/>
    public JsonElement GetElement(int index)
    {
        (string pointer, System.Text.Json.JsonElement element) = this.elements[index];
        return new JsonElement(this, index, pointer, element);
    }

    /// <inheritdoc/>
    public int Intern(string pointer, System.Text.Json.JsonElement element)
    {
        if (!this.indexByPointer.TryGetValue(pointer, out int index))
        {
            index = this.elements.Count;
            this.elements.Add((pointer, element));
            this.indexByPointer.Add(pointer, index);
        }

        return index;
    }

    /// <inheritdoc/>
    public ReadOnlyMemory<byte> GetRawSimpleValue(int index)
    {
        return Encoding.UTF8.GetBytes(this.elements[index].Element.GetRawText());
    }

    /// <inheritdoc/>
    public JsonTokenType GetJsonTokenType(int index)
    {
        return this.elements[index].Element.ValueKind switch
        {
            JsonValueKind.Object => JsonTokenType.StartObject,
            JsonValueKind.Array => JsonTokenType.StartArray,
            JsonValueKind.String => JsonTokenType.String,
            JsonValueKind.Number => JsonTokenType.Number,
            JsonValueKind.True => JsonTokenType.True,
            JsonValueKind.False => JsonTokenType.False,
            JsonValueKind.Null => JsonTokenType.Null,
            _ => JsonTokenType.None,
        };
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        this.document.Dispose();
    }
}

/// <summary>
/// An element with document identity: the document it belongs to, its intern ordinal and its JSON pointer.
/// </summary>
internal readonly struct JsonElement
{
    private readonly IJsonDocument? document;
    private readonly string pointer;
    private readonly System.Text.Json.JsonElement element;

    internal JsonElement(IJsonDocument document, int index, string pointer, System.Text.Json.JsonElement element)
    {
        this.document = document;
        this.ParentDocumentIndex = index;
        this.pointer = pointer;
        this.element = element;
    }

    /// <summary>Gets the owning document.</summary>
    public IJsonDocument ParentDocument => this.document ?? throw new InvalidOperationException("The element has no document.");

    /// <summary>Gets the element's intern ordinal within its document.</summary>
    public int ParentDocumentIndex { get; }

    /// <summary>Gets the value kind.</summary>
    public JsonValueKind ValueKind => this.element.ValueKind;

    /// <summary>Gets the string value.</summary>
    public string? GetString() => this.element.GetString();

    /// <summary>Gets the raw JSON text.</summary>
    public string GetRawText() => this.element.GetRawText();

    /// <summary>Gets the unescaped UTF-8 string value.</summary>
    public UnescapedUtf8JsonString GetUtf8String() => new(Encoding.UTF8.GetBytes(this.element.GetString() ?? string.Empty));

    /// <summary>Compares the string value to UTF-8 text.</summary>
    public bool ValueEquals(ReadOnlySpan<byte> utf8Text) => this.element.ValueEquals(utf8Text);

    /// <summary>Compares the string value to text.</summary>
    public bool ValueEquals(string? text) => this.element.ValueEquals(text);

    /// <summary>Tries to get the number as an <see cref="int"/>.</summary>
    public bool TryGetInt32(out int value) => this.element.TryGetInt32(out value);

    /// <summary>Tries to get the number as a <see cref="double"/>.</summary>
    public bool TryGetDouble(out double value) => this.element.TryGetDouble(out value);

    /// <summary>Tries to get a property by UTF-8 name.</summary>
    public bool TryGetProperty(ReadOnlySpan<byte> utf8Name, out JsonElement value)
    {
        if (this.element.ValueKind == JsonValueKind.Object && this.element.TryGetProperty(utf8Name, out System.Text.Json.JsonElement child))
        {
            value = this.Child(Encoding.UTF8.GetString(utf8Name.ToArray()), child);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Tries to get a property by name.</summary>
    public bool TryGetProperty(string name, out JsonElement value)
    {
        if (this.element.ValueKind == JsonValueKind.Object && this.element.TryGetProperty(name, out System.Text.Json.JsonElement child))
        {
            value = this.Child(name, child);
            return true;
        }

        value = default;
        return false;
    }

    /// <summary>Enumerates the properties of an object.</summary>
    public IEnumerable<JsonProperty<JsonElement>> EnumerateObject()
    {
        foreach (System.Text.Json.JsonProperty property in this.element.EnumerateObject())
        {
            yield return new JsonProperty<JsonElement>(property.Name, this.Child(property.Name, property.Value));
        }
    }

    /// <summary>Enumerates the items of an array.</summary>
    public IEnumerable<JsonElement> EnumerateArray()
    {
        int i = 0;
        foreach (System.Text.Json.JsonElement item in this.element.EnumerateArray())
        {
            yield return this.Child(i.ToString(System.Globalization.CultureInfo.InvariantCulture), item);
            i++;
        }
    }

    /// <summary>Writes the element's JSON pointer within its document.</summary>
    public bool TryGetJsonPointer(Span<byte> buffer, out int written)
    {
        byte[] utf8 = Encoding.UTF8.GetBytes(this.pointer);
        if (utf8.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        utf8.CopyTo(buffer);
        written = utf8.Length;
        return true;
    }

    /// <summary>Resolves a JSON pointer relative to this element.</summary>
    public bool TryResolvePointer(ReadOnlySpan<char> jsonPointer, out JsonElement resolved)
    {
        JsonElement current = this;
        ReadOnlySpan<char> remaining = jsonPointer;
        while (remaining.Length > 0)
        {
            if (remaining[0] != '/')
            {
                resolved = default;
                return false;
            }

            remaining = remaining[1..];
            int end = remaining.IndexOf('/');
            ReadOnlySpan<char> token = end < 0 ? remaining : remaining[..end];
            remaining = end < 0 ? default : remaining[end..];
            string name = Unescape(token);

            if (current.ValueKind == JsonValueKind.Object)
            {
                if (!current.TryGetProperty(name, out current))
                {
                    resolved = default;
                    return false;
                }
            }
            else if (current.ValueKind == JsonValueKind.Array)
            {
                if (!int.TryParse(name, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int index)
                    || index >= current.element.GetArrayLength())
                {
                    resolved = default;
                    return false;
                }

                current = current.Child(name, current.element[index]);
            }
            else
            {
                resolved = default;
                return false;
            }
        }

        resolved = current;
        return true;
    }

    private static string Unescape(ReadOnlySpan<char> token)
    {
        string s = token.ToString();
        return s.IndexOf('~') < 0 ? s : s.Replace("~1", "/").Replace("~0", "~");
    }

    private static string Escape(string name)
    {
        return name.IndexOfAny(['~', '/']) < 0 ? name : name.Replace("~", "~0").Replace("/", "~1");
    }

    private JsonElement Child(string token, System.Text.Json.JsonElement child)
    {
        IJsonDocument owner = this.document ?? throw new InvalidOperationException("The element has no document.");
        string childPointer = this.pointer + "/" + Escape(token);
        int index = owner.Intern(childPointer, child);
        return new JsonElement(owner, index, childPointer, child);
    }
}

/// <summary>
/// A named property of an object element.
/// </summary>
/// <typeparam name="TValue">The element type.</typeparam>
internal readonly struct JsonProperty<TValue>
{
    private readonly byte[] utf8Name;

    internal JsonProperty(string name, TValue value)
    {
        this.Name = name;
        this.utf8Name = Encoding.UTF8.GetBytes(name);
        this.Value = value;
    }

    /// <summary>Gets the property name.</summary>
    public string Name { get; }

    /// <summary>Gets the property name as UTF-8.</summary>
    public UnescapedUtf8JsonString Utf8NameSpan => new(this.utf8Name);

    /// <summary>Gets the property value.</summary>
    public TValue Value { get; }
}

/// <summary>
/// An unescaped UTF-8 string; nothing is rented in this model so disposal is a no-op.
/// </summary>
internal readonly ref struct UnescapedUtf8JsonString
{
    private readonly ReadOnlyMemory<byte> utf8Bytes;

    /// <summary>Initializes a new instance of the <see cref="UnescapedUtf8JsonString"/> struct.</summary>
    /// <param name="utf8Bytes">The UTF-8 bytes.</param>
    public UnescapedUtf8JsonString(ReadOnlyMemory<byte> utf8Bytes)
    {
        this.utf8Bytes = utf8Bytes;
    }

    /// <summary>Gets the bytes.</summary>
    public ReadOnlySpan<byte> Span => this.utf8Bytes.Span;

    /// <summary>Releases nothing.</summary>
    public void Dispose()
    {
    }
}

/// <summary>
/// The name hash the Corvus document model uses; kept identical so name maps built by the generator match.
/// </summary>
internal static class Utf8Hash
{
    public const int PerfectHashLength = 7;

    public const ulong HashMask = 0xFF00_0000_0000_0000UL;

    public static ulong GetHashCode(in ReadOnlySpan<byte> key)
    {
        int length = key.Length;

        return length switch
        {
            7 => System.Runtime.InteropServices.MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
            6 => System.Runtime.InteropServices.MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40),
            5 => System.Runtime.InteropServices.MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32),
            4 => System.Runtime.InteropServices.MemoryMarshal.Read<uint>(key.Slice(0, 4)),
            3 => ((ulong)key[2] << 16)
                    + ((ulong)key[1] << 8)
                    + key[0],
            2 => ((ulong)key[1] << 8)
                    + key[0],
            1 => key[0],
            0 => 0,
            _ => ((ulong)(((length + key[7] + key[key.Length - 1]) % 255) + 1) << 56)
                    + System.Runtime.InteropServices.MemoryMarshal.Read<uint>(key.Slice(0, 4))
                    + ((ulong)key[4] << 32)
                    + ((ulong)key[5] << 40)
                    + ((ulong)key[6] << 48),
        };
    }
}
#endif