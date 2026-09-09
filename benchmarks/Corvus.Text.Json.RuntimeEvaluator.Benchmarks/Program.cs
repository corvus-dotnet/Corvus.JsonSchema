using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Exporters.Json;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

// In-process: avoids BenchmarkDotNet rebuilding the 37 model projects for its host executable.
IConfig config = DefaultConfig.Instance
    .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance).WithId("inproc"))
    .AddExporter(MarkdownExporter.GitHub)
    .AddExporter(JsonExporter.Full)
    .WithOptions(ConfigOptions.DisableOptimizationsValidator);

if (args.Length > 0 && args[0] == "quick")
{
    int rounds = args.Length > 1 && int.TryParse(args[1], out int r) ? r : 30;
    Corvus.Text.Json.RuntimeEvaluator.Benchmarks.MicroBenchmarks.RunQuick(rounds);
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta.SourceMetaDiff.RunQuick(args.Length > 2 ? args[2..] : [], rounds);
}

if (args.Length > 0 && args[0] == "asm")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.AssemblyCheck.Run();
}

if (args.Length > 0 && args[0] == "dump")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.NodeDump.Run(args[1..]);
}

if (args.Length > 0 && args[0] == "profile")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.ProfileLoop.Run(args[1..]);
}

if (args.Length > 0 && args[0] == "alloctypes")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.AllocationTypes.Run(args[1..]);
}

if (args.Length > 0 && args[0] == "uniqueprobe")
{
    Corvus.Text.Json.RuntimeEvaluator.Benchmarks.UniqueProbe.Run();
    return 0;
}

if (args.Length > 0 && args[0] == "alloc")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.AllocationCheck.Run(args[1..]);
}

if (args.Length > 0 && args[0] == "diff")
{
    return Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta.SourceMetaDiff.Run(args[1..]) == 0 ? 0 : 1;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
return 0;
