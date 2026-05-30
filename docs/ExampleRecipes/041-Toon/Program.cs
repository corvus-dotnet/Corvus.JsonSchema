using System.Buffers;
using System.Text;
using Corvus.Text.Json;
using Corvus.Text.Json.Toon;

// ------------------------------------------------------------------
// 1. Parse TOON to a Corvus document
// ------------------------------------------------------------------
Console.WriteLine("=== Parse TOON to ParsedJsonDocument ===");
Console.WriteLine();

string profileToon = """
    name: Alice
    age: 30
    active: true
    scores[3]: 95,87,92
    """;

using (ParsedJsonDocument<JsonElement> document = ToonDocument.Parse<JsonElement>(profileToon))
{
    JsonElement root = document.RootElement;
    Console.WriteLine($"Name:   {root.GetProperty("name").GetString()}");
    Console.WriteLine($"Age:    {root.GetProperty("age").GetInt32()}");
    Console.WriteLine($"Scores: {root.GetProperty("scores")}");
}

Console.WriteLine();

// ------------------------------------------------------------------
// 2. Convert TOON to JSON
// ------------------------------------------------------------------
Console.WriteLine("=== Convert TOON to JSON ===");
Console.WriteLine();

string peopleToon = """
    [2]{id,name,score}:
      1,Alice,95
      2,Bob,87
    """;

string peopleJson = ToonDocument.ConvertToJsonString(peopleToon);
Console.WriteLine(peopleJson);

Console.WriteLine();

// ------------------------------------------------------------------
// 3. Convert JSON to TOON
// ------------------------------------------------------------------
Console.WriteLine("=== Convert JSON to TOON ===");
Console.WriteLine();

string sourceJson = """[{"id":1,"name":"Alice","score":95},{"id":2,"name":"Bob","score":87}]""";
string generatedToon = ToonDocument.ConvertToToonString(sourceJson);
Console.WriteLine(generatedToon);

Console.WriteLine();

// ------------------------------------------------------------------
// 4. Expand dotted TOON keys when decoding
// ------------------------------------------------------------------
Console.WriteLine("=== Expand dotted TOON keys ===");
Console.WriteLine();

ToonReaderOptions readerOptions = new()
{
    ExpandPaths = ToonPathExpansion.Safe,
};

string expandedJson = ToonDocument.ConvertToJsonString(
    "user.name: Alice\nuser.age: 30",
    readerOptions);

Console.WriteLine(expandedJson);

Console.WriteLine();

// ------------------------------------------------------------------
// 5. Fold JSON object paths when encoding
// ------------------------------------------------------------------
Console.WriteLine("=== Fold JSON object paths ===");
Console.WriteLine();

ToonWriterOptions writerOptions = new()
{
    KeyFolding = ToonKeyFolding.Safe,
};

using ParsedJsonDocument<JsonElement> foldSource =
    ParsedJsonDocument<JsonElement>.Parse("""{"user":{"name":"Alice"},"active":true}""");

JsonElement foldRoot = foldSource.RootElement;
string foldedToon = ToonDocument.ConvertToToon(in foldRoot, writerOptions);

Console.WriteLine(foldedToon);

Console.WriteLine();

// ------------------------------------------------------------------
// 6. Write TOON to a UTF-8 buffer
// ------------------------------------------------------------------
Console.WriteLine("=== Write TOON to a UTF-8 buffer ===");
Console.WriteLine();

ArrayBufferWriter<byte> buffer = new(256);
ToonDocument.ConvertToToon(
    """[{"id":1,"name":"Alice","score":95},{"id":2,"name":"Bob","score":87}]"""u8,
    buffer);

Console.WriteLine(Encoding.UTF8.GetString(buffer.WrittenSpan));