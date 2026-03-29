// <copyright file="EnableParallelTestExecution.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using NUnit.Framework;

// Note: Reqnroll doesn't support parallelization of multiple scenarios within a single feature,
// so this is the highest level of parallelization available to us. See:
// https://docs.reqnroll.net/latest/execution/parallel-execution.html#nunit-configuration
[assembly: Parallelizable(ParallelScope.Fixtures)]