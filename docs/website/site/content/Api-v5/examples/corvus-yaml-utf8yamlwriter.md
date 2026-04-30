Write structured YAML output to a buffer or stream using the standalone System.Text.Json package (no Corvus.Text.Json dependency required).

```csharp
using Corvus.Yaml;

var buffer = new ArrayBufferWriter<byte>();
using (var writer = new Utf8YamlWriter(buffer))
{
    writer.WriteStartMapping();
    writer.WritePropertyName("name"u8);
    writer.WriteStringValue("Alice"u8);
    writer.WritePropertyName("age"u8);
    writer.WriteNumberValue("30"u8);
    writer.WriteEndMapping();
}

string yaml = Encoding.UTF8.GetString(buffer.WrittenSpan);
// yaml:
// name: Alice
// age: 30
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
