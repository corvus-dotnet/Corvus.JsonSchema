using Corvus.Text.Json;
using CreatingAnArrayOfHigherRank.Models;

// ------------------------------------------------------------------
// Create a 2D array from JSON
// ------------------------------------------------------------------
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

Console.WriteLine("Original matrix:");
Console.WriteLine(matrix);

// ------------------------------------------------------------------
// Access an element of a 2D array
// ------------------------------------------------------------------
// To access matrix[2][1], first get the row at index 2, then get item at index 1
Matrix2d.Row row2 = matrix[2];
double value = row2[1];
Console.WriteLine($"\nValue at [2][1]: {value}");

// You can also chain the indexers:
double value2 = matrix[2][1];
Console.WriteLine($"Value at [2][1] (chained): {value2}");

// ------------------------------------------------------------------
// Set the item at the given indices in the 2D array via mutable builder
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = matrix.CreateBuilder(workspace);
Matrix2d.Mutable root = mutableDoc.RootElement;

// To set matrix[2][1] = 3.4:
// 1. Get the mutable element at index 2 (which is a mutable Row)
// 2. Set item at index 1 within that row
root[2].SetItem(1, 3.4);

Matrix2d updatedMatrix = mutableDoc.RootElement;
double updatedMatrixValue = updatedMatrix[2][1];

Console.WriteLine($"\nUpdated matrix (set [2][1] = 3.4):");
Console.WriteLine(updatedMatrix);
Console.WriteLine($"Updated value at [2][1]: {updatedMatrixValue}");

// ------------------------------------------------------------------
// Tensor operations - get all numeric values at once
// ------------------------------------------------------------------
Span<double> buffer = stackalloc double[Matrix2d.ValueBufferSize];
if (matrix.TryGetNumericValues(buffer, out int written))
{
    Console.WriteLine($"\nAll matrix values (tensor): [{string.Join(", ", buffer.Slice(0, written).ToArray())}]");
    Console.WriteLine($"Rank: {Matrix2d.Rank}, Dimension: {Matrix2d.Dimension}, Buffer size: {Matrix2d.ValueBufferSize}");
}