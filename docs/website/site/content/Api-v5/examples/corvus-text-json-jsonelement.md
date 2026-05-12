The following example demonstrates common property access patterns on a `JsonElement`-based generated type.

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(jsonString);
Person person = doc.RootElement;

// Explicit cast to a .NET type (throws if null or undefined)
string familyName = (string)person.FamilyName;

// TryGetValue for optional properties (does not throw)
if (person.GivenName.TryGetValue(out string? givenName))
{
    Console.WriteLine($"Given name: {givenName}");
}

// Check whether an optional property is present
if (person.OtherNames.IsUndefined())
{
    Console.WriteLine("otherNames is not present.");
}
```

### Zero-allocation string comparison

Compare string values directly against UTF-8 or UTF-16 literals without allocating:

```csharp
bool isAnne = person.GivenName.ValueEquals("Anne"u8);
```

### Equality

Generated types support structural equality — two elements are equal if their underlying JSON is equivalent, regardless of which document they came from:

```csharp
using ParsedJsonDocument<Person> doc1 =
    ParsedJsonDocument<Person>.Parse(jsonString);
using ParsedJsonDocument<Person> doc2 =
    ParsedJsonDocument<Person>.Parse(jsonString);

bool areEqual = doc1.RootElement == doc2.RootElement; // true
```
