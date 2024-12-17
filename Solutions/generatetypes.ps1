# Define the base path for the TypeGeneratorTool executable
$toolPath = './Corvus.Json.CodeGenerator/bin/Debug/net8.0/Corvus.Json.JsonSchema.TypeGeneratorTool.exe'

# Run the TypeGeneratorTool for each schema
& $toolPath --rootNamespace Corvus.Json.JsonSchema.Draft4 --outputPath .\Corvus.Json.JsonSchema.Draft4\Draft4\ https://json-schema.org/draft-04/schema
& $toolPath --rootNamespace Corvus.Json.JsonSchema.Draft6 --outputPath .\Corvus.Json.JsonSchema.Draft6\Draft6\ https://json-schema.org/draft-06/schema
& $toolPath --rootNamespace Corvus.Json.JsonSchema.Draft7 --outputPath .\Corvus.Json.JsonSchema.Draft7\Draft7\ https://json-schema.org/draft-07/schema
& $toolPath --rootNamespace Corvus.Json.JsonSchema.Draft201909 --outputPath .\Corvus.Json.JsonSchema.Draft201909\Draft201909\ https://json-schema.org/draft/2019-09/schema
& $toolPath --rootNamespace Corvus.Json.JsonSchema.Draft202012 --outputPath .\Corvus.Json.JsonSchema.Draft202012\Draft202012\ https://json-schema.org/draft/2020-12/schema

& $toolPath --rootNamespace Corvus.Json.JsonSchema.OpenApi30 --outputPath .\Corvus.Json.JsonSchema.OpenApi30\OpenApi30\ --outputRootTypeName OpenApiDocument .\Corvus.Json.JsonSchema.OpenApi30\OpenApi30.json

& $toolPath --rootNamespace Corvus.Json.JsonSchema.OpenApi31 --outputPath .\Corvus.Json.JsonSchema.OpenApi31\OpenApi31\ --outputRootTypeName OpenApiDocument .\Corvus.Json.JsonSchema.OpenApi31\OpenApi31.json

& $toolPath --rootNamespace Corvus.Json.Patch.Model --outputPath .\Corvus.Json.Patch\Corvus.Json.Patch\Model\ .\Corvus.Json.Patch\Corvus.Json.Patch\json-patch.json

& $toolPath --rootNamespace Corvus.Json.Patch.SpecGenerator --outputPath .\Corvus.Json.Patch.SpecGenerator\Model\ --rootPath '#/$defs/Feature' .\Corvus.Json.Patch.SpecGenerator\json-patch-test.json

& $toolPath --rootNamespace Sandbox.Models --outputPath .\Sandbox\PersonModel\ --rootPath '#/$defs/PersonArray' --optionalAsNullable NullOrUndefined .\Corvus.Json.Benchmarking\person-schema.json

& $toolPath --rootNamespace Corvus.Json.Benchmarking.Models.V4 --outputPath .\Corvus.Json.Benchmarking\PersonModel.V4\ --rootPath '#/$defs/PersonArray' .\Corvus.Json.Benchmarking\person-schema.json

& $toolPath config ./coretypesgeneratorconfig.json

& $toolPath --optionalAsNullable NullOrUndefined --disableNamingHeuristic DocumentationNameHeuristic --rootNamespace Corvus.Json.CodeGenerator --outputPath .\Corvus.Json.CodeGenerator\Model\ .\Corvus.Json.CodeGenerator\generator-config.json

& $toolPath --addExplicitUsings --optionalAsNullable NullOrUndefined --disableNamingHeuristic DocumentationNameHeuristic --rootNamespace Sandbox --outputPath .\Sandbox.NoGlobalUsings\Model .\Sandbox.NoGlobalUsings\test-model.json
