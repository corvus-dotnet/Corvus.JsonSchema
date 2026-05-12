---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-29T00:00:00.0+00:00
Title: "Creating JSON and Union Types"
---
## Creating JSON

### Creating primitives

Primitive types support both implicit conversion and explicit construction:

```csharp
JsonString myString = "hello";
JsonNumber myNumber = 3.14;
JsonBoolean myBool = true;
JsonNull myNull = JsonAny.Null;
```

Explore the extended types like `JsonDateTime`, `JsonInteger`, `JsonEmail`, and others for format-specific schemas.

### Creating arrays

Array types with a simple item schema emit `FromItems()` and `FromRange()` factory methods:

```csharp
// From individual items (with implicit conversion from string)
var otherNames = PersonNameElementArray.FromItems("Margaret", "Nancy");

// From an existing collection
var nameList = new List<PersonNameElement> { "Margaret", "Nancy" };
PersonNameElementArray array = PersonNameElementArray.FromRange(nameList);
```

### Creating objects with `Create()`

The code generator emits `Create()` factory methods that understand required vs. optional properties:

```csharp
Person audreyJones =
    Person.Create(
        name: PersonName.Create(
                givenName: "Audrey",
                otherNames: PersonNameElementArray.FromItems("Margaret", "Nancy"),
                familyName: "Jones"),
        dateOfBirth: new LocalDate(1947, 11, 7));
```

A minimal valid `Person` needs only the required `name` with a `familyName`:

```csharp
var minPerson = Person.Create(PersonName.Create("Jones"));
```

### Optional vs. Null in `Create()`

When creating objects, `.NET null` means *do not set the property* (undefined), while `JsonAny.Null` means *set the property to JSON null*:

```csharp
// dateOfBirth is undefined — not present in JSON
Person.Create(
  name: PersonName.Create("Jones"),
  dateOfBirth: null);
// Produces: { "name": {"familyName": "Jones"} }

// dateOfBirth is explicitly null
Person.Create(
  name: PersonName.Create("Jones"),
  dateOfBirth: JsonAny.Null);
// Produces: { "name": {"familyName": "Jones"}, "dateOfBirth": null }
```

## Union types

The `OtherNames` property uses `oneOf` to represent a union of `PersonNameElement` (string) and `PersonNameElementArray` (array). The code generator emits `Is...` and `As...` members for each variant:

```csharp
OtherNames otherNames = michaelOldroyd.Name.OtherNames;

if (otherNames.IsPersonNameElementArray)
{
    PersonNameElementArray array = otherNames.AsPersonNameElementArray;
    // Use the array
}
```

There is also a `TryGetAs...` pattern:

```csharp
if (michaelOldroyd.Name.OtherNames.TryGetAsPersonNameElementArray(
        out PersonNameElementArray otherNamesArray))
{
    otherNamesArray.EnumerateArray();
}
```

## Pattern matching with `Match()`

Any union type (`oneOf`, `anyOf`, `allOf`, `if`/`then`/`else`) emits an exhaustive `Match()` method that takes a delegate for each variant:

```csharp
string result = audreyJones.Name.OtherNames.Match(
    static (in PersonNameElement otherNames) =>
        $"Other names: {otherNames}",
    static (in PersonNameElementArray otherNames) =>
        $"Other names: {string.Join(", ", otherNames)}",
    static (in OtherNames value) =>
        throw new InvalidOperationException($"Unexpected type: {value}"));
```

Because the match is exhaustive, you avoid common errors with missing cases.

> There are similar match methods for `if`/`then`/`else` and `enum` types.