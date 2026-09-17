// <copyright file="ConditionalCodeSpecificationCallbackTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json.CodeGeneration;
using Corvus.Text.Json.CodeGeneration;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// The conditional-code helpers have two callback shapes: one receives a delegate over the specification (the
/// original API), one receives the specification itself (no boxing). Both must emit the same text.
/// </summary>
[TestClass]
public class ConditionalCodeSpecificationCallbackTests
{
    private static readonly ConditionalCodeSpecification[] Specifications =
    [
        new(g => g.Append("IAlways"), FrameworkType.All),
        new(g => g.Append("INet80OrGreater"), FrameworkType.Net80OrGreater),
        new(g => g.Append("IPreNet80"), FrameworkType.PreNet80),
        "IExplicit",
    ];

    [TestMethod]
    public void AppendConditionalsInOrder_DelegateAndSpecificationCallbacksEmitTheSameText()
    {
        string withDelegate = Emit(g => ConditionalCodeSpecification.AppendConditionalsInOrder(g, Specifications, (generator, append, index) => AppendItem(generator, index, append)));
        string withSpecification = Emit(g => ConditionalCodeSpecification.AppendConditionalsInOrder(g, Specifications, (generator, spec, index) => AppendItem(generator, index, spec.Append)));

        Assert.AreNotEqual(string.Empty, withSpecification);
        Assert.AreEqual(withDelegate, withSpecification);
        StringAssert.Contains(withSpecification, "IExplicit");
    }

    [TestMethod]
    public void AppendConditionalsGroupingBlocks_DelegateAndSpecificationCallbacksEmitTheSameText()
    {
        string withDelegate = Emit(g => ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(g, Specifications, (generator, append, index) => AppendItem(generator, index, append)));
        string withSpecification = Emit(g => ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(g, Specifications, (generator, spec, index) => AppendItem(generator, index, spec.Append)));

        Assert.AreNotEqual(string.Empty, withSpecification);
        Assert.AreEqual(withDelegate, withSpecification);
        StringAssert.Contains(withSpecification, "#if NET8_0_OR_GREATER");
    }

    private static string Emit(Action<Corvus.Json.CodeGeneration.CodeGenerator> append)
    {
        Corvus.Json.CodeGeneration.CodeGenerator generator = new(CSharpLanguageProvider.Default, CancellationToken.None);
        append(generator);
        return generator.ToString();
    }

    private static void AppendItem(Corvus.Json.CodeGeneration.CodeGenerator generator, int index, Action<Corvus.Json.CodeGeneration.CodeGenerator> append)
    {
        generator.AppendIndent(index == 0 ? ": " : ", ");
        append(generator);
        generator.AppendLine();
    }
}