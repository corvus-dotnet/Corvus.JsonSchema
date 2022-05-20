// <copyright file="EnableParallelTestExecution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NUnit.Framework;

// Note: SpecFlow doesn't support parallelization of multiple scenarios within a single feature,
// so this is the highest level of parallelization available to us. See:
// https://docs.specflow.org/projects/specflow/en/latest/Execution/Parallel-Execution.html#nunit-configuration
[assembly: Parallelizable(ParallelScope.Fixtures)]