// <copyright file="FormatSupport.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

// The format checks are linked from Corvus.Text.Json (see the project file). This file supplies, for this assembly
// only, the two small pieces of Corvus.Text.Json they use that this assembly does not otherwise have.
using System.Buffers;
using System.Globalization;

#pragma warning disable SA1403 // File may only contain a single namespace
#pragma warning disable SA1649 // File name should match first type name

namespace System
{
    /// <summary>
    /// The arguments the linked format checks name when they throw.
    /// </summary>
    internal enum ExceptionArgument
    {
        /// <summary>
        /// The index argument.
        /// </summary>
        index,
    }

    /// <summary>
    /// The throws the linked format checks use.
    /// </summary>
    internal static class ThrowHelper
    {
        /// <summary>
        /// Throws an <see cref="ArgumentOutOfRangeException"/> for the argument.
        /// </summary>
        /// <param name="argument">The argument.</param>
        internal static void ThrowArgumentOutOfRangeException(ExceptionArgument argument)
        {
            throw new ArgumentOutOfRangeException(argument.ToString());
        }
    }
}

#if !NET8_0_OR_GREATER
namespace Corvus.Text.Json.Internal
{
    /// <summary>
    /// The <see cref="Rune"/> the linked format checks use on netstandard: the members of <c>System.Text.Rune</c> they
    /// call, over this assembly's <see cref="Corvus.Json.Rune"/>.
    /// </summary>
    /// <remarks>
    /// The linked files are in this namespace or below it, so this type is found before any <c>Rune</c> their using
    /// directives import. The code of this assembly, in <c>Corvus.Json</c>, still uses <see cref="Corvus.Json.Rune"/>.
    /// </remarks>
    internal readonly struct Rune
    {
        private readonly Corvus.Json.Rune inner;

        private Rune(Corvus.Json.Rune inner)
        {
            this.inner = inner;
        }

        /// <summary>
        /// Gets the scalar value.
        /// </summary>
        public int Value => (int)this.inner.Value;

        /// <summary>
        /// Decodes the first scalar of UTF-8 text.
        /// </summary>
        /// <param name="source">The UTF-8 text.</param>
        /// <param name="result">The scalar.</param>
        /// <param name="bytesConsumed">The bytes read.</param>
        /// <returns>The status of the decoding.</returns>
        public static OperationStatus DecodeFromUtf8(ReadOnlySpan<byte> source, out Rune result, out int bytesConsumed)
        {
            OperationStatus status = Corvus.Json.Rune.DecodeFromUtf8(source, out Corvus.Json.Rune inner, out bytesConsumed);
            result = new(inner);
            return status;
        }

        /// <summary>
        /// Decodes the last scalar of UTF-8 text.
        /// </summary>
        /// <param name="source">The UTF-8 text.</param>
        /// <param name="result">The scalar.</param>
        /// <param name="bytesConsumed">The bytes read.</param>
        /// <returns>The status of the decoding.</returns>
        public static OperationStatus DecodeLastFromUtf8(ReadOnlySpan<byte> source, out Rune result, out int bytesConsumed)
        {
            OperationStatus status = Corvus.Json.Rune.DecodeLastFromUtf8(source, out Corvus.Json.Rune inner, out bytesConsumed);
            result = new(inner);
            return status;
        }

        /// <summary>
        /// Creates a scalar from a value, if it is a Unicode scalar value.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <param name="result">The scalar.</param>
        /// <returns><see langword="true"/> if the value is a scalar value.</returns>
        public static bool TryCreate(int value, out Rune result)
        {
            bool created = Corvus.Json.Rune.TryCreate(value, out Corvus.Json.Rune inner);
            result = new(inner);
            return created;
        }

        /// <summary>
        /// Gets the Unicode category of a scalar.
        /// </summary>
        /// <param name="value">The scalar.</param>
        /// <returns>The category.</returns>
        public static UnicodeCategory GetUnicodeCategory(Rune value) => Corvus.Json.Rune.GetUnicodeCategory(value.inner);

        /// <summary>
        /// Gets a value indicating whether a scalar is a letter.
        /// </summary>
        /// <param name="value">The scalar.</param>
        /// <returns><see langword="true"/> if it is a letter.</returns>
        public static bool IsLetter(Rune value) => Corvus.Json.Rune.IsLetter(value.inner);

        /// <summary>
        /// Gets a value indicating whether a scalar is a letter or a decimal digit.
        /// </summary>
        /// <param name="value">The scalar.</param>
        /// <returns><see langword="true"/> if it is a letter or a digit.</returns>
        public static bool IsLetterOrDigit(Rune value) => Corvus.Json.Rune.IsLetterOrDigit(value.inner);

        /// <summary>
        /// Encodes the scalar as UTF-8.
        /// </summary>
        /// <param name="destination">The buffer.</param>
        /// <returns>The bytes written.</returns>
        public int EncodeToUtf8(Span<byte> destination) => this.inner.EncodeToUtf8(destination);
    }
}
#endif