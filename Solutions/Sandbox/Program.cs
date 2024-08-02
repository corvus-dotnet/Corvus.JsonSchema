using System.Text.Json;
using Corvus.Json;
using JsonSchemaSample.Api;
using NodaTime;

Person audreyJones =
    Person.Create(
        name: PersonName.Create(
                givenName: "Audrey",
                otherNames: PersonNameElementArray.FromItems("Margaret", "Nancy"),
                familyName: "Jones"),
        dateOfBirth: new LocalDate(1947, 11, 7));