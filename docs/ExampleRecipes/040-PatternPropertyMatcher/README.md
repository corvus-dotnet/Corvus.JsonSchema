# JSON Schema Patterns in .NET - Pattern Property Matcher

This recipe demonstrates generated helpers for JSON Schema `patternProperties`. These helpers let you classify dynamic object properties by regex pattern without writing the property-enumeration boilerplate yourself.

## The Pattern

JSON objects often mix known properties with groups of dynamic properties whose names follow a convention. For example:

- `S_region`, `S_status` - string-valued metrics
- `I_cpu`, `I_memory` - integer-valued metrics
- `name` - a known property that does not belong to either dynamic group

JSON Schema models this with `patternProperties`: each regex describes a set of property names, and each regex has a schema for the matching values.

## The Schema

File: `metric-bag.json`

```json
{
  "title": "Metric Bag",
  "type": "object",
  "properties": {
    "name": {
      "type": "string"
    }
  },
  "required": [ "name" ],
  "patternProperties": {
    "^S_": {
      "type": "string"
    },
    "^I_": {
      "type": "integer",
      "format": "int32"
    }
  },
  "additionalProperties": false
}
```

This schema allows `name`, any string-valued property whose name starts with `S_`, and any `int32` property whose name starts with `I_`.

## Generated Code Usage

### Parsing a value

```csharp
string json = """
    {
      "name": "server-01",
      "S_region": "eu-west",
      "S_status": "green",
      "I_cpu": 42,
      "I_memory": 73
    }
    """;

using ParsedJsonDocument<MetricBag> parsed = ParsedJsonDocument<MetricBag>.Parse(json);
MetricBag metrics = parsed.RootElement;
```

### Testing property-name patterns

The generator emits static `MatchesPattern*` helpers for each pattern-property value type. Use UTF-8 string literals for zero-allocation name checks.

```csharp
System.Console.WriteLine(MetricBag.MatchesPatternJsonString("S_region"u8));
// Output: True

System.Console.WriteLine(MetricBag.MatchesPatternJsonInt32("I_cpu"u8));
// Output: True
```

### Casting a matching property value

Use `TryAsPattern*` when you already have a property name and value and want the typed pattern-property value.

```csharp
if (metrics.TryGetProperty("S_status"u8, out JsonElement statusValue) &&
    MetricBag.TryAsPatternJsonString("S_status"u8, in statusValue, out JsonString status))
{
    System.Console.WriteLine($"Status metric: {status}");
    // Output: Status metric: green
}
```

### Dispatching all pattern properties

For full object traversal, implement the generated visitor interface and call `MatchPatternProperties`. The visitor receives decoded UTF-8 property names and strongly typed values for each matching pattern.

```csharp
MetricSummary summary = default;
metrics.MatchPatternProperties(ref summary, MetricVisitor.Instance);

System.Console.WriteLine($"String metrics: {summary.StringMetricCount}");
System.Console.WriteLine($"Integer metrics: {summary.IntegerMetricCount}");
System.Console.WriteLine($"CPU: {summary.Cpu}");
```

The visitor handles each generated pattern and the unmatched case:

```csharp
readonly struct MetricVisitor : MetricBag.IPatternPropertyVisitor<MetricSummary>
{
    public static readonly MetricVisitor Instance = new();

    public bool VisitPatternJsonString(ReadOnlySpan<byte> name, in JsonString value, ref MetricSummary state)
    {
        state.StringMetricCount++;
        return true;
    }

    public bool VisitPatternJsonInt32(ReadOnlySpan<byte> name, in JsonInt32 value, ref MetricSummary state)
    {
        state.IntegerMetricCount++;

        if (name.SequenceEqual("I_cpu"u8))
        {
            state.Cpu = (int)value;
        }

        return true;
    }

    public bool VisitUnmatched(ReadOnlySpan<byte> name, in JsonElement value, ref MetricSummary state)
    {
        if (name.SequenceEqual("name"u8))
        {
            state.Name = value.GetString();
        }

        return true;
    }
}
```

## Short-Circuiting

By default, if a property name matches more than one pattern, every matching visitor method is called. Pass `shortCircuit: true` to stop after the first match for each property:

```csharp
metrics.MatchPatternProperties(ref summary, MetricVisitor.Instance, shortCircuit: true);
```

## Running the Example

```bash
cd docs/ExampleRecipes/040-PatternPropertyMatcher
dotnet run
```

## Related Patterns

- [012-PatternMatching](../012-PatternMatching/) - Discriminated union pattern matching with `oneOf`
- [016-Maps](../016-Maps/) - Dynamic object maps with `additionalProperties`
- [004-OpenVersusClosedTypes](../004-OpenVersusClosedTypes/) - Open and closed object shapes
