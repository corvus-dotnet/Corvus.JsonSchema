# JSON Schema Patterns in .NET - Pattern Matching and Discriminated Unions

This recipe demonstrates how to use JSON Schema `oneOf` to create discriminated unions that support pattern matching in .NET.

## The Pattern

One of the *most* requested features in .NET is [sum types or discriminated unions](https://en.wikipedia.org/wiki/Tagged_union).

Generally speaking, the request is for a value that can take on several different, but fixed types. There is some tag or other mechanism which uniquely discriminates between instances of the types, allowing pattern matching to dispatch the value to the correct handler for its type, from an exhaustive list.

One way to achieve a form of this in C# is via inheritance - through a base class (which represents the discriminated union type) and its derived classes, which represent the different types that could be dispatched:

```csharp
public class UnionType { }

public class FirstType : UnionType { }
public class SecondType : UnionType { }

string Process(UnionType type)
{
    return type switch
    {
        FirstType f => "The first type",
        SecondType s => "The second type",
        _ => "I don't know this type",
    };
}

Console.WriteLine(Process(new SneakyThirdTypeYouDidNotKnowAbout()));

public class SneakyThirdTypeYouDidNotKnowAbout : UnionType { }
```

However, this has two issues:

1. **It is invasive** - you have to implement the base class (or interface).
2. **There is no "exhaustive list"** - our `Process()` function has no way of knowing it has dealt with all the available cases. Someone might have added another type without us looking - like `SneakyThirdTypeYouDidNotKnowAbout`.

The good news is that we can achieve a more flexible sum type using JSON Schema, with the `oneOf` keyword.

This defines a list of schemas, and asserts that an instance is valid for _exactly_ one of those possible schemas.

This addresses the two issues above:

1. The schemas in the list do not _need_ to have anything in common. It is just a list of arbitrary schemas. Specifically, the schemas in the union do not _need_ to know that the union exists. Therefore it is not invasive in that sense. However, they _must_ have something which uniquely discriminates them such that only _one_ of the schemas in the `oneOf` array is valid for the instance. It is the responsibility of the person defining the `oneOf` union schema to ensure that is the case.

2. The `oneOf` keyword exhaustively lists the types in the union, so pattern matching guarantees that it will cover all valid cases.

## The Schemas

In our example, we are discriminating between a `string`, an `int32`, an `object` (conforming to the `person-open` schema) and an `array` of items (that also conform to the `person-open` schema).

### discriminated-union-by-type.json

```json
{
    "oneOf": [
        { "type": "string" },
        {
            "type": "integer",
            "format": "int32"
        },
        { "$ref": "./person-open.json" },
        { "$ref": "#/$defs/people" }
    ],
    "$defs": {
        "people": {
            "type": "array",
            "items": { "$ref": "./person-open.json" }
        }
    }
}
```

### person-open.json

```json
{
    "title": "The person schema https://schema.org/Person",
    "type": "object",
    "required": [ "familyName", "givenName", "birthDate" ],
    "properties": {
        "familyName": { "$ref": "#/$defs/constrainedString" },
        "givenName": { "$ref": "#/$defs/constrainedString" },
        "otherNames": { "$ref": "#/$defs/constrainedString" },
        "birthDate": {
            "type": "string",
            "format": "date"
        },
        "height": {
            "type": "number",
            "format": "double",
            "exclusiveMinimum": 0.0,
            "maximum": 3.0
        }
    },

    "$defs": {
        "constrainedString": {
            "type": "string",
            "minLength": 1,
            "maxLength": 256
        }
    }
}
```

In this example, we have an entirely heterogeneous set of schemas in our discriminated union.

In the [next recipe (013-PolymorphismWithDiscriminators)](../013-PolymorphismWithDiscriminators/), we will look at a tagged union of schemas, discriminated by a property that indicates type, similar to that found in [OpenAPI's polymorphism feature](https://swagger.io/docs/specification/data-models/inheritance-and-polymorphism/), and `System.Text.Json`'s [polymorphic serialization](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/polymorphism?pivots=dotnet-8-0).

## Generated Code

The code generator produces:
- **`DiscriminatedUnionByType`** - the union type with a `Match()` method
- **`JsonString`** - global type used for the string variant (simple types are reduced to global types)
- **`JsonInt32`** - global type used for the int32 variant
- **`PersonOpen`** - the object type
- **`DiscriminatedUnionByType.People`** - the array type

## Pattern Matching

The `Match()` method provides exhaustive pattern matching over all possible types in the union:

```csharp
string ProcessDiscriminatedUnion(in DiscriminatedUnionByType value)
{
    // Pattern matching requires you to deal with all known types 
    // and the fallback (failure) case
    return value.Match(
        static (in JsonString value) => $"It was a string: {value}",
        static (in JsonInt32 value) => $"It was an int32: {value}",
        static (in PersonOpen value) => $"It was a person. {value.FamilyName}, {value.GivenName}",
        static (in DiscriminatedUnionByType.People value) => $"It was an array of people. {value.GetArrayLength()}",
        static (in DiscriminatedUnionByType unknownValue) => throw new InvalidOperationException($"Unexpected instance {unknownValue}"));
}
```

### Creating instances

Parse JSON directly into the union type:

```csharp
// Parse a person
string personJson = """
    {
      "familyName": "Brontë",
      "givenName": "Anne",
      "birthDate": "1820-01-17",
      "height": 1.57
    }
    """;

using var parsedPerson = ParsedJsonDocument<PersonOpen>.Parse(personJson);
PersonOpen person = parsedPerson.RootElement;

// Use From() to convert to the union type
DiscriminatedUnionByType union1 = DiscriminatedUnionByType.From(person);

Console.WriteLine(ProcessDiscriminatedUnion(union1));
// Output: It was a person. Brontë, Anne

// Parse an array directly as the union type
string arrayJson = """
    [
      {
        "familyName": "Brontë",
        "givenName": "Anne",
        "birthDate": "1820-01-17",
        "height": 1.57
      }
    ]
    """;
using var parsedArray = ParsedJsonDocument<DiscriminatedUnionByType>.Parse(arrayJson);

Console.WriteLine(ProcessDiscriminatedUnion(parsedArray.RootElement));
// Output: It was an array of people. 1
```

## Key Differences from V4

The `Match()` API and pattern matching experience are essentially the same between V4 and V5. Both versions support implicit conversion from constituent entity types:

### V4 (Corvus.Json)
```csharp
// Implicit conversion from constituent types, including built-in types
Console.WriteLine(ProcessDiscriminatedUnion(personForDiscriminatedUnion));
Console.WriteLine(ProcessDiscriminatedUnion("Hello from the pattern matching"));
Console.WriteLine(ProcessDiscriminatedUnion(32));
```

### V5 (Corvus.Text.Json)
```csharp
// Implicit conversion from constituent entity types
Console.WriteLine(ProcessDiscriminatedUnion(person));

// For non-entity types, use From()
DiscriminatedUnionByType union = DiscriminatedUnionByType.From(JsonAny.Parse("\"Hello\""));
```

**Key differences:**
- V5 provides implicit conversion from constituent entity types (e.g., `PersonOpen`, `People`) just as V4 did
- V4 additionally supported implicit conversion from .NET built-in literals (strings, integers); in V5 you use `From()` or parse for those cases
- The `Match()` method, handler signatures, and exhaustive matching behaviour are unchanged

## Running the Example

```bash
cd docs/ExampleRecipes/012-PatternMatching
dotnet run
```

See [Recipe 013](../013-PolymorphismWithDiscriminators/) for pattern matching with discriminator properties, which is the more common approach in real APIs.

## Related Patterns

- [011-InterfacesAndMixInTypes](../011-InterfacesAndMixInTypes/) - Composition with `allOf`
- [013-PolymorphismWithDiscriminators](../013-PolymorphismWithDiscriminators/) - Discriminated unions with type properties
- [014-StringEnumerations](../014-StringEnumerations/) - Pattern matching over string enums

## Frequently Asked Questions

### Q: What happens if more than one `oneOf` variant matches?

**A:** Validation fails. The `oneOf` keyword requires that exactly one of the subschemas matches. If zero or more than one match, the value is invalid according to the schema. This is what makes `oneOf` suitable for discriminated unions — each value must belong to precisely one variant.

### Q: Can I use `oneOf` with variants of the same JSON type?

**A:** Yes, but you need a way to distinguish them. If multiple variants are objects, add a discriminator property with a `const` value to each variant (see [Recipe 013](../013-PolymorphismWithDiscriminators/)). Without a discriminator, variants of the same JSON type risk ambiguous matching, which would cause validation failures.

### Q: Why does the code generator use `OneOf0Entity`/`OneOf1Entity` names?

**A:** The `oneOf` subschemas don't have inherent names — they are anonymous entries in an array, so the code generator assigns ordinal names. With discriminator properties, variants are named by their required properties instead (e.g., `RequiredRadiusAndType`). You can always provide explicit names, either via CLI generator configuration or by adding a model type for the schema location:

```csharp
[JsonSchemaTypeGenerator("some-union-type.json#/oneOf/0")]
public readonly partial struct MySpeciallyNamedType;
```

Note that simple types may be reduced to global types like `JsonString` or `JsonInt32`, in which case the explicit name will not be used.

### Q: What is the `defaultMatch` handler for?

**A:** The `defaultMatch` handler is called when no other variant matches. In a well-validated schema this shouldn't happen, but it provides a safety net for cases where the JSON data hasn't been validated against the schema. It follows the same pattern as a `default` case in a C# `switch` expression, ensuring exhaustive handling.
