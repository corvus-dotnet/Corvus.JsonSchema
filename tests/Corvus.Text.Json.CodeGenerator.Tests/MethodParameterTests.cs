// <copyright file="MethodParameterTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using CG = Corvus.Json.CodeGeneration.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="MethodParameter"/>, exercising uncovered constructors,
/// implicit operators, and GetName paths.
/// </summary>
public static class MethodParameterTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region String parameter name constructor (already partially covered)

    [Fact]
    public static void StringConstructor_SetsAllProperties()
    {
        var p = new MethodParameter("ref", "int", "count", typeIsNullable: false, defaultValue: "0");
        Assert.Equal("ref", p.Modifiers);
        Assert.Equal("int", p.Type);
        Assert.False(p.TypeIsNullable);
        Assert.Equal("0", p.DefaultValue);
        Assert.True(p.RequiresReservedName);
    }

    #endregion

    #region MemberName parameter constructor (lines 47-52, uncovered)

    [Fact]
    public static void MemberNameConstructor_SetsProperties()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "value",
            Casing.CamelCase);

        var p = new MethodParameter("in", "ReadOnlySpan<byte>", memberName, defaultValue: null);
        Assert.Equal("in", p.Modifiers);
        Assert.Equal("ReadOnlySpan<byte>", p.Type);
        Assert.False(p.RequiresReservedName);
        Assert.Null(p.DefaultValue);
    }

    [Fact]
    public static void MemberNameConstructor_WithDefaultValue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "level",
            Casing.CamelCase);

        var p = new MethodParameter("", "int", memberName, "42");
        Assert.Equal("42", p.DefaultValue);
    }

    #endregion

    #region RequiresReservedName (line 67)

    [Fact]
    public static void RequiresReservedName_WithMemberName_ReturnsFalse()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "x",
            Casing.CamelCase);

        var p = new MethodParameter("", "int", memberName);
        Assert.False(p.RequiresReservedName);
    }

    [Fact]
    public static void RequiresReservedName_WithStringName_ReturnsTrue()
    {
        var p = new MethodParameter("", "int", "x");
        Assert.True(p.RequiresReservedName);
    }

    #endregion

    #region Implicit operators (lines 89, 101, 107)

    [Fact]
    public static void ImplicitOperator_TypeAndParameterName()
    {
        MethodParameter p = ("string", "name");
        Assert.Equal(string.Empty, p.Modifiers);
        Assert.Equal("string", p.Type);
        Assert.True(p.RequiresReservedName);
    }

    [Fact]
    public static void ImplicitOperator_WithNullableAndDefaultValue()
    {
        MethodParameter p = ("string?", "name", true, "null");
        Assert.Equal(string.Empty, p.Modifiers);
        Assert.Equal("string?", p.Type);
        Assert.True(p.TypeIsNullable);
        Assert.Equal("null", p.DefaultValue);
    }

    [Fact]
    public static void ImplicitOperator_WithDefaultValueOnly()
    {
        MethodParameter p = ("int", "count", "0");
        Assert.Equal(string.Empty, p.Modifiers);
        Assert.Equal("int", p.Type);
        Assert.Equal("0", p.DefaultValue);
    }

    [Fact]
    public static void ImplicitOperator_MemberNameTuple()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "result",
            Casing.CamelCase);

        MethodParameter p = ("bool", memberName);
        Assert.Equal(string.Empty, p.Modifiers);
        Assert.Equal("bool", p.Type);
        Assert.False(p.RequiresReservedName);
    }

    [Fact]
    public static void ImplicitOperator_MemberNameWithDefaultValue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "flag",
            Casing.CamelCase);

        MethodParameter p = ("bool", memberName, "true");
        Assert.Equal(string.Empty, p.Modifiers);
        Assert.Equal("bool", p.Type);
        Assert.Equal("true", p.DefaultValue);
        Assert.False(p.RequiresReservedName);
    }

    #endregion

    #region GetName (lines 131-133, 136)

    [Fact]
    public static void GetName_WithSpecificName_ReturnsSpecificName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var p = new MethodParameter("", "int", "myParam");
        string name = p.GetName(gen);
        Assert.Equal("myParam", name);
    }

    [Fact]
    public static void GetName_WithSpecificName_IsDeclaration_ReservesName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var p = new MethodParameter("", "int", "reserved");
        string name = p.GetName(gen, isDeclaration: true);
        Assert.Equal("reserved", name);

        // Should not throw — name is now reserved
        bool tryResult = gen.TryReserveName(
            new CSharpMemberName(gen.FullyQualifiedScope, "reserved", Casing.Unmodified));
        Assert.False(tryResult); // Already reserved
    }

    [Fact]
    public static void GetName_WithMemberName_ReturnsResolvedName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "input",
            Casing.CamelCase);

        var p = new MethodParameter("", "string", memberName);
        string name = p.GetName(gen);
        Assert.Equal("input", name);
    }

    [Fact]
    public static void GetName_NoNameSet_Throws()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);

        // Default struct — no name set
        MethodParameter p = default;
        Assert.Throws<InvalidOperationException>(() => p.GetName(gen));
    }

    #endregion
}
