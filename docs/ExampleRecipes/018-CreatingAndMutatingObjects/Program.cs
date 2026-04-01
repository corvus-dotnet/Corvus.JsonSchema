using System.Buffers;
using Corvus.Text.Json;
using CreatingAndMutatingObjects.Models;

// ------------------------------------------------------------------
// Parse a JSON string into an immutable document
// ------------------------------------------------------------------
string personJson =
    """
    {
        "name": {
            "familyName": "Smith",
            "givenName": "John"
        },
        "age": 30,
        "email": "john@example.com",
        "hobbies": ["reading", "hiking"]
    }
    """;

using var parsedDoc = ParsedJsonDocument<Person>.Parse(personJson);
Person person = parsedDoc.RootElement;

Console.WriteLine("Parsed person:");
Console.WriteLine(person);
Console.WriteLine();

// ------------------------------------------------------------------
// Create a mutable builder from the immutable document
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;

// ------------------------------------------------------------------
// Read properties from the mutable root
// ------------------------------------------------------------------
Console.WriteLine("Reading properties from mutable root:");
Console.WriteLine($"  Name: {root.Name.GivenName} {root.Name.FamilyName}");
Console.WriteLine($"  Age: {root.Age}");
Console.WriteLine($"  Email: {root.Email}");
Console.WriteLine();

// ------------------------------------------------------------------
// Set properties — change age and set email
// ------------------------------------------------------------------
root.SetAge(31);
root.SetEmail("john.smith@example.com");

Console.WriteLine("After setting age to 31 and updating email:");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Remove an optional property — remove email
// ------------------------------------------------------------------
bool removed = root.RemoveEmail();
Console.WriteLine($"Removed email: {removed}");
Console.WriteLine("After removing email:");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Mutate arrays — add, insert, and remove hobbies
// ------------------------------------------------------------------

// Add a hobby at the end
var hobbies = root.Hobbies;
hobbies.AddItem("cooking");

Console.WriteLine("After adding 'cooking':");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// Insert a hobby at the beginning (re-fetch after prior mutation)
hobbies = root.Hobbies;
hobbies.InsertItem(0, "painting");

Console.WriteLine("After inserting 'painting' at index 0:");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// Remove the hobby at index 1 (re-fetch after prior mutation)
hobbies = root.Hobbies;
hobbies.RemoveAt(1);

Console.WriteLine("After removing the item at index 1:");
Console.WriteLine(builder.RootElement);
Console.WriteLine();

// ------------------------------------------------------------------
// Version tracking — root is always live after mutations
// ------------------------------------------------------------------
Console.WriteLine("Root element is always live after mutations:");
Person current = builder.RootElement;
Console.WriteLine($"  Current age: {current.Age}");
Console.WriteLine($"  Hobbies count: {current.Hobbies.GetArrayLength()}");
Console.WriteLine();

// ------------------------------------------------------------------
// Serialize — ToString() and Utf8JsonWriter
// ------------------------------------------------------------------
string jsonString = builder.RootElement.ToString();
Console.WriteLine("Serialized via ToString():");
Console.WriteLine(jsonString);
Console.WriteLine();

ArrayBufferWriter<byte> abw = new();
using (Utf8JsonWriter writer = new(abw))
{
    builder.RootElement.WriteTo(writer);
    writer.Flush();
}

Console.WriteLine("Serialized via Utf8JsonWriter:");
Console.WriteLine(System.Text.Encoding.UTF8.GetString(abw.WrittenSpan));