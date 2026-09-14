using Corvus.Text.Json;
using NodaTime;
using PatternMatching.Models;

// Parse instances from JSON and convert to discriminated union type
string personJson = """
    {
      "familyName": "Brontë",
      "givenName": "Anne",
      "birthDate": "1820-01-17",
      "height": 1.57
    }
    """;

using var parsedPerson = ParsedJsonDocument<PersonOpen>.Parse(personJson);
PersonOpen personForDiscriminatedUnion = parsedPerson.RootElement;

// Implicit conversion to the discriminated union type from the matchable types
Console.WriteLine(
    ProcessDiscriminatedUnion(DiscriminatedUnionByType.From(personForDiscriminatedUnion)));

string arrayJson = """
    [
      {
        "familyName": "Brontë",
        "givenName": "Anne",
        "birthDate": "1820-01-17",
        "height": 1.57
      }
    ]
    """;
using var parsedArray = ParsedJsonDocument<DiscriminatedUnionByType>.Parse(arrayJson);
Console.WriteLine(
    ProcessDiscriminatedUnion(parsedArray.RootElement));

// With the C# 15 compiler (the .NET 11 SDK or later, any target framework except .NET Framework) the generated type is also a
// C# union: a switch over the branch types is exhaustive, and it does not box. A value that matches no branch
// has a null union value, so add a null arm when the input is untrusted.
string ProcessWithSwitch(in DiscriminatedUnionByType value) => value switch
{
    JsonString s => $"It was a string: {s}",
    JsonInt32 i => $"It was an int32: {i}",
    PersonOpen p => $"It was a person. {p.FamilyName}, {p.GivenName}",
    DiscriminatedUnionByType.People people => $"It was an array of people. {people.GetArrayLength()}",
    null => throw new InvalidOperationException($"Unexpected instance {value}"),
};

Console.WriteLine(ProcessWithSwitch(parsedPerson.RootElement));
Console.WriteLine(ProcessWithSwitch(parsedArray.RootElement));

// A function that processes the discriminated union type
string ProcessDiscriminatedUnion(in DiscriminatedUnionByType value)
{
    // Pattern matching against a discriminated union type
    // requires you to deal with all known types and the fallback (failure) case
    return value.Match(
        static (in JsonString value) => $"It was a string: {value}",
        static (in JsonInt32 value) => $"It was an int32: {value}",
        static (in PersonOpen value) => $"It was a person. {value.FamilyName}, {value.GivenName}",
        static (in DiscriminatedUnionByType.People value) => $"It was an array of people. {value.GetArrayLength()}",
        static (in DiscriminatedUnionByType unknownValue) => throw new InvalidOperationException($"Unexpected instance {unknownValue}"));
}