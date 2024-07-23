// <copyright file="FormatProviderExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions to get format information from <see cref="IEnumerable{IFormatProvider}"/>.
/// </summary>
public static class FormatProviderExtensions
{
    /// <summary>
    /// Gets the .NET type name for the given candidate format (e.g. JsonUuid, JsonIri, JsonInt64 etc).
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="format">The candidate format.</param>
    /// <returns>The corresponding .NET type name, or <see langword="null"/> if the format is not explicitly supported.</returns>
    public static string? GetDotnetTypeNameFor<T>(this IEnumerable<T> providers, string format)
        where T : notnull, IFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.GetDotnetTypeNameFor(format) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the expected <see cref="JsonValueKind"/> for instances
    /// that support the given format.
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="format">The format for which to get the value kind.</param>
    /// <returns>The expected <see cref="JsonValueKind"/>, or <see langword="null"/>
    /// if no value kind is expected for the format.</returns>
    public static JsonValueKind? GetExpectedValueKind<T>(this IEnumerable<T> providers, string format)
        where T : notnull, IFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.GetExpectedValueKind(format) is JsonValueKind result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the .NET BCL type name for the given C# numeric langword or type.
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="langword">The .NET numeric langword.</param>
    /// <returns>The JSON string form suffix (e.g. <see langword="long"/> becomes <c>Int64</c>.</returns>
    public static string? GetDotnetTypeNameForCSharpNumericLangwordOrTypeName<T>(this IEnumerable<T> providers, string langword)
        where T : notnull, INumberFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.GetDotnetTypeNameForCSharpNumericLangwordOrTypeName(langword) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the JSON type for the given integer format.
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    public static string? GetIntegerDotnetTypeNameFor<T>(this IEnumerable<T> providers, string format)
        where T : notnull, INumberFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.GetIntegerDotnetTypeNameFor(format) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the JSON type for the given floating-point format.
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <see cref="JsonNumber"/> if the format is not recognized.</returns>
    public static string? GetFloatDotnetTypeNameFor<T>(this IEnumerable<T> providers, string format)
        where T : notnull, INumberFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.GetFloatDotnetTypeNameFor(format) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format provider.</typeparam>
    /// <param name="providers">The providers to test.</param>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="valueIdentifier">The identifier for the value to test.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <returns><see langword="true"/> if the assertion was appended successfully.</returns>
    public static bool AppendFormatAssertion<T>(this IEnumerable<T> providers, CodeGenerator generator, string format, string valueIdentifier, string validationContextIdentifier)
        where T : notnull, IFormatProvider
    {
        foreach (T provider in providers)
        {
            if (provider.AppendFormatAssertion(generator, format, valueIdentifier, validationContextIdentifier))
            {
                return true;
            }
        }

        return false;
    }
}