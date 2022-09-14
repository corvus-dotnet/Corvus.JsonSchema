using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;

using Corvus.Json.Benchmarking.Models;

JsonDocument? objectDocument;
JsonDocument? invalidObjectDocument;
PersonArray personArray;

string InvalidJsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""Not likely!""
}";

string JsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

objectDocument = JsonDocument.Parse(JsonText);
invalidObjectDocument = JsonDocument.Parse(InvalidJsonText);

ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
for (int i = 0; i < 10000; ++i)
{
    if (i == 5000)
    {
        builder.Add(Person.FromJson(invalidObjectDocument.RootElement).AsDotnetBackedValue());
    }
    else
    {
        builder.Add(Person.FromJson(objectDocument.RootElement).AsDotnetBackedValue());
    }
}

personArray = PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();

var result = personArray.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);

Console.WriteLine("Validated!");