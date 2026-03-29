A `JsonWorkspace` manages pooled memory for mutable JSON operations. Always use a `using` statement to ensure resources are returned.

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(personJson);
using var builder = doc.RootElement.CreateBuilder(workspace);

Person.Mutable root = builder.RootElement;
root.SetAge(31);
```

### Multiple builders sharing a workspace

Several `JsonDocumentBuilder<T>` instances can share a single workspace:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

using var builder1 = doc1.RootElement.CreateBuilder(workspace);
using var builder2 = doc2.RootElement.CreateBuilder(workspace);
```

### Zero-allocation writing with pooled writers

Rent a `Utf8JsonWriter` and buffer from the workspace for high-throughput serialization. The workspace manages a thread-local cache of writers and buffers, so repeated rent/return cycles are allocation-free:

```csharp
using JsonWorkspace workspace = JsonWorkspace.Create();

Utf8JsonWriter writer = workspace.RentWriterAndBuffer(
    defaultBufferSize: 1024,
    out IByteBufferWriter bufferWriter);
try
{
    person.WriteTo(writer);
    writer.Flush();

    ReadOnlySpan<byte> utf8Json = bufferWriter.WrittenSpan;
}
finally
{
    workspace.ReturnWriterAndBuffer(writer, bufferWriter);
}
```
