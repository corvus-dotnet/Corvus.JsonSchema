# JSON Schema Patterns in .NET - Working with Tensors

This recipe demonstrates how to construct and manipulate fixed-size numeric arrays as tensors in .NET, including span-based construction, `TryGetNumericValues()` extraction, and integration with APIs like `System.Numerics.Tensors`.

## The Pattern

[We have seen how to create arrays of higher rank with fixed size](../008-CreatingAnArrayOfHigherRank/).

The array could contain any item type that you can define in schema. However, our previous example used a numeric type: we constrained it using the type `number` and the format `double`.

Here's another example, this time of rank 3.

The code generator produces:

- `TensorRank3` — the 3D array type (rank 3, dimension 4, buffer size 64)
- `TensorRank3.SecondRank` — the nested 2D array type (rank 2, dimension 4, buffer size 16)
- `TensorRank3.ThirdRank` — the innermost 1D array type (rank 1, dimension 4, buffer size 4)
- `TensorRank3.ThirdRank.ThirdRankEntity` — the double item type

Fixed-size numeric arrays gain **tensor operations**:
- `TryGetNumericValues(Span<double>, out int)` — extract all values into a flat buffer
- `Build(ReadOnlySpan<double>)` — create a `Source` directly from a flat numeric span (preferred for construction from raw data)
- `CreateBuilder(workspace, ReadOnlySpan<double>)` — single-call convenience that creates a mutable document from a span
- `CreateTensor(ReadOnlySpan<double>)` on the mutable builder — reconstruct from a flat buffer inside a `Build` delegate
- `Rank`, `Dimension`, `ValueBufferSize` — static metadata about the tensor structure

> **Note:** `Build(ReadOnlySpan<T>)` and `CreateBuilder(workspace, ReadOnlySpan<T>)` are also available on variable-length numeric arrays — see [Variable-length numeric arrays](#variable-length-numeric-arrays) below.

We can use this in the same way, and also convert it directly to and from `Span<TNumeric>` for use in APIs such as [System.Numerics.Tensors](https://learn.microsoft.com/en-us/dotnet/api/system.numerics.tensors).

## The Schema

File: `tensor-rank-3.json`

```json
{
  "title": "A 4x4x4 tensor of JsonDouble",
  "type": "array",
  "items": { "$ref": "#/$defs/SecondRank" },
  "minItems": 4,
  "maxItems": 4,
  "$defs": {
    "SecondRank": {
      "type": "array",
      "items": { "$ref": "#/$defs/ThirdRank" },
      "minItems": 4,
      "maxItems": 4
    },
    "ThirdRank": {
      "type": "array",
      "minItems": 4,
      "maxItems": 4,
      "items": {
        "type": "number",
        "format": "double"
      }
    }
  }
}
```

## Generated Code Usage

[Example code](./Program.cs)

### Parsing and accessing tensor elements

```csharp
using Corvus.Text.Json;
using WorkingWithTensors.Models;

// Parse a 3D tensor from JSON
string tensorJson = """...""";  // 4x4x4 array JSON
using var parsedTensor = ParsedJsonDocument<TensorRank3>.Parse(tensorJson);
TensorRank3 tensor = parsedTensor.RootElement;

// Access an element of an array of rank 3
double tensorValue = tensor[3][0][1];

// Set the item at the given indices via mutable builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = tensor.CreateBuilder(workspace);
TensorRank3.Mutable root = mutableDoc.RootElement;
root[3][0].SetItem(1, 3.4);
TensorRank3 updatedTensor = mutableDoc.RootElement;

// Fill a Span<double> with the tensor values
Span<double> tensorAsSpan = stackalloc double[TensorRank3.ValueBufferSize];
if (tensor.TryGetNumericValues(tensorAsSpan, out int written))
{
    // Manually format the output
    Console.Write("All tensor values (flat): [");
    for (int i = 0; i < written; i++)
    {
        if (i > 0) Console.Write(", ");
        Console.Write(tensorAsSpan[i]);
    }
    Console.WriteLine("]");
}

// Find the rank of each array (note the sub arrays of diminishing rank)
Console.WriteLine($"Rank: {TensorRank3.Rank}, {TensorRank3.SecondRank.Rank}, {TensorRank3.ThirdRank.Rank}");

// Find the dimension (extent) of each particular rank of the array
Console.WriteLine($"Dimension: {TensorRank3.Dimension}, {TensorRank3.SecondRank.Dimension}, {TensorRank3.ThirdRank.Dimension}");
```

### Constructing a tensor from a flat numeric span

The simplest way to create a tensor from raw numeric data is `CreateBuilder(workspace, span)` — a single call that goes directly from a `ReadOnlySpan<T>` to a mutable document:

```csharp
Span<double> flatValues = stackalloc double[TensorRank3.ValueBufferSize];
// ... populate flatValues ...

using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = TensorRank3.CreateBuilder(workspace, flatValues);
TensorRank3 result = builder.RootElement;
```

If you prefer to separate construction from materialisation (e.g., to pass the source to another method), use `Build()` + `CreateBuilder()`:

```csharp
// Two-step: span → Source → builder
TensorRank3.Source source = TensorRank3.Build(flatValues);
using var builder = TensorRank3.CreateBuilder(workspace, source);
```

If you need more control (e.g., building the tensor alongside other mutations in a single delegate), use the `Build` + `CreateTensor` pattern:

```csharp
// Delegate route
using var builder = TensorRank3.CreateBuilder(
    workspace,
    TensorRank3.Build(
        static (ref TensorRank3.Builder b) => b.CreateTensor(flatValues)));
```

The span must contain exactly `ValueBufferSize` elements; passing the wrong number throws `ArgumentException`.

### Variable-length numeric arrays

The `Build(ReadOnlySpan<T>)` and `CreateBuilder(workspace, ReadOnlySpan<T>)` overloads are not limited to fixed-size tensors — they work on **any** numeric array type (i.e. an array whose items have a numeric type and format). For variable-length arrays, the span can contain any number of elements:

```csharp
// Given a schema with no minItems/maxItems:
// { "type": "array", "items": { "type": "number", "format": "double" } }

using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = ScoresArray.CreateBuilder(workspace, [1.5, 2.5, 3.5]);
ScoresArray scores = builder.RootElement;
// scores is [1.5, 2.5, 3.5]
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create a tensor from a flat span
TensorRank3 tensor = TensorRank3.FromValues(flatValues);

// Create with collection expressions
TensorRank3 tensor = [[[1.0, 2.0, 3.0, 4.0], ...], ...];

// Access elements
double value = tensor[3][0][1];

// Extract to flat buffer
Span<double> buffer = stackalloc double[TensorRank3.ValueBufferSize];
tensor.TryGetNumericValues(buffer, out int written);

// Immutable set — returns a new tensor
TensorRank3 updated = tensor.SetItem(3, tensor[3].SetItem(0, tensor[3][0].SetItem(1, 3.4)));
```

### V5 (Corvus.Text.Json)
```csharp
// Create a tensor from a flat span (preferred)
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = TensorRank3.CreateBuilder(workspace, flatValues);
TensorRank3 tensor = builder.RootElement;

// Or use Build() for two-step construction
TensorRank3.Source source = TensorRank3.Build(flatValues);
using var builder = TensorRank3.CreateBuilder(workspace, source);

// Or use the delegate pattern with CreateTensor()
using var builder = TensorRank3.CreateBuilder(
    workspace,
    TensorRank3.Build(static (ref TensorRank3.Builder b) => b.CreateTensor(flatValues)));

// Access elements (same chaining)
double value = tensor[3][0][1];

// Extract to flat buffer (same API)
Span<double> buffer = stackalloc double[TensorRank3.ValueBufferSize];
tensor.TryGetNumericValues(buffer, out int written);

// Mutable set — in-place via workspace builder
using var mutableDoc = tensor.CreateBuilder(workspace);
mutableDoc.RootElement[3][0].SetItem(1, 3.4);
```

**Key differences:**
- V4's `FromValues(span)` becomes `Build(span)` for direct construction, or `CreateBuilder(workspace, span)` for single-call convenience
- V5 does not support collection expressions — parse from JSON or use `Build(span)`
- V5 mutations are in-place via the builder pattern instead of returning new immutable copies
- V5 requires a `JsonWorkspace` for all construction and mutation operations
- Indexer chaining, `TryGetNumericValues()`, and tensor metadata (`Rank`, `Dimension`, `ValueBufferSize`) are the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/009-WorkingWithTensors
dotnet run
```

## Related Patterns

- [007-CreatingAStronglyTypedArray](../007-CreatingAStronglyTypedArray/) - Single-dimensional strongly-typed arrays
- [008-CreatingAnArrayOfHigherRank](../008-CreatingAnArrayOfHigherRank/) - Multi-dimensional array construction
- [010-CreatingTuples](../010-CreatingTuples/) - Fixed-length heterogeneous arrays (tuples)

## Frequently Asked Questions

### How does the tensor API integrate with `System.Numerics.Tensors`?

Use `TryGetNumericValues()` to extract all tensor values into a flat `Span<double>`, which you can then pass directly to `System.Numerics.Tensors` APIs (e.g., `TensorPrimitives.Sum()`, `TensorPrimitives.Multiply()`). To go the other direction, use `Build(span)` or `CreateBuilder(workspace, span)` to construct a tensor from a flat span after performing numeric operations.

### Is the span from `TryGetNumericValues()` safe to use after the document is disposed?

**A:** Yes. `TryGetNumericValues()` *copies* the values into the `Span<T>` you provide, so the data has no dependency on the document once the call returns. You can safely dispose the document and continue using the buffer. The only constraint is the normal span lifetime rule: don't return a `stackalloc` span from a method.

### What's the difference between variable-length and fixed-length numeric arrays?

Fixed-length arrays (with matching `minItems`/`maxItems`) gain tensor metadata (`Rank`, `Dimension`, `ValueBufferSize`) and `CreateTensor()` on the builder. Variable-length arrays still get `Build(span)` and `CreateBuilder(workspace, span)` for construction, but the span can be any length and there is no `ValueBufferSize` constant.

### What happens if I pass the wrong number of elements to `Build(span)`?

For fixed-size tensors, the span must contain exactly `ValueBufferSize` elements. Passing more or fewer throws `ArgumentException`. For variable-length numeric arrays, any span length is accepted.

### Why can't I use collection expressions to create tensors in V5?

V5 generated types do not support implicit conversions from collection expressions (e.g., `[[1.0, 2.0], ...]`). This is by design — V5 emphasizes explicit resource management through `JsonWorkspace` and the builder pattern. Use `Build(span)` or `CreateBuilder(workspace, span)` for efficient construction from raw data, or `ParsedJsonDocument<T>.Parse()` for construction from JSON strings.