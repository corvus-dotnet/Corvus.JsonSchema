# Building & Mutating JSON

This guide shows you how to build and modify JSON documents efficiently, with minimal allocations and excellent performance.

We have already looked at [`ParsedJsonDocument`](./ParsedJsonDocument.md) to see how we can parse and use *immutable* documents. Now, we will look at how we can build new documents.

## Overview

`JsonDocumentBuilder<T>` provides a high-performance way to create and modify JSON documents in memory. Think of it as a workshop where you craft complex JSON structures using pooled resources - you borrow the tools, build your document, and return them for reuse. It works hand-in-hand with `JsonWorkspace`, which manages those pooled resources.

**FAQ**: _Why not just use System.Text.Json's JsonNode?_

Both approaches let you build mutable JSON, but they differ significantly:

| Feature         | JsonDocumentBuilder                | System.Text.Json.JsonNode               |
|-----------------|------------------------------------|-----------------------------------------|
| Memory Strategy | Pooled resources, reused           | Per-document allocations                |
| Performance     | Optimised for high-throughput      | General-purpose                         |
| Threading       | Thread-affine (workspace)          | Thread-safe nodes                       |
| API Style       | Builder pattern                    | Property-based mutation                 |
| Best For        | Request/response cycles, pipelines | Long-lived documents, tree manipulation |

**The key difference** is that `JsonDocumentBuilder` is designed for scenarios where you create a document, use it briefly (write to an HTTP response, save to a file), then dispose it. The workspace pools memory across many such operations, dramatically reducing allocations. `JsonNode` is better when you need to keep documents around, pass them across threads, or manipulate them over time.

Let's get started by looking at `JsonWorkspace`.

## Understanding the Workspace

A `JsonWorkspace` is your resource manager. It keeps track of reusable buffers and writers, allocating them as needed and reclaiming them when you're done.

### Creating a Workspace

```csharp
using Corvus.Text.Json;

// Create a workspace for building documents
using JsonWorkspace workspace = JsonWorkspace.Create();

// Use it to create one or more documents
// ...
```

### Workspace Options

```csharp
// Create with custom writer options
var writerOptions = new JsonWriterOptions
{
    Indented = true,
    SkipValidation = false
};

using JsonWorkspace workspace = JsonWorkspace.Create(
    initialDocumentCapacity: 10,
    options: writerOptions);
```

The options you configure here determine how JSON gets serialized when you write documents. This becomes crucial when using rented writers (covered in the serialization section below).

Notice that you can specify the expected document capacity for the workspace. Typically, you will know exactly what this is, and be able to allocate only the resource you need.

### Disposal Semantics

When you dispose a workspace, it returns any resources it consumed to the pool.

```csharp
using ParsedJsonDocument<JsonElement> sourceDoc = ParsedJsonDocument<JsonElement>.Parse("""{"value": 42}""");

using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source((ref objectBuilder) =>
    {
        // Using data from sourceDoc
        objectBuilder.AddProperty("original"u8, sourceDoc.RootElement.GetProperty("value"));
        objectBuilder.AddProperty("modified"u8, 100);
    }));
```

 When the workspace disposes:
 - Pooled workspaces return to the cache for reuse
 - Mutable documents are disposed and their resources are returned.

## Creating Simple Documents

Sometimes you just need to wrap a single value in a JSON document - perhaps for an API response.

### From Primitive Values

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// Create from integers - useful for IDs, counts, status codes
using var intDoc = JsonElement.CreateBuilder(workspace, 42);
Console.WriteLine(intDoc.RootElement.GetInt32()); // 42

// Create from doubles - measurements, prices, coordinates
using var doubleDoc = JsonElement.CreateBuilder(workspace, 3.14159);
Console.WriteLine(doubleDoc.RootElement.GetDouble()); // 3.14159

// Create from strings
using var stringDoc = JsonElement.CreateBuilder(workspace, "Hello, World!"u8);
Console.WriteLine(stringDoc.RootElement.GetString()); // Hello, World!

// Create from UTF-8 byte spans
// This is faster! No encoding conversion needed - straight UTF-8 bytes
using var utf8Doc = JsonElement.CreateBuilder(workspace, "Hello"u8);
Console.WriteLine(utf8Doc.RootElement.GetString()); // Hello

// Create from booleans - flags, feature toggles, status indicators
using var boolDoc = JsonElement.CreateBuilder(workspace, true);
Console.WriteLine(boolDoc.RootElement.GetBoolean()); // True

// Create null value - for optional fields or explicit null responses
using var nullDoc = JsonElement.CreateBuilder(
    workspace,
    JsonElement.Source.Null());
Console.WriteLine(nullDoc.RootElement.ValueKind); // Null
```

**Performance Tip**: Use the `u8` suffix for string literals whenever possible. This creates UTF-8 bytes at compile time, avoiding runtime encoding overhead. You can just use UTF-16 `string` instances, but is more efficient to use the UTF-8 form.

## Creating Object Documents

Object documents are JSON objects with key-value pairs. They're the most common structure for representing entities, configurations, and API responses. The builder pattern allows you to construct complex nested structures efficiently.

### Using Builder Delegates

Builder delegates provide a fluent, type-safe way to construct JSON objects. The `static` keyword helps the compiler optimize the delegate by avoiding closure allocations. Use UTF-8 string literals (with `u8` suffix) for property names to avoid the overhead of transcoding from UTF-16 `string`.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// Build a person object - common pattern for entity representation
using var personDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("name"u8, "John Smith"u8);
        objectBuilder.AddProperty("age"u8, 30);
        objectBuilder.AddProperty("isActive"u8, true);
        objectBuilder.AddProperty("email"u8, "john@example.com"u8);
    }));

Console.WriteLine(personDoc.RootElement.ToString());
// Output: {"name":"John Smith","age":30,"isActive":true,"email":"john@example.com"}
```

### Nested Objects

Nested objects are essential for representing hierarchical data structures like user profiles, configuration files, or complex domain models. Each level of nesting uses its own builder delegate, keeping the code organized and readable.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// Build a hierarchical user structure - common in REST APIs
using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("user"u8, static (ref userBuilder) =>
        {
            userBuilder.AddProperty("id"u8, 1);

            // Nested profile object within user
            userBuilder.AddProperty("profile"u8, static (ref profileBuilder) =>
            {
                profileBuilder.AddProperty("firstName"u8, "Jane"u8);
                profileBuilder.AddProperty("lastName"u8, "Doe"u8);
                profileBuilder.AddProperty("age"u8, 28);
            });
        });

        objectBuilder.AddProperty("timestamp"u8, "2026-02-24T11:00:00Z"u8);
    }));

// Navigate through nested structure to access values
JsonElement.Mutable root = doc.RootElement;
JsonElement.Mutable user = root.GetProperty("user");
JsonElement.Mutable profile = user.GetProperty("profile");

string firstName = profile.GetProperty("firstName").GetString();
Console.WriteLine($"First Name: {firstName}"); // Jane
```

## Creating Array Documents

Arrays are ubiquitous in JSON - lists of search results, collections of entities, tags, and more.

### Simple Arrays

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// A numeric array - IDs, scores, measurements, counts
using var arrayDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref arrayBuilder) =>
    {
        arrayBuilder.AddItem(1);
        arrayBuilder.AddItem(2);
        arrayBuilder.AddItem(3);
        arrayBuilder.AddItem(4);
        arrayBuilder.AddItem(5);
    }));

Console.WriteLine(arrayDoc.RootElement.ToString());
// Output: [1,2,3,4,5]
```

### Arrays of Strings

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// String arrays - tags, categories, permissions, you name it
using var namesDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref arrayBuilder) =>
    {
        arrayBuilder.AddItem("Alice"u8);
        arrayBuilder.AddItem("Bob"u8);
        arrayBuilder.AddItem("Charlie"u8);
    }));

// Iterate through it
foreach (JsonElement.Mutable name in namesDoc.RootElement.EnumerateArray())
{
    Console.WriteLine(name.GetString());
}
```

### Arrays of Objects

This is the pattern you see everywhere in REST APIs - collections of entities.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// Users array - standard API list response
using var usersDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref arrayBuilder) =>
    {
        arrayBuilder.AddItem(static (ref userBuilder) =>
        {
            userBuilder.AddProperty("id"u8, 1);
            userBuilder.AddProperty("name"u8, "Alice"u8);
        });

        arrayBuilder.AddItem(static (ref userBuilder) =>
        {
            userBuilder.AddProperty("id"u8, 2);
            userBuilder.AddProperty("name"u8, "Bob"u8);
        });

        arrayBuilder.AddItem(static (ref userBuilder) =>
        {
            userBuilder.AddProperty("id"u8, 3);
            userBuilder.AddProperty("name"u8, "Charlie"u8);
        });
    }));

Console.WriteLine(usersDoc.RootElement.ToString());
```

## Working with Existing JSON

In many applications, you receive JSON from an API, file, or database, modify it, and send it on its way. The pattern is straightforward - parse, mutate, serialize.

### From ParsedJsonDocument

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// JSON from wherever - API, file, database
string json = """
    {
        "name": "Original",
        "value": 100
    }
    """;

// Parse it - gives you an immutable, read-only document
using ParsedJsonDocument<JsonElement> sourceDoc =
    ParsedJsonDocument<JsonElement>.Parse(json);

// Make it mutable - copies data into the workspace
using JsonDocumentBuilder<JsonElement.Mutable> builder =
    sourceDoc.RootElement.CreateBuilder(workspace);

// Now you can modify it (more on that below)
JsonElement.Mutable root = builder.RootElement;
Console.WriteLine(root.ToString());
```

### Cloning and Modifying

You'll often receive JSON from an API, file, or database, modify parts of it, and send it on its way. This pattern appears frequently in middleware, API gateways, and data transformation pipelines.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

string originalJson = """
    {
        "status": "pending",
        "count": 5
    }
    """;

using ParsedJsonDocument<JsonElement> original =
    ParsedJsonDocument<JsonElement>.Parse(originalJson);

using JsonDocumentBuilder<JsonElement.Mutable> modified =
    original.RootElement.CreateBuilder(workspace);

// Make your changes
JsonElement.Mutable root = modified.RootElement;
root.SetProperty("status", "completed"u8);
root.SetProperty("count", 10);

Console.WriteLine(modified.RootElement.ToString());
// Output: {"status":"completed","count":10}
```

## Building Dynamic JSON

Real-world JSON isn't all static strings. You've got collections from your database, computed values, user input. Here's how you mix static structure with runtime data.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

// Runtime data - from wherever
string[] tags = ["admin", "user", "active"];
int[] years = [2020, 2021, 2022, 2023, 2024];

using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source((ref objectBuilder) =>
    {
        // Static structure with runtime values
        objectBuilder.AddProperty("id"u8, Guid.NewGuid());

        objectBuilder.AddProperty("profile"u8, static (ref profileBuilder) =>
        {
            profileBuilder.AddProperty("username"u8, "john.doe"u8);
            profileBuilder.AddProperty("created"u8, DateTime.UtcNow);
        });

        // Dynamically add array from runtime collection
        // Note: Cannot use 'static' when capturing variables
        objectBuilder.AddProperty("tags"u8, (ref tagsBuilder) =>
        {
            foreach (string tag in tags)
            {
                tagsBuilder.AddProperty(tag);
            }
        });

        // Another dynamic array from collection
        objectBuilder.AddProperty("activeYears"u8, (ref yearsBuilder) =>
        {
            foreach (int year in years)
            {
                yearsBuilder.AddProperty(year);
            }
        });

        objectBuilder.AddProperty("metadata"u8, static (ref metaBuilder) =>
        {
            metaBuilder.AddProperty("version"u8, "1.0"u8);
            metaBuilder.AddProperty("revision"u8, 42);
        });
    }));

Console.WriteLine(doc.RootElement.ToString());
```

## Modifying Documents

After creating a document, you often need to update values based on business logic, user actions, or external events. The mutable API allows in-place modifications without rebuilding the entire structure.

**Common Scenarios:**
- Updating status flags after processing
- Incrementing counters or metrics
- Patching API responses before forwarding
- Applying business rules to data

### Setting Properties

`SetProperty` updates or adds properties on JSON objects. If the property exists, it's updated; if not, it's added. This is useful for applying changes without knowing the current state.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("name"u8, "Initial"u8);
        objectBuilder.AddProperty("count"u8, 0);
    }));

JsonElement.Mutable root = doc.RootElement;

// Update existing properties
root.SetProperty("name"u8, "Updated"u8);
root.SetProperty("count"u8, 100);

// Add new properties
root.SetProperty("timestamp"u8, DateTime.UtcNow);

Console.WriteLine(root.ToString());
```

### Adding Array Elements

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("items"u8, static (ref arrayBuilder) =>
        {
            arrayBuilder.AddItem("item1"u8);
            arrayBuilder.AddItem("item2"u8);
        });
    }));

JsonElement.Mutable root = doc.RootElement;
JsonElement.Mutable items = root.GetProperty("items");

// Add more items to the array
items.AddItem("item3"u8);

// Add multiple items at once
items.AddRange(static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("item4"u8);
    b.AddItem("item5"u8);
});

// Insert multiple items at a specific index
items.InsertRange(1, static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("inserted1"u8);
    b.AddItem("inserted2"u8);
});

// Set an item at an index
items.SetItem(0, "Replace item1"u8);

Console.WriteLine(root.ToString());
```

### Property Indexers

In addition to `GetProperty()`, mutable and immutable elements support indexed access using string, UTF-8, or UTF-16 property names:

```csharp
using var doc = ParsedJsonDocument<JsonElement>.Parse("""{"name":"Alice","age":30}""");

// Indexed access on immutable element
JsonElement name = doc.RootElement["name"u8];       // UTF-8 (most efficient)
JsonElement age  = doc.RootElement["age"];           // string

Console.WriteLine(name.GetString()); // "Alice"
Console.WriteLine(age.GetInt32());   // 30
```

The same indexers work on mutable elements:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = doc.RootElement.CreateBuilder(workspace);

JsonElement.Mutable root = builder.RootElement;
JsonElement.Mutable nameEl = root["name"u8];

Console.WriteLine(nameEl.GetString()); // "Alice"
```

UTF-8 access (`"key"u8`) avoids transcoding overhead and is the recommended approach in performance-critical paths.

## Serializing Documents

After building or modifying a document, you need to serialize it for storage, transmission, or API responses. The `WriteTo` method provides efficient UTF-8 serialization directly to a `Utf8JsonWriter`.

**Use Cases:**
- Writing to HTTP response streams
- Saving to files
- Sending to message queues
- Logging structured data

### Basic Serialization

The simplest approach - create your own writer and write to it:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("message"u8, "Hello"u8);
        objectBuilder.AddProperty("status"u8, 200);
    }));

// Write to a memory stream (could be any stream - file, network, etc.)
using var stream = new MemoryStream();
using (var writer = new Utf8JsonWriter(
    stream,
    new JsonWriterOptions { Indented = true }))
{
    // Efficiently writes UTF-8 bytes directly to the writer
    doc.WriteTo(writer);
}

string json = Encoding.UTF8.GetString(stream.ToArray());
Console.WriteLine(json);
```

### Renting Writers from the Workspace

Instead of creating a new `Utf8JsonWriter` every time, you can rent one from the workspace. The workspace maintains a pool of writers configured with your specified options.

**Why rent instead of create?**

1. **Performance** - Pooled writers eliminate allocations in hot paths
2. **Consistency** - All writers automatically use the workspace's configured options
3. **Integration** - Perfect for ASP.NET Core pipelines where you're serializing to an `IBufferWriter`

#### Pattern 1: Rent Writer and Buffer (In-Memory)

The workspace provides both the writer and a buffer. Build your document, write it out, and use the bytes:

```csharp
var writerOptions = new JsonWriterOptions { Indented = false };
using JsonWorkspace workspace = JsonWorkspace.Create(options: writerOptions);

// Build your document
using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("message"u8, "Hello, World!"u8);
        objectBuilder.AddProperty("timestamp"u8, DateTime.UtcNow);
        objectBuilder.AddProperty("status"u8, 200);
    }));

// Rent writer + buffer
Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
    defaultBufferSize: 1024,
    out IByteBufferWriter bufferWriter);

try
{
    // Write document to the rented writer
    doc.WriteTo(writer);
    writer.Flush();

    // Get the result
    ReadOnlySpan<byte> jsonBytes = bufferWriter.WrittenSpan;
    Console.WriteLine(Encoding.UTF8.GetString(jsonBytes));
}
finally
{
    // Always return what you rent
    workspace.ReturnWriterAndBuffer(writer, bufferWriter);
}
```

This pattern is perfect for scenarios where you need the JSON bytes in memory before sending them somewhere - maybe you're computing a hash, compressing them, or storing them in a cache.

#### Pattern 2: Rent Writer for Streaming (ASP.NET Core)

When you have your own `IBufferWriter<byte>` - such as ASP.NET Core's `PipeWriter` from `context.Response.BodyWriter` - rent a writer for it and write your document synchronously. This avoid intermediate buffers and copies, and writes straight to the response pipe.

```csharp
public async Task WriteApiResponse(HttpContext context)
{
    // Fetch data (async)
    string userData = await FetchUserDataAsync();

    // Build and write response (synchronous, within workspace scope)
    using (JsonWorkspace workspace = JsonWorkspace.Create(
        options: new JsonWriterOptions { Indented = false }))
    {
        // Build the document
        using var doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source((ref objectBuilder) =>
            {
                objectBuilder.AddProperty("success"u8, true);
                objectBuilder.AddProperty("timestamp"u8, DateTime.UtcNow);
                objectBuilder.AddProperty("data"u8, Encoding.UTF8.GetBytes(userData));
            }));

        // Rent writer for the response body writer
        Utf8JsonWriter writer = workspace.RentWriter(context.Response.BodyWriter);

        try
        {
            // Write directly to response pipe - zero copies!
            doc.WriteTo(writer);
            writer.Flush();
        }
        finally
        {
            workspace.ReturnWriter(writer);
        }
    } // Workspace disposed before any awaits

    // Flush the pipe (async operation happens AFTER workspace disposal)
    await context.Response.BodyWriter.FlushAsync();
}
```

As always, build synchronously within the workspace scope, then perform async I/O *after* disposing the workspace. The writer writes directly to the pipe's buffer, which you flush once the workspace is safely disposed.

### When to Rent Writers

**Rent when:**
- High-throughput scenarios (web APIs, message processing)
- You're serializing repeatedly in a loop
- You want zero-allocation serialization
- You're integrating with ASP.NET Core pipelines

**Don't bother when:**
- One-off serialization in a CLI tool
- You're already allocation-bound elsewhere
- Simple scripts or utilities

The rental pattern shines in hot paths where you're serializing thousands of documents per second. The workspace pools everything, and you get consistent, fast, allocation-free serialization.

## Performance Tips

For optimal performance:

1. **Workspaces can be rented for reuse** - There is one backing workspace per calling thread, with many documents. This minimizes pool contention.
2. **Pre-allocate capacity** - When you know the document size, pass `estimatedMemberCount` to avoid resizing.
3. **UTF-8 literals** - Use the `u8` suffix for no encoding conversion - just UTF-8 bytes.
4. **Stay native** - Work with JSON Elements directly rather than constantly converting to strings.
5. **Dispose promptly** - Don't hold resources longer than needed.
6. **Static delegates** - Mark lambdas `static` when possible to avoid closures and allocations. .NET will usually find this optimization for you but if you get in the habit of marking them static, you will discover when you were inadvertently creating closures. Use context-supplying overloads to marshal data into the delegate.

## Common Patterns

### Building API Responses

Standard web API response - success flag, timestamp, data payload:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var response = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("success"u8, true);
        objectBuilder.AddProperty("timestamp"u8, DateTime.UtcNow);
        objectBuilder.AddProperty("data"u8, static (ref dataBuilder) =>
        {
            dataBuilder.AddProperty("id"u8, 12345);
            dataBuilder.AddProperty("status"u8, "completed"u8);
        });
    }));

return response.RootElement.ToString();
```

### Enriching External API Data

A common use case in API gateways, middleware, and backend-for-frontend patterns: fetch data from an external API and augment it with additional information from databases or other services before returning to the client.

**Scenario:** You receive user data from an authentication service but need to add permissions and preferences from your database before sending to the client.

```csharp
// Parse API response from external service
string apiResponse = """
    {
        "id": 12345,
        "username": "johndoe",
        "email": "john@example.com"
    }
    """;

using ParsedJsonDocument<JsonElement> apiDoc = ParsedJsonDocument<JsonElement>.Parse(apiResponse);
JsonElement apiRoot = apiDoc.RootElement;

// Get additional data from your systems
string[] permissions = GetUserPermissions(userId);
var preferences = GetUserPreferences(userId);

// Build enriched document combining external and internal data
using JsonWorkspace workspace = JsonWorkspace.Create();
using var enrichedDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source((ref objectBuilder) =>
    {
        // Original API data
        objectBuilder.AddProperty("userId"u8, apiRoot.GetProperty("id"));
        objectBuilder.AddProperty("username"u8, apiRoot.GetProperty("username"));

        // Augmented data
        objectBuilder.AddProperty("permissions"u8, (ref permBuilder) =>
        {
            foreach (string perm in permissions)
            {
                permBuilder.AddProperty(perm);
            }
        });

        objectBuilder.AddProperty("preferences"u8, (ref prefBuilder) =>
        {
            prefBuilder.AddProperty("theme"u8, preferences.Theme);
            prefBuilder.AddProperty("notifications"u8, preferences.NotificationsEnabled);
        });
    }));
```

### Composing Documents from Multiple Async API Calls

A frequent pattern in modern applications involves fetching data from multiple services concurrently, then assembling the results into a single JSON document. This is common in API gateways, backend-for-frontend (BFF) layers, and orchestration services.

**Key insight**: `ParsedJsonDocument` instances are **immutable and thread-safe**. Unlike `JsonWorkspace`, they can safely cross async boundaries. This makes them perfect for gathering data from multiple sources asynchronously, then composing them into a new document.

**Scenario**: You're building a user profile endpoint that combines data from three microservices - user service, posts service, and analytics service. Each service call is independent, so you want to fetch them in parallel.

```csharp
public async Task<string> GetUserProfileAsync(int userId)
{
    // Step 1: Fetch data from multiple APIs concurrently
    // ParsedJsonDocument is safe across async boundaries - these can await freely
    Task<ParsedJsonDocument<JsonElement>> userTask =
        FetchUserDataAsync(userId);
    Task<ParsedJsonDocument<JsonElement>> postsTask =
        FetchUserPostsAsync(userId);
    Task<ParsedJsonDocument<JsonElement>> analyticsTask =
        FetchUserAnalyticsAsync(userId);

    // Wait for all APIs to complete
    await Task.WhenAll(userTask, postsTask, analyticsTask);

    using ParsedJsonDocument<JsonElement> userDoc = await userTask;
    using ParsedJsonDocument<JsonElement> postsDoc = await postsTask;
    using ParsedJsonDocument<JsonElement> analyticsDoc = await analyticsTask;

    // Step 2: Create workspace and compose the final document
    // All async work is done - workspace stays on this thread
    string result;
    using (JsonWorkspace workspace = JsonWorkspace.Create())
    {
        using var profileDoc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source((ref objectBuilder) =>
            {
                // User info from first API
                JsonElement user = userDoc.RootElement;
                objectBuilder.AddProperty("userId"u8, user.GetProperty("id"));
                objectBuilder.AddProperty("username"u8, user.GetProperty("username"));
                objectBuilder.AddProperty("email"u8, user.GetProperty("email"));

                // Posts from second API
                objectBuilder.AddProperty("recentPosts"u8, (ref postsBuilder) =>
                {
                    JsonElement posts = postsDoc.RootElement.GetProperty("posts");
                    foreach (JsonElement post in posts.EnumerateArray())
                    {
                        postsBuilder.AddProperty((ref postBuilder) =>
                        {
                            postBuilder.AddProperty("id"u8, post.GetProperty("id"));
                            postBuilder.AddProperty("title"u8, post.GetProperty("title"));
                            postBuilder.AddProperty("publishedAt"u8, post.GetProperty("publishedAt"));
                        });
                    }
                });

                // Analytics from third API
                objectBuilder.AddProperty("stats"u8, (ref statsBuilder) =>
                {
                    JsonElement analytics = analyticsDoc.RootElement;
                    statsBuilder.AddProperty("totalViews"u8, analytics.GetProperty("totalViews"));
                    statsBuilder.AddProperty("totalLikes"u8, analytics.GetProperty("totalLikes"));
                    statsBuilder.AddProperty("followerCount"u8, analytics.GetProperty("followerCount"));
                });

                // Computed fields
                objectBuilder.AddProperty("isActive"u8,
                    userDoc.RootElement.GetProperty("lastLoginAt").GetDateTime() > DateTime.UtcNow.AddDays(-30));
            }));

        // Step 3: Serialize the document
        // Note: ToString() is shown here for simplicity, but in production you'd typically
        // use WriteTo() with a rented writer for zero-allocation serialization (see serialization examples)
        result = profileDoc.RootElement.ToString();
    }

    return result;
}

// Helper methods to fetch from APIs
async Task<ParsedJsonDocument<JsonElement>> FetchUserDataAsync(int userId)
{
    using var httpClient = new HttpClient();
    string json = await httpClient.GetStringAsync($"https://api.example.com/users/{userId}");
    return ParsedJsonDocument<JsonElement>.Parse(json);
}

async Task<ParsedJsonDocument<JsonElement>> FetchUserPostsAsync(int userId)
{
    using var httpClient = new HttpClient();
    string json = await httpClient.GetStringAsync($"https://api.example.com/users/{userId}/posts");
    return ParsedJsonDocument<JsonElement>.Parse(json);
}

async Task<ParsedJsonDocument<JsonElement>> FetchUserAnalyticsAsync(int userId)
{
    using var httpClient = new HttpClient();
    string json = await httpClient.GetStringAsync($"https://analytics.example.com/users/{userId}/stats");
    return ParsedJsonDocument<JsonElement>.Parse(json);
}
```

**Why this pattern works**:

1. **Parallel fetching**: All three API calls happen concurrently with `Task.WhenAll`, minimizing total latency
2. **Safe async boundaries**: `ParsedJsonDocument` instances can be awaited and passed around freely - they're immutable
3. **Efficient composition**: The workspace is only created after all async work completes, staying on a single thread
4. **Zero unnecessary allocations**: We compose directly from the parsed documents using their native `JsonElement` properties - no string conversions or intermediate objects

If each API call takes 100ms, sequential calls would take 300ms. With parallel fetching, you pay only ~100ms (longest call) plus composition overhead. The composition itself is allocation-efficient because you're working with the original parsed bytes, not creating intermediate .NET objects.

### Building Documents Across Async Boundaries with CreateUnrented

The standard `JsonWorkspace.Create()` uses thread-static storage for optimal performance. This means it's tied to the current thread and cannot cross async boundaries. If your code hits an `await`, the continuation might resume on a different thread, and accessing the workspace from that thread causes a runtime error.

However, sometimes you need to partially build a document, make an async call mid-way, then continue building. For these cases, use `JsonWorkspace.CreateUnrented()`.

**Key difference**: `CreateUnrented()` allocates workspace storage without thread-static optimization. It's slightly less efficient, but can safely traverse async boundaries.

**Scenario**: You start building a document, need to fetch additional data asynchronously, then continue building:

```csharp
public async Task<JsonElement> BuildReportAsync()
{
    Console.WriteLine("Fetching initial data...");
    string initialData = await FetchInitialDataAsync();
    using ParsedJsonDocument<JsonElement> initialDoc = ParsedJsonDocument<JsonElement>.Parse(initialData);

    // Use CreateUnrented() - this workspace can cross async boundaries
    using (JsonWorkspace workspace = JsonWorkspace.CreateUnrented())
    {
        // Start building the document
        using var doc = JsonElement.CreateBuilder(
            workspace,
            new JsonElement.Source((ref objectBuilder) =>
            {
                objectBuilder.AddProperty("initialData"u8, initialDoc.RootElement.GetProperty("value"));
                objectBuilder.AddProperty("timestamp"u8, DateTime.UtcNow);
            }));

        // Make an async call - workspace survives the await because it's unrented
        string additionalData = await FetchAdditionalDataAsync();
        using ParsedJsonDocument<JsonElement> additionalDoc = ParsedJsonDocument<JsonElement>.Parse(additionalData);

        // Continue modifying the document after the await
        JsonElement.Mutable mutableRoot = doc.RootElement;
        mutableRoot.SetProperty("additionalData", additionalDoc.RootElement.GetProperty("extra"));
        mutableRoot.SetProperty("completedAt", DateTime.UtcNow);

        return doc.RootElement;
    } // Workspace disposes mutable documents (doc), but NOT immutable ParsedJsonDocuments
}
```

**When to use CreateUnrented():**
- Building documents in stages with async operations between stages
- Interactive scenarios where user input involves async I/O
- Complex workflows requiring async calls mid-construction

**Performance trade-off**: `CreateUnrented()` allocates slightly more resources than `Create()` because it doesn't use thread-static pooling. Use it only when you genuinely need to cross async boundaries during document construction.

**Preferred pattern**: If possible, gather all your data with async operations first, then use `Create()` to build the document synchronously. Reserve `CreateUnrented()` for cases where the async boundary is unavoidable during construction.

### Transforming API Response Format

Convert between different API formats:

```csharp
// Legacy API format
string legacyResponse = """
    {
        "user_id": 999,
        "user_name": "alice",
        "user_role": "admin"
    }
    """;

using ParsedJsonDocument<JsonElement> legacyDoc =
    ParsedJsonDocument<JsonElement>.Parse(legacyResponse);
JsonElement legacyRoot = legacyDoc.RootElement;

// Transform to modern format
using JsonWorkspace workspace = JsonWorkspace.Create();
using var transformedDoc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source((ref objectBuilder) =>
    {
        // Map old fields to new structure
        objectBuilder.AddProperty("id"u8, legacyRoot.GetProperty("user_id").GetInt32());

        objectBuilder.AddProperty("account"u8, (ref accountBuilder) =>
        {
            accountBuilder.AddProperty("username"u8,
                Encoding.UTF8.GetBytes(legacyRoot.GetProperty("user_name").GetString()!));
        });

        objectBuilder.AddProperty("authorization"u8, (ref authBuilder) =>
        {
            string role = legacyRoot.GetProperty("user_role").GetString()!;
            authBuilder.AddProperty("role"u8, Encoding.UTF8.GetBytes(role));
            authBuilder.AddProperty("isAdmin"u8, role == "admin");
        });

        // Add modern metadata
        objectBuilder.AddProperty("apiVersion"u8, "v2"u8);
    }));
```

### Building Configuration

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var config = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("appName"u8, "MyApp"u8);
        objectBuilder.AddProperty("version"u8, "1.0.0"u8);

        objectBuilder.AddProperty("database"u8, static (ref dbBuilder) =>
        {
            dbBuilder.AddProperty("host"u8, "localhost"u8);
            dbBuilder.AddProperty("port"u8, 5432);
            dbBuilder.AddProperty("name"u8, "mydb"u8);
        });

        objectBuilder.AddProperty("features"u8, static (ref featuresBuilder) =>
        {
            featuresBuilder.AddProperty("logging"u8, true);
            featuresBuilder.AddProperty("caching"u8, true);
            featuresBuilder.AddProperty("compression"u8, false);
        });
    }));

File.WriteAllText("config.json", config.RootElement.ToString());
```

## Version Tracking and Element Invalidation

The `JsonElement.Mutable` type includes **version tracking** to detect when references become invalid after modifications. This is a safety feature that prevents accessing stale data.

### Understanding Version Tracking

When you modify a mutable JSON document (by adding, removing, or changing elements), the document's internal version is incremented. Any `JsonElement.Mutable` references you obtained **before** the modification will detect this version change and throw an `InvalidOperationException` if you try to use them — with one important exception: the **root element** is always live.

### The Root Element Is Always Live

The root element of a `JsonDocumentBuilder` (obtained via `doc.RootElement`) is always at index 0 in the document and is never relocated by mutations. This means a cached root reference remains valid across any number of child mutations. You can navigate from the root to different children and mutate them without refreshing the root reference.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var doc = JsonElement.CreateBuilder(
    workspace,
    new JsonElement.Source(static (ref objectBuilder) =>
    {
        objectBuilder.AddProperty("person"u8, static (ref personBuilder) =>
        {
            personBuilder.AddProperty("name"u8, "Alice"u8);
        });
        objectBuilder.AddProperty("location"u8, static (ref locBuilder) =>
        {
            locBuilder.AddProperty("city"u8, "London"u8);
        });
    }));

JsonElement.Mutable root = doc.RootElement;

// ✅ Navigate from root, mutate child, then navigate to another child.
// Root is always live, so this works even though the first mutation bumped the version.
root.GetProperty("person"u8).SetProperty("name"u8, "Bob"u8);
root.GetProperty("location"u8).SetProperty("city"u8, "NYC"u8);
```

This also works when you cache intermediate children, provided you re-navigate from the root between mutations to different children:

```csharp
JsonElement.Mutable root = doc.RootElement;

// Cache a child and perform multiple operations on it
JsonElement.Mutable person = root.GetProperty("person"u8);
person.SetProperty("name"u8, "Bob"u8);
person.SetProperty("email"u8, "bob@example.com"u8);  // Same element — version is updated in-place

// Then navigate from root to a different child
JsonElement.Mutable location = root.GetProperty("location"u8);  // Root is still live
location.SetProperty("city"u8, "NYC"u8);
```

### Intermediate References Are Still Invalidated

While the root element is always live, intermediate child references behave as before: they are invalidated when a different element is mutated.

```csharp
JsonElement.Mutable root = doc.RootElement;

// Cache two intermediate children
JsonElement.Mutable person = root.GetProperty("person"u8);
JsonElement.Mutable location = root.GetProperty("location"u8);

// Mutate through 'person'
person.SetProperty("name"u8, "Bob"u8);

// ❌ BAD: 'location' was obtained before the mutation and is now stale
try
{
    location.SetProperty("city"u8, "NYC"u8);  // Throws InvalidOperationException!
}
catch (InvalidOperationException)
{
    // The cached 'location' reference is invalid — re-navigate from root
    location = root.GetProperty("location"u8);  // Root is always live
    location.SetProperty("city"u8, "NYC"u8);    // Now it works
}
```

### Three Patterns for Working with Mutable Elements

| Pattern | Valid? | Description |
|---------|--------|-------------|
| Navigate from cached root to different children | ✅ | Root is always live (index 0, never relocated) |
| Multiple mutations on the same element | ✅ | Each mutation updates the element's version in-place |
| Reuse a cached intermediate child after sibling mutation | ❌ | Intermediate references are invalidated by any other mutation |

### Best Practices for Version Tracking

1. **Use the root element as your navigation hub**
   ```csharp
   JsonElement.Mutable root = doc.RootElement;

   // Navigate from root for each mutation — root is always live
   root.GetProperty("field1"u8).SetProperty("value"u8, "a"u8);
   root.GetProperty("field2"u8).SetProperty("value"u8, "b"u8);
   ```

2. **Perform all operations on one child before moving to another**
   ```csharp
   JsonElement.Mutable numbers = root.GetProperty("numbers"u8);
   numbers.SetItem(0, 100);
   numbers.SetItem(1, 200);

   // Re-navigate from root (always live) to get a fresh child reference
   JsonElement.Mutable tags = root.GetProperty("tags"u8);
   tags.SetItem(0, "updated"u8);
   ```

3. **Re-navigate from root if you need a child reference after a mutation**
   ```csharp
   JsonElement.Mutable child = root.GetProperty("child"u8);
   child.SetProperty("x"u8, "value"u8);

   // 'otherChild' obtained before this point would be stale.
   // Re-navigate from root instead:
   JsonElement.Mutable otherChild = root.GetProperty("otherChild"u8);
   ```

### Why Version Tracking Exists

Version tracking is a **safety feature** that prevents bugs caused by:
- Using stale references to data that may have been relocated in memory
- Accessing elements at incorrect indices after array modifications
- Reading properties that may have been removed or reordered

Without version tracking, you could silently access incorrect data or crash with memory corruption. The `InvalidOperationException` is intentional and helps you write correct code. The root element exemption is safe because the root is always at index 0 and is never relocated.

## Comparison with System.Text.Json.Nodes

### Similar Capabilities

Both `JsonDocumentBuilder` and `System.Text.Json.Nodes` (JsonNode, JsonObject, JsonArray) provide mutable JSON document manipulation:

- **Mutable Documents**: Both allow in-place modification of JSON structures
- **Dynamic Construction**: Both support building JSON from code
- **Property Access**: Both provide ways to get/set properties and array elements
- **Type Conversions**: Both can convert between JSON and .NET types

### Key Differences

#### 1. **Memory Model**

**JsonNode (System.Text.Json.Nodes)**:
- Allocates individual objects for each JSON value (JsonObject, JsonArray, JsonValue)
- Each node is a separate heap allocation
- Can lead to significant GC pressure with large documents
- Suitable for small to medium documents or infrequent operations
- You can hold on to references to nodes without worrying about invalidation

**JsonDocumentBuilder**:
- Uses a flat, array-based representation in pooled memory
- All values stored in contiguous metadata arrays
- Minimal allocations through workspace pooling
- Optimized for high-throughput scenarios and large documents
- References to nodes are transient and only valid until the next modification (version tracking)

#### 2. **Performance Characteristics**

**JsonNode**:
```csharp
// Parse and modify with JsonNode - creates many objects
JsonNode? node = JsonNode.Parse(json);
JsonObject nameObj = node!["name"]?.AsObject() ?? throw new InvalidOperationException();
nameObj["firstName"] = "Matthew";  // Simple property set
string result = nameObj.ToJsonString();
```

**JsonDocumentBuilder**:
```csharp
// Parse and modify with JsonDocumentBuilder - pooled resources
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
using JsonWorkspace workspace = JsonWorkspace.Create();
using JsonDocumentBuilder<JsonElement.Mutable> builder = doc.RootElement.GetProperty("name").CreateBuilder(workspace);
builder.RootElement.SetProperty("firstName", "Matthew");
string result = builder.RootElement.ToString();
```

Benchmark results show `JsonDocumentBuilder` with significantly lower allocations, especially for repeated operations or large documents, with a small performance overhead (which is more than compensated by the improvement if you go on to validate the document with JSON Schema).

#### 3. **API Design**

**JsonNode**:
- Object-oriented, hierarchical tree structure
- Dictionary-like syntax: `node["property"]`
- Implicit type conversions
- More intuitive for simple scenarios

**JsonDocumentBuilder**:
- Struct-based, with mutable wrappers over flat arrays
- Explicit method calls: `GetProperty()`, `SetProperty()`
- UTF-8 byte-oriented APIs alongside string APIs
- Builder patterns for construction
- More control over memory and encoding

#### 4. **Interoperability**

We can interoperate with `System.Text.Json` using the `Corvus.Text.Json.Compatibility` library.

**From JsonElement to JsonNode**:
```csharp
using Corvus.Text.Json.Compatibility;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
JsonNode? node = doc.RootElement.AsJsonNode();
```

**From Corvus.Text.Json.JsonElement to System.Text.Json.JsonElement**:
```csharp
using Corvus.Text.Json.Compatibility;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
System.Text.Json.JsonElement element = doc.RootElement.AsSTJsonElement();
```

**From System.Text.Json.JsonElement to Corvus.Text.Json.JsonElement**:
```csharp
using Corvus.Text.Json.Compatibility;
using ParsedJsonDocument<JsonElement> doc = ParsedJsonDocument<JsonElement>.Parse(json);
System.Text.Json.JsonElement element = doc.RootElement.FromSTJsonElement();
```

#### 5. **Use Case Recommendations**

**Choose JsonDocumentBuilder when**:
- High-throughput scenarios (web services, message processing)
- Memory efficiency is critical
- Building or transforming JSON from external APIs
- Performing repeated operations where allocation costs matter
- Processing large JSON documents in memory
- You are going to go on to validate the document against JSON Schema
- You need to construct JSON dynamically from heterogeneous sources (databases, APIs, config) rather than serializing a single object graph

**Choose JsonNode when**:
- Working with small JSON documents
- Prioritizing code simplicity and readability
- Performance is not critical
- Making occasional modifications
- Integrating with APIs that expect JsonNode

**Choose POCO objects with `System.Text.Json` serialization when**:
- You have small, pre-existing .NET object hierarchies that already model your domain
- Computational speed is the priority (the serializer's code-gen path is heavily optimised)
- You do not need JSON Schema validation
- The shape of your JSON is fixed and well-known at compile time
- You are already using `JsonSerializer` throughout your codebase and want consistency

> **Tip**: POCO serialization is the fastest way to produce JSON when you already have .NET objects in hand. The `System.Text.Json` source generator (`JsonSerializerContext`) can outperform any DOM-based approach for simple serialize/deserialize cycles. Reach for `JsonDocumentBuilder` or `JsonNode` when you need to *construct* or *transform* JSON dynamically, or when you need schema validation.
