// <copyright file="NonParallelizable.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

#pragma warning disable SA1402 // File may only contain a single type

namespace Features.JsonModel;

/// <summary>
/// Non parallelizable fixtures.
/// </summary>
[NUnit.Framework.NonParallelizable]
public partial class JsonSerializationWithSerializerAndInefficientDeserializationIsDisabledFeature
{
}

/// <summary>
/// Non parallelizable fixtures.
/// </summary>
[NUnit.Framework.NonParallelizable]
public partial class JsonSerializationWithSerializerFeature
{
}