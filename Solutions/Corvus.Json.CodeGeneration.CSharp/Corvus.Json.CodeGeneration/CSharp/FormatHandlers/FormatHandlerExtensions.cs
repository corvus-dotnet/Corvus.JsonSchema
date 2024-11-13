// <copyright file="FormatHandlerExtensions.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extensions for <see cref="IEnumerable{IFormatHandler}"/>.
/// </summary>
public static class FormatHandlerExtensions
{
    /// <summary>
    /// Append format-specific expressions in the body of an <c>Equals&lt;T&gt;</c> method where the comparison values are <c>this</c> and <c>other</c>.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append expressions.</param>
    /// <param name="generator">The generator to which to append the format expressions.</param>
    /// <param name="typeDeclaration">The type declaration for which to append expressions.</param>
    /// <param name="format">The format for which to append expressions.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatEqualsTBody<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatEqualsTBody(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific public static methods to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append methods.</param>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPublicStaticMethods<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPublicStaticMethods(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific public methods to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append methods.</param>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPublicMethods<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPublicMethods(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific private static methods to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append methods.</param>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPrivateStaticMethods<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPrivateStaticMethods(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific private methods to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append methods.</param>
    /// <param name="generator">The generator to which to append the format methods.</param>
    /// <param name="typeDeclaration">The type declaration for which to append methods.</param>
    /// <param name="format">The format for which to append methods.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPrivateMethods<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPrivateMethods(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific conversion operators to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append conversion operators.</param>
    /// <param name="generator">The generator to which to append the format conversion operators.</param>
    /// <param name="typeDeclaration">The type declaration for which to append conversion operators.</param>
    /// <param name="format">The format for which to append conversion operators.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatConversionOperators<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatConversionOperators(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific public static properties to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append properties.</param>
    /// <param name="generator">The generator to which to append the format properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to append properties.</param>
    /// <param name="format">The format for which to append properties.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPublicStaticProperties<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPublicStaticProperties(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific public properties to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append properties.</param>
    /// <param name="generator">The generator to which to append the format properties.</param>
    /// <param name="typeDeclaration">The type declaration for which to append properties.</param>
    /// <param name="format">The format for which to append properties.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatPublicProperties<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatPublicProperties(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific constructors to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append constructors.</param>
    /// <param name="generator">The generator to which to append the format constructors.</param>
    /// <param name="typeDeclaration">The type declaration for which to append constructors.</param>
    /// <param name="format">The format for which to append constructors.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatConstructors<T>(this IEnumerable<T> handlers, CodeGenerator generator, TypeDeclaration typeDeclaration, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatConstructors(generator, typeDeclaration, format))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Append format-specific constant values to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers which may append properties.</param>
    /// <param name="generator">The generator to which to append the format constant value.</param>
    /// <param name="keyword">The keyword that produced the constant value.</param>
    /// <param name="format">The format for which to append the format constant value.</param>
    /// <param name="fieldName">The name of the field to generate.</param>
    /// <param name="constantValue">The constant value as a <see cref="JsonElement"/>.</param>
    /// <returns><see langword="true"/> if the instance handled this format.</returns>
    public static bool AppendFormatConstant<T>(
        this IEnumerable<T> handlers,
        CodeGenerator generator,
        ITypedValidationConstantProviderKeyword keyword,
        string format,
        string fieldName,
        JsonElement constantValue)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatConstant(generator, keyword, format, fieldName, constantValue))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Gets the Corvus.Json type name for the given candidate format (e.g. JsonUuid, JsonIri, JsonInt64 etc).
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="format">The candidate format.</param>
    /// <returns>The corresponding .NET type name, or <see langword="null"/> if the format is not explicitly supported.</returns>
    public static string? GetCorvusJsonTypeNameFor<T>(this IEnumerable<T> handlers, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (handler.GetCorvusJsonTypeNameFor(format) is string result)
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
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="format">The format for which to get the value kind.</param>
    /// <returns>The expected <see cref="JsonValueKind"/>, or <see langword="null"/>
    /// if no value kind is expected for the format.</returns>
    public static JsonValueKind? GetExpectedValueKind<T>(this IEnumerable<T> handlers, string format)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (handler.GetExpectedValueKind(format) is JsonValueKind result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the .NET BCL type name for the given C# numeric langword, or BCL type name.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="langwordOrTypeName">The .NET numeric langword, or BCL type name.</param>
    /// <returns>The JSON string form suffix (e.g. <see langword="long"/> becomes <c>Int64</c>.</returns>
    public static string? GetTypeNameForNumericLangwordOrTypeName<T>(this IEnumerable<T> handlers, string langwordOrTypeName)
        where T : notnull, INumberFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (handler.GetTypeNameForNumericLangwordOrTypeName(langwordOrTypeName) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the JSON type for the given integer format.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <c>JsonNumber</c> if the format is not recognized.</returns>
    public static string? GetIntegerCorvusJsonTypeNameFor<T>(this IEnumerable<T> handlers, string format)
        where T : notnull, INumberFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (handler.GetIntegerCorvusJsonTypeNameFor(format) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Gets the JSON type for the given floating-point format.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="format">The format for which to get the type.</param>
    /// <returns>The <c>Corvus.Json</c> type name corresponding to the format,
    /// or <c>JsonNumber</c> if the format is not recognized.</returns>
    public static string? GetFloatCorvusJsonTypeNameFor<T>(this IEnumerable<T> handlers, string format)
        where T : notnull, INumberFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (handler.GetFloatCorvusJsonTypeNameFor(format) is string result)
            {
                return result;
            }
        }

        return null;
    }

    /// <summary>
    /// Append a format assertion to the generator.
    /// </summary>
    /// <typeparam name="T">The type of the format handler.</typeparam>
    /// <param name="handlers">The handlers to test.</param>
    /// <param name="generator">The generator to which to append the format assertion.</param>
    /// <param name="format">The format to assert.</param>
    /// <param name="valueIdentifier">The identifier for the value to test.</param>
    /// <param name="validationContextIdentifier">The identifier for the validation context to update.</param>
    /// <param name="typeKeyword">The type keyword.</param>
    /// <param name="formatKeyword">The format keyword.</param>
    /// <param name="returnFromMethod"><see langword="true"/> if you should return from the method, otherwise it updates the <paramref name="validationContextIdentifier"/>.</param>
    /// <param name="includeType"><see langword="true"/> if you should also include type assertion.</param>
    /// <returns><see langword="true"/> if the assertion was appended successfully.</returns>
    public static bool AppendFormatAssertion<T>(
        this IEnumerable<T> handlers,
        CodeGenerator generator,
        string format,
        string valueIdentifier,
        string validationContextIdentifier,
        IKeyword? typeKeyword,
        IKeyword? formatKeyword,
        bool returnFromMethod,
        bool includeType = false)
        where T : notnull, IFormatHandler
    {
        foreach (T handler in handlers)
        {
            if (generator.IsCancellationRequested)
            {
                return false;
            }

            if (handler.AppendFormatAssertion(generator, format, valueIdentifier, validationContextIdentifier, includeType, typeKeyword, formatKeyword, returnFromMethod))
            {
                return true;
            }
        }

        return false;
    }
}