# Migration Analyzers

The `Corvus.Text.Json.Migration.Analyzers` NuGet package provides Roslyn analyzers that detect V4 (`Corvus.Json`) API patterns in your code and guide you toward the equivalent V5 (`Corvus.Text.Json`) patterns. Many of the diagnostics include code fixes that transform your code toward the V5 pattern.

> **Important:** Applying a code fix does not guarantee compilable code. The fixes get you closer to the correct V5 solution, but you will often need to make additional changes (e.g., introducing a `JsonWorkspace`, adjusting variable lifetimes, or updating surrounding code). Work through diagnostics steadily rather than applying them in bulk.

## Installation

Add the analyzer package to any project that references V4 `Corvus.Json` types:

```xml
<PackageReference Include="Corvus.Text.Json.Migration.Analyzers" Version="1.0.0" PrivateAssets="all" />
```

The analyzers run at build time and produce warnings. They do **not** change any runtime behavior — they only guide your migration.

## Tiers

The analyzers are organized into two tiers:

| Tier | Description |
|------|-------------|
| **Tier 1: Code Fix Available** | These diagnostics include code fixes that transform your code toward the V5 pattern. The result may not compile immediately — review and adjust surrounding code after applying each fix. |
| **Tier 2: Guidance** | These diagnostics flag patterns that require manual migration because the V5 equivalent involves structural changes (e.g., introducing a `JsonWorkspace`, converting functional mutation to imperative). The diagnostic message describes the required change. |

---

## Tier 1: Code Fix Available

### CVJ001 — Migrate namespace

**Severity:** Warning · **Code fix:** ✅ Yes

Detects `using Corvus.Json` directives and replaces them with the corresponding `Corvus.Text.Json` namespace.

```csharp
// Before (V4)
using Corvus.Json;
using Corvus.Json.Internal;

// After (V5)
using Corvus.Text.Json;
using Corvus.Text.Json.Internal;
```

---

### CVJ002 — Migrate `ParsedValue<T>` to `ParsedJsonDocument<T>`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects usage of `ParsedValue<T>` (the V4 parsed document wrapper) and replaces it with `ParsedJsonDocument<T>`. Also flags `.Instance` property access, which becomes `.RootElement` in V5.

```csharp
// Before (V4)
using ParsedValue<Person> parsed = ParsedValue<Person>.Parse(json);
Person person = parsed.Instance;

// After (V5)
using ParsedJsonDocument<Person> parsed = ParsedJsonDocument<Person>.Parse(json);
Person person = parsed.RootElement;
```

---

### CVJ003 — Migrate validation to `EvaluateSchema()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects calls to V4's `.IsValid()` and `.Validate(context, level)` methods and replaces them with V5's `.EvaluateSchema()`.

```csharp
// Before (V4)
bool valid = person.IsValid();
ValidationContext result = person.Validate(ValidationContext.ValidContext, ValidationLevel.Detailed);

// After (V5)
bool valid = person.EvaluateSchema();
// For detailed results, use the collector overload:
JsonSchemaResultsCollector collector = new();
bool valid = person.EvaluateSchema(collector);
```

---

### CVJ004 — Migrate `As<T>()` to `T.From()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects the V4 generic coercion method `value.As<T>()` and replaces it with the V5 static factory `T.From(value)`.

```csharp
// Before (V4)
Person person = element.As<Person>();

// After (V5)
Person person = Person.From(element);
```

---

### CVJ005 — Migrate `.Count` to `.GetPropertyCount()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects access to the V4 `.Count` property on JSON objects and replaces it with the V5 `.GetPropertyCount()` method.

```csharp
// Before (V4)
int count = jsonObject.Count;

// After (V5)
int count = jsonObject.GetPropertyCount();
```

---

### CVJ006 — Migrate `FromJson()` to `From()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects calls to the V4 `T.FromJson(element)` factory method and renames it to V5's `T.From(element)`.

```csharp
// Before (V4)
Person person = Person.FromJson(jsonElement);

// After (V5)
Person person = Person.From(jsonElement);
```

---

### CVJ007 — Migrate V4 core type to `JsonElement`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects usage of V4 untyped core types (`JsonAny`, `JsonNull`) that map directly to `JsonElement` in V5.

```csharp
// Before (V4)
JsonAny any = JsonAny.Parse(json);

// After (V5)
JsonElement any = JsonElement.Parse(json);
```

> **Note:** `JsonAny` always maps to `JsonElement`. For other V4 core types like `JsonString`, `JsonObject`, etc., see [CVJ009](#cvj009-v4-typed-core-type-may-need-replacement).

---

### CVJ008 — Migrate `JsonDocument.Parse()` to `ParsedJsonDocument<T>.Parse()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects usage of `System.Text.Json.JsonDocument.Parse()` and replaces it with `ParsedJsonDocument<T>.Parse()`. When followed by a `T.FromJson(doc.RootElement)` call, the code fix collapses both statements into a single `ParsedJsonDocument<T>.Parse()`.

```csharp
// Before (V4)
using var doc = JsonDocument.Parse(json);
Person person = Person.FromJson(doc.RootElement);

// After (V5) — collapsed to a single statement
using var doc = ParsedJsonDocument<Person>.Parse(json);
Person person = doc.RootElement;
```

When `JsonDocument.Parse()` appears without a `FromJson` follow-up, it is replaced with `ParsedJsonDocument<JsonElement>`:

```csharp
// Before (V4)
using var doc = JsonDocument.Parse(json);

// After (V5)
using var doc = ParsedJsonDocument<JsonElement>.Parse(json);
```

---

### CVJ011 — V4 immutable mutation replaced by mutable builder

**Severity:** Warning · **Code fix:** ✅ Yes (structural rewrite)

Detects V4's functional mutation methods that return a new immutable instance. In V5, you create a mutable builder from a `JsonWorkspace` and mutate in-place. This covers:

- **`With*()`** on generated types (renamed to `Set*()` in V5)
- **`SetProperty()`** on `JsonObject` / `IJsonObject<T>` types (name unchanged in V5, but called on `.Mutable`)
- **`RemoveProperty()`** on `JsonObject` / `IJsonObject<T>` types (name unchanged in V5, but called on `.Mutable`)

The code fix handles several patterns:

**`With*()` rename and unchain:**
```csharp
// Before (V4)
Person updated = person.WithName("Bob").WithAge(25);

// After (V5) — inside a mutable builder context
person.SetName("Bob");
person.SetAge(25);
```

**`SetProperty()` unchain and `.Mutable` rewrite:**
```csharp
// Before (V4)
JsonObject updated = person
    .SetProperty("age", (JsonNumber)31)
    .SetProperty("email", (JsonString)"alice@example.com");

// After (V5) — receiver becomes .Mutable, calls are unchained
person.SetProperty("age", 31);
person.SetProperty("email", "alice@example.com");
```

**`RemoveProperty()` on core types:**
```csharp
// Before (V4)
JsonObject updated = person.RemoveProperty("temporaryField");

// After (V5)
person.RemoveProperty("temporaryField");
```

**Nested extract-mutate-reassign collapse (with CVJ021):**
```csharp
// Before (V4)
Address address = person.AddressValue;
Address updated = address.WithCity("Manchester");
Person result = person.WithAddressValue(updated);

// After (V5)
person.AddressValue.SetCity("Manchester");
```

---

### CVJ012 — V4 functional array operations

**Severity:** Warning · **Code fix:** ✅ Yes

Detects all V4 functional array methods and rewrites them to V5 mutable builder equivalents. The code fix drops the assignment (since V5 mutates in-place), renames methods where needed, and rewrites the receiver variable type to `.Mutable`.

| V4 Method | V5 Equivalent | Renamed? |
|---|---|---|
| `Add(item)` | `AddItem(source)` | ✅ Yes |
| `AddRange(items)` | `AddRange(builder)` | ✅ Yes (uses builder delegate) |
| `Insert(idx, item)` | `InsertItem(idx, source)` | ✅ Yes |
| `InsertRange(idx, items)` | `InsertRange(idx, builder)` | ✅ Yes (uses builder delegate) |
| `SetItem(idx, val)` | `SetItem(idx, source)` | ❌ Same name |
| `RemoveAt(idx)` | `RemoveAt(idx)` | ❌ Same name |
| `Remove(val)` | `Remove(item)` | ❌ Same name |
| `RemoveRange(idx, count)` | `RemoveRange(idx, count)` | ❌ Same name |
| `Replace(old, new)` | `Replace(old, new)` | ❌ Same name |

```csharp
// Before (V4) — renamed methods
JsonArray updated = array.Add(newItem);
JsonArray replaced = array.Insert(0, item);

// After (V5)
array.AddItem(newItem);
array.InsertItem(0, item);
```

```csharp
// Before (V4) — same-name methods
JsonArray result = array.Remove(oldItem);
JsonArray trimmed = array.RemoveRange(0, 2);
JsonArray swapped = array.Replace(oldItem, newItem);

// After (V5) — assignment dropped, receiver becomes .Mutable
array.Remove(oldItem);
array.RemoveRange(0, 2);
array.Replace(oldItem, newItem);
```

---

### CVJ013 — V4 `Create()` replaced by `CreateBuilder()` or `Build()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects V4's static `Create(...)` factory method for constructing objects. The code fix chooses between two V5 patterns based on how the result is used:

- **Top-level (used as an instance):** `CreateBuilder(workspace, ...)` — the result is a mutable builder that you can call methods on.
- **Nested (used as a source value):** `Build(...)` — the result is passed into another construction or argument. When the result is assigned to an explicitly typed variable, the type is rewritten to `Type.Source` (since `Build()` returns a Source, not the entity type).

```csharp
// Before (V4) — top-level, used as an instance
Person person = Person.Create(name: "Alice", age: 30);
person.IsValid(); // member access → builder

// After (V5)
using JsonWorkspace workspace = JsonWorkspace.Create();
Person person = Person.CreateBuilder(workspace, name: "Alice", age: 30);
person.EvaluateSchema();
```

```csharp
// Before (V4) — nested, passed as argument
parent.SetChild(Person.Create(name: "Alice", age: 30));

// After (V5) — no workspace needed
parent.SetChild(Person.Build(name: "Alice", age: 30));
```

```csharp
// Before (V4) — assigned to variable, then passed as argument
Person child = Person.Create(name: "Alice", age: 30);
parent.SetChild(child);

// After (V5) — type becomes Person.Source
Person.Source child = Person.Build(name: "Alice", age: 30);
parent.SetChild(child);
```

---

### CVJ014 — V4 `FromItems()` replaced by `Build()` pattern

**Severity:** Warning · **Code fix:** ✅ Yes

Detects V4's `FromItems(...)` array factory. The code fix wraps items in `Build()` when used at the top level (inside `CreateBuilder`), or replaces with `Build()` directly when nested. Explicitly typed variables are rewritten to `Type.Source`.

```csharp
// Before (V4) — top-level
MyArray arr = MyArray.FromItems(item1, item2, item3);

// After (V5) — builder wrapping Build()
using JsonWorkspace workspace = JsonWorkspace.Create();
MyArray arr = MyArray.CreateBuilder(workspace, MyArray.Build(item1, item2, item3));
```

```csharp
// Before (V4) — nested, passed as argument
parent.SetItems(MyArray.FromItems(item1, item2));

// After (V5)
parent.SetItems(MyArray.Build(item1, item2));
```

---

### CVJ015 — V4 `FromValues()` replaced by `CreateBuilder()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects V4's `FromValues(span)` numeric array factory. In V5, use `CreateBuilder(workspace, span)` at top level or `Build(span)` when nested. Explicitly typed variables are rewritten to `Type.Source`.

```csharp
// Before (V4)
MyVector vec = MyVector.FromValues(stackalloc double[] { 1.0, 2.0, 3.0 });

// After (V5) — top-level
using JsonWorkspace workspace = JsonWorkspace.Create();
MyVector vec = MyVector.CreateBuilder(workspace, stackalloc double[] { 1.0, 2.0, 3.0 });
```

```csharp
// Before (V4) — nested
parent.SetVector(MyVector.FromValues(stackalloc double[] { 1.0, 2.0 }));

// After (V5)
parent.SetVector(MyVector.Build(stackalloc double[] { 1.0, 2.0 }));
```

---

### CVJ018 — Migrate `TryGetString()` to `TryGetValue()`

**Severity:** Warning · **Code fix:** ✅ Yes

Detects calls to V4's `.TryGetString(out string)` and renames it to V5's `.TryGetValue(out string)`.

```csharp
// Before (V4)
if (element.TryGetString(out string value)) { ... }

// After (V5)
if (element.TryGetValue(out string value)) { ... }
```

---

### CVJ021 — Nested mutation chain can use deep property setter

**Severity:** Warning · **Code fix:** ✅ Yes (via CVJ011 code fix)

Detects the V4 pattern of extracting a nested value, mutating it, and reassigning it back to the parent. In V5, this collapses to a single deep setter on the mutable builder because mutations are visible through the parent.

This diagnostic detects all mutator types — not just `With*()`:

- **Object mutators:** `With*()`, `SetProperty()`, `RemoveProperty()`
- **Array mutators:** `Add()`, `Insert()`, `SetItem()`, `RemoveAt()`

**Object property mutation:**
```csharp
// Before (V4)
Address address = person.AddressValue;
Address updatedAddress = address.WithCity("Manchester");
Person updatedPerson = person.WithAddressValue(updatedAddress);

// After (V5)
person.AddressValue.SetCity("Manchester");
```

**Nested SetProperty:**
```csharp
// Before (V4)
JsonObject address = root["address"].As<JsonObject>();
JsonObject updatedAddress = address.SetProperty("city", (JsonString)"Manchester");
JsonObject updatedRoot = root.SetProperty("address", updatedAddress.AsAny);

// After (V5)
root["address"].SetProperty("city", "Manchester");
```

**Nested array mutation:**
```csharp
// Before (V4)
JsonArray roles = person["roles"].As<JsonArray>();
JsonArray updatedRoles = roles.Add((JsonString)"admin");
Person updatedPerson = person.WithRoles(updatedRoles);

// After (V5)
person.Roles.AddItem("admin");
```

---

## Tier 2: Guidance Diagnostics

These diagnostics flag patterns that require manual migration. The V5 equivalents involve structural changes that cannot be automated reliably.

### CVJ009 — V4 typed core type may need replacement

**Severity:** Warning · **Code fix:** ❌ No (requires project-specific knowledge)

Detects usage of V4 typed core types like `JsonString`, `JsonNumber`, `JsonObject`, `JsonArray`, `JsonBoolean`, `JsonInteger`. In V5, these may map to:
- `JsonElement` (if no schema-backed type is needed), or
- A project-local generated global type (e.g., `JsonString`, `JsonInt32`)

The correct replacement depends on your project's generated types.

```csharp
// Before (V4)
JsonObject obj = JsonObject.Parse(json);
JsonString name = person.Name;

// After (V5) — depends on your schema
// Option A: Use JsonElement if no schema type exists
JsonElement obj = JsonElement.Parse(json);
// Option B: Use generated global type
JsonString name = person.Name;  // project-local JsonString
```

---

### CVJ010 — V4 `As*` accessors removed

**Severity:** Warning · **Code fix:** ❌ No

Detects access to V4's `.AsString`, `.AsNumber`, `.AsObject`, `.AsArray`, `.AsBoolean`, and `.AsAny` properties. These do not exist in V5. In V5, multi-core-type types emit all value accessors directly — use explicit casts or `TryGetValue()` instead.

```csharp
// Before (V4)
string name = element.AsString;
double value = element.AsNumber;

// After (V5)
string name = (string)element;
// or
element.TryGetValue(out string name);
```

---

### CVJ016 — V5 uses `Corvus.Text.Json.Utf8JsonWriter`

**Severity:** Warning · **Code fix:** ❌ No

Detects `WriteTo()` calls that pass a `System.Text.Json.Utf8JsonWriter`. V5 uses its own `Corvus.Text.Json.Utf8JsonWriter` type.

```csharp
// Before (V4)
person.WriteTo(systemUtf8JsonWriter);

// After (V5)
person.WriteTo(corvusUtf8JsonWriter);
// where corvusUtf8JsonWriter is a Corvus.Text.Json.Utf8JsonWriter
```

---

### CVJ019 — V4 backing model APIs removed

**Severity:** Warning · **Code fix:** ❌ No

Detects access to V4's backing model properties (`.HasJsonElementBacking`, `.HasDotnetBacking`). V5 uses a single document-index model — these properties do not exist.

```csharp
// Before (V4)
if (person.HasJsonElementBacking) { ... }

// After (V5)
// Remove — V5 always uses document-index backing.
// If you need to check whether a value was parsed vs. constructed,
// use other approaches specific to your use case.
```

---

### CVJ025 — Replace V4 package reference

**Severity:** Warning · **Code fix:** ❌ No

Fires at compilation end when V4 assembly references are detected. Guides you to replace V4 packages with their V5 equivalents.

| V4 Package | V5 Replacement |
|---|---|
| `Corvus.Json.ExtendedTypes` | `Corvus.Text.Json` |
| `Corvus.Json.SourceGenerator` | `Corvus.Text.Json.SourceGenerator` |

---

## Complete V4→V5 Mutator Equivalence Table

This table lists every V4 functional mutation method and its V5 mutable builder equivalent.

### Object Mutators

| V4 Method | V5 Equivalent | Diagnostic | Notes |
|---|---|---|---|
| `With*()` (generated) | `Set*()` on `.Mutable` | CVJ011 | Renamed; code fix handles |
| `SetProperty(name, val)` | `SetProperty(name, source)` on `.Mutable` | CVJ011 | Same name; assignment dropped |
| `RemoveProperty(name)` | `RemoveProperty(name)` on `.Mutable` | CVJ011 | Same name; assignment dropped |

### Array Mutators

| V4 Method | V5 Equivalent | Diagnostic | Notes |
|---|---|---|---|
| `Add(item)` | `AddItem(source)` | CVJ012 | Renamed |
| `AddRange(items)` | `AddRange(builder)` | CVJ012 | Uses builder delegate pattern |
| `Insert(idx, item)` | `InsertItem(idx, source)` | CVJ012 | Renamed |
| `InsertRange(idx, items)` | `InsertRange(idx, builder)` | CVJ012 | Uses builder delegate pattern |
| `SetItem(idx, val)` | `SetItem(idx, source)` | CVJ012 | Same name |
| `RemoveAt(idx)` | `RemoveAt(idx)` | CVJ012 | Same name |
| `Remove(val)` | `Remove(item)` | CVJ012 | Same name |
| `RemoveRange(idx, count)` | `RemoveRange(idx, count)` | CVJ012 | Same name |
| `Replace(old, new)` | `Replace(old, new)` | CVJ012 | Same name |

### V5-only Methods (no V4 equivalent)

| V5 Method | Description |
|---|---|
| `RemoveWhere(predicate)` | Remove all array elements matching a predicate |
| `AddRange(builder)` | Add multiple items using a builder delegate |
| `InsertRange(idx, builder)` | Insert multiple items at an index using a builder delegate |
| `AddRange(ReadOnlySpan<T>)` | Add numeric spans (on `ArrayBuilder`, not `Mutable`) |

## Suppressing Diagnostics

If a diagnostic is not relevant to your project, you can suppress it in your `.editorconfig`:

```ini
[*.cs]
dotnet_diagnostic.CVJ010.severity = none
```

Or suppress individual occurrences with a pragma:

```csharp
#pragma warning disable CVJ010
string name = element.AsString;
#pragma warning restore CVJ010
```

---

## Migration Workflow

We recommend working through diagnostics steadily, one file or one area at a time, rather than bulk-applying fixes across an entire project. Code fixes move your code toward V5 patterns, but they do not guarantee compilable output — you will typically need to adjust surrounding code after each fix (e.g., introducing a `JsonWorkspace`, updating variable types, or adapting control flow).

1. **Install the analyzer package** and build your project to see all diagnostics.
2. **Work through one file at a time.** Review each diagnostic, apply the code fix if available, then compile and address any remaining errors before moving on.
3. **Address Tier 2 guidance manually** — these require structural changes. The diagnostic messages link to the relevant section of the [migration guide](/docs/migrating-from-v4-to-v5.html).
4. **Use [GitHub Copilot](/docs/using-copilot-for-migration.html) as a migration partner.** Point Copilot at the diagnostic warnings in a file and ask it to complete the migration. The diagnostics serve as a checklist of patterns Copilot can help transform.
5. **Remove the analyzer package** once migration is complete.

## Reserved Diagnostic IDs

The following IDs were previously assigned and will not be reused:

| ID |
|---|
| CVJ017 |
| CVJ020 |
