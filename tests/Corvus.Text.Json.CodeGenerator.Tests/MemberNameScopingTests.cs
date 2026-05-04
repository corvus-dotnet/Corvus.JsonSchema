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
public static class MemberNameScopingTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region GetFieldNameInScope

    [Fact]
    public static void GetFieldNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("myField");
        Assert.Equal("myField", name);
    }

    [Fact]
    public static void GetFieldNameInScope_WithPrefix_IncludesPrefix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("field", prefix: "is");
        Assert.Contains("is", name);
    }

    [Fact]
    public static void GetFieldNameInScope_WithSuffix_IncludesSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("field", suffix: "Backing");
        Assert.Contains("Backing", name);
    }

    [Fact]
    public static void GetFieldNameInScope_WithChildScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetFieldNameInScope("backing", childScope: "Properties");
        Assert.Equal("backing", name);
    }

    [Fact]
    public static void GetFieldNameInScope_WithRootScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("SomeType", 0);
        string name = gen.GetFieldNameInScope("backing", rootScope: "Overridden");
        Assert.Equal("backing", name);
    }

    #endregion

    #region GetParameterNameInScope

    [Fact]
    public static void GetParameterNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("value");
        Assert.Equal("value", name);
    }

    [Fact]
    public static void GetParameterNameInScope_PascalInput_ConvertsToCamel()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("MyParam");
        Assert.Equal("myParam", name);
    }

    [Fact]
    public static void GetParameterNameInScope_WithPrefixAndSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetParameterNameInScope("item", prefix: "source", suffix: "Value");
        Assert.NotEmpty(name);
    }

    #endregion

    #region GetVariableNameInScope

    [Fact]
    public static void GetVariableNameInScope_BasicName_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetVariableNameInScope("result");
        Assert.Equal("result", name);
    }

    [Fact]
    public static void GetVariableNameInScope_WithChildScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetVariableNameInScope("counter", childScope: "Inner");
        Assert.Equal("counter", name);
    }

    #endregion

    #region GetTypeNameInScope

    [Fact]
    public static void GetTypeNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetTypeNameInScope("schema");
        Assert.Equal("Schema", name);
    }

    [Fact]
    public static void GetTypeNameInScope_AlreadyPascal()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetTypeNameInScope("MySchema");
        Assert.Equal("MySchema", name);
    }

    #endregion

    #region GetStaticReadOnlyFieldNameInScope

    [Fact]
    public static void GetStaticReadOnlyFieldNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetStaticReadOnlyFieldNameInScope("pattern");
        Assert.Equal("Pattern", name);
    }

    #endregion

    #region GetStaticReadOnlyPropertyNameInScope

    [Fact]
    public static void GetStaticReadOnlyPropertyNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetStaticReadOnlyPropertyNameInScope("instance");
        Assert.Equal("Instance", name);
    }

    #endregion

    #region GetUnique* methods

    [Fact]
    public static void GetUniqueClassNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueClassNameInScope("Helper");
        string name2 = gen.GetUniqueClassNameInScope("Helper");

        Assert.Equal("Helper", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueFieldNameInScope_ReturnsUniqueCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueFieldNameInScope("counter");
        string name2 = gen.GetUniqueFieldNameInScope("counter");

        Assert.Equal("counter", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueMethodNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueMethodNameInScope("Validate");
        string name2 = gen.GetUniqueMethodNameInScope("Validate");

        Assert.Equal("Validate", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueParameterNameInScope_ReturnsUniqueCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueParameterNameInScope("input");
        string name2 = gen.GetUniqueParameterNameInScope("input");

        Assert.Equal("input", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniquePropertyNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniquePropertyNameInScope("Value");
        string name2 = gen.GetUniquePropertyNameInScope("Value");

        Assert.Equal("Value", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueStaticReadOnlyFieldNameInScope_ReturnsUniquePascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticReadOnlyFieldNameInScope("Pattern");
        string name2 = gen.GetUniqueStaticReadOnlyFieldNameInScope("Pattern");

        Assert.Equal("Pattern", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueStaticReadOnlyPropertyNameInScope_ReturnsUnique()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticReadOnlyPropertyNameInScope("Instance");
        string name2 = gen.GetUniqueStaticReadOnlyPropertyNameInScope("Instance");

        Assert.Equal("Instance", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueStaticMethodNameInScope_ReturnsUnique()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueStaticMethodNameInScope("Create");
        string name2 = gen.GetUniqueStaticMethodNameInScope("Create");

        Assert.Equal("Create", name1);
        Assert.NotEqual(name1, name2);
    }

    [Fact]
    public static void GetUniqueVariableNameInScope_ReturnsCamelCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetUniqueVariableNameInScope("result");
        string name2 = gen.GetUniqueVariableNameInScope("result");

        Assert.Equal("result", name1);
        Assert.NotEqual(name1, name2);
    }

    #endregion

    #region ReserveName and TryReserveName

    [Fact]
    public static void ReserveName_BasicName_ReservesSuccessfully()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CG result = gen.ReserveName("Reserved");
        Assert.Same(gen, result);
    }

    [Fact]
    public static void ReserveName_WithChildScope_ReservesInCorrectScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("MyField", childScope: "Properties");

        // After reserving, getting the same name should still work
        // (reserve doesn't prevent get, it prevents unique collision)
        string name = gen.GetFieldNameInScope("MyField", childScope: "Properties");
        Assert.NotEmpty(name);
    }

    [Fact]
    public static void ReserveNameIfNotReserved_FirstTime_Reserves()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        CG result = gen.ReserveNameIfNotReserved("NewName");
        Assert.Same(gen, result);
    }

    [Fact]
    public static void ReserveNameIfNotReserved_AlreadyReserved_NoOp()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("Existing");
        CG result = gen.ReserveNameIfNotReserved("Existing");
        Assert.Same(gen, result);
    }

    [Fact]
    public static void TryReserveName_NewName_ReturnsTrue()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        bool reserved = gen.TryReserveName("Available");
        Assert.True(reserved);
    }

    [Fact]
    public static void TryReserveName_AlreadyReserved_ReturnsFalse()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        gen.ReserveName("Taken");
        bool reserved = gen.TryReserveName("Taken");
        Assert.False(reserved);
    }

    [Fact]
    public static void TryReserveName_WithRootScope()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        bool reserved = gen.TryReserveName("ScopedName", rootScope: "AltRoot");
        Assert.True(reserved);
    }

    #endregion

    #region Scoping with childScope and rootScope

    [Fact]
    public static void GetMethodNameInScope_WithChildScope_ScopesCorrectly()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("RootType", 0);
        string name = gen.GetMethodNameInScope("Validate", childScope: "Properties");
        Assert.Equal("Validate", name);
    }

    [Fact]
    public static void GetPropertyNameInScope_BasicName_ReturnsPascalCase()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetPropertyNameInScope("value");
        Assert.Equal("Value", name);
    }

    [Fact]
    public static void GetPropertyNameInScope_WithSuffix()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name = gen.GetPropertyNameInScope("item", suffix: "Array");
        Assert.Contains("Array", name);
    }

    #endregion

    #region Idempotency (GetOrAdd semantics)

    [Fact]
    public static void GetFieldNameInScope_SameNameTwice_ReturnsSameResult()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetFieldNameInScope("backing");
        string name2 = gen.GetFieldNameInScope("backing");
        Assert.Equal(name1, name2);
    }

    [Fact]
    public static void GetMethodNameInScope_SameNameTwice_ReturnsSameResult()
    {
        CG gen = CreateGenerator();
        gen.PushMemberScope("TestType", 0);
        string name1 = gen.GetMethodNameInScope("Process");
        string name2 = gen.GetMethodNameInScope("Process");
        Assert.Equal(name1, name2);
    }

    #endregion
}
