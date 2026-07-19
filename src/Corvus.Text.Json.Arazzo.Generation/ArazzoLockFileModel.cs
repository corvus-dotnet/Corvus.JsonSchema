// <copyright file="ArazzoLockFileModel.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.Arazzo.Generation;

/// <summary>
/// The persisted Arazzo code-generation lock file (<c>corvusjson-arazzo.lock</c>), generated from
/// <c>Schemas/corvusjson-arazzo-lock.json</c>. It records the Arazzo document hash, the generation parameters, the
/// generated file list, and — the Arazzo-specific part — the set of resolved source descriptions each pinned by the
/// SHA-256 digest of the exact bytes loaded, so a regeneration can be skipped when nothing changed (incremental) and a
/// remote-fetched generation is reproducible. Mirrors <c>OpenApiLockFileModel</c>.
/// </summary>
[JsonSchemaTypeGenerator("Schemas/corvusjson-arazzo-lock.json")]
public readonly partial struct ArazzoLockFileModel;