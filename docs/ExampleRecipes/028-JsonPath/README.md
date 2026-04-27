# JSONPath Query Language — Example Recipe

Demonstrates the key features of the `Corvus.Text.Json.JsonPath` library using the classic [Goessner bookstore](https://goessner.net/articles/JsonPath/) dataset.

## What it shows

| Feature | JSONPath expression |
|---|---|
| Property access | `$.store.bicycle.color` |
| Wildcard | `$.store.book[*].author` |
| Recursive descent | `$..author` |
| Index access | `$.store.book[0].title` |
| Negative index | `$.store.book[-1].title` |
| Array slicing | `$.store.book[0:2].title` |
| Filter expression | `$.store.book[?@.price<10].title` |
| Logical operators | `$.store.book[?@.price<10 && @.category=='fiction'].title` |
| Function extension | `$.store.book[?length(@.title)>15].title` |
| Zero-allocation QueryNodes | `$.store.book[*].title` with span buffer |
| Source-generated expression | `$..author` from `all-authors.jsonpath` |

## Running the example

```bash
dotnet run --project docs/ExampleRecipes/028-JsonPath/JsonPath.csproj
```

## Key files

| File | Purpose |
|---|---|
| `bookstore.json` | Sample JSON data |
| `all-authors.jsonpath` | JSONPath expression used by the source generator |
| `Expressions.cs` | Source-generated expression class |
| `Program.cs` | Main program — demonstrates all evaluation modes |
