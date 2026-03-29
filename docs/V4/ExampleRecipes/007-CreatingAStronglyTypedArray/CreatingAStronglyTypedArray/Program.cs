using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

// Create an array of 30 people.

string peopleArrayJson =
    """
    [
      {
        "familyName": "Smith",
        "givenName": "John",
        "otherNames": "Edward,Michael",
        "birthDate": "2004-01-01",
        "height": 1.8
      },
      {
        "familyName": "Johnson",
        "givenName": "Alice",
        "birthDate": "2000-02-02",
        "height": 1.6
      },
      {
        "familyName": "Williams",
        "givenName": "Robert",
        "otherNames": "James,Thomas",
        "birthDate": "1995-03-03",
        "height": 1.7
      },
      {
        "familyName": "Brown",
        "givenName": "Jessica",
        "birthDate": "1990-04-04",
        "height": 1.9
      },
      {
        "familyName": "Jones",
        "givenName": "Michael",
        "otherNames": "Andrew,Patrick",
        "birthDate": "1985-05-05",
        "height": 1.75
      },
      {
        "familyName": "Garcia",
        "givenName": "Sarah",
        "birthDate": "1980-06-06",
        "height": 1.65
      },
      {
        "familyName": "Miller",
        "givenName": "William",
        "otherNames": "Robert,James",
        "birthDate": "1975-07-07",
        "height": 1.85
      },
      {
        "familyName": "Davis",
        "givenName": "Elizabeth",
        "birthDate": "1970-08-08",
        "height": 1.8
      },
      {
        "familyName": "Rodriguez",
        "givenName": "David",
        "otherNames": "Michael,Andrew",
        "birthDate": "1965-09-09",
        "height": 1.7
      },
      {
        "familyName": "Martinez",
        "givenName": "Jennifer",
        "birthDate": "1960-10-10",
        "height": 1.75
      },
      {
        "familyName": "Hernandez",
        "givenName": "Joseph",
        "otherNames": "Robert,James",
        "birthDate": "1955-11-11",
        "height": 1.8
      },
      {
        "familyName": "Lopez",
        "givenName": "Emily",
        "birthDate": "1950-12-12",
        "height": 1.85
      },
      {
        "familyName": "Gonzalez",
        "givenName": "James",
        "birthDate": "1945-01-13",
        "height": 1.9
      },
      {
        "familyName": "Wilson",
        "givenName": "Emma",
        "otherNames": "Elizabeth,Mary",
        "birthDate": "1940-02-14",
        "height": 1.95
      },
      {
        "familyName": "Anderson",
        "givenName": "Thomas",
        "birthDate": "1935-03-15",
        "height": 1.8
      },
      {
        "familyName": "Thomas",
        "givenName": "Mary",
        "otherNames": "Elizabeth,Anne",
        "birthDate": "1930-04-16",
        "height": 1.6
      },
      {
        "familyName": "Taylor",
        "givenName": "Christopher",
        "birthDate": "1935-05-17",
        "height": 1.7
      },
      {
        "familyName": "Moore",
        "givenName": "Patricia",
        "otherNames": "Anne,Elizabeth",
        "birthDate": "1940-06-18",
        "height": 1.9
      },
      {
        "familyName": "Jackson",
        "givenName": "Daniel",
        "birthDate": "1945-07-19",
        "height": 1.75
      },
      {
        "familyName": "Martin",
        "givenName": "Hannah",
        "birthDate": "1950-08-20",
        "height": 1.65
      },
      {
        "familyName": "Lee",
        "givenName": "Matthew",
        "otherNames": "James,Andrew",
        "birthDate": "1955-09-21",
        "height": 1.85
      },
      {
        "familyName": "Perez",
        "givenName": "Barbara",
        "birthDate": "1960-10-22",
        "height": 1.8
      },
      {
        "familyName": "Thompson",
        "givenName": "Anthony",
        "otherNames": "Robert,James",
        "birthDate": "1965-11-23",
        "height": 1.7
      },
      {
        "familyName": "White",
        "givenName": "Amanda",
        "birthDate": "1970-12-24",
        "height": 1.75
      },
      {
        "familyName": "Harris",
        "givenName": "Andrew",
        "otherNames": "Michael,Patrick",
        "birthDate": "1975-01-25",
        "height": 1.8
      },
      {
        "familyName": "Clark",
        "givenName": "Megan",
        "birthDate": "1980-02-26",
        "height": 1.85
      },
      {
        "familyName": "Lewis",
        "givenName": "Joshua",
        "otherNames": "Robert,James",
        "birthDate": "1985-03-27",
        "height": 1.9
      },
      {
        "familyName": "Robinson",
        "givenName": "Rebecca",
        "otherNames": "Elizabeth,Mary",
        "birthDate": "1990-04-28",
        "height": 1.95
      },
      {
        "familyName": "Walker",
        "givenName": "Ethan",
        "birthDate": "1995-05-29",
        "height": 1.8
      },
      {
        "familyName": "Hall",
        "givenName": "Julia",
        "otherNames": "Anne,Elizabeth",
        "birthDate": "2000-06-30",
        "height": 1.6
      }
    ]
    """;

using var peopleArrayParsed = ParsedValue<Person1dArray>.Parse(peopleArrayJson);
Person1dArray peopleArray = peopleArrayParsed.Instance;

// Access strongly-typed values by array index
PersonClosed personAtIndex0 = peopleArray[0];
PersonClosed personAtIndex1 = peopleArray[1];

if (peopleArray.IsValid())
{
    // The array is valid - there are 30 valid PersonClosed instances in it.
    Console.WriteLine("original array is valid.");
}
else
{
    Console.WriteLine("original array is not valid.");
}

// Item removed at index
Person1dArray updatedArray = peopleArray.RemoveAt(0);
// Remove first instance of a specific value
updatedArray = updatedArray.Remove(personAtIndex1);

if (updatedArray.IsValid())
{
    Console.WriteLine("Updated array is valid.");
}
else
{
    // The array is no longer valid - the length is 28
    Console.WriteLine("Updated array is not valid.");
}

// Insert an item at an index
updatedArray = updatedArray.Insert(0, personAtIndex1);
// Add an item at the end
updatedArray = updatedArray.Add(personAtIndex0);

if (updatedArray.IsValid())
{
    // The array is valid - the length is back up to 30
    Console.WriteLine("Updated array is valid.");
}
else
{
    Console.WriteLine("Updated array is not valid.");
}

PersonClosed personToSet = PersonClosed.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

updatedArray = updatedArray.SetItem(14, personToSet);

if (updatedArray[14].Equals(personToSet))
{
    // The person was replaced at position 14
    Console.WriteLine("Person was set at position 14.");
}
else
{
    Console.WriteLine("Person was not set at position 14.");
}

// Replace the first instance of a person.
updatedArray = updatedArray.Replace(personAtIndex0, personToSet);

// Enumerate the items in the array
foreach (PersonClosed enumeratedPerson in updatedArray.EnumerateArray())
{
    Console.WriteLine($"{enumeratedPerson.FamilyName}, {enumeratedPerson.GivenName}");
}