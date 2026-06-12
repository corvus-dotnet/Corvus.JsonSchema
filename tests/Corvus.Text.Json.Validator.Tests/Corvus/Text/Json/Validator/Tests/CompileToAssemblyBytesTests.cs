// <copyright file="CompileToAssemblyBytesTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Reflection;
using Corvus.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.Validator.Tests;

/// <summary>
/// Tests <see cref="DynamicCompiler.CompileToAssemblyBytes"/> — compiling a set of source files into a single
/// assembly and returning its raw PE bytes (so the assembly can be stored, not just loaded).
/// </summary>
[TestClass]
public class CompileToAssemblyBytesTests
{
    [TestMethod]
    public void Compiles_sources_to_loadable_assembly_bytes()
    {
        GeneratedCodeFile[] files =
        [
            new("Foo.cs", "namespace Demo { public static class Foo { public static int Answer() => 42; } }"),
        ];

        byte[] bytes = DynamicCompiler.CompileToAssemblyBytes(files, typeof(CompileToAssemblyBytesTests).Assembly);

        Assert.IsTrue(bytes.Length > 0);
        Assert.AreEqual((byte)'M', bytes[0]); // PE/DLL header "MZ"
        Assert.AreEqual((byte)'Z', bytes[1]);

        Assembly assembly = Assembly.Load(bytes);
        Type fooType = assembly.GetType("Demo.Foo");
        object result = fooType.GetMethod("Answer").Invoke(null, null);
        Assert.AreEqual(42, result);
    }

    [TestMethod]
    public void Throws_on_invalid_source()
    {
        GeneratedCodeFile[] files = [new("Bad.cs", "namespace Demo { public class Bad { this is not c# } }")];

        InvalidOperationException caught = null;
        try
        {
            DynamicCompiler.CompileToAssemblyBytes(files, typeof(CompileToAssemblyBytesTests).Assembly);
        }
        catch (InvalidOperationException ex)
        {
            caught = ex;
        }

        Assert.IsNotNull(caught);
        StringAssert.Contains(caught.Message, "Unable to compile generated code");
    }
}
