---
ContentType: "application/vnd.endjin.ssg.content+md"
PublicationStatus: Published
Date: 2026-03-15T00:00:00.0+00:00
Title: "Quick Start"
---
```csharp
// 1. Define a schema (Schemas/person.json)
// {
//     "$schema": "https://json-schema.org/draft/2020-12/schema",
//     "type": "object",
//     "required": ["name"],
//     "properties": {
//         "name": { "type": "string", "minLength": 1 },
//         "age": { "type": "integer", "format": "int32", "minimum": 0 }
//     }
// }

// 2. Use the source generator
[JsonSchemaTypeGenerator("Schemas/person.json")]
public readonly partial struct Person;

// 3. Parse and access properties
using var doc = ParsedJsonDocument<Person>.Parse(
    """{"name":"Alice","age":30}""");
Person person = doc.RootElement;

string name = (string)person.Name;           // "Alice"
int age = person.Age;                        // 30

// 4. Validate against the schema
bool valid = person.EvaluateSchema();        // true

// 5. Mutate with the builder pattern
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builder = person.CreateBuilder(workspace);
Person.Mutable root = builder.RootElement;
root.SetAge(31);

Console.WriteLine(root.ToString());
// {"name":"Alice","age":31}
```
