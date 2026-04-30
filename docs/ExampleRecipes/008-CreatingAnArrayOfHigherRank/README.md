# JSON Schema Patterns in .NET - Multi-Dimensional Arrays

This recipe demonstrates how to define multi-dimensional (higher-rank) JSON arrays in .NET by nesting array schemas, giving you strongly-typed indexers and tensor operations for fixed-size numeric arrays.

## The Pattern

You can think of an array of rank > 1 as being like an "array of arrays".

The rank of an array determines the number of indices you need to provide to access a value.

In C#, for example, we might define an array of rank 2 like this:

```csharp
double[][] values = new double[3][4];
```

To retrieve a value we need to provide 2 indices:

```csharp
double value = values[1][0];
```

We have seen how JSON schema allows you to [define an array that contains items of a particular type](../007-CreatingAStronglyTypedArray/) using its `items` property.

This was an array of rank 1. Only one index was required to retrieve the value.

To create an array of higher rank, just like the C# example above, we define each item to itself be an array.

Each array we define can be constrained with `minItems` and/or `maxItems` to ensure the dimensions are appropriate.

Specifying both, and making them the same value implies an array of *exactly* that number of items.

Ragged multi-dimensional arrays (where each item may be an array of a different length) can be created by *not* specifying the upper and lower limits on one of the arrays, allowing them to be any length.

**Remember:**

- Specifying both `maxItems` and `minItems`, and making them the same value constrains the array to *exactly* the given length.
- Specifying just `maxItems` indicates that the array can contain *up to* that many items.
- Specifying just `minItems` indicates that the array will contain *at least* that many items.
- Specifying both, and making them different values gives an upper and a lower bound to the number of items.

## The Schema

File: `matrix-2d.json`

```json
{
  "title": "A 4x2 array of JsonDouble",
  "type": "array",
  "items": { "$ref": "#/$defs/row" },
  "minItems": 4,
  "maxItems": 4,
  "$defs": {
    "row": {
      "type": "array",
      "minItems": 2,
      "maxItems": 2,
      "items": { "type": "number", "format": "double" }
    }
  }
}
```

The code generator produces:

- `Matrix2d` — the 2D array type (rank 2, dimension 4)
- `Matrix2d.Row` — the nested row type (rank 1, dimension 2)
- `Matrix2d.Row.RowEntity` — the double item type

Fixed-size numeric arrays are recognized as **tensor types** and gain additional API surface:
- `Rank` — the number of dimensions (2 for Matrix2d, 1 for Row)
- `Dimension` — the fixed length at the current rank (4 for Matrix2d, 2 for Row)
- `ValueBufferSize` — total element count for the flattened buffer (8 = 4×2)
- `TryGetNumericValues(Span<double>, out int)` — extract all values into a flat buffer

## Generated Code Usage

[Example code](./Program.cs)

### Parsing and accessing elements

```csharp
using Corvus.Text.Json;
using CreatingAnArrayOfHigherRank.Models;

// Create a 2D array from JSON
string matrixJson =
    """
    [
        [1.3, 1.4],
        [2.4, 3.2],
        [9.4, 6.2],
        [4.4, 9.4]
    ]
    """;

using var parsedMatrix = ParsedJsonDocument<Matrix2d>.Parse(matrixJson);
Matrix2d matrix = parsedMatrix.RootElement;

// Access an element of a 2D array
// Can chain the indexers: matrix[2][1]
Matrix2d.Row row2 = matrix[2];
double value = row2[1];
double value2 = matrix[2][1];  // same as above
```

### Mutable multi-dimensional operations

```csharp
// Set the item at the given indices via mutable builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = matrix.CreateBuilder(workspace);
Matrix2d.Mutable root = mutableDoc.RootElement;

// To set matrix[2][1] = 3.4:
// The indexer root[2] returns a Row.Mutable, so we can call SetItem on it
root[2].SetItem(1, 3.4);

Matrix2d updatedMatrix = mutableDoc.RootElement;
double updatedMatrixValue = updatedMatrix[2][1];

Console.WriteLine(updatedMatrix);
```

### Tensor operations

```csharp
// Tensor operations - get all numeric values at once
Span<double> buffer = stackalloc double[Matrix2d.ValueBufferSize];
if (matrix.TryGetNumericValues(buffer, out int written))
{
    Console.WriteLine($"Rank: {Matrix2d.Rank}, Dimension: {Matrix2d.Dimension}");
}
```

## Key Differences from V4

### V4 (Corvus.Json)
```csharp
// Create from nested collection expressions
Matrix2d matrix = [[1.3, 1.4], [2.4, 3.2], [9.4, 6.2], [4.4, 9.4]];

// Access elements by chaining indexers
double value = matrix[2][1];

// Immutable set — returns a new matrix
Matrix2d updated = matrix.SetItem(2, matrix[2].SetItem(1, 3.4));

// Tensor extraction
Span<double> buffer = stackalloc double[Matrix2d.ValueBufferSize];
matrix.TryGetNumericValues(buffer, out int written);
```

### V5 (Corvus.Text.Json)
```csharp
// Parse from JSON
using var parsedMatrix = ParsedJsonDocument<Matrix2d>.Parse(matrixJson);
Matrix2d matrix = parsedMatrix.RootElement;

// Access elements (same chaining)
double value = matrix[2][1];

// Mutable set — in-place via workspace builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = matrix.CreateBuilder(workspace);
mutableDoc.RootElement[2].SetItem(1, 3.4);

// Tensor extraction (same API)
Span<double> buffer = stackalloc double[Matrix2d.ValueBufferSize];
matrix.TryGetNumericValues(buffer, out int written);
```

**Key differences:**
- V5 does not support collection expressions for constructing multi-dimensional arrays — parse from JSON or use `Build(span)`
- V5 mutations are in-place via the builder pattern instead of returning new immutable copies
- V5 requires `ParsedJsonDocument<T>.Parse()` with `using` for explicit lifetime management
- Indexer chaining and tensor extraction APIs are the same in both versions

## Running the Example

```bash
cd docs/ExampleRecipes/008-CreatingAnArrayOfHigherRank
dotnet run
```

## Related Patterns

- [007-CreatingAStronglyTypedArray](../007-CreatingAStronglyTypedArray/) - Single-dimensional strongly-typed arrays
- [009-WorkingWithTensors](../009-WorkingWithTensors/) - Tensor construction and span-based operations
- [010-CreatingTuples](../010-CreatingTuples/) - Fixed-length heterogeneous arrays (tuples)

## Frequently Asked Questions

### What's the difference between rank and dimension?

**Rank** is the number of indices needed to access a scalar value (e.g., a 2D matrix has rank 2). **Dimension** is the length at a particular rank level (e.g., a 4×2 matrix has dimension 4 at the outer level and dimension 2 at the inner level). The static properties `Matrix2d.Rank` and `Matrix2d.Dimension` report these values.

### Can I create ragged (jagged) multi-dimensional arrays?

Yes. Omit `minItems` and/or `maxItems` from the inner array schema to allow rows of different lengths. However, ragged arrays will not be recognized as tensor types, so you won't get `TryGetNumericValues()`, `Build(span)`, or `ValueBufferSize`. Only fixed-size numeric arrays gain tensor operations.

### Are tensor operations available on all multi-dimensional arrays?

No. Tensor operations (`TryGetNumericValues`, `Build(span)`, `Rank`, `Dimension`, `ValueBufferSize`) are only generated when **all** dimensions have matching `minItems`/`maxItems` **and** the innermost items have a numeric type with a `format` (e.g., `double`, `int32`). Arrays of objects or variable-length arrays don't qualify.

### How do I access nested elements without intermediate variables?

Chain the indexers directly: `matrix[2][1]` returns the scalar value at row 2, column 1. For mutable operations, the same chaining works: `root[2].SetItem(1, 3.4)` sets the value at `[2][1]` in-place.