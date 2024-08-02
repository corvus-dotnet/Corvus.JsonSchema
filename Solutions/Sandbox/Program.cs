using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using NodaTime;

Person audreyJones =
    Person.Create(
        name: PersonName.Create(
                givenName: "Audrey",
                otherNames: PersonNameElementArray.FromItems("Margaret", "Nancy"),
                familyName: "Jones"),
        dateOfBirth: new LocalDate(1947, 11, 7));

string result = audreyJones.Name.OtherNames.Match(
    static (in PersonNameElement otherNames) => $"Other names: {otherNames}",
    static (in PersonNameElementArray otherNames) => $"Other names: {string.Join(", ", otherNames)}",
    static (in OtherNames value) => throw new InvalidOperationException($"Unexpected type: {value}"));

Console.WriteLine(result);
