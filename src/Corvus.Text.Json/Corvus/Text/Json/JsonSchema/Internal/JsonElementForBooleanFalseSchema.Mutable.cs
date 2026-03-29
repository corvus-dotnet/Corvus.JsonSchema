// <copyright file="JsonElementForBooleanFalseSchema.Mutable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>
// <licensing>
// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.
// https:// github.com/dotnet/runtime/blob/388a7c4814cb0d6e344621d017507b357902043a/LICENSE.TXT
// </licensing>
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Corvus.Text.Json.Internal;

namespace Corvus.Text.Json;

public readonly partial struct JsonElementForBooleanFalseSchema
{
    /// <summary>
    /// Represents a specific JSON value within a <see cref="IMutableJsonDocument"/>.
    /// </summary>
    [DebuggerDisplay("{DebuggerDisplay,nq}")]
    public readonly partial struct Mutable : IMutableJsonElement<Mutable>
    {
        private readonly IMutableJsonDocument _parent;

        private readonly int _idx;

        private readonly ulong _documentVersion;

#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        /// <summary>
        /// Initializes a new instance of the <see cref="Mutable"/> struct.
        /// </summary>
        /// <param name="parent">The parent JSON document.</param>
        /// <param name="idx">The index within the parent document.</param>
        internal Mutable(IJsonDocument parent, int idx)
        {
            // parent is usually not null, but the Current property
            // on the enumerators (when initialized as `default`) can
            // get here with a null.
            Debug.Assert(idx >= 0);

            _parent = (IMutableJsonDocument)parent;
            _idx = idx;
            _documentVersion = _parent?.Version ?? 0;
        }

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private readonly JsonTokenType TokenType
        {
            get
            {
                return _parent?.GetJsonTokenType(_idx) ?? JsonTokenType.None;
            }
        }

        /// <summary>
        /// The <see cref="JsonValueKind"/> that the value is.
        /// </summary>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public JsonValueKind ValueKind => TokenType.ToValueKind();

        /// <summary>
        /// Get the value at a specified index when the current value is a
        /// <see cref="JsonValueKind.Array"/>.
        /// </summary>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is not <see cref="JsonValueKind.Array"/>.
        /// </exception>
        /// <exception cref="IndexOutOfRangeException">
        /// <paramref name="index"/> is not in the range [0, <see cref="GetArrayLength"/>()).
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public Mutable this[int index]
        {
            get
            {
                CheckValidInstance();

                return _parent.GetArrayIndexElement<Mutable>(_idx, index);
            }
        }

        /// <summary>
        /// Implicitly converts a <see cref="Mutable"/> to a <see cref="JsonElementForBooleanFalseSchema"/>.
        /// </summary>
        /// <param name="value">The mutable value to convert.</param>
        /// <returns>A <see cref="JsonElementForBooleanFalseSchema"/> representing the same JSON element.</returns>
        public static implicit operator JsonElementForBooleanFalseSchema(Mutable value)
        {
            return new(value._parent, value._idx);
        }

        /// <summary>
        /// Explicitly converts a <see cref="JsonElementForBooleanFalseSchema"/> to a <see cref="Mutable"/>.
        /// </summary>
        /// <param name="value">The value to convert.</param>
        /// <returns>A <see cref="Mutable"/> representing the same JSON element.</returns>
        /// <exception cref="FormatException">The parent document is not a mutable document.</exception>
        public static explicit operator Mutable(JsonElementForBooleanFalseSchema value)
        {
            if (value._parent is not IMutableJsonDocument)
            {
                ThrowHelper.ThrowFormatException();

                // We will never get here
                return default;
            }

            return new(value._parent, value._idx);
        }

        /// <summary>
        /// Determines whether two <see cref="Mutable"/> values are equal.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Mutable left, Mutable right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two <see cref="Mutable"/> values are not equal.
        /// </summary>
        /// <param name="left">The first value to compare.</param>
        /// <param name="right">The second value to compare.</param>
        /// <returns><see langword="true"/> if the values are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Mutable left, Mutable right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether a <see cref="Mutable"/> and a <see cref="JsonElement"/> are equal.
        /// </summary>
        /// <param name="left">The mutable value to compare.</param>
        /// <param name="right">The JSON element to compare.</param>
        /// <returns><see langword="true"/> if the values are equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator ==(Mutable left, JsonElement right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether a <see cref="Mutable"/> and a <see cref="JsonElement"/> are not equal.
        /// </summary>
        /// <param name="left">The mutable value to compare.</param>
        /// <param name="right">The JSON element to compare.</param>
        /// <returns><see langword="true"/> if the values are not equal; otherwise, <see langword="false"/>.</returns>
        public static bool operator !=(Mutable left, JsonElement right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current mutable JSON element.
        /// </summary>
        /// <param name="obj">The object to compare with the current element.</param>
        /// <returns><see langword="true"/> if the specified object is equal to the current element; otherwise, <see langword="false"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override bool Equals(object? obj)
        {
            return (obj is IJsonElement other && Equals(new JsonElementForBooleanFalseSchema(other.ParentDocument, other.ParentDocumentIndex)))
                || (obj is null && this.IsNull());
        }

        /// <summary>
        /// Determines whether this mutable JSON element is equal to another JSON element using deep comparison.
        /// </summary>
        /// <typeparam name="T">The type of the JSON element to compare with.</typeparam>
        /// <param name="other">The JSON element to compare with this element.</param>
        /// <returns><see langword="true"/> if the elements are deeply equal; otherwise, <see langword="false"/>.</returns>
        [CLSCompliant(false)]
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool Equals<T>(T other)
            where T : struct, IJsonElement
        {
            return JsonElementHelpers.DeepEquals(this, other);
        }

        /// <summary>
        /// Creates a document builder for this mutable JSON element.
        /// </summary>
        /// <param name="workspace">The JSON workspace to use for creating the document builder.</param>
        /// <returns>A document builder that can be used to create a new JSON document based on this element.</returns>
        [CLSCompliant(false)]
        public readonly JsonDocumentBuilder<Mutable> CreateBuilder(JsonWorkspace workspace)
        {
            return workspace.CreateBuilder<Mutable, Mutable>(this);
        }

        /// <summary>
        /// Creates a new <see cref="Mutable"/> instance from another mutable JSON element.
        /// </summary>
        /// <typeparam name="T">The type of the source mutable JSON element.</typeparam>
        /// <param name="instance">The source mutable JSON element to create from.</param>
        /// <returns>A new <see cref="Mutable"/> instance representing the same JSON element.</returns>
        [CLSCompliant(false)]
        public static Mutable From<T>(in T instance)
            where T : struct, IMutableJsonElement<T>
        {
            return new(instance.ParentDocument, instance.ParentDocumentIndex);
        }

        /// <summary>
        /// Determines if the JSON element at the specified index in the parent document is valid.
        /// </summary>
        /// <param name="parentDocument">The parent document containing the JSON element.</param>
        /// <param name="parentIndex">The index of the JSON element within the parent document.</param>
        /// <returns><see langword="true"/> if the element at the specified index is valid; otherwise, <see langword="false"/>.</returns>
        internal static bool IsValid(IJsonDocument parentDocument, int parentIndex)
        {
            return IsValid(parentDocument, parentIndex);
        }

        /// <summary>
        /// Write the element into the provided writer as a JSON value.
        /// </summary>
        /// <param name="writer">The writer.</param>
        /// <exception cref="ArgumentNullException">
        /// The <paramref name="writer"/> parameter is <see langword="null"/>.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This value's <see cref="ValueKind"/> is <see cref="JsonValueKind.Undefined"/>.
        /// </exception>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public void WriteTo(Utf8JsonWriter writer)
        {
            ArgumentNullException.ThrowIfNull(writer);

            CheckValidInstance();

            _parent.WriteElementTo(_idx, writer);
        }

        /// <summary>
        /// Gets a string representation for the current value appropriate to the value type.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For Mutable built from <see cref="IMutableJsonDocument"/>:
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.Null"/>, <see cref="string.Empty"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.True"/>, <see cref="bool.TrueString"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.False"/>, <see cref="bool.FalseString"/> is returned.
        /// </para>
        ///
        /// <para>
        /// For <see cref="JsonValueKind.String"/>, the value of <see cref="GetString"/>() is returned.
        /// </para>
        ///
        /// <para>
        /// For other types, the value of <see cref="GetRawText"/>() is returned.
        /// </para>
        /// </remarks>
        /// <returns>
        /// A string representation for the current value appropriate to the value type.
        /// </returns>
        /// <exception cref="ObjectDisposedException">
        /// The parent <see cref="JsonDocument"/> has been disposed.
        /// </exception>
        public override readonly string ToString()
        {
            if (_parent == null || _documentVersion != _parent.Version)
            {
                return string.Empty;
            }

            return _parent.ToString(_idx);
        }

        /// <inheritdoc />
        public override readonly int GetHashCode()
        {
            if (_parent is null)
            {
                return 0;
            }

            return _parent.GetHashCode(_idx);
        }

        /// <summary>
        /// Get a JsonElement which can be safely stored beyond the lifetime of the
        /// original <see cref="IMutableJsonDocument"/>.
        /// </summary>
        /// <returns>
        /// A JsonElement which can be safely stored beyond the lifetime of the
        /// original <see cref="IMutableJsonDocument"/>.
        /// </returns>
        public readonly JsonElement Clone()
        {
            CheckValidInstance();

            return _parent.CloneElement(_idx);
        }

        private readonly void CheckValidInstance()
        {
            if (_parent == null)
            {
                throw new InvalidOperationException();
            }

            if (_idx != 0 && _documentVersion != _parent.Version)
            {
                throw new InvalidOperationException();
            }
        }

        void IJsonElement.CheckValidInstance() => CheckValidInstance();

#if NET

        static Mutable IJsonElement<Mutable>.CreateInstance(IJsonDocument parentDocument, int parentDocumentIndex) => new(parentDocument, parentDocumentIndex);

#endif

        /// <summary>
        /// Evaluates this mutable element against the boolean false schema.
        /// </summary>
        /// <param name="resultsCollector">The optional results collector for schema evaluation.</param>
        /// <returns><see langword="false"/> because this represents a boolean false schema.</returns>
        public readonly bool EvaluateSchema(IJsonSchemaResultsCollector? resultsCollector = null) => JsonSchema.Evaluate(_parent, _idx, resultsCollector);

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private string DebuggerDisplay => $"JsonElementForBooleanFalseSchema.Mutable: ValueKind = {ValueKind} : \"{ToString()}\"";

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly IJsonDocument IJsonElement.ParentDocument => _parent;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly int IJsonElement.ParentDocumentIndex => _idx;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        JsonTokenType IJsonElement.TokenType => TokenType;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        JsonValueKind IJsonElement.ValueKind => ValueKind;
    }
}