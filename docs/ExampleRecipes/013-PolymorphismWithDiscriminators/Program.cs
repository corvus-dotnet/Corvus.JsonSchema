using Corvus.Text.Json;
using PolymorphismWithDiscriminators.Models;

// Parse a circle shape
string circleJson = """
    {
      "type": "circle",
      "radius": 5.0
    }
    """;

using var parsedCircle = ParsedJsonDocument<Shape>.Parse(circleJson);
Shape circle = parsedCircle.RootElement;

// Parse a rectangle shape
string rectangleJson = """
    {
      "type": "rectangle",
      "width": 10.0,
      "height": 20.0
    }
    """;

using var parsedRectangle = ParsedJsonDocument<Shape>.Parse(rectangleJson);
Shape rectangle = parsedRectangle.RootElement;

// Pattern matching with discriminated types
Console.WriteLine(DescribeShape(circle));
Console.WriteLine(DescribeShape(rectangle));

// Pattern matching function that handles all shape types
string DescribeShape(in Shape shape)
{
    return shape.Match(
        matchRequiredRadiusAndType: static (in Shape.RequiredRadiusAndType circleShape) =>
        {
            // Circle has a radius property
            return $"A circle with radius {circleShape.Radius}";
        },
        matchRequiredHeightAndTypeAndWidth: static (in Shape.RequiredHeightAndTypeAndWidth rectShape) =>
        {
            // Rectangle has width and height properties
            return $"A rectangle {rectShape.Width}x{rectShape.Height}";
        },
        defaultMatch: static (in Shape unknownShape) =>
            throw new InvalidOperationException($"Unknown shape: {unknownShape}"));
}