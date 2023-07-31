using Corvus.Json;
using Corvus.Json.Patch;

var value = JsonObject.FromProperties(("foo", "hello"), ("bar", "world"));
PatchBuilder builder = value.BeginPatch().DeepAddOrReplaceObjectProperties(3, "/baz/bat/bash");
Console.WriteLine(builder.Value.ToString());