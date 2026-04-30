// <copyright file="Payment.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Corvus.Text.Json;

namespace V5Example.Model;

/// <summary>
/// A payment — either a credit card or bank transfer (oneOf).
/// </summary>
[JsonSchemaTypeGenerator("../Schemas/payment.json")]
public readonly partial struct Payment;
