// <copyright file="CodeGeneratorExtensions.CloneAndFreeze.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;

namespace Corvus.Text.Json.CodeGeneration;

/// <summary>
/// Extension methods for generating Clone() and Freeze() methods on generated types.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends a typed Clone() method on an immutable generated type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendCloneMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string typeName = typeDeclaration.DotnetTypeName();

        return generator
        .ReserveName("Clone")
        .AppendSeparatorLine()
        .AppendInheritDoc("Clone()")
        .AppendLineIndent("public ", typeName, " Clone()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("return _parent.CloneElement<", typeName, ">(_idx);")
        .PopIndent()
        .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends a typed Freeze() method on an immutable generated type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the method.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendFreezeMethod(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string typeName = typeDeclaration.DotnetTypeName();

        return generator
        .ReserveName("Freeze")
        .AppendSeparatorLine()
        .AppendInheritDoc("Freeze()")
        .AppendLineIndent("public ", typeName, " Freeze()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("if (_parent is global::Corvus.Text.Json.Internal.IMutableJsonDocument mutable)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("return mutable.FreezeElement<", typeName, ">(_idx);")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .AppendLineIndent("return this;")
        .PopIndent()
        .AppendLineIndent("}");
    }

    /// <summary>
    /// Appends typed Clone() and Freeze() methods on a mutable generated type.
    /// </summary>
    /// <param name="generator">The code generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A reference to the generator having completed the operation.</returns>
    public static CodeGenerator AppendMutableCloneAndFreezeMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        if (generator.IsCancellationRequested)
        {
            return generator;
        }

        string immutableTypeName = typeDeclaration.DotnetTypeName();

        return generator
        .ReserveName("Clone")
        .ReserveName("Freeze")
        .AppendSeparatorLine()
        .AppendInheritDoc("Clone()", forMutable: true)
        .AppendLineIndent("public readonly ", immutableTypeName, " Clone()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("return _parent.CloneElement<", immutableTypeName, ">(_idx);")
        .PopIndent()
        .AppendLineIndent("}")
        .AppendSeparatorLine()
        .AppendInheritDoc("Freeze()", forMutable: true)
        .AppendLineIndent("public readonly ", immutableTypeName, " Freeze()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("return _parent.FreezeElement<", immutableTypeName, ">(_idx);")
        .PopIndent()
        .AppendLineIndent("}");
    }
}