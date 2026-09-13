// <copyright file="JsonSchemaEvaluation.Messages.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Internal;

/// <summary>
/// UTF-8 message text for the results collector, materialised once per process on first use.
/// </summary>
/// <remarks>
/// A message provider runs every time a result is written (every keyword and child context in verbose mode),
/// so fetching the resource string and transcoding it on each call dominated collecting-mode evaluation.
/// The text is resolved for the culture current at first use and then reused for the life of the process.
/// </remarks>
public static partial class JsonSchemaEvaluation
{
    private static byte[]? s_JsonSchema_DidNotMatchAllSchema;
    private static byte[]? s_JsonSchema_DidNotMatchAtLeastOneConstantValue;
    private static byte[]? s_JsonSchema_DidNotMatchAtLeastOneSchema;
    private static byte[]? s_JsonSchema_DidNotMatchElse;
    private static byte[]? s_JsonSchema_DidNotMatchNotSchema;
    private static byte[]? s_JsonSchema_DidNotMatchThen;
    private static byte[]? s_JsonSchema_ElseWithoutIf;
    private static byte[]? s_JsonSchema_EvaluatedSubschema;
    private static byte[]? s_JsonSchema_ExpectedBase64Content;
    private static byte[]? s_JsonSchema_ExpectedBase64String;
    private static byte[]? s_JsonSchema_ExpectedBooleanFalseValue;
    private static byte[]? s_JsonSchema_ExpectedBooleanTrueValue;
    private static byte[]? s_JsonSchema_ExpectedConstantValue;
    private static byte[]? s_JsonSchema_ExpectedContainsCountEquals;
    private static byte[]? s_JsonSchema_ExpectedContainsCountGreaterThan;
    private static byte[]? s_JsonSchema_ExpectedContainsCountGreaterThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedContainsCountLessThan;
    private static byte[]? s_JsonSchema_ExpectedContainsCountLessThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedContainsCountNotEquals;
    private static byte[]? s_JsonSchema_ExpectedEmail;
    private static byte[]? s_JsonSchema_ExpectedEquals;
    private static byte[]? s_JsonSchema_ExpectedGreaterThan;
    private static byte[]? s_JsonSchema_ExpectedGreaterThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedHostname;
    private static byte[]? s_JsonSchema_ExpectedIPV4;
    private static byte[]? s_JsonSchema_ExpectedIPV6;
    private static byte[]? s_JsonSchema_ExpectedIdnEmail;
    private static byte[]? s_JsonSchema_ExpectedIdnHostname;
    private static byte[]? s_JsonSchema_ExpectedIri;
    private static byte[]? s_JsonSchema_ExpectedIriReference;
    private static byte[]? s_JsonSchema_ExpectedIso8601Date;
    private static byte[]? s_JsonSchema_ExpectedIso8601Duration;
    private static byte[]? s_JsonSchema_ExpectedIso8601OffsetDateTime;
    private static byte[]? s_JsonSchema_ExpectedIso8601OffsetTime;
    private static byte[]? s_JsonSchema_ExpectedItemCountEquals;
    private static byte[]? s_JsonSchema_ExpectedItemCountGreaterThan;
    private static byte[]? s_JsonSchema_ExpectedItemCountGreaterThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedItemCountLessThan;
    private static byte[]? s_JsonSchema_ExpectedItemCountLessThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedItemCountNotEquals;
    private static byte[]? s_JsonSchema_ExpectedJsonContent;
    private static byte[]? s_JsonSchema_ExpectedJsonPointer;
    private static byte[]? s_JsonSchema_ExpectedLengthEquals;
    private static byte[]? s_JsonSchema_ExpectedLengthGreaterThan;
    private static byte[]? s_JsonSchema_ExpectedLengthGreaterThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedLengthLessThan;
    private static byte[]? s_JsonSchema_ExpectedLengthLessThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedLengthNotEquals;
    private static byte[]? s_JsonSchema_ExpectedLessThan;
    private static byte[]? s_JsonSchema_ExpectedLessThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedMatchPatternPropertySchema;
    private static byte[]? s_JsonSchema_ExpectedMatchRegularExpression;
    private static byte[]? s_JsonSchema_ExpectedMatchesDependentSchema;
    private static byte[]? s_JsonSchema_ExpectedMultipleOf;
    private static byte[]? s_JsonSchema_ExpectedNotEquals;
    private static byte[]? s_JsonSchema_ExpectedNullValue;
    private static byte[]? s_JsonSchema_ExpectedNumberFormat;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountEquals;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountGreaterThan;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountGreaterThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountLessThan;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountLessThanOrEquals;
    private static byte[]? s_JsonSchema_ExpectedPropertyCountNotEquals;
    private static byte[]? s_JsonSchema_ExpectedPropertyMatchesFallbackSchema;
    private static byte[]? s_JsonSchema_ExpectedPropertyNameMatchesRegularExpression;
    private static byte[]? s_JsonSchema_ExpectedPropertyNameMatchesSchema;
    private static byte[]? s_JsonSchema_ExpectedRegex;
    private static byte[]? s_JsonSchema_ExpectedRelativeJsonPointer;
    private static byte[]? s_JsonSchema_ExpectedStringEquals;
    private static byte[]? s_JsonSchema_ExpectedType;
    private static byte[]? s_JsonSchema_ExpectedUniqueItems;
    private static byte[]? s_JsonSchema_ExpectedUri;
    private static byte[]? s_JsonSchema_ExpectedUriReference;
    private static byte[]? s_JsonSchema_ExpectedUriTemplate;
    private static byte[]? s_JsonSchema_ExpectedUuid;
    private static byte[]? s_JsonSchema_IgnoredFormatNotAsserted;
    private static byte[]? s_JsonSchema_IgnoredNotType;
    private static byte[]? s_JsonSchema_IgnoredUnrecognizedFormat;
    private static byte[]? s_JsonSchema_MatchedAllSchema;
    private static byte[]? s_JsonSchema_MatchedAtLeastOneConstantValue;
    private static byte[]? s_JsonSchema_MatchedAtLeastOneSchema;
    private static byte[]? s_JsonSchema_MatchedElse;
    private static byte[]? s_JsonSchema_MatchedExactlyOneSchema;
    private static byte[]? s_JsonSchema_MatchedIfForElse;
    private static byte[]? s_JsonSchema_MatchedIfForThen;
    private static byte[]? s_JsonSchema_MatchedMoreThanOneSchema;
    private static byte[]? s_JsonSchema_MatchedNoSchema;
    private static byte[]? s_JsonSchema_MatchedNotSchema;
    private static byte[]? s_JsonSchema_MatchedThen;
    private static byte[]? s_JsonSchema_RequiredPropertyNotPresent;
    private static byte[]? s_JsonSchema_RequiredPropertyPresent;
    private static byte[]? s_JsonSchema_ThenWithoutIf;

    private static byte[] Utf8(string text)
    {
        return System.Text.Encoding.UTF8.GetBytes(text);
    }

    private static bool TryCopyUtf8(byte[] utf8, Span<byte> buffer, out int written)
    {
        if (utf8.Length > buffer.Length)
        {
            written = 0;
            return false;
        }

        utf8.CopyTo(buffer);
        written = utf8.Length;
        return true;
    }
}