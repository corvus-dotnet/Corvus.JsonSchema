using Corvus.Text.Json;

namespace CreatingAndMutatingObjects.Models;

/// <summary>
/// A person with name, age, email, and hobbies.
/// Generated from person.json.
/// </summary>
[JsonSchemaTypeGenerator("person.json")]
public readonly partial struct Person;