using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;
using System.Buffers;
using System.Text.Json;

// Create from dotnet values
Person person = Person.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.52);

// Convert to a json-format string (allocates)
string jsonString = person.ToString();
Console.WriteLine(jsonString);

// Write to JSON Writer (does not allocate)
ArrayBufferWriter<byte> abw = new();
using Utf8JsonWriter writer = new(abw);
person.WriteTo(writer);
writer.Flush();

// Parse into a disposable entity using System.Text.Json behind the scenes.
using var parsedPerson = ParsedValue<Person>.Parse(jsonString);
using var parsedFromUtf8person = ParsedValue<Person>.Parse(abw.WrittenMemory);

// Retrieve the parsed instance from the disposable wrapper
var parsedPersonInstance = parsedPerson.Instance;

// Using property values

// Conversion to string requires an explicit cast because it allocates (throws if null or undefined)
string familyName = (string)person.FamilyName;

// Try to get a string value which may not be present (does not throw)
if (person.GivenName.TryGetString(out string? givenName))
{
    Console.WriteLine(givenName);
}

// Check if optional property is undefined
if (person.OtherNames.IsUndefined())
{
    Console.WriteLine("otherNames is not present.");
}
else
{
    Console.WriteLine("otherNames is present.");
}

// Conversion to LocalDate is allowed as an implicit conversion as it does not allocate
LocalDate date = person.BirthDate;

// Conversion to numeric types are only available explicitly as the value may not be representable in the chosen type.
double heightValue = (double)person.Height;
int heightValueAsInt = (int)person.Height;

// Set properties (objects are immutable, so this returns a copy)
// Note that we have an implicit conversion from LocalDate to JsonDate
var updatedPerson = person.WithBirthDate(new LocalDate(1984, 6, 3));

Console.WriteLine(updatedPerson);

// Compare values

if (person == updatedPerson)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}

if (person == parsedPersonInstance)
{
    Console.WriteLine("The same person.");
}
else
{
    Console.WriteLine("Different people.");
}

// Custom zero-allocation comparison functions are available that avoid allocations for string types
// Comparison with string
person.GivenName.EqualsString("Hello");
// Comparison with UTF8 byte array
person.GivenName.EqualsUtf8Bytes("Anne"u8);

// Low allocation parsing of character data
// Our CountInstances() method counts the number of instances of the given characters in a JsonString value
// but it doesn't allocate a .NET string to do so.
(char FirstChar, char SecondChar) someCharContext = ('A', 'B');
if (person.GivenName.TryGetValue(CountInstances, someCharContext, out (int FirstCount, int SecondCount) charResult))
{
    Console.WriteLine(charResult);
}

// Count the instances in the given span
bool CountInstances(
    ReadOnlySpan<char> span,
    in (char FirstChar, char SecondChar) state,
    out (int FirstCount, int SecondCount) result)
{
    result = (span.Count(state.FirstChar), span.Count(state.SecondChar));
    return true;
}

// Similarly, we can perform the same operation directly on the (decoded) UTF8 data
(byte FirstUtf8Byte, byte SecondUtf8Byte) someUtf8Context = ((byte)'A', (byte)'B');
if (person.GivenName.TryGetValue(ParseStringUtf8, someUtf8Context, out (int FirstCount, int SecondCount) utf8Result))
{
    Console.WriteLine(utf8Result);
}

bool ParseStringUtf8(
    ReadOnlySpan<byte> span,
    in (byte FirstUtf8Byte, byte SecondUtf8Byte) state,
    out (int FirstCount, int SecondCount) result)
{
    result = (span.Count(state.FirstUtf8Byte), span.Count(state.SecondUtf8Byte));
    return true;
}