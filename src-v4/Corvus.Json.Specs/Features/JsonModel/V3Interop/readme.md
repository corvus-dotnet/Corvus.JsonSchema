The basictype.json file is a json schema that is used to generate the types found in the Model.v3 folder.

You should use the current version of the V3 [Corvus.Json.JsonSchema.TypeGeneratorTool](https://www.nuget.org/packages/Corvus.Json.JsonSchema.TypeGeneratorTool)
if you need to re-generate these types.

The command line parameters are:

```
 --rootNamespace Model.V3 --outputPath Model.V3 .\basictypes.json
 ``