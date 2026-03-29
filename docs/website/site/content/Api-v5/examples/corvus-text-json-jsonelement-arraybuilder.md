Use `ArrayBuilder` to compose array values. The `AddItem` method appends elements, and `AddRange`/`InsertRange` accept a delegate for bulk operations.

### Adding items to an array

```csharp
Person.Mutable root = builder.RootElement;

root.Hobbies.AddItem("gardening"u8);
root.Hobbies.InsertItem(0, "cooking"u8);
root.Hobbies.SetItem(1, "swimming"u8);
root.Hobbies.RemoveAt(0);
```

### Bulk operations with AddRange and InsertRange

```csharp
root.Hobbies.AddRange(static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("yoga"u8);
    b.AddItem("hiking"u8);
});

root.Hobbies.InsertRange(1, static (ref JsonElement.ArrayBuilder b) =>
{
    b.AddItem("painting"u8);
    b.AddItem("music"u8);
});
```

### Building a typed array element

```csharp
root.SetItem(14, PersonClosed.Build(
    static (ref PersonClosed.Builder b) => b.Create(
        birthDate: new LocalDate(1820, 1, 17),
        familyName: "Brontë",
        givenName: "Anne",
        height: 1.57)));
```
