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
public static class ConditionalCodeSpecificationTests
{
    private static CG CreateGenerator()
    {
        return new CG(CSharpLanguageProvider.Default, CancellationToken.None);
    }

    #region AppendConditional

    [Fact]
    public static void AppendConditional_NotEmitted_EmitsNothing()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.Append("should not appear"),
            FrameworkType.NotEmitted);

        Assert.Equal(string.Empty, gen.ToString());
    }

    [Fact]
    public static void AppendConditional_All_EmitsWithoutPreprocessor()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.Append("code here"),
            FrameworkType.All);

        string output = gen.ToString();
        Assert.Contains("code here", output);
        Assert.DoesNotContain("#if", output);
    }

    [Fact]
    public static void AppendConditional_Net80OrGreater_WrapsInPreprocessor()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("net80+ code"),
            FrameworkType.Net80OrGreater);

        string output = gen.ToString();
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("net80+ code", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void AppendConditional_PreNet80_WrapsWithNotNet80()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("pre-net80 code"),
            FrameworkType.PreNet80);

        string output = gen.ToString();
        Assert.Contains("#if !NET8_0_OR_GREATER", output);
        Assert.Contains("pre-net80 code", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void AppendConditional_Net80_WrapsWithNet80()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditional(
            gen,
            g => g.AppendLine("net80 specific"),
            FrameworkType.Net80);

        string output = gen.ToString();
        Assert.Contains("#if NET8_0", output);
        Assert.Contains("net80 specific", output);
        Assert.Contains("#endif", output);
    }

    #endregion

    #region Constructor and Append

    [Fact]
    public static void ActionConstructor_InvokesFunction()
    {
        var spec = new ConditionalCodeSpecification(
            g => g.Append("action content"),
            FrameworkType.All);

        CG gen = CreateGenerator();
        spec.Append(gen);

        Assert.Contains("action content", gen.ToString());
    }

    [Fact]
    public static void StringConstructor_AppendsString()
    {
        var spec = new ConditionalCodeSpecification("IDisposable", FrameworkType.Net80OrGreater);

        CG gen = CreateGenerator();
        spec.Append(gen);

        Assert.Contains("IDisposable", gen.ToString());
    }

    [Fact]
    public static void ImplicitStringConversion_CreatesAllCondition()
    {
        ConditionalCodeSpecification spec = "IEnumerable";

        CG gen = CreateGenerator();
        spec.Append(gen);

        Assert.Contains("IEnumerable", gen.ToString());
    }

    [Fact]
    public static void DoNotEmit_AppendsNothing()
    {
        ConditionalCodeSpecification spec = ConditionalCodeSpecification.DoNotEmit;

        CG gen = CreateGenerator();
        spec.Append(gen);

        Assert.Equal(string.Empty, gen.ToString());
    }

    #endregion

    #region AppendConditionalsGroupingBlocks

    [Fact]
    public static void GroupingBlocks_EmptyArray_EmitsNothing()
    {
        CG gen = CreateGenerator();
        ConditionalCodeSpecification.AppendConditionalsGroupingBlocks(
            gen,
            [],
            (g, append, i) => append(g));

        Assert.Equal(string.Empty, gen.ToString());
    }

    [Fact]
    public static void GroupingBlocks_AllItems_NoConditional()
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
        Assert.Contains("IFoo", output);
        Assert.Contains("IBar", output);
        Assert.DoesNotContain("#if", output);
    }

    [Fact]
    public static void GroupingBlocks_Net80OrGreater_EmitsConditionalBlock()
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
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("IFoo", output);
        Assert.Contains("INet80Interface", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void GroupingBlocks_PreNet80_EmitsElseBlock()
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
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("INet80Interface", output);
        Assert.Contains("ILegacyInterface", output);
        Assert.Contains("#else", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void GroupingBlocks_Net80Only_EmitsNet80Conditional()
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
        Assert.Contains("#if NET8_0", output);
        Assert.Contains("INet80Only", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void GroupingBlocks_AllThreeGroups_EmitsFullConditional()
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
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("IModern", output);
        Assert.Contains("INet80", output);
        Assert.Contains("ILegacy", output);
        Assert.Contains("ICommon", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void GroupingBlocks_NotEmittedItems_AreSkipped()
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
        Assert.Contains("IFoo", output);
        Assert.Contains("IBar", output);
    }

    [Fact]
    public static void GroupingBlocks_PreNet80WithoutNet80OrGreater_EmitsNotNet80()
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
        Assert.Contains("#if !NET8_0_OR_GREATER", output);
        Assert.Contains("ILegacy", output);
        Assert.Contains("#endif", output);
    }

    [Fact]
    public static void GroupingBlocks_Net80OnlyWithPreNet80_EmitsElifNotNet80()
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
        Assert.Contains("#if NET8_0", output);
        Assert.Contains("INet80Only", output);
        Assert.Contains("ILegacy", output);
        Assert.Contains("#endif", output);
    }

    #endregion

    #region AppendConditionalsInOrder

    [Fact]
    public static void InOrder_AllItems_NoConditional()
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
        Assert.Contains("using System;", output);
        Assert.Contains("using System.Collections.Generic;", output);
        Assert.DoesNotContain("#if", output);
    }

    [Fact]
    public static void InOrder_MixedConditions_EmitsPreprocessorDirectives()
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
        Assert.Contains("using System;", output);
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("using System.Buffers;", output);
        Assert.Contains("using System.Buffers.Binary;", output);
    }

    [Fact]
    public static void InOrder_NotEmittedItems_AreSkipped()
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
        Assert.Contains("using System;", output);
        Assert.Contains("using System.Text;", output);
    }

    [Fact]
    public static void InOrder_ConditionChange_ClosesAndOpensBlocks()
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
        Assert.Contains("using System;", output);
        Assert.Contains("#if !NET8_0_OR_GREATER", output);
        Assert.Contains("using Legacy;", output);
        Assert.Contains("#endif", output);
        Assert.Contains("#if NET8_0_OR_GREATER", output);
        Assert.Contains("using Modern;", output);
    }

    #endregion
}
