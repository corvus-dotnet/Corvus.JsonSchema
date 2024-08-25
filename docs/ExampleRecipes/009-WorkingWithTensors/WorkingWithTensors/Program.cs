using JsonSchemaSample.Api;

// Initialize an array of rank 3 using collection initialization expressions

TensorRank3 tensor =
    [
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5],
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5],
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5],
        ],
        [
            [1.3, 1.4, 9.2, 8.4],
            [2.4, 3.2, 6.3, 7.2],
            [9.4, 6.2, 4.8, 1.4],
            [4.4, 9.4, -3.2, 6.5],
        ]
    ];

// Access an element of a array of rank 3.
double tensorValue = tensor[3][0][1];
// Set the item at the given indices in the array of rank 3
TensorRank3 updatedTensor = tensor.SetItem(3, 0, 1, 3.4);

// Fill a Span<double> with the tensor values.
Span<double> tensorAsSpan = stackalloc double[TensorRank3.ValueBufferSize];
if (updatedTensor.TryGetNumericValues(tensorAsSpan, out int writtenTensor))
{
    bool first = true;

    // Successfully get the tensor as a flat Span<double>
    foreach (double spanValue in tensorAsSpan)
    {
        if (first)
        {
            first = false;
        }
        else
        {
            Console.Write(", ");
        }

        Console.Write(spanValue);
    }

    Console.WriteLine();
}
else
{
    Console.WriteLine("Unable to get values.");
}

// Construct a tensor from a ReadOnlySpan<double>
TensorRank3 fromSpan = TensorRank3.FromValues(tensorAsSpan);

// Find the rank of each array (note that the sub arrays of diminishing rank)
Console.WriteLine($"Rank: {TensorRank3.Rank}, {TensorRank3.SecondRank.Rank}, {TensorRank3.ThirdRank.Rank}");

// Find the dimension (extent) of each particular rank of the array
Console.WriteLine($"Dimension: {TensorRank3.Dimension}, {TensorRank3.SecondRank.Dimension}, {TensorRank3.ThirdRank.Dimension}");
