// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

return args.Length > 0 && args[0] == "warm"
    ? Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta.BlazeBasis.Run(args[1..])
    : Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta.ColdRun.Run(args);
