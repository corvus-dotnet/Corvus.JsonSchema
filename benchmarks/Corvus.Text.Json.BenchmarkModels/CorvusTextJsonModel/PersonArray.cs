// Derived from code licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licensed this code under the MIT license.

using Corvus.Text.Json;

namespace Benchmark.CorvusTextJson;

[JsonSchemaTypeGenerator("../person-schema.json#/$defs/PersonArray")]
public readonly partial struct PersonArray;