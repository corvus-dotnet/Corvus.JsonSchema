// <copyright file="IJsonString{T}.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Diagnostics.CodeAnalysis;

namespace Corvus.Json;

/// <summary>
/// A JSON string.
/// </summary>
/// <typeparam name="T">The type implementing the interface.</typeparam>
public interface IJsonString<T> : IJsonValue<T>
    where T : struct, IJsonString<T>
{
#if NET8_0_OR_GREATER
    /// <summary>
    /// Conversion from string.
    /// </summary>
    /// <param name="value">The value from which to convert.</param>
    static abstract implicit operator T(string value);
#endif

    /// <summary>
    /// Try to get the string value.
    /// </summary>
    /// <param name="value">The value as a string.</param>
    /// <returns><c>True</c> if the value can be recovered as a string.</returns>
    bool TryGetString([NotNullWhen(true)] out string? value);

    /// <summary>
    /// Get the string value as <see cref="Nullable{String}"/>.
    /// </summary>
    /// <returns>If the value is a string, the value as a string. Otherwise <c>null</c>.</returns>
    string? GetString();

#if NET8_0_OR_GREATER
    /// <summary>
    /// Concatenate two JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2>(Span<byte> buffer, in T1 firstValue, in T2 secondValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>;

    /// <summary>
    /// Concatenate three JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>;

    /// <summary>
    /// Concatenate four JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3, T4>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>;

    /// <summary>
    /// Concatenate five JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3, T4, T5>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>;

    /// <summary>
    /// Concatenate six JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3, T4, T5, T6>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>;

    /// <summary>
    /// Concatenate seven JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3, T4, T5, T6, T7>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>;

    /// <summary>
    /// Concatenate eight JSON values, producing an instance of the string type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T1">The type of the first value.</typeparam>
    /// <typeparam name="T2">The type of the second value.</typeparam>
    /// <typeparam name="T3">The type of the third value.</typeparam>
    /// <typeparam name="T4">The type of the fourth value.</typeparam>
    /// <typeparam name="T5">The type of the fifth value.</typeparam>
    /// <typeparam name="T6">The type of the sixth value.</typeparam>
    /// <typeparam name="T7">The type of the seventh value.</typeparam>
    /// <typeparam name="T8">The type of the eighth value.</typeparam>
    /// <param name="buffer">The buffer into which to concatenate the values.</param>
    /// <param name="firstValue">The first value.</param>
    /// <param name="secondValue">The second value.</param>
    /// <param name="thirdValue">The third value.</param>
    /// <param name="fourthValue">The fourth value.</param>
    /// <param name="fifthValue">The fifth value.</param>
    /// <param name="sixthValue">The sixth value.</param>
    /// <param name="seventhValue">The seventh value.</param>
    /// <param name="eighthValue">The eighth value.</param>
    /// <returns>A count of the characters written.</returns>
    static abstract T Concatenate<T1, T2, T3, T4, T5, T6, T7, T8>(Span<byte> buffer, in T1 firstValue, in T2 secondValue, in T3 thirdValue, in T4 fourthValue, in T5 fifthValue, in T6 sixthValue, in T7 seventhValue, in T8 eighthValue)
        where T1 : struct, IJsonValue<T1>
        where T2 : struct, IJsonValue<T2>
        where T3 : struct, IJsonValue<T3>
        where T4 : struct, IJsonValue<T4>
        where T5 : struct, IJsonValue<T5>
        where T6 : struct, IJsonValue<T6>
        where T7 : struct, IJsonValue<T7>
        where T8 : struct, IJsonValue<T8>;
#endif
}