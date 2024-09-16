using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

// Create a tall person, although Anne is not
// tall enough to be a valid tall person!
PersonTall personTall = PersonTall.Create(
    birthDate: new LocalDate(1820, 1, 17),
    familyName: "Brontë",
    givenName: "Anne",
    height: 1.57);

// Implicit conversion to the base type
PersonClosed personClosedFromTall = personTall;

// personTall is not valid because of the additional constraint.
if (personTall.IsValid())
{
    Console.WriteLine("personTall is valid");
}
else
{
    Console.WriteLine("personTall is not valid");
}

// But personClosedFromTall is valid because it does not have the
// additional constraint.
if (personClosedFromTall.IsValid())
{
    Console.WriteLine("personClosedFromTall is valid");
}
else
{
    Console.WriteLine("personClosedFromTall is not valid");
}