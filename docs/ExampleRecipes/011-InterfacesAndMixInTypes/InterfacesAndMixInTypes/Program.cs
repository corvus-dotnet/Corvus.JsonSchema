using Corvus.Json;
using JsonSchemaSample.Api;

// Create a composite type. Required and optional properties are composed from all participating schema.
CompositeType composite =
    CompositeType.Create(123.7m, 4, "Greeting", "Hello world");
// Example omitting the description.
CompositeType composite2 =
    CompositeType.Create(1_438_274.3m, 3, "Salutation");

// Implicit conversion to each of the composed types,
// Notice how this is similar to implementing multiple interfaces
Console.WriteLine(FormatDocumentation(composite));
Console.WriteLine(FormatCount(composite));

Console.WriteLine(FormatDocumentation(composite2));
Console.WriteLine(FormatCount(composite2));

// Note that when we define functions over our types,
// we tend to pass parameters with the "in"
// modifier in order to avoid unnecessary copying.
string FormatDocumentation(in Documentation documentation)
{
    if (documentation.Description.IsNotUndefined())
    {
        return $"{documentation.Title}: {documentation.Description}";
    }
    else
    {
        return (string)documentation.Title;
    }
}

string FormatCount(in Countable countable)
{
    return $"Count: {countable.Count}";
}