The following example obtains a mutable element from a builder and modifies properties.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(personJson);
using var builder = doc.RootElement.CreateBuilder(workspace);

Person.Mutable root = builder.RootElement;
root.SetAge(31);
root.SetEmail("michael@example.com"u8);
```

### Version tracking

The builder tracks a version number, and every `Mutable` reference records the version at which it was obtained. If the document structure changes after you captured an intermediate reference, using that stale reference throws `InvalidOperationException`.

The **root element is always live** — it never needs to be re-obtained:

```csharp
Person.Mutable root = builder.RootElement;  // always live — cache freely
root.RemoveEmail();              // structural change
root.Address.SetCity("London"u8); // still valid — navigated from root
```

You can also make multiple modifications to the *same* child entity without re-obtaining it, because its own version is refreshed on modification:

```csharp
Person.Mutable root = builder.RootElement;
Person.AddressEntity.Mutable address = root.Address;
address.SetCity("London"u8);
address.SetZipCode("SE3"u8);  // same entity — still valid
```

However, caching an intermediate reference and using it **after a sibling mutation** is not permitted:

```csharp
Person.Mutable root = builder.RootElement;
Person.AddressEntity.Mutable address = root.Address;
address.SetCity("London"u8);       // OK

root.SetAge(32);                   // structural change on a sibling

address.SetZipCode("SE3"u8);      // throws InvalidOperationException — stale reference
```

### Removing properties

Optional properties can be removed from mutable instances:

```csharp
root.RemoveEmail();
```
