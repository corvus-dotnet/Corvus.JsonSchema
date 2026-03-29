The following example parses a JSON document from a string and accesses typed properties on the root element.

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(jsonString);
Person person = doc.RootElement;

string familyName = (string)person.FamilyName;
```

You can also parse from UTF-8 bytes, which avoids the cost of transcoding from UTF-16:

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(utf8Bytes);
```

The returned document implements `IDisposable` and **must** be disposed to return pooled memory. Always use a `using` statement or `using` declaration:

```csharp
using ParsedJsonDocument<Person> doc =
    ParsedJsonDocument<Person>.Parse(jsonString);
Person person = doc.RootElement;

// Work with person...

// Memory is returned when 'doc' is disposed at the end of the scope
```
