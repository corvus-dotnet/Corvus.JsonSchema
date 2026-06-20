// <copyright file="CodeGeneratorExtensions.Builder.cs" company="Endjin Limited">
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
/// Code generator extensions for builder pattern functionality.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends the builder pattern methods for the specified type declaration.
    /// </summary>
    /// <param name="generator">The generator to which to append the separator line.</param>
    /// <param name="typeDeclaration">The type declaration for which the builder is to be appended.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendSourceAndBuilder(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        List<ComposedBuilder> builders = [];

        return generator
            .AppendSourceRefStruct(typeDeclaration, builders)
            .AppendSourceOfContextRefStruct(typeDeclaration, builders)
            .AppendBuilderRefStruct(typeDeclaration, builders, forArray: true)
            .AppendBuilderRefStruct(typeDeclaration, builders, forArray: false)
            .AppendCommonBuild(typeDeclaration, builders)
            .AppendCommonCreateBuilder(typeDeclaration, builders);
    }

    private static CodeGenerator AppendAddAsItem(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, bool forContext = false)
    {
        HashSet<string> seenKinds = new(StringComparer.Ordinal);
        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        generator
            .ReserveNameIfNotReserved("AddAsItem")
            .AppendSeparatorLine()
            .AppendLineIndent("internal void AddAsItem(ref ComplexValueBuilder valueBuilder)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("switch(_kind)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("case Kind.Unknown:")
                    .PushIndent()
                        .AppendLineIndent("break;")
                    .PopIndent();

        if (forContext)
        {
            generator
                    .AppendLineIndent("case Kind.Source:")
                    .PushIndent()
                        .AppendLineIndent("_source.AddAsItem(ref valueBuilder);")
                        .AppendLineIndent("break;")
                    .PopIndent();
        }

        if (!forContext)
        {
            generator
                        .AppendLineIndent("case Kind.JsonElement:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_jsonElement);")
                            .AppendLineIndent("break;")
                        .PopIndent();

            if ((core & CoreTypes.Null) != 0)
            {
                if (seenKinds.Add("Null"))
                {
                    generator
                        .AppendLineIndent("case Kind.Null:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItemNull();")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.Boolean) != 0)
            {
                if (seenKinds.Add("True"))
                {
                    generator
                        .AppendLineIndent("case Kind.True:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(true);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("False"))
                {
                    generator
                        .AppendLineIndent("case Kind.False:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.String) != 0)
            {
                if (seenKinds.Add("RawUtf8StringRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_utf8Backing, escapeValue: false, requiresUnescaping: true);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("RawUtf8StringNotRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringNotRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_utf8Backing, escapeValue: false, requiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf8String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf8String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_utf8Backing, escapeValue: true, requiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf16String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf16String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_utf16Backing);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (typeDeclaration.Format() is string format &&
                    FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                    requiresSimpleType &&
                    seenKinds.Add("StringSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.StringSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItem(_simpleTypeBacking.Span(), escapeValue: false, requiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                if (seenKinds.Add("NumericSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.NumericSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItemFormattedNumber(_simpleTypeBacking.Span());")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("FormattedNumber"))
                {
                    generator.AppendLineIndent("case Kind.FormattedNumber:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItemFormattedNumber(_utf8Backing);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }
        }

        bool isObject = (core & CoreTypes.Object) != 0;
        bool isArray = (core & CoreTypes.Array) != 0;
        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            generator
                .AppendLineIndent("case Kind.", isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(), ":")
                .PushIndent();

            if (forContext)
            {
                generator
                        .AppendLineIndent("valueBuilder.AddItem(BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ", isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(), ".BuildValue(b.Context, b.Build, ref o));");
            }
            else
            {
                generator
                        .AppendLineIndent("valueBuilder.AddItem(_objectBuilder!, static (in b, ref o) => ", isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(), ".BuildValue(b, ref o));");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();

            if (EmitsCreateParamsBuildFor(typeDeclaration, forContext))
            {
                string createBuilderName = isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
                generator
                    .AppendLineIndent("case Kind.Create:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartItem();");
                AppendBuildCreateValueCall(generator, typeDeclaration, createBuilderName, forContext);
                generator
                        .AppendLineIndent("valueBuilder.EndItem(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        HashSet<string> numericArrayKinds = new(StringComparer.Ordinal);

        if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            generator
                .AppendLineIndent("case Kind.", isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(), ":")
                .PushIndent();

            if (forContext)
            {
                generator
                        .AppendLineIndent("valueBuilder.AddItem(BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ", isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(), ".BuildValue(b.Context, b.Build, ref o));");
            }
            else
            {
                generator
                        .AppendLineIndent("valueBuilder.AddItem(_arrayBuilder!, static (in b, ref o) => ", isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(), ".BuildValue(b, ref o));");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();

            if (!forContext && typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string numericArrayKindName = GetNumericArrayKind(generator, numericTypeName);
                if (numericArrayKinds.Add(numericArrayKindName))
                {
                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#if NET");
                    }

                    generator
                        .AppendLineIndent("case Kind.", numericArrayKindName, ":")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItemArrayValue(_", numericTypeName.Name, "Array!);")
                            .AppendLineIndent("break;")
                        .PopIndent();

                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#endif");
                    }
                }
            }

            // Handle tensor kind for fixed-size numeric arrays
            if (!forContext && typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string tensorBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#if NET");
                }

                generator
                    .AppendLineIndent("case Kind.Tensor:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartItem();")
                        .AppendLineIndent(tensorBuilderClassName, ".BuildTensorValue(_", numericTypeName.Name, "Tensor, ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndItem(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#endif");
                }
            }

            // Handle tuple kind for pure tuple types
            if (!forContext && typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForAddItem && !HasNotAnyTupleItem(tupleTypeForAddItem))
            {
                string tupleBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                generator
                    .AppendLineIndent("case Kind.Tuple:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartItem();")
                        .AppendIndent(tupleBuilderClassName, ".BuildTupleValue(");

                for (int i = 1; i <= tupleTypeForAddItem.ItemsTypes.Length; i++)
                {
                    if (i > 1)
                    {
                        generator.Append(", ");
                    }

                    generator.Append("_tupleItem").Append(i);
                }

                generator
                        .AppendLine(", ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndItem(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        foreach (ComposedBuilder composedBuilder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (composedBuilder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (composedBuilder.ObjectInstanceName is not null && composedBuilder.ObjectKindName is not null)
            {
                if (!(composedBuilder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
                {
                    if (seenKinds.Add(composedBuilder.ObjectKindName))
                    {
                        generator
                            .AppendLineIndent("case Kind.", composedBuilder.ObjectKindName, ":")
                            .PushIndent();

                        if (forContext)
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddItem(BuildWithContext.Create(_context, _", composedBuilder.ObjectInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName!, ".BuildValue(b.Context, b.Build, ref o));");
                        }
                        else
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddItem(_", composedBuilder.ObjectInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName!, ".BuildValue(b, ref o));");
                        }

                        generator
                                .AppendLineIndent("break;")
                            .PopIndent();
                    }
                }
            }

            if (!forContext && composedBuilder.SourceKindName is not null && composedBuilder.SourceInstanceName is not null && seenKinds.Add(composedBuilder.SourceKindName))
            {
                // #812: build the value by delegating to the stored constituent Source.
                generator
                    .AppendLineIndent("case Kind.", composedBuilder.SourceKindName, ":")
                    .PushIndent()
                        .AppendLineIndent("_", composedBuilder.SourceInstanceName, ".AddAsItem(ref valueBuilder);")
                        .AppendLineIndent("break;")
                    .PopIndent();
            }

            if (composedBuilder.ArrayInstanceName is not null && composedBuilder.ArrayKindName is not null)
            {
                if (seenKinds.Add(composedBuilder.ArrayKindName))
                {
                    generator
                        .AppendLineIndent("case Kind.", composedBuilder.ArrayKindName, ":")
                        .PushIndent();

                    if (forContext)
                    {
                        generator
                                .AppendLineIndent("valueBuilder.AddItem(BuildWithContext.Create(_context, _", composedBuilder.ArrayInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName!, ".BuildValue(b.Context, b.Build, ref o));");
                    }
                    else
                    {
                        generator
                                .AppendLineIndent("valueBuilder.AddItem(_", composedBuilder.ArrayInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName!, ".BuildValue(b, ref o));");
                    }

                    generator
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if (!forContext && composedBuilder.StringFormat is string format &&
                FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                requiresSimpleType &&
                seenKinds.Add("StringSimpleType"))
            {
                generator
                    .AppendLineIndent("case Kind.StringSimpleType:")
                    .PushIndent()
                        .AppendLineIndent("valueBuilder.AddItem(_simpleTypeBacking.Span(), escapeValue: false, requiresUnescaping: true);")
                        .AppendLineIndent("break;")
                    .PopIndent();
            }

            if (!forContext && composedBuilder.NumericArrayKindName is not null && composedBuilder.NumericArrayTypeName is not null)
            {
                if (numericArrayKinds.Add(composedBuilder.NumericArrayKindName))
                {
                    bool isNetOnly = composedBuilder.NumericArrayTypeName.Value.IsNetOnly;
                    if (isNetOnly)
                    {
                        generator.AppendLine("#if NET");
                    }

                    generator
                        .AppendLineIndent("case Kind.", composedBuilder.NumericArrayKindName, ":")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddItemArrayValue(_", composedBuilder.NumericArrayTypeName.Value.Name, "Array!);")
                            .AppendLineIndent("break;")
                        .PopIndent();

                    if (isNetOnly)
                    {
                        generator.AppendLine("#endif");
                    }
                }
            }
        }

        return generator
                    .AppendLineIndent("default:")
                    .PushIndent()
                        .AppendLineIndent("Debug.Fail(\"Unexpected Kind\");")
                        .AppendLineIndent("break;")
                    .PopIndent()
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddAsPrebakedProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, bool forContext = false)
    {
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("internal void AddAsPrebakedProperty(ReadOnlySpan<byte> prebakedPropertyName, ref ComplexValueBuilder valueBuilder)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("switch(_kind)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("case Kind.Unknown:")
                    .PushIndent()
                        .AppendLineIndent("break;")
                    .PopIndent();

        if (forContext)
        {
            generator
                    .AppendLineIndent("case Kind.Source:")
                    .PushIndent()
                        .AppendLineIndent("_source.AddAsPrebakedProperty(prebakedPropertyName, ref valueBuilder);")
                        .AppendLineIndent("break;")
                    .PopIndent();
        }

        HashSet<string> seenKinds = new(StringComparer.Ordinal);
        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if (!forContext)
        {
            generator
                    .AppendLineIndent("case Kind.JsonElement:")
                    .PushIndent()
                        .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _jsonElement);")
                        .AppendLineIndent("break;")
                    .PopIndent();

            if ((core & CoreTypes.Null) != 0)
            {
                if (seenKinds.Add("Null"))
                {
                    generator
                        .AppendLineIndent("case Kind.Null:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedPropertyNullValue(prebakedPropertyName);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.Boolean) != 0)
            {
                if (seenKinds.Add("True"))
                {
                    generator
                        .AppendLineIndent("case Kind.True:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, true);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("False"))
                {
                    generator
                        .AppendLineIndent("case Kind.False:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.String) != 0)
            {
                if (seenKinds.Add("RawUtf8StringRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _utf8Backing, escapeValue: false, valueRequiresUnescaping: true);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("RawUtf8StringNotRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringNotRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _utf8Backing, escapeValue: false, valueRequiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf8String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf8String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _utf8Backing, escapeValue: true, valueRequiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf16String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf16String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _utf16Backing);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (typeDeclaration.Format() is string format &&
                    FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                    requiresSimpleType &&
                    seenKinds.Add("StringSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.StringSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _simpleTypeBacking.Span(), escapeValue: false, valueRequiresUnescaping: false);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                if (seenKinds.Add("NumericSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.NumericSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedPropertyFormattedNumber(prebakedPropertyName, _simpleTypeBacking.Span());")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("FormattedNumber"))
                {
                    generator.AppendLineIndent("case Kind.FormattedNumber:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPrebakedPropertyFormattedNumber(prebakedPropertyName, _utf8Backing);")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }
        }

        bool isObject = (core & CoreTypes.Object) != 0;
        bool isArray = (core & CoreTypes.Array) != 0;

        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            string localBuilderName = isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
            generator
                .AppendLineIndent("case Kind.", localBuilderName, ":")
                .PushIndent();

            if (forContext)
            {
                generator
                        .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ", localBuilderName, ".BuildValue(b.Context, b.Build, ref o));");
            }
            else
            {
                generator
                        .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _objectBuilder!, static (in b, ref o) => ", localBuilderName, ".BuildValue(b, ref o));");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();

            if (EmitsCreateParamsBuildFor(typeDeclaration, forContext))
            {
                generator
                    .AppendLineIndent("case Kind.Create:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartPrebakedProperty(prebakedPropertyName);");
                AppendBuildCreateValueCall(generator, typeDeclaration, localBuilderName, forContext);
                generator
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            string localBuilderName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

            generator
                .AppendLineIndent("case Kind.", localBuilderName, ":")
                .PushIndent();

            if (forContext)
            {
                generator
                    .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ", localBuilderName, ".BuildValue(b.Context, b.Build, ref o));");
            }
            else
            {
                generator
                    .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _arrayBuilder!, static (in b, ref o) => ", localBuilderName, ".BuildValue(b, ref o));");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();
        }

        HashSet<string> numericArrayKinds = new(StringComparer.Ordinal);

        if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            if (!forContext && typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string numericArrayKindName = GetNumericArrayKind(generator, numericTypeName);
                if (numericArrayKinds.Add(numericArrayKindName))
                {
                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#if NET");
                    }

                    generator
                        .AppendLineIndent("case Kind.", numericArrayKindName, ":")
                        .PushIndent()
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartPrebakedProperty(prebakedPropertyName);")
                            .AppendLineIndent("valueBuilder.AddItemArrayValue(_", numericTypeName.Name, "Array!);")
                            .AppendLineIndent("valueBuilder.EndProperty(handle);")
                            .AppendLineIndent("break;")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .PopIndent();

                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#endif");
                    }
                }
            }

            if (!forContext && typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string tensorBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#if NET");
                }

                generator
                    .AppendLineIndent("case Kind.Tensor:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartPrebakedProperty(prebakedPropertyName);")
                        .AppendLineIndent(tensorBuilderClassName, ".BuildTensorValue(_", numericTypeName.Name, "Tensor, ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#endif");
                }
            }

            if (!forContext && typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForAddProp && !HasNotAnyTupleItem(tupleTypeForAddProp))
            {
                string tupleBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                generator
                    .AppendLineIndent("case Kind.Tuple:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartPrebakedProperty(prebakedPropertyName);");

                generator
                        .AppendIndent(tupleBuilderClassName, ".BuildTupleValue(");

                for (int i = 1; i <= tupleTypeForAddProp.ItemsTypes.Length; i++)
                {
                    if (i > 1)
                    {
                        generator.Append(", ");
                    }

                    generator.Append("_tupleItem").Append(i);
                }

                generator
                        .AppendLine(", ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        foreach (ComposedBuilder composedBuilder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (composedBuilder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (composedBuilder.ObjectInstanceName is not null && composedBuilder.ObjectKindName is not null && composedBuilder.ObjectBuilderName is not null)
            {
                if (!(composedBuilder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
                {
                    if (seenKinds.Add(composedBuilder.ObjectKindName))
                    {
                        generator
                            .AppendLineIndent("case Kind.", composedBuilder.ObjectKindName, ":")
                            .PushIndent();

                        if (forContext)
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, BuildWithContext.Create(_context, _", composedBuilder.ObjectInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName, ".BuildValue(b.Context, b.Build, ref o));");
                        }
                        else
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _", composedBuilder.ObjectInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName, ".BuildValue(b, ref o));");
                        }

                        generator
                                .AppendLineIndent("break;")
                            .PopIndent();
                    }
                }
            }

            if (!forContext && composedBuilder.SourceKindName is not null && composedBuilder.SourceInstanceName is not null && seenKinds.Add(composedBuilder.SourceKindName))
            {
                // #812: build the value by delegating to the stored constituent Source.
                generator
                    .AppendLineIndent("case Kind.", composedBuilder.SourceKindName, ":")
                    .PushIndent()
                        .AppendLineIndent("_", composedBuilder.SourceInstanceName, ".AddAsPrebakedProperty(prebakedPropertyName, ref valueBuilder);")
                        .AppendLineIndent("break;")
                    .PopIndent();
            }

            if (composedBuilder.ArrayInstanceName is not null && composedBuilder.ArrayKindName is not null && composedBuilder.ArrayBuilderName is not null)
            {
                if (seenKinds.Add(composedBuilder.ArrayKindName))
                {
                    generator
                        .AppendLineIndent("case Kind.", composedBuilder.ArrayKindName, ":")
                        .PushIndent();

                    if (forContext)
                    {
                        generator
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, BuildWithContext.Create(_context, _", composedBuilder.ArrayInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName, ".BuildValue(b.Context, b.Build, ref o));");
                    }
                    else
                    {
                        generator
                            .AppendLineIndent("valueBuilder.AddPrebakedProperty(prebakedPropertyName, _", composedBuilder.ArrayInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName, ".BuildValue(b, ref o));");
                    }

                    generator
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }
        }

        generator
                    .AppendLineIndent("default:")
                    .PushIndent()
                        .AppendLineIndent("Debug.Fail(\"Unexpected Kind\");")
                        .AppendLineIndent("break;")
                    .PopIndent()
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    private static CodeGenerator AppendAddAsProperty(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, string nameType, string nameName, bool includeEscaping, bool forContext = false)
    {
        generator
            .AppendSeparatorLine()
            .AppendLineIndent("internal void AddAsProperty(", nameType, " ", nameName, ", ref ComplexValueBuilder valueBuilder", includeEscaping ? ", bool escapeName = true, bool nameRequiresUnescaping = false" : "", ")")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("switch(_kind)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("case Kind.Unknown:")
                    .PushIndent()
                        .AppendLineIndent("break;")
                    .PopIndent();

        if (forContext)
        {
            generator
                    .AppendLineIndent("case Kind.Source:")
                    .PushIndent()
                        .AppendLineIndent("_source.AddAsProperty(", nameName, ", ref valueBuilder", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                        .AppendLineIndent("break;")
                    .PopIndent();
        }

        HashSet<string> seenKinds = new(StringComparer.Ordinal);
        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if (!forContext)
        {
            generator
                    .AppendLineIndent("case Kind.JsonElement:")
                    .PushIndent()
                        .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _jsonElement", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                        .AppendLineIndent("break;")
                    .PopIndent();

            if ((core & CoreTypes.Null) != 0)
            {
                if (seenKinds.Add("Null"))
                {
                    generator
                        .AppendLineIndent("case Kind.Null:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyNull(", nameName, includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.Boolean) != 0)
            {
                if (seenKinds.Add("True"))
                {
                    generator
                        .AppendLineIndent("case Kind.True:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", true", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("False"))
                {
                    generator
                        .AppendLineIndent("case Kind.False:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", false", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & CoreTypes.String) != 0)
            {
                if (seenKinds.Add("RawUtf8StringRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyRawString(", nameName, ", _utf8Backing, ", includeEscaping ? "escapeName, nameRequiresUnescaping, valueRequiresUnescaping: true" : "valueRequiresUnescaping: true", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("RawUtf8StringNotRequiresUnescaping"))
                {
                    generator
                        .AppendLineIndent("case Kind.RawUtf8StringNotRequiresUnescaping:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyRawString(", nameName, ", _utf8Backing, ", includeEscaping ? "escapeName, nameRequiresUnescaping, valueRequiresUnescaping: false" : "valueRequiresUnescaping: false", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf8String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf8String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _utf8Backing, ", includeEscaping ? "escapeName, escapeValue: true, nameRequiresUnescaping, valueRequiresUnescaping: false" : "escapeValue: true, valueRequiresUnescaping: false", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("Utf16String"))
                {
                    generator
                        .AppendLineIndent("case Kind.Utf16String:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _utf16Backing", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (typeDeclaration.Format() is string format &&
                    FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                    requiresSimpleType &&
                    seenKinds.Add("StringSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.StringSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _simpleTypeBacking.Span()", includeEscaping ? ", escapeName, escapeValue: false, nameRequiresUnescaping, valueRequiresUnescaping: false" : ", escapeValue: false, valueRequiresUnescaping: false", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                if (seenKinds.Add("NumericSimpleType"))
                {
                    generator
                        .AppendLineIndent("case Kind.NumericSimpleType:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyFormattedNumber(", nameName, ", _simpleTypeBacking.Span()", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }

                if (seenKinds.Add("FormattedNumber"))
                {
                    generator.AppendLineIndent("case Kind.FormattedNumber:")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyFormattedNumber(", nameName, ", _utf8Backing", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }
        }

        bool isObject = (core & CoreTypes.Object) != 0;
        bool isArray = (core & CoreTypes.Array) != 0;

        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            string builderName = isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
            generator
                .AppendLineIndent("case Kind.", builderName, ":")
                .PushIndent();

            if (forContext)
            {
                generator
                        .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", BuildWithContext.Create(_context, _objectBuilder!), static (in b, ref o) => ", builderName, ".BuildValue(b.Context, b.Build, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
            }
            else
            {
                generator
                        .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _objectBuilder!, static (in b, ref o) => ", builderName, ".BuildValue(b, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();

            if (EmitsCreateParamsBuildFor(typeDeclaration, forContext))
            {
                generator
                    .AppendLineIndent("case Kind.Create:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent();

                if (includeEscaping)
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ", escapeName, nameRequiresUnescaping);");
                }
                else
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ");");
                }

                AppendBuildCreateValueCall(generator, typeDeclaration, builderName, forContext);

                generator
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        HashSet<string> numericArrayKinds = new(StringComparer.Ordinal);

        if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            string builderName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

            generator
                .AppendLineIndent("case Kind.", builderName, ":")
                .PushIndent();

            if (forContext)
            {
                generator
                    .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", BuildWithContext.Create(_context, _arrayBuilder!), static (in b, ref o) => ", builderName, ".BuildValue(b.Context, b.Build, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
            }
            else
            {
                generator
                    .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _arrayBuilder!, static (in b, ref o) => ", builderName, ".BuildValue(b, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
            }

            generator
                    .AppendLineIndent("break;")
                .PopIndent();

            if (!forContext && typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string numericArrayKindName = GetNumericArrayKind(generator, numericTypeName);
                if (numericArrayKinds.Add(numericArrayKindName))
                {
                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#if NET");
                    }

                    generator
                        .AppendLineIndent("case Kind.", numericArrayKindName, ":")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyArrayValue(", nameName, ", _", numericTypeName.Name, "Array!", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();

                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#endif");
                    }
                }
            }

            // Handle tensor kind for fixed-size numeric arrays
            if (!forContext && typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                string tensorBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#if NET");
                }

                generator
                    .AppendLineIndent("case Kind.Tensor:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent();

                if (includeEscaping)
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ", escapeName, nameRequiresUnescaping);");
                }
                else
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ");");
                }

                generator
                        .AppendLineIndent(tensorBuilderClassName, ".BuildTensorValue(_", numericTypeName.Name, "Tensor, ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#endif");
                }
            }

            // Handle tuple kind for pure tuple types
            if (!forContext && typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForAddProp && !HasNotAnyTupleItem(tupleTypeForAddProp))
            {
                string tupleBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

                generator
                    .AppendLineIndent("case Kind.Tuple:")
                    .PushIndent()
                    .AppendLineIndent("{")
                    .PushIndent();

                if (includeEscaping)
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ", escapeName, nameRequiresUnescaping);");
                }
                else
                {
                    generator
                        .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = valueBuilder.StartProperty(", nameName, ");");
                }

                generator
                        .AppendIndent(tupleBuilderClassName, ".BuildTupleValue(");

                for (int i = 1; i <= tupleTypeForAddProp.ItemsTypes.Length; i++)
                {
                    if (i > 1)
                    {
                        generator.Append(", ");
                    }

                    generator.Append("_tupleItem").Append(i);
                }

                generator
                        .AppendLine(", ref valueBuilder);")
                        .AppendLineIndent("valueBuilder.EndProperty(handle);")
                        .AppendLineIndent("break;")
                    .PopIndent()
                    .AppendLineIndent("}")
                    .PopIndent();
            }
        }

        foreach (ComposedBuilder composedBuilder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (composedBuilder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (composedBuilder.ObjectInstanceName is not null && composedBuilder.ObjectKindName is not null && composedBuilder.ObjectBuilderName is not null)
            {
                if (!(composedBuilder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
                {
                    if (seenKinds.Add(composedBuilder.ObjectKindName))
                    {
                        generator
                            .AppendLineIndent("case Kind.", composedBuilder.ObjectKindName, ":")
                            .PushIndent();

                        if (forContext)
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", BuildWithContext.Create(_context, _", composedBuilder.ObjectInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName, ".BuildValue(b.Context, b.Build, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
                        }
                        else
                        {
                            generator
                                    .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _", composedBuilder.ObjectInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ObjectBuilderName, ".BuildValue(b, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
                        }

                        generator
                                .AppendLineIndent("break;")
                            .PopIndent();
                    }
                }
            }

            if (!forContext && composedBuilder.SourceKindName is not null && composedBuilder.SourceInstanceName is not null && seenKinds.Add(composedBuilder.SourceKindName))
            {
                // #812: build the value by delegating to the stored constituent Source.
                generator
                    .AppendLineIndent("case Kind.", composedBuilder.SourceKindName, ":")
                    .PushIndent()
                        .AppendLineIndent("_", composedBuilder.SourceInstanceName, ".AddAsProperty(", nameName, ", ref valueBuilder", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                        .AppendLineIndent("break;")
                    .PopIndent();
            }

            if (composedBuilder.ArrayInstanceName is not null && composedBuilder.ArrayKindName is not null && composedBuilder.ArrayBuilderName is not null)
            {
                if (seenKinds.Add(composedBuilder.ArrayKindName))
                {
                    generator
                        .AppendLineIndent("case Kind.", composedBuilder.ArrayKindName, ":")
                        .PushIndent();

                    if (forContext)
                    {
                        generator
                                .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", BuildWithContext.Create(_context, _", composedBuilder.ArrayInstanceName, "!), static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName, ".BuildValue(b.Context, b.Build, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
                    }
                    else
                    {
                        generator
                                .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _", composedBuilder.ArrayInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName, ".BuildValue(b, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");
                    }

                    generator
                            .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _", composedBuilder.ArrayInstanceName, "!, static (in b, ref o) => ", composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName(), ".", composedBuilder.ArrayBuilderName, ".BuildValue(b, ref o)", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");");

                    generator
                            .AppendLineIndent("break;")
                        .PopIndent();
                }
            }

            if (!forContext && composedBuilder.NumericArrayKindName is not null && composedBuilder.NumericArrayTypeName is not null)
            {
                if (numericArrayKinds.Add(composedBuilder.NumericArrayKindName))
                {
                    bool isNetOnly = composedBuilder.NumericArrayTypeName.Value.IsNetOnly;
                    if (isNetOnly)
                    {
                        generator.AppendLine("#if NET");
                    }

                    generator
                        .AppendLineIndent("case Kind.", composedBuilder.NumericArrayKindName, ":")
                        .PushIndent()
                            .AppendLineIndent("valueBuilder.AddPropertyArrayValue(", nameName, ", _", composedBuilder.NumericArrayTypeName.Value.Name, "Array!", includeEscaping ? ", escapeName, nameRequiresUnescaping" : "", ");")
                            .AppendLineIndent("break;")
                        .PopIndent();

                    if (isNetOnly)
                    {
                        generator.AppendLine("#endif");
                    }
                }
            }

            if (!forContext && composedBuilder.StringFormat is string format &&
                FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                requiresSimpleType &&
                seenKinds.Add("StringSimpleType"))
            {
                generator
                    .AppendLineIndent("case Kind.StringSimpleType:")
                    .PushIndent()
                        .AppendLineIndent("valueBuilder.AddProperty(", nameName, ", _simpleTypeBacking.Span()", includeEscaping ? ", escapeName, escapeValue: false, nameRequiresUnescaping, valueRequiresUnescaping: false" : ", escapeValue: false, valueRequiresUnescaping: false", ");")
                        .AppendLineIndent("break;")
                    .PopIndent();
            }
        }

        return generator
                    .AppendLineIndent("default:")
                    .PushIndent()
                        .AppendLineIndent("Debug.Fail(\"Unexpected Kind\");")
                        .AppendLineIndent("break;")
                    .PopIndent()
                .PopIndent()
                .AppendLineIndent("}")
            .PopIndent()
            .AppendLineIndent("}");
    }

    private static CodeGenerator AppendAddPropertyMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isAlsoArray)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        HashSet<string> seenTypes = new(StringComparer.Ordinal);

        bool seenFallback = false;
        bool seenLocalAndAppliedJsonNotAny = false;

        if (typeDeclaration.FallbackObjectPropertyType() is FallbackObjectPropertyType fallbackType)
        {
            seenFallback = true;
            if (!fallbackType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                string fqdtn = fallbackType.ReducedType.FullyQualifiedDotnetTypeName();
                if (seenTypes.Add(fqdtn))
                {
                    AppendAddPropertyMethods(generator, fqdtn, isAlsoArray, SupportsContextSource(fallbackType.ReducedType));
                }
            }
        }

        if (typeDeclaration.LocalEvaluatedPropertyType() is FallbackObjectPropertyType localFallbackType)
        {
            seenFallback = true;

            if (!localFallbackType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                string fqdtn = localFallbackType.ReducedType.FullyQualifiedDotnetTypeName();
                if (seenTypes.Add(fqdtn))
                {
                    AppendAddPropertyMethods(generator, fqdtn, isAlsoArray, SupportsContextSource(localFallbackType.ReducedType));
                }
            }
        }

        if (typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is FallbackObjectPropertyType localAndAppliedFallbackType)
        {
            seenFallback = true;
            if (!localAndAppliedFallbackType.ReducedType.IsBuiltInJsonNotAnyType())
            {
                string fqdtn = localAndAppliedFallbackType.ReducedType.FullyQualifiedDotnetTypeName();
                if (seenTypes.Add(fqdtn))
                {
                    AppendAddPropertyMethods(generator, fqdtn, isAlsoArray, SupportsContextSource(localAndAppliedFallbackType.ReducedType));
                }
            }
            else
            {
                seenLocalAndAppliedJsonNotAny = true;
            }
        }

        // Emit JsonElement fallback if no fallback was seen (no restriction), or if all
        // fallback types were JsonNotAny and there are pattern properties.
        // For LocalAndAppliedEvaluatedPropertyType (unevaluatedProperties), composed pattern
        // properties are also visible. For the other fallback types (additionalProperties),
        // only local pattern properties are visible.
        if (!seenFallback ||
            (seenTypes.Count == 0 &&
             (typeDeclaration.HasLocalPatternProperties() ||
              (seenLocalAndAppliedJsonNotAny && typeDeclaration.ImpliedPatternProperties()))))
        {
            if (seenTypes.Add("JsonElement"))
            {
                AppendAddPropertyMethods(generator, "JsonElement", isAlsoArray, supportsContextSource: true);
            }
        }

        return generator;

        static bool SupportsContextSource(TypeDeclaration typeDeclaration)
        {
            return (typeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0;
        }

        static void AppendAddPropertyMethods(CodeGenerator generator, string propertyTypeName, bool isAlsoArray, bool supportsContextSource)
        {
            AppendAddPropertyMethod(generator, propertyTypeName, isAlsoArray, "ReadOnlySpan<byte>", supportsContextSource);
            AppendAddPropertyMethod(generator, propertyTypeName, isAlsoArray, "ReadOnlySpan<char>", supportsContextSource);
            AppendAddPropertyMethod(generator, propertyTypeName, isAlsoArray, "string", supportsContextSource);
        }

        static void AppendAddPropertyMethod(CodeGenerator generator, string propertyTypeName, bool isAlsoArray, string nameType, bool supportsContextSource)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Add a property to the object.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"propertyName\">The name of the property to add.</param>")
                .AppendLineIndent("/// <param name=\"value\">The value of the property to add.</param>")
                .AppendLineIndent("public void AddProperty(", nameType, " propertyName, in ", propertyTypeName, ".", generator.SourceClassName(propertyTypeName), " value)")
                .AppendLineIndent("{")
                .PushIndent();

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("value.AddAsProperty(propertyName, ref _builder);")
                .PopIndent()
                .AppendLineIndent("}");

            if (!supportsContextSource)
            {
                return;
            }

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Add a property to the object.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"propertyName\">The name of the property to add.</param>")
                .AppendLineIndent("/// <param name=\"value\">The value of the property to add.</param>")
                .AppendLineIndent("public void AddProperty<TContext>(", nameType, " propertyName, in ", propertyTypeName, ".", generator.SourceClassName(propertyTypeName), "<TContext> value)")
                .AppendLine("#if NET9_0_OR_GREATER")
                .PushIndent()
                    .AppendLineIndent("where TContext : allows ref struct")
                .PopIndent()
                .AppendLine("#endif")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendSeparatorLine()
                    .AppendLineIndent("value.AddAsProperty(propertyName, ref _builder);")
                .PopIndent()
                .AppendLineIndent("}");
        }
    }

    private static CodeGenerator AppendArrayBuilders(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isObject)
    {
        bool allowsNonPrefixItems = !typeDeclaration.IsTuple();
        bool hasTuple = false;

        generator
            .AppendFixedSizeNumericArray(typeDeclaration, isObject);

        if (typeDeclaration.TupleType() is TupleTypeDeclaration tupleType && !HasNotAnyTupleItem(tupleType))
        {
            hasTuple = true;
            if (allowsNonPrefixItems)
            {
                generator
                    .ReserveName("_addedPrefixItems")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private bool _addedPrefixItems = false;");
            }

            generator
                .AppendSeparatorLine()
                .AppendCreateTuple(typeDeclaration, tupleType, allowsNonPrefixItems);
        }
        else if (typeDeclaration.ExplicitTupleType() is TupleTypeDeclaration tupleType2 && !HasNotAnyTupleItem(tupleType2))
        {
            hasTuple = true;
            if (allowsNonPrefixItems)
            {
                generator
                    .ReserveName("_addedPrefixItems")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private bool _addedPrefixItems = false;");
            }

            generator
                .AppendSeparatorLine()
                .AppendCreateTuple(typeDeclaration, tupleType2, allowsNonPrefixItems);
        }
        else if (typeDeclaration.ImplicitTupleType() is TupleTypeDeclaration tupleType3 && !HasNotAnyTupleItem(tupleType3))
        {
            hasTuple = true;
            if (allowsNonPrefixItems)
            {
                generator
                    .ReserveName("_addedPrefixItems")
                    .AppendSeparatorLine()
                    .AppendLineIndent("private bool _addedPrefixItems = false;");
            }

            generator
                .AppendSeparatorLine()
                .AppendCreateTuple(typeDeclaration, tupleType3, allowsNonPrefixItems);
        }

        if (allowsNonPrefixItems)
        {
            TypeDeclaration arrayItemsTypeDeclaration = typeDeclaration.ArrayItemsType()?.ReducedType ?? WellKnownTypeDeclarations.JsonAny;

            // You aren't allowed to create NotAny types.
            if (arrayItemsTypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                return generator;
            }

            string arrayItemsType = arrayItemsTypeDeclaration.FullyQualifiedDotnetTypeName();
            bool supportsContextSource = (arrayItemsTypeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0;

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Add an item to the array.")
                .AppendLineIndent("/// </summary>");

            if (hasTuple)
            {
                generator
                    .AppendLineIndent("/// <remarks>")
                    .AppendLineIndent("/// You must call <see cref=\"CreateTuple\"/> before adding additional items.")
                    .AppendLineIndent("/// </remarks>");
            }

            generator
                .AppendLineIndent("public void AddItem(in ", arrayItemsType, ".", generator.SourceClassName(arrayItemsType), " value)")
                .AppendLineIndent("{")
                .PushIndent();

            if (hasTuple)
            {
                // Note that we are already in the allowsNonPrefixItems case here, so we know we have added the _addedPrefixItems field.
                generator
                    .AppendLineIndent("if (!_addedPrefixItems)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("CodeGenThrowHelper.ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst();")
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("value.AddAsItem(ref _builder);")
                .PopIndent()
                .AppendLineIndent("}");

            if (supportsContextSource)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Add an item to the array.")
                    .AppendLineIndent("/// </summary>");

                if (hasTuple)
                {
                    generator
                        .AppendLineIndent("/// <remarks>")
                        .AppendLineIndent("/// You must call <see cref=\"CreateTuple\"/> before adding additional items.")
                        .AppendLineIndent("/// </remarks>");
                }

                generator
                    .AppendLineIndent("public void AddItem<TContext>(in ", arrayItemsType, ".", generator.SourceClassName(arrayItemsType), "<TContext> value)")
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .PushIndent()
                        .AppendLineIndent("where TContext : allows ref struct")
                    .PopIndent()
                    .AppendLine("#endif")
                    .AppendLineIndent("{")
                    .PushIndent();

                if (hasTuple)
                {
                    // Note that we are already in the allowsNonPrefixItems case here, so we know we have added the _addedPrefixItems field.
                    generator
                        .AppendLineIndent("if (!_addedPrefixItems)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("CodeGenThrowHelper.ThrowInvalidOperationException_PrefixTupleMustBeCreatedFirst();")
                        .PopIndent()
                        .AppendLineIndent("}");
                }

                generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("value.AddAsItem(ref _builder);")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        return generator;
    }

    /// <summary>
    /// Gets a value indicating whether building a single composed <c>oneOf</c> constituent always
    /// yields a valid instance of this type, so the constituent object-builder <c>Source</c> wiring
    /// may be emitted even though this type has its own properties (issue #812).
    /// </summary>
    /// <param name="typeDeclaration">The composing (union) type declaration.</param>
    /// <returns><see langword="true"/> for a discriminated union whose only required property is a const discriminator carried by every branch.</returns>
    /// <remarks>
    /// The constituent object-builder wiring is normally suppressed when the parent has its own
    /// properties, because building a single constituent might omit a property the parent requires.
    /// For a discriminated union that cannot happen: the parent requires only a const discriminator
    /// that every branch also requires, so any valid branch is a valid parent.
    /// </remarks>
    internal static bool ConstituentBuildYieldsValidInstance(this TypeDeclaration typeDeclaration)
    {
        if (!typeDeclaration.TryGetMetadata(nameof(ConstituentBuildYieldsValidInstance), out bool result))
        {
            result = Compute(typeDeclaration);
            typeDeclaration.SetMetadata(nameof(ConstituentBuildYieldsValidInstance), result);
        }

        return result;

        static bool Compute(TypeDeclaration typeDeclaration)
        {
            if (typeDeclaration.OneOfCompositionTypes() is not IReadOnlyDictionary<IOneOfSubschemaValidationKeyword, IReadOnlyCollection<TypeDeclaration>> oneOf
                || oneOf.Count == 0)
            {
                return false;
            }

            foreach (IReadOnlyCollection<TypeDeclaration> branches in oneOf.Values)
            {
                if (!CodeGenerationExtensions.TryGetOneOfDiscriminator(branches, out string? discriminator, out _, out _, requireRequired: true))
                {
                    return false;
                }

                // Every required local property of the parent must be the discriminator; otherwise a
                // constituent build could omit a property the parent requires.
                foreach (PropertyDeclaration property in typeDeclaration.PropertyDeclarations)
                {
                    if (property.LocalOrComposed == LocalOrComposed.Local
                        && property.RequiredOrOptional == RequiredOrOptional.Required
                        && property.JsonPropertyName != discriminator)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    private static CodeGenerator AppendSourceRefStruct(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        return generator
            .AppendSeparatorLine()
            .BeginRefStruct(GeneratedTypeAccessibility.Public, generator.SourceClassName(), isReadOnly: false)
                .CollectBuilderSourcesAndAppendSourceKindEnum(typeDeclaration, builders)
                .AppendSourceFields(typeDeclaration, builders)
                .AppendSourceConstructors(typeDeclaration, builders)
                .AppendSourceConversionOperators(typeDeclaration, builders)
                .AppendSourceFactoryMethods(typeDeclaration, builders)
                .AppendAddAsProperty(typeDeclaration, builders, "ReadOnlySpan<byte>", "utf8Name", includeEscaping: true)
                .AppendAddAsPrebakedProperty(typeDeclaration, builders)
                .AppendAddAsProperty(typeDeclaration, builders, "ReadOnlySpan<char>", "name", includeEscaping: false)
                .AppendAddAsProperty(typeDeclaration, builders, "string", "name", includeEscaping: false)
                .AppendAddAsItem(typeDeclaration, builders)
            .EndClassStructOrEnumDeclaration();
    }

    private static CodeGenerator AppendSourceOfContextRefStruct(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        if (!builders.Any(b => b.ArrayBuilderName is not null || b.ObjectBuilderName is not null) && (typeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) == 0)
        {
            return generator;
        }

        const string gs = """
            #if NET9_0_OR_GREATER
            where TContext : allows ref struct
            #endif
            """;

        return generator
            .AppendSeparatorLine()
            .BeginRefStruct(GeneratedTypeAccessibility.Public, $"{generator.SourceClassName()}<TContext>", isReadOnly: false, genericConstraints: gs)
                .AppendKindEnumForBuilders(typeDeclaration, builders)
                .AppendSourceFields(typeDeclaration, builders, forContext: true)
                .AppendSourceConstructors(typeDeclaration, builders, forContext: true)
                .AppendAddAsProperty(typeDeclaration, builders, "ReadOnlySpan<byte>", "utf8Name", includeEscaping: true, forContext: true)
                .AppendAddAsPrebakedProperty(typeDeclaration, builders, forContext: true)
                .AppendAddAsProperty(typeDeclaration, builders, "ReadOnlySpan<char>", "name", includeEscaping: false, forContext: true)
                .AppendAddAsProperty(typeDeclaration, builders, "string", "name", includeEscaping: false, forContext: true)
                .AppendAddAsItem(typeDeclaration, builders, forContext: true)
            .EndClassStructOrEnumDeclaration();
    }

    private static CodeGenerator AppendBuilderRefStruct(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, bool forArray)
    {
        bool forObject = !forArray;

        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        bool isArray = (core & CoreTypes.Array) != 0;
        bool isObject = (core & CoreTypes.Object) != 0;

        if (forArray && !isArray)
        {
            return generator;
        }

        if (forObject && !isObject)
        {
            return generator;
        }

        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (forArray && builders.Any(b => b.IsArray) && !hasFallbackArrayType)
        {
            return generator;
        }

        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;

        if (forObject && builders.Any(b => b.IsObject) && !hasFallbackObjectType)
        {
            return generator;
        }

        string builderClassName;

        if (forArray)
        {
            builderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();
        }
        else
        {
            builderClassName = isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
        }

        generator
                .AppendSeparatorLine()
                .BeginRefStruct(GeneratedTypeAccessibility.Public, builderClassName, isReadOnly: false)
                    .ReserveName("Build")
                    .ReserveName("_builder")
                    .AppendLineIndent("public delegate void Build(ref ", builderClassName, " builder);")
                    .AppendSeparatorLine()
                    .AppendLine("#if NET9_0_OR_GREATER")
                    .AppendLineIndent("public delegate void Build<TContext>(in TContext context, ref ", builderClassName, " builder)")
                    .PushIndent()
                        .AppendLineIndent("where TContext : allows ref struct;")
                    .PopIndent()
                    .AppendLine("#else")
                    .AppendLineIndent("public delegate void Build<TContext>(in TContext context, ref ", builderClassName, " builder);")
                    .AppendLine("#endif")
                    .AppendSeparatorLine()
                    .AppendLineIndent("internal ComplexValueBuilder _builder;")
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        internal {{builderClassName}}(ComplexValueBuilder builder)
                        {
                            _builder = builder;
                        }
                        """);

        if (forArray)
        {
            generator
                .AppendArrayBuilders(typeDeclaration, isObject);
        }

        if (forObject)
        {
            generator
                .AppendObjectBuilders(typeDeclaration, isArray, builders);
        }

        generator
            .ReserveName("BuildValue");

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("internal static void BuildValue(Build value, ref ComplexValueBuilder o)")
            .AppendLineIndent("{")
            .PushIndent();

        if (forArray)
        {
            generator
                .AppendLineIndent("o.StartArray();");
        }
        else
        {
            generator
                .AppendLineIndent("o.StartObject();");
        }

        generator
                .AppendSeparatorLine()
                .AppendLineIndent(builderClassName, " ovb = new(o);")
                .AppendLineIndent("value(ref ovb);")
                .AppendLineIndent("o = ovb._builder;");

        if (forArray)
        {
            generator
                .AppendLineIndent("o.EndArray();");
        }
        else
        {
            generator
                .AppendLineIndent("o.EndObject();");
        }

        generator
            .PopIndent()
            .AppendLineIndent("}");

        generator
    .AppendSeparatorLine()
    .AppendLineIndent("internal static void BuildValue<TContext>(in TContext context, Build<TContext> value, ref ComplexValueBuilder o)")
    .AppendLine("#if NET9_0_OR_GREATER")
    .PushIndent()
        .AppendLineIndent("where TContext : allows ref struct")
    .PopIndent()
    .AppendLine("#endif")
    .AppendLineIndent("{")
    .PushIndent();

        if (forArray)
        {
            generator
                .AppendLineIndent("o.StartArray();");
        }
        else
        {
            generator
                .AppendLineIndent("o.StartObject();");
        }

        generator
                .AppendSeparatorLine()
                .AppendLineIndent(builderClassName, " ovb = new(o);")
                .AppendLineIndent("value(context, ref ovb);")
                .AppendLineIndent("o = ovb._builder;");

        if (forArray)
        {
            generator
                .AppendLineIndent("o.EndArray();");
        }
        else
        {
            generator
                .AppendLineIndent("o.EndObject();");
        }

        generator
            .PopIndent()
            .AppendLineIndent("}");

        if (forObject)
        {
            generator.AppendBuildCreateValue(typeDeclaration);
        }

        return generator
            .EndClassStructOrEnumDeclaration();
    }

    private static CodeGenerator AppendCallStaticCreateWithBuilder(this CodeGenerator generator, MethodParameter[] parameters)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendIndent("Create(ref _builder");

        for (int i = 0; i < parameters.Length; ++i)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .Append(", ")
                .Append(parameters[i].GetName(generator));
        }

        return generator
            .AppendLine(");");
    }

    private static CodeGenerator AppendCallStaticCreateWithBuilderAndContext(this CodeGenerator generator, MethodParameter[] parameters)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
            .AppendIndent("Create(", parameters[0].GetName(generator), ", ref _builder");

        for (int i = 1; i < parameters.Length; ++i)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            generator
                .Append(", ")
                .Append(parameters[i].GetName(generator));
        }

        return generator
            .AppendLine(");");
    }

    /// <summary>
    /// Appends methods to create <c>JsonDocumentBuilder&lt;Mutable&gt;</c> instances for the specified type declaration.
    /// </summary>
    /// <param name="generator">The code generator to which to append the methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the document builder creation methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    private static CodeGenerator AppendCommonCreateBuilder(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        // We only expect 1 row for a simple type.
        int initialCapacity = 1;

        if ((typeDeclaration.ImpliedCoreTypes() & (CoreTypes.Object | CoreTypes.Array)) != 0)
        {
            // But we allow a default initial capacity of 30 for objects or arrays
            if (typeDeclaration.IsFixedSizeNumericArray())
            {
                // If this is a fixed size array, we use the value buffer size as the initial capacity
                initialCapacity = typeDeclaration.ArrayValueBufferSize() + (2 * typeDeclaration.ArrayRank()) ?? 30;
            }
            else
            {
                initialCapacity = 30;
            }
        }

        generator
            .ReserveNameIfNotReserved("CreateBuilder")
            .AppendSeparatorLine()
            .AppendBlockIndent(
            $$"""
            /// <summary>
            /// Creates and initializes a mutable document from a value.
            /// </summary>
            /// <param name="workspace">The JSON workspace.</param>
            /// <param name="value">The value with which to initialize the builder.</param>
            /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
            /// <returns>An instance of a mutable document initialized with the given value.</returns>
            public static JsonDocumentBuilder<{{generator.MutableClassName()}}> CreateBuilder(
                JsonWorkspace workspace, scoped in {{generator.SourceClassName()}} value, int initialCapacity = {{initialCapacity}})
            {
                // Create the document builder without a MetadataDb
                JsonDocumentBuilder<{{generator.MutableClassName()}}> documentBuilder = workspace.CreateBuilder<{{generator.MutableClassName()}}>(-1);
                ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);
                value.AddAsItem(ref cvb);
                Debug.Assert(cvb.MemberCount == 1);
                ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
                return documentBuilder;
            }
            """);

        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        bool isArray = (core & CoreTypes.Array) != 0;
        bool isObject = (core & CoreTypes.Object) != 0;

        // Emit parameterless CreateBuilder / CreateArrayBuilder / CreateObjectBuilder
        // that creates an empty array or object builder.
        // We do not emit this for tuple types (which have fixed prefix items)
        // or for object types with required properties, as an empty instance
        // would not be valid — unless the type has composition types (allOf/anyOf/oneOf)
        // which generate Apply() methods, allowing incremental construction.
        // We also skip the empty CreateBuilder for object types where the convenience
        // overload (AppendCreateBuilderFromProperties) would have all-default parameters,
        // as calling CreateBuilder(workspace) would be ambiguous between the two.
        // The convenience overload subsumes the empty case in that scenario.
        // We only know the convenience overload is definitely emitted when there is
        // more than one non-filtered optional property (the self-referencing guard in
        // AppendCreateBuilderFromProperties suppresses the overload for single-property
        // types that reference themselves).
        bool isTuple = typeDeclaration.IsTuple();
        bool hasRequiredProperties = typeDeclaration.PropertyDeclarations
            .Any(p => p.RequiredOrOptional != RequiredOrOptional.Optional);
        bool hasCompositionTypes =
            typeDeclaration.AllOfCompositionTypes().Values.SelectMany(t => t).Any(t => t.ReducedTypeDeclaration().ReducedType is not null) ||
            typeDeclaration.AnyOfCompositionTypes().Values.SelectMany(t => t).Any(t => t.ReducedTypeDeclaration().ReducedType is not null) ||
            typeDeclaration.OneOfCompositionTypes().Values.SelectMany(t => t).Any(t => t.ReducedTypeDeclaration().ReducedType is not null);
        int nonFilteredOptionalPropertyCount = typeDeclaration.PropertyDeclarations
            .Count(p => p.RequiredOrOptional == RequiredOrOptional.Optional &&
                        !p.ReducedPropertyType.IsBuiltInJsonNotAnyType());
        bool convenienceOverloadSubsumesEmpty =
            typeDeclaration.HasPropertyDeclarations && !hasRequiredProperties &&
            nonFilteredOptionalPropertyCount > 1;

        if (isArray && isObject)
        {
            if (!isTuple)
            {
                AppendEmptyCreateBuilder(generator, initialCapacity, "CreateArrayBuilder", "StartArray", "EndArray");
            }

            if (!hasRequiredProperties || hasCompositionTypes)
            {
                // CreateObjectBuilder uses a distinct method name, so no ambiguity
                // with AppendCreateBuilderFromProperties which emits CreateBuilder.
                AppendEmptyCreateBuilder(generator, initialCapacity, "CreateObjectBuilder", "StartObject", "EndObject");
            }
        }
        else if (isArray && !isTuple)
        {
            AppendEmptyCreateBuilder(generator, initialCapacity, "CreateBuilder", "StartArray", "EndArray");
        }
        else if (isObject && (!hasRequiredProperties || hasCompositionTypes) && !convenienceOverloadSubsumesEmpty)
        {
            AppendEmptyCreateBuilder(generator, initialCapacity, "CreateBuilder", "StartObject", "EndObject");
        }

        bool hasFallbackArrayType =
            typeDeclaration.ArrayItemsType() is not null;

        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;

        string sourceClassName = generator.SourceClassName();

        if (isArray && isObject)
        {
            if (hasFallbackArrayType && generator.ArrayBuilderClassName() is string arrayBuilderClassName)
            {
                AppendCreateBuilderForBuilder(generator, initialCapacity, sourceClassName, arrayBuilderClassName, forContextOnly: true);
            }

            if (hasFallbackObjectType && generator.ObjectBuilderClassName() is string objectBuilderClassName)
            {
                AppendCreateBuilderForBuilder(generator, initialCapacity, sourceClassName, objectBuilderClassName, forContextOnly: true);
            }
        }
        else
        {
            if (((isObject && hasFallbackObjectType) || (isArray && hasFallbackArrayType)) &&
                generator.BuilderClassName() is string builderClassName)
            {
                AppendCreateBuilderForBuilder(generator, initialCapacity, sourceClassName, builderClassName);
            }
        }

        foreach (ComposedBuilder builder in builders)
        {
            // Don't add them for built-in JsonNotAny types
            if (builder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (builder.IsArray && builder.ArrayBuilderName is string arrayBuilderClassName1)
            {
                AppendCreateBuilderForBuilder(generator, initialCapacity, $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{generator.SourceClassName(builder.TypeDeclaration.FullyQualifiedDotnetTypeName())}", $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{arrayBuilderClassName1}");
            }

            if (builder.IsObject && builder.ObjectBuilderName is string objectBuilderClassName1)
            {
                AppendCreateBuilderForBuilder(generator, initialCapacity, $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{generator.SourceClassName(builder.TypeDeclaration.FullyQualifiedDotnetTypeName())}", $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{objectBuilderClassName1}");
            }
        }

        // Add CreateBuilder(workspace, ReadOnlySpan<T>) overload for numeric arrays (non-tuple)
        if (typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple())
        {
            NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
            bool isFixedSize = typeDeclaration.IsFixedSizeNumericArray();

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#if NET");
            }

            if (isFixedSize)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        /// <summary>
                        /// Creates and initializes a mutable document from a flat numeric span.
                        /// </summary>
                        /// <param name="workspace">The JSON workspace.</param>
                        /// <param name="tensor">The data from which to create the tensor. It must contain exactly <see cref="ValueBufferSize"/> elements.</param>
                        /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                        /// <returns>An instance of a mutable document initialized with the given tensor values.</returns>
                        public static JsonDocumentBuilder<{{generator.MutableClassName()}}> CreateBuilder(
                            JsonWorkspace workspace, ReadOnlySpan<{{numericTypeName.Name}}> tensor, int initialCapacity = {{initialCapacity}})
                        {
                            return CreateBuilder(workspace, Build(tensor), initialCapacity);
                        }
                        """);
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        /// <summary>
                        /// Creates and initializes a mutable document from a numeric span.
                        /// </summary>
                        /// <param name="workspace">The JSON workspace.</param>
                        /// <param name="values">The numeric values from which to create the array.</param>
                        /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                        /// <returns>An instance of a mutable document initialized with the given values.</returns>
                        public static JsonDocumentBuilder<{{generator.MutableClassName()}}> CreateBuilder(
                            JsonWorkspace workspace, ReadOnlySpan<{{numericTypeName.Name}}> values, int initialCapacity = {{initialCapacity}})
                        {
                            return CreateBuilder(workspace, Build(values), initialCapacity);
                        }
                        """);
            }

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#endif");
            }
        }

        // Add CreateBuilder(workspace, in Source...) overload for pure tuple types
        if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForCreateBuilder && !HasNotAnyTupleItem(tupleTypeForCreateBuilder))
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Creates and initializes a mutable document from positional tuple item sources.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"workspace\">The JSON workspace.</param>");

            int cbDocIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForCreateBuilder.ItemsTypes)
            {
                cbDocIndex++;
                generator
                    .AppendLineIndent("/// <param name=\"item", cbDocIndex.ToString(), "\">The source for tuple item ", cbDocIndex.ToString(), ".</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"initialCapacity\">The (optional) estimate of the capacity to reserve for the document.</param>")
                .AppendLineIndent("/// <returns>An instance of a mutable document initialized with the given tuple values.</returns>")
                .AppendIndent("public static JsonDocumentBuilder<", generator.MutableClassName(), "> CreateBuilder(")
                .Append("JsonWorkspace workspace, ");

            int cbParamIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForCreateBuilder.ItemsTypes)
            {
                if (cbParamIndex > 0)
                {
                    generator.Append(", ");
                }

                cbParamIndex++;
                string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                generator
                    .Append("in ").Append(fqdtn).Append(".").Append(generator.SourceClassName(fqdtn)).Append(" item").Append(cbParamIndex);
            }

            generator
                .AppendLine(", int initialCapacity = ", initialCapacity.ToString(), ")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return CreateBuilder(workspace, Build(");

            for (int i = 1; i <= tupleTypeForCreateBuilder.ItemsTypes.Length; i++)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator.Append("item").Append(i);
            }

            generator
                    .AppendLine("), initialCapacity);")
                .PopIndent()
                .AppendLineIndent("}");
        }

        // Add CreateBuilder(workspace, sourceParams...) overload for object types with property declarations
        AppendCreateBuilderFromProperties(generator, typeDeclaration, initialCapacity);

        return generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Creates and initializes a mutable document from this instance.")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("/// <param name=\"workspace\">The JSON workspace.</param>")
            .AppendLineIndent("/// <returns>An instance of a mutable document initialized with this instance.</returns>")
            .AppendLineIndent("public JsonDocumentBuilder<", generator.MutableClassName(), "> CreateBuilder(JsonWorkspace workspace)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return workspace.CreateBuilder<", typeDeclaration.DotnetTypeName(), ", ", generator.MutableClassName(), ">(this);")
            .PopIndent()
            .AppendLineIndent("}");

        static void AppendCreateBuilderForBuilder(CodeGenerator generator, int initialCapacity, string sourceClassName, string builderClassName, bool forContextOnly = false)
        {
            if (!forContextOnly)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        /// <summary>
                        /// Creates and initializes a mutable document from a value.
                        /// </summary>
                        /// <param name="workspace">The JSON workspace.</param>
                        /// <param name="value">The value with which to initialize the builder.</param>
                        /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                        /// <param name="initialValueBufferSize">The initial size in bytes of the value buffer.</param>
                        /// <returns>An instance of a mutable document initialized with the given value.</returns>
                        public static JsonDocumentBuilder<{{generator.MutableClassName()}}> CreateBuilder(
                            JsonWorkspace workspace, scoped in {{builderClassName}}.Build value, int initialCapacity = {{initialCapacity}}, int initialValueBufferSize = 8192)
                        {
                            // Create the document builder without a MetadataDb
                            JsonDocumentBuilder<{{generator.MutableClassName()}}> documentBuilder = workspace.CreateBuilder<{{generator.MutableClassName()}}>(-1, initialValueBufferSize);
                            ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);
                            var source = new {{sourceClassName}}(value);
                            source.AddAsItem(ref cvb);
                            Debug.Assert(cvb.MemberCount == 1);
                            ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
                            return documentBuilder;
                        }
                        """);
            }

            generator
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    $$"""
                    /// <summary>
                    /// Creates and initializes a mutable document from a value.
                    /// </summary>
                    /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
                    /// <param name="workspace">The JSON workspace.</param>
                    /// <param name="context">The context to pass to the builder.</param>
                    /// <param name="value">The value with which to initialize the builder.</param>
                    /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                    /// <param name="initialValueBufferSize">The initial size in bytes of the value buffer.</param>
                    /// <returns>An instance of a mutable document initialized with the given value.</returns>
                    public static JsonDocumentBuilder<{{generator.MutableClassName()}}> CreateBuilder<TContext>(
                        JsonWorkspace workspace, scoped in TContext context, scoped in {{builderClassName}}.Build<TContext> value, int initialCapacity = {{initialCapacity}}, int initialValueBufferSize = 8192)
                        #if NET9_0_OR_GREATER
                        where TContext : allows ref struct
                        #endif
                    {
                        // Create the document builder without a MetadataDb
                        JsonDocumentBuilder<{{generator.MutableClassName()}}> documentBuilder = workspace.CreateBuilder<{{generator.MutableClassName()}}>(-1, initialValueBufferSize);
                        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);
                        var source = new {{sourceClassName}}<TContext>(context, value);
                        source.AddAsItem(ref cvb);
                        Debug.Assert(cvb.MemberCount == 1);
                        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
                        return documentBuilder;
                    }
                    """);
        }

        static void AppendEmptyCreateBuilder(CodeGenerator generator, int initialCapacity, string methodName, string startMethod, string endMethod)
        {
            generator
                .AppendSeparatorLine()
                .AppendBlockIndent(
                    $$"""
                    /// <summary>
                    /// Creates an empty mutable document builder.
                    /// </summary>
                    /// <param name="workspace">The JSON workspace.</param>
                    /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                    /// <param name="initialValueBufferSize">The initial size in bytes of the value buffer.</param>
                    /// <returns>An empty mutable document builder.</returns>
                    public static JsonDocumentBuilder<{{generator.MutableClassName()}}> {{methodName}}(
                        JsonWorkspace workspace, int initialCapacity = {{initialCapacity}}, int initialValueBufferSize = 8192)
                    {
                        JsonDocumentBuilder<{{generator.MutableClassName()}}> documentBuilder = workspace.CreateBuilder<{{generator.MutableClassName()}}>(-1, initialValueBufferSize);
                        ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);
                        cvb.{{startMethod}}();
                        cvb.{{endMethod}}();
                        ((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);
                        return documentBuilder;
                    }
                    """);
        }

        static void AppendCreateBuilderFromProperties(CodeGenerator generator, TypeDeclaration typeDeclaration, int initialCapacity)
        {
            if (generator.IsCancellationRequested)
            {
                return;
            }

            if (!typeDeclaration.HasPropertyDeclarations)
            {
                return;
            }

            CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();
            bool isArray = (core & CoreTypes.Array) != 0;
            bool isObject = (core & CoreTypes.Object) != 0;

            if (!isObject)
            {
                return;
            }

            // Determine the builder class name that contains the static Create method.
            // Builder is a peer of Mutable at the entity type level, not nested inside Mutable.
            string? builderClassName;
            if (isArray)
            {
                builderClassName = generator.ObjectBuilderClassName();
            }
            else
            {
                builderClassName = generator.BuilderClassName();
            }

            if (builderClassName is null)
            {
                return;
            }

            // Build the same method parameters as the Builder.Create method
            MethodParameter[] staticMethodParameters = BuildMethodParameters(generator, typeDeclaration);

            if (generator.IsCancellationRequested || staticMethodParameters.Length == 0)
            {
                return;
            }

            // Skip the first parameter (ref ComplexValueBuilder) to get the Source parameters
            MethodParameter[] sourceParameters = [.. staticMethodParameters.Skip(1)];

            if (sourceParameters.Length == 0)
            {
                return;
            }

            // If there's exactly one source parameter whose type is the containing type's own Source,
            // the convenience overload would collide with the existing CreateBuilder(workspace, Source, int).
            if (sourceParameters.Length == 1)
            {
                string containingTypeSource = typeDeclaration.FullyQualifiedDotnetTypeName() + "." + generator.SourceClassName();
                if (sourceParameters[0].Type == containingTypeSource)
                {
                    return;
                }
            }

            // Non-generic variant
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Creates and initializes a mutable document from the given property values.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"workspace\">The JSON workspace.</param>");

            foreach (MethodParameter p in sourceParameters)
            {
                generator.AppendLineIndent("/// <param name=\"", p.GetName(generator), "\">The value of the property.</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"initialCapacity\">The (optional) estimate of the capacity to reserve for the document.</param>")
                .AppendLineIndent("/// <returns>An instance of a mutable document initialized with the given property values.</returns>")
                .AppendIndent("public static JsonDocumentBuilder<", generator.MutableClassName(), "> CreateBuilder(JsonWorkspace workspace, ");

            for (int i = 0; i < sourceParameters.Length; i++)
            {
                if (i > 0)
                {
                    generator.Append(", ");
                }

                AppendParameterDeclaration(generator, sourceParameters[i]);
            }

            generator
                .AppendLine(", int initialCapacity = ", initialCapacity.ToString(), ")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("JsonDocumentBuilder<", generator.MutableClassName(), "> documentBuilder = workspace.CreateBuilder<", generator.MutableClassName(), ">(-1);")
                    .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);")
                    .AppendLineIndent("cvb.StartObject();")
                    .AppendLineIndent(builderClassName, " ovb = new(cvb);")
                    .AppendIndent("ovb.Create(");

            for (int i = 0; i < sourceParameters.Length; i++)
            {
                if (i > 0)
                {
                    generator.Append(", ");
                }

                generator.Append(sourceParameters[i].GetName(generator));
            }

            generator
                    .AppendLine(");")
                    .AppendLineIndent("cvb = ovb._builder;")
                    .AppendLineIndent("cvb.EndObject();")
                    .AppendLineIndent("((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);")
                    .AppendLineIndent("return documentBuilder;")
                .PopIndent()
                .AppendLineIndent("}");

            // TContext variant — only emit when there are object/array property types
            bool hasObjectOrArrayProperty = typeDeclaration.PropertyDeclarations.Any(p =>
                p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
                !p.ReducedPropertyType.IsBuiltInJsonNotAnyType() &&
                (p.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0);

            if (hasObjectOrArrayProperty)
            {
                MethodParameter[] staticMethodParametersWithContext = BuildMethodParametersWithContext(generator, typeDeclaration);

                // Skip the ComplexValueBuilder parameter (index 1) — keep context (index 0) and Source params (index 2+)
                MethodParameter[] sourceParametersWithContext = [staticMethodParametersWithContext[0], .. staticMethodParametersWithContext.Skip(2)];

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Creates and initializes a mutable document from the given property values.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the context to pass to the builder.</typeparam>")
                    .AppendLineIndent("/// <param name=\"workspace\">The JSON workspace.</param>");

                foreach (MethodParameter p in sourceParametersWithContext)
                {
                    generator.AppendLineIndent("/// <param name=\"", p.GetName(generator), "\">The value of the property.</param>");
                }

                generator
                    .AppendLineIndent("/// <param name=\"initialCapacity\">The (optional) estimate of the capacity to reserve for the document.</param>")
                    .AppendLineIndent("/// <returns>An instance of a mutable document initialized with the given property values.</returns>")
                    .AppendIndent("public static JsonDocumentBuilder<", generator.MutableClassName(), "> CreateBuilder<TContext>(JsonWorkspace workspace, ");

                for (int i = 0; i < sourceParametersWithContext.Length; i++)
                {
                    if (i > 0)
                    {
                        generator.Append(", ");
                    }

                    AppendParameterDeclaration(generator, sourceParametersWithContext[i]);
                }

                generator
                    .AppendLine(", int initialCapacity = ", initialCapacity.ToString(), ")")
                    .PushIndent()
                        .AppendLineIndent("#if NET9_0_OR_GREATER")
                        .AppendLineIndent("where TContext : allows ref struct")
                        .AppendLineIndent("#endif")
                    .PopIndent()
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("JsonDocumentBuilder<", generator.MutableClassName(), "> documentBuilder = workspace.CreateBuilder<", generator.MutableClassName(), ">(-1);")
                        .AppendLineIndent("ComplexValueBuilder cvb = ComplexValueBuilder.Create(documentBuilder, initialCapacity);")
                        .AppendLineIndent("cvb.StartObject();")
                        .AppendLineIndent(builderClassName, " ovb = new(cvb);")
                        .AppendIndent("ovb.Create(", sourceParametersWithContext[0].GetName(generator));

                for (int i = 1; i < sourceParametersWithContext.Length; i++)
                {
                    generator.Append(", ").Append(sourceParametersWithContext[i].GetName(generator));
                }

                generator
                        .AppendLine(");")
                        .AppendLineIndent("cvb = ovb._builder;")
                        .AppendLineIndent("cvb.EndObject();")
                        .AppendLineIndent("((IMutableJsonDocument)documentBuilder).SetAndDispose(ref cvb);")
                        .AppendLineIndent("return documentBuilder;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }

        static void AppendParameterDeclaration(CodeGenerator generator, MethodParameter parameter)
        {
            if (!string.IsNullOrEmpty(parameter.Modifiers))
            {
                generator.Append(parameter.Modifiers).Append(" ");
            }

            generator.Append(parameter.Type);

            if (parameter.TypeIsNullable)
            {
                generator.Append("?");
            }

            generator.Append(" ").Append(parameter.GetName(generator));

            if (parameter.DefaultValue is string defaultValue)
            {
                generator.Append(" = ").Append(defaultValue);
            }
        }
    }

    /// <summary>
    /// Appends methods to create <c>Source</c> and <c>Source&lt;TContext&gt;</c> instances for the specified type declaration.
    /// </summary>
    /// <param name="generator">The code generator to which to append the methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to emit the document builder creation methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    private static CodeGenerator AppendCommonBuild(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        // We only expect row for a simple type.
        const int initialCapacity = 1;

        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if ((core & (CoreTypes.Object | CoreTypes.Array)) != 0)
        {
            bool isArray = (core & CoreTypes.Array) != 0;
            bool isObject = (core & CoreTypes.Object) != 0;
            string sourceClassName = generator.SourceClassName();

            bool hasFallbackArrayType =
                typeDeclaration.ExplicitArrayItemsType() is not null;

            bool hasFallbackObjectType =
                typeDeclaration.LocalEvaluatedPropertyType() is not null ||
                typeDeclaration.HasPropertyDeclarations;

            if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
            {
                bool paramsBuildSuppressed = CreateParamsBuildSuppressed(typeDeclaration);
                AppendCreateBuild(generator, initialCapacity, sourceClassName, isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(), paramsBuildSuppressed, paramsBuildSuppressed && InCreateParamCycle(typeDeclaration));
                generator.AppendCreateParamsBuildFactory(typeDeclaration);
            }

            if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
            {
                AppendCreateBuild(generator, initialCapacity, sourceClassName, isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName());
            }
        }

        foreach (ComposedBuilder builder in builders)
        {
            // Don't add them for built-in JsonNotAny types
            if (builder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (builder.ArrayBuilderName is string arrayBuilderClassName1)
            {
                AppendCreateBuild(generator, initialCapacity, $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{generator.SourceClassName(builder.TypeDeclaration.FullyQualifiedDotnetTypeName())}", $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{arrayBuilderClassName1}");
            }

            if (builder.ObjectBuilderName is string objectBuilderClassName1)
            {
                AppendCreateBuild(generator, initialCapacity, $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{generator.SourceClassName(builder.TypeDeclaration.FullyQualifiedDotnetTypeName())}", $"{builder.TypeDeclaration.FullyQualifiedDotnetTypeName()}.{objectBuilderClassName1}");
            }
        }

        // Add Build(ReadOnlySpan<T>) factory method for numeric arrays (non-tuple)
        if (typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple())
        {
            NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
            string sourceClassName = generator.SourceClassName();
            bool isFixedSize = typeDeclaration.IsFixedSizeNumericArray();

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#if NET");
            }

            if (isFixedSize)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        /// <summary>
                        /// Build a tensor value from the given numeric span.
                        /// </summary>
                        /// <param name="tensor">The data from which to create the tensor. It must contain exactly <see cref="ValueBufferSize"/> elements.</param>
                        /// <returns>The source from which to build the value.</returns>
                        public static {{sourceClassName}} Build(ReadOnlySpan<{{numericTypeName.Name}}> tensor)
                        {
                            return new {{sourceClassName}}(tensor);
                        }
                        """);
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendBlockIndent(
                        $$"""
                        /// <summary>
                        /// Build an array value from the given numeric span.
                        /// </summary>
                        /// <param name="values">The numeric values from which to create the array.</param>
                        /// <returns>The source from which to build the value.</returns>
                        public static {{sourceClassName}} Build(ReadOnlySpan<{{numericTypeName.Name}}> values)
                        {
                            return new {{sourceClassName}}(values);
                        }
                        """);
            }

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#endif");
            }
        }

        // Add Build(in Source...) factory method for pure tuple types
        if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForFactory && !HasNotAnyTupleItem(tupleTypeForFactory))
        {
            string sourceClassName = generator.SourceClassName();

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Build a tuple value directly from its positional item sources.")
                .AppendLineIndent("/// </summary>");

            int docIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForFactory.ItemsTypes)
            {
                docIndex++;
                generator
                    .AppendLineIndent("/// <param name=\"item", docIndex.ToString(), "\">The source for tuple item ", docIndex.ToString(), ".</param>");
            }

            generator
                .AppendLineIndent("/// <returns>The source from which to build the value.</returns>")
                .AppendIndent("public static ", sourceClassName, " Build(");

            int paramIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForFactory.ItemsTypes)
            {
                if (paramIndex > 0)
                {
                    generator.Append(", ");
                }

                paramIndex++;
                string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                generator
                    .Append("in ").Append(fqdtn).Append(".").Append(generator.SourceClassName(fqdtn)).Append(" item").Append(paramIndex);
            }

            generator
                .AppendLine(")")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return new ", sourceClassName, "(");

            for (int i = 1; i <= tupleTypeForFactory.ItemsTypes.Length; i++)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator.Append("item").Append(i);
            }

            generator
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;

        static void AppendCreateBuild(CodeGenerator generator, int initialCapacity, string sourceClassName, string builderClassName, bool documentParamsBuildOmission = false, bool omittedDueToCycle = false)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Build an instance of the value.")
                .AppendLineIndent("/// </summary>");

            AppendOmissionRemark(generator, documentParamsBuildOmission, omittedDueToCycle);

            generator
                .AppendBlockIndent(
                    $$"""
                    /// <param name="buildValue">The callback that builds the value.</param>
                    /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                    /// <returns>The source from which to build the value.</returns>
                    public static {{sourceClassName}} Build(
                        scoped in {{builderClassName}}.Build buildValue, int initialCapacity = {{initialCapacity}})
                    {
                        return new {{sourceClassName}}(buildValue);
                    }
                    """);

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Build an instance of the value.")
                .AppendLineIndent("/// </summary>");

            AppendOmissionRemark(generator, documentParamsBuildOmission, omittedDueToCycle);

            generator
                .AppendBlockIndent(
                    $$"""
                    /// <typeparam name="TContext">The type of the context to pass to the builder.</typeparam>
                    /// <param name="context">The context to pass to the builder.</param>
                    /// <param name="buildValue">The callback that builds the value.</param>
                    /// <param name="initialCapacity">The (optional) estimate of the capacity to reserve for the document.</param>
                    /// <returns>The source from which to build the value.</returns>
                    public static {{sourceClassName}}<TContext> Build<TContext>(
                        scoped in TContext context, scoped in {{builderClassName}}.Build<TContext> buildValue, int initialCapacity = {{initialCapacity}})
                        #if NET9_0_OR_GREATER
                        where TContext : allows ref struct
                        #endif
                    {
                        return new {{sourceClassName}}<TContext>(context, buildValue);
                    }
                    """);
        }

        static void AppendOmissionRemark(CodeGenerator generator, bool documentParamsBuildOmission, bool omittedDueToCycle)
        {
            if (!documentParamsBuildOmission)
            {
                return;
            }

            if (omittedDueToCycle)
            {
                generator
                    .AppendBlockIndent(
                        """
                        /// <remarks>
                        /// <para>
                        /// To build this value without allocating a closure, use the <c>Build&lt;TContext&gt;</c>
                        /// overload with a <c>static</c> callback, capturing your source data in the context.
                        /// </para>
                        /// <para>
                        /// A <c>Build(...)</c> overload taking the individual property values directly is
                        /// intentionally not generated for this type, because it participates in a recursive
                        /// reference cycle that would otherwise produce a self-referential value type.
                        /// </para>
                        /// </remarks>
                        """);
            }
            else
            {
                generator
                    .AppendBlockIndent(
                        """
                        /// <remarks>
                        /// <para>
                        /// To build this value without allocating a closure, use the <c>Build&lt;TContext&gt;</c>
                        /// overload with a <c>static</c> callback, capturing your source data in the context.
                        /// </para>
                        /// <para>
                        /// A <c>Build(...)</c> overload taking the individual property values directly is
                        /// intentionally not generated for this type, because its estimated captured-argument
                        /// footprint exceeds the configured build-parameters threshold. The threshold is
                        /// configurable via <c>CSharpLanguageProvider.Options.BuildParametersThreshold</c>.
                        /// </para>
                        /// </remarks>
                        """);
            }
        }
    }

    private static CodeGenerator AppendCreateAddProperties(this CodeGenerator generator, MethodParameter[] parameters, PropertyDeclaration[] properties)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        int parameterIndex = 0;

        // The next parameter is the builder, so we grab the builder name
        // then start the parameter index up one more
        string builderName = parameters[parameterIndex++].GetName(generator);

        foreach (PropertyDeclaration property in properties)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (property.RequiredOrOptional != RequiredOrOptional.Optional)
            {
                parameterIndex = AppendRequiredProperty(generator, parameters, parameterIndex, property, builderName);
            }
            else
            {
                parameterIndex = AppendOptionalProperty(generator, parameters, parameterIndex, property, builderName);
            }
        }

        return generator;
    }

    private static bool HasNotAnyTupleItem(TupleTypeDeclaration tupleType)
    {
        foreach (ReducedTypeDeclaration item in tupleType.ItemsTypes)
        {
            if (item.ReducedType.IsBuiltInJsonNotAnyType())
            {
                return true;
            }
        }

        return false;
    }

    private static TupleTypeDeclaration? GetTupleType(TypeDeclaration typeDeclaration)
    {
        return
            typeDeclaration.TupleType() ??
            typeDeclaration.ExplicitTupleType() ??
            typeDeclaration.ImplicitTupleType();
    }

    private static CodeGenerator AppendCreateTuple(this CodeGenerator generator, TypeDeclaration typeDeclaration, TupleTypeDeclaration tupleType, bool allowsNonPrefixItems)
    {
        generator
            .AppendSeparatorLine()
            .AppendIndent("public void CreateTuple(in ");

        int index = 0;
        foreach (ReducedTypeDeclaration item in tupleType.ItemsTypes)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (index > 0)
            {
                generator.Append(", in ");
            }

            index++;

            string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
            generator
                .Append(fqdtn)
                .Append(".")
                .Append(generator.SourceClassName(fqdtn))
                .Append(" item")
                .Append(index);
        }

        generator
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent();

        for (int i = 1; i <= tupleType.ItemsTypes.Length; i++)
        {
            string indexStr = i.ToString();
            generator
                .AppendLineIndent("item", indexStr, ".AddAsItem(ref _builder);");
        }

        if (allowsNonPrefixItems)
        {
            generator
                .AppendLineIndent("_addedPrefixItems = true;");
        }

        generator
            .PopIndent()
            .AppendLineIndent("}");

        return generator;
    }

    private static CodeGenerator AppendFixedSizeNumericArray(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isObject)
    {
        if (typeDeclaration.IsFixedSizeNumericArray())
        {
            NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#if NET");
            }

            generator
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Creates a tensor from the given numeric span.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"tensor\">The data from which to create the tensor.</param>")
                .AppendLineIndent("/// <returns>The number of items consumed.</returns>")
                .AppendLineIndent("/// <exception cref=\"ArgumentException\">The tensor did not contain the correct number of values for the array rank and dimension.</exception>")
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public int CreateTensor(ReadOnlySpan<", numericTypeName.Name, "> tensor)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("    return CreateTensor(tensor, false);")
                .PopIndent()
                .AppendLineIndent("}");

            if (typeDeclaration.ArrayRank() > 1)
            {
                TypeDeclaration arrayItemsType = typeDeclaration.ArrayItemsType()!.ReducedType;
                bool isAlsoObject = (arrayItemsType.ImpliedCoreTypesOrAny() & CoreTypes.Object) != 0;

                string arrayItemsTypeName = arrayItemsType.FullyQualifiedDotnetTypeName() ?? throw new InvalidOperationException(SR.ExpectedArrayItemsTypeName);
                string builderClassName = isAlsoObject ? generator.ArrayBuilderClassName(arrayItemsTypeName) : generator.BuilderClassName(arrayItemsTypeName);
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Creates a tensor from the given numeric span.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <param name=\"tensor\">The data from which to create the tensor.</param>")
                    .AppendLineIndent("/// <param name=\"createArray\">Determines whether to create the wrapping array around the items.</param>")
                    .AppendLineIndent("/// <returns>The number of items consumed.</returns>")
                    .AppendLineIndent("/// <exception cref=\"ArgumentException\">The tensor did not contain the correct number of values for the array rank and dimension.</exception>")
                    .AppendLineIndent("internal int CreateTensor(ReadOnlySpan<", numericTypeName.Name, "> tensor, bool createArray)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendSeparatorLine()
                        .AppendLineIndent("int index = 0;")
                        .AppendLineIndent("if (tensor.Length != ValueBufferSize)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("CodeGenThrowHelper.ThrowArgumentException_ArrayBufferLength(nameof(tensor), ValueBufferSize);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (createArray)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("_builder.StartArray();")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("while (index < tensor.Length)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("ComplexValueBuilder.ComplexValueHandle handle = default;")
                            .AppendSeparatorLine()
                            .AppendLineIndent("handle = _builder.StartItem();")
                            .AppendLineIndent(arrayItemsTypeName, ".", builderClassName, " inner = new(_builder);")
                            .AppendLineIndent("index += inner.CreateTensor(tensor.Slice(index, ", arrayItemsTypeName, ".ValueBufferSize), createArray: true);")
                            .AppendLineIndent("_builder = inner._builder;")
                            .AppendSeparatorLine()
                            .AppendLineIndent("_builder.EndItem(handle);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (createArray)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("_builder.EndArray();")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("return ValueBufferSize;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("/// <summary>")
                    .AppendLineIndent("/// Creates a tensor from the given numeric span.")
                    .AppendLineIndent("/// </summary>")
                    .AppendLineIndent("/// <param name=\"tensor\">The data from which to create the tensor.</param>")
                    .AppendLineIndent("/// <param name=\"createArray\">Determines whether to create the wrapping array around the items.</param>")
                    .AppendLineIndent("/// <returns>The number of items consumed.</returns>")
                    .AppendLineIndent("/// <exception cref=\"ArgumentException\">The tensor did not contain the correct number of values for the array rank and dimension.</exception>")
                    .AppendLineIndent("internal int CreateTensor(ReadOnlySpan<", numericTypeName.Name, "> tensor, bool createArray)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("if (tensor.Length != ValueBufferSize)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("CodeGenThrowHelper.ThrowArgumentException_ArrayBufferLength(nameof(tensor), ValueBufferSize);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("if (createArray)")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("_builder.AddItemArrayValue(tensor);")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendLineIndent("else")
                        .AppendLineIndent("{")
                        .PushIndent()
                            .AppendLineIndent("foreach (")
                            .Append(numericTypeName.Name)
                            .Append(" item in tensor)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("_builder.AddItem(item);")
                            .PopIndent()
                            .AppendLineIndent("}")
                        .PopIndent()
                        .AppendLineIndent("}")
                        .AppendSeparatorLine()
                        .AppendLineIndent("return ValueBufferSize;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#endif");
            }

            // Add BuildTensorValue static method for use by Source.AddAsItem/AddAsProperty
            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#if NET");
            }

            string currentBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Builds the tensor value directly into the given complex value builder.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <param name=\"tensor\">The data from which to create the tensor.</param>")
                .AppendLineIndent("/// <param name=\"o\">The complex value builder into which to write the tensor.</param>")
                .AppendLineIndent("internal static void BuildTensorValue(ReadOnlySpan<", numericTypeName.Name, "> tensor, ref ComplexValueBuilder o)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("o.StartArray();")
                    .AppendSeparatorLine()
                    .AppendLineIndent(currentBuilderClassName, " b = new(o);")
                    .AppendLineIndent("b.CreateTensor(tensor);")
                    .AppendLineIndent("o = b._builder;")
                    .AppendSeparatorLine()
                    .AppendLineIndent("o.EndArray();")
                .PopIndent()
                .AppendLineIndent("}");

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#endif");
            }
        }

        // Add BuildTupleValue static method for pure tuple types
        if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForBTV && !HasNotAnyTupleItem(tupleTypeForBTV))
        {
            string currentBuilderClassName = isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Builds the tuple value directly into the given complex value builder.")
                .AppendLineIndent("/// </summary>");

            int btDocIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForBTV.ItemsTypes)
            {
                btDocIndex++;
                string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                generator
                    .AppendLineIndent("/// <param name=\"item", btDocIndex.ToString(), "\">The source for tuple item ", btDocIndex.ToString(), ".</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"o\">The complex value builder into which to write the tuple.</param>")
                .AppendIndent("internal static void BuildTupleValue(");

            int btParamIndex = 0;
            foreach (ReducedTypeDeclaration item in tupleTypeForBTV.ItemsTypes)
            {
                if (btParamIndex > 0)
                {
                    generator.Append(", ");
                }

                btParamIndex++;
                string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                generator
                    .Append("in ").Append(fqdtn).Append(".").Append(generator.SourceClassName(fqdtn)).Append(" item").Append(btParamIndex);
            }

            generator
                .AppendLine(", ref ComplexValueBuilder o)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("o.StartArray();")
                    .AppendSeparatorLine()
                    .AppendLineIndent(currentBuilderClassName, " b = new(o);");

            // Call CreateTuple with all item Sources
            generator
                    .AppendIndent("b.CreateTuple(");

            for (int i = 1; i <= tupleTypeForBTV.ItemsTypes.Length; i++)
            {
                if (i > 1)
                {
                    generator.Append(", ");
                }

                generator.Append("item").Append(i);
            }

            generator
                    .AppendLine(");")
                    .AppendLineIndent("o = b._builder;")
                    .AppendSeparatorLine()
                    .AppendLineIndent("o.EndArray();")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator AppendNumericArrayConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration, HashSet<string> seenArrayValues)
    {
        if (typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
        {
            NumericTypeName? arrayType = typeDeclaration.ArrayItemsType()?.ReducedType.PreferredDotnetNumericTypeName();

            if (arrayType is NumericTypeName at)
            {
                if (at.IsNetOnly)
                {
                    if (seenArrayValues.Add($"[{at.Name}]"))
                    {
                        generator
                            .AppendLine("#if NET")
                            .AppendLineIndent("internal ", generator.SourceClassName(), "(ReadOnlySpan<", at.Name, "> value)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("_", at.Name, "Array = value;")
                                .AppendLineIndent("_kind = Kind.", GetNumericArrayKind(generator, at), ";")
                            .PopIndent()
                            .AppendLineIndent("}")
                            .AppendLine("#endif");
                    }
                }
                else
                {
                    if (seenArrayValues.Add($"[{at.Name}]"))
                    {
                        generator
                            .AppendLineIndent("internal ", generator.SourceClassName(), "(ReadOnlySpan<", at.Name, "> value)")
                            .AppendLineIndent("{")
                            .PushIndent()
                                .AppendLineIndent("_", at.Name, "Array = value;")
                                .AppendLineIndent("_kind = Kind.", GetNumericArrayKind(generator, at), ";")
                            .PopIndent()
                            .AppendLineIndent("}");
                    }
                }
            }
        }

        return generator;
    }

    private static void AppendNumericArrayTypeFields(this CodeGenerator generator, TypeDeclaration typeDeclaration, HashSet<string> seenArrayValues)
    {
        if (typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
        {
            NumericTypeName? arrayType = typeDeclaration.ArrayItemsType()?.ReducedType.PreferredDotnetNumericTypeName();
            if (arrayType is NumericTypeName at)
            {
                if (at.IsNetOnly)
                {
                    if (seenArrayValues.Add($"[{at.Name}]"))
                    {
                        generator
                            .ReserveNameIfNotReserved($"_{at.Name}Array")
                            .AppendLine("#if NET")
                            .AppendLineIndent("private readonly ReadOnlySpan<", at.Name, "> _", at.Name, "Array;")
                            .AppendLine("#endif");
                    }
                }
                else
                {
                    if (seenArrayValues.Add($"[{at.Name}]"))
                    {
                        generator
                            .ReserveNameIfNotReserved($"_{at.Name}Array")
                            .AppendLineIndent("private readonly ReadOnlySpan<", at.Name, "> _", at.Name, "Array;");
                    }
                }
            }
        }
    }

    private static CodeGenerator AppendObjectBuilders(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isAlsoArray, List<ComposedBuilder> builders)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        return generator
            .AppendObjectCreateMethods(typeDeclaration, isAlsoArray, builders)
            .AppendAddPropertyMethod(typeDeclaration, isAlsoArray);
    }

    private static CodeGenerator AppendObjectCreateMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, bool isAlsoArray, List<ComposedBuilder> builders)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        if (!typeDeclaration.HasPropertyDeclarations)
        {
            return generator;
        }

        // The static method requires the builder
        MethodParameter[] staticMethodParameters = BuildMethodParameters(generator, typeDeclaration);

        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        PropertyDeclaration[] orderedProperties = BuildOrderedProperties(typeDeclaration);

        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        generator
                .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Creates an instance of a <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.")
            .AppendLineIndent("/// </summary>")
            .BeginReservedMethodDeclaration(
                "internal static",
                "void",
                "Create",
                staticMethodParameters);

        generator
                .AppendCreateAddProperties(staticMethodParameters, orderedProperties)
            .EndMethodDeclaration();

        MethodParameter[] nonStaticMethodParameters = [.. staticMethodParameters.Skip(1)];

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Creates an instance of a <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.")
            .AppendLineIndent("/// </summary>")
            .BeginReservedMethodDeclaration(
                "public",
                "void",
                "Create",
                nonStaticMethodParameters)
                .AppendCallStaticCreateWithBuilder(nonStaticMethodParameters)
            .EndMethodDeclaration();

        if ((typeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0 &&
            typeDeclaration.PropertyDeclarations.Any(p =>
                p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
                !p.ReducedPropertyType.IsBuiltInJsonNotAnyType() &&
                (p.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0))
        {
            MethodParameter[] staticMethodParametersWithContext = BuildMethodParametersWithContext(generator, typeDeclaration);
            MethodParameter[] nonStaticMethodParametersWithContext = [staticMethodParametersWithContext[0], .. staticMethodParametersWithContext.Skip(2)];

            generator
                    .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Creates an instance of a <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .BeginReservedMethodDeclaration(
                    "internal static",
                    "void",
                    "Create<TContext>",
                    """
                    #if NET9_0_OR_GREATER
                    where TContext : allows ref struct
                    #endif
                    """,
                    staticMethodParametersWithContext);

            generator
                    .AppendCreateAddProperties([.. staticMethodParametersWithContext.Skip(1)], orderedProperties)
                .EndMethodDeclaration();

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Creates an instance of a <see cref=\"", typeDeclaration.DotnetTypeName(), "\"/>.")
                .AppendLineIndent("/// </summary>")
                .BeginReservedMethodDeclaration(
                    "public",
                    "void",
                    "Create<TContext>",
                    """
                    #if NET9_0_OR_GREATER
                    where TContext : allows ref struct
                    #endif
                    """,
                    nonStaticMethodParametersWithContext)
                    .AppendCallStaticCreateWithBuilderAndContext(nonStaticMethodParametersWithContext)
                .EndMethodDeclaration();
        }

        return generator;
    }

    private static int AppendOptionalProperty(CodeGenerator generator, MethodParameter[] parameters, int parameterIndex, PropertyDeclaration property, string builderName)
    {
        if (generator.IsCancellationRequested)
        {
            return parameterIndex;
        }

        string propertyNamesClass = generator.JsonPropertyNamesPrebakedClassName();
        string parameterName = parameters[parameterIndex++].GetName(generator);

        generator
            .AppendLineIndent(
                parameterName,
                ".AddAsPrebakedProperty(",
                propertyNamesClass,
                ".",
                property.DotnetPropertyName(),
                ", ref ",
                builderName,
                ");");

        return parameterIndex;
    }

    private static int AppendRequiredProperty(CodeGenerator generator, MethodParameter[] parameters, int parameterIndex, PropertyDeclaration property, string builderName)
    {
        if (generator.IsCancellationRequested)
        {
            return parameterIndex;
        }

        string propertyNamesClass = generator.JsonPropertyNamesPrebakedClassName();
        if (property.ReducedPropertyType.SingleConstantValue().ValueKind != JsonValueKind.Undefined)
        {
            generator
                .AppendLineIndent(
                    builderName,
                    ".AddPrebakedProperty(",
                    propertyNamesClass,
                    ".",
                    property.DotnetPropertyName(),
                    ", ",
                    property.ReducedPropertyType.FullyQualifiedDotnetTypeName(),
                    ".ConstInstance);");
        }
        else
        {
            string parameterName = parameters[parameterIndex++].GetName(generator);

            generator
                .AppendLineIndent(
                    parameterName,
                    ".AddAsPrebakedProperty(",
                    propertyNamesClass,
                    ".",
                    property.DotnetPropertyName(),
                    ", ref ",
                    builderName,
                    ");");
        }

        return parameterIndex;
    }

    private static CodeGenerator AppendSourceConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, bool forContext = false)
    {
        HashSet<string> seenConstructorParameters = new(StringComparer.Ordinal);
        HashSet<string> seenNumericArrayTypes = new(StringComparer.Ordinal);
        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if (!forContext)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("private ", generator.SourceClassName(), "(JsonElement jsonElement)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("_jsonElement = jsonElement;")
                    .AppendLineIndent("_kind = jsonElement.ValueKind == JsonValueKind.Undefined ? Kind.Unknown : Kind.JsonElement;")
                .PopIndent()
                .AppendLineIndent("}");

            if ((core & CoreTypes.String) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private ", generator.SourceClassName(), "(ReadOnlySpan<byte> value)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("_utf8Backing = value;")
                        .AppendLineIndent("_kind = Kind.Utf8String;")
                    .PopIndent()
                    .AppendLineIndent("}");

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private ", generator.SourceClassName(), "(ReadOnlySpan<char> value)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("_utf16Backing = value;")
                        .AppendLineIndent("_kind = Kind.Utf16String;")
                    .PopIndent()
                    .AppendLineIndent("}");

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private ", generator.SourceClassName(), "(ReadOnlySpan<byte> value, bool requiresUnescaping)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("_utf8Backing = value;")
                        .AppendLineIndent("_kind = requiresUnescaping ? Kind.RawUtf8StringRequiresUnescaping : Kind.RawUtf8StringNotRequiresUnescaping;")
                    .PopIndent()
                    .AppendLineIndent("}");

                if (typeDeclaration.Format() is string format)
                {
                    FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatSourceConstructors(generator, typeDeclaration, format, seenConstructorParameters);
                }
            }

            if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("private ", generator.SourceClassName(), "(ReadOnlySpan<byte> value, Kind kind)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("Debug.Assert(kind is Kind.FormattedNumber);")
                        .AppendLineIndent("_utf8Backing = value;")
                        .AppendLineIndent("_kind = kind;")
                    .PopIndent()
                    .AppendLineIndent("}");

                if (typeDeclaration.Format() is not string format ||
                    !FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatSourceConstructors(generator, typeDeclaration, format, seenConstructorParameters))
                {
                    // There were no format-specific constructors, so we fall back to a default of double for number,
                    // and long for integer.
                    if ((core & CoreTypes.Number) != 0)
                    {
                        if (seenConstructorParameters.Add("double"))
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendLineIndent("private ", generator.SourceClassName(), "(double value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (isAlsoArray, buffer, out written) => Utf8Formatter.TryFormat(isAlsoArray, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                        }

                        // Also emit int and long constructors so integer values use the cheaper
                        // integer formatting path instead of widening to double.
                        if (seenConstructorParameters.Add("int"))
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendLineIndent("private ", generator.SourceClassName(), "(int value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (isAlsoArray, buffer, out written) => Utf8Formatter.TryFormat(isAlsoArray, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                        }

                        if (seenConstructorParameters.Add("long"))
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendLineIndent("private ", generator.SourceClassName(), "(long value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (isAlsoArray, buffer, out written) => Utf8Formatter.TryFormat(isAlsoArray, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                        }
                    }
                    else
                    {
                        if (seenConstructorParameters.Add("long"))
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendLineIndent("private ", generator.SourceClassName(), "(long value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (isAlsoArray, buffer, out written) => Utf8Formatter.TryFormat(isAlsoArray, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                        }

                        // Also emit int constructor for integer types so int values
                        // don't widen to long unnecessarily.
                        if (seenConstructorParameters.Add("int"))
                        {
                            generator
                                .AppendSeparatorLine()
                                .AppendLineIndent("private ", generator.SourceClassName(), "(int value) { SimpleTypesBacking.Initialize(ref _simpleTypeBacking, value, static (isAlsoArray, buffer, out written) => Utf8Formatter.TryFormat(isAlsoArray, buffer, out written)); _kind = Kind.NumericSimpleType; }");
                        }
                    }
                }
            }

            if ((core & CoreTypes.Boolean) != 0)
            {
                if (seenConstructorParameters.Add("bool"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private ", generator.SourceClassName(), "(bool value) { _kind = value ? Kind.True : Kind.False; }");
                }
            }

            if ((core & CoreTypes.Null) != 0)
            {
                if (seenConstructorParameters.Add("null"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("private ", generator.SourceClassName(), "(Kind kind) { Debug.Assert(kind == Kind.Null); _kind = Kind.Null; }");
                }
            }

            generator
                .AppendNumericArrayConstructors(typeDeclaration, seenNumericArrayTypes);

            // Add tensor constructor for fixed-size numeric arrays
            if (typeDeclaration.IsFixedSizeNumericArray())
            {
                NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#if NET");
                }

                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("internal ", generator.SourceClassName(), "(ReadOnlySpan<", numericTypeName.Name, "> value)")
                    .AppendLineIndent("{")
                    .PushIndent()
                        .AppendLineIndent("_", numericTypeName.Name, "Tensor = value;")
                        .AppendLineIndent("_kind = Kind.Tensor;")
                    .PopIndent()
                    .AppendLineIndent("}");

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#endif");
                }
            }

            // Add tuple constructor for pure tuple types
            if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForCtor && !HasNotAnyTupleItem(tupleTypeForCtor))
            {
                generator
                    .AppendSeparatorLine()
                    .AppendIndent("internal ", generator.SourceClassName(), "(");

                int ctorIndex = 0;
                foreach (ReducedTypeDeclaration item in tupleTypeForCtor.ItemsTypes)
                {
                    if (ctorIndex > 0)
                    {
                        generator.Append(", ");
                    }

                    ctorIndex++;
                    string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                    generator
                        .Append("in ").Append(fqdtn).Append(".").Append(generator.SourceClassName(fqdtn)).Append(" item").Append(ctorIndex);
                }

                generator
                    .AppendLine(")")
                    .AppendLineIndent("{")
                    .PushIndent();

                for (int i = 1; i <= tupleTypeForCtor.ItemsTypes.Length; i++)
                {
                    string indexStr = i.ToString();
                    generator
                        .AppendLineIndent("_tupleItem", indexStr, " = item", indexStr, ";");
                }

                generator
                        .AppendLineIndent("_kind = Kind.Tuple;")
                    .PopIndent()
                    .AppendLineIndent("}");
            }
        }
        else
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("private ", generator.SourceClassName(), "(", generator.SourceClassName(), " source) { _kind = Kind.Source; _context = default!; _source = source; }")
                .AppendSeparatorLine()
                .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "<TContext>(", generator.SourceClassName(), " source) => new (source);");
        }

        string buildContextType = forContext ? "Build<TContext>" : "Build";

        // This is the "has builder" case
        if ((core & (CoreTypes.Array | CoreTypes.Object)) != 0)
        {
            bool isArray = (core & CoreTypes.Array) != 0;
            bool isObject = (core & CoreTypes.Object) != 0;

            bool hasFallbackObjectType =
                typeDeclaration.LocalEvaluatedPropertyType() is not null ||
                typeDeclaration.HasPropertyDeclarations;
            bool hasFallbackArrayType =
                typeDeclaration.ExplicitArrayItemsType() is not null;

            if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
            {
                string fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "internal ", generator.SourceClassName(), "(",
                        forContext ? "scoped in TContext context, " : "",
                         fqdtn,
                        ".",
                        isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(),
                        ".", buildContextType, " value) {", forContext ? "_context = context; " : "", "_objectBuilder = value; _kind = Kind.",
                        isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(),
                        "; }");

                // Capturing constructor for the property-parameter Build(...) overload.
                if (EmitsCreateParamsBuildFor(typeDeclaration, forContext))
                {
                    List<(string Type, bool IsOptional, string JsonName)> createArgs = CreateParamSourceTypes(generator, typeDeclaration, forContext);

                    generator
                        .AppendSeparatorLine()
                        .AppendIndent("internal ", generator.SourceClassName(), "(", forContext ? "scoped in TContext context" : "");

                    if (createArgs.Count > 0)
                    {
                        if (forContext)
                        {
                            generator.Append(", ");
                        }

                        AppendCreateArgDeclarations(generator, createArgs, includeDefaults: false);
                    }

                    generator
                        .AppendLine(")")
                        .AppendLineIndent("{")
                        .PushIndent();

                    if (forContext)
                    {
                        generator.AppendLineIndent("_context = context;");
                    }

                    for (int i = 0; i < createArgs.Count; i++)
                    {
                        generator.AppendLineIndent("_createArg", (i + 1).ToString(), " = arg", (i + 1).ToString(), ";");
                    }

                    generator
                            .AppendLineIndent("_kind = Kind.Create;")
                        .PopIndent()
                        .AppendLineIndent("}");
                }
            }

            if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
            {
                string fqdtn = typeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "internal ", generator.SourceClassName(), "(",
                        forContext ? "scoped in TContext context, " : "",
                        fqdtn,
                        ".",
                        isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(),
                        ".", buildContextType, " value) {", forContext ? "_context = context; " : "", "_arrayBuilder = value; _kind = Kind.",
                        isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(),
                        "; }");
            }
        }

        foreach (ComposedBuilder composedBuilder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (composedBuilder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            generator
                .AppendNumericArrayConstructors(composedBuilder.TypeDeclaration, seenNumericArrayTypes);

            // Format-specific constructors are only for the non-context Source struct
            if (!forContext && composedBuilder.TypeDeclaration.Format() is string format)
            {
                CoreTypes composedCore = composedBuilder.TypeDeclaration.ImpliedCoreTypesOrAny();
                if ((composedCore & (CoreTypes.Number | CoreTypes.Integer)) != 0)
                {
                    FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatSourceConstructors(generator, composedBuilder.TypeDeclaration, format, seenConstructorParameters);
                }

                if ((composedCore & CoreTypes.String) != 0)
                {
                    FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatSourceConstructors(generator, composedBuilder.TypeDeclaration, format, seenConstructorParameters);
                }
            }

            if (composedBuilder.ObjectInstanceName is not null && composedBuilder.ObjectKindName is not null)
            {
                if (!(composedBuilder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
                {
                    string fqdtn = composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent(
                            "public ", generator.SourceClassName(), "(",
                            forContext ? "scoped in TContext context, " : "",
                            fqdtn,
                            ".",
                            composedBuilder.IsArray ? generator.ObjectBuilderClassName(fqdtn) : generator.BuilderClassName(fqdtn),
                            ".", buildContextType, " value) {", forContext ? "_context = context; " : "", "_",
                            composedBuilder.ObjectInstanceName,
                            " = value; _kind = Kind.",
                            composedBuilder.ObjectKindName,
                            "; }");
                }
            }

            if (!forContext && composedBuilder.SourceInstanceName is not null && composedBuilder.SourceKindName is not null)
            {
                // #812: construct this union's Source from a constituent's own Source (e.g. the result
                // of Constituent.Build(...)). The parameter is by value (not 'in'): the implicit
                // conversion operator passes a by-value local, so a by-value copy keeps it ref-safe.
                string fqdtn = composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "public ", generator.SourceClassName(), "(",
                        fqdtn,
                        ".",
                        generator.SourceClassName(fqdtn),
                        " value) { _",
                        composedBuilder.SourceInstanceName,
                        " = value; _kind = Kind.",
                        composedBuilder.SourceKindName,
                        "; }");
            }

            if (composedBuilder.ArrayInstanceName is not null && composedBuilder.ArrayKindName is not null)
            {
                string fqdtn = composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent(
                        "public ", generator.SourceClassName(), "(",
                        forContext ? "scoped in TContext context, " : "",
                        fqdtn,
                        ".",
                        composedBuilder.IsObject ? generator.ArrayBuilderClassName(fqdtn) : generator.BuilderClassName(fqdtn),
                        ".", buildContextType, " value) {", forContext ? "_context = context; " : "", "_",
                        composedBuilder.ArrayInstanceName,
                        " = value; _kind = Kind.",
                        composedBuilder.ArrayKindName,
                        "; }");
            }
        }

        return generator;
    }

    private static CodeGenerator AppendSourceConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        HashSet<string> seenConversionOperators = new(StringComparer.Ordinal);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(", typeDeclaration.DotnetTypeName(), " instance) => new(JsonElement.From(instance));");

        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if ((core & CoreTypes.String) != 0)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(ReadOnlySpan<byte> value) => new (value);")
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(ReadOnlySpan<char> value) => new (value);")
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(string value) => new (value.AsSpan());");

            if (typeDeclaration.Format() is string format)
            {
                FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatSourceConversionOperators(generator, typeDeclaration, format, seenConversionOperators);
            }
        }

        bool hasNumericBuilder = builders.Any(b => (b.TypeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Number | CoreTypes.Integer)) != 0);
        if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0 && !hasNumericBuilder)
        {
            if (typeDeclaration.Format() is not string format ||
                !FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatSourceConversionOperators(generator, typeDeclaration, format, seenConversionOperators))
            {
                // There were no format-specific constructors, so we fall back to a default of double for number,
                // and long for integer.
                if ((core & CoreTypes.Number) != 0)
                {
                    if (seenConversionOperators.Add("double"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(double value) => new (value);");
                    }

                    // Also emit int and long operators so integer values use the cheaper
                    // integer formatting path instead of widening to double.
                    if (seenConversionOperators.Add("int"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(int value) => new (value);");
                    }

                    if (seenConversionOperators.Add("long"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(long value) => new (value);");
                    }
                }
                else
                {
                    if (seenConversionOperators.Add("long"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(long value) => new (value);");
                    }

                    // Also emit int operator for integer types so int values
                    // don't widen to long unnecessarily.
                    if (seenConversionOperators.Add("int"))
                    {
                        generator
                            .AppendSeparatorLine()
                            .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(int value) => new (value);");
                    }
                }
            }
        }

        if ((core & CoreTypes.Boolean) != 0)
        {
            if (seenConversionOperators.Add("bool"))
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(bool value) => new (value);");
            }
        }

        // Implicit conversion from ReadOnlySpan<T> for numeric array types
        HashSet<string> seenArrayConversions = new(StringComparer.Ordinal);
        generator.AppendNumericArrayImplicitOperator(typeDeclaration, seenArrayConversions);
        foreach (ComposedBuilder cb in builders)
        {
            generator.AppendNumericArrayImplicitOperator(cb.TypeDeclaration, seenArrayConversions);
        }

        foreach (ComposedBuilder composedBuilder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (composedBuilder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (composedBuilder.TypeDeclaration.Format() is string format)
            {
                CoreTypes composedCore = composedBuilder.TypeDeclaration.ImpliedCoreTypesOrAny();
                if ((composedCore & (CoreTypes.Number | CoreTypes.Integer)) != 0)
                {
                    FormatHandlerRegistry.Instance.NumberFormatHandlers.AppendFormatSourceConversionOperators(generator, composedBuilder.TypeDeclaration, format, seenConversionOperators);
                }

                if ((composedCore & CoreTypes.String) != 0)
                {
                    FormatHandlerRegistry.Instance.StringFormatHandlers.AppendFormatSourceConversionOperators(generator, composedBuilder.TypeDeclaration, format, seenConversionOperators);
                }
            }

            if (!(composedBuilder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
            {
                string fqdtn = composedBuilder.TypeDeclaration.FullyQualifiedDotnetTypeName();

                if (seenConversionOperators.Add(fqdtn))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent(
                                "public static implicit operator ", generator.SourceClassName(), "(",
                                fqdtn,
                                " instance) => new(JsonElement.From(instance));");
                }

                // #812: also convert the constituent's own Source (e.g. the result of
                // Constituent.Build(...)) so it can be passed wherever this union's Source is expected.
                if (composedBuilder.SourceKindName is not null && seenConversionOperators.Add($"{fqdtn}.{generator.SourceClassName(fqdtn)}"))
                {
                    generator
                        .AppendSeparatorLine()
                        .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                            .AppendLineIndent(
                                "public static implicit operator ", generator.SourceClassName(), "(",
                                fqdtn,
                                ".",
                                generator.SourceClassName(fqdtn),
                                " value) => new(value);");
                }
            }
        }

        return generator;
    }

    private static void AppendNumericArrayImplicitOperator(this CodeGenerator generator, TypeDeclaration typeDeclaration, HashSet<string> seenConversions)
    {
        if (!typeDeclaration.IsNumericArray() || typeDeclaration.IsTuple())
        {
            return;
        }

        NumericTypeName? arrayType = typeDeclaration.ArrayItemsType()?.ReducedType.PreferredDotnetNumericTypeName();
        if (arrayType is NumericTypeName at && seenConversions.Add($"ReadOnlySpan<{at.Name}>"))
        {
            if (at.IsNetOnly)
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLine("#if NET")
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(ReadOnlySpan<", at.Name, "> value) => new(value);")
                    .AppendLine("#endif");
            }
            else
            {
                generator
                    .AppendSeparatorLine()
                    .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                    .AppendLineIndent("public static implicit operator ", generator.SourceClassName(), "(ReadOnlySpan<", at.Name, "> value) => new(value);");
            }
        }
    }

    private static CodeGenerator AppendSourceFactoryMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        if ((core & CoreTypes.String) != 0)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static ", generator.SourceClassName(), " RawString(ReadOnlySpan<byte> value, bool requiresUnescaping) => new(value, requiresUnescaping);");
        }

        if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static ", generator.SourceClassName(), " FormattedNumber(ReadOnlySpan<byte> value) => new(value, Kind.FormattedNumber);");
        }

        if ((core & CoreTypes.Null) != 0)
        {
            generator
                .AppendSeparatorLine()
                .AppendLineIndent("[MethodImpl(MethodImplOptions.AggressiveInlining)]")
                .AppendLineIndent("public static ", generator.SourceClassName(), " Null() => new(Kind.Null);");
        }

        return generator;
    }

    private static CodeGenerator AppendSourceFields(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders, bool forContext = false)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("_kind")
            .AppendLineIndent("private readonly Kind _kind;");

        CoreTypes core = typeDeclaration.ImpliedCoreTypesOrAny();

        bool hasSimpleTypeBacking = false;

        if (!forContext)
        {
            generator
                .ReserveNameIfNotReserved("_jsonElement")
                .AppendLineIndent("private readonly JsonElement _jsonElement;");

            bool hasUtf8Backing = false;

            if ((core & CoreTypes.String) != 0)
            {
                generator
                .ReserveNameIfNotReserved("_utf8Backing")
                .ReserveNameIfNotReserved("_utf16Backing")
                    .AppendLineIndent("private readonly ReadOnlySpan<byte> _utf8Backing;")
                    .AppendLineIndent("private readonly ReadOnlySpan<char> _utf16Backing;");

                if (typeDeclaration.Format() is string format &&
                    FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
                    requiresSimpleType)
                {
                    generator
                        .ReserveNameIfNotReserved("_simpleTypeBacking")
                        .AppendLineIndent("private readonly SimpleTypesBacking _simpleTypeBacking;");
                    hasSimpleTypeBacking = true;
                }

                hasUtf8Backing = true;
            }

            if ((core & (CoreTypes.Number | CoreTypes.Integer)) != 0)
            {
                if (!hasUtf8Backing)
                {
                    generator
                        .ReserveNameIfNotReserved("_utf8Backing")
                        .AppendLineIndent("private readonly ReadOnlySpan<byte> _utf8Backing;");
                }

                if (!hasSimpleTypeBacking)
                {
                    generator
                        .ReserveNameIfNotReserved("_simpleTypeBacking")
                        .AppendLineIndent("private readonly SimpleTypesBacking _simpleTypeBacking;");
                    hasSimpleTypeBacking = true;
                }
            }

            // Also check composed builders for string formats that require SimpleTypesBacking
            if (!hasSimpleTypeBacking)
            {
                foreach (ComposedBuilder composedBuilder in builders)
                {
                    if (composedBuilder.StringFormat is string cf &&
                        FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(cf, out bool cr) &&
                        cr)
                    {
                        generator
                            .ReserveNameIfNotReserved("_simpleTypeBacking")
                            .AppendLineIndent("private readonly SimpleTypesBacking _simpleTypeBacking;");
                        hasSimpleTypeBacking = true;
                        break;
                    }

                    if ((composedBuilder.TypeDeclaration.ImpliedCoreTypesOrAny() & (CoreTypes.Number | CoreTypes.Integer)) != 0)
                    {
                        generator
                            .ReserveNameIfNotReserved("_simpleTypeBacking")
                            .AppendLineIndent("private readonly SimpleTypesBacking _simpleTypeBacking;");
                        hasSimpleTypeBacking = true;
                        break;
                    }
                }
            }
        }
        else
        {
            generator
                .AppendLineIndent("TContext _context;")
                .AppendLineIndent("Source _source;");
        }

        bool isObject = (core & CoreTypes.Object) != 0;
        bool isArray = (core & CoreTypes.Array) != 0;
        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.LocalAndAppliedEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        string contextBuildType = forContext ? "Build<TContext>" : "Build";
        if (isObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            generator
                .ReserveNameIfNotReserved("_objectBuilder")
                .AppendLineIndent("private readonly ", isArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName(), ".", contextBuildType, "? _objectBuilder;");

            // Add the captured Create(...) parameter fields for the property-parameter Build(...) overload.
            if (EmitsCreateParamsBuildFor(typeDeclaration, forContext))
            {
                List<(string Type, bool IsOptional, string JsonName)> createArgs = CreateParamSourceTypes(generator, typeDeclaration, forContext);
                for (int i = 0; i < createArgs.Count; i++)
                {
                    generator
                        .ReserveNameIfNotReserved($"_createArg{i + 1}")
                        .AppendLineIndent("private readonly ", createArgs[i].Type, " _createArg", (i + 1).ToString(), ";");
                }
            }
        }

        HashSet<string> seenArrayValues = new(StringComparer.Ordinal);

        if (isArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            generator
                .ReserveNameIfNotReserved("_arrayBuilder")
                .AppendLineIndent("private readonly ", isObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName(), ".", contextBuildType, "? _arrayBuilder;");

            if (!forContext)
            {
                generator
                    .AppendNumericArrayTypeFields(typeDeclaration, seenArrayValues);

                // Add tensor span field for fixed-size numeric arrays
                if (typeDeclaration.IsFixedSizeNumericArray())
                {
                    NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#if NET");
                    }

                    generator
                        .ReserveNameIfNotReserved($"_{numericTypeName.Name}Tensor")
                        .AppendLineIndent("private readonly ReadOnlySpan<", numericTypeName.Name, "> _", numericTypeName.Name, "Tensor;");

                    if (numericTypeName.IsNetOnly)
                    {
                        generator
                            .AppendLine("#endif");
                    }
                }

                // Add tuple item Source fields for pure tuple types
                if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForFields && !HasNotAnyTupleItem(tupleTypeForFields))
                {
                    int fieldIndex = 0;
                    foreach (ReducedTypeDeclaration item in tupleTypeForFields.ItemsTypes)
                    {
                        fieldIndex++;
                        string fqdtn = item.ReducedType.FullyQualifiedDotnetTypeName();
                        generator
                            .ReserveNameIfNotReserved($"_tupleItem{fieldIndex}")
                            .AppendLineIndent("private readonly ", fqdtn, ".", generator.SourceClassName(fqdtn), " _tupleItem", fieldIndex.ToString(), ";");
                    }
                }
            }
        }

        foreach (ComposedBuilder builder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (builder.TypeDeclaration.IsBuiltInJsonNotAnyType())
            {
                continue;
            }

            if (builder.ObjectInstanceName is string oin)
            {
                if (!(builder.IsObject && typeDeclaration.HasPropertyDeclarations) || typeDeclaration.ConstituentBuildYieldsValidInstance())
                {
                    string fqdtn = builder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                    generator
                        .ReserveNameIfNotReserved($"_{oin}")
                        .AppendLineIndent(
                        "private readonly ",
                        fqdtn,
                        ".",
                        builder.IsArray ? generator.ObjectBuilderClassName(fqdtn) : generator.BuilderClassName(fqdtn),
                        ".", contextBuildType, "? _",
                        oin,
                        ";");
                }
            }

            if (!forContext && builder.SourceInstanceName is string sin)
            {
                // #812: a by-value backing field for the constituent's own Source, so the result of
                // Constituent.Build(...) can be stored in (and built through) this union's Source.
                string fqdtn = builder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .ReserveNameIfNotReserved($"_{sin}")
                    .AppendLineIndent(
                    "private readonly ",
                    fqdtn,
                    ".",
                    generator.SourceClassName(fqdtn),
                    " _",
                    sin,
                    ";");
            }

            if (builder.ArrayInstanceName is string ain)
            {
                string fqdtn = builder.TypeDeclaration.FullyQualifiedDotnetTypeName();
                generator
                    .ReserveNameIfNotReserved($"_{ain}")
                    .AppendLineIndent(
                    "private readonly ",
                    fqdtn,
                    ".",
                    builder.IsObject ? generator.ArrayBuilderClassName(fqdtn) : generator.BuilderClassName(fqdtn),
                    ".", contextBuildType, "? _",
                    ain,
                    ";")
                   .AppendNumericArrayTypeFields(builder.TypeDeclaration, seenArrayValues);
            }
        }

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Gets a value indicating whether this Source is undefined (uninitialized).")
            .AppendLineIndent("/// </summary>")
            .AppendLineIndent("public bool IsUndefined => _kind == Kind.Unknown;");

        return generator;
    }

    private static MethodParameter[] BuildMethodParameters(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return [];
        }

        return
        [
                new MethodParameter("ref", "ComplexValueBuilder", generator.GetUniqueParameterNameInScope("builder", childScope: "Create")),
                .. typeDeclaration.PropertyDeclarations
                        .Where(p => p.RequiredOrOptional != RequiredOrOptional.Optional &&
                               p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
                               !p.ReducedPropertyType.IsBuiltInJsonNotAnyType())
                        .Select(p => new MethodParameter("in", GetSource(generator, p.ReducedPropertyType.FullyQualifiedDotnetTypeName()), generator.GetUniqueParameterNameInScope(p.JsonPropertyName, childScope: "Create"))),
                .. typeDeclaration.PropertyDeclarations
                        .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional &&
                               !p.ReducedPropertyType.IsBuiltInJsonNotAnyType())
                        .Select(p =>
                            new MethodParameter(
                                "in",
                                GetSource(generator, p.ReducedPropertyType.FullyQualifiedDotnetTypeName()),
                                generator.GetUniqueParameterNameInScope(p.JsonPropertyName, childScope: "Create"),
                                typeIsNullable: false,
                                defaultValue: "default")),
            ];

        static string GetSource(CodeGenerator generator, string fqdtn)
        {
            return fqdtn + "." + generator.SourceClassName(fqdtn);
        }
    }

    private static MethodParameter[] BuildMethodParametersWithContext(CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return [];
        }

        return
        [
                new MethodParameter("in", "TContext", generator.GetUniqueParameterNameInScope("context", childScope: "Create<TContext>")),
                new MethodParameter("ref", "ComplexValueBuilder", generator.GetUniqueParameterNameInScope("builder", childScope: "Create<TContext>")),
                .. typeDeclaration.PropertyDeclarations
                        .Where(p => p.RequiredOrOptional != RequiredOrOptional.Optional &&
                               p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
                               !p.ReducedPropertyType.IsBuiltInJsonNotAnyType())
                        .Select(p => new MethodParameter("in", $"{GetSource(generator, p.ReducedPropertyType.FullyQualifiedDotnetTypeName())}{((p.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Array | CoreTypes.Object)) != 0 ? "<TContext>" : "")}", generator.GetUniqueParameterNameInScope(p.JsonPropertyName, childScope: "Create<TContext>"))),
                .. typeDeclaration.PropertyDeclarations
                        .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional &&
                               !p.ReducedPropertyType.IsBuiltInJsonNotAnyType())
                        .Select(p => new MethodParameter("in", $"{GetSource(generator, p.ReducedPropertyType.FullyQualifiedDotnetTypeName())}{((p.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Array | CoreTypes.Object)) != 0 ? "<TContext>" : "")}", generator.GetUniqueParameterNameInScope(p.JsonPropertyName, childScope: "Create<TContext>"), defaultValue: "default")),
            ];

        static string GetSource(CodeGenerator generator, string fqdtn)
        {
            return fqdtn + "." + generator.SourceClassName(fqdtn);
        }
    }

    private static PropertyDeclaration[] BuildOrderedProperties(TypeDeclaration typeDeclaration)
    {
        return
        [
            .. typeDeclaration.PropertyDeclarations
                                        .Where(p => p.RequiredOrOptional != RequiredOrOptional.Optional && !p.ReducedPropertyType.IsBuiltInJsonNotAnyType()),
                .. typeDeclaration.PropertyDeclarations
                                        .Where(p => p.RequiredOrOptional == RequiredOrOptional.Optional && !p.ReducedPropertyType.IsBuiltInJsonNotAnyType()),
            ];
    }

    // Metadata keys used to memoize the gating decision for the property-parameter Build(...) overload.
    private const string EmitsCreateParamsBuildKey = "CSharp_LanguageProvider_EmitsCreateParamsBuild";
    private const string CreateParamCycleKey = "CSharp_LanguageProvider_CreateParamCycle";
    private const string CreateParamWeightKey = "CSharp_LanguageProvider_CreateParamWeight";

    /// <summary>
    /// Gets the property declarations of <paramref name="typeDeclaration"/> that become parameters
    /// of its <c>Create(...)</c> method (and hence of the property-parameter <c>Build(...)</c> overload).
    /// </summary>
    /// <remarks>
    /// This must mirror the filtering in <see cref="BuildMethodParameters"/>: required, non-constant
    /// properties and all optional properties, excluding <c>NotAny</c> property types and required
    /// properties whose value is a single constant (those are pre-baked, not parameters).
    /// </remarks>
    private static IEnumerable<PropertyDeclaration> CreateParameterProperties(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.PropertyDeclarations.Where(p =>
            !p.ReducedPropertyType.IsBuiltInJsonNotAnyType() &&
            (
                (p.RequiredOrOptional != RequiredOrOptional.Optional && p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined) ||
                p.RequiredOrOptional == RequiredOrOptional.Optional));
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="typeDeclaration"/> is an object type that
    /// exposes at least one <c>Create(...)</c> parameter, and is therefore a candidate to emit (and
    /// to be inlined by-value into) a property-parameter <c>Build(...)</c> overload.
    /// </summary>
    private static bool IsCreateParamsBuildCandidate(TypeDeclaration typeDeclaration)
    {
        return (typeDeclaration.ImpliedCoreTypesOrAny() & CoreTypes.Object) != 0 &&
            CreateParameterProperties(typeDeclaration).Any();
    }

    /// <summary>
    /// Gets the distinct candidate object types that are the reduced types of
    /// <paramref name="typeDeclaration"/>'s <c>Create(...)</c> parameters.
    /// </summary>
    /// <remarks>
    /// These are the edges of the "create-parameter object" graph used to detect reference cycles.
    /// Only candidate children matter: a non-candidate (e.g. a scalar or array) child can never emit
    /// the overload, so it stores a fixed-size <c>Source</c> and terminates any containment chain.
    /// </remarks>
    private static IEnumerable<TypeDeclaration> CreateParamCandidateChildren(TypeDeclaration typeDeclaration)
    {
        HashSet<string> seen = new(StringComparer.Ordinal);
        foreach (PropertyDeclaration p in CreateParameterProperties(typeDeclaration))
        {
            TypeDeclaration child = p.ReducedPropertyType;
            if (IsCreateParamsBuildCandidate(child) && seen.Add(child.FullyQualifiedDotnetTypeName()))
            {
                yield return child;
            }
        }
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="typeDeclaration"/> is reachable from itself
    /// through the create-parameter object graph (i.e. it participates in a reference cycle).
    /// </summary>
    /// <remarks>
    /// Such a type must not emit the property-parameter <c>Build(...)</c> overload: its <c>CreateArgs</c>
    /// would store its own <c>Source</c> by value (transitively), producing an illegal self-containing
    /// ref struct (CS0523). Excluding <em>every</em> type on a cycle keeps the emitting set acyclic by
    /// construction. The result depends only on <paramref name="typeDeclaration"/>, so it is deterministic
    /// regardless of the order in which types are generated.
    /// </remarks>
    private static bool InCreateParamCycle(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(CreateParamCycleKey, out bool? cached) && cached is bool cachedValue)
        {
            return cachedValue;
        }

        string targetName = typeDeclaration.FullyQualifiedDotnetTypeName();
        bool result = ReachesSelf(typeDeclaration, targetName, new HashSet<string>(StringComparer.Ordinal));
        typeDeclaration.SetMetadata(CreateParamCycleKey, result);
        return result;

        static bool ReachesSelf(TypeDeclaration current, string targetName, HashSet<string> visited)
        {
            foreach (TypeDeclaration child in CreateParamCandidateChildren(current))
            {
                string childName = child.FullyQualifiedDotnetTypeName();
                if (childName == targetName)
                {
                    return true;
                }

                if (visited.Add(childName) && ReachesSelf(child, targetName, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets a value indicating whether surfacing <paramref name="constituent"/>'s <c>Source</c> through
    /// <paramref name="union"/>'s <c>Source</c> (the #812 constituent-<c>Source</c> projection) would create
    /// a value-type containment cycle — i.e. whether <paramref name="constituent"/>'s <c>Source</c> already
    /// embeds <paramref name="union"/>'s <c>Source</c> by value, directly or transitively.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A constituent <c>Source</c> embeds another type's <c>Source</c> by value through two kinds of edge:
    /// a captured <c>_createArg</c> field (one per <c>Create(...)</c> parameter, present only when the type
    /// actually emits that capture — see <see cref="EmitsCreateParamsBuild"/>), and a nested #812 projection
    /// of its own composition constituents. When <paramref name="union"/> is reachable along those edges,
    /// also embedding <paramref name="constituent"/>'s <c>Source</c> in <paramref name="union"/>'s would close
    /// a value cycle and emit an illegal self-containing ref struct (CS0523). The projection is therefore
    /// suppressed for that constituent, which remains reachable through the builder/<c>JsonElement</c> path.
    /// </para>
    /// <para>
    /// The composition edge is over-approximated by following every constituent (whether or not it is itself
    /// ultimately projected): suppressing more projections can only break cycles, never create them, so —
    /// mirroring <see cref="InCreateParamCycle"/> — this keeps the projected set acyclic by construction and
    /// depends only on the two type arguments, independent of generation order.
    /// </para>
    /// </remarks>
    private static bool ConstituentSourceReachesUnion(TypeDeclaration constituent, TypeDeclaration union)
    {
        string targetName = union.FullyQualifiedDotnetTypeName();
        return Reaches(constituent, targetName, new HashSet<string>(StringComparer.Ordinal));

        static bool Reaches(TypeDeclaration current, string targetName, HashSet<string> visited)
        {
            if (EmitsCreateParamsBuild(current))
            {
                foreach (PropertyDeclaration p in CreateParameterProperties(current))
                {
                    TypeDeclaration child = p.ReducedPropertyType;
                    string childName = child.FullyQualifiedDotnetTypeName();
                    if (childName == targetName)
                    {
                        return true;
                    }

                    if (visited.Add(childName) && Reaches(child, targetName, visited))
                    {
                        return true;
                    }
                }
            }

            foreach (TypeDeclaration c in current.CompositionSources())
            {
                string childName = c.FullyQualifiedDotnetTypeName();
                if (childName == targetName)
                {
                    return true;
                }

                if (visited.Add(childName) && Reaches(c, targetName, visited))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// Gets the estimated number of captured value slots the property-parameter <c>Build(...)</c>
    /// overload for <paramref name="typeDeclaration"/> would hold, counting nested emitting object
    /// properties by-value (bottom-up) and everything else as a single fixed slot.
    /// </summary>
    /// <remarks>
    /// Only ever invoked for acyclic types (emitting requires <see cref="InCreateParamCycle"/> to be
    /// <see langword="false"/>), so the recursion terminates over the create-parameter object DAG.
    /// </remarks>
    private static int CreateParamWeight(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(CreateParamWeightKey, out int? cached) && cached is int cachedValue)
        {
            return cachedValue;
        }

        int weight = 0;
        foreach (PropertyDeclaration p in CreateParameterProperties(typeDeclaration))
        {
            TypeDeclaration child = p.ReducedPropertyType;
            if (IsCreateParamsBuildCandidate(child) && EmitsCreateParamsBuild(child))
            {
                // The child inlines its own CreateArgs by value, so it contributes its expanded weight.
                weight += 1 + CreateParamWeight(child);
            }
            else
            {
                // Scalars, arrays and non-emitting (cyclic or over-threshold) objects are a single fixed slot.
                weight++;
            }
        }

        typeDeclaration.SetMetadata(CreateParamWeightKey, weight);
        return weight;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="typeDeclaration"/> emits the property-parameter
    /// <c>Build(...)</c> overload that captures its <c>Create(...)</c> arguments directly.
    /// </summary>
    /// <remarks>
    /// True when the type has at least one <c>Create(...)</c> parameter, does not participate in a
    /// create-parameter reference cycle, and its estimated captured-slot weight is within the configured
    /// <see cref="TypeDeclarationExtensions.BuildParametersThreshold"/>. Memoized per type.
    /// </remarks>
    private static bool EmitsCreateParamsBuild(TypeDeclaration typeDeclaration)
    {
        if (typeDeclaration.TryGetMetadata(EmitsCreateParamsBuildKey, out bool? cached) && cached is bool cachedValue)
        {
            return cachedValue;
        }

        bool result;
        if (!IsCreateParamsBuildCandidate(typeDeclaration) || InCreateParamCycle(typeDeclaration))
        {
            result = false;
        }
        else
        {
            result = CreateParamWeight(typeDeclaration) <= typeDeclaration.BuildParametersThreshold();
        }

        typeDeclaration.SetMetadata(EmitsCreateParamsBuildKey, result);
        return result;
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="typeDeclaration"/> would expose <c>Create(...)</c>
    /// parameters but has the property-parameter <c>Build(...)</c> overload suppressed (by a cycle or the
    /// size threshold). Used to decide whether to document the omission on the delegate <c>Build</c> overload.
    /// </summary>
    private static bool CreateParamsBuildSuppressed(TypeDeclaration typeDeclaration)
    {
        return IsCreateParamsBuildCandidate(typeDeclaration) && !EmitsCreateParamsBuild(typeDeclaration);
    }

    /// <summary>
    /// Gets a value indicating whether the property-parameter <c>Build(...)</c> machinery should be
    /// emitted on the <c>Source</c> (<paramref name="forContext"/> is <see langword="false"/>) or
    /// <c>Source&lt;TContext&gt;</c> (<paramref name="forContext"/> is <see langword="true"/>) ref struct.
    /// </summary>
    /// <remarks>
    /// The context-flowing form additionally requires an object/array <c>Create(...)</c> parameter to
    /// thread the context through; scalar-only objects emit only the non-context form.
    /// </remarks>
    private static bool EmitsCreateParamsBuildFor(TypeDeclaration typeDeclaration, bool forContext)
    {
        return EmitsCreateParamsBuild(typeDeclaration) && (!forContext || HasObjectOrArrayCreateParam(typeDeclaration));
    }

    /// <summary>
    /// Gets a value indicating whether <paramref name="typeDeclaration"/> has at least one
    /// non-constant object or array <c>Create(...)</c> parameter, and therefore exposes a
    /// context-flowing <c>Create&lt;TContext&gt;</c> (and matching <c>Build&lt;TContext&gt;</c>) overload.
    /// </summary>
    private static bool HasObjectOrArrayCreateParam(TypeDeclaration typeDeclaration)
    {
        return typeDeclaration.PropertyDeclarations.Any(p =>
            p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
            !p.ReducedPropertyType.IsBuiltInJsonNotAnyType() &&
            (p.ReducedPropertyType.ImpliedCoreTypesOrAny() & (CoreTypes.Object | CoreTypes.Array)) != 0);
    }

    /// <summary>
    /// Appends the <c>BuildCreateValue(...)</c> invocation for the <c>Kind.Create</c> apply arm,
    /// passing the captured <c>_createArg</c> fields (and the context, when <paramref name="forContext"/>).
    /// </summary>
    private static void AppendBuildCreateValueCall(CodeGenerator generator, TypeDeclaration typeDeclaration, string builderName, bool forContext)
    {
        int argCount = CreateParameterProperties(typeDeclaration).Count();

        generator.AppendIndent(builderName, ".BuildCreateValue(");

        bool first = true;
        if (forContext)
        {
            generator.Append("_context");
            first = false;
        }

        for (int i = 1; i <= argCount; i++)
        {
            if (!first)
            {
                generator.Append(", ");
            }

            first = false;
            generator.Append("_createArg").Append(i);
        }

        generator.AppendLine(", ref valueBuilder);");
    }

    /// <summary>
    /// Gets the ordered source-type strings for the <c>Create(...)</c> parameters of
    /// <paramref name="typeDeclaration"/> (required, non-constant properties first, then optional),
    /// together with whether each is optional.
    /// </summary>
    /// <remarks>
    /// This is the pure counterpart of <see cref="BuildMethodParameters"/>: it computes only the
    /// parameter types and ordering and never reserves a parameter name, so it can be called freely
    /// without perturbing the <c>Create</c>/<c>CreateBuilder</c> parameter-naming scope. The emitted
    /// machinery uses fixed positional names (<c>arg1</c>, <c>arg2</c>, ...) instead.
    /// </remarks>
    private static List<(string Type, bool IsOptional, string JsonName)> CreateParamSourceTypes(CodeGenerator generator, TypeDeclaration typeDeclaration, bool forContext)
    {
        List<(string Type, bool IsOptional, string JsonName)> result = new();

        foreach (PropertyDeclaration p in typeDeclaration.PropertyDeclarations.Where(p =>
            p.RequiredOrOptional != RequiredOrOptional.Optional &&
            p.ReducedPropertyType.SingleConstantValue().ValueKind == JsonValueKind.Undefined &&
            !p.ReducedPropertyType.IsBuiltInJsonNotAnyType()))
        {
            result.Add((SourceTypeFor(generator, p.ReducedPropertyType, forContext), false, p.JsonPropertyName));
        }

        foreach (PropertyDeclaration p in typeDeclaration.PropertyDeclarations.Where(p =>
            p.RequiredOrOptional == RequiredOrOptional.Optional &&
            !p.ReducedPropertyType.IsBuiltInJsonNotAnyType()))
        {
            result.Add((SourceTypeFor(generator, p.ReducedPropertyType, forContext), true, p.JsonPropertyName));
        }

        return result;

        static string SourceTypeFor(CodeGenerator generator, TypeDeclaration reducedType, bool forContext)
        {
            string fqdtn = reducedType.FullyQualifiedDotnetTypeName();
            string source = fqdtn + "." + generator.SourceClassName(fqdtn);
            if (forContext && (reducedType.ImpliedCoreTypesOrAny() & (CoreTypes.Array | CoreTypes.Object)) != 0)
            {
                source += "<TContext>";
            }

            return source;
        }
    }

    private static void AppendCreateArgDeclarations(CodeGenerator generator, List<(string Type, bool IsOptional, string JsonName)> args, bool includeDefaults)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0)
            {
                generator.Append(", ");
            }

            generator.Append("in ").Append(args[i].Type).Append(" arg").Append(i + 1);

            if (includeDefaults && args[i].IsOptional)
            {
                generator.Append(" = default");
            }
        }
    }

    /// <summary>
    /// Computes the user-facing parameter names (derived from the JSON property names) for the
    /// property-parameter <c>Build(...)</c> factory, in a dedicated naming scope so they never
    /// perturb the <c>Create</c>/<c>CreateBuilder</c> parameter-naming scope.
    /// </summary>
    private static List<string> FactoryParamNames(CodeGenerator generator, List<(string Type, bool IsOptional, string JsonName)> args, bool forContext)
    {
        string childScope = forContext ? "ParamsBuildContext" : "ParamsBuild";
        List<string> names = new(args.Count);
        foreach ((string Type, bool IsOptional, string JsonName) arg in args)
        {
            names.Add(generator.GetUniqueParameterNameInScope(arg.JsonName, childScope: childScope));
        }

        return names;
    }

    private static void AppendFactoryParamDeclarations(CodeGenerator generator, List<(string Type, bool IsOptional, string JsonName)> args, List<string> names)
    {
        for (int i = 0; i < args.Count; i++)
        {
            if (i > 0)
            {
                generator.Append(", ");
            }

            generator.Append("in ").Append(args[i].Type).Append(" ").Append(names[i]);

            if (args[i].IsOptional)
            {
                generator.Append(" = default");
            }
        }
    }

    /// <summary>
    /// Appends the public <c>Build(...)</c> property-parameter factory overloads (non-generic and
    /// context-flowing) that capture the <c>Create(...)</c> arguments directly into a <c>Source</c>.
    /// </summary>
    private static CodeGenerator AppendCreateParamsBuildFactory(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested || !EmitsCreateParamsBuild(typeDeclaration))
        {
            return generator;
        }

        string sourceClassName = generator.SourceClassName();

        // Non-generic variant.
        List<(string Type, bool IsOptional, string JsonName)> args = CreateParamSourceTypes(generator, typeDeclaration, forContext: false);
        List<string> names = FactoryParamNames(generator, args, forContext: false);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Build an instance of the value directly from its property values.")
            .AppendLineIndent("/// </summary>");

        for (int i = 0; i < args.Count; i++)
        {
            generator.AppendLineIndent("/// <param name=\"", names[i], "\">The value of the <c>", Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(args[i].JsonName, true), "</c> property.</param>");
        }

        generator
            .AppendLineIndent("/// <returns>The source from which to build the value.</returns>")
            .AppendIndent("public static ", sourceClassName, " Build(");

        AppendFactoryParamDeclarations(generator, args, names);

        generator
            .AppendLine(")")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendIndent("return new ", sourceClassName, "(");

        for (int i = 0; i < names.Count; i++)
        {
            if (i > 0)
            {
                generator.Append(", ");
            }

            generator.Append(names[i]);
        }

        generator
                .AppendLine(");")
            .PopIndent()
            .AppendLineIndent("}");

        // Context-flowing variant — only when there is an object/array property to thread the context through.
        if (HasObjectOrArrayCreateParam(typeDeclaration))
        {
            List<(string Type, bool IsOptional, string JsonName)> contextArgs = CreateParamSourceTypes(generator, typeDeclaration, forContext: true);

            // Reserve the synthetic context parameter name first so a property whose parameter name
            // would also be "context" is uniquified away from it (avoiding a duplicate-parameter error).
            string contextName = generator.GetUniqueParameterNameInScope("context", childScope: "ParamsBuildContext");
            List<string> contextNames = FactoryParamNames(generator, contextArgs, forContext: true);

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Build an instance of the value directly from its property values.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the context to pass to the builder.</typeparam>")
                .AppendLineIndent("/// <param name=\"", contextName, "\">The context to pass to the builder.</param>");

            for (int i = 0; i < contextArgs.Count; i++)
            {
                generator.AppendLineIndent("/// <param name=\"", contextNames[i], "\">The value of the <c>", Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(contextArgs[i].JsonName, true), "</c> property.</param>");
            }

            generator
                .AppendLineIndent("/// <returns>The source from which to build the value.</returns>")
                .AppendIndent("public static ", sourceClassName, "<TContext> Build<TContext>(scoped in TContext ", contextName);

            if (contextArgs.Count > 0)
            {
                generator.Append(", ");
                AppendFactoryParamDeclarations(generator, contextArgs, contextNames);
            }

            generator
                .AppendLine(")")
                .PushIndent()
                    .AppendLineIndent("#if NET9_0_OR_GREATER")
                    .AppendLineIndent("where TContext : allows ref struct")
                    .AppendLineIndent("#endif")
                .PopIndent()
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendIndent("return new ", sourceClassName, "<TContext>(", contextName);

            for (int i = 0; i < contextNames.Count; i++)
            {
                generator.Append(", ").Append(contextNames[i]);
            }

            generator
                    .AppendLine(");")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    /// <summary>
    /// Appends the <c>BuildCreateValue</c> helper(s) that materialize the captured <c>Create(...)</c>
    /// arguments into a complex value builder (the property-parameter analogue of <c>BuildValue</c>).
    /// </summary>
    private static CodeGenerator AppendBuildCreateValue(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested || !EmitsCreateParamsBuild(typeDeclaration))
        {
            return generator;
        }

        // Non-generic variant.
        List<(string Type, bool IsOptional, string JsonName)> args = CreateParamSourceTypes(generator, typeDeclaration, forContext: false);

        generator
            .AppendSeparatorLine()
            .AppendLineIndent("/// <summary>")
            .AppendLineIndent("/// Builds the object value directly from its captured property values into the given complex value builder.")
            .AppendLineIndent("/// </summary>");

        for (int i = 0; i < args.Count; i++)
        {
            generator.AppendLineIndent("/// <param name=\"arg", (i + 1).ToString(), "\">The value of the property.</param>");
        }

        generator
            .AppendLineIndent("/// <param name=\"o\">The complex value builder into which to write the object.</param>")
            .AppendIndent("internal static void BuildCreateValue(");

        AppendCreateArgDeclarations(generator, args, includeDefaults: false);

        generator
            .AppendLine(", ref ComplexValueBuilder o)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("o.StartObject();")
                .AppendIndent("Create(ref o");

        for (int i = 0; i < args.Count; i++)
        {
            generator.Append(", arg").Append(i + 1);
        }

        generator
                .AppendLine(");")
                .AppendLineIndent("o.EndObject();")
            .PopIndent()
            .AppendLineIndent("}");

        // Context-flowing variant.
        if (HasObjectOrArrayCreateParam(typeDeclaration))
        {
            List<(string Type, bool IsOptional, string JsonName)> contextArgs = CreateParamSourceTypes(generator, typeDeclaration, forContext: true);

            generator
                .AppendSeparatorLine()
                .AppendLineIndent("/// <summary>")
                .AppendLineIndent("/// Builds the object value directly from its captured property values into the given complex value builder.")
                .AppendLineIndent("/// </summary>")
                .AppendLineIndent("/// <typeparam name=\"TContext\">The type of the context to pass to the builder.</typeparam>")
                .AppendLineIndent("/// <param name=\"context\">The context to pass to the builder.</param>");

            for (int i = 0; i < contextArgs.Count; i++)
            {
                generator.AppendLineIndent("/// <param name=\"arg", (i + 1).ToString(), "\">The value of the property.</param>");
            }

            generator
                .AppendLineIndent("/// <param name=\"o\">The complex value builder into which to write the object.</param>")
                .AppendIndent("internal static void BuildCreateValue<TContext>(scoped in TContext context");

            if (contextArgs.Count > 0)
            {
                generator.Append(", ");
                AppendCreateArgDeclarations(generator, contextArgs, includeDefaults: false);
            }

            generator
                .AppendLine(", ref ComplexValueBuilder o)")
                .AppendLine("#if NET9_0_OR_GREATER")
                .PushIndent()
                    .AppendLineIndent("where TContext : allows ref struct")
                .PopIndent()
                .AppendLine("#endif")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("o.StartObject();")
                    .AppendIndent("Create(context, ref o");

            for (int i = 0; i < contextArgs.Count; i++)
            {
                generator.Append(", arg").Append(i + 1);
            }

            generator
                    .AppendLine(");")
                    .AppendLineIndent("o.EndObject();")
                .PopIndent()
                .AppendLineIndent("}");
        }

        return generator;
    }

    private static CodeGenerator CollectBuilderSourcesAndAppendKinds(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        HashSet<string> numericArrayKinds = new(StringComparer.Ordinal);
        bool hasStringSimpleType = false;

        if (typeDeclaration.Format() is string format &&
            FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(format, out bool requiresSimpleType) &&
            requiresSimpleType)
        {
            generator
                .ReserveName("StringSimpleType")
                .AppendLineIndent("StringSimpleType,");
            hasStringSimpleType = true;
        }

        foreach (TypeDeclaration t in typeDeclaration.CompositionSources())
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            CoreTypes core = t.ImpliedCoreTypesOrAny();

            bool isObject = (core & CoreTypes.Object) != 0;
            bool isArray = (core & CoreTypes.Array) != 0;
            bool isString = (core & CoreTypes.String) != 0;
            bool isNumber = (core & CoreTypes.Number) != 0;

            string? arrayKindName = null;
            string? objectKindName = null;

            string? arrayInstanceName = null;
            string? objectInstanceName = null;

            string? arrayBuilderName = null;
            string? objectBuilderName = null;

            string? numericArrayKindName = null;
            NumericTypeName? numericArrayTypeName = null;

            string? stringFormat = null;
            string? numericFormat = null;

            string? sourceKindName = null;
            string? sourceInstanceName = null;

            if (isString)
            {
                stringFormat = t.Format();

                if (!hasStringSimpleType && stringFormat is string f &&
                    FormatHandlerRegistry.Instance.StringFormatHandlers.RequiresSimpleTypesBacking(f, out bool r) &&
                    r)
                {
                    generator
                        .ReserveName("StringSimpleType")
                       .AppendLineIndent("StringSimpleType,");
                    hasStringSimpleType = true;
                }
            }

            if (isNumber)
            {
                numericFormat = t.Format();
            }

            bool shouldAdd = true;

            if (isArray)
            {
                if (t.ExplicitArrayItemsType() is not null)
                {
                    arrayKindName = generator.GetUniqueMethodNameInScope(t.DotnetTypeName(), suffix: isObject ? "ArrayBuilder" : "Builder");
                    arrayInstanceName = generator.GetUniqueFieldNameInScope(arrayKindName, suffix: "Instance");
                    arrayBuilderName = isObject ? generator.ArrayBuilderClassName(t.FullyQualifiedDotnetTypeName()) : generator.BuilderClassName(t.FullyQualifiedDotnetTypeName());

                    generator
                        .AppendLineIndent(arrayKindName, ",");
                    if (t.IsNumericArray() && !t.IsTuple() && !t.IsFixedSizeNumericArray())
                    {
                        NumericTypeName numericTypeName = t.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
                        numericArrayKindName = GetNumericArrayKind(generator, numericTypeName, reserve: true);
                        numericArrayTypeName = numericTypeName;
                        if (numericArrayKinds.Add(numericArrayKindName))
                        {
                            if (numericTypeName.IsNetOnly)
                            {
                                generator
                                    .AppendLine("#if NET");
                            }

                            generator
                                .AppendLineIndent(numericArrayKindName, ",");

                            if (numericTypeName.IsNetOnly)
                            {
                                generator
                                    .AppendLine("#endif");
                            }
                        }
                    }
                }
                else
                {
                    shouldAdd = false;
                }
            }

            if (isObject)
            {
                if (t.LocalEvaluatedPropertyType() is not null ||
                    t.HasPropertyDeclarations)
                {
                    objectKindName = generator.GetUniqueMethodNameInScope(t.DotnetTypeName(), suffix: isArray ? "ObjectBuilder" : "Builder");
                    objectInstanceName = generator.GetUniqueFieldNameInScope(objectKindName, suffix: "Instance");
                    objectBuilderName = isArray ? generator.ObjectBuilderClassName(t.FullyQualifiedDotnetTypeName()) : generator.BuilderClassName(t.FullyQualifiedDotnetTypeName());

                    generator
                        .AppendLineIndent(objectKindName, ",");

                    // #812: when the constituent object-builder wiring is enabled (a pure oneOf, or a
                    // discriminated union), also surface the constituent's own Source through the
                    // union's Source, so the result of Constituent.Build(...) can be passed wherever
                    // the union's Source is expected. Suppress it for a constituent whose Source would
                    // (transitively) embed this union's Source by value — surfacing it would close a
                    // ref-struct containment cycle (CS0523) for a recursive union; the constituent stays
                    // reachable through the builder/JsonElement path.
                    if ((!typeDeclaration.HasPropertyDeclarations || typeDeclaration.ConstituentBuildYieldsValidInstance()) &&
                        !ConstituentSourceReachesUnion(t, typeDeclaration))
                    {
                        sourceKindName = generator.GetUniqueMethodNameInScope(t.DotnetTypeName(), suffix: "Source");
                        sourceInstanceName = generator.GetUniqueFieldNameInScope(sourceKindName, suffix: "Instance");

                        generator
                            .AppendLineIndent(sourceKindName, ",");
                    }
                }
                else
                {
                    shouldAdd = false;
                }
            }

            if (shouldAdd)
            {
                builders.Add(new(t, arrayKindName, objectKindName, arrayInstanceName, objectInstanceName, objectBuilderName, arrayBuilderName, numericArrayKindName, numericArrayTypeName, stringFormat, numericFormat, sourceKindName, sourceInstanceName));
            }
        }

        // Now add the numeric array kind for the base type
        if (typeDeclaration.IsNumericArray() && !typeDeclaration.IsTuple() && !typeDeclaration.IsFixedSizeNumericArray())
        {
            NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
            string numericArrayKindName = GetNumericArrayKind(generator, numericTypeName, reserve: true);
            if (numericArrayKinds.Add(numericArrayKindName))
            {
                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#if NET");
                }

                generator
                    .AppendLineIndent(numericArrayKindName, ",");

                if (numericTypeName.IsNetOnly)
                {
                    generator
                        .AppendLine("#endif");
                }
            }
        }

        // Add tensor kind for fixed-size numeric arrays
        if (typeDeclaration.IsFixedSizeNumericArray())
        {
            NumericTypeName numericTypeName = typeDeclaration.PreferredDotnetNumericTypeName() ?? throw new InvalidOperationException(SR.ExpectedNumericTypeName);
            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#if NET");
            }

            generator
                .ReserveName("Tensor")
                .AppendLineIndent("Tensor,");

            if (numericTypeName.IsNetOnly)
            {
                generator
                    .AppendLine("#endif");
            }
        }

        // Add tuple kind for pure tuple types
        if (typeDeclaration.IsTuple() && GetTupleType(typeDeclaration) is TupleTypeDeclaration tupleTypeForKind && !HasNotAnyTupleItem(tupleTypeForKind))
        {
            generator
                .ReserveName("Tuple")
                .AppendLineIndent("Tuple,");
        }

        // Add the captured-create kind for the property-parameter Build(...) overload.
        if (EmitsCreateParamsBuild(typeDeclaration))
        {
            generator
                .ReserveName("Create")
                .AppendLineIndent("Create,");
        }

        CoreTypes rootCore = typeDeclaration.ImpliedCoreTypesOrAny();

        if ((rootCore & CoreTypes.String) != 0)
        {
            generator
                .ReserveName("RawUtf8StringRequiresUnescaping")
                .ReserveName("RawUtf8StringNotRequiresUnescaping")
                .ReserveName("Utf8String")
                .ReserveName("Utf16String")
                .AppendLineIndent("RawUtf8StringRequiresUnescaping,")
                .AppendLineIndent("RawUtf8StringNotRequiresUnescaping,")
                .AppendLineIndent("Utf8String,")
                .AppendLineIndent("Utf16String,");
        }

        if ((rootCore & (CoreTypes.Number | CoreTypes.Integer)) != 0)
        {
            generator
                .ReserveName("NumericSimpleType")
                .ReserveName("FormattedNumber")
                .AppendLineIndent("NumericSimpleType,")
                .AppendLineIndent("FormattedNumber,");
        }

        if ((rootCore & CoreTypes.Boolean) != 0)
        {
            generator
                .ReserveName("True")
                .ReserveName("False")
                .AppendLineIndent("True,")
                .AppendLineIndent("False,");
        }

        if ((rootCore & CoreTypes.Null) != 0)
        {
            generator
                .ReserveName("Null")
                .AppendLineIndent("Null,");
        }

        bool isRootObject = (rootCore & CoreTypes.Object) != 0;
        bool isRootArray = (rootCore & CoreTypes.Array) != 0;
        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (isRootObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            string builderKindName = isRootArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
            generator
                .ReserveName(builderKindName)
                .AppendLineIndent(builderKindName, ",");
        }

        if (isRootArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            string builderKindName = isRootObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();
            generator
                .ReserveName(builderKindName)
                .AppendLineIndent(builderKindName, ",");
        }

        return generator;
    }

    private static CodeGenerator AppendKindsForBuilders(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        foreach (ComposedBuilder builder in builders)
        {
            if (generator.IsCancellationRequested)
            {
                return generator;
            }

            if (builder.ArrayKindName is not null)
            {
                generator
                    .AppendLineIndent(builder.ArrayKindName, ",");
            }

            if (builder.ObjectKindName is not null)
            {
                generator
                    .AppendLineIndent(builder.ObjectKindName, ",");
            }
        }

        CoreTypes rootCore = typeDeclaration.ImpliedCoreTypesOrAny();

        bool isRootObject = (rootCore & CoreTypes.Object) != 0;
        bool isRootArray = (rootCore & CoreTypes.Array) != 0;
        bool hasFallbackObjectType =
            typeDeclaration.LocalEvaluatedPropertyType() is not null ||
            typeDeclaration.HasPropertyDeclarations;
        bool hasFallbackArrayType =
            typeDeclaration.ExplicitArrayItemsType() is not null;

        if (isRootObject && (hasFallbackObjectType || !builders.Any(b => b.IsObject)))
        {
            string builderKindName = isRootArray ? generator.ObjectBuilderClassName() : generator.BuilderClassName();
            generator
                .ReserveName(builderKindName)
                .AppendLineIndent(builderKindName, ",");
        }

        if (isRootArray && (hasFallbackArrayType || !builders.Any(b => b.IsArray)))
        {
            string builderKindName = isRootObject ? generator.ArrayBuilderClassName() : generator.BuilderClassName();
            generator
                .ReserveName(builderKindName)
                .AppendLineIndent(builderKindName, ",");
        }

        // Add the captured-create kind for the context-flowing property-parameter Build(...) overload.
        if (EmitsCreateParamsBuildFor(typeDeclaration, forContext: true))
        {
            generator
                .ReserveName("Create")
                .AppendLineIndent("Create,");
        }

        return generator;
    }

    private static CodeGenerator CollectBuilderSourcesAndAppendSourceKindEnum(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        return generator
            .AppendSeparatorLine()
            .BeginEnum(GeneratedTypeAccessibility.Private, "Kind")
                .ReserveName("Unknown")
                .ReserveName("JsonElement")
                .AppendLineIndent("Unknown,")
                .AppendLineIndent("JsonElement,")
                .CollectBuilderSourcesAndAppendKinds(typeDeclaration, builders)
            .EndClassStructOrEnumDeclaration();
    }

    private static CodeGenerator AppendKindEnumForBuilders(this CodeGenerator generator, TypeDeclaration typeDeclaration, List<ComposedBuilder> builders)
    {
        return generator
            .AppendSeparatorLine()
            .BeginEnum(GeneratedTypeAccessibility.Private, "Kind")
                .ReserveName("Unknown")
                .AppendLineIndent("Unknown,")
                .ReserveName("Source")
                .AppendLineIndent("Source,")
                .AppendKindsForBuilders(typeDeclaration, builders)
            .EndClassStructOrEnumDeclaration();
    }

    private static bool ComposedSchemaHasNonFixedSizeNumericArray(List<ComposedBuilder> builders, TypeDeclaration rootTypeDeclaration)
    {
        NumericTypeName? arrayType = rootTypeDeclaration.ArrayItemsType()?.ReducedType.PreferredDotnetNumericTypeName();
        if (arrayType is NumericTypeName at)
        {
            string name = at.Name;
            foreach (ComposedBuilder builder in builders)
            {
                if (rootTypeDeclaration == builder.TypeDeclaration)
                {
                    // Don't bother considering the current root type declaration
                    continue;
                }

                if (builder.TypeDeclaration.IsNumericArray() &&
                    !builder.TypeDeclaration.IsTuple() &&
                    builder.TypeDeclaration.ArrayItemsType()?.ReducedType.PreferredDotnetNumericTypeName()?.Name == name)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string GetNumericArrayKind(CodeGenerator generator, NumericTypeName at, bool reserve = false)
    {
        Span<char> buffer = stackalloc char[at.Name.Length + 5];
        at.Name.AsSpan().CopyTo(buffer);
        int written = Formatting.ToPascalCase(buffer.Slice(0, at.Name.Length));
        "Array".AsSpan().CopyTo(buffer.Slice(written));
        return reserve
            ? generator.GetUniquePropertyNameInScope(buffer.Slice(0, written + 5).ToString())
            : generator.GetPropertyNameInScope(buffer.Slice(0, written + 5).ToString());
    }

    internal sealed class ComposedBuilder
    {
        public ComposedBuilder(
            TypeDeclaration typeDeclaration,
            string? arrayKindName,
            string? objectKindName,
            string? arrayInstanceName,
            string? objectInstanceName,
            string? objectBuilderName,
            string? arrayBuilderName,
            string? numericArrayKindName,
            NumericTypeName? numericArrayTypeName,
            string? stringFormat,
            string? numericFormat,
            string? sourceKindName = null,
            string? sourceInstanceName = null)
        {
            TypeDeclaration = typeDeclaration;
            ArrayKindName = arrayKindName;
            ObjectKindName = objectKindName;
            ArrayInstanceName = arrayInstanceName;
            ObjectInstanceName = objectInstanceName;
            ObjectBuilderName = objectBuilderName;
            ArrayBuilderName = arrayBuilderName;
            NumericArrayKindName = numericArrayKindName;
            NumericArrayTypeName = numericArrayTypeName;
            StringFormat = stringFormat;
            NumericFormat = numericFormat;
            SourceKindName = sourceKindName;
            SourceInstanceName = sourceInstanceName;
        }

        public string? ArrayBuilderName { get; }

        public string? ArrayInstanceName { get; }

        public string? ArrayKindName { get; }

        public bool IsArray => ArrayKindName is not null;

        public bool IsObject => ObjectKindName is not null;

        public string? NumericArrayKindName { get; }

        public NumericTypeName? NumericArrayTypeName { get; }

        public string? NumericFormat { get; }

        public string? ObjectBuilderName { get; }

        public string? ObjectInstanceName { get; }

        public string? ObjectKindName { get; }

        public string? StringFormat { get; }

        public TypeDeclaration TypeDeclaration { get; }

        /// <summary>
        /// Gets the <c>Kind</c> enum value name for surfacing this constituent's own <c>Source</c>
        /// (e.g. the result of <c>Constituent.Build(...)</c>) through the union's <c>Source</c>.
        /// Non-null only for a discriminated-union object constituent (issue #812).
        /// </summary>
        public string? SourceKindName { get; }

        /// <summary>
        /// Gets the backing-field name for <see cref="SourceKindName"/>.
        /// </summary>
        public string? SourceInstanceName { get; }
    }
}