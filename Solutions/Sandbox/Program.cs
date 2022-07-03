using System.Diagnostics;
using Corvus.Json;
using Corvus.Json.Patch;
using Corvus.Json.Patch.Model;

Console.WriteLine("Hello, World!");


var jp = new Json.Patch.JsonPatch(
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/11/actor/id"),
        Json.Pointer.JsonPointer.Parse("/14/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/12/actor/id"),
        Json.Pointer.JsonPointer.Parse("/15/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/13/actor/id"),
        Json.Pointer.JsonPointer.Parse("/16/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/14/actor/id"),
        Json.Pointer.JsonPointer.Parse("/17/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/15/actor/id"),
        Json.Pointer.JsonPointer.Parse("/18/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/16/actor/id"),
        Json.Pointer.JsonPointer.Parse("/19/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/17/actor/id"),
        Json.Pointer.JsonPointer.Parse("/20/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/18/actor/id"),
        Json.Pointer.JsonPointer.Parse("/21/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/19/actor/id"),
        Json.Pointer.JsonPointer.Parse("/22/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/20/actor/id"),
        Json.Pointer.JsonPointer.Parse("/23/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/21/actor/id"),
        Json.Pointer.JsonPointer.Parse("/24/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/22/actor/id"),
        Json.Pointer.JsonPointer.Parse("/25/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/23/actor/id"),
        Json.Pointer.JsonPointer.Parse("/26/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/24/actor/id"),
        Json.Pointer.JsonPointer.Parse("/27/actor/id")),
    Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/25/actor/id"),
        Json.Pointer.JsonPointer.Parse("/28/actor/id")),
    Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/26/actor/id"),
        Json.Pointer.JsonPointer.Parse("/29/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/27/actor/id"),
        Json.Pointer.JsonPointer.Parse("/30/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/28/actor/id"),
        Json.Pointer.JsonPointer.Parse("/31/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/29/actor/id"),
        Json.Pointer.JsonPointer.Parse("/32/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/30/actor/id"),
        Json.Pointer.JsonPointer.Parse("/33/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/31/actor/id"),
        Json.Pointer.JsonPointer.Parse("/34/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/32/actor/id"),
        Json.Pointer.JsonPointer.Parse("/35/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/33/actor/id"),
        Json.Pointer.JsonPointer.Parse("/36/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/34/actor/id"),
        Json.Pointer.JsonPointer.Parse("/37/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/35/actor/id"),
        Json.Pointer.JsonPointer.Parse("/38/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/36/actor/id"),
        Json.Pointer.JsonPointer.Parse("/39/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/37/actor/id"),
        Json.Pointer.JsonPointer.Parse("/40/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/38/actor/id"),
        Json.Pointer.JsonPointer.Parse("/41/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/39/actor/id"),
        Json.Pointer.JsonPointer.Parse("/42/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/40/actor/id"),
        Json.Pointer.JsonPointer.Parse("/43/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/41/actor/id"),
        Json.Pointer.JsonPointer.Parse("/44/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/42/actor/id"),
        Json.Pointer.JsonPointer.Parse("/45/actor/id")),
        Json.Patch.PatchOperation.Copy(
        Json.Pointer.JsonPointer.Parse("/43/actor/id"),
        Json.Pointer.JsonPointer.Parse("/46/actor/id")));

var someFile = JsonAny.Parse(File.ReadAllText("large-array-file.json"));
var node = System.Text.Json.Nodes.JsonArray.Create(someFile.AsJsonElement);

Json.Patch.PatchResult jpResult = jp.Apply(node);

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

var someFile2 = JsonAny.Parse(File.ReadAllText("large-array-file.json"));

bool patched = someFile2.TryApplyPatch(patchDocument, out JsonAny result);
