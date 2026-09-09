using System.Diagnostics;
using System.Reflection;

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks;

public static class AssemblyCheck
{
    public static int Run()
    {
        foreach (Assembly a in new[]
        {
            typeof(Corvus.Text.Json.ParsedJsonDocument<>).Assembly,
            typeof(Corvus.Text.Json.RuntimeEvaluator.JsonSchemaEvaluator).Assembly,
            typeof(Corvus.Cql2Benchmark.Current.Cql2Schema).Assembly,
            typeof(Corvus.MicroModels.MicroObject).Assembly,
            typeof(AssemblyCheck).Assembly,
        })
        {
            var d = a.GetCustomAttribute<DebuggableAttribute>();
            var c = a.GetCustomAttribute<AssemblyConfigurationAttribute>();
            Console.WriteLine($"{a.GetName().Name,-50} config={c?.Configuration ?? "?",-8} jitOptimizerDisabled={d?.IsJITOptimizerDisabled ?? false} location={a.Location}");
        }

        return 0;
    }
}
