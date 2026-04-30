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
        .AppendLineIndent("/// <summary>")
        .AppendLineIndent("/// Gets a <see cref=\"", typeName, "\"/> which can be safely stored beyond the lifetime of the")
        .AppendLineIndent("/// original document.")
        .AppendLineIndent("/// </summary>")
        .AppendLineIndent("/// <returns>")
        .AppendLineIndent("/// A <see cref=\"", typeName, "\"/> which can be safely stored beyond the lifetime of the")
        .AppendLineIndent("/// original document.")
        .AppendLineIndent("/// </returns>")
        .AppendLineIndent("/// <remarks>")
        .AppendLineIndent("/// <para>")
        .AppendLineIndent("/// If this instance is already a clone (its backing document is not disposable),")
        .AppendLineIndent("/// this method returns the same instance without additional allocation.")
        .AppendLineIndent("/// </para>")
        .AppendLineIndent("/// </remarks>")
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
        .AppendLineIndent("/// <summary>")
        .AppendLineIndent("/// Creates a frozen (immutable) copy of this element if it is backed by a mutable document,")
        .AppendLineIndent("/// or returns this instance if it is already immutable.")
        .AppendLineIndent("/// </summary>")
        .AppendLineIndent("/// <returns>")
        .AppendLineIndent("/// An immutable <see cref=\"", typeName, "\"/> that lives for the lifetime of its")
        .AppendLineIndent("/// workspace and its associated documents.")
        .AppendLineIndent("/// </returns>")
        .AppendLineIndent("/// <remarks>")
        .AppendLineIndent("/// <para>")
        .AppendLineIndent("/// Unlike <see cref=\"Clone()\"/>, which serializes the element and re-parses it")
        .AppendLineIndent("/// into a standalone heap-allocated document, <c>Freeze()</c> performs a cheap")
        .AppendLineIndent("/// blit of the metadata and value backing arrays. The resulting element is")
        .AppendLineIndent("/// immutable but is only valid for the lifetime of the workspace.")
        .AppendLineIndent("/// </para>")
        .AppendLineIndent("/// <para>")
        .AppendLineIndent("/// If this instance is already backed by an immutable document, it is returned as-is.")
        .AppendLineIndent("/// </para>")
        .AppendLineIndent("/// </remarks>")
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
        .AppendLineIndent("/// <summary>")
        .AppendLineIndent("/// Gets a <see cref=\"", immutableTypeName, "\"/> which can be safely stored beyond the lifetime of the")
        .AppendLineIndent("/// original document.")
        .AppendLineIndent("/// </summary>")
        .AppendLineIndent("/// <returns>")
        .AppendLineIndent("/// A <see cref=\"", immutableTypeName, "\"/> which can be safely stored beyond the lifetime of the")
        .AppendLineIndent("/// original document.")
        .AppendLineIndent("/// </returns>")
        .AppendLineIndent("/// <remarks>")
        .AppendLineIndent("/// <para>")
        .AppendLineIndent("/// This serializes the element and re-parses it into a standalone heap-allocated")
        .AppendLineIndent("/// document. The result is independent of the workspace.")
        .AppendLineIndent("/// </para>")
        .AppendLineIndent("/// </remarks>")
        .AppendLineIndent("public readonly ", immutableTypeName, " Clone()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("return _parent.CloneElement<", immutableTypeName, ">(_idx);")
        .PopIndent()
        .AppendLineIndent("}")
        .AppendSeparatorLine()
        .AppendLineIndent("/// <summary>")
        .AppendLineIndent("/// Creates a frozen (immutable) copy of this element, backed by a new")
        .AppendLineIndent("/// document builder registered in the same workspace.")
        .AppendLineIndent("/// </summary>")
        .AppendLineIndent("/// <returns>")
        .AppendLineIndent("/// An immutable <see cref=\"", immutableTypeName, "\"/> that lives for the lifetime of its")
        .AppendLineIndent("/// workspace and its associated documents.")
        .AppendLineIndent("/// </returns>")
        .AppendLineIndent("/// <remarks>")
        .AppendLineIndent("/// <para>")
        .AppendLineIndent("/// Unlike <see cref=\"Clone()\"/>, which serializes the element and re-parses it")
        .AppendLineIndent("/// into a standalone heap-allocated document, <c>Freeze()</c> performs a cheap")
        .AppendLineIndent("/// blit of the metadata and value backing arrays. The resulting element is")
        .AppendLineIndent("/// immutable but is only valid for the lifetime of the workspace.")
        .AppendLineIndent("/// </para>")
        .AppendLineIndent("/// </remarks>")
        .AppendLineIndent("public readonly ", immutableTypeName, " Freeze()")
        .AppendLineIndent("{")
        .PushIndent()
            .AppendLineIndent("CheckValidInstance();")
            .AppendLineIndent("return _parent.FreezeElement<", immutableTypeName, ">(_idx);")
        .PopIndent()
        .AppendLineIndent("}");
    }
}