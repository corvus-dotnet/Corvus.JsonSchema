using JsonSchemaSample.Api;

// Initialize a 2D array using collection initialization expressions
Matrix2d matrix =
    [
        [1.3, 1.4],
        [2.4, 3.2],
        [9.4, 6.2],
        [4.4, 9.4]
    ];

// Access an element of a 2D array.
double value = matrix[2][1];

// Set the item at the given indices in the 2D array
Matrix2d updatedMatrix = matrix.SetItem(2, 1, 3.4);

double updateMatrixValue = updatedMatrix[2][1];

Console.WriteLine(updatedMatrix);
