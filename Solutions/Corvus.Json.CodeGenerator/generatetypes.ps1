# Define the base path for the TypeGeneratorTool executable
$toolPath = '../Corvus.Json.CodeGenerator/bin/Debug/net8.0/Corvus.Json.JsonSchema.TypeGeneratorTool.exe'

# Run the TypeGeneratorTool for each schema
& $toolPath --rootNamespace Corvus.Json.CodeGenerator --disableNamingHeuristic DocumentationNameHeuristic --optionalAsNullable NullOrUndefined --outputPath ./Model ./generator-config.json
