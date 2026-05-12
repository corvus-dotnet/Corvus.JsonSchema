// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json;

namespace Corvus.ClassicBenchmarkModels;

[JsonSchemaTypeGenerator("../person-array-schema.json#/$defs/PersonArray", EmitEvaluator = true)]
public readonly partial struct PersonArray;