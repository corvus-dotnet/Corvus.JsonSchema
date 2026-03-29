The following example creates a mutable builder from a parsed document, modifies properties, and serializes the result.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(
        """
        {
          "name": { "familyName": "Oldroyd", "givenName": "Michael" },
          "age": 30
        }
        """);
using var builder = doc.RootElement.CreateBuilder(workspace);

Person.Mutable root = builder.RootElement;
root.SetAge(31);
root.SetEmail("michael@example.com"u8);

Console.WriteLine(root.ToString());
// {"name":{"familyName":"Oldroyd","givenName":"Michael"},"age":31,"email":"michael@example.com"}
```

You can also build a document from scratch using the convenience `CreateBuilder()` overload with named parameters:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var builder = Person.CreateBuilder(
    workspace,
    name: Person.PersonNameEntity.Build(
        (ref nb) => nb.Create(familyName: "Oldroyd"u8, givenName: "Michael"u8)),
    age: 30,
    email: "michael@example.com"u8);

Console.WriteLine(builder.RootElement.ToString());
```
