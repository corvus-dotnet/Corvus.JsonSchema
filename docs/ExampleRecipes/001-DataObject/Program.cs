using System.Buffers;
using Corvus.Text.Json;
using DataObject.Models;
using NodaTime;

// ------------------------------------------------------------------
// Creating a Person from .NET values using the convenience overload
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var personDoc = Person.CreateBuilder(
    workspace,
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52);

// ------------------------------------------------------------------
// Serialization — convert to JSON string (allocates)
// ------------------------------------------------------------------
string jsonString = personDoc.RootElement.ToString();
Console.WriteLine(jsonString);

// ------------------------------------------------------------------
// Serialization — write to a Utf8JsonWriter (does not allocate)
// ------------------------------------------------------------------
ArrayBufferWriter<byte> abw = new();
using (Utf8JsonWriter writer = new(abw))
{
    personDoc.RootElement.WriteTo(writer);
    writer.Flush();
}

// ------------------------------------------------------------------
// Parsing — from a string (produces an immutable document)
// ------------------------------------------------------------------
using var parsedDoc = ParsedJsonDocument<Person>.Parse(jsonString);
Person person = parsedDoc.RootElement;

// ------------------------------------------------------------------
// Parsing — from UTF-8 bytes
// ------------------------------------------------------------------
using var parsedFromUtf8 = ParsedJsonDocument<Person>.Parse(abw.WrittenMemory);

// ------------------------------------------------------------------
// Property access
// ------------------------------------------------------------------

// Explicit cast to string (throws if null or undefined)
string familyName = (string)person.FamilyName;
Console.WriteLine($"Family name: {familyName}");

// Try to get a string value which may not be present (does not throw)
if (person.GivenName.TryGetValue(out string? givenName))
{
    Console.WriteLine($"Given name: {givenName}");
}

// Check if an optional property is undefined
if (person.OtherNames.IsUndefined())
{
    Console.WriteLine("otherNames is not present.");
}
else
{
    Console.WriteLine("otherNames is present.");
}

// Implicit conversion to NodaTime LocalDate (does not allocate)
LocalDate date = person.BirthDate;
Console.WriteLine($"Birth date: {date}");

// Explicit conversion to double
double heightValue = (double)person.Height;
Console.WriteLine($"Height: {heightValue}");

// ------------------------------------------------------------------
// Mutation — create a modified copy via builder
// (start from an immutable parsed instance, then modify properties)
// ------------------------------------------------------------------
using var updatedDoc = person.CreateBuilder(workspace);
Person.Mutable root = updatedDoc.RootElement;
root.SetBirthDate(new LocalDate(1984, 6, 3));
Person updatedPerson = updatedDoc.RootElement;
Console.WriteLine(updatedPerson);

// ------------------------------------------------------------------
// Equality
// ------------------------------------------------------------------
if (person == updatedPerson)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}

if (person == parsedFromUtf8.RootElement)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}

// ------------------------------------------------------------------
// String comparison — zero-allocation
// ------------------------------------------------------------------
Console.WriteLine($"GivenName ValueEquals \"Hello\": {person.GivenName.ValueEquals("Hello")}");
Console.WriteLine($"GivenName ValueEquals \"Anne\"u8: {person.GivenName.ValueEquals("Anne"u8)}");

// ------------------------------------------------------------------
// Low-allocation access to character data via GetUtf16String()
// (replaces V4's TryGetValue delegate pattern with ReadOnlySpan<char>)
// ------------------------------------------------------------------
using UnescapedUtf16JsonString utf16 = person.GivenName.GetUtf16String();
ReadOnlySpan<char> chars = utf16.Span;
int countA = chars.Count('A');
int countB = chars.Count('B');
Console.WriteLine($"Character counts in GivenName: A={countA}, B={countB}");

// ------------------------------------------------------------------
// Low-allocation access to UTF-8 byte data via GetUtf8String()
// (replaces V4's TryGetValue delegate pattern with ReadOnlySpan<byte>)
// ------------------------------------------------------------------
using UnescapedUtf8JsonString utf8 = person.GivenName.GetUtf8String();
ReadOnlySpan<byte> bytes = utf8.Span;
int countUtf8A = bytes.Count((byte)'A');
int countUtf8B = bytes.Count((byte)'B');
Console.WriteLine($"UTF-8 byte counts in GivenName: A={countUtf8A}, B={countUtf8B}");