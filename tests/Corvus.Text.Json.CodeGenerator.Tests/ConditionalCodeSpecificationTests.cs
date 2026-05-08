// <copyright file="ConditionalCodeSpecificationTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json.CodeGeneration;
using CG = Corvus.Json.CodeGeneration.CodeGenerator;

namespace Corvus.Text.Json.CodeGenerator.Tests;

/// <summary>
/// Tests for <see cref="ConditionalCodeSpecification"/> covering conditional code emission,
/// grouping blocks, and ordering.
/// </summary>
[TestClass]
    public class ConditionalCodeSpecificationTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region AppendConditional

    [TestMethod]
    public void AppendConditional_NotEmitted_EmitsNothing()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.Append("should not appear"),
            FrameworkType.NotEmitted);

        Assert.AreEqual(string.Empty, gen.ToString());
    }

    [TestMethod]
    public void AppendConditional_All_EmitsWithoutPreprocessor()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.Append("code here"),
            FrameworkType.All);

        string output = gen.ToString();
        StringAssert.Contains(output, "code here");
        Assert.DoesNotContain("#if", output);
    }

    [TestMethod]
    public void AppendConditional_Net80OrGreater_WrapsInPreprocessor()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("net80+ code"),
            FrameworkType.Net80OrGreater);

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "net80+ code");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void AppendConditional_PreNet80_WrapsWithNotNet80()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("pre-net80 code"),
            FrameworkType.PreNet80);

        string output = gen.ToString();
        StringAssert.Contains(output, "#if !NET8_0_OR_GREATER");
        StringAssert.Contains(output, "pre-net80 code");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void AppendConditional_Net80_WrapsWithNet80()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("net80 specific"),
            FrameworkType.Net80);

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0");
        StringAssert.Contains(output, "net80 specific");
        StringAssert.Contains(output, "#endif");
    }

    #endregion

    #region Constructor and Append

    [TestMethod]
    public void ActionConstructor_InvokesFunction()
    {
        var spec = new ConditionalCodeSpecification(
            g => g.Append("action content"),
            FrameworkType.All);

        CG gen = CreateGenerator();
        spec.Append(gen);

        StringAssert.Contains(gen.ToString(), "action content");
    }

    [TestMethod]
    public void StringConstructor_AppendsString()
    {
        var spec = new ConditionalCodeSpecification("IDisposable", FrameworkType.Net80OrGreater);

        CG gen = CreateGenerator();
        spec.Append(gen);

        StringAssert.Contains(gen.ToString(), "IDisposable");
    }

    [TestMethod]
    public void ImplicitStringConversion_CreatesAllCondition()
    {
        ConditionalCodeSpecification spec = "IEnumerable";

        CG gen = CreateGenerator();
        spec.Append(gen);

        StringAssert.Contains(gen.ToString(), "IEnumerable");
    }

    [TestMethod]
    public void DoNotEmit_AppendsNothing()
    {
        ConditionalCodeSpecification spec = ConditionalCodeSpecification.DoNotEmit;

        CG gen = CreateGenerator();
        spec.Append(gen);

        Assert.AreEqual(string.Empty, gen.ToString());
    }

    #endregion

    #region AppendConditionalsGroupingBlocks

    [TestMethod]
    public void GroupingBlocks_EmptyArray_EmitsNothing()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            [],
            (g, append, i) => append(g));

        Assert.AreEqual(string.Empty, gen.ToString());
    }

    [TestMethod]
    public void GroupingBlocks_AllItems_NoConditional()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "IFoo",
            "IBar",
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "IFoo");
        StringAssert.Contains(output, "IBar");
        Assert.DoesNotContain("#if", output);
    }

    [TestMethod]
    public void GroupingBlocks_Net80OrGreater_EmitsConditionalBlock()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "IFoo",
            new ConditionalCodeSpecification("INet80Interface", FrameworkType.Net80OrGreater),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "IFoo");
        StringAssert.Contains(output, "INet80Interface");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void GroupingBlocks_PreNet80_EmitsElseBlock()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "ICommon",
            new ConditionalCodeSpecification("INet80Interface", FrameworkType.Net80OrGreater),
            new ConditionalCodeSpecification("ILegacyInterface", FrameworkType.PreNet80),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "INet80Interface");
        StringAssert.Contains(output, "ILegacyInterface");
        StringAssert.Contains(output, "#else");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void GroupingBlocks_Net80Only_EmitsNet80Conditional()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "ICommon",
            new ConditionalCodeSpecification("INet80Only", FrameworkType.Net80),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0");
        StringAssert.Contains(output, "INet80Only");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void GroupingBlocks_AllThreeGroups_EmitsFullConditional()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "ICommon",
            new ConditionalCodeSpecification("IModern", FrameworkType.Net80OrGreater),
            new ConditionalCodeSpecification("INet80", FrameworkType.Net80),
            new ConditionalCodeSpecification("ILegacy", FrameworkType.PreNet80),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "IModern");
        StringAssert.Contains(output, "INet80");
        StringAssert.Contains(output, "ILegacy");
        StringAssert.Contains(output, "ICommon");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void GroupingBlocks_NotEmittedItems_AreSkipped()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "IFoo",
            ConditionalCodeSpecification.DoNotEmit,
            "IBar",
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "IFoo");
        StringAssert.Contains(output, "IBar");
    }

    [TestMethod]
    public void GroupingBlocks_PreNet80WithoutNet80OrGreater_EmitsNotNet80()
    {
        // When only preNet80 is specified (no net80OrGreater), it uses #if !NET8_0_OR_GREATER
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "ICommon",
            new ConditionalCodeSpecification("ILegacy", FrameworkType.PreNet80),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if !NET8_0_OR_GREATER");
        StringAssert.Contains(output, "ILegacy");
        StringAssert.Contains(output, "#endif");
    }

    [TestMethod]
    public void GroupingBlocks_Net80OnlyWithPreNet80_EmitsElifNotNet80()
    {
        // Net80 solo + preNet80 (no net80OrGreater) — emits #if NET8_0 then #elif !NET8_0_OR_GREATER
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "ICommon",
            new ConditionalCodeSpecification("INet80Only", FrameworkType.Net80),
            new ConditionalCodeSpecification("ILegacy", FrameworkType.PreNet80),
        ];

        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            specs,
            (g, append, i) =>
            {
                if (i > 0)
                {
                    g.Append(", ");
                }

                append(g);
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "#if NET8_0");
        StringAssert.Contains(output, "INet80Only");
        StringAssert.Contains(output, "ILegacy");
        StringAssert.Contains(output, "#endif");
    }

    #endregion

    #region AppendConditionalsInOrder

    [TestMethod]
    public void InOrder_AllItems_NoConditional()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "using System;",
            "using System.Collections.Generic;",
        ];

        ConditionalCodeSpecification.AppendConditionalsInOrder(
            gen,
            specs,
            (g, append, i) =>
            {
                append(g);
                g.AppendLine();
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "using System;");
        StringAssert.Contains(output, "using System.Collections.Generic;");
        Assert.DoesNotContain("#if", output);
    }

    [TestMethod]
    public void InOrder_MixedConditions_EmitsPreprocessorDirectives()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "using System;",
            new ConditionalCodeSpecification("using System.Buffers;", FrameworkType.Net80OrGreater),
            new ConditionalCodeSpecification("using System.Buffers.Binary;", FrameworkType.Net80OrGreater),
        ];

        ConditionalCodeSpecification.AppendConditionalsInOrder(
            gen,
            specs,
            (g, append, i) =>
            {
                append(g);
                g.AppendLine();
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "using System;");
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "using System.Buffers;");
        StringAssert.Contains(output, "using System.Buffers.Binary;");
    }

    [TestMethod]
    public void InOrder_NotEmittedItems_AreSkipped()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification[] specs =
        [
            "using System;",
            ConditionalCodeSpecification.DoNotEmit,
            "using System.Text;",
        ];

        ConditionalCodeSpecification.AppendConditionalsInOrder(
            gen,
            specs,
            (g, append, i) =>
            {
                append(g);
                g.AppendLine();
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "using System;");
        StringAssert.Contains(output, "using System.Text;");
    }

    [TestMethod]
    public void InOrder_ConditionChange_ClosesAndOpensBlocks()
    {
        CG gen = CreateGenerator();

        // AppendSeparatorLine requires the generator to have content first
        // (it checks EndsWith on the StringBuilder). Write an initial "All" item.
        ConditionalCodeSpecification[] specs =
        [
            "using System;",
            new ConditionalCodeSpecification("using Legacy;", FrameworkType.PreNet80),
            new ConditionalCodeSpecification("using Modern;", FrameworkType.Net80OrGreater),
        ];

        ConditionalCodeSpecification.AppendConditionalsInOrder(
            gen,
            specs,
            (g, append, i) =>
            {
                append(g);
                g.AppendLine();
            });

        string output = gen.ToString();
        StringAssert.Contains(output, "using System;");
        StringAssert.Contains(output, "#if !NET8_0_OR_GREATER");
        StringAssert.Contains(output, "using Legacy;");
        StringAssert.Contains(output, "#endif");
        StringAssert.Contains(output, "#if NET8_0_OR_GREATER");
        StringAssert.Contains(output, "using Modern;");
    }

    #endregion
}
