using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Yaml;

// ------------------------------------------------------------------
// 1. Parse YAML to a typed document
// ------------------------------------------------------------------
Console.WriteLine("=== Parse YAML to ParsedJsonDocument ===");
Console.WriteLine();

string yaml = """
    name: Alice
    age: 30
    hobbies:
      - reading
      - cycling
    """;

using (ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(yaml))
{
    JsonElement root = doc.RootElement;
    Console.WriteLine($"Name:    {root.GetProperty("name").GetString()}");
    Console.WriteLine($"Age:     {root.GetProperty("age").GetInt32()}");
    Console.WriteLine($"Hobbies: {root.GetProperty("hobbies")}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 2. Convert to JSON string
// ------------------------------------------------------------------
Console.WriteLine("=== Convert to JSON string ===");
Console.WriteLine();

string json = YamlDocument.ConvertToJsonString(
    Encoding.UTF8.GetBytes("key: value"));
Console.WriteLine($"JSON: {json}");

Console.WriteLine();

// ------------------------------------------------------------------
// 3. Core Schema type resolution
// ------------------------------------------------------------------
Console.WriteLine("=== Core Schema type resolution ===");
Console.WriteLine();

string typesYaml = """
    string: hello
    integer: 42
    hex: 0xFF
    octal: 0o77
    float: 3.14
    null_value: null
    tilde_null: ~
    boolean: true
    """;

json = YamlDocument.ConvertToJsonString(Encoding.UTF8.GetBytes(typesYaml));
Console.WriteLine($"Types: {json}");

Console.WriteLine();

// ------------------------------------------------------------------
// 4. JSON Schema (strict mode)
// ------------------------------------------------------------------
Console.WriteLine("=== JSON Schema (strict mode) ===");
Console.WriteLine();

var jsonSchemaOptions = new YamlReaderOptions { Schema = YamlSchema.Json };
string strictYaml = """
    value: true
    text: hello
    number: 42
    """;

json = YamlDocument.ConvertToJsonString(
    Encoding.UTF8.GetBytes(strictYaml),
    jsonSchemaOptions);
Console.WriteLine($"JSON Schema: {json}");

Console.WriteLine();

// ------------------------------------------------------------------
// 5. YAML 1.1 compatibility (yes/no booleans, merge keys)
// ------------------------------------------------------------------
Console.WriteLine("=== YAML 1.1 compatibility ===");
Console.WriteLine();

var yaml11Options = new YamlReaderOptions { Schema = YamlSchema.Yaml11 };
string yaml11Content = """
    defaults: &defaults
      adapter: postgres
      host: localhost

    development:
      <<: *defaults
      database: dev_db
    """;

using (ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(
    yaml11Content, yaml11Options))
{
    Console.WriteLine($"Development: {doc.RootElement.GetProperty("development")}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 6. Multi-document mode
// ------------------------------------------------------------------
Console.WriteLine("=== Multi-document mode ===");
Console.WriteLine();

var multiOptions = new YamlReaderOptions
{
    DocumentMode = YamlDocumentMode.MultiAsArray,
};

string multiYaml = """
    ---
    name: first
    ---
    name: second
    ---
    name: third
    """;

using (ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(
    multiYaml, multiOptions))
{
    Console.WriteLine($"Documents: {doc.RootElement}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 7. Block scalars (literal and folded)
// ------------------------------------------------------------------
Console.WriteLine("=== Block scalars ===");
Console.WriteLine();

string blockYaml = """
    literal: |
      Line 1
      Line 2
    folded: >
      This is a long
      paragraph.
    """;

using (ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(blockYaml))
{
    Console.WriteLine($"Literal: {doc.RootElement.GetProperty("literal")}");
    Console.WriteLine($"Folded:  {doc.RootElement.GetProperty("folded")}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 8. Duplicate key handling
// ------------------------------------------------------------------
Console.WriteLine("=== Duplicate key handling ===");
Console.WriteLine();

var lastWinsOptions = new YamlReaderOptions
{
    DuplicateKeyBehavior = DuplicateKeyBehavior.LastWins,
};

string dupeYaml = """
    key: first
    key: second
    """;

using (ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(
    dupeYaml, lastWinsOptions))
{
    Console.WriteLine($"Last wins: {doc.RootElement.GetProperty("key")}");
}

// With Error mode (default), duplicates throw YamlException
try
{
    using ParsedJsonDocument<JsonElement> doc = YamlDocument.Parse<JsonElement>(dupeYaml);
}
catch (YamlException ex)
{
    Console.WriteLine($"Error mode: {ex.Message}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 9. Flow style collections
// ------------------------------------------------------------------
Console.WriteLine("=== Flow style collections ===");
Console.WriteLine();

string flowYaml = """
    mapping: {name: Alice, age: 30}
    sequence: [one, two, three]
    nested: {items: [1, 2, {deep: true}]}
    """;

json = YamlDocument.ConvertToJsonString(Encoding.UTF8.GetBytes(flowYaml));
Console.WriteLine($"Flow: {json}");

Console.WriteLine();

// ------------------------------------------------------------------
// 10. ConvertToJsonString with different content
// ------------------------------------------------------------------
Console.WriteLine("=== ConvertToJsonString ===");
Console.WriteLine();

string serverYaml = """
    server:
      host: localhost
      port: 8080
      routes:
        - path: /api
          handler: apiController
        - path: /health
          handler: healthCheck
    """;

json = YamlDocument.ConvertToJsonString(Encoding.UTF8.GetBytes(serverYaml));
Console.WriteLine(json);
