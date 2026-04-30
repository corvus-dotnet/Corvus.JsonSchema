// <copyright file="YamlEventCallback.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#if STJ
namespace Corvus.Yaml;
#else
namespace Corvus.Text.Json.Yaml;
#endif

/// <summary>
/// A callback delegate invoked for each YAML parse event during
/// <see cref="YamlDocument.EnumerateEvents(System.ReadOnlySpan{byte}, YamlEventCallback, YamlReaderOptions)"/>.
/// </summary>
/// <param name="yamlEvent">The current YAML event. This is only valid for the duration of the callback;
/// do not store the event or its <see cref="YamlEvent.Value"/>, <see cref="YamlEvent.Anchor"/>,
/// or <see cref="YamlEvent.Tag"/> spans beyond the callback return.</param>
/// <returns><see langword="true"/> to continue parsing, or <see langword="false"/> to stop early.</returns>
public delegate bool YamlEventCallback(in YamlEvent yamlEvent);