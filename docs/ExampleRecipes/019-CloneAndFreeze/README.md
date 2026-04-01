# JSON Schema Patterns in .NET - Clone and Freeze

This recipe demonstrates the two ways to obtain an immutable snapshot from a mutable document: **Clone** and **Freeze**. Both return a strongly-typed immutable element, but they differ in cost and lifetime.

- **Clone()** — serializes the element and re-parses it into a standalone, heap-allocated document. The result can outlive the workspace and the original builder.
- **Freeze()** — performs a cheap blit of the metadata and value backing arrays. The result is immutable but only valid for the lifetime of the workspace.

## The Schema

File: `order.json`

```json
{
    "title": "Order",
    "type": "object",
    "required": ["orderId", "customer"],
    "properties": {
        "orderId": { "type": "string" },
        "customer": { "type": "string" },
        "items": {
            "type": "array",
            "items": {
                "type": "object",
                "required": ["product", "quantity"],
                "properties": {
                    "product": { "type": "string" },
                    "quantity": { "type": "integer", "format": "int32" },
                    "price": { "type": "number" }
                }
            }
        },
        "notes": { "type": "string" }
    }
}
```

The generated types include:

- `Order` — the root object type
- `Order.RequiredProductAndQuantityArray` — the strongly-typed array of line items
- `Order.RequiredProductAndQuantityArray.RequiredProductAndQuantity` — an individual line item

## Generated Code Usage

[Example code](./Program.cs)

### Parse and create a mutable builder

```csharp
string orderJson =
    """
    {
        "orderId": "ORD-001",
        "customer": "Alice Smith",
        "items": [
            { "product": "Widget", "quantity": 3, "price": 9.99 },
            { "product": "Gadget", "quantity": 1, "price": 24.50 }
        ],
        "notes": "Handle with care"
    }
    """;

using JsonWorkspace workspace = JsonWorkspace.Create();
using var parsedOrder = ParsedJsonDocument<Order>.Parse(orderJson);
using var builder = parsedOrder.RootElement.CreateBuilder(workspace);
Order.Mutable root = builder.RootElement;
```

### Clone — standalone copy

`Clone()` serializes the mutable element and re-parses it into an independent heap-allocated document. The cloned value can be stored, returned from methods, or cached — it has no dependency on the workspace.

```csharp
Order cloned = root.Clone();
Console.WriteLine(cloned);
Console.WriteLine($"  Customer: {cloned.Customer}");
```

### Freeze — workspace-scoped snapshot

`Freeze()` performs a cheap blit of the internal metadata and value arrays. The frozen value is immutable but only valid while the workspace is alive.

```csharp
Order frozen = root.Freeze();
Console.WriteLine(frozen);
Console.WriteLine($"  Customer: {frozen.Customer}");
```

### Freeze a nested element

You can freeze any element in the tree, not just the root. Here we freeze just the first line item:

```csharp
Order.RequiredProductAndQuantityArray.RequiredProductAndQuantity firstItem =
    root.Items[0].Freeze();

Console.WriteLine(firstItem);
Console.WriteLine($"  Product: {firstItem.Product}, Quantity: {firstItem.Quantity}");
```

### Snapshot before and after mutation

Freeze is ideal for capturing point-in-time snapshots of a document that is still being mutated:

```csharp
Order snapshotBefore = root.Freeze();

root.SetNotes("URGENT — ship by Friday");

Order snapshotAfter = root.Freeze();

Console.WriteLine($"Before: notes = {snapshotBefore.Notes}");
Console.WriteLine($"After:  notes = {snapshotAfter.Notes}");
Console.WriteLine($"Snapshots equal? {snapshotBefore == snapshotAfter}");
// Output: Snapshots equal? False
```

### Cross-document freeze

Elements from different documents can be combined in a single builder. Here we take a line item from order 1 and add it to order 2, then freeze the result:

```csharp
using var parsedOrder2 = ParsedJsonDocument<Order>.Parse(secondOrderJson);
using var builder2 = parsedOrder2.RootElement.CreateBuilder(workspace);
Order.Mutable root2 = builder2.RootElement;

// Add the first item from order 1 into order 2's items array
root2.Items.AddItem(parsedOrder.RootElement.Items[0]);

Order frozenOrder2 = root2.Freeze();
Console.WriteLine(frozenOrder2);
```

## Clone vs Freeze — Comparison

| | Clone | Freeze |
|---|---|---|
| **Cost** | Serializes and re-parses (allocates) | Cheap blit of backing arrays |
| **Lifetime** | Independent — outlives workspace | Workspace-scoped |
| **Use when** | The snapshot must be stored, cached, or returned beyond the workspace scope | You need a temporary immutable view during a processing pipeline |

Both methods return the strongly-typed immutable element (e.g., `Order`, not `JsonElement`), so you retain full access to generated properties and schema validation. If the element is already immutable (e.g., from a `ParsedJsonDocument`), both methods return the same instance without additional work.

## Running the Example

```bash
cd docs/ExampleRecipes/019-CloneAndFreeze
dotnet run
```

## Related Patterns

- [001-DataObject](../001-DataObject/) - Creating and manipulating data objects
- [007-CreatingAStronglyTypedArray](../007-CreatingAStronglyTypedArray/) - Mutable array operations (add, remove, insert, replace)
- [016-Maps](../016-Maps/) - Mutable object operations (set, remove properties)
- [017-MappingInputAndOutputValues](../017-MappingInputAndOutputValues/) - Zero-allocation mapping between types