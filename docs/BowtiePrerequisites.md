# Bowtie Prerequisites (Windows)

This guide walks through installing the prerequisites for running [Bowtie](https://github.com/bowtie-json-schema/bowtie) on Windows. Bowtie is a meta-validator that runs JSON Schema test suites against multiple implementations, including the Corvus.JsonSchema harness. Once you have these prerequisites in place, see the [Testing with Bowtie](LocalNuGetTesting.md#testing-with-bowtie) section of the local NuGet testing guide for the Corvus-specific workflow.

> **Linux / macOS:** If you are not on Windows, follow the [official Bowtie installation guide](https://docs.bowtie.report/en/latest/) instead. The Corvus-specific workflow in `LocalNuGetTesting.md` applies to all platforms once Bowtie is installed.

## Requirements summary

| Prerequisite | Minimum version | Purpose |
|---|---|---|
| Windows | 11 (or Server with WSL2) | Required for container runtime |
| Python | 3.13+ | Bowtie is a Python tool |
| Container runtime | Podman **or** Docker Desktop | Runs JSON Schema implementations in containers |
| uv | Latest | Python package/environment manager |
| Git | Any recent | Cloning the Bowtie repository |

## Step 1 — Install a container runtime

Bowtie runs each JSON Schema implementation inside a container. The `configure-bowtie-for-local-development.ps1` script looks for Podman first, falling back to Docker.

### Podman (recommended)

Podman is open-source with no commercial licensing restrictions. On Windows it runs via WSL2 or Hyper-V.

**System requirements:**

- Windows 11 (or Windows 10 build 19041+ with WSL2)
- Hardware virtualization enabled in BIOS/UEFI
- WSL2 installed — if not already present, run from an **admin** PowerShell and restart when prompted:

  ```powershell
  wsl --install
  ```

**Install Podman:**

```powershell
winget install RedHat.Podman
```

Alternatively, download the MSI from the [Podman releases page](https://github.com/containers/podman/releases). During installation, select **WSL** as the virtualization provider (the default on Windows Home; Hyper-V requires Pro/Enterprise/Education).

**Initialise the Podman machine:**

After installing, restart your terminal and run:

```powershell
podman machine init
podman machine start
```

Verify with:

```powershell
podman run --rm hello-world
```

> **Troubleshooting:** If you get "Cannot connect to Podman", make sure the machine is running (`podman machine start`). If issues persist, try rootful mode:
>
> ```powershell
> podman machine stop
> podman machine set --rootful
> podman machine start
> ```

**Set `DOCKER_HOST`:**

Bowtie uses the Docker API to manage containers. When using Podman, you need to set the `DOCKER_HOST` environment variable so Bowtie can find the Podman socket. Podman usually listens on the default `docker_engine` pipe on Windows, but setting `DOCKER_HOST` explicitly avoids "socket not set" errors.

Find your Podman pipe path and set the variable:

```powershell
# Find the pipe path
podman machine inspect --format '{{.ConnectionInfo.PodmanPipe.Path}}'
# Typically: \\.\pipe\podman-machine-default

# Set for the current session (replace back slashes with forward slashes)
$env:DOCKER_HOST = "npipe:////./pipe/podman-machine-default"
```

To set it permanently, add it as a user environment variable:

```powershell
[Environment]::SetEnvironmentVariable("DOCKER_HOST", "npipe:////./pipe/podman-machine-default", "User")
```

> If you are using Docker Desktop instead, `DOCKER_HOST` is set automatically — you do not need to configure it.

### Docker Desktop (alternative)

If you already have Docker Desktop or prefer it:

1. Download from <https://www.docker.com/products/docker-desktop/>
2. Install with the WSL2 backend (the default on Windows 11)
3. Start Docker Desktop
4. Verify: `docker run --rm hello-world`

> **Note:** Docker Desktop requires a paid licence for commercial use in larger organisations. Podman has no such restriction.

## Step 2 — Install uv

[uv](https://docs.astral.sh/uv/) is a fast Python package and project manager. Bowtie recommends it for installation.

**Option A — PowerShell one-liner (recommended):**

```powershell
powershell -ExecutionPolicy ByPass -c "irm https://astral.sh/uv/install.ps1 | iex"
```

Restart your terminal after installation.

**Option B — winget:**

```powershell
winget install astral-sh.uv
```

Verify with:

```powershell
uv --version
```

## Step 3 — Install Python 3.13+

Bowtie requires Python 3.13 or later (declared in its `pyproject.toml` as `requires-python = ">=3.13"`).

If you installed `uv` in the previous step, you can let it manage Python for you — it will automatically download 3.13 when you create a virtual environment in step 4. Otherwise, install Python explicitly:

**Option A — winget:**

```powershell
winget install Python.Python.3.13
```

**Option B — python.org installer:**

Download from <https://www.python.org/downloads/windows/>. Choose the 64-bit installer for Python 3.13.x and tick **"Add python.exe to PATH"** during installation.

Verify with:

```powershell
python --version
```

## Step 4 — Clone and set up Bowtie

The Corvus workflow requires a local clone of the Bowtie repository rather than a system-wide install, because the checkout may contain Windows-specific fixes that have not yet been released.

**Clone the repository:**

```powershell
git clone https://github.com/bowtie-json-schema/bowtie.git
cd bowtie
```

**Create a virtual environment and install Bowtie into it:**

Using uv (recommended):

```powershell
uv venv --python 3.13
.\.venv\Scripts\Activate.ps1
uv pip install -e .
```

Or using Python directly:

```powershell
python -m venv .venv
.\.venv\Scripts\Activate.ps1
.\.venv\Scripts\pip.exe install -e .
```

## Step 5 — Verify the installation

Run the Bowtie help command using the venv binary:

```powershell
$bowtie = ".\.venv\Scripts\bowtie.exe"  # from the bowtie clone directory
& $bowtie --help
```

Then run a quick smoke test against a public implementation to verify the full stack (Python → Bowtie → container runtime):

```powershell
& $bowtie smoke -i python-jsonschema
```

This downloads a container image on first run, so it may take a minute. If it succeeds, your setup is complete.

## Next steps

With all prerequisites installed, follow the [Testing with Bowtie](LocalNuGetTesting.md#testing-with-bowtie) guide to configure Bowtie to test against locally-built Corvus.JsonSchema packages.

The short version:

```powershell
# From the Corvus.JsonSchema repo root:
.\build-local-packages.ps1
.\configure-bowtie-for-local-development.ps1 -BowtiePath <path-to-bowtie-clone>

# From the bowtie clone:
$bowtie = ".\.venv\Scripts\bowtie.exe"
& $bowtie suite -i localhost/dotnet-corvus-jsonschema 2020-12 | & $bowtie summary --show failures
```
