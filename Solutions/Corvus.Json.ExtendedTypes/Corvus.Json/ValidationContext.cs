// <copyright file="ValidationContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.InteropServices;
using CommunityToolkit.HighPerformance;

namespace Corvus.Json;

/// <summary>
/// The current validation context.
/// </summary>
[DebuggerDisplay("IsValid = {IsValid}")]
[StructLayout(LayoutKind.Sequential)]
public readonly struct ValidationContext
{
    /// <summary>
    /// Gets a valid context.
    /// </summary>
    public static readonly ValidationContext ValidContext = new(0, 0, null, [], [], UsingFeatures.IsValid);

    /// <summary>
    /// Gets an invalid context.
    /// </summary>
    public static readonly ValidationContext InvalidContext = new(0, 0, null, [], [], UsingFeatures.None);

    private static readonly ImmutableStack<(JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)> RootLocationStack = ImmutableStack.Create((JsonReference.RootFragment, JsonReference.RootFragment, JsonReference.RootFragment));

    private readonly ulong evaluatedItems;
    private readonly ulong evaluatedProperties;
    private readonly EvaluatedExtensions? evaluatedExtensions;
    private readonly ImmutableStack<(JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)> locationStack;
    private readonly UsingFeatures usingFeatures;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationContext"/> struct.
    /// </summary>
    /// <param name="evaluatedItems">The set of locally evaluated item indices.</param>
    /// <param name="evaluatedProperties">The hash set of locally evaluated properties in this location.</param>
    /// <param name="evaluatedExtensions">Extensions if we have > 32 properties or array items.</param>
    /// <param name="locationStack">The current location stack.</param>
    /// <param name="results">The validation results.</param>
    /// <param name="usingFeatures">Indicates which features are being used.</param>
    private ValidationContext(ulong evaluatedItems, ulong evaluatedProperties, EvaluatedExtensions? evaluatedExtensions, ImmutableStack<(JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)> locationStack, ImmutableList<ValidationResult> results, UsingFeatures usingFeatures)
    {
        this.evaluatedItems = evaluatedItems;
        this.evaluatedProperties = evaluatedProperties;
        this.evaluatedExtensions = evaluatedExtensions;
        this.locationStack = locationStack;
        this.Results = results;
        this.usingFeatures = usingFeatures;
    }

    [Flags]
    private enum UsingFeatures
    {
        None = 0b0000,
        EvaluatedProperties = 0b0001,
        EvaluatedItems = 0b0010,
        Results = 0b0100,
        Stack = 0b1000,
        IsValid = 0b10000,
    }

    /// <summary>
    /// Gets a value indicating whether the context is valid.
    /// </summary>
    public bool IsValid => (this.usingFeatures & UsingFeatures.IsValid) != 0;

    /// <summary>
    /// Gets a value indicating whether this is using results.
    /// </summary>
    public bool IsUsingResults => (this.usingFeatures & UsingFeatures.Results) != 0;

    /// <summary>
    /// Gets a value indicating whether this is using the location stack.
    /// </summary>
    public bool IsUsingStack => (this.usingFeatures & UsingFeatures.Stack) != 0;

    /// <summary>
    /// Gets a value indicating whether this is using evaluated properties.
    /// </summary>
    public bool IsUsingEvaluatedProperties => (this.usingFeatures & UsingFeatures.EvaluatedProperties) != 0;

    /// <summary>
    /// Gets a value indicating whether this is using evaluated items.
    /// </summary>
    public bool IsUsingEvaluatedItems => (this.usingFeatures & UsingFeatures.EvaluatedItems) != 0;

    /// <summary>
    /// Gets the validation results.
    /// </summary>
    public ImmutableList<ValidationResult> Results { get; }

    private uint LocalEvaluatedItemIndex => (uint)this.evaluatedItems;

    private uint LocalEvaluatedProperties => (uint)this.evaluatedProperties;

    private uint AppliedEvaluatedItemIndex => (uint)(this.evaluatedItems >> 32);

    private uint AppliedEvaluatedProperties => (uint)(this.evaluatedProperties >> 32);

    /// <summary>
    /// Use the results set.
    /// </summary>
    /// <returns>The validation context enabled with the keyword stack.</returns>
    public ValidationContext UsingResults()
    {
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results, this.usingFeatures | UsingFeatures.Results);
    }

    /// <summary>
    /// Use the keyword stack.
    /// </summary>
    /// <returns>The validation context enabled with the keyword stack.</returns>
    /// <remarks>If you enable the keyword stack, this automatically enables results.</remarks>
    public ValidationContext UsingStack()
    {
        bool usingStack = (this.usingFeatures & UsingFeatures.Stack) != 0;
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, usingStack ? this.locationStack : RootLocationStack, this.Results, this.usingFeatures | UsingFeatures.Stack);
    }

    /// <summary>
    /// Force using the keyword stack.
    /// </summary>
    /// <returns>The validation context enabled with the keyword stack.</returns>
    /// <remarks>This will reset any existing stack using the root location stack.</remarks>
    public ValidationContext ForceUsingStack()
    {
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, RootLocationStack, this.Results, this.usingFeatures | UsingFeatures.Stack);
    }

    /// <summary>
    /// Use the evaluated properties set.
    /// </summary>
    /// <returns>The validation context enabled with evaluated properties.</returns>
    public ValidationContext UsingEvaluatedProperties()
    {
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results, this.usingFeatures | UsingFeatures.EvaluatedProperties);
    }

    /// <summary>
    /// Use the evaluated properties set.
    /// </summary>
    /// <returns>The validation context enabled with evaluated properties.</returns>
    public ValidationContext UsingEvaluatedItems()
    {
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results, this.usingFeatures | UsingFeatures.EvaluatedItems);
    }

    /// <summary>
    /// Determines if a property has been locally evaluated.
    /// </summary>
    /// <param name="propertyIndex">The index of the property.</param>
    /// <returns><c>True</c> if the property has been evaluated locally.</returns>
    public bool HasEvaluatedLocalProperty(int propertyIndex)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedProperties) == 0)
        {
            return false;
        }

        int offset = Math.DivRem(propertyIndex, 32, out int bit);
        return
            this.evaluatedExtensions is EvaluatedExtensions extensions ?
                offset < extensions.LocalEvaluatedProperties.Length && ((extensions.LocalEvaluatedProperties[offset] & (1U << bit)) != 0) :
                offset == 0 && ((this.LocalEvaluatedProperties & (1U << bit)) != 0);
    }

    /// <summary>
    /// Determines if an item has been locally evaluated.
    /// </summary>
    /// <param name="itemIndex">The index of the item.</param>
    /// <returns><c>True</c> if the item has been evaluated locally.</returns>
    public bool HasEvaluatedLocalItemIndex(int itemIndex)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedItems) == 0)
        {
            return false;
        }

        int offset = Math.DivRem(itemIndex, 32, out int bit);
        return
            this.evaluatedExtensions is EvaluatedExtensions extensions ?
                offset < extensions.LocalEvaluatedItemIndex.Length && ((extensions.LocalEvaluatedItemIndex[offset] & (1U << bit)) != 0) :
                offset == 0 && ((this.LocalEvaluatedItemIndex & (1U << bit)) != 0);
    }

    /// <summary>
    /// Determines if a property has been evaluated locally or by applied schema.
    /// </summary>
    /// <param name="propertyIndex">The index of the property.</param>
    /// <returns><c>True</c> if the property has been evaluated either locally or by applied schema.</returns>
    public bool HasEvaluatedLocalOrAppliedProperty(int propertyIndex)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedProperties) == 0)
        {
            return false;
        }

        int offset = Math.DivRem(propertyIndex, 32, out int bit);
        uint bitPattern = 1U << bit;

        if (this.evaluatedExtensions is EvaluatedExtensions extensions)
        {
            if (offset < extensions.LocalEvaluatedProperties.Length && ((extensions.LocalEvaluatedProperties[offset] & bitPattern) != 0))
            {
                return true;
            }

            if (offset < extensions.AppliedEvaluatedProperties.Length && ((extensions.AppliedEvaluatedProperties[offset] & bitPattern) != 0))
            {
                return true;
            }
        }
        else if (offset == 0)
        {
            if ((this.LocalEvaluatedProperties & bitPattern) != 0)
            {
                return true;
            }

            if ((this.AppliedEvaluatedProperties & bitPattern) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines if an item has been evaluated locally or by applied schema.
    /// </summary>
    /// <param name="itemIndex">The index of the item.</param>
    /// <returns><c>True</c> if an item has been evaluated either locally or by applied schema.</returns>
    public bool HasEvaluatedLocalOrAppliedItemIndex(int itemIndex)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedItems) == 0)
        {
            return false;
        }

        int offset = Math.DivRem(itemIndex, 32, out int bit);
        uint bitPattern = 1U << bit;

        if (this.evaluatedExtensions is EvaluatedExtensions extensions)
        {
            if (offset < extensions.LocalEvaluatedItemIndex.Length && ((extensions.LocalEvaluatedItemIndex[offset] & bitPattern) != 0))
            {
                return true;
            }

            if (offset < extensions.AppliedEvaluatedItemIndex.Length && ((extensions.AppliedEvaluatedItemIndex[offset] & bitPattern) != 0))
            {
                return true;
            }
        }
        else if (offset == 0)
        {
            if ((this.LocalEvaluatedItemIndex & bitPattern) != 0)
            {
                return true;
            }

            if ((this.AppliedEvaluatedItemIndex & bitPattern) != 0)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Adds a result to the validation context.
    /// </summary>
    /// <param name="isValid">Whether the result is valid.</param>
    /// <param name="message">The validation message.</param>
    /// <param name="keyword">The keyword that produced the result.</param>
    /// <returns>The updated validation context.</returns>
    public ValidationContext WithResult(bool isValid, string? message = null, string? keyword = null)
    {
        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results, isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results.Add(new ValidationResult(isValid, message ?? string.Empty, null)), isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        if (keyword is string k)
        {
            (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation) location = this.locationStack.Peek();
            JsonReference reference = new(k);
            (JsonReference, JsonReference, JsonReference DocumentLocation) newLocation = (location.ValidationLocation.AppendUnencodedPropertyNameToFragment(reference), location.SchemaLocation.AppendUnencodedPropertyNameToFragment(reference), location.DocumentLocation);
            return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results.Add(new ValidationResult(isValid, message ?? string.Empty, newLocation)), isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results.Add(new ValidationResult(isValid, message ?? string.Empty, this.locationStack.Peek())), isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Adds a result to the validation context.
    /// </summary>
    /// <param name="isValid">Whether the result is valid.</param>
    /// <param name="validationLocationReducedPathModifier">The validation location reduced path modifier.</param>
    /// <param name="message">The validation message.</param>
    /// <returns>The updated validation context.</returns>
    public ValidationContext WithResult(bool isValid, JsonReference validationLocationReducedPathModifier, string? message = null)
    {
        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results, isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results.Add(new ValidationResult(isValid, message ?? string.Empty, null)), isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        (JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation) location = this.locationStack.Peek();
        (JsonReference, JsonReference, JsonReference DocumentLocation) newLocation = (location.ValidationLocation.AppendFragment(validationLocationReducedPathModifier), location.SchemaLocation.AppendFragment(validationLocationReducedPathModifier), location.DocumentLocation);
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack, this.Results.Add(new ValidationResult(isValid, message ?? string.Empty, newLocation)), isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Adds an item index to the evaluated items array.
    /// </summary>
    /// <param name="index">The index to add.</param>
    /// <returns>The updated validation context.</returns>
    public ValidationContext WithLocalItemIndex(int index)
    {
        (uint item, EvaluatedExtensions? extensions) = this.AddLocalEvaluatedItem(index);
        return new ValidationContext(item | (this.evaluatedItems & 0xFFFFFFFF00000000), this.evaluatedProperties, extensions, this.locationStack, this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Adds a property name to the evaluated properties array.
    /// </summary>
    /// <param name="propertyIndex">The property index to add.</param>
    /// <returns>The updated validation context.</returns>
    public ValidationContext WithLocalProperty(int propertyIndex)
    {
        (uint item, EvaluatedExtensions? extensions) = this.AddLocalEvaluatedProperty(propertyIndex);
        return new ValidationContext(this.evaluatedItems, item | (this.evaluatedProperties & 0xFFFFFFFF00000000), extensions, this.locationStack, this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Merges the local and applied evaluated entities from a child context into the applied evaluated entities in a parent context.
    /// </summary>
    /// <param name="childContext">The evaluated child context.</param>
    /// <param name="includeResults">Also merge the results into the parent.</param>
    /// <returns>The updated validation context.</returns>
    public ValidationContext MergeChildContext(in ValidationContext childContext, bool includeResults)
    {
        bool makeInvalid = includeResults && !childContext.IsValid;
        (uint combinedItems, uint combinedProperties, EvaluatedExtensions? extensions) = this.CombineItemsAndProperties(childContext);
        return new ValidationContext((this.evaluatedItems & 0xFFFFFFFF) | ((ulong)combinedItems << 32), (this.evaluatedProperties & 0xFFFFFFFF) | ((ulong)combinedProperties << 32), extensions, this.locationStack, includeResults && (this.usingFeatures & UsingFeatures.Results) != 0 && (childContext.usingFeatures & UsingFeatures.Results) != 0 ? this.Results.AddRange(childContext.Results) : this.Results, !makeInvalid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Pushes a location onto the location stack for the context.
    /// </summary>
    /// <param name="schemaLocation">The location in the schema to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushSchemaLocation(string schemaLocation)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, _, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation, new JsonReference(schemaLocation), documentLocation)), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes a location onto the location stack for the context.
    /// </summary>
    /// <param name="propertiesMapName">The name of the properties map containing the property name to be validated, in the schema.</param>
    /// <param name="propertyName">The property name to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushDocumentProperty(string propertiesMapName, string propertyName)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        // We push both the document property, and the fact that we are validating a "properties" value.
        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        int length = propertiesMapName.Length + propertyName.Length + 1;
        Span<char> buffer = stackalloc char[length];
        propertiesMapName.AsSpan().CopyTo(buffer);
        buffer[propertiesMapName.Length] = '/';
        propertyName.AsSpan().CopyTo(buffer[(propertiesMapName.Length + 1)..]);

        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation.AppendUnencodedPropertyNameToFragment(buffer[..length]), schemaLocation.AppendUnencodedPropertyNameToFragment(buffer[..length]), documentLocation.AppendUnencodedPropertyNameToFragment(propertyName))), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes a location onto the location stack for the context.
    /// </summary>
    /// <param name="arrayIndex">The array index to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushDocumentArrayIndex(int arrayIndex)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation, schemaLocation, documentLocation.AppendArrayIndexToFragment(arrayIndex))), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes a location onto the location stack for the context.
    /// </summary>
    /// <param name="propertyName">The property name to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushValidationLocationProperty(string propertyName)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation.AppendUnencodedPropertyNameToFragment(propertyName), schemaLocation.AppendUnencodedPropertyNameToFragment(propertyName), documentLocation)), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes the reduced path modifier onto the location stack for the context.
    /// </summary>
    /// <param name="reducedPathModifier">The reduced path modifier to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushValidationLocationReducedPathModifier(JsonReference reducedPathModifier)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation.AppendFragment(reducedPathModifier), schemaLocation.AppendFragment(reducedPathModifier), documentLocation)), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Replaces the reduced path modifier on the location stack for the context.
    /// </summary>
    /// <param name="reducedPathModifier">The reduced path modifier to push.</param>
    /// <returns>Pops the current location, then updates the context with the given location.</returns>
    public ValidationContext ReplaceValidationLocationReducedPathModifier(JsonReference reducedPathModifier)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        ImmutableStack<(JsonReference ValidationLocation, JsonReference SchemaLocation, JsonReference DocumentLocation)> stack = this.locationStack.Pop();
        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, stack.Push((validationLocation.AppendFragment(reducedPathModifier), schemaLocation.AppendFragment(reducedPathModifier), documentLocation)), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes the reduced path modifier and property name onto the location and document stacks for the context.
    /// </summary>
    /// <param name="reducedPathModifier">The reduced path modifier to push.</param>
    /// <param name="propertyName">The property name to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushValidationLocationReducedPathModifierAndProperty(JsonReference reducedPathModifier, string propertyName)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation.AppendFragment(reducedPathModifier), schemaLocation.AppendFragment(reducedPathModifier), documentLocation.AppendUnencodedPropertyNameToFragment(propertyName))), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pushes a location onto the location stack for the context.
    /// </summary>
    /// <param name="arrayIndex">The array index to push.</param>
    /// <returns>The context updated with the given location.</returns>
    public ValidationContext PushValidationLocationArrayIndex(int arrayIndex)
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        (JsonReference validationLocation, JsonReference schemaLocation, JsonReference documentLocation) = this.locationStack.Peek();
        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Push((validationLocation.AppendArrayIndexToFragment(arrayIndex), schemaLocation.AppendArrayIndexToFragment(arrayIndex), documentLocation)), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Pops a location off the location stack.
    /// </summary>
    /// <returns>The updated context.</returns>
    public ValidationContext PopLocation()
    {
        if ((this.usingFeatures & UsingFeatures.Stack) == 0)
        {
            return this;
        }

        return new ValidationContext(this.evaluatedItems, this.evaluatedProperties, this.evaluatedExtensions, this.locationStack.Pop(), this.Results, this.usingFeatures);
    }

    /// <summary>
    /// Creates a child context from the current location.
    /// </summary>
    /// <returns>A new (valid) validation context with no evaluated items or properties, at the current location.</returns>
    public ValidationContext CreateChildContext()
    {
        return new ValidationContext(0, 0, null, this.locationStack, ImmutableList<ValidationResult>.Empty, this.usingFeatures | UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        ImmutableList<ValidationResult> results = this.Results;
        if (results.IsEmpty)
        {
            results = result1.Results;
        }
        else if (!result1.Results.IsEmpty)
        {
            var builder = this.Results.ToBuilder();

            builder.AddRange(result1.Results);
            results = builder.ToImmutable();
        }

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            results,
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);

        builder.AddRange(result2.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);

        builder.AddRange(result2.Results);

        builder.AddRange(result3.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3, in ValidationContext result4)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);

        builder.AddRange(result2.Results);

        builder.AddRange(result3.Results);

        builder.AddRange(result4.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    /// <param name="result5">The fifth result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3, in ValidationContext result4, in ValidationContext result5)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);
        builder.AddRange(result2.Results);
        builder.AddRange(result3.Results);
        builder.AddRange(result4.Results);
        builder.AddRange(result5.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    /// <param name="result5">The fifth result.</param>
    /// <param name="result6">The sixth result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3, in ValidationContext result4, in ValidationContext result5, in ValidationContext result6)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);
        builder.AddRange(result2.Results);
        builder.AddRange(result3.Results);
        builder.AddRange(result4.Results);
        builder.AddRange(result5.Results);
        builder.AddRange(result6.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    /// <param name="result5">The fifth result.</param>
    /// <param name="result6">The sixth result.</param>
    /// <param name="result7">The seventh result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3, in ValidationContext result4, in ValidationContext result5, in ValidationContext result6, in ValidationContext result7)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);
        builder.AddRange(result2.Results);
        builder.AddRange(result3.Results);
        builder.AddRange(result4.Results);
        builder.AddRange(result5.Results);
        builder.AddRange(result6.Results);
        builder.AddRange(result7.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="result1">The first result.</param>
    /// <param name="result2">The second result.</param>
    /// <param name="result3">The third result.</param>
    /// <param name="result4">The fourth result.</param>
    /// <param name="result5">The fifth result.</param>
    /// <param name="result6">The sixth result.</param>
    /// <param name="result7">The seventh result.</param>
    /// <param name="result8">The eighth result.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, in ValidationContext result1, in ValidationContext result2, in ValidationContext result3, in ValidationContext result4, in ValidationContext result5, in ValidationContext result6, in ValidationContext result7, in ValidationContext result8)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        builder.AddRange(result1.Results);
        builder.AddRange(result2.Results);
        builder.AddRange(result3.Results);
        builder.AddRange(result4.Results);
        builder.AddRange(result5.Results);
        builder.AddRange(result6.Results);
        builder.AddRange(result7.Results);
        builder.AddRange(result8.Results);

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    /// Merge the messages for a given set of results into this result,
    /// applying the given validity at the end, regardless of the individual
    /// validity of the results in the set.
    /// </summary>
    /// <param name="isValid">The ultimate validity of this context.</param>
    /// <param name="level">The current validation level for the context.</param>
    /// <param name="results">The array of results to merege.</param>
    /// <returns>The updated validation context.</returns>
    /// <remarks>
    /// <para>
    /// Unlike <see cref="MergeChildContext(in ValidationContext, bool)"/>, which merges the elements
    /// that were visited, this simply takes the contextual messages from the children, adds them to this context,
    /// and sets the validity as per the <paramref name="isValid"/> parameter.
    /// </para>
    /// <para>
    /// This is typically used when one of a number of validations may be valid, and the ultimate result is some
    /// function of those child validations, but you wish to capture the details about the validation.
    /// </para>
    /// </remarks>
    public ValidationContext MergeResults(bool isValid, ValidationLevel level, params ValidationContext[] results)
    {
        if (level == ValidationLevel.Flag && isValid)
        {
            return this;
        }

        if ((this.usingFeatures & UsingFeatures.Results) == 0)
        {
            return new ValidationContext(
                this.evaluatedItems,
                this.evaluatedProperties,
                this.evaluatedExtensions,
                this.locationStack,
                this.Results,
                isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
        }

        var builder = this.Results.ToBuilder();

        foreach (ValidationContext result in results)
        {
            if (result.Results is ImmutableList<ValidationResult> resultResults)
            {
                builder.AddRange(resultResults);
            }
        }

        return new ValidationContext(
            this.evaluatedItems,
            this.evaluatedProperties,
            this.evaluatedExtensions,
            this.locationStack,
            builder.ToImmutable(),
            isValid ? this.usingFeatures : this.usingFeatures & ~UsingFeatures.IsValid);
    }

    /// <summary>
    ///  Merges the bitfields representing the items we have seen in the array.
    /// </summary>
    private static void ApplyBits(ImmutableArray<uint>.Builder result, in ImmutableArray<uint> items)
    {
        for (int i = 0; i < items.Length; ++i)
        {
            if (i < result.Count)
            {
                result[i] |= items.ItemRef(i);
            }
            else
            {
                result.Add(items[i]);
            }
        }
    }

    private (uint LocalEvaluatedItemIndex, EvaluatedExtensions? Extensions) AddLocalEvaluatedProperty(int index)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedProperties) != 0)
        {
            // Calculate the offset into the array
            int offset = Math.DivRem(index, 32, out int bitOffset);
            uint bit = 1U << bitOffset;

            if (this.evaluatedExtensions is EvaluatedExtensions extensions)
            {
                ImmutableArray<uint> lep = this.evaluatedExtensions.LocalEvaluatedProperties;

                if (offset >= lep.Length)
                {
                    lep = lep.AddRange(Enumerable.Repeat(0U, offset - lep.Length + 1));
                }

                return (0, new EvaluatedExtensions(extensions.LocalEvaluatedItemIndex, lep.SetItem(offset, lep.ItemRef(offset) | bit), extensions.AppliedEvaluatedItemIndex, extensions.AppliedEvaluatedProperties));
            }

            if (offset == 0)
            {
                return (this.LocalEvaluatedProperties | bit, null);
            }

            ImmutableArray<uint>.Builder builder = ImmutableArray.CreateBuilder<uint>(offset + 1);
            builder.Add(this.LocalEvaluatedProperties);
            for (int i = 1; i < offset; ++i)
            {
                builder.Add(0);
            }

            builder.Add(bit);
            return (0, new EvaluatedExtensions([this.LocalEvaluatedItemIndex], builder.ToImmutable(), [this.AppliedEvaluatedItemIndex], [this.AppliedEvaluatedProperties]));
        }

        return (0, null);
    }

    private (uint LocalEvaluatedItemIndex, EvaluatedExtensions? Extensions) AddLocalEvaluatedItem(int index)
    {
        if ((this.usingFeatures & UsingFeatures.EvaluatedItems) != 0)
        {
            // Calculate the offset into the array
            int offset = Math.DivRem(index, 32, out int bitOffset);
            uint bit = 1U << bitOffset;
            if (this.evaluatedExtensions is EvaluatedExtensions extensions)
            {
                ImmutableArray<uint> lei = this.evaluatedExtensions.LocalEvaluatedItemIndex;

                if (offset >= lei.Length)
                {
                    lei = lei.AddRange(Enumerable.Repeat(0U, offset - lei.Length + 1));
                }

                return (0, new EvaluatedExtensions(lei.SetItem(offset, lei.ItemRef(offset) | bit), extensions.LocalEvaluatedProperties, extensions.AppliedEvaluatedItemIndex, extensions.AppliedEvaluatedProperties));
            }

            if (offset == 0)
            {
                return (this.LocalEvaluatedItemIndex | bit, null);
            }

            ImmutableArray<uint>.Builder builder = ImmutableArray.CreateBuilder<uint>(offset + 1);
            builder.Add(this.LocalEvaluatedItemIndex);
            for (int i = 1; i < offset; ++i)
            {
                builder.Add(0);
            }

            builder.Add(bit);
            return (0, new EvaluatedExtensions(builder.ToImmutable(), [this.LocalEvaluatedProperties], [this.AppliedEvaluatedItemIndex], [this.AppliedEvaluatedProperties]));
        }

        return (0, null);
    }

    private (uint CombinedItems, uint CombinedProperties, EvaluatedExtensions? Extensions) CombineItemsAndProperties(in ValidationContext childContext)
    {
        if ((this.usingFeatures & (UsingFeatures.EvaluatedItems | UsingFeatures.EvaluatedProperties)) == 0)
        {
            return (0, 0, null);
        }

        if (this.evaluatedExtensions is null)
        {
            if (childContext.evaluatedExtensions is null)
            {
                return (this.AppliedEvaluatedItemIndex | childContext.AppliedEvaluatedItemIndex | childContext.LocalEvaluatedItemIndex, this.AppliedEvaluatedProperties | childContext.AppliedEvaluatedProperties | childContext.LocalEvaluatedProperties, null);
            }
            else
            {
                ImmutableArray<uint>.Builder result1 = ImmutableArray.CreateBuilder<uint>(Math.Max(childContext.evaluatedExtensions!.AppliedEvaluatedItemIndex.Length, childContext.evaluatedExtensions!.LocalEvaluatedItemIndex.Length));
                result1.Add(this.AppliedEvaluatedItemIndex);
                ImmutableArray<uint>.Builder result2 = ImmutableArray.CreateBuilder<uint>(Math.Max(childContext.evaluatedExtensions!.AppliedEvaluatedProperties.Length, childContext.evaluatedExtensions!.LocalEvaluatedProperties.Length));
                result2.Add(this.AppliedEvaluatedProperties);

                ApplyBits(result1, childContext.evaluatedExtensions!.AppliedEvaluatedItemIndex);
                ApplyBits(result1, childContext.evaluatedExtensions!.LocalEvaluatedItemIndex);
                ApplyBits(result2, childContext.evaluatedExtensions!.AppliedEvaluatedProperties);
                ApplyBits(result2, childContext.evaluatedExtensions!.LocalEvaluatedProperties);

                return (0, 0, new EvaluatedExtensions([this.LocalEvaluatedItemIndex], [this.LocalEvaluatedProperties], result1.ToImmutable(), result2.ToImmutable()));
            }
        }

        if (childContext.evaluatedExtensions is null)
        {
            var result1 = this.evaluatedExtensions!.AppliedEvaluatedItemIndex.ToBuilder();
            var result2 = this.evaluatedExtensions!.AppliedEvaluatedProperties.ToBuilder();
            result1[0] |= childContext.AppliedEvaluatedItemIndex | childContext.LocalEvaluatedItemIndex;
            result2[0] |= childContext.AppliedEvaluatedProperties | childContext.LocalEvaluatedProperties;

            return (0, 0, new EvaluatedExtensions(this.evaluatedExtensions!.LocalEvaluatedItemIndex, this.evaluatedExtensions!.LocalEvaluatedProperties, result1.ToImmutable(), result2.ToImmutable()));
        }
        else
        {
            // Extensions in both parent and child
            var result1 = this.evaluatedExtensions!.AppliedEvaluatedItemIndex.ToBuilder();
            var result2 = this.evaluatedExtensions!.AppliedEvaluatedProperties.ToBuilder();
            ApplyBits(result1, childContext.evaluatedExtensions!.AppliedEvaluatedItemIndex);
            ApplyBits(result1, childContext.evaluatedExtensions!.LocalEvaluatedItemIndex);
            ApplyBits(result2, childContext.evaluatedExtensions!.AppliedEvaluatedProperties);
            ApplyBits(result2, childContext.evaluatedExtensions!.LocalEvaluatedProperties);

            return (0, 0, new EvaluatedExtensions(this.evaluatedExtensions!.LocalEvaluatedItemIndex, this.evaluatedExtensions!.LocalEvaluatedProperties, result1.ToImmutable(), result2.ToImmutable()));
        }
    }

    private class EvaluatedExtensions(ImmutableArray<uint> localEvaluatedItemIndex, ImmutableArray<uint> localEvaluatedProperties, ImmutableArray<uint> appliedEvaluatedItemIndex, ImmutableArray<uint> appliedEvaluatedProperties)
    {
        public ImmutableArray<uint> LocalEvaluatedItemIndex { get; } = localEvaluatedItemIndex;

        public ImmutableArray<uint> LocalEvaluatedProperties { get; } = localEvaluatedProperties;

        public ImmutableArray<uint> AppliedEvaluatedItemIndex { get; } = appliedEvaluatedItemIndex;

        public ImmutableArray<uint> AppliedEvaluatedProperties { get; } = appliedEvaluatedProperties;
    }
}