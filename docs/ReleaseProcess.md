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

A PR, or a push to `main`, that changes only the Python, Rust, TypeScript, Ruby or PHP packages (`src-py`, `src-rs`, `src-ts`, `src-rb`, `src-php`) and their own workflows skips these phases: its first job, `Detect .NET changes`, finds nothing the .NET build reads (`.github/actions/dotnet-changes` holds the list). Any other change builds, and so does every release tag push and every manual run. Branch protection requires the `.NET build gate` job, which passes when the build succeeded or was not needed.

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
2. Leaves out a PR that changes nothing the .NET build reads (only `src-py`, `src-rs` or `src-ts`, as `build.yml` decides), and removes its `pending_release` label
3. Waits for any pending Dependabot PRs to complete (batched releases)
4. Uses GitVersion to compute the next version
5. Creates a Git tag in the format `{Major}.{Minor}.{Patch}`
6. The tag push triggers the build workflow, which publishes to NuGet.org
7. Removes any `pending_release` labels from included PRs

A release includes every merged PR still labelled `pending_release`, so closing a PR without .NET changes can still release earlier ones.

### Skipping a release

A PR that changes only the Python, Rust, TypeScript, Ruby or PHP packages makes no NuGet release; nothing needs adding.

Add the `no_release` label to a PR before merging to stop every release its merge would otherwise make: the NuGet release tag, and the crates.io, PyPI, npm, RubyGems and PHP publishes that a version change in `src-rs`, `src-py`, `src-ts`, `src-rb` or `src-php` triggers (`.github/actions/no-release-label` reads the label). This is useful for documentation-only changes, internal refactoring, or a version change to be released later. The `NO_RELEASE:` prefix some PR titles carry is for readers only: the label is what counts. A manual run of a publish workflow ignores the label.

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
2. gathers the Sourcemeta corpora from the benchmark model projects, publishes the cold runner framework-dependent
   with the identity (assembly, file and informational version) read back from the `Corvus.Text.Json.dll` the build
   produced, since the publish rebuilds the library into the output the package phase packs and a plain publish would
   give it the default 1.0.0.0 (as 5.6.0 shipped), failing if the identity changes; then traces the runner's
   instrumented warm run over every corpus (`DOTNET_TieredPGO=1`, a call-count threshold of 10,000 so
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

## The Rust crate

The Rust port of the runtime evaluator, `corvus-json-schema` in `src-rs/corvus-json-schema`, publishes to
[crates.io](https://crates.io/crates/corvus-json-schema). It is versioned independently of the NuGet packages, by the
`version` in its `Cargo.toml`. GitVersion and the tag-triggered NuGet pipeline play no part.

To release, bump `version` in `src-rs/corvus-json-schema/Cargo.toml` and merge to `main`.
`.github/workflows/crates-publish.yml` then does the following.

1. It does nothing if crates.io already has that version, or if the merged PR is labelled `no_release`.
2. It runs fmt, clippy and the tests, checks that the crate's `LICENSE` matches the repository's, and runs
   `cargo publish --dry-run`, which builds the crate from its packaged sources.
3. It publishes through crates.io trusted publishing. No token is stored. The crate's trusted publisher on crates.io
   names this repository and that workflow file, so don't rename it.
4. It tags the commit `rs-v<version>`.

Unlike the npm package, there is no staged approval. The publish step makes the version live at once. A published
version can be yanked (`cargo yank --version <version>`), but never replaced or deleted, so check a release on a
branch first: `rust.yml` runs the same packaging check on every pull request that touches `src-rs`.

The package holds the library sources, `README.md`, `Cargo.toml` and `LICENSE`, as listed by `include` in
`Cargo.toml`. The crate's `LICENSE` is a copy of the repository's, because a crate can only package files inside its
own directory. Update both together; CI fails if they differ.

Never push an `rs-v` tag by hand. `build.yml` publishes NuGet packages for the tags that trigger it. Its tag filter only
accepts release versions (`[0-9]+.[0-9]+.[0-9]+*`), and the workflow's own tag push uses `GITHUB_TOKEN`, which starts
no other workflow.

### The first release

crates.io only accepts a trusted publisher for a crate that already exists, so version 0.1.0 is published by hand,
from a clean checkout of `main`.

```bash
cd src-rs/corvus-json-schema
cargo login              # a token with the publish-new scope, from crates.io Account Settings, API Tokens
cargo publish --dry-run  # check the file list, and that the crate builds from its packaged sources
cargo publish
```

Then revoke the token, and on crates.io open the crate's Settings, Trusted Publishing, and add a GitHub publisher with
repository owner `corvus-dotnet`, repository `Corvus.JsonSchema` and workflow `crates-publish.yml`. Add the
maintainers as owners too (`cargo owner --add <github-user>`), so the crate does not depend on one account.

## The Python packages

`src-py` holds two Python packages, published to PyPI and versioned independently of the NuGet packages and of each
other, each by the `version` in its `pyproject.toml`:

- `corvus-json-schema`, pure Python (`src-py/corvus-json-schema`);
- `corvus-json-schema-rs`, backed by the Rust crate
  (`src-py/corvus-json-schema-rs`): an sdist and one abi3 wheel per platform (Linux x86_64 and aarch64, glibc and musl;
  macOS universal2; Windows x64 and arm64).

To release one, bump `version` in its `pyproject.toml` and `__version__` in its `__init__.py` (CI checks they agree),
add the version's entry at the top of its `VERSIONHISTORY.md`, and merge to `main`. `.github/workflows/pypi-publish.yml`
then does the following for each package.

1. It does nothing if PyPI already has that version, or if the merged PR is labelled `no_release`.
2. It builds the package: the sdist and wheel for the pure package, and for the Rust-backed one every wheel and the
   sdist through `python-wheels.yml`, which tests each wheel on its own platform, as CI does on every pull request.
3. It publishes through PyPI trusted publishing, in the `pypi` GitHub environment for `corvus-json-schema` and
   `pypi-rs` for `corvus-json-schema-rs`. No token is stored. Each project's trusted publisher on PyPI names this
   repository, that workflow file and its environment, so don't rename them.
4. It tags the commit `py-v<version>` (pure) or `py-rs-v<version>` (Rust-backed).

There is no staged approval: the upload makes the version live. A published version can be yanked on PyPI, but its
files can never be replaced, so check a release on a branch first.

The Rust-backed package builds the crate from `src-rs/corvus-json-schema` by path (its sdist includes the crate's
sources), so it can use crate changes before the crate is released. Release the crate too when the package depends on
new crate API, so the crate on crates.io matches what the wheels contain.

Never push a `py-v` or `py-rs-v` tag by hand (see the crate's tags above).

### The first release: PyPI setup

PyPI accepts a trusted publisher for a project that does not exist yet (a "pending" publisher), and the first
publish through it creates the project. A pending publisher does not reserve the name, so publish soon after adding it.
Do this once, before merging the first version:

1. **Create the GitHub environments.** In the repository's **Settings**, **Environments**, add environments named
   `pypi` and `pypi-rs`. They need no secrets. Required reviewers are optional; without them a merge publishes at
   once. Each project needs its own environment: PyPI refuses a second pending publisher with the same repository,
   workflow and environment, even for another project name.
2. **Add the pending publishers.** Sign in to PyPI (with two-factor authentication), open
   [**Publishing**](https://pypi.org/manage/account/publishing/) in your account, and add a GitHub publisher for each
   project:

   | Field | `corvus-json-schema` | `corvus-json-schema-rs` |
   |---|---|---|
   | PyPI Project Name | `corvus-json-schema` | `corvus-json-schema-rs` |
   | Owner | `corvus-dotnet` | `corvus-dotnet` |
   | Repository name | `Corvus.JsonSchema` | `Corvus.JsonSchema` |
   | Workflow name | `pypi-publish.yml` | `pypi-publish.yml` |
   | Environment name | `pypi` | `pypi-rs` |

3. **Merge.** The workflow publishes both projects, owned by the account that added the publishers.
4. **Add co-owners.** On each project's **Manage**, **Collaborators** page, invite the other maintainers as owners, so
   the projects do not depend on one account.

To keep the projects in a PyPI organization instead (a group of users and teams that owns projects together):

1. On PyPI, open [**Your organizations**](https://pypi.org/manage/organizations/), enter the name and details under
   **Create new organization**, choose **Community** (free; **Company** is billed per member) and click **Create**.
   A PyPI administrator approves new organizations, with no fixed timeline; the creator becomes its Owner.
2. Once it is approved, add the maintainers on its **People** page (owners or managers), and optionally a team on
   **Teams**.
3. Move each project in on the organization's **Projects** page: select it under **Transfer existing project** (an
   Owner of the organization who also owns the project does this). Its trusted publisher moves with it.

Publishing does not wait for the organization: the projects can be published from an account first and transferred
when the organization is approved.

## The Ruby gem

`src-rb/corvus-json-schema` is the `corvus_json_schema` gem, a native extension over the Rust crate, published to
RubyGems and versioned independently of the NuGet packages and of the crate, by `VERSION` in
`lib/corvus_json_schema/version.rb`. A release is a source gem (which carries the crate's sources and builds the
extension where it is installed) and one native gem per platform (Linux x86_64 and aarch64, glibc and musl; macOS
x86_64 and arm64; Windows x64), each holding the extension for Ruby 3.3, 3.4 and 4.0.

To release it, bump `VERSION`, add the version's entry at the top of its `VERSIONHISTORY.md` (the workflow checks it is
there), and merge to `main`. `.github/workflows/rubygems-publish.yml` then does the following.

1. It does nothing if RubyGems already has that version's source gem, or if the merged PR is labelled `no_release`.
2. It builds every gem through `rubygems-build.yml`, which cross-compiles the native gems with rb-sys-dock and installs
   and tests each on its own platform, as CI does on every pull request.
3. It pushes the native gems and then the source gem through RubyGems trusted publishing, in the `rubygems` GitHub
   environment. No API key is stored. The gem's trusted publisher names this repository, that workflow file and the
   environment, so don't rename them. A run that fails part of the way can be run again: it skips native gems RubyGems
   already has.
4. It tags the commit `rb-v<version>`.

A pushed version can be yanked, but never replaced. Like the Rust-backed Python package, the gem builds the crate from
`src-rs/corvus-json-schema` by path, so release the crate too when the gem depends on new crate API.

Never push an `rb-v` tag by hand (see the crate's tags above).

### The first release: RubyGems setup

RubyGems accepts a trusted publisher for a gem that does not exist yet (a "pending" publisher), and the first push
through it creates the gem. Do this once, before merging the first version:

1. **Create the GitHub environment.** In the repository's **Settings**, **Environments**, add an environment named
   `rubygems`. It needs no secrets.
2. **Add the pending publisher.** Sign in to rubygems.org (with multi-factor authentication), open
   [**Pending trusted publishers**](https://rubygems.org/profile/oidc/pending_trusted_publishers) in your settings,
   click **Create**, and enter:

   | Field | Value |
   |---|---|
   | Gem name | `corvus_json_schema` |
   | Trusted publisher type | GitHub Actions |
   | Repository owner | `corvus-dotnet` |
   | Repository name | `Corvus.JsonSchema` |
   | Workflow filename | `rubygems-publish.yml` |
   | Environment | `rubygems` |

3. **Merge.** The workflow publishes the gem, owned by the account that added the publisher.
4. **Add owners.** On the gem's **Ownership** page, add the other maintainers, so the gem does not depend on one account.

## The PHP extension

`src-php/corvus-json-schema` is the `corvus_json_schema` PHP extension, installed with PIE as the Packagist package
`corvus-dotnet/corvus-json-schema`, and versioned independently of the NuGet packages and of the crate, by the `version`
in its `Cargo.toml`. A release is one prebuilt extension per PHP minor version (8.2 to 8.5), thread-safety mode and
platform (Linux x86_64 and arm64, glibc and musl; macOS arm64; Windows x64): 48 archives, named as PIE looks
for them.

Packagist reads a package from the root of a repository, and its versions from that repository's tags, so this
repository cannot be the package's (its tags are the NuGet versions). The package's repository is
[corvus-dotnet/corvus-json-schema-php](https://github.com/corvus-dotnet/corvus-json-schema-php), which holds only what
the publish workflow pushes there: changes are made here.

To release it, bump `version` in its `Cargo.toml`, add the version's entry at the top of its `VERSIONHISTORY.md` (the
workflow checks it is there), and merge to `main`. `.github/workflows/php-publish.yml` then does the following.

1. It does nothing if the package's repository already has a release for that version, or if the merged PR is
   labelled `no_release`. It fails if the crate version the extension needs is not on crates.io: release the crate
   first.
2. It builds every archive through `php-build.yml`, which tests each build on its own platform (CI runs a subset of
   them on every pull request).
3. It replaces the package repository's files with `src-php/corvus-json-schema`, with the crate taken from crates.io
   instead of by path, commits, and tags the commit `<version>` (no prefix: Packagist reads the tag as the version).
4. It creates the release for that tag, with the version's history entry as its notes, and attaches the archives.
   Packagist picks up the tag, and `pie install` finds the archives on the release.
5. It tags this repository's commit `php-v<version>`.

Writing to the package's repository takes a token, the `PHP_REPOSITORY_TOKEN` secret of the `php` environment. A run
that fails part of the way can be run again: it skips the push when the tag is there, and replaces the archives on an
existing release.

Never push a `php-v` tag here, or a version tag to the package's repository, by hand.

### The first release: Packagist setup

Do this once, before merging the first version:

1. **Create the package's repository.** Create `corvus-dotnet/corvus-json-schema-php`, public, with a README (so that
   it has a default branch, `main`). The publish workflow replaces its files.
2. **Create the token.** In GitHub's **Settings**, **Developer settings**, **Personal access tokens**, **Fine-grained
   tokens**, generate a token with the `corvus-dotnet` organization as its resource owner, access to that repository
   only, and the repository permission **Contents: Read and write**. If the organization requires approval for
   fine-grained tokens, an owner approves it.
3. **Create the GitHub environment.** In this repository's **Settings**, **Environments**, add an environment named
   `php`, and add the token to it as the secret `PHP_REPOSITORY_TOKEN`.
4. **Merge.** The workflow pushes the sources, tags them and creates the release.
5. **Submit the package to Packagist.** Sign in to [Packagist](https://packagist.org) (with GitHub), click
   **Submit**, and enter `https://github.com/corvus-dotnet/corvus-json-schema-php`. Packagist reads the tag. With
   Packagist's GitHub integration (connected under your Packagist profile's **Settings**) it updates by itself when a
   tag is pushed; otherwise click **Update** on the package's page after each release.
6. **Add maintainers.** On the package's Packagist page, add the other maintainers, so the package does not depend on
   one account.

The token expires: renew it, and update the secret, before it does.

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