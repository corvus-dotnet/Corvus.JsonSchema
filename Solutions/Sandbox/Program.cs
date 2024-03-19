using System.Text.Json;
using Corvus.Json;
using Corvus.Json.Benchmarking.Models;
using Corvus.Json.JsonSchema.Draft202012;

using var parsedPerson = ParsedValue<Person>.Parse(
    """
    {
        "name": {
            "familyName": "Oldroyd",
            "givenName": "Michael",
            "otherNames": [],
            "email": "michael.oldryoyd@contoso.com"
        },
        "dateOfBirth": "1944-07-14",
        "netWorth": 1234567890.1234567891,
        "height": 1.8
    }
    """);


Person person = parsedPerson.Instance;
PersonName personName = person.Name;


using var schema = ParsedValue<Schema>.Parse(File.ReadAllText("D:\\source\\corvus-dotnet\\Corvus.JsonSchema\\Solutions\\Sandbox\\PersonModel\\person-schema.json"));

if (schema.Instance.Type.TryGetAsSimpleTypes(out Validation.SimpleTypes simpleTypes))
{
    MatchSimpleType(simpleTypes);
}
else if (schema.Instance.Type.TryGetAsSimpleTypesArray(out Validation.TypeEntity.SimpleTypesArray simpleTypesArray))
{
    foreach (Validation.SimpleTypes simpleTypesFromArray in simpleTypesArray.EnumerateArray())
    {
        MatchSimpleType(simpleTypesFromArray);
    }
}

static void MatchSimpleType(Validation.SimpleTypes simpleTypes)
{
    simpleTypes.Match(
        matchArray: static () =>
        {
            Console.WriteLine($"It's an array!");
            return JsonValueKind.Array;
        },
        matchObject: static () =>
        {
            Console.WriteLine($"It's an object!");
            return JsonValueKind.Object;
        },
        matchString: static () =>
        {
            Console.WriteLine($"It's a string!");
            return JsonValueKind.String;
        },
        matchNumber: static () =>
        {
            Console.WriteLine($"It's a number!");
            return JsonValueKind.Number;
        },
        matchBoolean: static () =>
        {
            Console.WriteLine($"It's a boolean!");
            return JsonValueKind.True;
        },
        matchInteger: static () =>
        {
            Console.WriteLine($"It's an integer!");
            return JsonValueKind.Number;
        },
        matchNull: static () =>
        {
            Console.WriteLine($"It's a null!");
            return JsonValueKind.Null;
        },
        defaultMatch: static () =>
        {
            Console.WriteLine($"Doesn't seem to be anything");
            return JsonValueKind.Null;
        });
}