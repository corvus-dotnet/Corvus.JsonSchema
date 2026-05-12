// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

using V5Example.Model;

// ─── Parse a Person from JSON ─────────────────────────────────────────
string personJson = """
    {
        "name": "Alice",
        "age": 30,
        "email": "alice@example.com",
        "address": { "city": "London", "postcode": "SW1A 1AA" },
        "tags": ["developer", "speaker"]
    }
    """;

using var personDoc = ParsedJsonDocument<Person>.Parse(personJson);
Person person = personDoc.RootElement;

Console.WriteLine($"Person: {person.Name}, Age: {person.Age}");
Console.WriteLine($"Address city: {person.AddressValue.City}");
Console.WriteLine($"Tags: {person.Tags}");

// ─── Navigate properties with string and u8 literals ──────────────────
// CTJ001 will fire on the next line — suggesting "name"u8 instead of "name"
if (person.TryGetProperty("name", out JsonElement nameElement))
{
    Console.WriteLine($"Name via string key: {nameElement}");
}

// This is the preferred form — using UTF-8 string literal
if (person.TryGetProperty("name"u8, out JsonElement nameElementU8))
{
    Console.WriteLine($"Name via u8 key: {nameElementU8}");
}

// ─── Parse an Order ───────────────────────────────────────────────────
string orderJson = """
    {
        "orderId": "550e8400-e29b-41d4-a716-446655440000",
        "customer": {
            "name": "Bob",
            "email": "bob@example.com"
        },
        "items": [
            { "product": "Widget", "quantity": 3, "unitPrice": 9.99 },
            { "product": "Gadget", "quantity": 1, "unitPrice": 24.99 }
        ],
        "total": 54.96,
        "status": "pending",
        "notes": "Please gift wrap"
    }
    """;

using var orderDoc = ParsedJsonDocument<Order>.Parse(orderJson);
Order order = orderDoc.RootElement;

Console.WriteLine($"\nOrder: {order.OrderId}");
Console.WriteLine($"Customer: {order.Customer.Name}");
Console.WriteLine($"Items: {order.Items.GetArrayLength()}");
Console.WriteLine($"Total: {order.Total}");
Console.WriteLine($"Status: {order.Status}");

// ─── Match on a oneOf payment type ────────────────────────────────────
string creditCardJson = """
    {
        "type": "credit_card",
        "cardNumber": "4111111111111111",
        "amount": 99.99
    }
    """;

using var paymentDoc = ParsedJsonDocument<Payment>.Parse(creditCardJson);
Payment payment = paymentDoc.RootElement;

// CTJ003 will fire here — lambdas are not static
string description = payment.Match<string>(
    (in cc) => $"Credit card ending {cc.CardNumber.ToString()[^4..]}",
    (in bt) => $"Bank transfer to {bt.AccountNumber}",
    (in p) => "Unknown payment type");

Console.WriteLine($"\nPayment: {description}");

// Preferred form — static lambdas (no CTJ003 diagnostic)
string descriptionStatic = payment.Match<string>(
    static (in cc) => $"Credit card ending {cc.CardNumber.ToString()[^4..]}",
    static (in bt) => $"Bank transfer to {bt.AccountNumber}",
    static (in p) => "Unknown payment type");

Console.WriteLine($"Payment (static): {descriptionStatic}");

using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Basic);
payment.EvaluateSchema(collector);
