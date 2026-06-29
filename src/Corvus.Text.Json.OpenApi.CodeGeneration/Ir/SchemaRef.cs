// <copyright file="SchemaRef.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// A language-neutral handle to a schema referenced from an emit-boundary record.
/// </summary>
/// <remarks>
/// <para>
/// The handle carries only the positional JSON Pointer that identifies the schema in the
/// entry document. An <see cref="ISchemaTypeResolver"/> turns the handle into a
/// language-specific type name (for example a fully qualified .NET type name, or a
/// TypeScript final name).
/// </para>
/// </remarks>
/// <param name="PositionalPointer">
/// The positional JSON Pointer that identifies the schema (e.g.
/// <c>#/paths/~1pets/get/parameters/0/schema</c>).
/// </param>
public readonly record struct SchemaRef(string PositionalPointer);