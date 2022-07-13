// <copyright file="ValidationContext.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System.Collections.Immutable;
    using System.Linq;

    /// <summary>
    /// The current validation context.
    /// </summary>
    public readonly struct ValidationContext
    {
        [Flags]
        private enum UsingFeatures
        {
            None = 0b0000,
            EvaluatedProperties = 0b0001,
            EvaluatedItems = 0b0010,
            Results = 0b0100,
            Stack = 0b1000,
        }

        /// <summary>
        /// Gets a valid context.
        /// </summary>
        public static readonly ValidationContext ValidContext = new(true, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableStack<string>.Empty, ImmutableStack<string>.Empty, ImmutableArray<ValidationResult>.Empty, UsingFeatures.None);

        /// <summary>
        /// Gets an invalid context.
        /// </summary>
        public static readonly ValidationContext InvalidContext = new(false, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableArray<ulong>.Empty, ImmutableStack<string>.Empty, ImmutableStack<string>.Empty, ImmutableArray<ValidationResult>.Empty, UsingFeatures.None);

        private static readonly ImmutableStack<string> RootLocationStack = ImmutableStack.Create("#");
        private static readonly ImmutableStack<string> RootAbsoluteLocationStack = ImmutableStack.Create("#");

        private readonly UsingFeatures usingFeatures;
        private readonly ImmutableArray<ulong> localEvaluatedItemIndex;
        private readonly ImmutableArray<ulong> localEvaluatedProperties;
        private readonly ImmutableArray<ulong> appliedEvaluatedItemIndex;
        private readonly ImmutableArray<ulong> appliedEvaluatedProperties;
        private readonly ImmutableStack<string> absoluteKeywordLocationStack;
        private readonly ImmutableStack<string> locationStack;
        private readonly ImmutableArray<ValidationResult> results;

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationContext"/> struct.
        /// </summary>
        /// <param name="isValid">Whether this context is valid.</param>
        /// <param name="localEvaluatedItemIndex">The set of locally evaluated item indices.</param>
        /// <param name="localEvaluatedProperties">The hash set of locally evaluated properties in this location.</param>
        /// <param name="appliedEvaluatedItemIndex">The maximum evaluated item index from applied schema.</param>
        /// <param name="appliedEvaluatedProperties">The hash set of evaluated properties from applied schema.</param>
        /// <param name="locationStack">The current location stack.</param>
        /// <param name="absoluteKeywordLocationStack">The current absolute keyword location stack.</param>
        /// <param name="results">The validation results.</param>
        /// <param name="usingFeatures">Indicates which features are being used.</param>
        private ValidationContext(bool isValid, in ImmutableArray<ulong> localEvaluatedItemIndex, in ImmutableArray<ulong> localEvaluatedProperties, in ImmutableArray<ulong> appliedEvaluatedItemIndex, in ImmutableArray<ulong> appliedEvaluatedProperties, in ImmutableStack<string> locationStack, in ImmutableStack<string> absoluteKeywordLocationStack, in ImmutableArray<ValidationResult> results, UsingFeatures usingFeatures)
        {
            this.localEvaluatedItemIndex = localEvaluatedItemIndex;
            this.localEvaluatedProperties = localEvaluatedProperties;
            this.appliedEvaluatedItemIndex = appliedEvaluatedItemIndex;
            this.appliedEvaluatedProperties = appliedEvaluatedProperties;
            this.locationStack = locationStack;
            this.absoluteKeywordLocationStack = absoluteKeywordLocationStack;
            this.IsValid = isValid;
            this.results = results;
            this.usingFeatures = usingFeatures;
        }

        /// <summary>
        /// Gets a value indicating whether the context is valid.
        /// </summary>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the validation results.
        /// </summary>
        public ImmutableArray<ValidationResult> Results => this.results;

        /// <summary>
        /// Use the results set.
        /// </summary>
        /// <returns>The validation context enabled with the keyword stack.</returns>
        public ValidationContext UsingResults()
        {
            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures | UsingFeatures.Results);
        }

        /// <summary>
        /// Use the keyword stack.
        /// </summary>
        /// <returns>The validation context enabled with the keyword stack.</returns>
        /// <remarks>If you enable the keyword stack, this automatically enables results.</remarks>
        public ValidationContext UsingStack()
        {
            bool usingStack = (this.usingFeatures & UsingFeatures.Stack) != 0;
            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, usingStack ? this.locationStack : RootLocationStack, usingStack ? this.absoluteKeywordLocationStack : RootAbsoluteLocationStack, this.results, this.usingFeatures | UsingFeatures.Stack);
        }

        /// <summary>
        /// Use the evaluated properties set.
        /// </summary>
        /// <returns>The validation context enabled with evaluated properties.</returns>
        public ValidationContext UsingEvaluatedProperties()
        {
            bool usingEvaluatedProperties = (this.usingFeatures & UsingFeatures.EvaluatedProperties) != 0;
            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, usingEvaluatedProperties ? this.localEvaluatedProperties : ImmutableArray.Create<ulong>(0), this.appliedEvaluatedItemIndex, usingEvaluatedProperties ? this.appliedEvaluatedProperties : ImmutableArray.Create<ulong>(0), this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures | UsingFeatures.EvaluatedProperties);
        }

        /// <summary>
        /// Use the evaluated properties set.
        /// </summary>
        /// <returns>The validation context enabled with evaluated properties.</returns>
        public ValidationContext UsingEvaluatedItems()
        {
            bool usingEvaluatedItems = (this.usingFeatures & UsingFeatures.EvaluatedItems) != 0;

            return new ValidationContext(this.IsValid, usingEvaluatedItems ? this.localEvaluatedItemIndex : ImmutableArray.Create<ulong>(0), this.localEvaluatedProperties, usingEvaluatedItems ? this.appliedEvaluatedItemIndex : ImmutableArray.Create<ulong>(0), this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures | UsingFeatures.EvaluatedItems);
        }

        /// <summary>
        /// Determines if a property has been locally evaluated.
        /// </summary>
        /// <param name="propertyIndex">The index of the property.</param>
        /// <returns><c>True</c> if the property has been evaluated locally.</returns>
        public bool HasEvaluatedLocalProperty(int propertyIndex)
        {
            if (this.localEvaluatedProperties is ImmutableArray<ulong> lep)
            {
                int offset = propertyIndex / 64;
                int bit = propertyIndex % 64;
                return offset < lep.Length && ((lep[offset] & (1UL << bit)) != 0);
            }

            return false;
        }

        /// <summary>
        /// Determines if an item has been locally evaluated.
        /// </summary>
        /// <param name="itemIndex">The index of the item.</param>
        /// <returns><c>True</c> if the item has been evaluated locally.</returns>
        public bool HasEvaluatedLocalItemIndex(int itemIndex)
        {
            if (this.localEvaluatedItemIndex is ImmutableArray<ulong> leii)
            {
                int offset = itemIndex / 64;
                int bit = itemIndex % 64;
                return offset < leii.Length && ((leii[offset] & (1UL << bit)) != 0);
            }

            return false;
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

            int offset = propertyIndex / 64;
            int bit = propertyIndex % 64;
            ulong bitPattern = 1UL << bit;

            if (offset < this.localEvaluatedProperties.Length && ((this.localEvaluatedProperties[offset] & bitPattern) != 0))
            {
                return true;
            }

            if (offset < this.appliedEvaluatedProperties.Length && ((this.appliedEvaluatedProperties[offset] & bitPattern) != 0))
            {
                return true;
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

            int offset = itemIndex / 64;
            int bit = itemIndex % 64;
            ulong bitPattern = 1UL << bit;

            if (offset < this.localEvaluatedItemIndex.Length && ((this.localEvaluatedItemIndex[offset] & bitPattern) != 0))
            {
                return true;
            }

            if (offset < this.appliedEvaluatedItemIndex.Length && ((this.appliedEvaluatedItemIndex[offset] & bitPattern) != 0))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Adds a result to the validation context.
        /// </summary>
        /// <param name="isValid">Whether the result is valid.</param>
        /// <param name="message">The validation message.</param>
        /// <returns>The updated validation context.</returns>
        public ValidationContext WithResult(bool isValid, string? message = null)
        {
            if ((this.usingFeatures & UsingFeatures.Results) == 0)
            {
                return new ValidationContext(this.IsValid && isValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures);
            }

            if ((this.usingFeatures & UsingFeatures.Stack) == 0)
            {
                return new ValidationContext(this.IsValid && isValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, message is string msg1 ? this.results.Add(new ValidationResult(isValid, msg1, null, null)) : this.results, this.usingFeatures);
            }

            return new ValidationContext(this.IsValid && isValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, message is string msg2 ? this.results.Add(new ValidationResult(isValid, msg2, this.absoluteKeywordLocationStack.Peek(), this.locationStack?.Peek())) : this.results, this.usingFeatures);
        }

        /// <summary>
        /// Adds an item index to the evaluated items array.
        /// </summary>
        /// <param name="index">The index to add.</param>
        /// <returns>The updated validation context.</returns>
        public ValidationContext WithLocalItemIndex(int index)
        {
            return new ValidationContext(this.IsValid, this.AddLocalEvaluatedItem(index), this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures);
        }

        /// <summary>
        /// Adds an property name to the evaluated properties array.
        /// </summary>
        /// <param name="propertyIndex">The property index to add.</param>
        /// <returns>The updated validation context.</returns>
        public ValidationContext WithLocalProperty(int propertyIndex)
        {
            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, this.AddLocalEvaluatedProperty(propertyIndex), this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack, this.absoluteKeywordLocationStack, this.results, this.usingFeatures);
        }

        /// <summary>
        /// Merges the local and applied evaluated entities from a child context into the applied evaluated entities in a parent context.
        /// </summary>
        /// <param name="childContext">The evaluated child context.</param>
        /// <param name="includeResults">Also merge the results into the parent.</param>
        /// <returns>The updated validation context.</returns>
        public ValidationContext MergeChildContext(in ValidationContext childContext, bool includeResults)
        {
            return new ValidationContext(includeResults ? this.IsValid && childContext.IsValid : this.IsValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.CombineItems(childContext), this.CombineProperties(childContext), this.locationStack, this.absoluteKeywordLocationStack, includeResults && (this.usingFeatures & UsingFeatures.Results) != 0 && (childContext.usingFeatures & UsingFeatures.Results) != 0 ? this.results.AddRange(childContext.results) : this.results, this.usingFeatures);
        }

        /// <summary>
        /// Pushes a location onto the location stack for the context.
        /// </summary>
        /// <param name="location">The location to push.</param>
        /// <param name="absoluteKeywordLocation">The abolute keyword location to push.</param>
        /// <returns>The context updated with the given location.</returns>
        public ValidationContext PushLocation(string location, string absoluteKeywordLocation)
        {
            if ((this.usingFeatures & UsingFeatures.Stack) == 0)
            {
                return this;
            }

            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack.Push(location), this.absoluteKeywordLocationStack.Push(absoluteKeywordLocation), this.results, this.usingFeatures);
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

            return new ValidationContext(this.IsValid, this.localEvaluatedItemIndex, this.localEvaluatedProperties, this.appliedEvaluatedItemIndex, this.appliedEvaluatedProperties, this.locationStack.Pop(), this.absoluteKeywordLocationStack.Pop(), this.results, this.usingFeatures);
        }

        /// <summary>
        /// Creates a child context from the current location.
        /// </summary>
        /// <returns>A new (valid) validation context with no evaluated items or properties, at the current location.</returns>
        public ValidationContext CreateChildContext()
        {
            bool usingEvaluatedItems = (this.usingFeatures & UsingFeatures.EvaluatedItems) != 0;
            bool usingEvaluatedProperties = (this.usingFeatures & UsingFeatures.EvaluatedProperties) != 0;

            return new ValidationContext(this.IsValid, usingEvaluatedItems ? ImmutableArray.Create<ulong>(0) : ImmutableArray<ulong>.Empty, usingEvaluatedProperties ? ImmutableArray.Create<ulong>(0) : ImmutableArray<ulong>.Empty, usingEvaluatedItems ? ImmutableArray.Create<ulong>(0) : ImmutableArray<ulong>.Empty, usingEvaluatedProperties ? ImmutableArray.Create<ulong>(0) : ImmutableArray<ulong>.Empty, this.locationStack, this.absoluteKeywordLocationStack, ImmutableArray<ValidationResult>.Empty, this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.Results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);

            builder.AddRange(result2.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);

            builder.AddRange(result2.results);

            builder.AddRange(result3.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);

            builder.AddRange(result2.results);

            builder.AddRange(result3.results);

            builder.AddRange(result4.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);
            builder.AddRange(result2.results);
            builder.AddRange(result3.results);
            builder.AddRange(result4.results);
            builder.AddRange(result5.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);
            builder.AddRange(result2.results);
            builder.AddRange(result3.results);
            builder.AddRange(result4.results);
            builder.AddRange(result5.results);
            builder.AddRange(result6.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);
            builder.AddRange(result2.results);
            builder.AddRange(result3.results);
            builder.AddRange(result4.results);
            builder.AddRange(result5.results);
            builder.AddRange(result6.results);
            builder.AddRange(result7.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            builder.AddRange(result1.results);
            builder.AddRange(result2.results);
            builder.AddRange(result3.results);
            builder.AddRange(result4.results);
            builder.AddRange(result5.results);
            builder.AddRange(result6.results);
            builder.AddRange(result7.results);
            builder.AddRange(result8.results);

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
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
                    isValid,
                    this.localEvaluatedItemIndex,
                    this.localEvaluatedProperties,
                    this.appliedEvaluatedItemIndex,
                    this.appliedEvaluatedProperties,
                    this.locationStack,
                    this.absoluteKeywordLocationStack,
                    this.results,
                    this.usingFeatures);
            }

            var builder = this.results.ToBuilder();

            foreach (ValidationContext result in results)
            {
                if (result.results is ImmutableArray<ValidationResult> resultResults)
                {
                    builder.AddRange(resultResults);
                }
            }

            return new ValidationContext(
                isValid,
                this.localEvaluatedItemIndex,
                this.localEvaluatedProperties,
                this.appliedEvaluatedItemIndex,
                this.appliedEvaluatedProperties,
                this.locationStack,
                this.absoluteKeywordLocationStack,
                builder.ToImmutable(),
                this.usingFeatures);
        }

        /// <summary>
        ///  Merges the bitfields representing the items we have seen in the array.
        /// </summary>
        private static void ApplyBits(ImmutableArray<ulong>.Builder result, in ImmutableArray<ulong> items)
        {
            for (int i = 0; i < items.Length; ++i)
            {
                if (i < result.Count)
                {
                    result[i] = result[i] | items.ItemRef(i);
                }
                else
                {
                    result.Add(items[i]);
                }
            }
        }

        private ImmutableArray<ulong> AddLocalEvaluatedProperty(int index)
        {
            if ((this.usingFeatures & UsingFeatures.EvaluatedProperties) != 0)
            {
                ImmutableArray<ulong> lep = this.localEvaluatedProperties;

                // Calculate the offset into the array
                int offset = index / 64;
                ulong bit = 1UL << (index % 64);
                if (offset >= lep.Length)
                {
                    lep = lep.AddRange(Enumerable.Repeat(0UL, offset - lep.Length + 1));
                }

                lep = lep.SetItem(offset, lep.ItemRef(offset) | bit);
                return lep;
            }

            return this.localEvaluatedProperties;
        }

        private ImmutableArray<ulong> AddLocalEvaluatedItem(int index)
        {
            if ((this.usingFeatures & UsingFeatures.EvaluatedItems) != 0)
            {
                ImmutableArray<ulong> lei = this.localEvaluatedItemIndex;

                // Calculate the offset into the array
                int offset = index / 64;
                ulong bit = 1UL << (index % 64);
                if (offset >= lei.Length)
                {
                    lei = lei.AddRange(Enumerable.Repeat(0UL, offset - lei.Length + 1));
                }

                lei = lei.SetItem(offset, lei.ItemRef(offset) | bit);
                return lei;
            }

            return this.localEvaluatedItemIndex;
        }

        private ImmutableArray<ulong> CombineItems(in ValidationContext childContext)
        {
            if ((this.usingFeatures & UsingFeatures.EvaluatedItems) == 0)
            {
                return this.localEvaluatedItemIndex;
            }

            var result = this.appliedEvaluatedItemIndex.ToBuilder();
            ApplyBits(result, childContext.appliedEvaluatedItemIndex);
            ApplyBits(result, childContext.localEvaluatedItemIndex);
            return result.ToImmutable();
        }

        private ImmutableArray<ulong> CombineProperties(in ValidationContext childContext)
        {
            if ((this.usingFeatures & UsingFeatures.EvaluatedProperties) == 0)
            {
                return this.localEvaluatedProperties;
            }

            var result = this.appliedEvaluatedProperties.ToBuilder();
            ApplyBits(result, childContext.appliedEvaluatedProperties);
            ApplyBits(result, childContext.localEvaluatedProperties);
            return result.ToImmutable();
        }
    }
}
