REM This transforms the templates in ./Templates and produces the code in ./Generators
REM See ./Templates/CodeGeneratorTemplate.tt.txt for details

t4 -l -o Generators/CodeGenerator.Array.Add.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorArrayAdd ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.Add.tt
t4 -l -o Generators/CodeGenerator.Array.Remove.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorArrayRemove ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.Remove.tt
t4 -l -o Generators/CodeGenerator.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorArray ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.tt
t4 -l -o Generators/CodeGenerator.Boolean.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorBoolean ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Boolean.tt
t4 -l -o Generators/CodeGenerator.Conversions.Accessors.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorConversionsAccessors ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Conversions.Accessors.tt
t4 -l -o Generators/CodeGenerator.Conversions.Operators.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorConversionsOperators ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Conversions.Operators.tt
t4 -l -o Generators/CodeGenerator.Defaults.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorDefaults ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Defaults.tt
t4 -l -o Generators/CodeGenerator.DependentRequired.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorDependentRequired ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.DependentRequired.tt
t4 -l -o Generators/CodeGenerator.DependentSchema.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorDependentSchema ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.DependentSchema.tt
t4 -l -o Generators/CodeGenerator.Enum.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorEnum ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Enum.tt
t4 -l -o Generators/CodeGenerator.Number.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorNumber ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Number.tt
t4 -l -o Generators/CodeGenerator.Object.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorObject ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Object.tt
t4 -l -o Generators/CodeGenerator.Pattern.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorPattern ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Pattern.tt
t4 -l -o Generators/CodeGenerator.PatternProperties.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorPatternProperties ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.PatternProperties.tt
t4 -l -o Generators/CodeGenerator.Properties.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorProperties ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Properties.tt
t4 -l -o Generators/CodeGenerator.String.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorString ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.String.tt
t4 -l -o Generators/CodeGenerator.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGenerator ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.tt
t4 -l -o Generators/CodeGenerator.Validate.AllOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateAllOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.AllOf.tt
t4 -l -o Generators/CodeGenerator.Validate.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateArray ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Array.tt
t4 -l -o Generators/CodeGenerator.Validate.Format.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateFormat ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Format.tt
t4 -l -o Generators/CodeGenerator.Validate.MediaTypeAndEncoding.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateMediaTypeAndEncoding ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.MediaTypeAndEncoding.tt
t4 -l -o Generators/CodeGenerator.Validate.Not.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateNot ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Not.tt
t4 -l -o Generators/CodeGenerator.Validate.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidate ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.tt
t4 -l -o Generators/CodeGenerator.Validate.Type.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateType ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Type.tt
t4 -l -o Generators/CodeGenerator.AnyOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorAnyOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.AnyOf.tt
t4 -l -o Generators/CodeGenerator.OneOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorOneOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.OneOf.tt

REM Overrides for Draft 4 schema v. shared templates
t4 -l -o Generators/CodeGenerator.Validate.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidate ./Templates/CodeGenerator.Validate.tt
t4 -l -o Generators/CodeGenerator.Validate.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateArray ./Templates/CodeGenerator.Validate.Array.tt
t4 -l -o Generators/CodeGenerator.Validate.AnyOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateAnyOf ./Templates/CodeGenerator.Validate.AnyOf.tt
t4 -l -o Generators/CodeGenerator.Validate.OneOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateOneOf ./Templates/CodeGenerator.Validate.OneOf.tt
t4 -l -o Generators/CodeGenerator.Validate.Object.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateObject ./Templates/CodeGenerator.Validate.Object.tt
t4 -l -o Generators/CodeGenerator.Validate.Format.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateFormat ./Templates/CodeGenerator.Validate.Format.tt
t4 -l -o Generators/CodeGenerator.Validate.MediaTypeAndEncoding.cs -c Corvus.Json.CodeGeneration.Generators.Draft4.CodeGeneratorValidateMediaTypeAndEncoding ./Templates/CodeGenerator.Validate.MediaTypeAndEncoding.tt

REM Partials
t4 -l -o Generators/CodeGenerator.Array.Add.Partial.cs -p=PartialClassName=CodeGeneratorArrayAdd -p"=PartialFileName=CodeGenerator.Array.Add.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Array.Remove.Partial.cs -p=PartialClassName=CodeGeneratorArrayRemove -p"=PartialFileName=CodeGenerator.Array.Remove.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Array.Partial.cs -p=PartialClassName=CodeGeneratorArray -p"=PartialFileName=CodeGenerator.Array.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Boolean.Partial.cs -p=PartialClassName=CodeGeneratorBoolean -p"=PartialFileName=CodeGenerator.Boolean.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Conversions.Accessors.Partial.cs -p=PartialClassName=CodeGeneratorConversionsAccessors -p"=PartialFileName=CodeGenerator.Conversions.Accessors.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Conversions.Operators.Partial.cs -p=PartialClassName=CodeGeneratorConversionsOperators -p"=PartialFileName=CodeGenerator.Conversions.Operators.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Defaults.Partial.cs -p=PartialClassName=CodeGeneratorDefaults -p"=PartialFileName=CodeGenerator.Defaults.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.DependentRequired.Partial.cs -p=PartialClassName=CodeGeneratorDependentRequired -p"=PartialFileName=CodeGenerator.DependentRequired.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.DependentSchema.Partial.cs -p=PartialClassName=CodeGeneratorDependentSchema -p"=PartialFileName=CodeGenerator.DependentSchema.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Enum.Partial.cs -p=PartialClassName=CodeGeneratorEnum -p"=PartialFileName=CodeGenerator.Enum.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Number.Partial.cs -p=PartialClassName=CodeGeneratorNumber -p"=PartialFileName=CodeGenerator.Number.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Object.Partial.cs -p=PartialClassName=CodeGeneratorObject -p"=PartialFileName=CodeGenerator.Object.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Pattern.Partial.cs -p=PartialClassName=CodeGeneratorPattern -p"=PartialFileName=CodeGenerator.Pattern.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.PatternProperties.Partial.cs -p=PartialClassName=CodeGeneratorPatternProperties -p"=PartialFileName=CodeGenerator.PatternProperties.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Properties.Partial.cs -p=PartialClassName=CodeGeneratorProperties -p"=PartialFileName=CodeGenerator.Properties.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.String.Partial.cs -p=PartialClassName=CodeGeneratorString -p"=PartialFileName=CodeGenerator.String.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Partial.cs -p=PartialClassName=CodeGenerator -p"=PartialFileName=CodeGenerator.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.AllOf.Partial.cs -p=PartialClassName=CodeGeneratorValidateAllOf -p"=PartialFileName=CodeGenerator.Validate.AllOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.AnyOf.Partial.cs -p=PartialClassName=CodeGeneratorValidateAnyOf -p"=PartialFileName=CodeGenerator.Validate.AnyOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Array.Partial.cs -p=PartialClassName=CodeGeneratorValidateArray -p"=PartialFileName=CodeGenerator.Validate.Array.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Format.Partial.cs -p=PartialClassName=CodeGeneratorValidateFormat -p"=PartialFileName=CodeGenerator.Validate.Format.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.MediaTypeAndEncoding.Partial.cs -p=PartialClassName=CodeGeneratorValidateMediaTypeAndEncoding -p"=PartialFileName=CodeGenerator.Validate.MediaTypeAndEncoding.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Not.Partial.cs -p=PartialClassName=CodeGeneratorValidateNot -p"=PartialFileName=CodeGenerator.Validate.Not.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Object.Partial.cs -p=PartialClassName=CodeGeneratorValidateObject -p"=PartialFileName=CodeGenerator.Validate.Object.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.OneOf.Partial.cs -p=PartialClassName=CodeGeneratorValidateOneOf -p"=PartialFileName=CodeGenerator.Validate.OneOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Partial.cs -p=PartialClassName=CodeGeneratorValidate -p"=PartialFileName=CodeGenerator.Validate.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Type.Partial.cs -p=PartialClassName=CodeGeneratorValidateType -p"=PartialFileName=CodeGenerator.Validate.Type.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.AnyOf.Partial.cs -p=PartialClassName=CodeGeneratorAnyOf -p"=PartialFileName=CodeGenerator.AnyOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.OneOf.Partial.cs -p=PartialClassName=CodeGeneratorOneOf -p"=PartialFileName=CodeGenerator.OneOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt