// <copyright file="ProgramImage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Text;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json.RuntimeEvaluator.Compilation;

/// <summary>
/// Serialises a compiled program to a compact binary image and back, so that a schema compiled ahead of time
/// (by a generator) can be loaded without the schema text, the loader or the compiler.
/// </summary>
/// <remarks>
/// <para>
/// The image holds the node graph exactly as the compiler produced it (flags, plans, property maps, presence bits,
/// discriminators, resolved static and dynamic references, evaluation-path segments, annotation bytes) plus one
/// JSON array of every <c>const</c> and <c>enum</c> value, which is the only thing the evaluator still needs a parsed
/// document for (deep equality of object and array constants). Regular expressions are stored as their source
/// pattern and constructed on load.
/// </para>
/// <para>
/// The format is private to this assembly version: the header carries a version number and a loader rejects any
/// other version.
/// </para>
/// </remarks>
internal static class ProgramImage
{
    private const uint Magic = 0x50534A43; // "CJSP" little-endian
    private const int Version = 1;

    /// <summary>Writes the program to an image.</summary>
    public static byte[] Write(CompiledSchema program)
    {
        SchemaNode[] nodes = program.Nodes;
        var constants = new ConstantPool();
        var w = new ImageWriter(64 * 1024);

        w.WriteUInt32(Magic);
        w.WriteInt(Version);
        w.WriteBool(program.UsesDynamicScope);
        w.WriteInt(program.ResourceCount);
        w.WriteInt(program.RootNode);

        IReadOnlyDictionary<string, int> entryPoints = program.EntryPoints;
        w.WriteInt(entryPoints.Count);
        foreach (KeyValuePair<string, int> e in entryPoints)
        {
            w.WriteString(e.Key);
            w.WriteInt(e.Value);
        }

        // The node section is written to a separate buffer so the constants array, which the nodes reference by
        // index, can be placed before it.
        var body = new ImageWriter(64 * 1024);
        body.WriteInt(nodes.Length);
        foreach (SchemaNode node in nodes)
        {
            WriteNode(ref body, node, constants);
        }

        w.WriteBytes(constants.ToJsonArray());
        w.WriteRaw(body.WrittenSpan);
        return w.ToArray();
    }

    /// <summary>Reads a program from an image.</summary>
    public static CompiledSchema Read(ReadOnlyMemory<byte> image, JsonSchemaEvaluatorOptions options)
    {
        var r = new ImageReader(image.Span);
        if (r.ReadUInt32() != Magic)
        {
            throw new JsonSchemaCompilationException("The data is not a compiled schema program image.");
        }

        int version = r.ReadInt();
        if (version != Version)
        {
            throw new JsonSchemaCompilationException($"The program image is version {version}; this evaluator reads version {Version}.");
        }

        bool usesDynamicScope = r.ReadBool();
        int resourceCount = r.ReadInt();
        int rootNode = r.ReadInt();

        int entryCount = r.ReadInt();
        var entryPoints = new Dictionary<string, int>(entryCount, StringComparer.Ordinal);
        for (int i = 0; i < entryCount; i++)
        {
            string key = r.ReadString()!;
            entryPoints[key] = r.ReadInt();
        }

        byte[] constantsJson = r.ReadBytes()!;
        ParsedJsonDocument<JsonElement> constantsDocument = ParsedJsonDocument<JsonElement>.Parse(constantsJson);
        var constants = new List<ConstantValue>();
        foreach (JsonElement v in constantsDocument.RootElement.EnumerateArray())
        {
            IJsonDocument d = Elements.Document(v);
            int index = Elements.Index(v);
            constants.Add(new ConstantValue(d, index, d.GetJsonTokenType(index)));
        }

        int nodeCount = r.ReadInt();
        var nodes = new SchemaNode[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = ReadNode(ref r, constants, options);
        }

        return new CompiledSchema(nodes, rootNode, usesDynamicScope, resourceCount, options, entryPoints, constantsDocument);
    }

    private static void WriteNode(ref ImageWriter w, SchemaNode n, ConstantPool constants)
    {
        w.WriteInt(n.Id);
        w.WriteInt(n.ResourceId);
        w.WriteByte((byte)n.Dialect);

        uint bits = 0;
        int bit = 0;
        Pack(ref bits, ref bit, n.AlwaysTrue);
        Pack(ref bits, ref bit, n.AlwaysFalse);
        Pack(ref bits, ref bit, n.IsTypeOnly);
        Pack(ref bits, ref bit, n.IsLeaf);
        Pack(ref bits, ref bit, n.IsSimpleArray);
        Pack(ref bits, ref bit, n.TracksProperties);
        Pack(ref bits, ref bit, n.TracksItems);
        Pack(ref bits, ref bit, n.MarksProperties);
        Pack(ref bits, ref bit, n.MarksItems);
        Pack(ref bits, ref bit, n.HasNumberKeywords);
        Pack(ref bits, ref bit, n.HasStringKeywords);
        Pack(ref bits, ref bit, n.HasObjectKeywords);
        Pack(ref bits, ref bit, n.HasArrayKeywords);
        Pack(ref bits, ref bit, n.HasInPlaceApplicators);
        Pack(ref bits, ref bit, n.InPlaceCycle);
        Pack(ref bits, ref bit, n.HasSeenBits);
        Pack(ref bits, ref bit, n.HasType);
        Pack(ref bits, ref bit, n.HasConst);
        Pack(ref bits, ref bit, n.EnumAllStrings);
        Pack(ref bits, ref bit, n.AssertFormat);
        Pack(ref bits, ref bit, n.WarnFormat);
        Pack(ref bits, ref bit, n.AssertContent);
        Pack(ref bits, ref bit, n.UniqueItems);
        Pack(ref bits, ref bit, n.ContainsMarksEvaluated);
        w.WriteUInt32(bits);

        w.WriteUInt32((uint)n.Flags);
        w.WriteByte((byte)n.Plan);
        w.WriteBytes(n.SchemaLocation);
        w.WriteByte((byte)n.Type);

        // const / enum
        w.WriteInt(n.HasConst ? constants.Add(n.Const) : -1);
        w.WriteBytes(n.ConstString);
        w.WriteString(n.ConstNumber?.Text);
        if (n.Enum is ConstantValue[] values)
        {
            w.WriteInt(values.Length);
            foreach (ConstantValue v in values)
            {
                w.WriteInt(constants.Add(v));
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        if (n.EnumStrings is Utf8NameMap<object> enumStrings)
        {
            w.WriteInt(enumStrings.Count);
            foreach (byte[] key in enumStrings.Keys)
            {
                w.WriteBytes(key);
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        // number
        w.WriteString(n.Minimum?.Text);
        w.WriteString(n.Maximum?.Text);
        w.WriteString(n.ExclusiveMinimum?.Text);
        w.WriteString(n.ExclusiveMaximum?.Text);
        w.WriteString(n.MultipleOf?.Text);

        // string
        w.WriteInt(n.MinLength);
        w.WriteInt(n.MaxLength);
        w.WriteString(n.Pattern?.Source);
        w.WriteByte((byte)n.Format);
        w.WriteInt(n.ElidedTarget);
        w.WriteByte((byte)n.Content);

        // object
        PropertyEntry[]? propertyValues = null;
        if (n.Properties is Utf8NameMap<PropertyEntry> properties)
        {
            propertyValues = properties.Values.ToArray();
            w.WriteInt(propertyValues.Length);
            foreach (PropertyEntry p in propertyValues)
            {
                w.WriteBytes(p.Name);
                WriteChildRef(ref w, p.Schema);
                w.WriteInt(p.SeenBit);
                w.WriteBool(p.IsRequired);
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        if (n.UnrolledProperties is PropertyEntry[] unrolled)
        {
            w.WriteInt(unrolled.Length);
            foreach (PropertyEntry p in unrolled)
            {
                w.WriteInt(Array.IndexOf(propertyValues!, p));
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        w.WriteInt(n.SeenBitCount);
        WriteInts(ref w, n.RequiredSeenBits);
        WriteByteArrays(ref w, n.RequiredNames);

        if (n.PatternProperties is PatternPropertyEntry[] patternProperties)
        {
            w.WriteInt(patternProperties.Length);
            foreach (PatternPropertyEntry p in patternProperties)
            {
                w.WriteString(p.Matcher.Source);
                WriteChildRef(ref w, p.Schema);
                w.WriteBytes(p.Name);
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        WriteChildRef(ref w, n.AdditionalProperties);
        WriteChildRef(ref w, n.PropertyNames);
        WriteChildRef(ref w, n.UnevaluatedProperties);
        w.WriteInt(n.MinProperties);
        w.WriteInt(n.MaxProperties);

        if (n.Dependencies is DependencyEntry[] dependencies)
        {
            w.WriteInt(dependencies.Length);
            foreach (DependencyEntry d in dependencies)
            {
                w.WriteBytes(d.Name);
                w.WriteInt(d.SeenBit);
                WriteInts(ref w, d.RequiredSeenBits);
                WriteByteArrays(ref w, d.RequiredNames);
                WriteChildRef(ref w, d.Schema);
            }
        }
        else
        {
            w.WriteInt(-1);
        }

        // array
        WriteChildRefs(ref w, n.PrefixItems);
        WriteChildRef(ref w, n.Items);
        WriteChildRef(ref w, n.Contains);
        w.WriteInt(n.MinContains);
        w.WriteInt(n.MaxContains);
        w.WriteInt(n.MinItems);
        w.WriteInt(n.MaxItems);
        WriteChildRef(ref w, n.UnevaluatedItems);

        // in-place applicators
        WriteChildRef(ref w, n.Ref);
        if (n.DynamicRef is DynamicRefTarget dynamicRef)
        {
            w.WriteBool(true);
            w.WriteString(dynamicRef.Anchor);
            w.WriteInt(dynamicRef.FallbackNode);
            WriteInts(ref w, dynamicRef.NodeByResource);
            w.WriteBytes(dynamicRef.PathSegment);
            w.WriteBool(dynamicRef.IsRecursive);
        }
        else
        {
            w.WriteBool(false);
        }

        WriteChildRefs(ref w, n.AllOf);
        WriteChildRefs(ref w, n.AnyOf);
        WriteChildRefs(ref w, n.OneOf);
        WriteDiscriminator(ref w, n.AnyOfDiscriminator);
        WriteDiscriminator(ref w, n.OneOfDiscriminator);
        w.WriteByte((byte)n.AnyOfTypeUnion);
        w.WriteByte((byte)n.OneOfTypeUnion);
        WriteChildRef(ref w, n.Not);
        WriteChildRef(ref w, n.If);
        WriteChildRef(ref w, n.Then);
        WriteChildRef(ref w, n.Else);

        // annotations
        if (n.Annotations is AnnotationEntry[] annotations)
        {
            w.WriteInt(annotations.Length);
            foreach (AnnotationEntry a in annotations)
            {
                w.WriteBytes(a.Keyword);
                w.WriteBytes(a.RawJson);
                w.WriteBool(a.StringsOnly);
            }
        }
        else
        {
            w.WriteInt(-1);
        }
    }

    private static SchemaNode ReadNode(ref ImageReader r, List<ConstantValue> constants, JsonSchemaEvaluatorOptions options)
    {
        var n = new SchemaNode
        {
            Id = r.ReadInt(),
            ResourceId = r.ReadInt(),
            Dialect = (JsonSchemaDialect)r.ReadByte(),
        };

        uint bits = r.ReadUInt32();
        int bit = 0;
        n.AlwaysTrue = Unpack(bits, ref bit);
        n.AlwaysFalse = Unpack(bits, ref bit);
        n.IsTypeOnly = Unpack(bits, ref bit);
        n.IsLeaf = Unpack(bits, ref bit);
        n.IsSimpleArray = Unpack(bits, ref bit);
        n.TracksProperties = Unpack(bits, ref bit);
        n.TracksItems = Unpack(bits, ref bit);
        n.MarksProperties = Unpack(bits, ref bit);
        n.MarksItems = Unpack(bits, ref bit);
        n.HasNumberKeywords = Unpack(bits, ref bit);
        n.HasStringKeywords = Unpack(bits, ref bit);
        n.HasObjectKeywords = Unpack(bits, ref bit);
        n.HasArrayKeywords = Unpack(bits, ref bit);
        n.HasInPlaceApplicators = Unpack(bits, ref bit);
        n.InPlaceCycle = Unpack(bits, ref bit);
        n.HasSeenBits = Unpack(bits, ref bit);
        n.HasType = Unpack(bits, ref bit);
        n.HasConst = Unpack(bits, ref bit);
        n.EnumAllStrings = Unpack(bits, ref bit);
        n.AssertFormat = Unpack(bits, ref bit);
        n.WarnFormat = Unpack(bits, ref bit);
        n.AssertContent = Unpack(bits, ref bit);
        n.UniqueItems = Unpack(bits, ref bit);
        n.ContainsMarksEvaluated = Unpack(bits, ref bit);

        n.Flags = (NodeFlags)r.ReadUInt32();
        n.Plan = (NodePlan)r.ReadByte();
        n.SchemaLocation = r.ReadBytes() ?? [];
        n.Type = (TypeMask)r.ReadByte();
        n.TypeMessage = SchemaCompiler.TypeMessageFor(n.Type);

        // const / enum
        int constIndex = r.ReadInt();
        if (constIndex >= 0)
        {
            n.Const = constants[constIndex];
        }

        n.ConstString = r.ReadBytes();
        string? constNumber = r.ReadString();
        if (n.ConstString is not null)
        {
            n.ConstText = Encoding.UTF8.GetString(n.ConstString);
        }
        else if (constNumber is not null)
        {
            n.ConstNumber = new NumberValue(Encoding.UTF8.GetBytes(constNumber));
            n.ConstText = n.ConstNumber.Text;
        }

        int enumCount = r.ReadInt();
        if (enumCount >= 0)
        {
            var values = new ConstantValue[enumCount];
            for (int i = 0; i < enumCount; i++)
            {
                values[i] = constants[r.ReadInt()];
            }

            n.Enum = values;
        }

        int enumStringCount = r.ReadInt();
        if (enumStringCount >= 0)
        {
            var strings = new List<KeyValuePair<byte[], object>>(enumStringCount);
            for (int i = 0; i < enumStringCount; i++)
            {
                strings.Add(new KeyValuePair<byte[], object>(r.ReadBytes()!, SchemaCompiler.EnumSentinel));
            }

            n.EnumStrings = new Utf8NameMap<object>(strings);
        }

        // number
        n.Minimum = ReadNumber(ref r);
        n.Maximum = ReadNumber(ref r);
        n.ExclusiveMinimum = ReadNumber(ref r);
        n.ExclusiveMaximum = ReadNumber(ref r);
        string? multipleOf = r.ReadString();
        n.MultipleOf = multipleOf is null ? null : new DivisorValue(Encoding.UTF8.GetBytes(multipleOf));

        // string
        n.MinLength = r.ReadInt();
        n.MaxLength = r.ReadInt();
        string? pattern = r.ReadString();
        n.Pattern = pattern is null ? null : PatternMatcher.Create(pattern, options);
        n.Format = (FormatKind)r.ReadByte();
        n.ElidedTarget = r.ReadInt();
        n.Content = (ContentKind)r.ReadByte();

        // object
        PropertyEntry[]? propertyValues = null;
        int propertyCount = r.ReadInt();
        if (propertyCount >= 0)
        {
            propertyValues = new PropertyEntry[propertyCount];
            var entries = new List<KeyValuePair<byte[], PropertyEntry>>(propertyCount);
            for (int i = 0; i < propertyCount; i++)
            {
                var p = new PropertyEntry { Name = r.ReadBytes()! };
                p.Schema = ReadChildRef(ref r);
                p.SeenBit = r.ReadInt();
                p.IsRequired = r.ReadBool();
                propertyValues[i] = p;
                entries.Add(new KeyValuePair<byte[], PropertyEntry>(p.Name, p));
            }

            n.Properties = new Utf8NameMap<PropertyEntry>(entries);
        }

        int unrolledCount = r.ReadInt();
        if (unrolledCount >= 0)
        {
            var unrolled = new PropertyEntry[unrolledCount];
            for (int i = 0; i < unrolledCount; i++)
            {
                unrolled[i] = propertyValues![r.ReadInt()];
            }

            n.UnrolledProperties = unrolled;
        }

        n.SeenBitCount = r.ReadInt();
        n.RequiredSeenBits = ReadInts(ref r);
        n.RequiredNames = ReadByteArrays(ref r);

        int patternPropertyCount = r.ReadInt();
        if (patternPropertyCount >= 0)
        {
            var patternProperties = new PatternPropertyEntry[patternPropertyCount];
            for (int i = 0; i < patternPropertyCount; i++)
            {
                var p = new PatternPropertyEntry { Matcher = PatternMatcher.Create(r.ReadString()!, options) };
                p.Schema = ReadChildRef(ref r);
                p.Name = r.ReadBytes()!;
                patternProperties[i] = p;
            }

            n.PatternProperties = patternProperties;
        }

        n.AdditionalProperties = ReadChildRef(ref r);
        n.PropertyNames = ReadChildRef(ref r);
        n.UnevaluatedProperties = ReadChildRef(ref r);
        n.MinProperties = r.ReadInt();
        n.MaxProperties = r.ReadInt();

        int dependencyCount = r.ReadInt();
        if (dependencyCount >= 0)
        {
            var dependencies = new DependencyEntry[dependencyCount];
            for (int i = 0; i < dependencyCount; i++)
            {
                var d = new DependencyEntry { Name = r.ReadBytes()! };
                d.NameText = Encoding.UTF8.GetString(d.Name);
                d.SeenBit = r.ReadInt();
                d.RequiredSeenBits = ReadInts(ref r) ?? [];
                d.RequiredNames = ReadByteArrays(ref r) ?? [];
                d.Schema = ReadChildRef(ref r);
                dependencies[i] = d;
            }

            n.Dependencies = dependencies;
        }

        // array
        n.PrefixItems = ReadChildRefs(ref r);
        n.Items = ReadChildRef(ref r);
        n.Contains = ReadChildRef(ref r);
        n.MinContains = r.ReadInt();
        n.MaxContains = r.ReadInt();
        n.MinItems = r.ReadInt();
        n.MaxItems = r.ReadInt();
        n.UnevaluatedItems = ReadChildRef(ref r);

        // in-place applicators
        n.Ref = ReadChildRef(ref r);
        if (r.ReadBool())
        {
            n.DynamicRef = new DynamicRefTarget
            {
                Anchor = r.ReadString()!,
                FallbackNode = r.ReadInt(),
                NodeByResource = ReadInts(ref r) ?? [],
                PathSegment = r.ReadBytes() ?? [],
                IsRecursive = r.ReadBool(),
            };
        }

        n.AllOf = ReadChildRefs(ref r);
        n.AnyOf = ReadChildRefs(ref r);
        n.OneOf = ReadChildRefs(ref r);
        n.AnyOfDiscriminator = ReadDiscriminator(ref r);
        n.OneOfDiscriminator = ReadDiscriminator(ref r);
        n.AnyOfTypeUnion = (TypeMask)r.ReadByte();
        n.OneOfTypeUnion = (TypeMask)r.ReadByte();
        n.Not = ReadChildRef(ref r);
        n.If = ReadChildRef(ref r);
        n.Then = ReadChildRef(ref r);
        n.Else = ReadChildRef(ref r);

        // annotations
        int annotationCount = r.ReadInt();
        if (annotationCount >= 0)
        {
            var annotations = new AnnotationEntry[annotationCount];
            for (int i = 0; i < annotationCount; i++)
            {
                annotations[i] = new AnnotationEntry { Keyword = r.ReadBytes()!, RawJson = r.ReadBytes()!, StringsOnly = r.ReadBool() };
            }

            n.Annotations = annotations;
        }

        return n;
    }

    private static NumberValue? ReadNumber(ref ImageReader r)
    {
        string? text = r.ReadString();
        return text is null ? null : new NumberValue(Encoding.UTF8.GetBytes(text));
    }

    private static void WriteChildRef(ref ImageWriter w, in ChildRef c)
    {
        w.WriteInt(c.Node);
        if (c.Node >= 0)
        {
            w.WriteBytes(c.Path);
            w.WriteBytes(c.CollectingPath);
            w.WriteInt(c.FastNode);
        }
    }

    private static ChildRef ReadChildRef(ref ImageReader r)
    {
        int node = r.ReadInt();
        if (node < 0)
        {
            return ChildRef.None;
        }

        byte[]? path = r.ReadBytes();
        var c = new ChildRef(node, path) { CollectingPath = r.ReadBytes() };
        c.FastNode = r.ReadInt();
        return c;
    }

    private static void WriteChildRefs(ref ImageWriter w, ChildRef[]? refs)
    {
        if (refs is null)
        {
            w.WriteInt(-1);
            return;
        }

        w.WriteInt(refs.Length);
        foreach (ChildRef c in refs)
        {
            WriteChildRef(ref w, c);
        }
    }

    private static ChildRef[]? ReadChildRefs(ref ImageReader r)
    {
        int count = r.ReadInt();
        if (count < 0)
        {
            return null;
        }

        var refs = new ChildRef[count];
        for (int i = 0; i < count; i++)
        {
            refs[i] = ReadChildRef(ref r);
        }

        return refs;
    }

    private static void WriteDiscriminator(ref ImageWriter w, Discriminator? d)
    {
        if (d is null)
        {
            w.WriteBool(false);
            return;
        }

        w.WriteBool(true);
        w.WriteBytes(d.PropertyName);
        w.WriteInt(d.KnownValues.Count);
        byte[][] keys = d.KnownValues.Keys;
        ReadOnlySpan<int[]> values = d.KnownValues.Values;
        for (int i = 0; i < keys.Length; i++)
        {
            w.WriteBytes(keys[i]);
            WriteInts(ref w, values[i]);
        }

        WriteInts(ref w, d.UnknownString);
        WriteInts(ref w, d.NonString);
        w.WriteBool(d.AllRequire);
    }

    private static Discriminator? ReadDiscriminator(ref ImageReader r)
    {
        if (!r.ReadBool())
        {
            return null;
        }

        var d = new Discriminator { PropertyName = r.ReadBytes()! };
        int count = r.ReadInt();
        var known = new List<KeyValuePair<byte[], int[]>>(count);
        for (int i = 0; i < count; i++)
        {
            byte[] key = r.ReadBytes()!;
            known.Add(new KeyValuePair<byte[], int[]>(key, ReadInts(ref r) ?? []));
        }

        d.KnownValues = new Utf8NameMap<int[]>(known);
        d.UnknownString = ReadInts(ref r) ?? [];
        d.NonString = ReadInts(ref r) ?? [];
        d.AllRequire = r.ReadBool();
        return d;
    }

    private static void WriteInts(ref ImageWriter w, int[]? values)
    {
        if (values is null)
        {
            w.WriteInt(-1);
            return;
        }

        w.WriteInt(values.Length);
        foreach (int v in values)
        {
            w.WriteInt(v);
        }
    }

    private static int[]? ReadInts(ref ImageReader r)
    {
        int count = r.ReadInt();
        if (count < 0)
        {
            return null;
        }

        var values = new int[count];
        for (int i = 0; i < count; i++)
        {
            values[i] = r.ReadInt();
        }

        return values;
    }

    private static void WriteByteArrays(ref ImageWriter w, byte[][]? values)
    {
        if (values is null)
        {
            w.WriteInt(-1);
            return;
        }

        w.WriteInt(values.Length);
        foreach (byte[] v in values)
        {
            w.WriteBytes(v);
        }
    }

    private static byte[][]? ReadByteArrays(ref ImageReader r)
    {
        int count = r.ReadInt();
        if (count < 0)
        {
            return null;
        }

        var values = new byte[count][];
        for (int i = 0; i < count; i++)
        {
            values[i] = r.ReadBytes()!;
        }

        return values;
    }

    private static void Pack(ref uint bits, ref int bit, bool value)
    {
        if (value)
        {
            bits |= 1u << bit;
        }

        bit++;
    }

    private static bool Unpack(uint bits, ref int bit)
    {
        bool value = (bits & (1u << bit)) != 0;
        bit++;
        return value;
    }

    /// <summary>
    /// Collects the raw JSON of every <c>const</c> and <c>enum</c> value into one JSON array; a value's index in the
    /// array is its identity in the image.
    /// </summary>
    private sealed class ConstantPool
    {
        private readonly List<byte[]> values = [];
        private readonly Dictionary<(IJsonDocument Document, int Index), int> indices = [];

        public int Add(in ConstantValue value)
        {
            (IJsonDocument Document, int Index) key = (value.Document, value.Index);
            if (this.indices.TryGetValue(key, out int existing))
            {
                return existing;
            }

            JsonElement element = Elements.Create<JsonElement>(value.Document, value.Index);
            int index = this.values.Count;
            this.values.Add(Encoding.UTF8.GetBytes(element.GetRawText()));
            this.indices.Add(key, index);
            return index;
        }

        public byte[] ToJsonArray()
        {
            int length = 2;
            foreach (byte[] v in this.values)
            {
                length += v.Length + 1;
            }

            byte[] result = new byte[length];
            int pos = 0;
            result[pos++] = (byte)'[';
            for (int i = 0; i < this.values.Count; i++)
            {
                if (i > 0)
                {
                    result[pos++] = (byte)',';
                }

                this.values[i].CopyTo(result, pos);
                pos += this.values[i].Length;
            }

            result[pos++] = (byte)']';
            return pos == length ? result : result.AsSpan(0, pos).ToArray();
        }
    }

    private struct ImageWriter
    {
        private byte[] buffer;
        private int position;

        public ImageWriter(int capacity)
        {
            this.buffer = new byte[capacity];
            this.position = 0;
        }

        public readonly ReadOnlySpan<byte> WrittenSpan => this.buffer.AsSpan(0, this.position);

        public readonly byte[] ToArray() => this.WrittenSpan.ToArray();

        public void WriteByte(byte value)
        {
            this.Ensure(1);
            this.buffer[this.position++] = value;
        }

        public void WriteBool(bool value) => this.WriteByte(value ? (byte)1 : (byte)0);

        public void WriteUInt32(uint value)
        {
            this.Ensure(4);
            BinaryPrimitives.WriteUInt32LittleEndian(this.buffer.AsSpan(this.position), value);
            this.position += 4;
        }

        /// <summary>Writes an int as a zig-zag varint so small and negative values stay short.</summary>
        public void WriteInt(int value)
        {
            uint v = (uint)((value << 1) ^ (value >> 31));
            this.Ensure(5);
            while (v >= 0x80)
            {
                this.buffer[this.position++] = (byte)(v | 0x80);
                v >>= 7;
            }

            this.buffer[this.position++] = (byte)v;
        }

        /// <summary>Writes a length-prefixed byte array; <see langword="null"/> is distinct from empty.</summary>
        public void WriteBytes(byte[]? value)
        {
            if (value is null)
            {
                this.WriteInt(-1);
                return;
            }

            this.WriteInt(value.Length);
            this.WriteRaw(value);
        }

        public void WriteString(string? value)
        {
            this.WriteBytes(value is null ? null : Encoding.UTF8.GetBytes(value));
        }

        public void WriteRaw(ReadOnlySpan<byte> value)
        {
            this.Ensure(value.Length);
            value.CopyTo(this.buffer.AsSpan(this.position));
            this.position += value.Length;
        }

        private void Ensure(int count)
        {
            if (this.position + count > this.buffer.Length)
            {
                int size = Math.Max(this.buffer.Length * 2, this.position + count);
                Array.Resize(ref this.buffer, size);
            }
        }
    }

    private ref struct ImageReader
    {
        private readonly ReadOnlySpan<byte> data;
        private int position;

        public ImageReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            this.position = 0;
        }

        public byte ReadByte() => this.data[this.position++];

        public bool ReadBool() => this.ReadByte() != 0;

        public uint ReadUInt32()
        {
            uint value = BinaryPrimitives.ReadUInt32LittleEndian(this.data.Slice(this.position));
            this.position += 4;
            return value;
        }

        public int ReadInt()
        {
            uint v = 0;
            int shift = 0;
            while (true)
            {
                byte b = this.data[this.position++];
                v |= (uint)(b & 0x7F) << shift;
                if ((b & 0x80) == 0)
                {
                    break;
                }

                shift += 7;
            }

            return (int)(v >> 1) ^ -(int)(v & 1);
        }

        public byte[]? ReadBytes()
        {
            int length = this.ReadInt();
            if (length < 0)
            {
                return null;
            }

            byte[] value = this.data.Slice(this.position, length).ToArray();
            this.position += length;
            return value;
        }

        public string? ReadString()
        {
            int length = this.ReadInt();
            if (length < 0)
            {
                return null;
            }

#if NETSTANDARD2_0
            string value = Encoding.UTF8.GetString(this.data.Slice(this.position, length).ToArray());
#else
            string value = Encoding.UTF8.GetString(this.data.Slice(this.position, length));
#endif
            this.position += length;
            return value;
        }
    }
}
