// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Flat object parsing benchmarks - 50 mixed properties representing common business objects.
/// Compares System.Text.Json baseline against Corvus.Text.Json for flat object parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkFlatObjectParsing
{
    private string? flatObjectJson;

    [GlobalSetup]
    public void Setup()
    {
        flatObjectJson = GenerateFlatObjectJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseFlatObjectCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(flatObjectJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseFlatObjectSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(flatObjectJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateFlatObjectJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");

        // Mix of different property types to simulate real-world objects
        for (int i = 0; i < 50; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();

            string value = (i % 4) switch
            {
                0 => $"\"{GenerateRandomString(10 + (i % 20))}\"", // Varying string lengths
                1 => (i * 123.456 + i).ToString("F3"), // Numbers
                2 => (i % 2 == 0).ToString().ToLower(), // Booleans
                _ => "null"
            };

            sb.Append($"  \"property{i}\": {value}");
        }

        sb.AppendLine();
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var random = new Random(42); // Fixed seed for consistent benchmarks
        char[] result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
}

/// <summary>
/// Nested object parsing benchmarks - 4 levels deep representing user profile data.
/// Compares System.Text.Json baseline against Corvus.Text.Json for nested object parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkNestedObjectParsing
{
    private string? nestedObjectJson;

    [GlobalSetup]
    public void Setup()
    {
        nestedObjectJson = GenerateNestedObjectJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseNestedObjectCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(nestedObjectJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseNestedObjectSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(nestedObjectJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateNestedObjectJson()
    {
        return """
        {
          "user": {
            "id": 12345,
            "profile": {
              "name": "John Doe",
              "email": "john.doe@example.com",
              "preferences": {
                "theme": "dark",
                "language": "en-US",
                "notifications": {
                  "email": true,
                  "push": false,
                  "sms": true,
                  "frequency": "daily"
                }
              },
              "address": {
                "street": "123 Main St",
                "city": "Anytown",
                "state": "CA",
                "zipCode": "12345",
                "country": "USA"
              }
            },
            "activity": {
              "lastLogin": "2024-01-15T10:30:00Z",
              "loginCount": 1523,
              "isActive": true
            }
          },
          "metadata": {
            "version": "1.2.3",
            "timestamp": "2024-01-15T10:30:00Z",
            "source": "api-v2"
          }
        }
        """;
    }
}

/// <summary>
/// Large array parsing benchmarks - 1000 elements with mixed types.
/// Compares System.Text.Json baseline against Corvus.Text.Json for large array parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkLargeArrayParsing
{
    private string? largeArrayJson;

    [GlobalSetup]
    public void Setup()
    {
        largeArrayJson = GenerateLargeArrayJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseLargeArrayCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(largeArrayJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseLargeArraySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(largeArrayJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateLargeArrayJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("[");

        for (int i = 0; i < 1000; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();

            // Mix of numbers, strings, and booleans
            string value = (i % 3) switch
            {
                0 => (i * 1.234567).ToString("F6"),
                1 => $"\"{GenerateRandomString(5 + (i % 15))}\"",
                _ => (i % 2 == 0).ToString().ToLower()
            };

            sb.Append($"  {value}");
        }

        sb.AppendLine();
        sb.AppendLine("]");
        return sb.ToString();
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var random = new Random(42); // Fixed seed for consistent benchmarks
        char[] result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
}

/// <summary>
/// API response parsing benchmarks - typical REST API response structure.
/// Compares System.Text.Json baseline against Corvus.Text.Json for API response parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkApiResponseParsing
{
    private string? apiResponseJson;

    [GlobalSetup]
    public void Setup()
    {
        apiResponseJson = GenerateApiResponseJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseApiResponseCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(apiResponseJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseApiResponseSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(apiResponseJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateApiResponseJson()
    {
        return """
        {
          "success": true,
          "data": {
            "users": [
              {
                "id": 1,
                "username": "alice",
                "email": "alice@example.com",
                "profile": {
                  "firstName": "Alice",
                  "lastName": "Smith",
                  "avatar": "https://example.com/avatars/alice.jpg",
                  "bio": "Software engineer passionate about clean code"
                },
                "roles": ["user", "admin"],
                "createdAt": "2023-01-15T08:30:00Z",
                "lastActiveAt": "2024-01-15T14:22:33Z"
              },
              {
                "id": 2,
                "username": "bob",
                "email": "bob@example.com",
                "profile": {
                  "firstName": "Bob",
                  "lastName": "Johnson",
                  "avatar": "https://example.com/avatars/bob.jpg",
                  "bio": "Product manager with 10+ years experience"
                },
                "roles": ["user"],
                "createdAt": "2023-03-22T12:45:00Z",
                "lastActiveAt": "2024-01-14T09:15:21Z"
              }
            ],
            "pagination": {
              "page": 1,
              "pageSize": 10,
              "totalPages": 1,
              "totalItems": 2,
              "hasNext": false,
              "hasPrevious": false
            }
          },
          "metadata": {
            "requestId": "req_1234567890",
            "timestamp": "2024-01-15T15:30:00Z",
            "version": "v1.2.3",
            "processingTimeMs": 45
          }
        }
        """;
    }
}

/// <summary>
/// Numeric-heavy JSON parsing benchmarks - financial/scientific data with various numeric formats.
/// Compares System.Text.Json baseline against Corvus.Text.Json for numeric data parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkNumericHeavyParsing
{
    private string? numericHeavyJson;

    [GlobalSetup]
    public void Setup()
    {
        numericHeavyJson = GenerateNumericHeavyJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseNumericHeavyCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(numericHeavyJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseNumericHeavySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(numericHeavyJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateNumericHeavyJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"financialData\": {");

        // Generate various numeric formats
        for (int i = 0; i < 100; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();

            string value = (i % 5) switch
            {
                0 => (i * 1234.5678).ToString("F4"), // Regular decimals
                1 => (i * 1000000).ToString(), // Large integers
                2 => (0.000001 * i).ToString("E"), // Scientific notation
                3 => (-i * 456.789).ToString("F3"), // Negative numbers
                _ => (i * Math.PI).ToString("F8") // High precision
            };

            sb.Append($"    \"metric{i}\": {value}");
        }

        sb.AppendLine();
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }
}

/// <summary>
/// String-heavy JSON parsing benchmarks - text processing data with varying string lengths.
/// Compares System.Text.Json baseline against Corvus.Text.Json for string-heavy data parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkStringHeavyParsing
{
    private string? stringHeavyJson;

    [GlobalSetup]
    public void Setup()
    {
        stringHeavyJson = GenerateStringHeavyJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseStringHeavyCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(stringHeavyJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseStringHeavySystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(stringHeavyJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateStringHeavyJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"textData\": {");

        // Generate strings of varying lengths
        for (int i = 0; i < 50; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();

            int length = 10 + (i * 3); // Increasing lengths
            string value = GenerateRandomString(length);
            sb.Append($"    \"text{i}\": \"{value}\"");
        }

        sb.AppendLine();
        sb.AppendLine("  }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789 ";
        var random = new Random(42); // Fixed seed for consistent benchmarks
        char[] result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random.Next(chars.Length)];
        }

        return new string(result);
    }
}

/// <summary>
/// Unicode string parsing benchmarks - Unicode, emoji, escaped characters, multilingual text.
/// Compares System.Text.Json baseline against Corvus.Text.Json for Unicode string parsing.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkUnicodeStringParsing
{
    private string? unicodeStringJson;

    [GlobalSetup]
    public void Setup()
    {
        unicodeStringJson = GenerateUnicodeStringJson();
    }

    [Benchmark]
    public Corvus.Text.Json.JsonValueKind ParseUnicodeStringCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(unicodeStringJson!);
        return document.RootElement.ValueKind;
    }

    [Benchmark(Baseline = true)]
    public System.Text.Json.JsonValueKind ParseUnicodeStringSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(unicodeStringJson!);
        return document.RootElement.ValueKind;
    }

    private static string GenerateUnicodeStringJson()
    {
        return """
        {
          "unicodeStrings": {
            "emoji": "Hello 👋 World 🌍 JSON 📝",
            "accents": "Café, naïve, résumé, piñata",
            "multilingual": "English, 中文, 日本語, العربية, Русский",
            "escaped": "Line 1\nLine 2\tTabbed\r\nWindows\\"Quote\\"",
            "unicode": "\u0048\u0065\u006C\u006C\u006F \u0057\u006F\u0072\u006C\u0064",
            "specialChars": "Special: @#$%^&*()_+-=[]{}|;':\",./<>?",
            "longUnicode": "这是一个很长的中文字符串，包含各种字符：数字123，标点符号！@#$%，以及其他Unicode字符：🚀🎉🌟",
            "mixedEscapes": "Mixed: \"quotes\", \\backslashes\\, \nlines\n, \ttabs\t, and unicode: 🎯"
          }
        }
        """;
    }
}