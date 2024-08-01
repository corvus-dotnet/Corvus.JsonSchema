using Corvus.Json;
using Sandbox.Models;

var person = Person.Create(PersonName.Create("Doe", "John"), netWorth: 1, dateOfBirth: Person.DateOfBirthEntity.Null);

if (person.NetWorth is JsonDecimal networth)
{
    Console.WriteLine(networth * 1000);
}

Console.WriteLine(person.HasProperty(Person.JsonPropertyNames.DateOfBirth));