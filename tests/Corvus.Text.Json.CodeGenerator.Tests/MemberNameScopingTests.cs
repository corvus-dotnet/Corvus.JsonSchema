// <copyright file="MemberNameScopingTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using CG = Corvus.Json.CodeGeneration.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="CodeGeneratorExtensions"/> member name scoping methods,
/// exercising uncovered GetFieldNameInScope, GetParameterNameInScope,
/// GetVariableNameInScope, GetTypeNameInScope, GetStaticReadOnlyFieldNameInScope,
/// GetStaticReadOnlyPropertyNameInScope, all GetUnique* variants,
/// and the Reserve*/TryReserveName methods.
/// </summary>
[TestClass]
    public class MemberNameScopingTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region GetFieldNameInScope

    [TestMethod]
    public void GetFieldNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("myField");
        Assert.AreEqual("myField", name);
    }

    [TestMethod]
    public void GetFieldNameInScope_WithPrefix_IncludesPrefix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("field", prefix: "is");
        StringAssert.Contains(name, "is");
    }

    [TestMethod]
    public void GetFieldNameInScope_WithSuffix_IncludesSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("field", suffix: "Backing");
        StringAssert.Contains(name, "Backing");
    }

    [TestMethod]
    public void GetFieldNameInScope_WithChildScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("backing", childScope: "Properties");
        Assert.AreEqual("backing", name);
    }

    [TestMethod]
    public void GetFieldNameInScope_WithRootScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("SomeType", 0);
        string name = gen.GetFieldNameInScope("backing", rootScope: "Overridden");
        Assert.AreEqual("backing", name);
    }

    #endregion

    #region GetParameterNameInScope

    [TestMethod]
    public void GetParameterNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("value");
        Assert.AreEqual("value", name);
    }

    [TestMethod]
    public void GetParameterNameInScope_PascalInput_ConvertsToCamel()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("MyParam");
        Assert.AreEqual("myParam", name);
    }

    [TestMethod]
    public void GetParameterNameInScope_WithPrefixAndSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("item", prefix: "source", suffix: "Value");
        Assert.IsTrue((name).Any());
    }

    #endregion

    #region GetVariableNameInScope

    [TestMethod]
    public void GetVariableNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetVariableNameInScope("result");
        Assert.AreEqual("result", name);
    }

    [TestMethod]
    public void GetVariableNameInScope_WithChildScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetVariableNameInScope("counter", childScope: "Inner");
        Assert.AreEqual("counter", name);
    }

    #endregion

    #region GetTypeNameInScope

    [TestMethod]
    public void GetTypeNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetTypeNameInScope("schema");
        Assert.AreEqual("Schema", name);
    }

    [TestMethod]
    public void GetTypeNameInScope_AlreadyPascal()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetTypeNameInScope("MySchema");
        Assert.AreEqual("MySchema", name);
    }

    #endregion

    #region GetStaticReadOnlyFieldNameInScope

    [TestMethod]
    public void GetStaticReadOnlyFieldNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetStaticReadOnlyFieldNameInScope("pattern");
        Assert.AreEqual("Pattern", name);
    }

    #endregion

    #region GetStaticReadOnlyPropertyNameInScope

    [TestMethod]
    public void GetStaticReadOnlyPropertyNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetStaticReadOnlyPropertyNameInScope("instance");
        Assert.AreEqual("Instance", name);
    }

    #endregion

    #region GetUnique* methods

    [TestMethod]
    public void GetUniqueClassNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueClassNameInScope("Helper");
        string name2 = gen.GetUniqueClassNameInScope("Helper");

        Assert.AreEqual("Helper", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueFieldNameInScope_ReturnsUniqueCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueFieldNameInScope("counter");
        string name2 = gen.GetUniqueFieldNameInScope("counter");

        Assert.AreEqual("counter", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueMethodNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueMethodNameInScope("Validate");
        string name2 = gen.GetUniqueMethodNameInScope("Validate");

        Assert.AreEqual("Validate", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueParameterNameInScope_ReturnsUniqueCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueParameterNameInScope("input");
        string name2 = gen.GetUniqueParameterNameInScope("input");

        Assert.AreEqual("input", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniquePropertyNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniquePropertyNameInScope("Value");
        string name2 = gen.GetUniquePropertyNameInScope("Value");

        Assert.AreEqual("Value", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueStaticReadOnlyFieldNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticReadOnlyFieldNameInScope("Pattern");
        string name2 = gen.GetUniqueStaticReadOnlyFieldNameInScope("Pattern");

        Assert.AreEqual("Pattern", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueStaticReadOnlyPropertyNameInScope_ReturnsUnique()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticReadOnlyPropertyNameInScope("Instance");
        string name2 = gen.GetUniqueStaticReadOnlyPropertyNameInScope("Instance");

        Assert.AreEqual("Instance", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueStaticMethodNameInScope_ReturnsUnique()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticMethodNameInScope("Create");
        string name2 = gen.GetUniqueStaticMethodNameInScope("Create");

        Assert.AreEqual("Create", name1);
        Assert.AreNotEqual(name1, name2);
    }

    [TestMethod]
    public void GetUniqueVariableNameInScope_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueVariableNameInScope("result");
        string name2 = gen.GetUniqueVariableNameInScope("result");

        Assert.AreEqual("result", name1);
        Assert.AreNotEqual(name1, name2);
    }

    #endregion

    #region ReserveName and TryReserveName

    [TestMethod]
    public void ReserveName_BasicName_ReservesSuccessfully()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CG result = gen.ReserveName("Reserved");
        Assert.AreSame(gen, result);
    }

    [TestMethod]
    public void ReserveName_WithChildScope_ReservesInCorrectScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("MyField", childScope: "Properties");

        // After reserving, getting the same name should still work
        // (reserve doesn't prevent get, it prevents unique collision)
        string name = gen.GetFieldNameInScope("MyField", childScope: "Properties");
        Assert.IsTrue((name).Any());
    }

    [TestMethod]
    public void ReserveNameIfNotReserved_FirstTime_Reserves()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CG result = gen.ReserveNameIfNotReserved("NewName");
        Assert.AreSame(gen, result);
    }

    [TestMethod]
    public void ReserveNameIfNotReserved_AlreadyReserved_NoOp()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("Existing");
        CG result = gen.ReserveNameIfNotReserved("Existing");
        Assert.AreSame(gen, result);
    }

    [TestMethod]
    public void TryReserveName_NewName_ReturnsTrue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        bool reserved = gen.TryReserveName("Available");
        Assert.IsTrue(reserved);
    }

    [TestMethod]
    public void TryReserveName_AlreadyReserved_ReturnsFalse()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("Taken");
        bool reserved = gen.TryReserveName("Taken");
        Assert.IsFalse(reserved);
    }

    [TestMethod]
    public void TryReserveName_WithRootScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        bool reserved = gen.TryReserveName("ScopedName", rootScope: "AltRoot");
        Assert.IsTrue(reserved);
    }

    #endregion

    #region Scoping with childScope and rootScope

    [TestMethod]
    public void GetMethodNameInScope_WithChildScope_ScopesCorrectly()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("RootType", 0);
        string name = gen.GetMethodNameInScope("Validate", childScope: "Properties");
        Assert.AreEqual("Validate", name);
    }

    [TestMethod]
    public void GetPropertyNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetPropertyNameInScope("value");
        Assert.AreEqual("Value", name);
    }

    [TestMethod]
    public void GetPropertyNameInScope_WithSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetPropertyNameInScope("item", suffix: "Array");
        StringAssert.Contains(name, "Array");
    }

    #endregion

    #region Idempotency (GetOrAdd semantics)

    [TestMethod]
    public void GetFieldNameInScope_SameNameTwice_ReturnsSameResult()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetFieldNameInScope("backing");
        string name2 = gen.GetFieldNameInScope("backing");
        Assert.AreEqual(name1, name2);
    }

    [TestMethod]
    public void GetMethodNameInScope_SameNameTwice_ReturnsSameResult()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetMethodNameInScope("Process");
        string name2 = gen.GetMethodNameInScope("Process");
        Assert.AreEqual(name1, name2);
    }

    #endregion
}
