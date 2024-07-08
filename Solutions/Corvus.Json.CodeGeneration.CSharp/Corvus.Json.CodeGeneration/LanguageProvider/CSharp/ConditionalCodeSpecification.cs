// <copyright file="ConditionalCodeSpecification.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// A type for which allows code to be conditionally emitted, either
/// by <see cref="FrameworkType"/> and/or via a do-nothing placeholder <see cref="DoNotEmit"/>.
/// </summary>
public readonly struct ConditionalCodeSpecification
{
    private readonly string? explicitString;
    private readonly FrameworkType condition;
    private readonly Action<CodeGenerator>? generationFunction;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalCodeSpecification"/> struct.
    /// </summary>
    /// <param name="generationFunction">The generation function for the interface spec.</param>
    /// <param name="condition">The condition for generation.</param>
    public ConditionalCodeSpecification(
        Action<CodeGenerator> generationFunction,
        FrameworkType condition = FrameworkType.All)
    {
        this.generationFunction = generationFunction;
        this.condition = condition;
        this.explicitString = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConditionalCodeSpecification"/> struct.
    /// </summary>
    /// <param name="explicitString">The explicit string for the interface to implement.</param>
    /// <param name="condition">The condition for generation.</param>
    public ConditionalCodeSpecification(
        string explicitString,
        FrameworkType condition = FrameworkType.All)
    {
        this.generationFunction = null;
        this.explicitString = explicitString;
        this.condition = condition;
    }

    /// <summary>
    /// Gets a value which emits no code.
    /// </summary>
    public static ConditionalCodeSpecification DoNotEmit => default;

    /// <summary>
    /// Implicit conversion of a string to a <see cref="ConditionalCodeSpecification"/> with
    /// <see cref="FrameworkType.All"/>.
    /// </summary>
    /// <param name="explicitString">The explicit string.</param>
    public static implicit operator ConditionalCodeSpecification(string explicitString)
    {
        return new ConditionalCodeSpecification(explicitString);
    }

    /// <summary>
    /// Append conditionals to the generator, preserving their ordering.
    /// </summary>
    /// <param name="generator">The generator to which to append the set of conditionals.</param>
    /// <param name="conditionalSpecifications">
    /// The conditional specifications to assemble into conditional blocks and appended.</param>
    /// <param name="appendCallback">The callback to append a single block.</param>
    /// <remarks>
    /// <para>
    /// This method expects the <paramref name="appendCallback"/> to leave the generator at the end of the
    /// line that it appends, rather than appending an additional newline, and leaving it at the start of the
    /// next line.
    /// </para>
    /// <para>
    /// The callback is passed the generator to emit, a function that will emit the conditional code itself, and
    /// an integer that indicates the index of the item in the conditional block. This allows the caller to wrap
    /// the conditional code appropriately (e.g. if it requires comma separation as in a set of interfaces or wrapping
    /// with <c>using [conditional block];</c> as in a using statement.
    /// </para>
    /// </remarks>
    public static void AppendConditionalsInOrder(
        CodeGenerator generator,
        ConditionalCodeSpecification[] conditionalSpecifications,
        Action<CodeGenerator, Action<CodeGenerator>, int> appendCallback)
    {
        FrameworkType lastFrameworkType = FrameworkType.All;
        int i = 0;

        foreach (ConditionalCodeSpecification spec in conditionalSpecifications)
        {
            if (spec.condition == FrameworkType.NotEmitted)
            {
                continue;
            }

            if (spec.condition != lastFrameworkType)
            {
                if (lastFrameworkType != FrameworkType.All)
                {
                    generator.AppendLine();
                    generator.Append("#endif");
                    i = 0;
                }

                if (spec.condition != FrameworkType.All)
                {
                    generator.AppendSeparatorLine();
                    generator.Append("#if ");
                    generator.Append(GetCondition(spec.condition));
                }
            }

            generator.AppendLine();

            appendCallback(generator, spec.Append, i++);

            lastFrameworkType = spec.condition;
        }

        if (i > 0)
        {
            generator.AppendLine();
        }

        static string GetCondition(FrameworkType condition)
        {
            return condition switch
            {
                FrameworkType.Net80OrGreater => "NET8_0_OR_GREATER",
                FrameworkType.Net80 => "NET8_0",
                FrameworkType.PreNet80 => "!NET8_0_OR_GREATER",
                _ => throw new InvalidOperationException($"Unsupported framework type: {condition}"),
            };
        }
    }

    /// <summary>
    /// Append conditionals to the generator, grouping them by <see cref="FrameworkType"/>.
    /// </summary>
    /// <param name="generator">The generator to which to append the set of conditionals.</param>
    /// <param name="conditionalSpecifications">
    /// The conditional specifications to assemble into conditional blocks and appended.</param>
    /// <param name="appendCallback">The callback to append a single block.</param>
    /// <remarks>
    /// <para>
    /// This may cause <see cref="FrameworkType.All"/> conditional code to be repeated in each more constrained block, but
    /// makes it easier to setup blocks with complex in-block formatting.
    /// </para>
    /// <para>
    /// This method expects the <paramref name="appendCallback"/> to leave the generator at the end of the
    /// line that it appends, rather than appending an additional newline, and leaving it at the start of the
    /// next line.
    /// </para>
    /// <para>
    /// The callback is passed the generator to emit, a function that will emit the conditional code itself, and
    /// an integer that indicates the index of the item in the conditional block. This allows the caller to wrap
    /// the conditional code appropriately (e.g. if it requires comma separation as in a set of interfaces or wrapping
    /// with <c>using [conditional block];</c> as in a using statement.
    /// </para>
    /// </remarks>
    public static void AppendConditionalsGroupingBlocks(
    CodeGenerator generator,
    ConditionalCodeSpecification[] conditionalSpecifications,
    Action<CodeGenerator, Action<CodeGenerator>, int> appendCallback)
    {
        if (conditionalSpecifications.Length == 0)
        {
            return;
        }

        // We can extend these as we support later versions of dotnet
        List<ConditionalCodeSpecification> all = [];
        List<ConditionalCodeSpecification> preNet80 = [];
        List<ConditionalCodeSpecification> net80 = [];
        List<ConditionalCodeSpecification> net80OrGreater = [];
        GroupConditionals(conditionalSpecifications, all, preNet80, net80, net80OrGreater);

        // Do a check for the "no actual conditionals" case so we just emit
        // the "all" case
        if (preNet80.Count == 0 &&
            net80.Count == 0 &&
            net80OrGreater.Count == 0)
        {
            // If we have no conditionals, just append the "all".
            for (int i = 0; i < all.Count; i++)
            {
                ConditionalCodeSpecification spec = all[i];
                appendCallback(generator, spec.Append, i);
            }

            generator.AppendLine();

            return;
        }

        // Now, work down the hierarcy
        if (net80OrGreater.Count != 0)
        {
            generator.AppendLine("#if NET8_0_OR_GREATER");

            // Concat "all" with this and then append it
            int i = 0;
            foreach (ConditionalCodeSpecification spec in
                    all
                        .Concat(net80)
                        .Concat(net80OrGreater))
            {
                appendCallback(generator, spec.Append, i);
                i++;
            }
        }

        if (net80OrGreater.Count == 0 && net80.Count != 0)
        {
            // We append net80 "solo" only if we have no net80OrGreater
            // so we always need to emit the conditional
            generator.AppendLine("#if NET8_0");

            // Concat "all" with this and then append it
            int i = 0;
            foreach (ConditionalCodeSpecification spec in
                    all
                        .Concat(net80))
            {
                appendCallback(generator, spec.Append, i);
                i++;
            }
        }

        if (preNet80.Count != 0)
        {
            if (net80OrGreater.Count != 0)
            {
                // Something else appended a conditional, so we need to go in an else clause
                generator.AppendLine();
                generator.AppendLine("#else");
            }
            else if (net80.Count != 0)
            {
                generator.AppendLine();
                generator.AppendLine("#elif !NET8_0_OR_GREATER");
            }
            else
            {
                // The condition is that we are lower than the lowest explicit conditional
                generator.AppendLine("#if !NET8_0_OR_GREATER");
            }

            int i = 0;
            foreach (ConditionalCodeSpecification spec in
                        all
                            .Concat(preNet80))
            {
                appendCallback(generator, spec.Append, i);
                i++;
            }
        }

        // Add the "just all" case
        if (all.Count > 0)
        {
            generator.AppendLine();
            generator.AppendLine("#else");
            int i = 0;
            foreach (ConditionalCodeSpecification spec in all)
            {
                appendCallback(generator, spec.Append, i);
                i++;
            }
        }

        generator.AppendLine();
        generator.AppendLine("#endif");
    }

    /// <summary>
    /// Append the interface to the generator.
    /// </summary>
    /// <param name="generator">The generator to which to append the interface.</param>
    public void Append(CodeGenerator generator)
    {
        if (this.generationFunction is Action<CodeGenerator> generationFunction)
        {
            generationFunction(generator);
        }

        if (this.explicitString is string name)
        {
            generator.Append(name);
        }
    }

    private static void GroupConditionals(
        ConditionalCodeSpecification[] conditionalSpecifications,
        List<ConditionalCodeSpecification> all,
        List<ConditionalCodeSpecification> preNet80,
        List<ConditionalCodeSpecification> net80,
        List<ConditionalCodeSpecification> net80OrGreater)
    {
        foreach (ConditionalCodeSpecification spec in conditionalSpecifications)
        {
            switch (spec.condition)
            {
                case FrameworkType.NotEmitted:
                    break;

                case FrameworkType.PreNet80:
                    preNet80.Add(spec);
                    break;

                case FrameworkType.Net80:
                    net80.Add(spec);
                    break;

                case FrameworkType.Net80OrGreater:
                    net80OrGreater.Add(spec);
                    break;

                case FrameworkType.All:
                    all.Add(spec);
                    break;

                default:
                    throw new NotSupportedException($"Unsupported framework type: {spec.condition}");
            }
        }
    }
}