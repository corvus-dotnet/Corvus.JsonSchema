namespace Corvus.Text.Json.Playground.Models;

/// <summary>
/// Represents a type in the generated type map.
/// </summary>
/// <param name="TypeName">Short type name (e.g. "Person").</param>
/// <param name="FullTypeName">Fully qualified type name (e.g. "MyNamespace.Person").</param>
/// <param name="Kind">Schema type kind: object, array, string, number, integer, boolean, enum, oneOf, anyOf, allOf, or unknown.</param>
/// <param name="SchemaPointer">JSON Pointer into the source schema (e.g. "#/properties/name").</param>
/// <param name="SourceSchemaName">The schema filename this type originates from (e.g. "person.json").</param>
/// <param name="Properties">Child properties/members for object types.</param>
/// <param name="CompositionGroups">Composition groups (allOf, anyOf, oneOf) for this type.</param>
/// <param name="ArrayItemType">For array types, the item type info.</param>
/// <param name="TupleItems">For tuple types, the ordered list of item type info.</param>
/// <param name="PrefixItems">For non-pure-tuple arrays with prefixItems, the ordered list of prefix item types.</param>
/// <param name="EnumValues">For enum types, the list of allowed values.</param>
/// <param name="ConstValue">For const types, the constant value.</param>
public record TypeMapEntry(
    string TypeName,
    string FullTypeName,
    string Kind,
    string? SchemaPointer,
    string? SourceSchemaName,
    IReadOnlyList<TypeMapProperty> Properties,
    IReadOnlyList<TypeMapCompositionGroup> CompositionGroups,
    TypeMapArrayItemType? ArrayItemType = null,
    IReadOnlyList<TypeMapTupleItem>? TupleItems = null,
    IReadOnlyList<TypeMapTupleItem>? PrefixItems = null,
    IReadOnlyList<string>? EnumValues = null,
    string? ConstValue = null);

/// <summary>
/// Represents a property within a generated type.
/// </summary>
/// <param name="Name">JSON property name (e.g. "firstName").</param>
/// <param name="DotnetPropertyName">C# property accessor name (e.g. "FirstName").</param>
/// <param name="TypeName">Property type short name (e.g. "JsonString").</param>
/// <param name="FullTypeName">Property type fully qualified name (e.g. "Playground.JsonString").</param>
/// <param name="SchemaPointer">JSON Pointer for this property in the schema.</param>
/// <param name="SourceSchemaName">The schema filename this property's type originates from.</param>
/// <param name="IsRequired">Whether the property is required by the schema.</param>
/// <param name="IsComposed">Whether the property is composed rather than locally declared.</param>
public record TypeMapProperty(
    string Name,
    string DotnetPropertyName,
    string TypeName,
    string FullTypeName,
    string? SchemaPointer,
    string? SourceSchemaName,
    bool IsRequired,
    bool IsComposed = false);

/// <summary>
/// A composition group (allOf, anyOf, oneOf) containing referenced type names.
/// </summary>
/// <param name="Keyword">The composition keyword (e.g. "allOf", "anyOf", "oneOf").</param>
/// <param name="Types">The types in this composition group.</param>
public record TypeMapCompositionGroup(
    string Keyword,
    IReadOnlyList<TypeMapCompositionMember> Types);

/// <summary>
/// A single member of a composition group.
/// </summary>
/// <param name="TypeName">Short type name.</param>
/// <param name="FullTypeName">Fully qualified type name.</param>
/// <param name="SchemaPointer">JSON Pointer into the source schema.</param>
/// <param name="SourceSchemaName">The schema filename this type originates from.</param>
/// <param name="ConstValue">For const members, the constant value.</param>
public record TypeMapCompositionMember(
    string TypeName,
    string FullTypeName,
    string? SchemaPointer,
    string? SourceSchemaName,
    string? ConstValue = null);

/// <summary>
/// A single item in a tuple type (prefixItems).
/// </summary>
/// <param name="Index">The 1-based index (Item1, Item2, etc.).</param>
/// <param name="TypeName">Short type name of this tuple item.</param>
/// <param name="FullTypeName">Fully qualified type name.</param>
/// <param name="SchemaPointer">JSON Pointer into the source schema.</param>
/// <param name="SourceSchemaName">The schema filename this type originates from.</param>
public record TypeMapTupleItem(
    int Index,
    string TypeName,
    string FullTypeName,
    string? SchemaPointer,
    string? SourceSchemaName);

/// <summary>
/// The items type for an array (non-tuple).
/// </summary>
/// <param name="TypeName">Short type name of the array item type.</param>
/// <param name="FullTypeName">Fully qualified type name.</param>
/// <param name="SchemaPointer">JSON Pointer into the source schema.</param>
/// <param name="SourceSchemaName">The schema filename this type originates from.</param>
public record TypeMapArrayItemType(
    string TypeName,
    string FullTypeName,
    string? SchemaPointer,
    string? SourceSchemaName);
