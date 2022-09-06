using System.Buffers;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Corvus.Json;
using Corvus.Json.Benchmarking.Models;
using Microsoft.Extensions.ObjectPool;

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

personArray = PersonArray.From(builder.ToImmutable());

await Task.Delay(5000);

personArray.Validate(ValidationContext.ValidContext);

////const string JsonString = "\"Hello there \u0061 everyone!\"";


////using var document = JsonDocument.Parse(JsonString);
////JsonElement element = document.RootElement;

////Console.WriteLine($"'{JsonString}' {(element.ValidateString(ValidationContext.ValidContext).IsValid ? "is" : "is not")} valid.");

////public static class CustomJsonElementExtensions
////{
////    public static ValidationContext ValidateString(this JsonElement element, in ValidationContext validationContext)
////    {
////        if (element.TryGetValue(StringValidator, validationContext, out ValidationContext result))
////        {
////            return result;
////        }

////        throw new InvalidOperationException();
////    }

////    private static bool StringValidator(ReadOnlySpan<char> input, in ValidationContext context, out ValidationContext result)
////    {
////        // Emitted if minLength or maxLength
////        int runeCount = 0;
////        SpanRuneEnumerator enumerator = input.EnumerateRunes();
////        while (enumerator.MoveNext())
////        {
////            runeCount++;
////        }

////        result = context.WithResult(runeCount < 10);

////        return true;
////    }
////}