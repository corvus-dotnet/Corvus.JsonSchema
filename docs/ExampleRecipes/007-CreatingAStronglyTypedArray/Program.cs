using Corvus.Text.Json;
using CreatingAStronglyTypedArray.Models;
using NodaTime;

// ------------------------------------------------------------------
// Parse an array of 30 people from JSON
// ------------------------------------------------------------------
string peopleArrayJson =
    """
    [
      {"familyName":"Smith","givenName":"John","otherNames":"Edward,Michael","birthDate":"2004-01-01","height":1.8},
      {"familyName":"Johnson","givenName":"Alice","birthDate":"2000-02-02","height":1.6},
      {"familyName":"Williams","givenName":"Robert","otherNames":"James,Thomas","birthDate":"1995-03-03","height":1.7},
      {"familyName":"Brown","givenName":"Jessica","birthDate":"1990-04-04","height":1.9},
      {"familyName":"Jones","givenName":"Michael","otherNames":"Andrew,Patrick","birthDate":"1985-05-05","height":1.75},
      {"familyName":"Garcia","givenName":"Sarah","birthDate":"1980-06-06","height":1.65},
      {"familyName":"Miller","givenName":"William","otherNames":"Robert,James","birthDate":"1975-07-07","height":1.85},
      {"familyName":"Davis","givenName":"Elizabeth","birthDate":"1970-08-08","height":1.8},
      {"familyName":"Rodriguez","givenName":"David","otherNames":"Michael,Andrew","birthDate":"1965-09-09","height":1.7},
      {"familyName":"Martinez","givenName":"Jennifer","birthDate":"1960-10-10","height":1.75},
      {"familyName":"Hernandez","givenName":"Joseph","otherNames":"Robert,James","birthDate":"1955-11-11","height":1.8},
      {"familyName":"Lopez","givenName":"Emily","birthDate":"1950-12-12","height":1.85},
      {"familyName":"Gonzalez","givenName":"James","birthDate":"1945-01-13","height":1.9},
      {"familyName":"Wilson","givenName":"Emma","otherNames":"Elizabeth,Mary","birthDate":"1940-02-14","height":1.95},
      {"familyName":"Anderson","givenName":"Thomas","birthDate":"1935-03-15","height":1.8},
      {"familyName":"Thomas","givenName":"Mary","otherNames":"Elizabeth,Anne","birthDate":"1930-04-16","height":1.6},
      {"familyName":"Taylor","givenName":"Christopher","birthDate":"1935-05-17","height":1.7},
      {"familyName":"Moore","givenName":"Patricia","otherNames":"Anne,Elizabeth","birthDate":"1940-06-18","height":1.9},
      {"familyName":"Jackson","givenName":"Daniel","birthDate":"1945-07-19","height":1.75},
      {"familyName":"Martin","givenName":"Hannah","birthDate":"1950-08-20","height":1.65},
      {"familyName":"Lee","givenName":"Matthew","otherNames":"James,Andrew","birthDate":"1955-09-21","height":1.85},
      {"familyName":"Perez","givenName":"Barbara","birthDate":"1960-10-22","height":1.8},
      {"familyName":"Thompson","givenName":"Anthony","otherNames":"Robert,James","birthDate":"1965-11-23","height":1.7},
      {"familyName":"White","givenName":"Amanda","birthDate":"1970-12-24","height":1.75},
      {"familyName":"Harris","givenName":"Andrew","otherNames":"Michael,Patrick","birthDate":"1975-01-25","height":1.8},
      {"familyName":"Clark","givenName":"Megan","birthDate":"1980-02-26","height":1.85},
      {"familyName":"Lewis","givenName":"Joshua","otherNames":"Robert,James","birthDate":"1985-03-27","height":1.9},
      {"familyName":"Robinson","givenName":"Rebecca","otherNames":"Elizabeth,Mary","birthDate":"1990-04-28","height":1.95},
      {"familyName":"Walker","givenName":"Ethan","birthDate":"1995-05-29","height":1.8},
      {"familyName":"Hall","givenName":"Julia","otherNames":"Anne,Elizabeth","birthDate":"2000-06-30","height":1.6}
    ]
    """;

using var parsedArray = ParsedJsonDocument<Person1dArray>.Parse(peopleArrayJson);
Person1dArray peopleArray = parsedArray.RootElement;

// ------------------------------------------------------------------
// Strongly-typed array indexing
// ------------------------------------------------------------------
PersonClosed personAtIndex0 = peopleArray[0];
PersonClosed personAtIndex1 = peopleArray[1];

Console.WriteLine("Array indexing:");
Console.WriteLine($"  [0] = {personAtIndex0.GivenName} {personAtIndex0.FamilyName}");
Console.WriteLine($"  [1] = {personAtIndex1.GivenName} {personAtIndex1.FamilyName}");

// ------------------------------------------------------------------
// Validate the array (30 items, matching minItems/maxItems)
// ------------------------------------------------------------------
Console.WriteLine();
if (peopleArray.EvaluateSchema())
{
    Console.WriteLine("Original array is valid (30 items).");
}

// ------------------------------------------------------------------
// Mutable array operations — remove, insert, add, set, replace
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var mutableDoc = peopleArray.CreateBuilder(workspace);
Person1dArray.Mutable root = mutableDoc.RootElement;

Console.WriteLine();
Console.WriteLine("Removing item at index 0 and first instance of person at index 1:");
root.RemoveAt(0);
root.Remove(personAtIndex1);

Person1dArray updatedArray = mutableDoc.RootElement;
if (updatedArray.EvaluateSchema())
{
    Console.WriteLine("  Updated array is valid.");
}
else
{
    Console.WriteLine("  Updated array is not valid (length is now 28, needs 30).");
}

// Re-insert and add to restore the count
Console.WriteLine();
Console.WriteLine("Inserting person[1] at index 0 and adding person[0] at the end:");
root.InsertItem(0, personAtIndex1);
root.AddItem(personAtIndex0);

updatedArray = mutableDoc.RootElement;
if (updatedArray.EvaluateSchema())
{
    Console.WriteLine("  Updated array is valid (length restored to 30).");
}

// Set item at a specific index
Console.WriteLine();
Console.WriteLine("Setting a new person at index 14:");
root.SetItem(14, PersonClosed.Build(
    static (ref PersonClosed.Builder b) => b.Create(
        birthDate: new LocalDate(1820, 1, 17),
        familyName: "Brontë",
        givenName: "Anne",
        height: 1.57)));

// Verify the replacement
updatedArray = mutableDoc.RootElement;
Console.WriteLine($"  [14] = {updatedArray[14].GivenName} {updatedArray[14].FamilyName}");

// Replace the first instance of a person by value
Console.WriteLine();
Console.WriteLine("Replacing person[0] with the Brontë person:");
root.Replace(personAtIndex0, PersonClosed.Build(
    static (ref PersonClosed.Builder b) => b.Create(
        birthDate: new LocalDate(1820, 1, 17),
        familyName: "Brontë",
        givenName: "Anne",
        height: 1.57)));

// ------------------------------------------------------------------
// Enumerate the array
// ------------------------------------------------------------------
Console.WriteLine();
Console.WriteLine("All people in the updated array:");
updatedArray = mutableDoc.RootElement;
foreach (PersonClosed person in updatedArray.EnumerateArray())
{
    Console.WriteLine($"  {person.FamilyName}, {person.GivenName}");
}