// <copyright file="Program.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using Corvus.Text.Json.Arazzo.Durability.Benchmarks;

ManualConfig config = ManualConfig.CreateMinimumViable()
    .AddJob(Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance))
    .AddDiagnoser(MemoryDiagnoser.Default);

// Discover every benchmark class in this assembly (so a new one can never silently not-run for want of manual
// registration) and honour command-line args — most importantly `--filter`, so any single benchmark can be run in
// isolation to read its ledger figure. With no args, run them all (`--filter *`) rather than dropping to the
// interactive menu, which would hang a non-interactive run.
string[] effectiveArgs = args.Length == 0 ? ["--filter", "*"] : args;
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(effectiveArgs, config);