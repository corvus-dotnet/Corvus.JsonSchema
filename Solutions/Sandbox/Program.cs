using Corvus.Json;
using Repro672;

// Arrange
const decimal expectedValue = 999999999998.999999m;
var capacity = LargeIntegerExample.Create(
    new JsonNumber(
        new BinaryJsonNumber(expectedValue)
    )
);

// Act
var result = LargeIntegerExample.Parse(capacity.Serialize());
var validationContext = result.Validate(ValidationContext.ValidContext.UsingResults(), ValidationLevel.Detailed);

// Assert
Console.WriteLine($"Results count: {validationContext.Results.Count()}");
Console.WriteLine($"IsValid: {validationContext.IsValid}");
Console.WriteLine($"{result.LargeDecimal.As<JsonDecimal>().AsDecimal()} == {expectedValue} is {result.LargeDecimal.As<JsonDecimal>().AsDecimal() == expectedValue}");