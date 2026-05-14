using Corvus.Text.Json;
using Corvus.Text.Json.OpenApi;
using Corvus.Text.Json.OpenApi.CodeGeneration;
using Corvus.Text.Json.OpenApi31;

string json = File.ReadAllText(@"tests\\Corvus.Text.Json.OpenApi.CodeGeneration.Tests\\TestData\\petstore-3.1.json");
using var doc = ParsedJsonDocument<Corvus.Text.Json.JsonElement>.Parse(json);
var root = doc.RootElement.Clone();
var model = ClientModelBuilder.Build(root, new OpenApi31Walker());

var schemaTypeMap = new Dictionary<string, string>(StringComparer.Ordinal)
{
    ["#/paths/~1pets/get/parameters/0/schema"] = "Petstore.Client.JsonInt32",
    ["#/paths/~1pets~1{petId}/get/parameters/0/schema"] = "Petstore.Client.JsonString",
    ["#/paths/~1pets/post/requestBody/content/application~1json/schema"] = "Petstore.Client.NewPet",
    ["#/paths/~1pets/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pets",
    ["#/paths/~1pets/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
    ["#/paths/~1pets/post/responses/201/content/application~1json/schema"] = "Petstore.Client.Pet",
    ["#/paths/~1pets/post/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
    ["#/paths/~1pets~1{petId}/get/responses/200/content/application~1json/schema"] = "Petstore.Client.Pet",
    ["#/paths/~1pets~1{petId}/get/responses/default/content/application~1json/schema"] = "Petstore.Client.Error",
};

var emitter = new ClientCodeEmitter("Petstore.Client", schemaTypeMap);
var files = emitter.Emit(model);

string outDir = @"petstore-generated";
Directory.CreateDirectory(outDir);

foreach (var f in Directory.GetFiles(outDir, "*Request.cs").Concat(Directory.GetFiles(outDir, "*Response.cs")).Concat(Directory.GetFiles(outDir, "IApi*.cs")).Concat(Directory.GetFiles(outDir, "Api*Client.cs")))
    File.Delete(f);

foreach (var file in files)
{
    File.WriteAllText(Path.Combine(outDir, file.FileName), file.Content);
    Console.WriteLine(file.FileName);
}
Console.WriteLine($"Wrote {files.Count} files");
