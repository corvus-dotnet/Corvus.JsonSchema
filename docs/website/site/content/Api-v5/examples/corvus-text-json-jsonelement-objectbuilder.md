Use `ObjectBuilder` inside a `Build()` delegate to compose object values with named properties.

```csharp
Person.PersonNameEntity name = Person.PersonNameEntity.Build(
    (ref Person.PersonNameEntity.Builder b) => b.Create(
        familyName: "Oldroyd"u8,
        givenName: "Michael"u8));
```

The `AddProperty` method adds key-value pairs to the builder. It is used internally by the `Create()` method and is available for advanced scenarios where you need to build an object dynamically:

```csharp
using var builder = workspace.BuildDocument<JsonElement.Mutable>(
    initialCapacity: 10,
    initialValueBufferSize: 256);

JsonElement.Mutable root = builder.RootElement;
root.SetProperty(
    "name"u8,
    JsonElement.Build(static (ref JsonElement.ObjectBuilder b) =>
    {
        b.AddProperty("familyName"u8, "Oldroyd"u8);
        b.AddProperty("givenName"u8, "Michael"u8);
    }));
```
