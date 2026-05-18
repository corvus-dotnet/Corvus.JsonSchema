// <copyright file="SchemaReference.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.OpenApi.CodeGeneration;

/// <summary>
/// Represents a reference to a schema within an OpenAPI specification.
/// </summary>
/// <remarks>
/// <para>
/// Each schema reference carries two pointer strings:
/// </para>
/// <list type="bullet">
/// <item>
/// <see cref="PositionalPointer"/> — a JSON Pointer reflecting the schema's positional
/// location in the entry document (e.g. <c>#/paths/~1pets/get/parameters/0/schema</c>).
/// Used by the naming heuristic and as the key in the schema-type map.
/// </item>
/// <item>
/// <see cref="ResolvablePointer"/> — a URI-reference (possibly with a document path) that
/// the JSON Schema type builder can resolve directly. When the component is inline, this
/// is identical to <see cref="PositionalPointer"/>. When it comes from a <c>$ref</c>,
/// this is the resolved reference with the base URI applied (e.g.
/// <c>#/components/parameters/PetId/schema</c> for same-document, or
/// <c>./common/types.json#/components/parameters/OrderId/schema</c> for external).
/// </item>
/// </list>
/// </remarks>
/// <param name="PositionalPointer">
/// The positional JSON Pointer in the entry document. Always starts with <c>#</c>.
/// </param>
/// <param name="ResolvablePointer">
/// The resolvable reference for the type builder. May be a fragment-only pointer (same
/// document) or a relative/absolute URI with fragment (external document).
/// </param>
public readonly record struct SchemaReference(string PositionalPointer, string ResolvablePointer);