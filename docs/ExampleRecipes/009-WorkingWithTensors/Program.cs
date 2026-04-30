using Corvus.Text.Json;
using WorkingWithTensors.Models;

// Initialize an array of rank 3 from JSON (V5 doesn't support collection expressions)
string tensorJson =
    """
    [
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5]
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5]
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5]
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5]
        ]
    ]
    """;

using var parsedTensor = ParsedJsonDocument<TensorRank3>.Parse(tensorJson);
TensorRank3 tensor = parsedTensor.RootElement;

Console.WriteLine("Original tensor:");
Console.WriteLine(tensor);
Console.WriteLine();

// Access an element of a array of rank 3.
double tensorValue = tensor[3][0][1];
Console.WriteLine($"Value at [3][0][1]: {tensorValue}");
Console.WriteLine();

// Set the item at the given indices via mutable builder
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = tensor.CreateBuilder(workspace);
TensorRank3.Mutable root = mutableDoc.RootElement;

// To set tensor[3][0][1] = 3.4, chain the indexers:
root[3][0].SetItem(1, 3.4);

TensorRank3 updatedTensor = mutableDoc.RootElement;
double updatedValue = updatedTensor[3][0][1];
Console.WriteLine($"Updated tensor (set [3][0][1] = 3.4):");
Console.WriteLine(updatedTensor);
Console.WriteLine($"Updated value at [3][0][1]: {updatedValue}");
Console.WriteLine();

// Fill a Span<double> with the tensor values.
Span<double> tensorAsSpan = stackalloc double[TensorRank3.ValueBufferSize];
if (tensor.TryGetNumericValues(tensorAsSpan, out int writtenTensor))
{
    Console.Write("All tensor values (flat): [");
    for (int i = 0; i < writtenTensor; i++)
    {
        if (i > 0) Console.Write(", ");
        Console.Write(tensorAsSpan[i]);
    }
    Console.WriteLine("]");
    Console.WriteLine();
}
else
{
    Console.WriteLine("Unable to get values.");
}

// Find the rank of each array (note the sub arrays of diminishing rank)
Console.WriteLine($"Rank: {TensorRank3.Rank}, {TensorRank3.SecondRank.Rank}, {TensorRank3.ThirdRank.Rank}");

// Find the dimension (extent) of each particular rank of the array
Console.WriteLine($"Dimension: {TensorRank3.Dimension}, {TensorRank3.SecondRank.Dimension}, {TensorRank3.ThirdRank.Dimension}");

// Show the value buffer size
Console.WriteLine($"Value buffer size: {TensorRank3.ValueBufferSize}");
Console.WriteLine();

// ---- Build from span: construct a tensor directly from a flat span ----

// Fill a span with sequential values
Span<double> newValues = stackalloc double[TensorRank3.ValueBufferSize];
for (int i = 0; i < newValues.Length; i++)
{
    newValues[i] = i * 0.5;
}

// CreateBuilder convenience: span → mutable document in a single call
using JsonWorkspace workspace2 = JsonWorkspace.Create();
using var builtTensor = TensorRank3.CreateBuilder(workspace2, newValues);
TensorRank3 constructed = builtTensor.RootElement;

Console.WriteLine("Tensor constructed from flat span via Build:");
Console.WriteLine(constructed);
Console.WriteLine();

// Round-trip: extract values, verify they match
Span<double> roundTrip = stackalloc double[TensorRank3.ValueBufferSize];
if (constructed.TryGetNumericValues(roundTrip, out int rtWritten))
{
    bool match = true;
    for (int i = 0; i < rtWritten; i++)
    {
        if (roundTrip[i] != newValues[i])
        {
            match = false;
            break;
        }
    }

    Console.WriteLine($"Round-trip match: {match}");
}