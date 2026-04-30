using Corvus.Json;
using JsonSchemaSample.Api;

// Gets a constant instance of the enum value with implicit conversion to the enumerated type
NumericOptions options = NumericOptions.Foo.ConstInstance;

// Because our numeric values are not explicitly declared to be "type": "number",
// there are no implicit conversions from arbitrary numeric values.
// This helps you avoid accidentally creating invalid values
// NumericOptions.Foo invalidOption = 19; // This does not compile

// However, you can *explicitly* convert them if you are getting a numeric value
// from elsewhere in the system (which may create an invalid value)
NumericOptions invalidOption = (NumericOptions)19;
if (!invalidOption.IsValid())
{
    Console.WriteLine("The invalid option is not valid!");
}

// The value itself is numeric
Console.WriteLine(options);

Console.WriteLine(
    // Dispatch the value to a function in the usual way
    ProcessOptions(options));

try
{
    // The matcher handles the invalid case
    ProcessOptions(invalidOption);
}
catch (InvalidOperationException ex)
{
    Console.WriteLine(ex.Message);
}

string ProcessOptions(NumericOptions options)
{
    // You could pass some state.
    // Here we are just using the options value.
    return options.Match(
        (in NumericOptions.Foo o) => $"It was a foo: {o}",
        (in NumericOptions.Bar o) => $"It was a bar: {o}",
        (in NumericOptions.Baz o) => $"It was a baz: {o}",
        (in NumericOptions o) => throw new InvalidOperationException($"Unknown numeric option: {o}"));
}