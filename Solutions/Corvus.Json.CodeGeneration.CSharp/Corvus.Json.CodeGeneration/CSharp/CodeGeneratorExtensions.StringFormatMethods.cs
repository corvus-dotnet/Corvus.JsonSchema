// <copyright file="CodeGeneratorExtensions.StringFormatMethods.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Text.Json;

namespace Corvus.Json.CodeGeneration.CSharp;

/// <summary>
/// Extension methods for the <see cref="CodeGenerator"/>.
/// </summary>
internal static partial class CodeGeneratorExtensions
{
    /// <summary>
    /// Appends uri-template format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriTemplateFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as a <see cref="Corvus.Json.UriTemplates.UriTemplate"/>.
                /// </summary>
                /// <param name="result">The value as a UriTemplate.</param>
                /// <param name="resolvePartially">Whether to allow partial resolution.</param>
                /// <param name="caseInsensitiveParameterNames">Whether to use case insensitive parameter names.</param>
                /// <param name="createParameterRegex">Whether to create a parameter regex (defaults to true).</param>
                /// <param name="parameters">The parameter values with which to initialize the template.</param>
                /// <returns><see langword="true" /> if the value could be retrieved.</returns>
                """)
            .ReserveName("TryGetUriTemplate")
            .AppendLineIndent("public bool TryGetUriTemplate(out Corvus.Json.UriTemplates.UriTemplate result, bool resolvePartially = false, bool caseInsensitiveParameterNames = false, bool createParameterRegex = true, System.Collections.Immutable.ImmutableDictionary<string, JsonAny>? parameters = null)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (this.GetString() is string str)")
                .AppendLineIndent("{")
                .PushIndent()
                    .AppendLineIndent("result = new Corvus.Json.UriTemplates.UriTemplate(str, resolvePartially, caseInsensitiveParameterNames, createParameterRegex, parameters);")
                    .AppendLineIndent("return true;")
                .PopIndent()
                .AppendLineIndent("}")
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}")
            .AppendSeparatorLine()
            .ReserveName("TryCreateUriTemplateParser")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Get a <see cref="Corvus.UriTemplates.IUriTemplateParser"/>.
                /// </summary>
                /// <param name="result">The <see cref="Corvus.UriTemplates.IUriTemplateParser"/>, or <see langword="null"/> if it was
                /// not possible to build a URI template parser.</param>
                /// <returns><see langword="true"/> if the template parser was created.</returns>
                """)
            .AppendLineIndent("public bool TryCreateUriTemplateParser([NotNullWhen(true)] out Corvus.UriTemplates.IUriTemplateParser? result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(Parse, default(object?), out result);")
                .AppendSeparatorLine()
                .AppendConditionalBackingValueCallbackIndent(
                    "Backing.String",
                    "stringBacking",
                    AppendCreateParser)
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
                .AppendSeparatorLine()
                .AppendBlockIndent(
                """
                static bool Parse(ReadOnlySpan<char> str, in object? st, out Corvus.UriTemplates.IUriTemplateParser result)
                {
                    result = Corvus.UriTemplates.UriTemplateParserFactory.CreateParser(str);
                    return true;
                }
                """)
            .PopIndent()
            .AppendLineIndent("}");

        return true;

        static void AppendCreateParser(CodeGenerator generator, string backingValue)
        {
            generator
                .AppendLineIndent("result = Corvus.UriTemplates.UriTemplateParserFactory.CreateParser(this.", backingValue, ");")
                .AppendLineIndent("return true;");
        }
    }

    /// <summary>
    /// Appends the uri format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Uri", CoreTypes.String, "StandardUri.FormatUri(value)");

        return true;
    }

    /// <summary>
    /// Appends uri format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "Uri", "value.GetUri();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Uri", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends uri format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return AppendCommonUriFormatPublicMethods(generator);
    }

    /// <summary>
    /// Appends the uri-reference format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriReferenceFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Uri", CoreTypes.String, "StandardUri.FormatUri(value)");

        return true;
    }

    /// <summary>
    /// Appends uri-reference format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriReferenceFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "Uri", "value.GetUri();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Uri", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends uri-reference format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUriReferenceFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return AppendCommonUriReferenceFormatPublicMethods(generator);
    }

    /// <summary>
    /// Appends the iri format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Uri", CoreTypes.String, "StandardUri.FormatUri(value)");

        return true;
    }

    /// <summary>
    /// Appends iri format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "Uri", "value.GetUri();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Uri", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends iri format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return AppendCommonUriFormatPublicMethods(generator);
    }

    /// <summary>
    /// Appends the iri-reference format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriReferenceFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Uri", CoreTypes.String, "StandardUri.FormatUri(value)");

        return true;
    }

    /// <summary>
    /// Appends iri-reference format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriReferenceFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "Uri", "value.GetUri();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Uri", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends iri-reference format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIriReferenceFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        return AppendCommonUriReferenceFormatPublicMethods(generator);
    }

    /// <summary>
    /// Appends the uuid format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUuidFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Guid", CoreTypes.String, "StandardUuid.FormatGuid(value)");

        return true;
    }

    /// <summary>
    /// Appends uuid format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUuidFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "Guid", "value.GetGuid();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Guid", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends uuid format <c>Equals&lt;T&gt;</c> method body.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUuidFormatEqualsTBody(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendBlockIndent(
            """
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            """)
            .AppendLineIndent("if (!other.As<", typeDeclaration.DotnetTypeName(), ">().TryGetGuid(out Guid otherGuid))")
            .AppendBlockIndent(
            """
            {
                return false;
            }

            if (!this.TryGetGuid(out Guid thisGuid))
            {
                return false;
            }

            return thisGuid.Equals(otherGuid);
            """);

        return true;
    }

    /// <summary>
    /// Appends uuid format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendUuidFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetGuid")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as a <see cref="Guid"/>.
                /// </summary>
                /// <returns>The value as a <see cref="Guid"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a uuid.</exception>
                public Guid GetGuid()
                {
                    if (this.TryGetGuid(out Guid result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the uuid value as a <see cref="Guid"/>.
                /// </summary>
                /// <param name="result">The value as a <see cref="Guid"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a uuid value from the instance.</returns>
                """)
            .ReserveName("TryGetGuid")
            .AppendLineIndent("public bool TryGetGuid(out Guid result)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardUuid.GuidParser, (object?)null, out result);")
            .AppendLine("#else")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return StandardUuid.GuidParser(",
                    "jsonElementBacking",
                    ".GetString(), null, out result);")
            .AppendLine("#endif")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardUuid.GuidParser(",
                    "stringBacking",
                    ", null, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = Guid.Empty;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends the ipV6 format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV6FormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "System.Net.IPAddress", CoreTypes.String, "value.ToString()");

        return true;
    }

    /// <summary>
    /// Appends ipV6 format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV6FormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "System.Net.IPAddress", "value.GetIPAddress();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "System.Net.IPAddress", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends ipV6 format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV6FormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetIPAddress")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an <see cref="System.Net.IPAddress"/>.
                /// </summary>
                /// <returns>The value as an <see cref="System.Net.IPAddress"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a ipV6.</exception>
                public System.Net.IPAddress GetIPAddress()
                {
                    if (this.TryGetIPAddress(out System.Net.IPAddress? result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the ipv6 value as an <see cref="System.Net.IPAddress"/>.
                /// </summary>
                /// <param name="result">The value as an <see cref="System.Net.IPAddress"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a ipV6 value from the instance.</returns>
                """)
            .ReserveName("TryGetIPAddress")
            .AppendLineIndent("public bool TryGetIPAddress([NotNullWhen(true)] out System.Net.IPAddress? result)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardIPAddress.IPAddressParser, (object?)null, out result);")
            .AppendLine("#else")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return StandardIPAddress.IPAddressParser(",
                    "jsonElementBacking",
                    ".GetString(), null, out result);")
            .AppendLine("#endif")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardIPAddress.IPAddressParser(",
                    "stringBacking",
                    ", null, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = System.Net.IPAddress.None;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends the ipv4 format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV4FormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "System.Net.IPAddress", CoreTypes.String, "value.ToString()");

        return true;
    }

    /// <summary>
    /// Appends ipv4 format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV4FormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "System.Net.IPAddress", "value.GetIPAddress();")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "System.Net.IPAddress", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends ipv4 format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendIpV4FormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetIPAddress")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an <see cref="System.Net.IPAddress"/>.
                /// </summary>
                /// <returns>The value as an <see cref="System.Net.IPAddress"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a ipv4.</exception>
                public System.Net.IPAddress GetIPAddress()
                {
                    if (this.TryGetIPAddress(out System.Net.IPAddress? result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the ipv4 value as an <see cref="System.Net.IPAddress"/>.
                /// </summary>
                /// <param name="result">The value as an <see cref="System.Net.IPAddress"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a ipv4 value from the instance.</returns>
                """)
            .ReserveName("TryGetIPAddress")
            .AppendLineIndent("public bool TryGetIPAddress([NotNullWhen(true)] out System.Net.IPAddress? result)")
            .AppendLineIndent("{")
            .PushIndent()
            .AppendLine("#if NET8_0_OR_GREATER")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardIPAddress.IPAddressParser, (object?)null, out result);")
            .AppendLine("#else")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return StandardIPAddress.IPAddressParser(",
                    "jsonElementBacking",
                    ".GetString(), null, out result);")
            .AppendLine("#endif")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardIPAddress.IPAddressParser(",
                    "stringBacking",
                    ", null, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = System.Net.IPAddress.None;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends the duration format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDurationFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "Period", CoreTypes.String, "StandardDateFormat.FormatPeriod(value)")
            .AppendPublicConvertedValueConstructor(typeDeclaration, "NodaTime.Period", CoreTypes.String, "StandardDateFormat.FormatPeriod(value)");

        return true;
    }

    /// <summary>
    /// Appends duration format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDurationFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "NodaTime.Period", "(NodaTime.Period)value.GetPeriod()")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "NodaTime.Period", useInForSourceType: true)
            .AppendImplicitConversionToType(typeDeclaration, "Period", "value.GetPeriod()")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "Period", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends duration format <c>Equals&lt;T&gt;</c> method body.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDurationFormatEqualsTBody(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendBlockIndent(
            """
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            """)
            .AppendLineIndent("if (!other.As<", typeDeclaration.DotnetTypeName(), ">().TryGetPeriod(out Period otherPeriod))")
            .AppendBlockIndent(
            """
            {
                return false;
            }

            if (!this.TryGetPeriod(out Period thisPeriod))
            {
                return false;
            }

            return thisPeriod.Equals(otherPeriod);
            """);

        return true;
    }

    /// <summary>
    /// Appends duration format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDurationFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetPeriod")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an <see cref="Period"/>.
                /// </summary>
                /// <returns>The value as an <see cref="Period"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a duration.</exception>
                /// <remarks>
                /// Note that a <see cref="Period"/> is implicitly convertible to a
                /// <see cref="NodaTime.Period"/>.
                /// </remarks>
                public Period GetPeriod()
                {
                    if (this.TryGetPeriod(out Period result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the duration value as an <see cref="Period"/>.
                /// </summary>
                /// <param name="result">The value as an <see cref="Period"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a duration value from the instance.</returns>
                /// <remarks>
                /// Note that a <see cref="Period"/> is implicitly convertible to a
                /// <see cref="NodaTime.Period"/>.
                /// </remarks>
                """)
            .ReserveName("TryGetPeriod")
            .AppendLineIndent("public bool TryGetPeriod(out Period result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLine("#if NET8_0_OR_GREATER")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "if (",
                    "jsonElementBacking",
                    ".TryGetValue(static (ReadOnlySpan<char> text, in object? _, out PeriodBuilder builder) => Period.PeriodParser(text, out builder), default, out PeriodBuilder builder)) { result = builder.BuildPeriod(); return true; }")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "if (Period.PeriodParser(",
                    "stringBacking",
                    ", out PeriodBuilder builder)) { result = builder.BuildPeriod(); return true; }")
                .AppendSeparatorLine()
                .AppendLine("#else")
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "if (",
                    "jsonElementBacking",
                    ".TryGetValue(static (ReadOnlySpan<char> text, in object? _, out Period period) => Period.TryParse(text, out period), default, out result)) { return true; }")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "if (Period.TryParse(",
                    "stringBacking",
                    ".AsSpan(), out result)) { return true; }")
                .AppendLine("#endif")
                .AppendLineIndent("result = Period.Zero;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends the time format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendTimeFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "NodaTime.OffsetTime", CoreTypes.String, "StandardDateFormat.FormatTime(value)")
            .AppendSeparatorLine()
            .AppendLine("#if NET8_0_OR_GREATER")
                .AppendPublicConvertedValueConstructor(typeDeclaration, "TimeOnly", CoreTypes.String, "StandardDateFormat.FormatTime(new NodaTime.OffsetTime(NodaTime.LocalTime.FromTimeOnly(value), NodaTime.Offset.Zero))")
                .AppendPublicConvertedValueConstructor(typeDeclaration, "TimeOnly", "NodaTime.Offset", CoreTypes.String, "StandardDateFormat.FormatTime(new NodaTime.OffsetTime(NodaTime.LocalTime.FromTimeOnly(value1), value2))")
            .AppendLine("#endif");
        return true;
    }

    /// <summary>
    /// Appends time format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendTimeFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "NodaTime.OffsetTime", "value.GetTime()")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "NodaTime.OffsetTime", useInForSourceType: true)
            .AppendSeparatorLine()
            .AppendLine("#if NET8_0_OR_GREATER")
                .AppendImplicitConversionToType(typeDeclaration, "TimeOnly", "new(value.GetTime().TickOfDay)")
                .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "TimeOnly", useInForSourceType: true)
            .AppendLine("#endif");

        return true;
    }

    /// <summary>
    /// Appends time format <c>Equals&lt;T&gt;</c> method body.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendTimeFormatEqualsTBody(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendBlockIndent(
            """
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            """)
            .AppendLineIndent("if (!other.As<", typeDeclaration.DotnetTypeName(), ">().TryGetTime(out NodaTime.OffsetTime otherTime))")
            .AppendBlockIndent(
            """
            {
                return false;
            }

            if (!this.TryGetTime(out NodaTime.OffsetTime thisTime))
            {
                return false;
            }

            return thisTime.Equals(otherTime);
            """);

        return true;
    }

    /// <summary>
    /// Appends time format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendTimeFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetTime")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an <see cref="NodaTime.OffsetTime"/>.
                /// </summary>
                /// <returns>The value as an <see cref="NodaTime.OffsetTime"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a time.</exception>
                public NodaTime.OffsetTime GetTime()
                {
                    if (this.TryGetTime(out NodaTime.OffsetTime result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the time value as an <see cref="NodaTime.OffsetTime"/>.
                /// </summary>
                /// <param name="result">The value as an <see cref="NodaTime.OffsetTime"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a time value from the instance.</returns>
                """)
            .ReserveName("TryGetTime")
            .AppendLineIndent("public bool TryGetTime(out NodaTime.OffsetTime result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardDateFormat.TimeParser, default(object?), out result);")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardDateFormat.TimeParser(",
                    "stringBacking",
                    ".AsSpan(), default, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends the date-time format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateTimeFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "NodaTime.OffsetDateTime", CoreTypes.String, "StandardDateFormat.FormatDateTime(value)")
            .AppendPublicConvertedValueConstructor(typeDeclaration, "DateTimeOffset", CoreTypes.String, "StandardDateFormat.FormatDateTime(NodaTime.OffsetDateTime.FromDateTimeOffset(value))");
        return true;
    }

    /// <summary>
    /// Appends date-time format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateTimeFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "NodaTime.OffsetDateTime", "value.GetDateTime()")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "NodaTime.OffsetDateTime", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends date-time format <c>Equals&lt;T&gt;</c> method body.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateTimeFormatEqualsTBody(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendBlockIndent(
            """
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            """)
            .AppendLineIndent("if (!other.As<", typeDeclaration.DotnetTypeName(), ">().TryGetDateTime(out NodaTime.OffsetDateTime otherDateTime))")
            .AppendBlockIndent(
            """
            {
                return false;
            }

            if (!this.TryGetDateTime(out NodaTime.OffsetDateTime thisDateTime))
            {
                return false;
            }

            return thisDateTime.Equals(otherDateTime);
            """);

        return true;
    }

    /// <summary>
    /// Appends date-time format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateTimeFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetDateTime")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as an <see cref="NodaTime.OffsetDateTime"/>.
                /// </summary>
                /// <returns>The value as an <see cref="NodaTime.OffsetDateTime"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a date-time.</exception>
                public NodaTime.OffsetDateTime GetDateTime()
                {
                    if (this.TryGetDateTime(out NodaTime.OffsetDateTime result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the date-time value as an <see cref="NodaTime.OffsetDateTime"/>.
                /// </summary>
                /// <param name="result">The value as an <see cref="NodaTime.OffsetDateTime"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a date-time value from the instance.</returns>
                """)
            .ReserveName("TryGetDateTime")
            .AppendLineIndent("public bool TryGetDateTime(out NodaTime.OffsetDateTime result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardDateFormat.DateTimeParser, default(object?), out result);")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardDateFormat.DateTimeParser(",
                    "stringBacking",
                    ".AsSpan(), default, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    /// <summary>
    /// Appends date-time format <c>Equals&lt;T&gt;</c> method body.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateFormatEqualsTBody(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendBlockIndent(
            """
            if (this.IsNull() && other.IsNull())
            {
                return true;
            }

            if (other.ValueKind != JsonValueKind.String)
            {
                return false;
            }
            """)
            .AppendLineIndent("if (!other.As<", typeDeclaration.DotnetTypeName(), ">().TryGetDate(out NodaTime.LocalDate otherDate))")
            .AppendBlockIndent(
            """
            {
                return false;
            }

            if (!this.TryGetDate(out NodaTime.LocalDate thisDate))
            {
                return false;
            }

            return thisDate.Equals(otherDate);
            """);

        return true;
    }

    /// <summary>
    /// Appends the date format constructors.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the constructors.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateFormatConstructors(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendPublicConvertedValueConstructor(typeDeclaration, "NodaTime.LocalDate", CoreTypes.String, "StandardDateFormat.FormatDate(value)")
            .AppendPublicConvertedValueConstructor(typeDeclaration, "DateTime", CoreTypes.String, "StandardDateFormat.FormatDate(NodaTime.LocalDate.FromDateTime(value))")
            .AppendPublicConvertedValueConstructor(typeDeclaration, "DateTime", "NodaTime.CalendarSystem", CoreTypes.String, "StandardDateFormat.FormatDate(NodaTime.LocalDate.FromDateTime(value1, value2))");
        return true;
    }

    /// <summary>
    /// Appends date format conversions.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the conversions.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateFormatConversionOperators(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendImplicitConversionToType(typeDeclaration, "NodaTime.LocalDate", "value.GetDate()")
            .AppendImplicitConversionFromTypeUsingConstructor(typeDeclaration, "NodaTime.LocalDate", useInForSourceType: true);

        return true;
    }

    /// <summary>
    /// Appends date format public methods.
    /// </summary>
    /// <param name="generator">The generator.</param>
    /// <param name="typeDeclaration">The type declaration for which to append the methods.</param>
    /// <returns>A <see langword="true"/> if this handled the format for the type declaration.</returns>
    public static bool AppendDateFormatPublicMethods(this CodeGenerator generator, TypeDeclaration typeDeclaration)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetDate")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as a <see cref="NodaTime.LocalDate"/>.
                /// </summary>
                /// <returns>The value as a <see cref="NodaTime.LocalDate"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a date.</exception>
                public NodaTime.LocalDate GetDate()
                {
                    if (this.TryGetDate(out NodaTime.LocalDate result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the date value.
                /// </summary>
                /// <param name="result">The value as a <see cref="NodaTime.LocalDate"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a date value from the instance.</returns>
                """)
            .ReserveName("TryGetDate")
            .AppendLineIndent("public bool TryGetDate(out NodaTime.LocalDate result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendConditionalWrappedJsonElementBackingValueKindLineIndent(
                    JsonValueKind.String,
                    "return ",
                    "jsonElementBacking",
                    ".TryGetValue(StandardDateFormat.DateParser, default(object?), out result);")
                .AppendConditionalWrappedBackingValueLineIndent(
                    "Backing.String",
                    "return StandardDateFormat.DateParser(",
                    "stringBacking",
                    ".AsSpan(), default, out result);")
                .AppendSeparatorLine()
                .AppendLineIndent("result = default;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    private static bool AppendCommonUriReferenceFormatPublicMethods(CodeGenerator generator)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetUri")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as a <see cref="Uri"/>.
                /// </summary>
                /// <returns>The value as a <see cref="Uri"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a uri.</exception>
                public Uri GetUri()
                {
                    if (this.TryGetUri(out Uri? result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the uri value as a <see cref="Uri"/>.
                /// </summary>
                /// <param name="result">The value as a <see cref="Uri"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a uri value from the instance.</returns>
                """)
            .ReserveName("TryGetUri")
            .AppendLineIndent("public bool TryGetUri([NotNullWhen(true)] out Uri? result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (StandardUri.TryParseUriReference((string)this, out result)) { return true; }")
                .AppendLineIndent("result = StandardUri.EmptyUri;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }

    private static bool AppendCommonUriFormatPublicMethods(CodeGenerator generator)
    {
        generator
            .AppendSeparatorLine()
            .ReserveNameIfNotReserved("GetUri")
            .AppendBlockIndent(
                """
                /// <summary>
                /// Gets the value as a <see cref="Uri"/>.
                /// </summary>
                /// <returns>The value as a <see cref="Uri"/>.</returns>
                /// <exception cref="InvalidOperationException">The value was not a uri.</exception>
                public Uri GetUri()
                {
                    if (this.TryGetUri(out Uri? result))
                    {
                        return result;
                    }

                    throw new InvalidOperationException();
                }
                """)
            .AppendSeparatorLine()
            .AppendBlockIndent(
                """
                /// <summary>
                /// Try to get the uri value as a <see cref="Uri"/>.
                /// </summary>
                /// <param name="result">The value as a <see cref="Uri"/>.</param>
                /// <returns><see langword="true"/> if it was possible to get a uri value from the instance.</returns>
                """)
            .ReserveName("TryGetUri")
            .AppendLineIndent("public bool TryGetUri([NotNullWhen(true)] out Uri? result)")
            .AppendLineIndent("{")
            .PushIndent()
                .AppendLineIndent("if (StandardUri.TryParseUri((string)this, out result)) { return true; }")
                .AppendLineIndent("result = StandardUri.EmptyUri;")
                .AppendLineIndent("return false;")
            .PopIndent()
            .AppendLineIndent("}");

        return true;
    }
}