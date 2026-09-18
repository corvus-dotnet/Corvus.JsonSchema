// <copyright file="JsonSchemaLockFileModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Json.CodeGenerator;

/// <summary>
/// The <c>corvusjson-jsonschema.lock</c> file a JSON Schema code generation run leaves in its output folder.
/// </summary>
[Corvus.Json.JsonSchemaTypeGenerator("./jsonschema-lock.json")]
public readonly partial struct JsonSchemaLockFileModel
{
}