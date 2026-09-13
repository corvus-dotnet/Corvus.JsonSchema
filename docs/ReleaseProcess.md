# Release Process

This document describes how versioning, tagging, and NuGet publishing work in Corvus.JsonSchema.

## Overview

The release pipeline uses:
- **GitVersion** for semantic versioning
- **GitHub Actions** for CI/CD
- **NuGet.org** for stable releases (tag-triggered)
- **GitHub Packages** for pre-release packages (branch builds)
- **Automated release workflow** for tag creation on PR merge

## GitVersion configuration

`GitVersion.yml` at the repository root:

```yaml
mode: ContinuousDeployment
branches:
  master:
    regex: ^main
    tag: preview
    increment: patch
  dependabot-pr:
    regex: ^dependabot
    tag: dependabot
    source-branches:
    - develop
    - master
    - release
    - feature
    - support
    - hotfix
next-version: "5.0"
```

Key points:
- **ContinuousDeployment** mode — every commit gets a unique pre-release version
- Builds from `main` get a `-preview.N` suffix
- Builds from `dependabot/*` get a `-dependabot.N` suffix
- The `next-version: "5.0"` ensures the major version starts at 5.0

## Version examples

| Trigger | Version format |
|---------|---------------|
| Commit on `main` | `5.0.1-preview.42` |
| Tag `v5.0.1` on `main` | `5.0.1` (stable) |
| Dependabot PR build | `5.0.1-dependabot.7` |
| Feature branch build | `5.0.1-feature-name.3` |

## Publishing pipeline

### Build workflow (`build.yml`)

The build workflow runs on every push and PR. It has three phases:

1. **Compile** — builds the solution on Ubuntu and Windows
2. **Test** — runs the test suite with `.NET 10.0` and `.NET Framework 4.8.1`
3. **NuGet** — packages and publishes

### NuGet source selection

Publishing is conditional based on whether the build was triggered by a tag:

| Trigger | NuGet source | API key |
|---------|-------------|---------|
| Tag (`refs/tags/*`) | `https://api.nuget.org/v3/index.json` | `NUGET_APIKEY` secret |
| Branch / PR | `https://nuget.pkg.github.com/{owner}/index.json` | `BUILD_PUBLISHER_PAT` secret |

This means:
- **Stable releases** go to NuGet.org when a version tag is pushed
- **Pre-release packages** go to GitHub Packages for every branch build

## Automated release workflow (`auto_release.yml`)

When a PR is merged to `main`, the `auto_release.yml` workflow:

1. Checks for the `no_release` label — if present, skips the release
2. Waits for any pending Dependabot PRs to complete (batched releases)
3. Uses GitVersion to compute the next version
4. Creates a Git tag in the format `v{Major}.{Minor}.{Patch}`
5. The tag push triggers the build workflow, which publishes to NuGet.org
6. Removes any `pending_release` labels from included PRs

### Skipping a release

Add the `no_release` label to a PR before merging to prevent automatic tag creation. This is useful for documentation-only changes or internal refactoring.

### Batched Dependabot releases

When multiple Dependabot PRs are open, the workflow waits until all are merged before creating a single release tag. This avoids publishing many patch versions for dependency bumps.

## Manual release

To create a release manually:

```powershell
# Ensure you're on main with the latest changes
git checkout main
git pull

# Create and push a version tag
git tag v5.0.1
git push origin v5.0.1
```

The tag push triggers the build workflow, which publishes to NuGet.org.

## The native AOT profile

`profiles/Corvus.Text.Json.mibc` in the `Corvus.Text.Json` package is the static optimisation profile its
`buildTransitive/Corvus.Text.Json.targets` hands to the native AOT compiler (docs/RuntimeEvaluator.md, "Publishing
an application"). It is recorded from the code being packaged by the build's `GenerateAotProfile` task
(`.zf/config.ps1`), which runs in the compile phase on CI (`PostBuild`, Linux) before the package phase packs it:

1. finds a .NET 11 runtime for dotnet-pgo (the dotnet-eng feed publishes `dotnet-pgo` as a .NET 11 tool only,
   11.0.0-preview.6, pinned in the task): on CI the pipeline installs the 11 SDK (`additionalNetSdkVersion` in
   `.github/workflows/build.yml`, an exact RC version since setup-dotnet resolves `11.0.x` to released builds only);
   locally the task takes it from `dotnet`, then from `~/.dotnet`, and only otherwise installs one under
   `.zf/aot-profile`; downloads and unpacks the tool; installs `dotnet-trace`;
2. gathers the Sourcemeta corpora from the benchmark model projects, publishes the cold runner framework-dependent,
   and traces its instrumented warm run over every corpus (`DOTNET_TieredPGO=1`, a call-count threshold of 10,000 so
   methods stay instrumented, `ReadyToRun=0`, the runtime provider at keyword 0x1E000080018 level 5);
3. `create-mibc` into `src/Corvus.Text.Json/obj/profiles/Corvus.Text.Json.mibc`, which `Corvus.Text.Json.csproj`
   packs in preference to the checked-in file when it exists (`obj` travels between the pipeline's phases in the
   build cache), and fails the build if the result is small or names fewer than 1,000 of the library's methods.

The checked-in `src/Corvus.Text.Json/profiles/Corvus.Text.Json.mibc` is the fallback for local packs and is refreshed
when the evaluator changes: `BUILDVAR_GenerateAotProfile=true ./build.ps1 -Tasks GenerateAotProfile`, then copy the
`obj/profiles` file over it and commit. To check a packed library picks the profile up, publish the cold runner
against the package (docs/LocalNuGetTesting.md for the feed):
`dotnet publish benchmarks/Corvus.Text.Json.RuntimeEvaluator.ColdRunner -c Release -r linux-x64 -p:ColdAot=true -p:ColdPackage=<version> -p:RestoreConfigFile=<nuget.config>`;
the ILC response file under the runner's `obj/.../native/` carries one `--mibc:` argument pointing into the package,
and `<runner> warm 200 <corpus>` reads within about 10% of the JIT harness's figure (`tools/measure.sh warm`).

## Pre-release testing

Pre-release packages are published to GitHub Packages on every branch build. To test a pre-release package:

1. Add the GitHub Packages source to your `nuget.config`:

```xml
<packageSources>
  <add key="github" value="https://nuget.pkg.github.com/corvus-dotnet/index.json" />
</packageSources>
```

2. Reference the pre-release version in your project:

```xml
<PackageReference Include="Corvus.Text.Json" Version="5.0.1-preview.42" />
```

See `docs/LocalNuGetTesting.md` for testing with locally-built packages.