// <copyright file="FixedSizeNumericArrayTests.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Json;
using Corvus.Json.Specs.Tests.Infrastructure;
using Drivers;
using Xunit;

namespace Corvus.Json.Specs.Tests.HandWritten;

/// <summary>
/// Tests that fixed-size numeric arrays can be created from spans
/// and round-tripped via TryGetNumericValues.
/// </summary>
public class FixedSizeNumericArrayTests
{
    private const string SchemaTemplate = """
        {
            "title": "A 4x3x2 tensor of {{format}}",
            "type": "array",
            "items": { "$ref": "#/$defs/SecondRank" },
            "minItems": 4,
            "maxItems": 4,
            "$defs": {
                "SecondRank": {
                    "type": "array",
                    "items": { "$ref": "#/$defs/ThirdRank" },
                    "minItems": 3,
                    "maxItems": 3
                },
                "ThirdRank": {
                    "type": "array",
                    "minItems": 2,
                    "maxItems": 2,
                    "items": {
                        "type": "number",
                        "format": "{{format}}"
                    }
                }
            }
        }
        """;

    private const string CorrectData = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23";
    private const string TooFewData = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22";
    private const string TooManyData = "0,1,2,3,4,5,6,7,8,9,10,11,12,13,14,15,16,17,18,19,20,21,22,23,24";

    public static TheoryData<string, string, bool> Draft6Data => BuildTheoryData();

    public static TheoryData<string, string, bool> Draft7Data => BuildTheoryData();

    public static TheoryData<string, string, bool> Draft201909Data => BuildTheoryData();

    public static TheoryData<string, string, bool> Draft202012Data => BuildTheoryData();

    [Theory]
    [MemberData(nameof(Draft6Data))]
    public async Task Draft6_FromValuesRoundTrip(string format, string inputData, bool shouldSucceed)
    {
        await RunFromValuesRoundTrip(DriverFactory.CreateAdditionalDraft6Driver(), format, inputData, shouldSucceed);
    }

    [Theory]
    [MemberData(nameof(Draft7Data))]
    public async Task Draft7_FromValuesRoundTrip(string format, string inputData, bool shouldSucceed)
    {
        await RunFromValuesRoundTrip(DriverFactory.CreateAdditionalDraft7Driver(), format, inputData, shouldSucceed);
    }

    [Theory]
    [MemberData(nameof(Draft201909Data))]
    public async Task Draft201909_FromValuesRoundTrip(string format, string inputData, bool shouldSucceed)
    {
        await RunFromValuesRoundTrip(DriverFactory.CreateAdditionalDraft201909Driver(), format, inputData, shouldSucceed);
    }

    [Theory]
    [MemberData(nameof(Draft202012Data))]
    public async Task Draft202012_FromValuesRoundTrip(string format, string inputData, bool shouldSucceed)
    {
        await RunFromValuesRoundTrip(DriverFactory.CreateAdditionalDraft202012Driver(), format, inputData, shouldSucceed);
    }

    private static TheoryData<string, string, bool> BuildTheoryData()
    {
        string[] formats = ["sbyte", "int16", "int32", "int64", "byte", "uint16", "uint32", "uint64", "double", "decimal", "single"];
        var data = new TheoryData<string, string, bool>();
        foreach (string format in formats)
        {
            data.Add(format, CorrectData, true);
            data.Add(format, TooFewData, false);
            data.Add(format, TooManyData, false);
        }

        return data;
    }

    private static async Task RunFromValuesRoundTrip(JsonSchemaBuilderDriver driver, string format, string inputData, bool shouldSucceed)
    {
        using (driver)
        {
            string schema = SchemaTemplate.Replace("{{format}}", format);
            Type generatedType = await driver.GenerateTypeForVirtualFile(
                schema,
                $"fixed-size-numeric-arrays.{format}.json",
                "FixedSizeNumericArrays",
                $"FromValues{format}",
                validateFormat: true,
                optionalAsNullable: false,
                useImplicitOperatorString: false);

            if (shouldSucceed)
            {
                IJsonValue instance = CreateFromValues(generatedType, format, inputData);
                bool roundTripsCorrectly = CompareWithTryGetValues(generatedType, instance, format, inputData);
                Assert.True(roundTripsCorrectly);
            }
            else
            {
                Assert.ThrowsAny<Exception>(() => CreateFromValues(generatedType, format, inputData));
            }
        }
    }

    private static IJsonValue CreateFromValues(Type generatedType, string format, string inputData)
    {
        return format switch
        {
            "sbyte" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<sbyte>(inputData, sbyte.Parse)),
            "int16" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<short>(inputData, short.Parse)),
            "int32" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<int>(inputData, int.Parse)),
            "int64" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<long>(inputData, long.Parse)),
            "byte" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<byte>(inputData, byte.Parse)),
            "uint16" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<ushort>(inputData, ushort.Parse)),
            "uint32" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<uint>(inputData, uint.Parse)),
            "uint64" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<ulong>(inputData, ulong.Parse)),
            "double" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<double>(inputData, double.Parse)),
            "decimal" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<decimal>(inputData, decimal.Parse)),
            "single" => JsonSchemaBuilderDriver.CreateInstanceOfNumericArrayFromValues(generatedType, ParseArray<float>(inputData, float.Parse)),
            _ => throw new InvalidOperationException($"Unsupported format: {format}"),
        };
    }

    private static bool CompareWithTryGetValues(Type generatedType, IJsonValue instance, string format, string inputData)
    {
        return format switch
        {
            "sbyte" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<sbyte>(inputData, sbyte.Parse)),
            "int16" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<short>(inputData, short.Parse)),
            "int32" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<int>(inputData, int.Parse)),
            "int64" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<long>(inputData, long.Parse)),
            "byte" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<byte>(inputData, byte.Parse)),
            "uint16" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<ushort>(inputData, ushort.Parse)),
            "uint32" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<uint>(inputData, uint.Parse)),
            "uint64" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<ulong>(inputData, ulong.Parse)),
            "double" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<double>(inputData, double.Parse)),
            "decimal" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<decimal>(inputData, decimal.Parse)),
            "single" => JsonSchemaBuilderDriver.CompareInstanceOfNumericArrayWithValues(generatedType, instance, ParseArray<float>(inputData, float.Parse)),
            _ => throw new InvalidOperationException($"Unsupported format: {format}"),
        };
    }

    private static T[] ParseArray<T>(string csv, Func<string, T> parser)
    {
        return csv.Split(',').Select(parser).ToArray();
    }
}