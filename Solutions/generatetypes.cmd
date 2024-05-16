.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.Draft4 --outputPath .\Corvus.Json.JsonSchema.Draft4\Draft4\ https://json-schema.org/draft-04/schema
.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.Draft6 --outputPath .\Corvus.Json.JsonSchema.Draft6\Draft6\ https://json-schema.org/draft-06/schema
.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.Draft7 --outputPath .\Corvus.Json.JsonSchema.Draft7\Draft7\ https://json-schema.org/draft-07/schema
.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.Draft201909 --outputPath .\Corvus.Json.JsonSchema.Draft201909\Draft201909\ https://json-schema.org/draft/2019-09/schema
.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.Draft202012 --outputPath .\Corvus.Json.JsonSchema.Draft202012\Draft202012\ https://json-schema.org/draft/2020-12/schema

.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.JsonSchema.OpenApi30 --outputPath .\Corvus.Json.JsonSchema.OpenApi30\OpenApi30\ --outputRootTypeName OpenApiDocument https://raw.githubusercontent.com/OAI/OpenAPI-Specification/main/schemas/v3.0/schema.json

.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.Patch.Model --outputPath .\Corvus.Json.Patch\Corvus.Json.Patch\Model\ .\Corvus.Json.Patch\Corvus.Json.Patch\Model\json-patch.json

.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.Patch.SpecGenerator --outputPath .\Corvus.Json.Patch.SpecGenerator\Model\ --rootPath "#/$defs/Feature" .\Corvus.Json.Patch.SpecGenerator\Model\json-patch-test.json

.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.Benchmarking.Models --outputPath .\Sandbox\PersonModel\ --rootPath "#/$defs/PersonArray" .\Sandbox\PersonModel\person-schema.json
.\Corvus.Json.CodeGenerator\bin\Debug\net8.0\Corvus.Json.JsonSchema.TypeGeneratorTool.exe --rootNamespace Corvus.Json.Benchmarking.Models --outputPath .\Corvus.Json.Benchmarking\PersonModel\ --rootPath "#/$defs/PersonArray" .\Sandbox\PersonModel\person-schema.json
