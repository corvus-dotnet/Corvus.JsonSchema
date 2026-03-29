// <copyright file="Order.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace V5Example.Model;

/// <summary>
/// An order, generated from order.json schema.
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/order.json")]
public readonly partial struct Order;
