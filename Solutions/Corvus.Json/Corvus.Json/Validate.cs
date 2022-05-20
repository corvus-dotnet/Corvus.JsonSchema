﻿// <copyright file="Validate.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json
{
    using System;
    using System.Globalization;
    using System.Net;
    using System.Text.Json;
    using System.Text.RegularExpressions;
    using Corvus.Extensions;

    /// <summary>
    /// JsonSchema validation errors.
    /// </summary>
    public static class Validate
    {
        private static readonly Regex DurationPattern = new (@"^P(?!$)((\d+(?:\.\d+)?Y)?(\d+(?:\.\d+)?M)?|(\d+(?:\.\d+)?W)?)?(\d+(?:\.\d+)?D)?(T(?=\d)(\d+(?:\.\d+)?H)?(\d+(?:\.\d+)?M)?(\d+(?:\.\d+)?S)?)?$", RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.ECMAScript); // ECMAScript mode stops \d matching non-ASCII digits
        private static readonly Regex EmailPattern = new (@"^(?:[a-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[a-z0-9!#$%&'*+/=?^_`{|}~-]+)*|""(?:[ \x01-\x08\x0b\x0c\x0e-\x1f\x21\x23-\x5b\x5d-\x7f]|\\[\x01-\x09\x0b\x0c\x0e-\x7f])*"")@(?:(?:[a-z0-9](?:[a-z0-9-]*[a-z0-9])?\.)+[a-z0-9](?:[a-z0-9-]*[a-z0-9])?|\[(((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|IPv6:(([0-9a-fA-F]{1,4}:){7,7}[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,7}:|([0-9a-fA-F]{1,4}:){1,6}:[0-9a-fA-F]{1,4}|([0-9a-fA-F]{1,4}:){1,5}(:[0-9a-fA-F]{1,4}){1,2}|([0-9a-fA-F]{1,4}:){1,4}(:[0-9a-fA-F]{1,4}){1,3}|([0-9a-fA-F]{1,4}:){1,3}(:[0-9a-fA-F]{1,4}){1,4}|([0-9a-fA-F]{1,4}:){1,2}(:[0-9a-fA-F]{1,4}){1,5}|[0-9a-fA-F]{1,4}:((:[0-9a-fA-F]{1,4}){1,6})|:((:[0-9a-fA-F]{1,4}){1,7}|:)|fe80:(:[0-9a-fA-F]{0,4}){0,4}%[0-9a-zA-Z]{1,}|::(ffff(:0{1,4}){0,1}:){0,1}((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])|([0-9a-fA-F]{1,4}:){1,4}:((25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])\.){3,3}(25[0-5]|(2[0-4]|1{0,1}[0-9]){0,1}[0-9])))\])$", RegexOptions.Compiled);
        private static readonly Regex HostnamePattern = new ("^(?=.{1,255}$)((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?(\\.((?!_)\\w)((((?!_)\\w)|\\b-){0,61}((?!_)\\w))?)*\\.?$", RegexOptions.Compiled);
        private static readonly Regex InvalidIdnHostnamePattern = new (@"(^[\p{Mn}\p{Mc}\p{Me}\u302E\u00b7])|.*\u302E.*|.*[^l]\u00b7.*|.*\u00b7[^l].*|.*\u00b7$|\u0374$|\u0375$|\u0374[^\p{IsGreekandCoptic}]|\u0375[^\p{IsGreekandCoptic}]|^\u05F3|[^\p{IsHebrew}]\u05f3|^\u05f4|[^\p{IsHebrew}]\u05f4|[\u0660-\u0669][\u06F0-\u06F9]|[\u06F0-\u06F9][\u0660-\u0669]|^\u200D|[^\uA953\u094d\u0acd\u0c4d\u0d3b\u09cd\u0a4d\u0b4d\u0bcd\u0ccd\u0d4d\u1039\u0d3c\u0eba\ua8f3\ua8f4]\u200D|^\u30fb$|[^\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]\u30fb|\u30fb[^\p{IsHiragana}\p{IsKatakana}\p{IsCJKUnifiedIdeographs}]|[\u0640\u07fa\u3031\u3032\u3033\u3034\u3035\u302e\u302f\u303b]|..--", RegexOptions.Compiled);
        private static readonly Regex IpV4Pattern = new ("^(?:(?:^|\\.)(?:2(?:5[0-5]|[0-4]\\d)|1?\\d?\\d)){4}$", RegexOptions.Compiled);
        private static readonly Regex ZoneIdExpression = new ("%.*$", RegexOptions.Compiled);
        private static readonly Regex JsonPointerPattern = new ("^((/(([^/~])|(~[01]))*))*$", RegexOptions.Compiled);
        private static readonly Regex RelativeJsonPointerPattern = new ("^(0|[1-9]+)(#|(/(/|[^/~]|(~[01]))*))?$", RegexOptions.Compiled);
        private static readonly Regex UriTemplatePattern = new (@"^([^\x00-\x20\x7f""'%<>\\^`{|}]|%[0-9A-Fa-f]{2}|{[+#./;?&=,!@|]?((\w|%[0-9A-Fa-f]{2})(\.?(\w|%[0-9A-Fa-f]{2}))*(:[1-9]\d{0,3}|\*)?)(,((\w|%[0-9A-Fa-f]{2})(\.?(\w|%[0-9A-Fa-f]{2}))*(:[1-9]\d{0,3}|\*)?))*})*$", RegexOptions.Compiled);
        private static readonly Regex UuidTemplatePattern = new (@"[0-9a-fA-F]{8}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{4}\-[0-9a-fA-F]{12}", RegexOptions.Compiled);

        private static readonly IdnMapping IdnMapping = new () { AllowUnassigned = true, UseStd3AsciiRules = true };

        /// <summary>
        /// Validate a string type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeString(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'string'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate a number type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeNumber(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Number)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'number'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate a null type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeNull(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Null)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'null' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'null'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'null'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate an undefined type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeUndefined(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Null)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'undefined' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'undefined'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'undefined'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format integer.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeInteger<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.Number)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' with zero fractional part but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number' with zero fractional part.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else
            {
                double value = instance.AsNumber();
                if (value != Math.Floor(value))
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'number' with zero fractional part but was '{valueKind}' with fractional part {value - Math.Floor(value)}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'number' with zero fractional part.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'number' with zero fractional part.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format regex.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeRegex<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'regex' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'regex'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonRegex regexInstance;
            if (instance.GetType() == typeof(JsonRegex))
            {
                regexInstance = CastTo<JsonRegex>.From(instance);
            }
            else
            {
                regexInstance = instance.AsString().As<JsonRegex>();
            }

            if (!regexInstance.TryGetRegex(out Regex _))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'regex' but was '{regexInstance.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'regex'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'regex'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format iri-reference.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri-reference' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'iri-reference'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonIriReference iri;
            if (instance.GetType() == typeof(JsonIriReference))
            {
                iri = CastTo<JsonIriReference>.From(instance);
            }
            else
            {
                iri = instance.AsString().As<JsonIriReference>();
            }

            if (!iri.TryGetUri(out Uri? uri) ||
                uri.OriginalString.StartsWith("\\\\") ||
                (uri.IsAbsoluteUri && uri.Fragment.Contains('\\')) ||
                (uri.OriginalString.StartsWith('#') && uri.OriginalString.Contains('\\')))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri-reference' but was '{iri.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'iri-reference'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'iri-reference'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format json-pointer.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeJsonPointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'json-pointer' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'json-pointer'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString jsonPointer = instance.AsString();
            if (!JsonPointerPattern.IsMatch(jsonPointer))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'json-pointer' but was '{jsonPointer.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'json-pointer'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'json-pointer'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format relative-json-pointer.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeRelativeJsonPointer<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString jsonPointer = instance.AsString();
            if (!RelativeJsonPointerPattern.IsMatch(jsonPointer))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'relative-json-pointer' but was '{jsonPointer.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'relative-json-pointer'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'relative-json-pointer'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format iri.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'iri'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonIri iri;
            if (instance.GetType() == typeof(JsonIri))
            {
                iri = CastTo<JsonIri>.From(instance);
            }
            else
            {
                iri = instance.AsString().As<JsonIri>();
            }

            if (!iri.TryGetUri(out Uri? uri) || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'iri' but was '{iri.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'iri'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'iri'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format uri.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeUri<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonUri uriInstance;
            if (instance.GetType() == typeof(JsonUri))
            {
                uriInstance = CastTo<JsonUri>.From(instance);
            }
            else
            {
                uriInstance = instance.AsString().As<JsonUri>();
            }

            if (!(uriInstance.TryGetUri(out Uri? testUri) && (!testUri.IsAbsoluteUri || !testUri.IsUnc)))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri' but was '{uriInstance.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format uri-reference.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeUriReference<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-reference' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri-reference'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonUriReference uriReferenceInstance;
            if (instance.GetType() == typeof(JsonUriReference))
            {
                uriReferenceInstance = CastTo<JsonUriReference>.From(instance);
            }
            else
            {
                uriReferenceInstance = instance.AsString().As<JsonUriReference>();
            }

            if (!uriReferenceInstance.TryGetUri(out Uri? uri) ||
                uri.OriginalString.StartsWith("\\\\") ||
                (uri.IsAbsoluteUri && uri.Fragment.Contains('\\')) ||
                (uri.OriginalString.StartsWith('#') && uri.OriginalString.Contains('\\')))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-reference' but was '{uriReferenceInstance.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri-reference'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri-reference'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format time.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'date'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonTime time;
            if (instance.GetType() == typeof(JsonTime))
            {
                time = CastTo<JsonTime>.From(instance);
            }
            else
            {
                time = instance.AsString().As<JsonTime>();
            }

            if (!time.TryGetTime(out _))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'time' but was '{time.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'time'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'time'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format date.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeDate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'date'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonDate date;
            if (instance.GetType() == typeof(JsonDate))
            {
                date = CastTo<JsonDate>.From(instance);
            }
            else
            {
                date = instance.AsString().As<JsonDate>();
            }

            if (!date.TryGetDate(out _))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'date' but was '{date.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'date'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'date'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format email.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'email' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'email'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString email = instance.AsString();
            if (!EmailPattern.IsMatch(email))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'email', but was '{email.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'email'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'email'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format ipv6.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIpV6<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv6' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'ipv6'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonIpV6 ipv6 = instance.As<JsonIpV6>();

            if (ipv6.TryGetIPAddress(out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 &&
                !ZoneIdExpression.IsMatch(ipv6))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext
                        .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'ipv6'.");
                }

                return validationContext;
            }
            else
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv6', but was '{ipv6.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'ipv6'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        /// <summary>
        /// Validates the format uri-template.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeUriTemplate<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-template' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uri-template'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString uriTemplate = instance.AsString();

            if (!UriTemplatePattern.IsMatch(uriTemplate))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uri-template', but was '{uriTemplate}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uri-template'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uri-template'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format ipv4.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIpV4<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv4' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'ipv4'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonIpV4 ipv4 = instance.As<JsonIpV4>();

            if (ipv4.TryGetIPAddress(out IPAddress? address) &&
                address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork &&
                IpV4Pattern.IsMatch(ipv4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext
                        .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'ipv4'.");
                }

                return validationContext;
            }
            else
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'ipv4', but was '{ipv4.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'ipv4'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
        }

        /// <summary>
        /// Validates the format idn-email.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIdnEmail<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-email' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'idn-email'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString email = instance.AsString();

            bool isMatch = false;

            try
            {
                // Normalize the domain
                email = Regex.Replace(email, @"(@)(.+)$", DomainMapper, RegexOptions.None, TimeSpan.FromMilliseconds(200));
                try
                {
                    isMatch = Regex.IsMatch(
                        email,
                        @"^[^@\s]+@[^@\s]+\.[^@\s]+$",
                        RegexOptions.IgnoreCase,
                        TimeSpan.FromMilliseconds(250));
                }
                catch (RegexMatchTimeoutException)
                {
                    isMatch = false;
                }

                // Examines the domain part of the email and normalizes it.
                static string DomainMapper(Match match)
                {
                    // Use IdnMapping class to convert Unicode domain names.
                    var idn = new IdnMapping();

                    // Pull out and process domain name (throws ArgumentException on invalid)
                    string domainName = idn.GetAscii(match.Groups[2].Value);

                    return match.Groups[1].Value + domainName;
                }
            }
            catch (RegexMatchTimeoutException)
            {
                isMatch = false;
            }
            catch (ArgumentException)
            {
                isMatch = false;
            }

            if (!isMatch)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-email', but was '{email.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'idn-email'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'idn-email'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format idn-hostname.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeIdnHostname<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-hostname' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'idn-hostname'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            string value = instance.AsString();

            bool isMatch;
            if (value.StartsWith("xn--"))
            {
                string decodedValue;

                try
                {
                    decodedValue = IdnMapping.GetUnicode(value);
                    isMatch = !InvalidIdnHostnamePattern.IsMatch(decodedValue);
                }
                catch (ArgumentException)
                {
                    isMatch = false;
                }
            }
            else
            {
                try
                {
                    IdnMapping.GetAscii(value);
                    isMatch = !InvalidIdnHostnamePattern.IsMatch(value);
                }
                catch (ArgumentException)
                {
                    isMatch = false;
                }
            }

            if (!isMatch)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'idn-hostname', but was '{value}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'idn-hostname'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'idn-hostname'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format hostname.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeHostname<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'hostname' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'hostname'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            string value = instance.AsString();

            bool isMatch;
            if (value.StartsWith("xn--"))
            {
                string decodedValue;

                try
                {
                    decodedValue = IdnMapping.GetUnicode(value);
                    isMatch = HostnamePattern.IsMatch(decodedValue);
                }
                catch (ArgumentException)
                {
                    isMatch = false;
                }
            }
            else
            {
                isMatch = HostnamePattern.IsMatch(value);
            }

            if (!isMatch)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'hostname', but was '{value}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'hostname'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'hostname'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format uuid.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeUuid<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uuid' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'uuid'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (!UuidTemplatePattern.IsMatch(instance.AsString()))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'uuid', but was '{instance.AsString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'uuid'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'uuid'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format duration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeDuration<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'duration' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'duration'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonString date = instance.AsString();
            if (!DurationPattern.IsMatch(date))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'duration', but was '{date.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'duration'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'duration'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validates the format datetime.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="instance">The instance to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeDateTime<T>(in T instance, in ValidationContext validationContext, ValidationLevel level)
            where T : struct, IJsonValue
        {
            JsonValueKind valueKind = instance.ValueKind;
            if (valueKind != JsonValueKind.String)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'datetime' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'string' with format 'datetime'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            JsonDateTime date;
            if (instance.GetType() == typeof(JsonDateTime))
            {
                date = CastTo<JsonDateTime>.From(instance);
            }
            else
            {
                date = instance.AsString().As<JsonDateTime>();
            }

            if (!date.TryGetDateTime(out _))
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'string' with format 'datetime' but was '{date.GetString()}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been a 'string' with format 'datetime'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }

            if (level == ValidationLevel.Verbose)
            {
                return validationContext
                    .WithResult(isValid: true, "Validation 6.1.1 type - was a 'string' with format 'datetime'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate an array type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeArray(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Array)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'array' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'array'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'array'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate a boolean type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeBoolean(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.True && valueKind != JsonValueKind.False)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'boolean' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'boolean'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'boolean'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate an object type value.
        /// </summary>
        /// <param name="valueKind">The actual value kind.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext TypeObject(JsonValueKind valueKind, in ValidationContext validationContext, ValidationLevel level)
        {
            if (valueKind != JsonValueKind.Object)
            {
                if (level >= ValidationLevel.Detailed)
                {
                    return validationContext.WithResult(isValid: false, $"Validation 6.1.1 type - should have been 'object' but was '{valueKind}'.");
                }
                else if (level >= ValidationLevel.Basic)
                {
                    return validationContext.WithResult(isValid: false, "Validation 6.1.1 type - should have been 'object'.");
                }
                else
                {
                    return validationContext.WithResult(isValid: false);
                }
            }
            else if (level == ValidationLevel.Verbose)
            {
                return validationContext.WithResult(isValid: true, "Validation 6.1.1 type - was 'object'.");
            }

            return validationContext;
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <param name="enum4">The fourth enumeration value.</param>
        /// <param name="enum5">The fifth enumeration value.</param>
        /// <param name="enum6">The sixth enumeration value.</param>
        /// <param name="enum7">The seventh enumeration value.</param>
        /// <param name="enum8">The eighth enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6, in T enum7, in T enum8)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum5))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum6))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum7))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum8))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum8}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}, {enum8}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <param name="enum4">The fourth enumeration value.</param>
        /// <param name="enum5">The fifth enumeration value.</param>
        /// <param name="enum6">The sixth enumeration value.</param>
        /// <param name="enum7">The seventh enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6, in T enum7)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum5))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum6))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum7))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum7}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}, {enum7}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <param name="enum4">The fourth enumeration value.</param>
        /// <param name="enum5">The fifth enumeration value.</param>
        /// <param name="enum6">The sixth enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5, in T enum6)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum5))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum6))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum6}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}, {enum6}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <param name="enum4">The fourth enumeration value.</param>
        /// <param name="enum5">The fifth enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4, in T enum5)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum5))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum5}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}, {enum5}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <param name="enum4">The fourth enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3, in T enum4)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum4))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum4}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}, {enum4}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <param name="enum3">The third enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2, in T enum3)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum3))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum3}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}, {enum3}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <param name="enum2">The second enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1, in T enum2)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (value.Equals(enum2))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum2}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}, {enum2}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enum1">The first enumeration value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T enum1)
            where T : struct, IJsonValue
        {
            if (value.Equals(enum1))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum - '{value}' matched '{enum1}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum - '{value}' did not match any of the required values: [{enum1}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate an enumeration.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="enums">The enumeration values.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateEnum<T>(in T value, in ValidationContext validationContext, ValidationLevel level, params T[] enums)
            where T : struct, IJsonValue
        {
            foreach (T enumValue in enums)
            {
                if (value.Equals(enumValue))
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        return validationContext.WithResult(isValid: true, $"Validation 6.1.2 enum -  '{value}' matched '{enumValue}'.");
                    }

                    return validationContext;
                }
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.2 enum -  '{value}' did not match any of the required values: [{string.Join(", ", enums)}].");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.2 enum - did not match any of the required values.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Validate a const value.
        /// </summary>
        /// <typeparam name="T">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="constValue">The const value.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateConst<T>(in T value, in ValidationContext validationContext, ValidationLevel level, in T constValue)
            where T : struct, IJsonValue
        {
            if (value.Equals(constValue))
            {
                if (level == ValidationLevel.Verbose)
                {
                    return validationContext.WithResult(isValid: true, $"Validation 6.1.3 const - '{value}' matched '{constValue}'.");
                }

                return validationContext;
            }

            if (level >= ValidationLevel.Detailed)
            {
                return validationContext.WithResult(isValid: false, $"Validation 6.1.3 const - '{value}' did not match the required value: {constValue}.");
            }
            else if (level >= ValidationLevel.Basic)
            {
                return validationContext.WithResult(isValid: false, "Validation 6.1.3 const - did not match the required value.");
            }
            else
            {
                return validationContext.WithResult(isValid: false);
            }
        }

        /// <summary>
        /// Perform numeric validation on the value.
        /// </summary>
        /// <typeparam name="TValue">The type of <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The current validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="multipleOf">The optional multiple-of validation.</param>
        /// <param name="maximum">The optional maximum validation.</param>
        /// <param name="exclusiveMaximum">The optional exclusive maximum validation.</param>
        /// <param name="minimum">The optional minimum validation.</param>
        /// <param name="exclusiveMinimum">The optional exclusive minimum validation.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateNumber<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, double? multipleOf, double? maximum, double? exclusiveMaximum, double? minimum, double? exclusiveMinimum)
            where TValue : struct, IJsonValue
        {
            if (value.ValueKind != JsonValueKind.Number)
            {
                if (level == ValidationLevel.Verbose)
                {
                    ValidationContext ignoredResult = validationContext;
                    if (multipleOf is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.1 multipleOf - ignored because the value is not a number");
                    }

                    if (maximum is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.2 maximum- ignored because the value is not a number");
                    }

                    if (exclusiveMaximum is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.3 exclusiveMaximum - ignored because the value is not a number");
                    }

                    if (minimum is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.4 minimum - ignored because the value is not a number");
                    }

                    if (exclusiveMinimum is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.2.5 exclusiveMinimum - ignored because the value is not a number");
                    }

                    return ignoredResult;
                }

                return validationContext;
            }

            ValidationContext result = validationContext;

            double currentValue = value.AsNumber();

            if (multipleOf is double mo)
            {
                if (Math.Abs(Math.IEEERemainder(currentValue, mo)) <= 1.0E-18)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.1 multipleOf -  {currentValue} was a multiple of {mo}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.1 multipleOf -  {currentValue} was not a multiple of {mo}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.1 multipleOf - was not a multiple of the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (maximum is double max)
            {
                if (currentValue <= max)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.2 maximum -  {currentValue} was less than or equal to {max}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.2 maximum -  {currentValue} was greater than {max}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.2 maximum - was greater than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMaximum is double emax)
            {
                if (currentValue < emax)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was less than {emax}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.3 exclusiveMaximum -  {currentValue} was greater than or equal to {emax}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.3 exclusiveMaximum - was greater than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (minimum is double min)
            {
                if (currentValue >= min)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.4 minimum -  {currentValue} was greater than or equal to {min}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.4 minimum - {currentValue} was less than {min}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.4 minimum - was less than the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            if (exclusiveMinimum is double emin)
            {
                if (currentValue > emin)
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was greater than {emin}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.2.5 exclusiveMinimum -  {currentValue} was less than or equal to {emin}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.2.5 exclusiveMinimum - was less than or equal to the required value.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Validates a string value.
        /// </summary>
        /// <typeparam name="TValue">The type of the <see cref="IJsonValue"/> to validate.</typeparam>
        /// <param name="value">The instance to validate.</param>
        /// <param name="validationContext">The current validation context.</param>
        /// <param name="level">The validation level.</param>
        /// <param name="maxLength">The optional maxLength validation.</param>
        /// <param name="minLength">The optional minLenth validation.</param>
        /// <param name="pattern">The optional pattern validation.</param>
        /// <returns>The updated validation context.</returns>
        public static ValidationContext ValidateString<TValue>(in TValue value, in ValidationContext validationContext, ValidationLevel level, int? maxLength, int? minLength, Regex? pattern)
            where TValue : struct, IJsonValue
        {
            if (value.ValueKind != JsonValueKind.String)
            {
                if (level == ValidationLevel.Verbose)
                {
                    ValidationContext ignoredResult = validationContext;
                    if (maxLength is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.1 maxLength - ignored because the value is not a string");
                    }

                    if (minLength is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.2 minLength - ignored because the value is not a string");
                    }

                    if (pattern is not null)
                    {
                        ignoredResult = ignoredResult.WithResult(isValid: true, "Validation 6.3.3 pattern - ignored because the value is not a string");
                    }

                    return ignoredResult;
                }

                return validationContext;
            }

            ValidationContext result = validationContext;

            string stringValue = value.AsString();

            if (maxLength is not null || minLength is not null)
            {
                int length = 0;
                TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(stringValue);
                while (enumerator.MoveNext())
                {
                    length++;
                }

                if (maxLength is int maxl)
                {
                    if (length <= maxl)
                    {
                        if (level == ValidationLevel.Verbose)
                        {
                            result = result.WithResult(isValid: true, $"Validation 6.3.1 maxLength - {stringValue} of {length} was less than or equal to {maxl}.");
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Detailed)
                        {
                            result = result.WithResult(isValid: false, $"Validation 6.3.1 maxLength - {stringValue} of {length} was greater than {maxl}.");
                        }
                        else if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation 6.3.1 maxLength - was greater than the required length.");
                        }
                        else
                        {
                            return validationContext.WithResult(isValid: false);
                        }
                    }
                }

                if (minLength is int minl)
                {
                    if (length >= minl)
                    {
                        if (level == ValidationLevel.Verbose)
                        {
                            result = result.WithResult(isValid: true, $"Validation 6.3.2 minLength - {stringValue} of {length} was greater than or equal to {minl}.");
                        }
                    }
                    else
                    {
                        if (level >= ValidationLevel.Detailed)
                        {
                            result = result.WithResult(isValid: false, $"Validation 6.3.2 minLength - {stringValue} of {length} was less than {minl}.");
                        }
                        else if (level >= ValidationLevel.Basic)
                        {
                            result = result.WithResult(isValid: false, "Validation 6.3.2 minLength - was less than the required length.");
                        }
                        else
                        {
                            return validationContext.WithResult(isValid: false);
                        }
                    }
                }
            }

            if (pattern is Regex prex)
            {
                if (prex.IsMatch(stringValue))
                {
                    if (level == ValidationLevel.Verbose)
                    {
                        result = result.WithResult(isValid: true, $"Validation 6.3.3 pattern - {stringValue} matched {prex}.");
                    }
                }
                else
                {
                    if (level >= ValidationLevel.Detailed)
                    {
                        result = result.WithResult(isValid: false, $"Validation 6.3.3 pattern - {stringValue} did not match {prex}.");
                    }
                    else if (level >= ValidationLevel.Basic)
                    {
                        result = result.WithResult(isValid: false, "Validation 6.3.13 pattern - did not match the required pattern.");
                    }
                    else
                    {
                        return validationContext.WithResult(isValid: false);
                    }
                }
            }

            return result;
        }
    }
}