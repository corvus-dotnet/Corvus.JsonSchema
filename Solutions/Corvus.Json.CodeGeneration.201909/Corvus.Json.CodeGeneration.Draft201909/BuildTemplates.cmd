REM This transforms the templates in ./Templates and produces the code in ./Generators
REM See ./Templates/CodeGeneratorTemplate.tt.txt for details

t4 -l -o Generators/CodeGenerator.Array.Add.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorArrayAdd ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.Add.tt
t4 -l -o Generators/CodeGenerator.Array.Remove.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorArrayRemove ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.Remove.tt
t4 -l -o Generators/CodeGenerator.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorArray ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Array.tt
t4 -l -o Generators/CodeGenerator.Boolean.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorBoolean ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Boolean.tt
t4 -l -o Generators/CodeGenerator.Const.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorConst ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Const.tt
t4 -l -o Generators/CodeGenerator.Conversions.Accessors.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorConversionsAccessors ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Conversions.Accessors.tt
t4 -l -o Generators/CodeGenerator.Conversions.Operators.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorConversionsOperators ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Conversions.Operators.tt
t4 -l -o Generators/CodeGenerator.Defaults.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorDefaults ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Defaults.tt
t4 -l -o Generators/CodeGenerator.DependentRequired.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorDependentRequired ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.DependentRequired.tt
t4 -l -o Generators/CodeGenerator.DependentSchema.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorDependentSchema ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.DependentSchema.tt
t4 -l -o Generators/CodeGenerator.Enum.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorEnum ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Enum.tt
t4 -l -o Generators/CodeGenerator.IfThenElse.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorIfThenElse ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.IfThenElse.tt
t4 -l -o Generators/CodeGenerator.Number.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorNumber ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Number.tt
t4 -l -o Generators/CodeGenerator.Object.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorObject ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Object.tt
t4 -l -o Generators/CodeGenerator.Pattern.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorPattern ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Pattern.tt
t4 -l -o Generators/CodeGenerator.PatternProperties.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorPatternProperties ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.PatternProperties.tt
t4 -l -o Generators/CodeGenerator.Properties.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorProperties ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Properties.tt
t4 -l -o Generators/CodeGenerator.String.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorString ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.String.tt
t4 -l -o Generators/CodeGenerator.AnyOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorAnyOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.AnyOf.tt
t4 -l -o Generators/CodeGenerator.OneOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorOneOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.OneOf.tt

t4 -l -o Generators/CodeGenerator.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGenerator ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.tt
t4 -l -o Generators/CodeGenerator.Validate.AllOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateAllOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.AllOf.tt
t4 -l -o Generators/CodeGenerator.Validate.AnyOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateAnyOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.AnyOf.tt
t4 -l -o Generators/CodeGenerator.Validate.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateArray ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Array.tt
t4 -l -o Generators/CodeGenerator.Validate.Format.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateFormat ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Format.tt
t4 -l -o Generators/CodeGenerator.Validate.IfThenElse.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateIfThenElse ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.IfThenElse.tt
t4 -l -o Generators/CodeGenerator.Validate.MediaTypeAndEncoding.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateMediaTypeAndEncoding ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.MediaTypeAndEncoding.tt
t4 -l -o Generators/CodeGenerator.Validate.Not.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateNot ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Not.tt
t4 -l -o Generators/CodeGenerator.Validate.Object.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateObject ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Object.tt
t4 -l -o Generators/CodeGenerator.Validate.OneOf.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateOneOf ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.OneOf.tt
t4 -l -o Generators/CodeGenerator.Validate.Ref.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateRef ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Ref.tt
t4 -l -o Generators/CodeGenerator.Validate.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidate ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.tt
t4 -l -o Generators/CodeGenerator.Validate.Type.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateType ../../Corvus.Json.CodeGeneration.Abstractions/SharedTemplates/CodeGenerator.Validate.Type.tt

REM Overrides for 2019 schema v. shared templates
t4 -l -o Generators/CodeGenerator.Validate.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidate ./Templates/CodeGenerator.Validate.tt
t4 -l -o Generators/CodeGenerator.Validate.Array.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateArray ./Templates/CodeGenerator.Validate.Array.tt
t4 -l -o Generators/CodeGenerator.Validate.RecursiveRef.cs -c Corvus.Json.CodeGeneration.Generators.Draft201909.CodeGeneratorValidateRecursiveRef ./Templates/CodeGenerator.Validate.RecursiveRef.tt

REM Partials
t4 -l -o Generators/CodeGenerator.Array.Add.Partial.cs -p=PartialClassName=CodeGeneratorArrayAdd -p"=PartialFileName=CodeGenerator.Array.Add.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Array.Remove.Partial.cs -p=PartialClassName=CodeGeneratorArrayRemove -p"=PartialFileName=CodeGenerator.Array.Remove.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Array.Partial.cs -p=PartialClassName=CodeGeneratorArray -p"=PartialFileName=CodeGenerator.Array.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Boolean.Partial.cs -p=PartialClassName=CodeGeneratorBoolean -p"=PartialFileName=CodeGenerator.Boolean.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Const.Partial.cs -p=PartialClassName=CodeGeneratorConst -p"=PartialFileName=CodeGenerator.Const.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Conversions.Accessors.Partial.cs -p=PartialClassName=CodeGeneratorConversionsAccessors -p"=PartialFileName=CodeGenerator.Conversions.Accessors.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Conversions.Operators.Partial.cs -p=PartialClassName=CodeGeneratorConversionsOperators -p"=PartialFileName=CodeGenerator.Conversions.Operators.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Defaults.Partial.cs -p=PartialClassName=CodeGeneratorDefaults -p"=PartialFileName=CodeGenerator.Defaults.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.DependentRequired.Partial.cs -p=PartialClassName=CodeGeneratorDependentRequired -p"=PartialFileName=CodeGenerator.DependentRequired.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.DependentSchema.Partial.cs -p=PartialClassName=CodeGeneratorDependentSchema -p"=PartialFileName=CodeGenerator.DependentSchema.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Enum.Partial.cs -p=PartialClassName=CodeGeneratorEnum -p"=PartialFileName=CodeGenerator.Enum.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.IfThenElse.Partial.cs -p=PartialClassName=CodeGeneratorIfThenElse -p"=PartialFileName=CodeGenerator.IfThenElse.Partial.cs" ./Templates/CodeGeneratorPartial.tt
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
t4 -l -o Generators/CodeGenerator.Validate.IfThenElse.Partial.cs -p=PartialClassName=CodeGeneratorValidateIfThenElse -p"=PartialFileName=CodeGenerator.Validate.IfThenElse.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.MediaTypeAndEncoding.Partial.cs -p=PartialClassName=CodeGeneratorValidateMediaTypeAndEncoding -p"=PartialFileName=CodeGenerator.Validate.MediaTypeAndEncoding.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Not.Partial.cs -p=PartialClassName=CodeGeneratorValidateNot -p"=PartialFileName=CodeGenerator.Validate.Not.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Object.Partial.cs -p=PartialClassName=CodeGeneratorValidateObject -p"=PartialFileName=CodeGenerator.Validate.Object.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.OneOf.Partial.cs -p=PartialClassName=CodeGeneratorValidateOneOf -p"=PartialFileName=CodeGenerator.Validate.OneOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Ref.Partial.cs -p=PartialClassName=CodeGeneratorValidateRef -p"=PartialFileName=CodeGenerator.Validate.Ref.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.RecursiveRef.Partial.cs -p=PartialClassName=CodeGeneratorValidateRecursiveRef -p"=PartialFileName=CodeGenerator.Validate.RecursiveRef.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Partial.cs -p=PartialClassName=CodeGeneratorValidate -p"=PartialFileName=CodeGenerator.Validate.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.Validate.Type.Partial.cs -p=PartialClassName=CodeGeneratorValidateType -p"=PartialFileName=CodeGenerator.Validate.Type.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.AnyOf.Partial.cs -p=PartialClassName=CodeGeneratorAnyOf -p"=PartialFileName=CodeGenerator.AnyOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt
t4 -l -o Generators/CodeGenerator.OneOf.Partial.cs -p=PartialClassName=CodeGeneratorOneOf -p"=PartialFileName=CodeGenerator.OneOf.Partial.cs" ./Templates/CodeGeneratorPartial.tt