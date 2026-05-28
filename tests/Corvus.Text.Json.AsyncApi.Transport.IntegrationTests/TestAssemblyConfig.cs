// <copyright file="TestAssemblyConfig.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using Microsoft.VisualStudio.TestTools.UnitTesting;

// Integration tests start containers that share limited resources (Podman/Docker API,
// port mappings, VM memory). Running test classes in parallel causes non-deterministic
// failures from container startup races. Enforce sequential class execution.
[assembly: DoNotParallelize]