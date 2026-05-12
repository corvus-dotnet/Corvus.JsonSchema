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
[TestClass]
    public class MethodParameterTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region String parameter name constructor (already partially covered)

    [TestMethod]
    public void StringConstructor_SetsAllProperties()
    {
        var p = new MethodParameter("ref", "int", "count", typeIsNullable: false, defaultValue: "0");
        Assert.AreEqual("ref", p.Modifiers);
        Assert.AreEqual("int", p.Type);
        Assert.IsFalse(p.TypeIsNullable);
        Assert.AreEqual("0", p.DefaultValue);
        Assert.IsTrue(p.RequiresReservedName);
    }

    #endregion

    #region MemberName parameter constructor (lines 47-52, uncovered)

    [TestMethod]
    public void MemberNameConstructor_SetsProperties()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "value",
            Casing.CamelCase);

        var p = new MethodParameter("in", "ReadOnlySpan<byte>", memberName, defaultValue: null);
        Assert.AreEqual("in", p.Modifiers);
        Assert.AreEqual("ReadOnlySpan<byte>", p.Type);
        Assert.IsFalse(p.RequiresReservedName);
        Assert.IsNull(p.DefaultValue);
    }

    [TestMethod]
    public void MemberNameConstructor_WithDefaultValue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "level",
            Casing.CamelCase);

        var p = new MethodParameter("", "int", memberName, "42");
        Assert.AreEqual("42", p.DefaultValue);
    }

    #endregion

    #region RequiresReservedName (line 67)

    [TestMethod]
    public void RequiresReservedName_WithMemberName_ReturnsFalse()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "x",
            Casing.CamelCase);

        var p = new MethodParameter("", "int", memberName);
        Assert.IsFalse(p.RequiresReservedName);
    }

    [TestMethod]
    public void RequiresReservedName_WithStringName_ReturnsTrue()
    {
        var p = new MethodParameter("", "int", "x");
        Assert.IsTrue(p.RequiresReservedName);
    }

    #endregion

    #region Implicit operators (lines 89, 101, 107)

    [TestMethod]
    public void ImplicitOperator_TypeAndParameterName()
    {
        MethodParameter p = ("string", "name");
        Assert.AreEqual(string.Empty, p.Modifiers);
        Assert.AreEqual("string", p.Type);
        Assert.IsTrue(p.RequiresReservedName);
    }

    [TestMethod]
    public void ImplicitOperator_WithNullableAndDefaultValue()
    {
        MethodParameter p = ("string?", "name", true, "null");
        Assert.AreEqual(string.Empty, p.Modifiers);
        Assert.AreEqual("string?", p.Type);
        Assert.IsTrue(p.TypeIsNullable);
        Assert.AreEqual("null", p.DefaultValue);
    }

    [TestMethod]
    public void ImplicitOperator_WithDefaultValueOnly()
    {
        MethodParameter p = ("int", "count", "0");
        Assert.AreEqual(string.Empty, p.Modifiers);
        Assert.AreEqual("int", p.Type);
        Assert.AreEqual("0", p.DefaultValue);
    }

    [TestMethod]
    public void ImplicitOperator_MemberNameTuple()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "result",
            Casing.CamelCase);

        MethodParameter p = ("bool", memberName);
        Assert.AreEqual(string.Empty, p.Modifiers);
        Assert.AreEqual("bool", p.Type);
        Assert.IsFalse(p.RequiresReservedName);
    }

    [TestMethod]
    public void ImplicitOperator_MemberNameWithDefaultValue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "flag",
            Casing.CamelCase);

        MethodParameter p = ("bool", memberName, "true");
        Assert.AreEqual(string.Empty, p.Modifiers);
        Assert.AreEqual("bool", p.Type);
        Assert.AreEqual("true", p.DefaultValue);
        Assert.IsFalse(p.RequiresReservedName);
    }

    #endregion

    #region GetName (lines 131-133, 136)

    [TestMethod]
    public void GetName_WithSpecificName_ReturnsSpecificName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var p = new MethodParameter("", "int", "myParam");
        string name = p.GetName(gen);
        Assert.AreEqual("myParam", name);
    }

    [TestMethod]
    public void GetName_WithSpecificName_IsDeclaration_ReservesName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var p = new MethodParameter("", "int", "reserved");
        string name = p.GetName(gen, isDeclaration: true);
        Assert.AreEqual("reserved", name);

        // Should not throw — name is now reserved
        bool tryResult = gen.TryReserveName(
            new CSharpMemberName(gen.FullyQualifiedScope, "reserved", Casing.Unmodified));
        Assert.IsFalse(tryResult); // Already reserved
    }

    [TestMethod]
    public void GetName_WithMemberName_ReturnsResolvedName()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        var memberName = new CSharpMemberName(
            gen.FullyQualifiedScope,
            "input",
            Casing.CamelCase);

        var p = new MethodParameter("", "string", memberName);
        string name = p.GetName(gen);
        Assert.AreEqual("input", name);
    }

    [TestMethod]
    public void GetName_NoNameSet_Throws()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);

        // Default struct — no name set
        MethodParameter p = default;
        Assert.ThrowsExactly<InvalidOperationException>(() => p.GetName(gen));
    }

    #endregion
}
