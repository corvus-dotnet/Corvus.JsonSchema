// <copyright file="ProgramImage.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Text;
#if !STJ
using Corvus.Text.Json.Internal;
#endif

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
/// pattern in a table and constructed (or supplied by the regex provider) on load.
/// </para>
/// <para>
/// Size is kept down three ways: every byte payload (path segments, names, keywords, number text, annotation JSON)
/// is interned in one table and referenced by index; a node's schema location is stored as the earlier node whose
/// location is its longest prefix plus the interned remainder; and keyword groups a node does not use are skipped
/// under a per-node section mask.
/// </para>
/// <para>
/// The format is private to this assembly version: the header carries a version number and a loader rejects any
/// other version.
/// </para>
/// </remarks>
internal static class ProgramImage
{
    private const uint Magic = 0x50534A43; // "CJSP" little-endian
    private const int Version = 6;

    /// <summary>Writes the program to an image.</summary>
    public static byte[] Write(CompiledSchema program)
    {
        SchemaNode[] nodes = program.Nodes;
        var constants = new ConstantPool();
        var patterns = new PatternTable();
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
        // Node bodies intern every byte payload (schema locations, path segments, property names, annotation
        // bytes, number texts) in one table, since the same names and segments recur across nodes.
        var table = new BytesTable();
        var locations = new LocationTable();
        var body = new ImageWriter(64 * 1024, table);
        body.WriteInt(nodes.Length);
        foreach (SchemaNode node in nodes)
        {
            WriteNode(ref body, node, constants, patterns, locations);
        }

        w.WriteBytes(constants.ToJsonArray());
        w.WriteInt(patterns.Count);
        foreach (string pattern in patterns.Patterns)
        {
            w.WriteString(pattern);
        }

        w.WriteInt(table.Count);
        foreach (byte[] entry in table.Entries)
        {
            w.WriteBytes(entry);
        }

        w.WriteRaw(body.WrittenSpan);
        return w.ToArray();
    }

    /// <summary>Reads the pattern table of an image: the patterns that need a regular expression, in index order.</summary>
    public static string[] ReadPatterns(ReadOnlyMemory<byte> image)
    {
        var r = new ImageReader(image.Span);
        ReadHeader(ref r);
        r.ReadBool();
        r.ReadInt();
        r.ReadInt();
        int entryCount = r.ReadInt();
        for (int i = 0; i < entryCount; i++)
        {
            r.ReadString();
            r.ReadInt();
        }

        r.ReadBytes();
        return ReadPatternTable(ref r);
    }

#if !STJ
    /// <summary>Reads a program from an image.</summary>
    public static CompiledSchema Read(ReadOnlyMemory<byte> image, JsonSchemaEvaluatorOptions options)
    {
        var r = new ImageReader(image.Span);
        ReadHeader(ref r);
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

        string[] patterns = ReadPatternTable(ref r);
        var matchers = new PatternMatcher?[patterns.Length];

        int tableCount = r.ReadInt();
        var table = new byte[tableCount][];
        for (int i = 0; i < tableCount; i++)
        {
            table[i] = r.ReadBytes()!;
        }

        r.UseTable(table);
        int nodeCount = r.ReadInt();
        var nodes = new SchemaNode[nodeCount];
        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = ReadNode(ref r, constants, patterns, matchers, options, nodes);
        }

        // Fused plans are derived from the graph rather than stored; a node whose plan was fused when the image was
        // written but is not now (fusion disabled) falls back to the general path, and the reverse.
        FusedObjects.Compute(nodes);
        foreach (SchemaNode n in nodes)
        {
            if (n.Plan == NodePlan.FusedObject && n.Fused is null)
            {
                n.Plan = NodePlan.General;
            }
            else if (n.Fused is not null && n.Plan == NodePlan.General)
            {
                n.Plan = NodePlan.FusedObject;
            }
        }

        return new CompiledSchema(nodes, rootNode, usesDynamicScope, resourceCount, options, entryPoints, constantsDocument);
    }
#endif

    private static void ReadHeader(ref ImageReader r)
    {
        if (r.ReadUInt32() != Magic)
        {
            throw new JsonSchemaCompilationException("The data is not a compiled schema program image.");
        }

        int version = r.ReadInt();
        if (version != Version)
        {
            throw new JsonSchemaCompilationException($"The program image is version {version}; this evaluator reads version {Version}.");
        }
    }

    private static string[] ReadPatternTable(ref ImageReader r)
    {
        int count = r.ReadInt();
        var patterns = new string[count];
        for (int i = 0; i < count; i++)
        {
            patterns[i] = r.ReadString()!;
        }

        return patterns;
    }

    /// <summary>
    /// A pattern is written as its table index when it needs a regular expression (so a provider can supply one) and
    /// as its source otherwise; matchers for the same index are shared within a loaded program.
    /// </summary>
    private static void WritePattern(ref ImageWriter w, PatternMatcher? matcher, PatternTable patterns)
    {
        if (matcher is null)
        {
            w.WriteInt(-2);
        }
        else if (matcher.UsesRegex)
        {
            w.WriteInt(patterns.IndexOf(matcher.Source));
        }
        else
        {
            w.WriteInt(-1);
            w.WriteString(matcher.Source);
        }
    }

#if !STJ
    private static PatternMatcher? ReadPattern(ref ImageReader r, string[] patterns, PatternMatcher?[] matchers, JsonSchemaEvaluatorOptions options)
    {
        int index = r.ReadInt();
        if (index == -2)
        {
            return null;
        }

        if (index == -1)
        {
            return PatternMatcher.Create(r.ReadString()!, options);
        }

        return matchers[index] ??= PatternMatcher.Create(patterns[index], options, index);
    }
#endif

    private static void WriteNode(ref ImageWriter w, SchemaNode n, ConstantPool constants, PatternTable patterns, LocationTable locations)
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

        // A location is written as the earlier node whose location it extends, plus the (interned) suffix.
        (int baseNode, byte[] suffix) = locations.Add(n.Id, n.SchemaLocation);
        w.WriteInt(baseNode);
        w.WriteBytes(suffix);
        w.WriteByte((byte)n.Type);

        // Keyword groups that are entirely absent are skipped; the reader leaves the node's defaults.
        byte sections = Sections(n);
        w.WriteByte(sections);

        if ((sections & SectionValue) != 0)
        {
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
        }

        if ((sections & SectionNumber) != 0)
        {
            w.WriteString(n.Minimum?.Text);
            w.WriteString(n.Maximum?.Text);
            w.WriteString(n.ExclusiveMinimum?.Text);
            w.WriteString(n.ExclusiveMaximum?.Text);
            w.WriteString(n.MultipleOf?.Text);
        }

        if ((sections & SectionString) != 0)
        {
            w.WriteInt(n.MinLength);
            w.WriteInt(n.MaxLength);
            WritePattern(ref w, n.Pattern, patterns);
            w.WriteByte((byte)n.Format);
            w.WriteInt(n.ElidedTarget);
            w.WriteByte((byte)n.Content);
        }

        if ((sections & SectionObject) != 0)
        {
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
                WritePattern(ref w, p.Matcher, patterns);
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
        }

        if ((sections & SectionArray) != 0)
        {
            WriteChildRefs(ref w, n.PrefixItems);
            WriteChildRef(ref w, n.Items);
            WriteChildRef(ref w, n.Contains);
            w.WriteInt(n.MinContains);
            w.WriteInt(n.MaxContains);
            w.WriteInt(n.MinItems);
            w.WriteInt(n.MaxItems);
            WriteChildRef(ref w, n.UnevaluatedItems);
        }

        if ((sections & SectionInPlace) != 0)
        {
            WriteChildRef(ref w, n.Ref);
            if (n.DynamicRef is DynamicRefTarget dynamicRef)
            {
            w.WriteBool(true);
            w.WriteString(dynamicRef.Anchor);
            w.WriteInt(dynamicRef.FallbackNode);
            WriteInts(ref w, dynamicRef.NodeByResource);
            WriteInts(ref w, dynamicRef.NodeByEntryResource);
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
        }

        if ((sections & SectionAnnotations) != 0)
        {
            AnnotationEntry[] annotations = n.Annotations!;
            w.WriteInt(annotations.Length);
            foreach (AnnotationEntry a in annotations)
            {
                w.WriteBytes(a.Keyword);
                w.WriteBytes(a.RawJson);
                w.WriteBool(a.StringsOnly);
            }
        }
    }

    private const byte SectionValue = 1;
    private const byte SectionNumber = 2;
    private const byte SectionString = 4;
    private const byte SectionObject = 8;
    private const byte SectionArray = 16;
    private const byte SectionInPlace = 32;
    private const byte SectionAnnotations = 64;

    /// <summary>The keyword groups a node carries; a group whose every field is at its default is absent.</summary>
    private static byte Sections(SchemaNode n)
    {
        byte sections = 0;
        if (n.HasConst || n.ConstString is not null || n.ConstNumber is not null || n.Enum is not null || n.EnumStrings is not null)
        {
            sections |= SectionValue;
        }

        if (n.Minimum is not null || n.Maximum is not null || n.ExclusiveMinimum is not null || n.ExclusiveMaximum is not null || n.MultipleOf is not null)
        {
            sections |= SectionNumber;
        }

        if (n.MinLength >= 0 || n.MaxLength >= 0 || n.Pattern is not null || n.Format != FormatKind.None || n.ElidedTarget >= 0 || n.Content != ContentKind.None)
        {
            sections |= SectionString;
        }

        if (n.Properties is not null || n.UnrolledProperties is not null || n.SeenBitCount != 0 || n.RequiredSeenBits is not null || n.RequiredNames is not null
            || n.PatternProperties is not null || n.AdditionalProperties.IsPresent || n.PropertyNames.IsPresent || n.UnevaluatedProperties.IsPresent
            || n.MinProperties >= 0 || n.MaxProperties >= 0 || n.Dependencies is not null)
        {
            sections |= SectionObject;
        }

        if (n.PrefixItems is not null || n.Items.IsPresent || n.Contains.IsPresent || n.MinContains != 1 || n.MaxContains >= 0 || n.MinItems >= 0 || n.MaxItems >= 0 || n.UnevaluatedItems.IsPresent)
        {
            sections |= SectionArray;
        }

        if (n.Ref.IsPresent || n.DynamicRef is not null || n.AllOf is not null || n.AnyOf is not null || n.OneOf is not null || n.AnyOfDiscriminator is not null
            || n.OneOfDiscriminator is not null || n.AnyOfTypeUnion != TypeMask.None || n.OneOfTypeUnion != TypeMask.None || n.Not.IsPresent || n.If.IsPresent || n.Then.IsPresent || n.Else.IsPresent)
        {
            sections |= SectionInPlace;
        }

        if (n.Annotations is not null)
        {
            sections |= SectionAnnotations;
        }

        return sections;
    }

#if !STJ
    private static SchemaNode ReadNode(ref ImageReader r, List<ConstantValue> constants, string[] patterns, PatternMatcher?[] matchers, JsonSchemaEvaluatorOptions options, SchemaNode[] nodes)
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
        int baseNode = r.ReadInt();
        byte[] suffix = r.ReadBytes() ?? [];
        if (baseNode < 0)
        {
            n.SchemaLocation = suffix;
        }
        else
        {
            byte[] baseLocation = nodes[baseNode].SchemaLocation;
            byte[] location = new byte[baseLocation.Length + suffix.Length];
            baseLocation.CopyTo(location, 0);
            suffix.CopyTo(location, baseLocation.Length);
            n.SchemaLocation = location;
        }

        n.Type = (TypeMask)r.ReadByte();
        n.TypeMessage = SchemaCompiler.TypeMessageFor(n.Type);
        byte sections = r.ReadByte();

        if ((sections & SectionValue) != 0)
        {
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
        }

        if ((sections & SectionNumber) != 0)
        {
            n.Minimum = ReadNumber(ref r);
            n.Maximum = ReadNumber(ref r);
            n.ExclusiveMinimum = ReadNumber(ref r);
            n.ExclusiveMaximum = ReadNumber(ref r);
            string? multipleOf = r.ReadString();
            n.MultipleOf = multipleOf is null ? null : new DivisorValue(Encoding.UTF8.GetBytes(multipleOf));
        }

        if ((sections & SectionString) != 0)
        {
            n.MinLength = r.ReadInt();
            n.MaxLength = r.ReadInt();
            n.Pattern = ReadPattern(ref r, patterns, matchers, options);
            n.Format = (FormatKind)r.ReadByte();
            n.ElidedTarget = r.ReadInt();
            n.Content = (ContentKind)r.ReadByte();
        }

        if ((sections & SectionObject) != 0)
        {
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
                var p = new PatternPropertyEntry { Matcher = ReadPattern(ref r, patterns, matchers, options)! };
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
        }

        if ((sections & SectionArray) != 0)
        {
            n.PrefixItems = ReadChildRefs(ref r);
            n.Items = ReadChildRef(ref r);
            n.Contains = ReadChildRef(ref r);
            n.MinContains = r.ReadInt();
            n.MaxContains = r.ReadInt();
            n.MinItems = r.ReadInt();
            n.MaxItems = r.ReadInt();
            n.UnevaluatedItems = ReadChildRef(ref r);
        }

        if ((sections & SectionInPlace) != 0)
        {
            n.Ref = ReadChildRef(ref r);
            if (r.ReadBool())
            {
            n.DynamicRef = new DynamicRefTarget
            {
                Anchor = r.ReadString()!,
                FallbackNode = r.ReadInt(),
                NodeByResource = ReadInts(ref r) ?? [],
                NodeByEntryResource = ReadInts(ref r),
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
        }

        if ((sections & SectionAnnotations) != 0)
        {
            int annotationCount = r.ReadInt();
            var annotations = new AnnotationEntry[annotationCount];
            for (int i = 0; i < annotationCount; i++)
            {
                annotations[i] = new AnnotationEntry { Keyword = r.ReadBytes()!, RawJson = r.ReadBytes()!, StringsOnly = r.ReadBool() };
            }

            n.Annotations = annotations;
        }

        return n;
    }
#endif

    /// <summary>
    /// Encodes schema locations as the earlier node whose location is the longest prefix on a segment boundary, plus
    /// the remaining suffix, so a node's pointer costs an index and one interned segment instead of the whole pointer.
    /// </summary>
    private sealed class LocationTable
    {
        private readonly Dictionary<string, int> firstNodeByLocation = new(StringComparer.Ordinal);

        public (int BaseNode, byte[] Suffix) Add(int nodeId, byte[] location)
        {
            string text = Encoding.UTF8.GetString(location);
            (int BaseNode, byte[] Suffix) result = (-1, location);
            int cut = text.Length;
            while (true)
            {
                cut = text.LastIndexOf('/', Math.Max(0, cut - 1));
                if (cut <= 0)
                {
                    break;
                }

                if (this.firstNodeByLocation.TryGetValue(text.Substring(0, cut), out int baseNode))
                {
                    result = (baseNode, Encoding.UTF8.GetBytes(text.Substring(cut)));
                    break;
                }
            }

            if (!this.firstNodeByLocation.ContainsKey(text))
            {
                this.firstNodeByLocation.Add(text, nodeId);
            }

            return result;
        }
    }

#if !STJ
    private static NumberValue? ReadNumber(ref ImageReader r)
    {
        string? text = r.ReadString();
        return text is null ? null : new NumberValue(Encoding.UTF8.GetBytes(text));
    }
#endif

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

#if !STJ
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
#endif

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

#if !STJ
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
#endif

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
        WriteInts(ref w, d.AllBranches);
        w.WriteBool(d.AllRequire);
    }

#if !STJ
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
        d.AllBranches = ReadInts(ref r) ?? [];
        d.AllRequire = r.ReadBool();
        return d;
    }
#endif

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

#if !STJ
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
#endif

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

#if !STJ
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
#endif

    private static void Pack(ref uint bits, ref int bit, bool value)
    {
        if (value)
        {
            bits |= 1u << bit;
        }

        bit++;
    }

#if !STJ
    private static bool Unpack(uint bits, ref int bit)
    {
        bool value = (bits & (1u << bit)) != 0;
        bit++;
        return value;
    }
#endif

    /// <summary>Interns byte payloads by content, in first-seen order.</summary>
    private sealed class BytesTable
    {
        private readonly List<byte[]> entries = [];
        private readonly Dictionary<byte[], int> indices = new(ByteArrayComparer.Instance);

        public int Count => this.entries.Count;

        public IReadOnlyList<byte[]> Entries => this.entries;

        public int Intern(byte[] value)
        {
            if (!this.indices.TryGetValue(value, out int index))
            {
                index = this.entries.Count;
                this.entries.Add(value);
                this.indices.Add(value, index);
            }

            return index;
        }

        private sealed class ByteArrayComparer : IEqualityComparer<byte[]>
        {
            public static readonly ByteArrayComparer Instance = new();

            public bool Equals(byte[]? x, byte[]? y) => x.AsSpan().SequenceEqual(y);

            public int GetHashCode(byte[] obj)
            {
                HashCode hash = default;
#if NET6_0_OR_GREATER
                hash.AddBytes(obj);
#else
                foreach (byte b in obj)
                {
                    hash.Add(b);
                }
#endif
                return hash.ToHashCode();
            }
        }
    }

    /// <summary>Assigns each distinct regular-expression pattern an index, in first-seen order.</summary>
    private sealed class PatternTable
    {
        private readonly List<string> patterns = [];
        private readonly Dictionary<string, int> indices = new(StringComparer.Ordinal);

        public int Count => this.patterns.Count;

        public IReadOnlyList<string> Patterns => this.patterns;

        public int IndexOf(string pattern)
        {
            if (!this.indices.TryGetValue(pattern, out int index))
            {
                index = this.patterns.Count;
                this.patterns.Add(pattern);
                this.indices.Add(pattern, index);
            }

            return index;
        }
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
        private readonly BytesTable? table;
        private byte[] buffer;
        private int position;

        public ImageWriter(int capacity, BytesTable? table = null)
        {
            this.buffer = new byte[capacity];
            this.position = 0;
            this.table = table;
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

        /// <summary>
        /// Writes a byte array: as a table index when this writer interns, else length-prefixed inline;
        /// <see langword="null"/> is distinct from empty either way.
        /// </summary>
        public void WriteBytes(byte[]? value)
        {
            if (value is null)
            {
                this.WriteInt(-1);
                return;
            }

            if (this.table is BytesTable table)
            {
                this.WriteInt(table.Intern(value));
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
        private byte[][]? table;
        private string?[]? strings;

        public ImageReader(ReadOnlySpan<byte> data)
        {
            this.data = data;
            this.position = 0;
            this.table = null;
            this.strings = null;
        }

        /// <summary>Switches to table-indexed byte arrays and strings for the node section.</summary>
        public void UseTable(byte[][] entries)
        {
            this.table = entries;
            this.strings = new string?[entries.Length];
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

            if (this.table is byte[][] table)
            {
                return table[length];
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

            if (this.table is byte[][] table)
            {
                return this.strings![length] ??= Encoding.UTF8.GetString(table[length]);
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