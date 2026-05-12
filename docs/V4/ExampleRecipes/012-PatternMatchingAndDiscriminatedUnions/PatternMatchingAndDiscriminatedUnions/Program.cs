using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

// Create an instance of a type in the discriminated union.
PersonOpen personForDiscriminatedUnion = PersonOpen.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

// Implicit conversion to the discriminated union type from
// the matchable-types
Console.WriteLine(
    ProcessDiscriminatedUnion(personForDiscriminatedUnion));

Console.WriteLine(
    ProcessDiscriminatedUnion("Hello from the pattern matching"));

Console.WriteLine(
    ProcessDiscriminatedUnion(32));

Console.WriteLine(
    ProcessDiscriminatedUnion([personForDiscriminatedUnion]));

// A function that processes the discriminated union type
string ProcessDiscriminatedUnion(in DiscriminatedUnionByType value)
{
    // Pattern matching against a discriminated union type
    // requires you to deal with all known types and the fallback (failure) case
    return value.Match(
        (in JsonString value) => $"It was a string. {value}",
        (in JsonInt32 value) => $"It was an int32. {value}",
        (in PersonOpen value) => $"It was a person. {value.FamilyName}, {value.GivenName}",
        (in DiscriminatedUnionByType.People value) => $"It was an array of people. {value.GetArrayLength()}",
        (in DiscriminatedUnionByType unknownValue) => throw new InvalidOperationException($"Unexpected instance {unknownValue}"));
}