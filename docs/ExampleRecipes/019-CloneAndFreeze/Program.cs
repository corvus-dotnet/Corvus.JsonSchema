using Corvus.Text.Json;
using CloneAndFreeze.Models;

// ------------------------------------------------------------------
// 1. Parse an order and create a mutable builder
// ------------------------------------------------------------------
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

Console.WriteLine("=== 1. Parsed Order ===");
Console.WriteLine(root);
Console.WriteLine();

// ------------------------------------------------------------------
// 2. Clone — standalone copy that outlives the workspace
// ------------------------------------------------------------------
Order cloned = root.Clone();

Console.WriteLine("=== 2. Clone ===");
Console.WriteLine("Cloned order (standalone, heap-allocated):");
Console.WriteLine(cloned);
Console.WriteLine($"  Customer: {cloned.Customer}");
Console.WriteLine();

// ------------------------------------------------------------------
// 3. Freeze — cheap immutable copy scoped to the workspace
// ------------------------------------------------------------------
Order frozen = root.Freeze();

Console.WriteLine("=== 3. Freeze ===");
Console.WriteLine("Frozen order (immutable, workspace-scoped):");
Console.WriteLine(frozen);
Console.WriteLine($"  Customer: {frozen.Customer}");
Console.WriteLine();

// ------------------------------------------------------------------
// 4. Freeze a nested element — just the first line item
// ------------------------------------------------------------------
Order.RequiredProductAndQuantityArray.RequiredProductAndQuantity firstItem =
    root.Items[0].Freeze();

Console.WriteLine("=== 4. Freeze Nested Element ===");
Console.WriteLine("Frozen first line item:");
Console.WriteLine(firstItem);
Console.WriteLine($"  Product: {firstItem.Product}, Quantity: {firstItem.Quantity}");
Console.WriteLine();

// ------------------------------------------------------------------
// 5. Capturing state before and after mutation
// ------------------------------------------------------------------
Order frozenBefore = root.Freeze();

root.SetNotes("URGENT — ship by Friday");

Order frozenAfter = root.Freeze();

Console.WriteLine("=== 5. Capturing State Before and After Mutation ===");
Console.WriteLine($"Before: notes = {frozenBefore.Notes}");
Console.WriteLine($"After:  notes = {frozenAfter.Notes}");
Console.WriteLine($"Equal? {frozenBefore == frozenAfter}");
Console.WriteLine();

// ------------------------------------------------------------------
// 6. Cross-document freeze — move an item between orders
// ------------------------------------------------------------------
string secondOrderJson =
    """
    {
        "orderId": "ORD-002",
        "customer": "Bob Jones",
        "items": [
            { "product": "Sprocket", "quantity": 5, "price": 3.75 }
        ]
    }
    """;

using var parsedOrder2 = ParsedJsonDocument<Order>.Parse(secondOrderJson);
using var builder2 = parsedOrder2.RootElement.CreateBuilder(workspace);
Order.Mutable root2 = builder2.RootElement;

// Take the first item from order 1 and add it to order 2's items array
root2.Items.AddItem(parsedOrder.RootElement.Items[0]);

Order frozenOrder2 = root2.Freeze();

Console.WriteLine("=== 6. Cross-Document Freeze ===");
Console.WriteLine("Order 2 after adding an item from Order 1:");
Console.WriteLine(frozenOrder2);
Console.WriteLine("Items in Order 2:");
foreach (var item in frozenOrder2.Items.EnumerateArray())
{
    Console.WriteLine($"  {item.Product} x{item.Quantity} @ {item.Price}");
}
Console.WriteLine();

// ------------------------------------------------------------------
// 7. Builder state management — CreateSnapshot and Restore
// ------------------------------------------------------------------
// Capture the builder's current state (rents copies of backing arrays)
using var builderSnapshot = builder.CreateSnapshot();

// Make some experimental changes
root.SetCustomer("Charlie Brown");
root.SetNotes("Expedited shipping");

Console.WriteLine("=== 7. CreateSnapshot and Restore ===");
Console.WriteLine("After experimental changes:");
Console.WriteLine($"  Customer: {root.Customer}");
Console.WriteLine($"  Notes: {root.Notes}");

// Roll back the builder to the captured state — pure memcpy, no allocations
builder.Restore(builderSnapshot);
root = builder.RootElement;

Console.WriteLine("After restore:");
Console.WriteLine($"  Customer: {root.Customer}");
Console.WriteLine($"  Notes: {root.Notes}");
Console.WriteLine();

// ------------------------------------------------------------------
// 8. Clone vs Freeze vs CreateSnapshot — summary
// ------------------------------------------------------------------
Console.WriteLine("=== Clone vs Freeze vs CreateSnapshot ===");
Console.WriteLine("Clone():");
Console.WriteLine("  - Serializes and re-parses into a new document");
Console.WriteLine("  - Heap-allocated; lives independently of the workspace");
Console.WriteLine("  - Use when the result must outlive the workspace");
Console.WriteLine();
Console.WriteLine("Freeze():");
Console.WriteLine("  - Cheap blit of metadata and value arrays");
Console.WriteLine("  - Immutable, but scoped to the workspace lifetime");
Console.WriteLine("  - Use for temporary immutable copies within a processing pipeline");
Console.WriteLine();
Console.WriteLine("CreateSnapshot() / Restore():");
Console.WriteLine("  - Captures the builder's entire internal state");
Console.WriteLine("  - Restore is pure memcpy — no allocations");
Console.WriteLine("  - Use to roll back a builder to a known state without re-parsing");