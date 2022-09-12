using System.Collections.Immutable;
using System.Text.Json;
using Corvus.Json;

using static Corvus.Json.Benchmarking.Models.Schema;

JsonDocument? objectDocument;
PersonArray personArray;

string JsonText = @"{
    ""name"": {
      ""familyName"": ""Oldroyd"",
      ""givenName"": ""Michael"",
      ""otherNames"": [""Francis"", ""James""]
    },
    ""dateOfBirth"": ""1944-07-14""
}";

objectDocument = JsonDocument.Parse(JsonText);

ImmutableList<JsonAny>.Builder builder = ImmutableList.CreateBuilder<JsonAny>();
for (int i = 0; i < 10000; ++i)
{
    builder.Add(Person.FromJson(objectDocument.RootElement).AsDotnetBackedValue());
}

personArray = PersonArray.From(builder.ToImmutable()).AsJsonElementBackedValue();

await Task.Delay(5000);

personArray.Validate(ValidationContext.ValidContext);