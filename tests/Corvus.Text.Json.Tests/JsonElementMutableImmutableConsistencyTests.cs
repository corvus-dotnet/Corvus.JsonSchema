// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Collections.Generic;
using Xunit;

namespace Corvus.Text.Json.Tests;
/// <summary>
/// Tests for mutable/immutable consistency in JsonElement Get*() methods.
/// </summary>
public class JsonElementMutableImmutableConsistencyTests
{
    #region Numeric Consistency Tests

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetByte_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare byte values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                byte immutableResult = immutableElement.GetByte();
                byte mutableResult = mutableElement.GetByte();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetByte());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetSByte_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare sbyte values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                sbyte immutableResult = immutableElement.GetSByte();
                sbyte mutableResult = mutableElement.GetSByte();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetSByte());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetInt16_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare int16 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                short immutableResult = immutableElement.GetInt16();
                short mutableResult = mutableElement.GetInt16();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetInt16());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetUInt16_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare uint16 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                ushort immutableResult = immutableElement.GetUInt16();
                ushort mutableResult = mutableElement.GetUInt16();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetUInt16());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetInt32_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare int32 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                int immutableResult = immutableElement.GetInt32();
                int mutableResult = mutableElement.GetInt32();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetInt32());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetUInt32_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare uint32 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                uint immutableResult = immutableElement.GetUInt32();
                uint mutableResult = mutableElement.GetUInt32();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetUInt32());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetInt64_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare int64 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                long immutableResult = immutableElement.GetInt64();
                long mutableResult = mutableElement.GetInt64();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetInt64());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetUInt64_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare uint64 values (if in range)
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                ulong immutableResult = immutableElement.GetUInt64();
                ulong mutableResult = mutableElement.GetUInt64();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetUInt64());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetSingle_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare single values
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                float immutableResult = immutableElement.GetSingle();
                float mutableResult = mutableElement.GetSingle();
                
                // Handle special float values
                if (float.IsNaN(immutableResult))
                {
                    Assert.True(float.IsNaN(mutableResult));
                }
                else if (float.IsInfinity(immutableResult))
                {
                    Assert.Equal(immutableResult, mutableResult);
                }
                else
                {
                    Assert.Equal(immutableResult, mutableResult);
                }
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetSingle());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetDouble_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare double values
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                double immutableResult = immutableElement.GetDouble();
                double mutableResult = mutableElement.GetDouble();
                
                // Handle special double values
                if (double.IsNaN(immutableResult))
                {
                    Assert.True(double.IsNaN(mutableResult));
                }
                else if (double.IsInfinity(immutableResult))
                {
                    Assert.Equal(immutableResult, mutableResult);
                }
                else
                {
                    Assert.Equal(immutableResult, mutableResult);
                }
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetDouble());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    [Theory]
    [MemberData(nameof(GetNumericTestValues))]
    public void GetDecimal_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        // If numeric, compare decimal values
        if (immutableElement.ValueKind == JsonValueKind.Number)
        {
            try
            {
                decimal immutableResult = immutableElement.GetDecimal();
                decimal mutableResult = mutableElement.GetDecimal();
                Assert.Equal(immutableResult, mutableResult);
            }
            catch (Exception immutableEx)
            {
                // If immutable throws, mutable should throw same type
                Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetDecimal());
                Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
            }
        }
    }

    #endregion

    #region Boolean Consistency Tests

    [Theory]
    [MemberData(nameof(GetBooleanTestValues))]
    public void GetBoolean_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        try
        {
            bool immutableResult = immutableElement.GetBoolean();
            bool mutableResult = mutableElement.GetBoolean();
            Assert.Equal(immutableResult, mutableResult);
        }
        catch (Exception immutableEx)
        {
            // If immutable throws, mutable should throw same type
            Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetBoolean());
            Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
        }
    }

    #endregion

    #region String Consistency Tests

    [Theory]
    [MemberData(nameof(GetStringTestValues))]
    public void GetString_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        try
        {
            string immutableResult = immutableElement.GetString();
            string mutableResult = mutableElement.GetString();
            Assert.Equal(immutableResult, mutableResult);
        }
        catch (Exception immutableEx)
        {
            // If immutable throws, mutable should throw same type
            Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetString());
            Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
        }
    }

    [Theory]
    [MemberData(nameof(GetDateTimeTestValues))]
    public void GetDateTimeOffset_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        try
        {
            DateTimeOffset immutableResult = immutableElement.GetDateTimeOffset();
            DateTimeOffset mutableResult = mutableElement.GetDateTimeOffset();
            Assert.Equal(immutableResult, mutableResult);
        }
        catch (Exception immutableEx)
        {
            // If immutable throws, mutable should throw same type
            Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetDateTimeOffset());
            Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
        }
    }

    [Theory]
    [MemberData(nameof(GetGuidTestValues))]
    public void GetGuid_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        try
        {
            Guid immutableResult = immutableElement.GetGuid();
            Guid mutableResult = mutableElement.GetGuid();
            Assert.Equal(immutableResult, mutableResult);
        }
        catch (Exception immutableEx)
        {
            // If immutable throws, mutable should throw same type
            Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetGuid());
            Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
        }
    }

    [Theory]
    [MemberData(nameof(GetBase64TestValues))]
    public void GetBytesFromBase64_MutableImmutableConsistency(string json)
    {
        // Immutable version
        var immutableElement = JsonElement.ParseValue(json);
        
        // Mutable version
        using var workspace = JsonWorkspace.Create();
        using JsonDocumentBuilder<JsonElement.Mutable> mutableDoc = JsonElement.CreateBuilder(workspace, JsonElement.ParseValue(json));
        JsonElement.Mutable mutableElement = mutableDoc.RootElement;
        
        // Both should have same value kind
        Assert.Equal(immutableElement.ValueKind, mutableElement.ValueKind);
        
        try
        {
            byte[] immutableResult = immutableElement.GetBytesFromBase64();
            byte[] mutableResult = mutableElement.GetBytesFromBase64();
            Assert.Equal(immutableResult, mutableResult);
        }
        catch (Exception immutableEx)
        {
            // If immutable throws, mutable should throw same type
            Exception mutableEx = Assert.ThrowsAny<Exception>(() => mutableElement.GetBytesFromBase64());
            Assert.Equal(immutableEx.GetType(), mutableEx.GetType());
        }
    }

    #endregion

    #region Test Data

    public static IEnumerable<object[]> GetNumericTestValues()
    {
        return new object[][]
        {
            // Valid numeric values
            new object[] { "0" },
            new object[] { "1" },
            new object[] { "-1" },
            new object[] { "42" },
            new object[] { "-42" },
            new object[] { "255" },
            new object[] { "256" },
            new object[] { "32767" },
            new object[] { "32768" },
            new object[] { "65535" },
            new object[] { "65536" },
            new object[] { "2147483647" },
            new object[] { "2147483648" },
            new object[] { "4294967295" },
            new object[] { "4294967296" },
            new object[] { "9223372036854775807" },
            new object[] { "9223372036854775808" },
            new object[] { "18446744073709551615" },
            new object[] { "3.14159" },
            new object[] { "-2.71828" },
            new object[] { "0.0" },
            new object[] { "1.0" },
            new object[] { "-0.0" },
            
            // Non-numeric values that should cause exceptions
            new object[] { "\"not a number\"" },
            new object[] { "true" },
            new object[] { "false" },
            new object[] { "null" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    public static IEnumerable<object[]> GetBooleanTestValues()
    {
        return new object[][]
        {
            // Valid boolean values
            new object[] { "true" },
            new object[] { "false" },
            
            // Non-boolean values that should cause exceptions
            new object[] { "\"true\"" },
            new object[] { "\"false\"" },
            new object[] { "1" },
            new object[] { "0" },
            new object[] { "null" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    public static IEnumerable<object[]> GetStringTestValues()
    {
        return new object[][]
        {
            // Valid string values
            new object[] { "\"\"" },
            new object[] { "\"Hello World\"" },
            new object[] { "\"Line1\\nLine2\"" },
            new object[] { "\"Tab\\tSeparated\"" },
            new object[] { "\"Quote: \\\"Hello\\\"\"" },
            new object[] { "\"Backslash: \\\\\"" },
            new object[] { "\"\\u0048\\u0065\\u006C\\u006C\\u006F\"" },
            new object[] { "null" },
            
            // Non-string values that should cause exceptions (for string-only scenarios)
            new object[] { "true" },
            new object[] { "false" },
            new object[] { "42" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    public static IEnumerable<object[]> GetDateTimeTestValues()
    {
        return new object[][]
        {
            // Valid DateTime strings
            new object[] { "\"2024-01-01T00:00:00Z\"" },
            new object[] { "\"2024-12-31T23:59:59.999Z\"" },
            new object[] { "\"2024-06-15T12:30:45.123+05:30\"" },
            new object[] { "\"2024-06-15T12:30:45.123-08:00\"" },
            new object[] { "\"1997-07-16T19:20:30.4555555Z\"" },
            
            // Invalid DateTime strings
            new object[] { "\"not a date\"" },
            new object[] { "\"2024-13-01T00:00:00Z\"" }, // Invalid month
            new object[] { "\"2024-01-32T00:00:00Z\"" }, // Invalid day
            new object[] { "\"2024-01-01T25:00:00Z\"" }, // Invalid hour
            new object[] { "\"\"" },
            new object[] { "null" },
            
            // Non-string values
            new object[] { "true" },
            new object[] { "false" },
            new object[] { "42" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    public static IEnumerable<object[]> GetGuidTestValues()
    {
        return new object[][]
        {
            // Valid GUID strings
            new object[] { "\"12345678-1234-1234-1234-123456789012\"" },
            new object[] { "\"00000000-0000-0000-0000-000000000000\"" },
            new object[] { "\"FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF\"" },
            new object[] { "\"550e8400-e29b-41d4-a716-446655440000\"" },
            new object[] { "\"{12345678-1234-1234-1234-123456789012}\"" },
            
            // Invalid GUID strings
            new object[] { "\"not a guid\"" },
            new object[] { "\"12345678-1234-1234-1234-12345678901\"" }, // Too short
            new object[] { "\"12345678-1234-1234-1234-1234567890123\"" }, // Too long
            new object[] { "\"GGGGGGGG-GGGG-GGGG-GGGG-GGGGGGGGGGGG\"" }, // Invalid characters
            new object[] { "\"\"" },
            new object[] { "null" },
            
            // Non-string values
            new object[] { "true" },
            new object[] { "false" },
            new object[] { "42" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    public static IEnumerable<object[]> GetBase64TestValues()
    {
        return new object[][]
        {
            // Valid Base64 strings
            new object[] { "\"\"" },
            new object[] { "\"SGVsbG8=\"" }, // "Hello"
            new object[] { "\"AQID\"" }, // [1, 2, 3]
            new object[] { "\"QWxhZGRpbjpvcGVuIHNlc2FtZQ==\"" }, // "Aladdin:open sesame"
            
            // Invalid Base64 strings
            new object[] { "\"Invalid Base64!\"" },
            new object[] { "\"SGVsbG8\"" }, // Missing padding
            new object[] { "\"SGVsbG8@\"" }, // Invalid character
            new object[] { "null" },
            
            // Non-string values
            new object[] { "true" },
            new object[] { "false" },
            new object[] { "42" },
            new object[] { "[]" },
            new object[] { "{}" },
        };
    }

    #endregion
}
