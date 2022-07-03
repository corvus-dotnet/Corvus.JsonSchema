using System.Diagnostics;
using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;

Console.WriteLine("Hello, World!");


var jp = new Json.Patch.JsonPatch(
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/11/actor/id"),
        Json.Pointer.JsonPointer.Create("/14/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/12/actor/id"),
        Json.Pointer.JsonPointer.Create("/15/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/13/actor/id"),
        Json.Pointer.JsonPointer.Create("/16/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/14/actor/id"),
        Json.Pointer.JsonPointer.Create("/17/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/15/actor/id"),
        Json.Pointer.JsonPointer.Create("/18/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/16/actor/id"),
        Json.Pointer.JsonPointer.Create("/19/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/17/actor/id"),
        Json.Pointer.JsonPointer.Create("/20/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/18/actor/id"),
        Json.Pointer.JsonPointer.Create("/21/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/19/actor/id"),
        Json.Pointer.JsonPointer.Create("/22/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/20/actor/id"),
        Json.Pointer.JsonPointer.Create("/23/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/21/actor/id"),
        Json.Pointer.JsonPointer.Create("/24/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/22/actor/id"),
        Json.Pointer.JsonPointer.Create("/25/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/23/actor/id"),
        Json.Pointer.JsonPointer.Create("/26/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/24/actor/id"),
        Json.Pointer.JsonPointer.Create("/27/actor/id")),
    Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/25/actor/id"),
        Json.Pointer.JsonPointer.Create("/28/actor/id")),
    Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/26/actor/id"),
        Json.Pointer.JsonPointer.Create("/29/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/27/actor/id"),
        Json.Pointer.JsonPointer.Create("/30/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/28/actor/id"),
        Json.Pointer.JsonPointer.Create("/31/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/29/actor/id"),
        Json.Pointer.JsonPointer.Create("/32/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/30/actor/id"),
        Json.Pointer.JsonPointer.Create("/33/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/31/actor/id"),
        Json.Pointer.JsonPointer.Create("/34/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/32/actor/id"),
        Json.Pointer.JsonPointer.Create("/35/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/33/actor/id"),
        Json.Pointer.JsonPointer.Create("/36/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/34/actor/id"),
        Json.Pointer.JsonPointer.Create("/37/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/35/actor/id"),
        Json.Pointer.JsonPointer.Create("/38/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/36/actor/id"),
        Json.Pointer.JsonPointer.Create("/39/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/37/actor/id"),
        Json.Pointer.JsonPointer.Create("/40/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/38/actor/id"),
        Json.Pointer.JsonPointer.Create("/41/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/39/actor/id"),
        Json.Pointer.JsonPointer.Create("/42/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/40/actor/id"),
        Json.Pointer.JsonPointer.Create("/43/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/41/actor/id"),
        Json.Pointer.JsonPointer.Create("/44/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/42/actor/id"),
        Json.Pointer.JsonPointer.Create("/45/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Create("/43/actor/id"),
        Json.Pointer.JsonPointer.Create("/46/actor/id")));

var someFile = JsonAny.Parse(File.ReadAllText("large-File.json"));
var node = System.Text.Json.Nodes.JsonArray.Create(someFile.AsJsonElement);

var sw2 = Stopwatch.StartNew();
Json.Patch.PatchResult jpResult = jp.Apply(node);
sw2.Stop();

Console.WriteLine($"Time: {sw2.ElapsedMilliseconds}ms");

var patchDocument = PatchOperationArray.From(
    Copy.Create("/11/actor/id", "/14/actor/id"),
    Copy.Create("/12/actor/id", "/15/actor/id"),
    Copy.Create("/13/actor/id", "/16/actor/id"),
    Copy.Create("/14/actor/id", "/17/actor/id"),
    Copy.Create("/15/actor/id", "/18/actor/id"),
    Copy.Create("/16/actor/id", "/19/actor/id"),
    Copy.Create("/17/actor/id", "/20/actor/id"),
    Copy.Create("/18/actor/id", "/21/actor/id"),
    Copy.Create("/19/actor/id", "/22/actor/id"),
    Copy.Create("/20/actor/id", "/23/actor/id"),
    Copy.Create("/21/actor/id", "/24/actor/id"),
    Copy.Create("/22/actor/id", "/25/actor/id"),
    Copy.Create("/23/actor/id", "/26/actor/id"),
    Copy.Create("/24/actor/id", "/27/actor/id"),
    Copy.Create("/25/actor/id", "/28/actor/id"),
    Copy.Create("/26/actor/id", "/29/actor/id"),
    Copy.Create("/27/actor/id", "/30/actor/id"),
    Copy.Create("/28/actor/id", "/31/actor/id"),
    Copy.Create("/29/actor/id", "/32/actor/id"),
    Copy.Create("/30/actor/id", "/33/actor/id"),
    Copy.Create("/31/actor/id", "/34/actor/id"),
    Copy.Create("/31/actor/id", "/35/actor/id"),
    Copy.Create("/33/actor/id", "/36/actor/id"),
    Copy.Create("/34/actor/id", "/37/actor/id"),
    Copy.Create("/35/actor/id", "/38/actor/id"),
    Copy.Create("/36/actor/id", "/39/actor/id"),
    Copy.Create("/37/actor/id", "/40/actor/id"),
    Copy.Create("/38/actor/id", "/41/actor/id"),
    Copy.Create("/39/actor/id", "/42/actor/id"),
    Copy.Create("/40/actor/id", "/43/actor/id"),
    Copy.Create("/41/actor/id", "/44/actor/id"),
    Copy.Create("/42/actor/id", "/45/actor/id"),
    Copy.Create("/43/actor/id", "/46/actor/id")
    );

var someFile2 = JsonAny.Parse(File.ReadAllText("large-File.json"));

var sw = Stopwatch.StartNew();
bool patched = someFile2.TryApplyPatch(patchDocument, out JsonAny result);
sw.Stop();

Console.WriteLine($"Time: {sw.ElapsedMilliseconds}ms");