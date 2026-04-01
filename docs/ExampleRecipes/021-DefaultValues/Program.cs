using Corvus.Text.Json;
using DefaultValues.Models;

// ------------------------------------------------------------------
// 1. Parse minimal config — just the required "host" property
// ------------------------------------------------------------------
using var minimalDoc = ParsedJsonDocument<Config>.Parse(
    """{"host":"localhost"}""");
Config minimal = minimalDoc.RootElement;

Console.WriteLine("=== Minimal config (only 'host' provided) ===");
Console.WriteLine($"host:      {(string)minimal.Host}");
Console.WriteLine($"port:      {(int)minimal.Port}");
Console.WriteLine($"timeout:   {(int)minimal.Timeout}");
Console.WriteLine($"retries:   {(int)minimal.Retries}");
Console.WriteLine($"logLevel:  {(string)minimal.LogLevel}");
Console.WriteLine($"enableSsl: {(bool)minimal.EnableSsl}");
Console.WriteLine();

// The property getters on the immutable type automatically return the
// schema-declared default when a property is missing from the JSON.
// The underlying JSON still only contains {"host":"localhost"}.
Console.WriteLine($"Raw JSON: {minimal}");
Console.WriteLine();

// ------------------------------------------------------------------
// 2. Access default values directly via DefaultInstance
// ------------------------------------------------------------------
// Each property entity type with a "default" in the schema exposes a
// static DefaultInstance property containing the schema-declared value.
Console.WriteLine("=== Schema-declared defaults (via DefaultInstance) ===");
Console.WriteLine($"Config.PortEntity.DefaultInstance:      {(int)Config.PortEntity.DefaultInstance}");
Console.WriteLine($"Config.TimeoutEntity.DefaultInstance:   {(int)Config.TimeoutEntity.DefaultInstance}");
Console.WriteLine($"Config.RetriesEntity.DefaultInstance:   {(int)Config.RetriesEntity.DefaultInstance}");
Console.WriteLine($"Config.LogLevelEntity.DefaultInstance:  {(string)Config.LogLevelEntity.DefaultInstance}");
Console.WriteLine($"Config.EnableSslEntity.DefaultInstance: {(bool)Config.EnableSslEntity.DefaultInstance}");
Console.WriteLine();

// ------------------------------------------------------------------
// 3. Parse full config — explicit values override defaults
// ------------------------------------------------------------------
using var fullDoc = ParsedJsonDocument<Config>.Parse(
    """
    {
        "host": "api.example.com",
        "port": 443,
        "timeout": 60,
        "retries": 5,
        "logLevel": "debug",
        "enableSsl": false
    }
    """);
Config full = fullDoc.RootElement;

Console.WriteLine("=== Full config (all values provided) ===");
Console.WriteLine($"host:      {(string)full.Host}");
Console.WriteLine($"port:      {(int)full.Port}");
Console.WriteLine($"timeout:   {(int)full.Timeout}");
Console.WriteLine($"retries:   {(int)full.Retries}");
Console.WriteLine($"logLevel:  {(string)full.LogLevel}");
Console.WriteLine($"enableSsl: {(bool)full.EnableSsl}");
Console.WriteLine();

// ------------------------------------------------------------------
// 4. Validate — a config with just "host" passes validation
// ------------------------------------------------------------------
Console.WriteLine("=== Validation ===");
Console.WriteLine($"Minimal config valid: {minimal.EvaluateSchema()}");
Console.WriteLine($"Full config valid:    {full.EvaluateSchema()}");
Console.WriteLine();

// ------------------------------------------------------------------
// 5. Create with defaults — use CreateBuilder to set only the host
// ------------------------------------------------------------------
using JsonWorkspace workspace = JsonWorkspace.Create();
using var builtDoc = Config.CreateBuilder(workspace, host: "built.example.com");
Config built = builtDoc.RootElement;

// The generated JSON only contains the properties we explicitly set.
Console.WriteLine("=== Built config (only host set via CreateBuilder) ===");
Console.WriteLine($"Generated JSON: {built}");
Console.WriteLine();

// Property getters still return schema defaults for missing properties.
Console.WriteLine($"host:      {(string)built.Host}");
Console.WriteLine($"port:      {(int)built.Port}");
Console.WriteLine($"timeout:   {(int)built.Timeout}");
Console.WriteLine($"retries:   {(int)built.Retries}");
Console.WriteLine($"logLevel:  {(string)built.LogLevel}");
Console.WriteLine($"enableSsl: {(bool)built.EnableSsl}");