// <copyright file="JsonSchemaBuilder.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using System.Collections.Immutable;
using Corvus.Json.CodeGeneration.Generators.Draft201909;

namespace Corvus.Json.CodeGeneration.Draft201909;

/// <summary>
/// A JSON Schema() type builder.
/// </summary>
public class JsonSchemaBuilder : JsonSchemaBuilderBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="JsonSchemaBuilder"/> class.
    /// </summary>
    /// <param name="typeBuilder">The the type builder to use.</param>
    public JsonSchemaBuilder(JsonSchemaTypeBuilder typeBuilder)
        : base(typeBuilder.UseDraft201909())
    {
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The dependent schemas is split in 2019-09 from the base implementation.
    /// </remarks>
    public override TypeDeclaration GetTypeDeclarationForDependentSchema(TypeDeclaration typeDeclaration, string dependentSchema)
    {
        if (typeDeclaration.TryGetTypeDeclarationForMappedProperty("dependentSchemas", dependentSchema, out TypeDeclaration? result))
        {
            return result;
        }

        return typeDeclaration.GetTypeDeclarationForMappedProperty("dependencies", dependentSchema);
    }

    /// <inheritdoc/>
    protected override (JsonReference Location, TypeAndCode TypeAndCode) GenerateFilesForType((JsonReference Location, TypeDeclaration TypeDeclaration) typeForGeneration)
    {
        var codeGenerator = new CodeGenerator(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorOneOf = new CodeGeneratorOneOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorAnyOf = new CodeGeneratorAnyOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArrayAdd = new CodeGeneratorArrayAdd(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArrayRemove = new CodeGeneratorArrayRemove(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorArray = new CodeGeneratorArray(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorBoolean = new CodeGeneratorBoolean(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConst = new CodeGeneratorConst(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConversionsAccessors = new CodeGeneratorConversionsAccessors(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorConversionsOperators = new CodeGeneratorConversionsOperators(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDefaults = new CodeGeneratorDefaults(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDependentRequired = new CodeGeneratorDependentRequired(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorDependentSchema = new CodeGeneratorDependentSchema(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorEnum = new CodeGeneratorEnum(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorIfThenElse = new CodeGeneratorIfThenElse(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorNumber = new CodeGeneratorNumber(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorObject = new CodeGeneratorObject(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorPattern = new CodeGeneratorPattern(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorPatternProperties = new CodeGeneratorPatternProperties(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorProperties = new CodeGeneratorProperties(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorString = new CodeGeneratorString(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateAllOf = new CodeGeneratorValidateAllOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateAnyOf = new CodeGeneratorValidateAnyOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateArray = new CodeGeneratorValidateArray(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateFormat = new CodeGeneratorValidateFormat(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateIfThenElse = new CodeGeneratorValidateIfThenElse(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateMediaTypeAndEncoding = new CodeGeneratorValidateMediaTypeAndEncoding(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateNot = new CodeGeneratorValidateNot(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateObject = new CodeGeneratorValidateObject(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateOneOf = new CodeGeneratorValidateOneOf(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateRef = new CodeGeneratorValidateRef(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidate = new CodeGeneratorValidate(this, typeForGeneration.TypeDeclaration);
        var codeGeneratorValidateType = new CodeGeneratorValidateType(this, typeForGeneration.TypeDeclaration);

        string dotnetTypeName = typeForGeneration.TypeDeclaration.DotnetTypeName!;
        string fileName = GetDottedFileNameFor(typeForGeneration.TypeDeclaration);
        ImmutableArray<CodeAndFilename>.Builder files = ImmutableArray.CreateBuilder<CodeAndFilename>();

        files.Add(new(codeGenerator.TransformText(), $"{fileName}.cs"));
        files.Add(new(codeGeneratorValidate.TransformText(), $"{fileName}.Validate.cs"));

        if (codeGeneratorAnyOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorAnyOf.TransformText(), $"{fileName}.AnyOf.cs"));
        }

        if (codeGeneratorOneOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorOneOf.TransformText(), $"{fileName}.OneOf.cs"));
        }

        if (codeGeneratorArrayAdd.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArrayAdd.TransformText(), $"{fileName}.Array.Add.cs"));
        }

        if (codeGeneratorArrayRemove.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArrayRemove.TransformText(), $"{fileName}.Array.Remove.cs"));
        }

        if (codeGeneratorArray.ShouldGenerate)
        {
            files.Add(new(codeGeneratorArray.TransformText(), $"{fileName}.Array.cs"));
        }

        if (codeGeneratorBoolean.ShouldGenerate)
        {
            files.Add(new(codeGeneratorBoolean.TransformText(), $"{fileName}.Boolean.cs"));
        }

        if (codeGeneratorConst.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConst.TransformText(), $"{fileName}.Const.cs"));
        }

        if (codeGeneratorConversionsAccessors.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConversionsAccessors.TransformText(), $"{fileName}.Conversions.Accessors.cs"));
        }

        if (codeGeneratorConversionsOperators.ShouldGenerate)
        {
            files.Add(new(codeGeneratorConversionsOperators.TransformText(), $"{fileName}.Conversions.Operators.cs"));
        }

        if (codeGeneratorDefaults.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDefaults.TransformText(), $"{fileName}.Defaults.cs"));
        }

        if (codeGeneratorDependentRequired.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDependentRequired.TransformText(), $"{fileName}.DependentRequired.cs"));
        }

        if (codeGeneratorDependentSchema.ShouldGenerate)
        {
            files.Add(new(codeGeneratorDependentSchema.TransformText(), $"{fileName}.DependentSchema.cs"));
        }

        if (codeGeneratorEnum.ShouldGenerate)
        {
            files.Add(new(codeGeneratorEnum.TransformText(), $"{fileName}.Enum.cs"));
        }

        if (codeGeneratorIfThenElse.ShouldGenerate)
        {
            files.Add(new(codeGeneratorIfThenElse.TransformText(), $"{fileName}.IfThenElse.cs"));
        }

        if (codeGeneratorNumber.ShouldGenerate)
        {
            files.Add(new(codeGeneratorNumber.TransformText(), $"{fileName}.Number.cs"));
        }

        if (codeGeneratorObject.ShouldGenerate)
        {
            files.Add(new(codeGeneratorObject.TransformText(), $"{fileName}.Object.cs"));
        }

        if (codeGeneratorPattern.ShouldGenerate)
        {
            files.Add(new(codeGeneratorPattern.TransformText(), $"{fileName}.Pattern.cs"));
        }

        if (codeGeneratorPatternProperties.ShouldGenerate)
        {
            files.Add(new(codeGeneratorPatternProperties.TransformText(), $"{fileName}.PatternProperties.cs"));
        }

        if (codeGeneratorProperties.ShouldGenerate)
        {
            files.Add(new(codeGeneratorProperties.TransformText(), $"{fileName}.Properties.cs"));
        }

        if (codeGeneratorString.ShouldGenerate)
        {
            files.Add(new(codeGeneratorString.TransformText(), $"{fileName}.String.cs"));
        }

        if (codeGeneratorValidateAllOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateAllOf.TransformText(), $"{fileName}.Validate.AllOf.cs"));
        }

        if (codeGeneratorValidateAnyOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateAnyOf.TransformText(), $"{fileName}.Validate.AnyOf.cs"));
        }

        if (codeGeneratorValidateArray.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateArray.TransformText(), $"{fileName}.Validate.Array.cs"));
        }

        if (codeGeneratorValidateFormat.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateFormat.TransformText(), $"{fileName}.Validate.Format.cs"));
        }

        if (codeGeneratorValidateIfThenElse.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateIfThenElse.TransformText(), $"{fileName}.Validate.IfThenElse.cs"));
        }

        if (codeGeneratorValidateMediaTypeAndEncoding.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateMediaTypeAndEncoding.TransformText(), $"{fileName}.Validate.MediaTypeAndEncoding.cs"));
        }

        if (codeGeneratorValidateNot.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateNot.TransformText(), $"{fileName}.Validate.Not.cs"));
        }

        if (codeGeneratorValidateObject.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateObject.TransformText(), $"{fileName}.Validate.Object.cs"));
        }

        if (codeGeneratorValidateOneOf.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateOneOf.TransformText(), $"{fileName}.Validate.OneOf.cs"));
        }

        if (codeGeneratorValidateRef.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateRef.TransformText(), $"{fileName}.Validate.Ref.cs"));
        }

        if (codeGeneratorValidateType.ShouldGenerate)
        {
            files.Add(new(codeGeneratorValidateType.TransformText(), $"{fileName}.Validate.Type.cs"));
        }

        return new(
            typeForGeneration.Location,
            new(dotnetTypeName, files.ToImmutable()));
    }
}