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

// Creating polymorphic shapes from a branch (issue #812)
//
// You don't have to hand-write JSON to construct a discriminated union. Each
// branch exposes a strongly-typed CreateBuilder that sets the `const`
// discriminator (`type`) for you, and the resulting mutable branch converts
// implicitly to the union in a single hop. Because the generator recognises the
// oneOf + const pattern, a branch value can be used wherever the union is
// expected.
using var ws = JsonWorkspace.Create();

using var circleBuilder = Shape.RequiredRadiusAndType.CreateBuilder(ws, radius: 5.0);
Shape builtCircle = circleBuilder.RootElement; // implicit RequiredRadiusAndType.Mutable -> Shape

using var rectangleBuilder = Shape.RequiredHeightAndTypeAndWidth.CreateBuilder(ws, height: 20.0, width: 10.0);
Shape builtRectangle = rectangleBuilder.RootElement;

Console.WriteLine(DescribeShape(builtCircle));
Console.WriteLine(DescribeShape(builtRectangle));

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