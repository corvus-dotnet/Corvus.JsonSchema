// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;

namespace JsonParsingBenchmarks;

/// <summary>
/// Small object benchmarks - typical mobile/web API response (~1KB).
/// Compares System.Text.Json baseline against Corvus.Text.Json for property access patterns.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkSmallObjectPropertyAccess
{
    private string? smallObjectJson;

    [GlobalSetup]
    public void Setup()
    {
        // Small object - typical mobile/web API response (~1KB)
        smallObjectJson = GenerateSmallObjectJson();
    }

    [Benchmark]
    public (string?, int, bool) SmallObjectPropertyAccessCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(smallObjectJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        // Simulate typical property access patterns
        string? name = null;
        int id = 0;
        bool active = false;

        if (root.TryGetProperty("name", out Corvus.Text.Json.JsonElement nameElement))
        {
            name = nameElement.GetString();
        }

        if (root.TryGetProperty("id", out Corvus.Text.Json.JsonElement idElement))
        {
            id = idElement.GetInt32();
        }

        if (root.TryGetProperty("isActive", out Corvus.Text.Json.JsonElement activeElement))
        {
            active = activeElement.GetBoolean();
        }

        return (name, id, active);
    }

    [Benchmark(Baseline = true)]
    public (string?, int, bool) SmallObjectPropertyAccessSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(smallObjectJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        // Simulate typical property access patterns
        string? name = null;
        int id = 0;
        bool active = false;

        if (root.TryGetProperty("name", out System.Text.Json.JsonElement nameElement))
        {
            name = nameElement.GetString();
        }

        if (root.TryGetProperty("id", out System.Text.Json.JsonElement idElement))
        {
            id = idElement.GetInt32();
        }

        if (root.TryGetProperty("isActive", out System.Text.Json.JsonElement activeElement))
        {
            active = activeElement.GetBoolean();
        }

        return (name, id, active);
    }

    private static string GenerateSmallObjectJson()
    {
        return """
        {
          "id": 12345,
          "name": "John Doe",
          "email": "john.doe@example.com",
          "isActive": true,
          "age": 30,
          "score": 95.5,
          "lastLogin": "2024-01-15T10:30:00Z",
          "roles": ["user", "viewer"],
          "metadata": {
            "source": "web",
            "version": "1.0"
          }
        }
        """;
    }
}

/// <summary>
/// Medium object benchmarks - typical REST API response (~10KB).
/// Compares System.Text.Json baseline against Corvus.Text.Json for multiple property access.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkMediumObjectPropertyAccess
{
    private string? mediumObjectJson;

    [GlobalSetup]
    public void Setup()
    {
        // Medium object - typical REST API response (~10KB)
        mediumObjectJson = GenerateMediumObjectJson();
    }

    [Benchmark]
    public (string?, string?, int, double, string?) MediumObjectPropertyAccessCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(mediumObjectJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        // Access multiple properties at different levels
        string? userName = null;
        string? email = null;
        int loginCount = 0;
        double averageScore = 0.0;
        string? status = null;

        if (root.TryGetProperty("user", out Corvus.Text.Json.JsonElement userElement))
        {
            if (userElement.TryGetProperty("username", out Corvus.Text.Json.JsonElement userNameElement))
                userName = userNameElement.GetString();

            if (userElement.TryGetProperty("email", out Corvus.Text.Json.JsonElement emailElement))
                email = emailElement.GetString();
        }

        if (root.TryGetProperty("stats", out Corvus.Text.Json.JsonElement statsElement))
        {
            if (statsElement.TryGetProperty("loginCount", out Corvus.Text.Json.JsonElement loginElement))
                loginCount = loginElement.GetInt32();

            if (statsElement.TryGetProperty("averageScore", out Corvus.Text.Json.JsonElement scoreElement))
                averageScore = scoreElement.GetDouble();
        }

        if (root.TryGetProperty("status", out Corvus.Text.Json.JsonElement statusElement))
        {
            status = statusElement.GetString();
        }

        return (userName, email, loginCount, averageScore, status);
    }

    [Benchmark(Baseline = true)]
    public (string?, string?, int, double, string?) MediumObjectPropertyAccessSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(mediumObjectJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        // Access multiple properties at different levels
        string? userName = null;
        string? email = null;
        int loginCount = 0;
        double averageScore = 0.0;
        string? status = null;

        if (root.TryGetProperty("user", out System.Text.Json.JsonElement userElement))
        {
            if (userElement.TryGetProperty("username", out System.Text.Json.JsonElement userNameElement))
                userName = userNameElement.GetString();

            if (userElement.TryGetProperty("email", out System.Text.Json.JsonElement emailElement))
                email = emailElement.GetString();
        }

        if (root.TryGetProperty("stats", out System.Text.Json.JsonElement statsElement))
        {
            if (statsElement.TryGetProperty("loginCount", out System.Text.Json.JsonElement loginElement))
                loginCount = loginElement.GetInt32();

            if (statsElement.TryGetProperty("averageScore", out System.Text.Json.JsonElement scoreElement))
                averageScore = scoreElement.GetDouble();
        }

        if (root.TryGetProperty("status", out System.Text.Json.JsonElement statusElement))
        {
            status = statusElement.GetString();
        }

        return (userName, email, loginCount, averageScore, status);
    }

    private static string GenerateMediumObjectJson()
    {
        return """
        {
          "user": {
            "id": 12345,
            "username": "johndoe",
            "email": "john.doe@example.com",
            "profile": {
              "firstName": "John",
              "lastName": "Doe",
              "displayName": "John D.",
              "avatar": "https://example.com/avatars/john.jpg",
              "bio": "Software developer with 5+ years experience",
              "website": "https://johndoe.dev",
              "location": "San Francisco, CA",
              "timezone": "America/Los_Angeles"
            }
          },
          "stats": {
            "loginCount": 1523,
            "averageScore": 87.3,
            "totalPoints": 15420,
            "streakDays": 45,
            "achievements": ["first_login", "power_user", "top_scorer"],
            "activityByMonth": {
              "jan": 156,
              "feb": 142,
              "mar": 189,
              "apr": 167,
              "may": 203
            }
          },
          "settings": {
            "theme": "dark",
            "language": "en-US",
            "notifications": {
              "email": true,
              "push": false,
              "sms": true,
              "marketing": false
            },
            "privacy": {
              "profileVisible": true,
              "activityVisible": false,
              "searchable": true
            }
          },
          "status": "active",
          "createdAt": "2023-01-15T08:30:00Z",
          "updatedAt": "2024-01-15T14:22:33Z",
          "lastActiveAt": "2024-01-15T14:22:33Z"
        }
        """;
    }
}

/// <summary>
/// Large object benchmarks with property map - configuration or data export (~50KB).
/// Compares System.Text.Json baseline against Corvus.Text.Json with property map optimization.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkLargeObjectWithPropertyMap
{
    private string? largeObjectJson;

    [GlobalSetup]
    public void Setup()
    {
        // Large object - configuration or data export (~50KB)
        largeObjectJson = GenerateLargeObjectJson();
    }

    [Benchmark]
    public (int, string?, string?) LargeObjectWithPropertyMapCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(largeObjectJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        // Ensure property map is built for efficient access
        root.EnsurePropertyMap();

        // Access properties that would benefit from property map
        int totalItems = 0;
        string? configName = null;
        string? version = null;

        if (root.TryGetProperty("totalItems", out Corvus.Text.Json.JsonElement totalElement))
        {
            totalItems = totalElement.GetInt32();
        }

        if (root.TryGetProperty("configName", out Corvus.Text.Json.JsonElement nameElement))
        {
            configName = nameElement.GetString();
        }

        if (root.TryGetProperty("version", out Corvus.Text.Json.JsonElement versionElement))
        {
            version = versionElement.GetString();
        }

        return (totalItems, configName, version);
    }

    [Benchmark(Baseline = true)]
    public (int, string?, string?) LargeObjectWithPropertyMapSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(largeObjectJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        // Access properties
        int totalItems = 0;
        string? configName = null;
        string? version = null;

        if (root.TryGetProperty("totalItems", out System.Text.Json.JsonElement totalElement))
        {
            totalItems = totalElement.GetInt32();
        }

        if (root.TryGetProperty("configName", out System.Text.Json.JsonElement nameElement))
        {
            configName = nameElement.GetString();
        }

        if (root.TryGetProperty("version", out System.Text.Json.JsonElement versionElement))
        {
            version = versionElement.GetString();
        }

        return (totalItems, configName, version);
    }

    private static string GenerateLargeObjectJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"configName\": \"production-config\",");
        sb.AppendLine("  \"version\": \"2.1.0\",");
        sb.AppendLine("  \"totalItems\": 500,");

        // Generate many properties to test property map efficiency
        for (int i = 0; i < 100; i++)
        {
            sb.AppendLine($"  \"setting{i}\": {{");
            sb.AppendLine($"    \"name\": \"Setting {i}\",");
            sb.AppendLine($"    \"value\": \"{GenerateRandomString(20)}\",");
            sb.AppendLine($"    \"enabled\": {(i % 2 == 0).ToString().ToLower()},");
            sb.AppendLine($"    \"priority\": {i % 10},");
            sb.AppendLine($"    \"category\": \"Category {i % 5}\"");
            sb.AppendLine($"  }},");
        }

        // Remove last comma and close
        sb.Length -= 3; // Remove ",\r\n"
        sb.AppendLine();
        sb.AppendLine("}");

        return sb.ToString();
    }

    private static string GenerateRandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
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
/// Deep nested property access benchmarks - common in configuration/settings.
/// Compares System.Text.Json baseline against Corvus.Text.Json for deep navigation.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkDeepNestedAccess
{
    private string? nestedAccessJson;

    [GlobalSetup]
    public void Setup()
    {
        // Nested access pattern - common in configuration/settings
        nestedAccessJson = GenerateNestedAccessJson();
    }

    [Benchmark]
    public (string?, string?, bool) DeepNestedAccessCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(nestedAccessJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        // Deep nested property access
        string? theme = null;
        string? language = null;
        bool emailNotifications = false;

        if (root.TryGetProperty("user", out Corvus.Text.Json.JsonElement userElement) &&
            userElement.TryGetProperty("preferences", out Corvus.Text.Json.JsonElement prefElement))
        {
            if (prefElement.TryGetProperty("theme", out Corvus.Text.Json.JsonElement themeElement))
                theme = themeElement.GetString();

            if (prefElement.TryGetProperty("language", out Corvus.Text.Json.JsonElement langElement))
                language = langElement.GetString();

            if (prefElement.TryGetProperty("notifications", out Corvus.Text.Json.JsonElement notifElement) &&
                notifElement.TryGetProperty("email", out Corvus.Text.Json.JsonElement emailElement))
            {
                emailNotifications = emailElement.GetBoolean();
            }
        }

        return (theme, language, emailNotifications);
    }

    [Benchmark(Baseline = true)]
    public (string?, string?, bool) DeepNestedAccessSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(nestedAccessJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        // Deep nested property access
        string? theme = null;
        string? language = null;
        bool emailNotifications = false;

        if (root.TryGetProperty("user", out System.Text.Json.JsonElement userElement) &&
            userElement.TryGetProperty("preferences", out System.Text.Json.JsonElement prefElement))
        {
            if (prefElement.TryGetProperty("theme", out System.Text.Json.JsonElement themeElement))
                theme = themeElement.GetString();

            if (prefElement.TryGetProperty("language", out System.Text.Json.JsonElement langElement))
                language = langElement.GetString();

            if (prefElement.TryGetProperty("notifications", out System.Text.Json.JsonElement notifElement) &&
                notifElement.TryGetProperty("email", out System.Text.Json.JsonElement emailElement))
            {
                emailNotifications = emailElement.GetBoolean();
            }
        }

        return (theme, language, emailNotifications);
    }

    private static string GenerateNestedAccessJson()
    {
        return """
        {
          "user": {
            "id": 12345,
            "profile": {
              "name": "John Doe",
              "email": "john.doe@example.com"
            },
            "preferences": {
              "theme": "dark",
              "language": "en-US",
              "notifications": {
                "email": true,
                "push": false,
                "sms": true,
                "marketing": {
                  "newsletter": false,
                  "promotions": true,
                  "surveys": false
                }
              },
              "display": {
                "density": "comfortable",
                "animations": true,
                "sidebar": {
                  "collapsed": false,
                  "position": "left",
                  "width": 250
                }
              }
            }
          },
          "application": {
            "version": "1.2.3",
            "buildNumber": "456",
            "environment": "production"
          }
        }
        """;
    }
}

/// <summary>
/// Array processing benchmarks - common in data processing scenarios.
/// Compares System.Text.Json baseline against Corvus.Text.Json for array enumeration.
/// </summary>
[MemoryDiagnoser]
public class BenchmarkArrayProcessing
{
    private string? arrayAccessJson;

    [GlobalSetup]
    public void Setup()
    {
        // Array access pattern - common in data processing
        arrayAccessJson = GenerateArrayAccessJson();
    }

    [Benchmark]
    public (int, double, string?) ArrayProcessingCorvus()
    {
        using var document = Corvus.Text.Json.ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(arrayAccessJson!);
        Corvus.Text.Json.JsonElement root = document.RootElement;

        int itemCount = 0;
        double totalValue = 0.0;
        string? firstItemName = null;

        if (root.TryGetProperty("items", out Corvus.Text.Json.JsonElement itemsElement) && itemsElement.ValueKind == Corvus.Text.Json.JsonValueKind.Array)
        {
            foreach (Corvus.Text.Json.JsonElement item in itemsElement.EnumerateArray())
            {
                itemCount++;

                if (item.TryGetProperty("value", out Corvus.Text.Json.JsonElement valueElement) && valueElement.ValueKind == Corvus.Text.Json.JsonValueKind.Number)
                {
                    totalValue += valueElement.GetDouble();
                }

                if (firstItemName == null && item.TryGetProperty("name", out Corvus.Text.Json.JsonElement nameElement))
                {
                    firstItemName = nameElement.GetString();
                }
            }
        }

        return (itemCount, totalValue, firstItemName);
    }

    [Benchmark(Baseline = true)]
    public (int, double, string?) ArrayProcessingSystemTextJson()
    {
        using var document = System.Text.Json.JsonDocument.Parse(arrayAccessJson!);
        System.Text.Json.JsonElement root = document.RootElement;

        int itemCount = 0;
        double totalValue = 0.0;
        string? firstItemName = null;

        if (root.TryGetProperty("items", out System.Text.Json.JsonElement itemsElement) && itemsElement.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (System.Text.Json.JsonElement item in itemsElement.EnumerateArray())
            {
                itemCount++;

                if (item.TryGetProperty("value", out System.Text.Json.JsonElement valueElement) && valueElement.ValueKind == System.Text.Json.JsonValueKind.Number)
                {
                    totalValue += valueElement.GetDouble();
                }

                if (firstItemName == null && item.TryGetProperty("name", out System.Text.Json.JsonElement nameElement))
                {
                    firstItemName = nameElement.GetString();
                }
            }
        }

        return (itemCount, totalValue, firstItemName);
    }

    private static string GenerateArrayAccessJson()
    {
        var sb = new StringBuilder();
        sb.AppendLine("{");
        sb.AppendLine("  \"items\": [");

        for (int i = 0; i < 50; i++)
        {
            if (i > 0) sb.Append(",");
            sb.AppendLine();
            sb.AppendLine("    {");
            sb.AppendLine($"      \"id\": {i + 1},");
            sb.AppendLine($"      \"name\": \"Item {i + 1}\",");
            sb.AppendLine($"      \"value\": {(i * 12.34).ToString("F2")},");
            sb.AppendLine($"      \"active\": {(i % 2 == 0).ToString().ToLower()},");
            sb.AppendLine($"      \"category\": \"Category {(i % 5) + 1}\"");
            sb.Append("    }");
        }

        sb.AppendLine();
        sb.AppendLine("  ],");
        sb.AppendLine("  \"metadata\": {");
        sb.AppendLine("    \"totalCount\": 50,");
        sb.AppendLine("    \"page\": 1,");
        sb.AppendLine("    \"pageSize\": 50");
        sb.AppendLine("  }");
        sb.AppendLine("}");

        return sb.ToString();
    }
}