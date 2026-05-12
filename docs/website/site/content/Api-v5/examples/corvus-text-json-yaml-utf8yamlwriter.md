Write structured YAML output to a buffer or stream using the low-level writer API.

```csharp
using Corvus.Text.Json.Yaml;

var buffer = new ArrayBufferWriter<byte>();
using (var writer = new Utf8YamlWriter(buffer))
{
    writer.WriteStartMapping();
    writer.WritePropertyName("name"u8);
    writer.WriteStringValue("Alice"u8);
    writer.WritePropertyName("age"u8);
    writer.WriteNumberValue("30"u8);
    writer.WritePropertyName("active"u8);
    writer.WriteBooleanValue(true);
    writer.WriteEndMapping();
}

string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
// yaml:
// name: Alice
// age: 30
// active: true
```

### Nested mappings and sequences

```csharp
using (var writer = new Utf8YamlWriter(buffer))
{
    writer.WriteStartMapping();

    writer.WritePropertyName("person"u8);
    writer.WriteStartMapping();
    writer.WritePropertyName("name"u8);
    writer.WriteStringValue("Alice"u8);
    writer.WriteEndMapping();

    writer.WritePropertyName("hobbies"u8);
    writer.WriteStartSequence();
    writer.WriteStringValue("reading"u8);
    writer.WriteStringValue("cycling"u8);
    writer.WriteEndSequence();

    writer.WriteEndMapping();
}
```

### Flow-style collections

Use `YamlCollectionStyle.Flow` for compact inline output:

```csharp
using (var writer = new Utf8YamlWriter(buffer))
{
    writer.WriteStartMapping();
    writer.WritePropertyName("tags"u8);
    writer.WriteStartSequence(YamlCollectionStyle.Flow);
    writer.WriteStringValue("admin"u8);
    writer.WriteStringValue("user"u8);
    writer.WriteEndSequence();
    writer.WriteEndMapping();
}
// tags: [admin, user]
```

### Writing to a stream

```csharp
using var stream = new MemoryStream();
using (var writer = new Utf8YamlWriter(stream))
{
    writer.WriteStartMapping();
    writer.WritePropertyName("key"u8);
    writer.WriteStringValue("value"u8);
    writer.WriteEndMapping();
    writer.Flush();
}
```
