// <copyright file="CodeGeneratorExtensions.Conversions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https://github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>

using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Code generator extensions for type conversion functionality.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends conversions from the .NET types supported by the <paramref name="typeDeclaration"/>
    /// based on the core type and format.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which is the basis of the conversions.</param>
    /// <param name="forMutable">If <see langword="true"/>, the code should be emitted for a mutable type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCoreTypeAndFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable)
    {
        HashSet<string> seenConversionOperators = new(StringComparer.Ordinal);
        bool handledNumber = false;
        if (typeDeclaration.Format() is string format)
        {
            handledNumber = FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatConversionOperators(generator, typeDeclaration, format, seenConversionOperators, forMutable);
            FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatConversionOperators(generator, typeDeclaration, format, seenConversionOperators, forMutable);
        }

        string typeName = forMutable ? "Mutable" : typeDeclaration.DotnetTypeName();

        if (typeDeclaration.ImpliedCoreTypes().CountTypes() == 1)
        {
            switch (typeDeclaration.ImpliedCoreTypes())
            {
                case CoreTypes.String:
                    // We always add the string conversion to string handlers.
                    if (seenConversionOperators.Add("string"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static ", typeDeclaration.UseImplicitOperatorString() ? "implicit" : "explicit", " operator string(", typeName, " value) => value._parent.GetString(value._idx, JsonTokenType.String) ?? throw new FormatException();");
                    }

                    break;

                case CoreTypes.Number:
                case CoreTypes.Integer:
                    if (handledNumber)
                    {
                        // Don't add any more numeric conversion operators if we handled it.
                        break;
                    }

                    if (seenConversionOperators.Add("long"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator long(", typeName, " value) => value._parent.TryGetValue(value._idx, out long result) ? result : throw new FormatException();");
                    }

                    if (seenConversionOperators.Add("double"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator double(", typeName, " value) => value._parent.TryGetValue(value._idx, out double result) ? result : throw new FormatException();");
                    }

                    if (seenConversionOperators.Add("BigNumber"))
                    {
                        // This is explicit because it allocates
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static explicit operator Corvus.Numerics.BigNumber(", typeName, " value) => value._parent.TryGetValue(value._idx, out Corvus.Numerics.BigNumber result) ? result : throw new FormatException();");
                    }

                    if (seenConversionOperators.Add("BigInteger"))
                    {
                        // This is explicit because it allocates
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static explicit operator System.Numerics.BigInteger(", typeName, " value) => value._parent.TryGetValue(value._idx, out System.Numerics.BigInteger result) ? result : throw new FormatException();");
                    }

                    if (seenConversionOperators.Add("decimal"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static explicit operator decimal(", typeName, " value) => value._parent.TryGetValue(value._idx, out decimal result) ? result : throw new FormatException();");
                    }

                    break;
                case CoreTypes.Boolean:
                    if (seenConversionOperators.Add("bool"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator bool(", typeName, " value)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("JsonTokenType type = value._parent.GetJsonTokenType(value._idx);")
                                .AppendSeparatorLine()
                                .AppendLineIndent("switch (type)")
                                .AppendLineIndent("{")
                                .PushIndent()
                                    .AppendLineIndent("case JsonTokenType.True:")
                                    .PushIndent()
                                        .AppendLineIndent("return true;")
                                    .PopIndent()
                                    .AppendLineIndent("case JsonTokenType.False:")
                                    .PushIndent()
                                        .AppendLineIndent("return false;")
                                    .PopIndent()
                                    .AppendLineIndent("default:")
                                    .PushIndent()
                                        .AppendLineIndent("throw new FormatException();")
                                    .PopIndent()
                                .PopIndent()
                                .AppendLineIndent("}")
                            .PopIndent()
                            .AppendLineIndent("}");
                    }

                    break;
                default:
                    break;
            }
        }
        else if (typeDeclaration.ImpliedCoreTypes().CountTypes() > 1)
        {
            // For multi-type declarations (e.g. unions), emit explicit conversion operators
            // for each implied core type.
            CoreTypes coreTypes = typeDeclaration.ImpliedCoreTypes();

            if ((coreTypes & CoreTypes.String) != 0)
            {
                if (seenConversionOperators.Add("string"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator string(", typeName, " value) => value._parent.GetString(value._idx, JsonTokenType.String) ?? throw new FormatException();");
                }
            }

            if ((coreTypes & (CoreTypes.Number | CoreTypes.Integer)) != 0 && !handledNumber)
            {
                if (seenConversionOperators.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator long(", typeName, " value) => value._parent.TryGetValue(value._idx, out long result) ? result : throw new FormatException();");
                }

                if (seenConversionOperators.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator double(", typeName, " value) => value._parent.TryGetValue(value._idx, out double result) ? result : throw new FormatException();");
                }

                if (seenConversionOperators.Add("BigNumber"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator Corvus.Numerics.BigNumber(", typeName, " value) => value._parent.TryGetValue(value._idx, out Corvus.Numerics.BigNumber result) ? result : throw new FormatException();");
                }

                if (seenConversionOperators.Add("BigInteger"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator System.Numerics.BigInteger(", typeName, " value) => value._parent.TryGetValue(value._idx, out System.Numerics.BigInteger result) ? result : throw new FormatException();");
                }

                if (seenConversionOperators.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator decimal(", typeName, " value) => value._parent.TryGetValue(value._idx, out decimal result) ? result : throw new FormatException();");
                }
            }

            if ((coreTypes & CoreTypes.Boolean) != 0)
            {
                if (seenConversionOperators.Add("bool"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public static explicit operator bool(", typeName, " value)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("JsonTokenType type = value._parent.GetJsonTokenType(value._idx);")
                            .AppendSeparatorLine()
                            .AppendLineIndent("switch (type)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("case JsonTokenType.True:")
                                .PushIndent()
                                    .AppendLineIndent("return true;")
                                .PopIndent()
                                .AppendLineIndent("case JsonTokenType.False:")
                                .PushIndent()
                                    .AppendLineIndent("return false;")
                                .PopIndent()
                                .AppendLineIndent("default:")
                                .PushIndent()
                                    .AppendLineIndent("throw new FormatException();")
                                .PopIndent()
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }

            // Emit explicit operators for formats discovered in composition sub-types.
            AppendCompositionSubtypeFormatOperators(generator, typeDeclaration, seenConversionOperators, forMutable);
        }

        return generator;
    }

    /// <summary>
    /// Iterates the composition sub-types (oneOf, anyOf, allOf) of a multi-type declaration
    /// and emits explicit conversion operators for any format-specific CLR types they imply,
    /// delegating to the registered format handlers.
    /// </summary>
    private static void AppendCompositionSubtypeFormatOperators(
        CodeGenerator generator,
        TypeDeclaration typeDeclaration,
        HashSet<string> seenConversionOperators,
        bool forMutable)
    {
        if (generator.IsCancellationRequested)
        {
            return;
        }

        HashSet<string> formats = new(StringComparer.Ordinal);
        CollectCompositionFormats(typeDeclaration, formats);

        foreach (string format in formats)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatConversionOperators(generator, typeDeclaration, format, seenConversionOperators, forMutable, useExplicit: true);
            FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatConversionOperators(generator, typeDeclaration, format, seenConversionOperators, forMutable, useExplicit: true);
        }

        static void CollectCompositionFormats(TypeDeclaration typeDeclaration, HashSet<string> formats)
        {
            if (typeDeclaration.OneOfCompositionTypes() is IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> oneOf)
            {
                foreach (TypeDeclaration subschema in oneOf.SelectMany(k => k.Value))
                {
                    if (subschema.ReducedTypeDeclaration().ReducedType.Format() is string format)
                    {
                        formats.Add(format);
                    }
                }
            }

            if (typeDeclaration.AnyOfCompositionTypes() is IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> anyOf)
            {
                foreach (TypeDeclaration subschema in anyOf.SelectMany(k => k.Value))
                {
                    if (subschema.ReducedTypeDeclaration().ReducedType.Format() is string format)
                    {
                        formats.Add(format);
                    }
                }
            }

            if (typeDeclaration.AllOfCompositionTypes() is IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> allOf)
            {
                foreach (TypeDeclaration subschema in allOf.SelectMany(k => k.Value))
                {
                    if (subschema.ReducedTypeDeclaration().ReducedType.Format() is string format)
                    {
                        formats.Add(format);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Appends value getters for the .NET types supported by the <paramref name="typeDeclaration"/>
    /// based on the core type and format.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration which is the basis of the conversions.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCoreTypeAndFormatValueGetters(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        HashSet<string> seenConversionOperators = new(StringComparer.Ordinal);
        bool handledNumber = false;
        if (typeDeclaration.Format() is string format)
        {
            handledNumber = FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatValueGetters(generator, typeDeclaration, format, seenConversionOperators);
            FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatValueGetters(generator, typeDeclaration, format, seenConversionOperators);
        }

        _ = typeDeclaration.DotnetTypeName();

        CoreTypes coreTypes = typeDeclaration.ImpliedCoreTypesOrAny();

        if ((coreTypes & CoreTypes.String) != 0)
        {
            // We always add the string conversion to string handlers.
            if (seenConversionOperators.Add("string"))
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public bool TryGetValue(out string? value) { CheckValidInstance(); return _parent.TryGetString(_idx, JsonTokenType.String, out value); }")
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public UnescapedUtf8JsonString GetUtf8String() { CheckValidInstance(); return _parent.GetUtf8JsonString(_idx, JsonTokenType.String); }")
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public UnescapedUtf16JsonString GetUtf16String() { CheckValidInstance(); return _parent.GetUtf16JsonString(_idx, JsonTokenType.String); }")
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public string? GetString() { CheckValidInstance(); return _parent.GetString(_idx, JsonTokenType.String); }");
            }
        }

        if ((coreTypes & (CoreTypes.Number | CoreTypes.Integer)) != 0)
        {
            if (!handledNumber)
            {
                if (seenConversionOperators.Add("long"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out long value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("int"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out int value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("short"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out short value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("sbyte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out sbyte value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("ulong"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out ulong value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("uint"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out uint value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("ushort"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out ushort value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("byte"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out byte value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("Int128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Int128 value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                if (seenConversionOperators.Add("UInt128"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out UInt128 value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                if (seenConversionOperators.Add("Half"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLine("#if NET")
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Half value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }")
                        .AppendLine("#endif");
                }

                if (seenConversionOperators.Add("double"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out double value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("float"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out float value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("BigNumber"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out Corvus.Numerics.BigNumber value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("BigInteger"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out System.Numerics.BigInteger value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }

                if (seenConversionOperators.Add("decimal"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                        .AppendLineIndent("public bool TryGetValue(out decimal value) { CheckValidInstance(); return _parent.TryGetValue(_idx, out value); }");
                }
            }
        }

        if ((coreTypes & CoreTypes.Boolean) != 0)
        {
            if (seenConversionOperators.Add("bool"))
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Tries to get the value as a boolean")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <param name=\"value\">Provides the boolean value if successful.</param>")
                    .AppendLineIndent("/// <returns><see langword=\"true\"/> if the value was a boolean, otherwise false.</returns>")
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public bool TryGetValue(out bool value)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CheckValidInstance();")
                        .AppendSeparatorLine()
                        .AppendLineIndent("JsonTokenType type = _parent.GetJsonTokenType(_idx);")
                        .AppendSeparatorLine()
                        .AppendLineIndent("switch (type)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("case JsonTokenType.True:")
                            .PushIndent()
                                .AppendLineIndent("value = true;")
                                .AppendLineIndent("return true;")
                            .PopIndent()
                            .AppendLineIndent("case JsonTokenType.False:")
                            .PushIndent()
                                .AppendLineIndent("value = false;")
                                .AppendLineIndent("return true;")
                            .PopIndent()
                            .AppendLineIndent("default:")
                            .PushIndent()
                                .AppendLineIndent("value = default;")
                                .AppendLineIndent("return false;")
                            .PopIndent()
                        .PopIndent()
                        .AppendLineIndent("}")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;
    }

    /// <summary>
    /// Appends conversions from dotnet type of the <paramref name="rootDeclaration"/>
    /// to the composition types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="rootDeclaration">The type declaration which is the basis of the conversions.</param>
    /// <param name="forMutable">If <see langword="true"/>, the code should be emitted for a mutable type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendConversionToCompositionTypes(
    this CodeGenerator generator,
    TypeDeclaration rootDeclaration,
    bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        HashSet<TypeDeclaration> appliedConversions = [];
        Queue<(TypeDeclaration Target, bool AllowsImplicitFrom, bool AllowsImplicitTo)> typesToProcess = [];

        typesToProcess.Enqueue((rootDeclaration, true, true));

        while (typesToProcess.Count > 0)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            (TypeDeclaration subschema, bool allowsImplicitFrom, bool allowsImplicitTo) = typesToProcess.Dequeue();
            AppendConversions(generator, appliedConversions, rootDeclaration, subschema, allowsImplicitFrom, allowsImplicitTo, forMutable);
            AppendCompositionConversions(generator, appliedConversions, typesToProcess, rootDeclaration, subschema, allowsImplicitFrom: allowsImplicitFrom, allowsImplicitTo: allowsImplicitTo, forMutable);
        }

        return generator;

        static void AppendCompositionConversions(
            CodeGenerator generator,
            HashSet<TypeDeclaration> appliedConversions,
            Queue<(TypeDeclaration Target, bool AllowsImplicitFrom, bool AllowsImplicitTo)> typesToProcess,
            TypeDeclaration rootType,
            TypeDeclaration sourceType,
            bool allowsImplicitFrom,
            bool allowsImplicitTo,
            bool forMutable)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            if (sourceType.AllOfCompositionTypes() is IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> allOf)
            {
                AppendSubschemaConversions(generator, appliedConversions, typesToProcess, rootType, allOf.SelectMany(k => k.Value), isImplicitFrom: false, isImplicitTo: allowsImplicitTo, forMutable: forMutable);
            }

            if (sourceType.AnyOfCompositionTypes() is IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> anyOf)
            {
                // Defer any of until all the AllOf have been processed so we prefer an implicit to the allOf types
                foreach (TypeDeclaration subschema in anyOf.SelectMany(k => k.Value))
                {
                    if (generator.IsCancellationRequested)
                    {
                        return;
                    }

                    typesToProcess.Enqueue((subschema.ReducedTypeDeclaration().ReducedType, allowsImplicitFrom, false));
                }
            }

            if (sourceType.OneOfCompositionTypes() is IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> oneOf)
            {
                // Defer any of until all the AllOf have been processed so we prefer an implicit to the allOf types
                foreach (TypeDeclaration subschema in oneOf.SelectMany(k => k.Value))
                {
                    if (generator.IsCancellationRequested)
                    {
                        return;
                    }

                    typesToProcess.Enqueue((subschema.ReducedTypeDeclaration().ReducedType, allowsImplicitFrom, false));
                }
            }
        }

        static void AppendSubschemaConversions(
            CodeGenerator generator,
            HashSet<TypeDeclaration> appliedConversions,
            Queue<(TypeDeclaration Target, bool AllowsImplicitFrom, bool AllowsImplicitTo)> typesToProcess,
            TypeDeclaration rootDeclaration,
            IEnumerable<TypeDeclaration> subschemas,
            bool isImplicitFrom,
            bool isImplicitTo,
            bool forMutable)
        {
            foreach (TypeDeclaration candidate in subschemas)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                TypeDeclaration subschema = candidate.ReducedTypeDeclaration().ReducedType;
                if (!AppendConversions(generator, appliedConversions, rootDeclaration, subschema, isImplicitFrom, isImplicitTo, forMutable))
                {
                    continue;
                }

                // Recurse, which will add more allOfs, and queue up the anyOfs and oneOfs.
                AppendCompositionConversions(generator, appliedConversions, typesToProcess, rootDeclaration, subschema, isImplicitFrom, isImplicitTo, forMutable);
            }
        }

        static bool AppendConversions(
            CodeGenerator generator,
            HashSet<TypeDeclaration> appliedConversions,
            TypeDeclaration rootDeclaration,
            TypeDeclaration subschema,
            bool isImplicitFrom,
            bool isImplicitTo,
            bool forMutable)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (rootDeclaration == subschema)
            {
                return false;
            }

            if (!appliedConversions.Add(subschema) || subschema.DoNotGenerate())
            {
                // We've already seen it.
                return false;
            }

            string implicitOrExplicitFrom = isImplicitFrom ? "implicit" : "explicit";
            string implicitOrExplicitTo = isImplicitTo ? "implicit" : "explicit";

            string subschemaTypeName = subschema.ReducedTypeDeclaration().ReducedType.FullyQualifiedDotnetTypeName();
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Conversion to <see cref=\"", subschema.ReducedTypeDeclaration().ReducedType.FullyQualifiedDotnetTypeName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
                .AppendIndent("public static ", implicitOrExplicitTo, " operator ", subschemaTypeName, forMutable ? ".Mutable" : "", "(")
                .Append(forMutable ? "Mutable" : rootDeclaration.DotnetTypeName())
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return ", subschemaTypeName, forMutable ? ".Mutable" : "", ".From(value);")
                .PopIndent()
                .AppendLineIndent("}");

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Conversion from <see cref=\"", subschema.ReducedTypeDeclaration().ReducedType.FullyQualifiedDotnetTypeName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"value\">The value from which to convert.</param>")
                .AppendIndent("public static ", implicitOrExplicitFrom, " operator ", forMutable ? "Mutable" : rootDeclaration.DotnetTypeName(), "(")
                .Append(subschema.ReducedTypeDeclaration().ReducedType.FullyQualifiedDotnetTypeName())
                .Append(forMutable ? ".Mutable" : "")
                .AppendLine(" value)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("return From(value);")
                .PopIndent()
                .AppendLineIndent("}");

            return true;
        }
    }

    /// <summary>
    /// Appends the pattern-matching methods.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration to which to append the method.</param>
    /// <param name="forMutable">If <see langword="true"/>, the code should be emitted for a mutable type.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendMatchMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        int matchOverloadIndex = 0;
        if (typeDeclaration.AllOfCompositionTypes() is IReadOnlyDictionary<IAllOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> allOf)
        {
            foreach (IAllOfSubschemaValidationKeyword keyword in allOf.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                var subschema = allOf[keyword].Select(o => o.ReducedTypeDeclaration().ReducedType).Distinct().ToList();
                if (subschema.Count > 1)
                {
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: true, matchOverloadIndex++, forMutable);
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: false, matchOverloadIndex++, forMutable);
                }
            }
        }

        if (typeDeclaration.AnyOfCompositionTypes() is IReadOnlyDictionary<IAnyOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> anyOf)
        {
            foreach (IAnyOfSubschemaValidationKeyword keyword in anyOf.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                var subschema = anyOf[keyword].Select(o => o.ReducedTypeDeclaration().ReducedType).Distinct().ToList();
                if (subschema.Count > 1)
                {
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: true, matchOverloadIndex++, forMutable);
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: false, matchOverloadIndex++, forMutable);
                }
            }
        }

        if (typeDeclaration.OneOfCompositionTypes() is IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> oneOf)
        {
            foreach (IOneOfSubschemaValidationKeyword keyword in oneOf.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                var subschema = oneOf[keyword].Select(o => o.ReducedTypeDeclaration().ReducedType).Distinct().ToList();
                if (subschema.Count > 1)
                {
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: true, matchOverloadIndex++, forMutable);
                    AppendMatchCompositionMethod(generator, typeDeclaration, subschema, includeContext: false, matchOverloadIndex++, forMutable);
                }
            }
        }

        if (typeDeclaration.AnyOfConstantValues() is IReadOnlyDictionary<IAnyOfConstantValidationKeyword, JsonElement[]> anyOfConstant)
        {
            foreach (IAnyOfConstantValidationKeyword keyword in anyOfConstant.Keys)
            {
                if (generator.IsCancellationRequested)
                {
                    return generator;
                }

                JsonElement[] constantValues = [.. anyOfConstant[keyword].Distinct()];
                if (constantValues.Length > 1)
                {
                    AppendMatchConstantMethod(generator, keyword, constantValues, includeContext: true, matchOverloadIndex: matchOverloadIndex++);
                    AppendMatchConstantMethod(generator, keyword, constantValues, includeContext: false, matchOverloadIndex: matchOverloadIndex++);
                }
            }
        }

        if (typeDeclaration.IfSubschemaType() is SingleSubschemaKeywordTypeDeclaration ifSubschema)
        {
            AppendMatchIfMethod(generator, typeDeclaration, ifSubschema, includeContext: true, matchOverloadIndex++, forMutable);
            AppendMatchIfMethod(generator, typeDeclaration, ifSubschema, includeContext: false, matchOverloadIndex++, forMutable);
        }

        return generator;

        static void AppendMatchCompositionMethod(CodeGenerator generator, TypeDeclaration typeDeclaration, IReadOnlyCollection<TypeDeclaration> subschema, bool includeContext, int matchOverloadIndex, bool forMutable)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            // No matcher if any of the subschema are a built-in JsonAny type, as we can always match that.
            if (subschema.Any(s => s.IsBuiltInJsonAnyType()))
            {
                return;
            }

            string scopeName = $"Match{matchOverloadIndex}";

            generator
                .ReserveNameIfNotReserved("Match")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                /// <summary>
                /// Matches the value against the composed values, and returns the result of calling the provided match function for the first match found.
                /// </summary>
                """);

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the immutable context to pass in to the match function.</typeparam>");
            }

            generator
                .AppendLineIndent("/// <typeparam name=\"TResult\">The result of calling the match function.</typeparam>");

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <param name=\"context\">The context to pass to the match function.</param>")
                    .ReserveNameIfNotReserved("context", childScope: scopeName);
            }

            // Reserve the parameter names we are going to require
            generator
                .ReserveNameIfNotReserved("defaultMatch", childScope: scopeName);

            string[] parameterNames = new string[subschema.Count];

            int i = 0;
            foreach (TypeDeclaration match in subschema)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                if (match.IsBuiltInJsonNotAnyType())
                {
                    // You can never match the built in NotAny type.
                    continue;
                }

                // This is the parameter name for the match match method.
                string matchTypeName = match.ReducedTypeDeclaration().ReducedType.FullyQualifiedDotnetTypeName();
                string matchParamName = generator.GetUniqueParameterNameInScope(match.ReducedTypeDeclaration().ReducedType.DotnetTypeName(), childScope: scopeName, prefix: "match");

                parameterNames[i++] = matchParamName;

                generator
                    .AppendLineIndent("/// <param name=\"", matchParamName, "\">Match a <see cref=\"", matchTypeName, "\"/>.</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"defaultMatch\">Match any other value.</param>")
                .AppendLineIndent("/// <returns>An instance of the value returned by the match function.</returns>")
                .AppendLineIndent("public TResult Match<", includeContext ? "TContext, " : string.Empty, "TResult>(")
                .PushMemberScope(scopeName, ScopeType.Method)
                .PushIndent();

            if (includeContext)
            {
                generator
                    .AppendIndent("in TContext context");
            }

            i = 0;
            foreach (TypeDeclaration match in subschema)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                TypeDeclaration t = match.ReducedTypeDeclaration().ReducedType;

                if (t.IsBuiltInJsonNotAnyType())
                {
                    continue;
                }

                if (i > 0 || includeContext)
                {
                    generator
                        .AppendLine(",");
                }

                generator
                    .AppendIndent(
                        "Matcher<",
                        t.FullyQualifiedDotnetTypeName(),
                        includeContext ? ", TContext" : string.Empty,
                        ", TResult> ",
                        parameterNames[i++]);
            }

            generator
                .AppendLine(",")
                .AppendLineIndent(
                    "Matcher<",
                    typeDeclaration.FullyQualifiedDotnetTypeName(),
                    forMutable ? ".Mutable" : "",
                    includeContext ? ", TContext" : string.Empty,
                    ", TResult> defaultMatch)")
                .PopIndent();

            if (includeContext)
            {
                generator
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .AppendLineIndent("where TContext : allows ref struct")
                    .AppendLine("#endif");
            }

            generator
                .AppendLineIndent("{")
                .PushIndent();

            i = 0;
            foreach (TypeDeclaration match in subschema)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                TypeDeclaration matchType = match.ReducedTypeDeclaration().ReducedType;
                string matchTypeName = matchType.FullyQualifiedDotnetTypeName();

                if (matchType.IsBuiltInJsonNotAnyType())
                {
                    // You can never match the built in NotAny type.
                    continue;
                }

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("if (", matchTypeName, ".", generator.JsonSchemaClassName(matchTypeName), ".Evaluate(_parent, _idx))")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("return ", parameterNames[i], "(", matchTypeName, forMutable ? ".Mutable" : "", ".From(this)", includeContext ? ", context" : string.Empty, ");")
                    .PopIndent()
                    .AppendLineIndent("}");

                i++;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("return defaultMatch(this", includeContext ? ", context" : string.Empty, ");")
                .PopMemberScope()
                .PopIndent()
                .AppendLineIndent("}");
        }

        static void AppendMatchConstantMethod(CodeGenerator generator, IAnyOfConstantValidationKeyword keyword, JsonElement[] constValues, bool includeContext, int matchOverloadIndex)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            string scopeName = $"Match{matchOverloadIndex}";

            generator
                .ReserveNameIfNotReserved("Match")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
            /// <summary>
            /// Matches the value against the constant values, and returns the result of calling the provided match function for the first match found.
            /// </summary>
            """);

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <typeparam name=\"TContext\">The immutable context to pass in to the match function.</typeparam>");
            }

            generator
                .AppendLineIndent("/// <typeparam name=\"TResult\">The result of calling the match function.</typeparam>");

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <param name=\"context\">The context to pass to the match function.</param>")
                    .ReserveNameIfNotReserved("context", childScope: scopeName);
            }

            // Reserve the parameter names we are going to require
            generator
                .ReserveNameIfNotReserved("defaultMatch", childScope: scopeName);

            int count = constValues.Length;
            string[] parameterNames = new string[count];
            string[] constFields = new string[count];

            for (int i = 1; i <= count; ++i)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                JsonElement constValue = constValues[i - 1];

                string matchParamName = GetUniqueParameterName(generator, scopeName, constValue, i);
                string constField =
                    generator.GetStaticReadOnlyPropertyNameInScope(
                        keyword.Keyword,
                        rootScope: generator.ConstantsScope(),
                        suffix: count > 1 ? i.ToString() : null);

                parameterNames[i - 1] = matchParamName;
                constFields[i - 1] = constField;

                generator
                    .AppendIndent("/// <param name=\"", matchParamName, "\">Match ")
                    .AppendOrdinalName(i)
                    .AppendLine(" item.</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"defaultMatch\">Match any other value.</param>")
                .AppendLineIndent("/// <returns>An instance of the value returned by the match function.</returns>")
                .AppendLineIndent("public TResult Match<", includeContext ? "TContext, " : string.Empty, "TResult>(")
                .PushMemberScope(scopeName, ScopeType.Method)
                .PushIndent();

            if (includeContext)
            {
                generator
                    .AppendIndent("in TContext context");
            }

            for (int i = 0; i < count; ++i)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                if (i > 0 || includeContext)
                {
                    generator
                        .AppendLine(",");
                }

                generator
                    .AppendIndent(
                        "Func<",
                        includeContext ? "TContext, " : string.Empty,
                        "TResult> ",
                        parameterNames[i]);
            }

            generator
                .AppendLine(",")
                .AppendLineIndent(
                    "Func<",
                    includeContext ? "TContext, " : string.Empty,
                    "TResult> defaultMatch)")
                .PopIndent();

            if (includeContext)
            {
                generator
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .AppendLineIndent("where TContext : allows ref struct")
                    .AppendLine("#endif");
            }

            generator
                .AppendLineIndent("{")
                .PushIndent();

            for (int i = 0; i < count; ++i)
            {
                if (generator.IsCancellationRequested)
                {
                    return;
                }

                if (constValues[i].ValueKind == JsonValueKind.True)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (this.TokenType == JsonTokenType.True)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", parameterNames[i], "(", includeContext ? "context);" : ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
                else if (constValues[i].ValueKind == JsonValueKind.False)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (this.TokenType == JsonTokenType.False)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", parameterNames[i], "(", includeContext ? "context);" : ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
                else if (constValues[i].ValueKind == JsonValueKind.Null)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (this.TokenType == JsonTokenType.Null)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", parameterNames[i], "(", includeContext ? "context);" : ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
                else
                {
                    bool isStringOrNumber =
                        constValues[i].ValueKind is JsonValueKind.String or JsonValueKind.Number;

                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (this.", isStringOrNumber ? "Value" : string.Empty, "Equals(", generator.ConstantsClassName(), ".", constFields[i], "))")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", parameterNames[i], "(", includeContext ? "context);" : ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("return defaultMatch(", includeContext ? "context" : string.Empty, ");")
                .PopMemberScope()
                .PopIndent()
                .AppendLineIndent("}");
        }

        static string GetUniqueParameterName(CodeGenerator generator, string scopeName, JsonElement constValue, int index)
        {
            if (generator.IsCancellationRequested)
            {
                return string.Empty;
            }

            return constValue.ValueKind switch
            {
                JsonValueKind.Object => generator.GetUniqueParameterNameInScope("matchObjectValue", childScope: scopeName, suffix: index.ToString()),
                JsonValueKind.Array => generator.GetUniqueParameterNameInScope("matchArrayValue", childScope: scopeName, suffix: index.ToString()),
                JsonValueKind.String => generator.GetUniqueParameterNameInScope(constValue.GetString()!, childScope: scopeName, prefix: "match"),
                JsonValueKind.Number => generator.GetUniqueParameterNameInScope(constValue.GetRawText().Replace(".", "point"), childScope: scopeName, prefix: "matchNumber"),
                JsonValueKind.True => generator.GetUniqueParameterNameInScope("matchTrue", childScope: scopeName),
                JsonValueKind.False => generator.GetUniqueParameterNameInScope("matchFalse", childScope: scopeName),
                JsonValueKind.Null => generator.GetUniqueParameterNameInScope("matchNull", childScope: scopeName),
                _ => throw new InvalidOperationException($"Unsupported JsonValueKind: {constValue.ValueKind}"),
            };
        }

        static void AppendMatchIfMethod(CodeGenerator generator, TypeDeclaration typeDeclaration, SingleSubschemaKeywordTypeDeclaration ifSubschema, bool includeContext, int matchOverloadIndex, bool forMutable)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            SingleSubschemaKeywordTypeDeclaration? thenDeclaration = typeDeclaration.ThenSubschemaType();
            SingleSubschemaKeywordTypeDeclaration? elseDeclaration = typeDeclaration.ElseSubschemaType();

            if (thenDeclaration is null && elseDeclaration is null)
            {
                return;
            }

            if (ifSubschema.ReducedType.IsBuiltInJsonAnyType() &&
                    thenDeclaration is null)
            {
                // Only ever need to evaluate the then clause and there isn't one.
                return;
            }

            if (ifSubschema.ReducedType.IsBuiltInJsonNotAnyType() &&
                elseDeclaration is null)
            {
                // Only ever need to evaluate the else clause and there isn't one.
                return;
            }

            string scopeName = $"Match{matchOverloadIndex}";

            generator
                .ReserveNameIfNotReserved("Match")
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent(
                    "/// Matches the value against the 'if' type, and returns the result of calling the provided match function for");
            if (thenDeclaration is not null)
            {
                generator
                    .AppendLineIndent("/// the 'then' type if the match is successful", elseDeclaration is not null ? " or" : ".");
            }

            if (elseDeclaration is not null)
            {
                generator
                    .AppendLineIndent("/// the 'else' type if the match is not successful.");
            }

            generator
                .AppendLineIndent("/// </summary>");

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <typeparam name=\"TContext\">The immutable context to pass in to the match function.</typeparam>");
            }

            generator
                .AppendLineIndent("/// <typeparam name=\"TResult\">The result of calling the match function.</typeparam>");

            if (includeContext)
            {
                generator
                    .AppendLineIndent("/// <param name=\"context\">The context to pass to the match function.</param>")
                    .ReserveNameIfNotReserved("context", childScope: scopeName);
            }

            string? thenMatchParamName = null;

            if (thenDeclaration is SingleSubschemaKeywordTypeDeclaration thenSubschema)
            {
                // This is the parameter name for the if match method.
                string? thenMatchTypeName = thenSubschema.ReducedType.FullyQualifiedDotnetTypeName();
                thenMatchParamName = generator.GetUniqueParameterNameInScope(thenMatchTypeName, childScope: scopeName, prefix: "match");

                generator
                    .AppendLineIndent("/// <param name=\"", thenMatchParamName, "\">Match a <see cref=\"", thenMatchTypeName, "\"/>.</param>");
            }

            string? elseMatchParamName = null;
            if (elseDeclaration is SingleSubschemaKeywordTypeDeclaration elseSubschema)
            {
                // This is the parameter name for the if match method.
                string? elseMatchTypeName = elseSubschema.ReducedType.FullyQualifiedDotnetTypeName();
                elseMatchParamName = generator.GetUniqueParameterNameInScope(elseMatchTypeName, childScope: scopeName, prefix: "match");

                generator
                    .AppendLineIndent("/// <param name=\"", elseMatchParamName, "\">Match a <see cref=\"", elseMatchTypeName, "\"/>.</param>");
            }

            if (elseMatchParamName is null)
            {
                generator
                    .AppendLineIndent("/// <param name=\"defaultMatch\">Default match if the 'if' schema did not match.</param>");
            }

            if (thenMatchParamName is null)
            {
                generator
                    .AppendLineIndent("/// <param name=\"defaultMatch\">Default match if the 'if' schema matched.</param>");
            }

            generator
                .AppendLineIndent("/// <returns>An instance of the value returned by the match function.</returns>")
                .AppendLineIndent("public TResult Match<", includeContext ? "TContext, " : string.Empty, "TResult>(")
                .PushMemberScope(scopeName, ScopeType.Method)
                .PushIndent();

            if (includeContext)
            {
                generator
                    .AppendIndent("in TContext context");
            }

            if (thenDeclaration is SingleSubschemaKeywordTypeDeclaration thenSubschema2 &&
                thenMatchParamName is string thenMatchParamName2)
            {
                if (includeContext)
                {
                    generator
                        .AppendLine(",");
                }

                generator
                    .AppendIndent(
                        "Matcher<",
                        thenSubschema2.ReducedType.FullyQualifiedDotnetTypeName(),
                        includeContext ? ", TContext" : string.Empty,
                        ", TResult> ",
                        thenMatchParamName2);
            }

            if (elseDeclaration is SingleSubschemaKeywordTypeDeclaration elseSubschema2 &&
                elseMatchParamName is string elseMatchParamName2)
            {
                if (thenDeclaration is not null || includeContext)
                {
                    generator
                        .AppendLine(",");
                }

                generator
                    .AppendIndent(
                        "Matcher<",
                        elseSubschema2.ReducedType.FullyQualifiedDotnetTypeName(),
                        includeContext ? ", TContext" : string.Empty,
                        ", TResult> ",
                        elseMatchParamName2);
            }

            if (thenDeclaration is null || elseDeclaration is null)
            {
                generator
                    .AppendLine(",")
                    .AppendIndent(
                        "Matcher<",
                        typeDeclaration.DotnetTypeName(),
                        includeContext ? ", TContext" : string.Empty,
                        ", TResult> defaultMatch");
            }

            generator
                .AppendLine(")")
                .PopIndent();

            if (includeContext)
            {
                generator
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .AppendLineIndent("where TContext : allows ref struct")
                    .AppendLine("#endif");
            }

            generator
                .AppendLineIndent("{")
                .PushIndent();

            if (ifSubschema.ReducedType.IsBuiltInJsonAnyType())
            {
                // Only ever need to evaluate the then clause
                if (thenDeclaration is SingleSubschemaKeywordTypeDeclaration thenDeclaration3 &&
                    thenMatchParamName is string thenMatchParam3)
                {
                    generator
                        .AppendLineIndent("return ", thenMatchParam3, "(", thenDeclaration3.ReducedType.FullyQualifiedDotnetTypeName(), forMutable ? ".Mutable" : "", ".From(this)", includeContext ? ", context" : string.Empty, ");");
                }
            }
            else if (ifSubschema.ReducedType.IsBuiltInJsonNotAnyType())
            {
                // Only ever need to evaluate the else clause
                if (elseDeclaration is SingleSubschemaKeywordTypeDeclaration elseDeclaration3 &&
                    elseMatchParamName is string elseMatchParam3)
                {
                    generator
                        .AppendLineIndent("return ", elseMatchParam3, "(", elseDeclaration3.ReducedType.FullyQualifiedDotnetTypeName(), forMutable ? ".Mutable" : "", ".From(this)", includeContext ? ", context" : string.Empty, ");");
                }
            }
            else
            {
                string matchTypeName = ifSubschema.ReducedType.FullyQualifiedDotnetTypeName();

                generator
                    .AppendSeparatorLine();

                string ifSubschemaTypeName = ifSubschema.ReducedType.FullyQualifiedDotnetTypeName();
                if (thenDeclaration is not null)
                {
                    generator
                        .AppendLineIndent("if (", ifSubschemaTypeName, ".", generator.JsonSchemaClassName(ifSubschemaTypeName), ".Evaluate(_parent, _idx))");
                }
                else
                {
                    generator
                        .AppendLineIndent("if (!", ifSubschemaTypeName, ".", generator.JsonSchemaClassName(ifSubschemaTypeName), ".Evaluate(_parent, _idx))");
                }

                if (thenDeclaration is SingleSubschemaKeywordTypeDeclaration thenDeclaration3 &&
                    thenMatchParamName is string thenMatchParam3)
                {
                    generator
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", thenMatchParam3, "(", thenDeclaration3.ReducedType.FullyQualifiedDotnetTypeName(), ".From(this)", includeContext ? ", context" : string.Empty, ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }

                if (elseDeclaration is SingleSubschemaKeywordTypeDeclaration elseDeclaration3 &&
                    elseMatchParamName is string elseMatchParam3)
                {
                    if (thenDeclaration is not null)
                    {
                        generator
                            .AppendLineIndent("else");
                    }

                    generator
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("return ", elseMatchParam3, "(", elseDeclaration3.ReducedType.FullyQualifiedDotnetTypeName(), ".From(this)", includeContext ? ", context" : string.Empty, ");")
                        .PopIndent()
                        .AppendLineIndent("}");
                }

                if (thenDeclaration is null || elseDeclaration is null)
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("return defaultMatch(this", includeContext ? ", context" : string.Empty, ");");
                }
            }

            generator
                .PopMemberScope()
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    /// <summary>
    /// Appends <c>TryGet()</c> methods for composition types.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="rootDeclaration">The type declaration which is the basis of the composition types.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendTryGetAsCompositionTypeMethods(
        this CodeGenerator generator,
        TypeDeclaration rootDeclaration,
        bool forMutable = false)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        HashSet<string> visitedTypes = new(StringComparer.Ordinal);

        foreach (TypeDeclaration t in rootDeclaration.CompositionTypeDeclarations())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (!visitedTypes.Add(t.FullyQualifiedDotnetTypeName()))
            {
                continue;
            }

            if (t.IsBuiltInJsonNotAnyType())
            {
                // You can never TryGetAs the not any type - it will always fail
                continue;
            }

            string methodName = generator.GetMethodNameInScope("TryGetAs", suffix: t.DotnetTypeName());
            string typeName = t.FullyQualifiedDotnetTypeName();
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Gets the value as a <see cref=\"", typeName, "\" />.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"result\">The result of the conversions.</param>")
                .AppendLineIndent("/// <returns><see langword=\"true\" /> if the conversion was valid.</returns>")
                .AppendLineIndent("public bool ", methodName, "(out ", typeName, " result)")
                .AppendLineIndent("{")
                .PushIndent();

            bool isBuiltInType = t.IsBuiltInJsonAnyType();

            if (!isBuiltInType)
            {
                generator
                        .AppendLineIndent("if (", typeName, ".", generator.JsonSchemaClassName(typeName), ".Evaluate(_parent, _idx))")
                        .AppendLineIndent("{")
                        .PushIndent();
            }

            generator
                        .AppendLineIndent("result = ", typeName, forMutable ? ".Mutable" : "", ".From(this);")
                        .AppendLineIndent("return true;");

            if (!isBuiltInType)
            {
                generator
                    .PopIndent()
                    .AppendLineIndent("}")
                    .AppendSeparatorLine()
                    .AppendLineIndent("result = default;")
                    .AppendLineIndent("return false;");
            }

            generator
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }
}