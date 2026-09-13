// <copyright file="GeneratedCases.cs" company="Endjin Limited">
// Copyright (c) Endjin Limited. All rights reserved.
// </copyright>

namespace Corvus.Text.Json.RuntimeEvaluator.Benchmarks.SourceMeta;

/// <summary>The checked-in generated model type of each corpus (the shipping generator's output), for <see cref="GeneratedRun"/>.</summary>
public static class GeneratedCases
{
    public static readonly (string File, Action<string[], int, bool> Run)[] All =
    [
        ("ansible-meta", static (lines, loop, cold) => GeneratedRun.Run<Corvus.AnsibleMetaBenchmark.Current.AnsibleMetaSchema>("ansible-meta", lines, loop, cold)),
        ("aws-cdk", static (lines, loop, cold) => GeneratedRun.Run<Corvus.AwsCdkBenchmark.Current.AwsCdkSchema>("aws-cdk", lines, loop, cold)),
        ("babelrc", static (lines, loop, cold) => GeneratedRun.Run<Corvus.BabelrcBenchmark.Current.BabelrcSchema>("babelrc", lines, loop, cold)),
        ("clang-format", static (lines, loop, cold) => GeneratedRun.Run<Corvus.ClangFormatBenchmark.Current.ClangFormatSchema>("clang-format", lines, loop, cold)),
        ("cmake-presets", static (lines, loop, cold) => GeneratedRun.Run<Corvus.CmakePresetsBenchmark.Current.CmakePresetsSchema>("cmake-presets", lines, loop, cold)),
        ("code-climate", static (lines, loop, cold) => GeneratedRun.Run<Corvus.CodeClimateBenchmark.Current.CodeClimateSchema>("code-climate", lines, loop, cold)),
        ("cql2", static (lines, loop, cold) => GeneratedRun.Run<Corvus.Cql2Benchmark.Current.Cql2Schema>("cql2", lines, loop, cold)),
        ("cspell", static (lines, loop, cold) => GeneratedRun.Run<Corvus.CspellBenchmark.Current.CspellSchema>("cspell", lines, loop, cold)),
        ("cypress", static (lines, loop, cold) => GeneratedRun.Run<Corvus.CypressBenchmark.Current.CypressSchema>("cypress", lines, loop, cold)),
        ("deno", static (lines, loop, cold) => GeneratedRun.Run<Corvus.DenoBenchmark.Current.DenoSchema>("deno", lines, loop, cold)),
        ("dependabot", static (lines, loop, cold) => GeneratedRun.Run<Corvus.DependabotBenchmark.Current.DependabotSchema>("dependabot", lines, loop, cold)),
        ("draft-04", static (lines, loop, cold) => GeneratedRun.Run<Corvus.Draft04Benchmark.Current.Draft04Schema>("draft-04", lines, loop, cold)),
        ("fabric-mod", static (lines, loop, cold) => GeneratedRun.Run<Corvus.FabricModBenchmark.Current.FabricModSchema>("fabric-mod", lines, loop, cold)),
        ("geojson", static (lines, loop, cold) => GeneratedRun.Run<Corvus.GeoJsonBenchmark.Current.GeoJsonSchema>("geojson", lines, loop, cold)),
        ("gitpod-configuration", static (lines, loop, cold) => GeneratedRun.Run<Corvus.GitpodConfigurationBenchmark.Current.GitpodConfigurationSchema>("gitpod-configuration", lines, loop, cold)),
        ("helm-chart-lock", static (lines, loop, cold) => GeneratedRun.Run<Corvus.HelmChartLockBenchmark.Current.HelmChartLockSchema>("helm-chart-lock", lines, loop, cold)),
        ("importmap", static (lines, loop, cold) => GeneratedRun.Run<Corvus.ImportmapBenchmark.Current.ImportmapSchema>("importmap", lines, loop, cold)),
        ("jasmine", static (lines, loop, cold) => GeneratedRun.Run<Corvus.JasmineBenchmark.Current.JasmineSchema>("jasmine", lines, loop, cold)),
        ("jsconfig", static (lines, loop, cold) => GeneratedRun.Run<Corvus.JsconfigBenchmark.Current.JsconfigSchema>("jsconfig", lines, loop, cold)),
        ("jshintrc", static (lines, loop, cold) => GeneratedRun.Run<Corvus.JshintrcBenchmark.Current.JshintrcSchema>("jshintrc", lines, loop, cold)),
        ("krakend", static (lines, loop, cold) => GeneratedRun.Run<Corvus.KrakendBenchmark.Current.KrakendSchema>("krakend", lines, loop, cold)),
        ("lazygit", static (lines, loop, cold) => GeneratedRun.Run<Corvus.LazygitBenchmark.Current.LazygitSchema>("lazygit", lines, loop, cold)),
        ("lerna", static (lines, loop, cold) => GeneratedRun.Run<Corvus.LernaBenchmark.Current.LernaSchema>("lerna", lines, loop, cold)),
        ("nest-cli", static (lines, loop, cold) => GeneratedRun.Run<Corvus.NestCliBenchmark.Current.NestCliSchema>("nest-cli", lines, loop, cold)),
        ("omnisharp", static (lines, loop, cold) => GeneratedRun.Run<Corvus.OmnisharpBenchmark.Current.OmnisharpSchema>("omnisharp", lines, loop, cold)),
        ("openapi", static (lines, loop, cold) => GeneratedRun.Run<Corvus.OpenapiBenchmark.Current.OpenapiSchema>("openapi", lines, loop, cold)),
        ("pre-commit-hooks", static (lines, loop, cold) => GeneratedRun.Run<Corvus.PreCommitHooksBenchmark.Current.PreCommitHooksSchema>("pre-commit-hooks", lines, loop, cold)),
        ("pulumi", static (lines, loop, cold) => GeneratedRun.Run<Corvus.PulumiBenchmark.Current.PulumiSchema>("pulumi", lines, loop, cold)),
        ("semantic-release", static (lines, loop, cold) => GeneratedRun.Run<Corvus.SemanticReleaseBenchmark.Current.SemanticReleaseSchema>("semantic-release", lines, loop, cold)),
        ("stale", static (lines, loop, cold) => GeneratedRun.Run<Corvus.StaleBenchmark.Current.StaleSchema>("stale", lines, loop, cold)),
        ("stylecop", static (lines, loop, cold) => GeneratedRun.Run<Corvus.StylecopBenchmark.Current.StylecopSchema>("stylecop", lines, loop, cold)),
        ("tmuxinator", static (lines, loop, cold) => GeneratedRun.Run<Corvus.TmuxinatorBenchmark.Current.TmuxinatorSchema>("tmuxinator", lines, loop, cold)),
        ("ui5", static (lines, loop, cold) => GeneratedRun.Run<Corvus.Ui5Benchmark.Current.Ui5Schema>("ui5", lines, loop, cold)),
        ("ui5-manifest", static (lines, loop, cold) => GeneratedRun.Run<Corvus.Ui5ManifestBenchmark.Current.Ui5ManifestSchema>("ui5-manifest", lines, loop, cold)),
        ("unreal-engine-uproject", static (lines, loop, cold) => GeneratedRun.Run<Corvus.UnrealEngineUprojectBenchmark.Current.UnrealEngineUprojectSchema>("unreal-engine-uproject", lines, loop, cold)),
        ("vercel", static (lines, loop, cold) => GeneratedRun.Run<Corvus.VercelBenchmark.Current.VercelSchema>("vercel", lines, loop, cold)),
        ("yamllint", static (lines, loop, cold) => GeneratedRun.Run<Corvus.YamllintBenchmark.Current.YamllintSchema>("yamllint", lines, loop, cold)),
    ];
}
