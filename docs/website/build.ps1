<#
.SYNOPSIS
    Builds the Corvus.Text.Json documentation website.
.DESCRIPTION
    End-to-end build pipeline:
      1a. Build Corvus.Text.Json V5 (generates XML doc + assembly)
      1b. Build V4 libraries (generates XML docs + assemblies)
      2a. Generate V5 API markdown, taxonomy & views
      2b. Generate V4 API markdown, taxonomy & views
      3. Generate recipe content from ExampleRecipes source docs
      4. Generate docs content from source documentation
      5. Install Vellum SSG (if not present)
      6. Run Vellum to render the core site
      7. Compile SCSS to CSS
      8. Build Lunr search index
      9. Build and publish Playground (Blazor WASM)
     10. Rewrite root-relative paths for subpath hosting (when -BasePathPrefix is set)
.PARAMETER Preview
    Launches a local preview server after building.
.PARAMETER ServeOnly
    Skips the build and just starts the preview server on existing output.
    Requires a previous build to have been completed.
.PARAMETER Watch
    Monitors for file changes and auto-regenerates.
.PARAMETER SkipDotNetBuild
    Skips steps 1a (V5 build) and 1b (V4 builds). Use when the .NET solution
    has already been built (e.g. by the root build.ps1) and the compiled
    binaries are already in the bin/Release directories.
#>
[CmdletBinding()]
param (
    [Parameter()]
    [switch] $Preview,

    [Parameter()]
    [switch] $ServeOnly,

    [Parameter()]
    [switch] $Watch,

    [Parameter()]
    [string] $BasePathPrefix = "",

    [Parameter()]
    [switch] $SkipDotNetBuild
)

$ErrorActionPreference = 'Stop'
$InformationPreference = 'Continue'

$here = Split-Path -Parent $PSCommandPath
$repoRoot = Resolve-Path (Join-Path $here "..\..")
$siteDir = Join-Path $here "site"
$outputDir = Join-Path $here ".output"

# Paths used by multiple steps
$toolProject = Join-Path $here "tools\XmlDocToMarkdown"
$sharedViewsDir = Join-Path $siteDir "theme\corvus\views\Shared"

# Derive canonical repo URL from git remote (normalise SSH → HTTPS, strip .git)
$canonicalRepoUrl = $null
try {
    $remoteUrl = (git -C $repoRoot remote get-url origin 2>$null)
    if ($remoteUrl) {
        $remoteUrl = $remoteUrl.Trim()
        if ($remoteUrl -match '^git@github\.com:(.+)$') {
            $remoteUrl = "https://github.com/$($Matches[1])"
        }
        $remoteUrl = $remoteUrl -replace '\.git$', ''
        $canonicalRepoUrl = $remoteUrl
    }
} catch { }
if (-not $canonicalRepoUrl) {
    $canonicalRepoUrl = "https://github.com/corvus-dotnet/Corvus.JsonSchema"
    Write-Warning "Could not detect repo URL from git remote — using default: $canonicalRepoUrl"
}

# V5 paths
$v5XmlPath = Join-Path $repoRoot "src\Corvus.Text.Json\bin\Release\net10.0\Corvus.Text.Json.xml"
$v5AssemblyPath = Join-Path $repoRoot "src\Corvus.Text.Json\bin\Release\net10.0\Corvus.Text.Json.dll"
$v5Ns20AssemblyPath = Join-Path $repoRoot "src\Corvus.Text.Json\bin\Release\netstandard2.0\Corvus.Text.Json.dll"
$v5ApiContentDir = Join-Path $siteDir "content\Api-v5"
$v5ApiTaxonomyDir = Join-Path $siteDir "taxonomy\api-v5"
$v5ApiViewsDir = Join-Path $siteDir "theme\corvus\views\api\v5"
$v5NsDescriptionsDir = Join-Path $siteDir "content\Api-v5\namespaces"
$v5TypeExamplesDir = Join-Path $siteDir "content\Api-v5\examples"

# V4 paths
$v4SrcDir = Join-Path $repoRoot "src-v4"
$v4ApiContentDir = Join-Path $siteDir "content\Api-v4"
$v4ApiTaxonomyDir = Join-Path $siteDir "taxonomy\api-v4"
$v4ApiViewsDir = Join-Path $siteDir "theme\corvus\views\api\v4"

# V4 consumer-facing libraries to document
# NOTE: The 7 JsonSchema dialect libraries (Draft4/6/7/201909/202012/OpenApi30/31) are excluded
# because they contain thousands of generated types with repetitive patterns, adding ~25K pages
# that make the build impractically slow. The core API libraries below cover the primary user-facing surface.
$v4Projects = @(
    "Corvus.Json.ExtendedTypes",
    "Corvus.Json.JsonReference",
    "Corvus.Json.Patch",
    "Corvus.Json.Validator",
    "Corvus.Json.CodeGeneration",
    "Corvus.Json.CodeGeneration.CSharp",
    "Corvus.Json.CodeGeneration.CSharp.QuickStart",
    "Corvus.Json.CodeGeneration.HttpClientDocumentResolver"
)

# -- Helper: PascalCase to kebab-case ----------------------------------------
function ConvertTo-KebabCase([string]$text) {
    $result = $text -creplace '([a-z0-9])([A-Z])', '$1-$2'
    $result = $result -creplace '([A-Z]+)([A-Z][a-z])', '$1-$2'
    return $result.ToLower()
}

# -- Helper: timed step reporting --------------------------------------------
function Write-StepDuration($stepName, $sw) {
    $elapsed = $sw.Elapsed
    if ($elapsed.TotalMinutes -ge 1) {
        Write-Host "  $stepName completed in $([math]::Floor($elapsed.TotalMinutes))m $($elapsed.Seconds)s." -ForegroundColor Green
    } else {
        Write-Host "  $stepName completed in $([math]::Round($elapsed.TotalSeconds, 1))s." -ForegroundColor Green
    }
}

# -- ServeOnly: skip build, just start preview server -----------------------
if ($ServeOnly) {
    if (!(Test-Path $outputDir)) {
        throw "No output directory found at $outputDir. Run a full build first."
    }

    Write-Host "Starting static file server on existing output..." -ForegroundColor Cyan
    Write-Host "  Serving: $outputDir" -ForegroundColor DarkGray
    Write-Host "  URL:     http://localhost:5000" -ForegroundColor Green
    Write-Host "  Press Ctrl+C to stop." -ForegroundColor DarkGray

    $serverScript = @"
const http = require('http');
const fs = require('fs');
const path = require('path');
const mimeTypes = {
  '.html': 'text/html', '.css': 'text/css', '.js': 'application/javascript',
  '.json': 'application/json', '.png': 'image/png', '.svg': 'image/svg+xml',
  '.ico': 'image/x-icon', '.woff2': 'font/woff2', '.woff': 'font/woff',
  '.jpg': 'image/jpeg', '.gif': 'image/gif', '.xml': 'application/xml',
};
const root = process.argv[2];
http.createServer((req, res) => {
  let url = req.url.split('?')[0];
  if (url.endsWith('/')) url += 'index.html';
  const fp = path.join(root, url);
  fs.readFile(fp, (err, data) => {
    if (err) { res.writeHead(404); res.end('Not found'); return; }
    const ext = path.extname(fp);
    res.writeHead(200, { 'Content-Type': mimeTypes[ext] || 'application/octet-stream' });
    res.end(data);
  });
}).listen(5000, () => console.log('Serving at http://localhost:5000'));
"@
    & node -e $serverScript $outputDir
    return
}

# -- Step 0: Copy hand-authored source files ----------------------------------
Write-Host "`n[0/10] Copying hand-authored source files..." -ForegroundColor Cyan
$sourceDir = Join-Path $siteDir "source"
# Ensure destination directories exist (they are generated, not in source control)
foreach ($dir in @("content\Docs", "content\Examples")) {
    $dst = Join-Path $siteDir $dir
    if (!(Test-Path $dst)) { New-Item -ItemType Directory -Path $dst -Force | Out-Null }
}
# Copy content overviews
Copy-Item (Join-Path $sourceDir "content\Docs\Overview.md") (Join-Path $siteDir "content\Docs\Overview.md") -Force
Copy-Item (Join-Path $sourceDir "content\Examples\Overview.md") (Join-Path $siteDir "content\Examples\Overview.md") -Force
# Copy taxonomy index files
foreach ($sub in @("docs", "examples", "api", "api-v5", "api-v4")) {
    $src = Join-Path $sourceDir "taxonomy\$sub\index.yml"
    $dst = Join-Path $siteDir "taxonomy\$sub\index.yml"
    if (Test-Path $src) {
        $dstDir = Split-Path $dst -Parent
        if (!(Test-Path $dstDir)) { New-Item -ItemType Directory -Path $dstDir -Force | Out-Null }
        Copy-Item $src $dst -Force
    }
}
Write-Host "  Copied source files to site tree." -ForegroundColor Green

# -- Step 1a/1b: Build .NET projects (skip when -SkipDotNetBuild) ---------------
if ($SkipDotNetBuild) {
    Write-Host "`n[1a/10] Skipping V5 build (-SkipDotNetBuild)." -ForegroundColor DarkGray
    Write-Host "[1b/10] Skipping V4 builds (-SkipDotNetBuild)." -ForegroundColor DarkGray
} else {
    # -- Step 1a: Build Corvus.Text.Json (V5) ------------------------------------
    Write-Host "`n[1a/10] Building Corvus.Text.Json (V5)..." -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $mainProject = Join-Path $repoRoot "src\Corvus.Text.Json\Corvus.Text.Json.csproj"
    & dotnet build $mainProject -c Release -f net10.0 /p:GenerateDocumentationFile=true --no-incremental -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build Corvus.Text.Json (net10.0)" }
    & dotnet build $mainProject -c Release -f netstandard2.0 --no-incremental -v q
    if ($LASTEXITCODE -ne 0) { throw "Failed to build Corvus.Text.Json (netstandard2.0)" }
    Write-StepDuration "V5 build" $sw

    # -- Step 1b: Build V4 libraries ---------------------------------------------
    Write-Host "`n[1b/10] Building V4 libraries..." -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()
    $projIndex = 0
    foreach ($proj in $v4Projects) {
        $projIndex++
        $projPath = Join-Path $v4SrcDir "$proj\$proj.csproj"
        if (!(Test-Path $projPath)) {
            Write-Warning "  V4 project not found: $projPath - skipping"
            continue
        }
        Write-Host "  [$projIndex/$($v4Projects.Count)] Building $proj..." -ForegroundColor Gray
        & dotnet build $projPath -c Release -f net10.0 --no-incremental -v q
        if ($LASTEXITCODE -ne 0) { throw "Failed to build $proj (net10.0)" }
        & dotnet build $projPath -c Release -f netstandard2.0 --no-incremental -v q
        if ($LASTEXITCODE -ne 0) { throw "Failed to build $proj (netstandard2.0)" }
    }
    Write-StepDuration "V4 builds - $($v4Projects.Count) libraries" $sw
}

# -- Step 2a: Generate V5 API markdown, taxonomy & views ---------------------
Write-Host "`n[2a/10] Generating V5 API markdown, taxonomy & views..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
& dotnet run --project $toolProject -c Release -- `
    --xml $v5XmlPath `
    --assembly $v5AssemblyPath `
    --ns20-assembly $v5Ns20AssemblyPath `
    --output $v5ApiContentDir `
    --taxonomy-output $v5ApiTaxonomyDir `
    --api-views-dir $v5ApiViewsDir `
    --shared-views-dir $sharedViewsDir `
    --ns-descriptions $v5NsDescriptionsDir `
    --type-examples $v5TypeExamplesDir `
    --api-base-url /api/v5 `
    --repo-url $canonicalRepoUrl `
    --sidebar-partial-name _ApiSidebarV5 `
    --layout-path "../../Shared/_Layout.cshtml" `
    --version-label "V5 Engine" `
    --alt-version-label "V4 Engine" `
    --alt-version-url "/api/v4/index.html"
if ($LASTEXITCODE -ne 0) { throw "V5 API generation failed" }
Write-StepDuration "V5 API generation" $sw

# -- Step 2b: Generate V4 API markdown, taxonomy & views ---------------------
Write-Host "`n[2b/10] Generating V4 API markdown, taxonomy & views..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Build the --xml / --assembly argument pairs for all V4 libraries
$v4ToolArgs = @()
foreach ($proj in $v4Projects) {
    $binDir = Join-Path $v4SrcDir "$proj\bin\Release\net10.0"
    $xmlFile = Join-Path $binDir "$proj.xml"
    $dllFile = Join-Path $binDir "$proj.dll"
    $ns20Dll = Join-Path $v4SrcDir "$proj\bin\Release\netstandard2.0\$proj.dll"
    if ((Test-Path $xmlFile) -and (Test-Path $dllFile)) {
        $v4ToolArgs += "--xml", $xmlFile, "--assembly", $dllFile
        if (Test-Path $ns20Dll) {
            $v4ToolArgs += "--ns20-assembly", $ns20Dll
        }
    } else {
        Write-Warning "  Missing XML or DLL for $proj - skipping"
    }
}

& dotnet run --project $toolProject -c Release -- @v4ToolArgs `
    --output $v4ApiContentDir `
    --taxonomy-output $v4ApiTaxonomyDir `
    --api-views-dir $v4ApiViewsDir `
    --shared-views-dir $sharedViewsDir `
    --api-base-url /api/v4 `
    --repo-url $canonicalRepoUrl `
    --sidebar-partial-name _ApiSidebarV4 `
    --layout-path "../../Shared/_Layout.cshtml" `
    --version-label "V4 Engine" `
    --alt-version-label "V5 Engine" `
    --alt-version-url "/api/v5/index.html"
if ($LASTEXITCODE -ne 0) { throw "V4 API generation failed" }
Write-StepDuration "V4 API generation" $sw

# -- Step 3: Generate recipe content from ExampleRecipes ---------------------
Write-Host "`n[3/10] Generating recipe content from ExampleRecipes..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

$recipesSourceDir = Join-Path $repoRoot "docs\ExampleRecipes"
$recipesContentDir = Join-Path $siteDir "content\Examples"
$recipesTaxonomyDir = Join-Path $siteDir "taxonomy\examples"

# Clean old generated recipe files (hand-authored files are in source/ and copied in step 0)
Get-ChildItem $recipesContentDir -Filter "*.md" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "Overview.md" } | Remove-Item -Force
Get-ChildItem $recipesTaxonomyDir -Filter "*.yml" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "index.yml" } | Remove-Item -Force

$recipeDirs = Get-ChildItem $recipesSourceDir -Directory | Sort-Object Name
$recipeCount = 0

foreach ($dir in $recipeDirs) {
    if ($dir.Name -notmatch '^(\d+)-(.+)$') { continue }
    $number = $Matches[1]
    $pascalName = $Matches[2]
    $slug = ConvertTo-KebabCase $pascalName

    $readmePath = Join-Path $dir.FullName "README.md"
    if (!(Test-Path $readmePath)) { continue }

    $raw = Get-Content $readmePath -Raw -Encoding utf8

    # Extract title from "# JSON Schema Patterns in .NET - <title>"
    if ($raw -match '^# JSON Schema Patterns in \.NET\s*[---]\s*(.+?)[\r\n]') {
        $title = $Matches[1].Trim()
    } else {
        $title = ($pascalName -creplace '([a-z])([A-Z])', '$1 $2')
    }

    # Strip the # heading line and any leading blank lines after it
    $body = ($raw -replace '^#[^\n]+\n\s*', '').TrimStart()

    # Extract first sentence as description
    if ($body -match '^(.+?\.)\s') {
        $description = $Matches[1] -replace '"', '\"'
    } else {
        $description = $title
    }

    # Extract FAQ Q&A pairs from the FAQ section
    $faqQuestions = @()
    $faqPairs = @()
    if ($raw -match '(?s)## Frequently Asked Questions(.+?)(?=\n## |\z)') {
        $faqSection = $Matches[1]
        # Split on ### headings to get Q&A pairs
        $parts = $faqSection -split '(?=### )'
        foreach ($part in $parts) {
            $part = $part.Trim()
            if ($part -match '^###\s+(.+?)[\r\n]+(.+)') {
                $question = $Matches[1].Trim()
                $answer = ($Matches[2].Trim() -replace '[\r\n]+', ' ').Trim()
                $faqQuestions += ($question -replace '`', '')
                # Escape for JSON
                $qJson = $question -replace '\\', '\\' -replace '"', '\"'
                $aJson = $answer -replace '\\', '\\' -replace '"', '\"' -replace '`', ''
                $faqPairs += @{ q = $qJson; a = $aJson }
            }
        }
    }

    # Build Keywords array: title words + JSON Schema keywords + Corvus API terms
    # Start with the recipe title split into words (skip short ones)
    $titleWords = $title -split '\s+' | Where-Object { $_.Length -ge 3 } |
        ForEach-Object { $_.ToLower() -replace '[^a-z0-9]', '' } | Where-Object { $_ -and $_ -notin @('and', 'the', 'for', 'with', 'from') }

    # Extract JSON Schema keywords used in the content
    $schemaKeywords = [regex]::Matches($raw, '"(type|properties|required|additionalProperties|unevaluatedProperties|format|enum|const|oneOf|anyOf|allOf|if|then|else|prefixItems|items|unevaluatedItems|patternProperties|\$ref|\$defs|minimum|maximum|minLength|maxLength|pattern|not|contains|minItems|maxItems|uniqueItems)"') |
        ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique

    # Extract Corvus API method/type names from backtick spans
    $apiMethods = [regex]::Matches($raw, '`(IsUndefined|IsNull|IsValid|TryGetValue|ValueEquals|EvaluateSchema|GetRawText|ToString|Parse|From|Match|SetProperty|RemoveProperty|TryGetProperty|AddProperty|CreateBuilder|BuildDocument|Build|Clone|AsArray|AsObject|RootElement|TryGetNumericValues|CreateTuple|ConstInstance)\b') |
        ForEach-Object { $_.Groups[1].Value } | Select-Object -Unique

    $keywordItems = @('recipe', 'JSON Schema', 'C#') + $titleWords + $schemaKeywords + $apiMethods |
        Select-Object -Unique
    $keywordsYaml = ($keywordItems | ForEach-Object { "`"$($_ -replace '"', '\"')`"" }) -join ', '

    # Build FAQPage JSON-LD structured data
    $faqJsonLd = ''
    if ($faqPairs.Count -gt 0) {
        $mainEntity = ($faqPairs | ForEach-Object {
            "    {`n      `"@type`": `"Question`",`n      `"name`": `"$($_.q)`",`n      `"acceptedAnswer`": {`n        `"@type`": `"Answer`",`n        `"text`": `"$($_.a)`"`n      }`n    }"
        }) -join ",`n"
        $faqJsonLd = @"

<script type="application/ld+json">
{
  "@context": "https://schema.org",
  "@type": "FAQPage",
  "mainEntity": [
$mainEntity
  ]
}
</script>
"@
    }

    # 1) Content markdown with Vellum frontmatter + FAQ JSON-LD
    $contentPath = Join-Path $recipesContentDir "$pascalName.md"
    $frontmatter = "---`nContentType: `"application/vnd.endjin.ssg.content+md`"`nPublicationStatus: Published`nDate: 2026-03-15T00:00:00.0+00:00`nTitle: `"$title`"`n---`n"
    $contentMd = $frontmatter + $body + $faqJsonLd
    [System.IO.File]::WriteAllText($contentPath, $contentMd, [System.Text.Encoding]::UTF8)

    # 2) Taxonomy YAML (with Template property for shared view)
    $rank = $recipeCount + 1
    $taxonomyYml = @"
ContentType: application/vnd.endjin.ssg.page+yaml
Title: "$title"
Template: examples/recipe-detail
Navigation:
  Title: $title
  Description: "$description"
  Parent: /examples/index.html
  Url: /examples/$slug.html
  Rank: $rank
  Header:
    Visible: False
    Link: False
  Footer:
    Visible: False
    Link: False
MetaData:
  Title: "$title - Corvus.Text.Json Examples"
  Description: "$description"
  Keywords: [$keywordsYaml]
OpenGraph:
  Title: "$title - Corvus.Text.Json Examples"
  Description: "$description"
  Image:
ContentBlocks:
  - ContentType: application/vnd.endjin.ssg.content+md
    Id: $pascalName
    Spec:
      Path: ../../content/Examples/$pascalName.md
"@
    $taxonomyPath = Join-Path $recipesTaxonomyDir "$slug.yml"
    [System.IO.File]::WriteAllText($taxonomyPath, $taxonomyYml, [System.Text.Encoding]::UTF8)

    $recipeCount++
    Write-Host "  $number $title -> $slug" -ForegroundColor Gray
}
Write-StepDuration "Recipe generation ($recipeCount recipes)" $sw

# -- Step 4: Generate docs content from source documentation -----------------
Write-Host "`n[4/10] Generating docs content from source documentation..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

$docsSourceDir = Join-Path $repoRoot "docs"
$docsContentDir = Join-Path $siteDir "content\Docs"
$docsTaxonomyDir = Join-Path $siteDir "taxonomy\docs"
$descriptorsDir = Join-Path $here "doc-descriptors"

# Clean old generated doc files (hand-authored files are in source/ and copied in step 0)
Get-ChildItem $docsContentDir -Filter "*.md" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "Overview.md" } | Remove-Item -Force
Get-ChildItem $docsTaxonomyDir -Filter "*.yml" -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -ne "index.yml" } | Remove-Item -Force

# Build a map of source filenames -> slugs for cross-doc link rewriting
$docLinkMap = @{}
$descriptorFiles = Get-ChildItem $descriptorsDir -Filter "*.yml" | Sort-Object Name
foreach ($df in $descriptorFiles) {
    $dc = Get-Content $df.FullName -Raw -Encoding utf8
    foreach ($ln in ($dc -split "`n")) {
        $ln = $ln.Trim()
        if ($ln -match '^source:\s*"?(.+?)"?\s*$') {
            $src = $Matches[1]
            $bn = [System.IO.Path]::GetFileNameWithoutExtension($src)
            $docLinkMap[$src] = (ConvertTo-KebabCase $bn) + ".html"
        }
    }
}

$docCount = 0

foreach ($descriptorFile in $descriptorFiles) {
    # Parse simple YAML descriptor (source, navTitle, description)
    $descriptorContent = Get-Content $descriptorFile.FullName -Raw -Encoding utf8
    $descriptor = @{}
    foreach ($line in ($descriptorContent -split "`n")) {
        $line = $line.Trim()
        if ($line -match '^(\w+):\s*"?(.+?)"?\s*$') {
            $descriptor[$Matches[1]] = $Matches[2]
        }
    }

    $docFile = $descriptor['source']
    if (!$docFile) {
        Write-Warning "  Descriptor $($descriptorFile.Name) missing 'source' - skipping"
        continue
    }

    $sourcePath = Join-Path $docsSourceDir $docFile
    if (!(Test-Path $sourcePath)) {
        Write-Warning "  Source doc not found: $docFile - skipping"
        continue
    }

    $raw = Get-Content $sourcePath -Raw -Encoding utf8
    $baseName = [System.IO.Path]::GetFileNameWithoutExtension($docFile)
    $slug = ConvertTo-KebabCase $baseName

    # Extract title from # heading
    if ($raw -match '^# (.+?)[\r\n]') {
        $docTitle = $Matches[1].Trim() -replace '`', ''
    } else {
        $docTitle = ($baseName -creplace '([a-z])([A-Z])', '$1 $2')
    }

    # Strip the # heading line
    $docBody = ($raw -replace '^#[^\n]+\n\s*', '').TrimStart()

    # Strip markdown "## Table of Contents" section (the TOC through the next --- or ## heading)
    $docBody = $docBody -replace '(?ms)^## Table of Contents\s*\n(- \[.*?\]\(#.*?\)\s*\n)+\s*---\s*\n?', ''

    # Rewrite cross-doc markdown links: (Source.md) -> (slug.html)
    foreach ($srcFile in $docLinkMap.Keys) {
        $docBody = $docBody -replace [regex]::Escape("($srcFile)"), "($($docLinkMap[$srcFile]))"
    }

    # Use descriptor nav title, or fall back to doc title
    $navTitle = if ($descriptor['navTitle']) { $descriptor['navTitle'] } else {
        $t = $docTitle
        if ($t.Length -gt 30) { $t = $t.Substring(0, 27) + '...' }
        $t
    }

    # Use descriptor description, or extract first sentence
    if ($descriptor['description']) {
        $docDescription = $descriptor['description']
    } elseif ($docBody -match '^(.+?\.)\s') {
        $docDescription = $Matches[1] -replace '"', '\"'
    } else {
        $docDescription = $docTitle
    }

    # 1) Content markdown
    $contentPath = Join-Path $docsContentDir "$baseName.md"
    $frontmatter = "---`nContentType: `"application/vnd.endjin.ssg.content+md`"`nPublicationStatus: Published`nDate: 2026-03-15T00:00:00.0+00:00`nTitle: `"$($docTitle -replace '"', '\"')`"`n---`n"
    [System.IO.File]::WriteAllText($contentPath, ($frontmatter + $docBody), [System.Text.Encoding]::UTF8)

    # 2) Taxonomy YAML (with Template property for shared view)
    $docRank = $docCount + 1
    $docTaxonomyYml = @"
ContentType: application/vnd.endjin.ssg.page+yaml
Title: "$($docTitle -replace '"', '\"')"
Template: docs/doc-page
Navigation:
  Title: $navTitle
  Description: "$docDescription"
  Parent: /docs/index.html
  Url: /docs/$slug.html
  Rank: $docRank
  Header:
    Visible: False
    Link: False
  Footer:
    Visible: False
    Link: False
MetaData:
  Title: "$($docTitle -replace '"', '\"') - Corvus.Text.Json"
  Description: "$docDescription"
  Keywords: [documentation, Corvus.Text.Json]
OpenGraph:
  Title: "$($docTitle -replace '"', '\"') - Corvus.Text.Json"
  Description: "$docDescription"
  Image:
ContentBlocks:
  - ContentType: application/vnd.endjin.ssg.content+md
    Id: $baseName
    Spec:
      Path: ../../content/Docs/$baseName.md
"@
    $docTaxonomyPath = Join-Path $docsTaxonomyDir "$slug.yml"
    [System.IO.File]::WriteAllText($docTaxonomyPath, $docTaxonomyYml, [System.Text.Encoding]::UTF8)

    $docCount++
    Write-Host "  $docTitle -> $slug" -ForegroundColor Gray
}
Write-StepDuration "Docs generation ($docCount pages)" $sw

# -- Step 5: Install Vellum --------------------------------------------------
$vellumVersion = "2.0.9"
$vellumDir = Join-Path $here ".endjin"
$vellumCmd = Join-Path $vellumDir "vellum"

if (!(Test-Path $vellumCmd) -and !(Test-Path "$vellumCmd.exe")) {
    Write-Host "`n[5/10] Installing Vellum $vellumVersion..." -ForegroundColor Cyan
    if (!(Test-Path $vellumDir)) {
        New-Item -ItemType Directory -Path $vellumDir | Out-Null
    }
    & gh release download -R endjin/Endjin.StaticSiteGen $vellumVersion -p "vellum.$vellumVersion.nupkg" -D $vellumDir --clobber
    & dotnet tool install vellum --version $vellumVersion --tool-path $vellumDir --add-source $vellumDir
    if ($LASTEXITCODE -ne 0) { throw "Failed to install Vellum" }
    Write-Host "  Vellum installed." -ForegroundColor Green
} else {
    Write-Host "`n[5/10] Vellum already installed." -ForegroundColor DarkGray
}

# -- Step 6: Run Vellum ------------------------------------------------------
Write-Host "`n[6/10] Running Vellum..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()

# Prepare output directory (clean contents but keep dir if locked by another process)
if (Test-Path $outputDir) {
    try { Remove-Item $outputDir -Recurse -Force }
    catch { Get-ChildItem $outputDir -Force | Remove-Item -Recurse -Force -ErrorAction SilentlyContinue }
}
$assetsSource = Join-Path $siteDir "theme\corvus\assets"
$assetsDest = Join-Path $outputDir "assets"
Copy-Item -Path $assetsSource -Destination $assetsDest -Recurse -Force

# Run Vellum from the site/ directory so it only sees site source files.
# This eliminates the need to clean up spurious copies of build.ps1, tools/, etc.
Push-Location $siteDir
try {
    $vellumArgs = @("content", "generate", "-t", (Join-Path $siteDir "site.yml"), "-o", $outputDir)
    if ($Watch) { $vellumArgs += "--watch" }
    & $vellumCmd $vellumArgs
    if ($LASTEXITCODE -ne 0) { throw "Vellum generation failed" }
} finally {
    Pop-Location
}

# Vellum copies the site/ source files into output - remove the lightweight copies
foreach ($dir in @("taxonomy", "content", "theme")) {
    $spurious = Join-Path $outputDir $dir
    if (Test-Path $spurious) { Remove-Item $spurious -Recurse -Force }
}
foreach ($file in @("site.yml")) {
    $spurious = Join-Path $outputDir $file
    if (Test-Path $spurious) { Remove-Item $spurious -Force }
}
Write-StepDuration "Vellum site generation" $sw

# -- Step 7: Compile SCSS ----------------------------------------------------
Write-Host "`n[7/10] Compiling SCSS..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$scssPath = Join-Path $assetsSource "css\scss\main.scss"
$cssOutputPath = Join-Path $outputDir "main.css"
& npx sass $scssPath $cssOutputPath --style=compressed --no-source-map
if ($LASTEXITCODE -ne 0) { throw "SCSS compilation failed" }
Write-StepDuration "SCSS compilation" $sw

# -- Step 7b: Copy per-version API search indices ----------------------------
Write-Host "`n[7b/10] Copying API search indices..." -ForegroundColor Cyan
$v5SearchSrc = Join-Path $v5ApiContentDir "search-index.json"
$v4SearchSrc = Join-Path $v4ApiContentDir "search-index.json"
$v5SearchDst = Join-Path $outputDir "api\v5\search-index.json"
$v4SearchDst = Join-Path $outputDir "api\v4\search-index.json"
if (Test-Path $v5SearchSrc) {
    Copy-Item $v5SearchSrc $v5SearchDst -Force
    Write-Host "  V5: $([math]::Round((Get-Item $v5SearchDst).Length/1024,1)) KB"
} else { Write-Warning "  V5 search index not found at $v5SearchSrc" }
if (Test-Path $v4SearchSrc) {
    Copy-Item $v4SearchSrc $v4SearchDst -Force
    Write-Host "  V4: $([math]::Round((Get-Item $v4SearchDst).Length/1024,1)) KB"
} else { Write-Warning "  V4 search index not found at $v4SearchSrc" }

# -- Step 8: Build search index ----------------------------------------------
Write-Host "`n[8/10] Building search index..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$searchIndexOutput = Join-Path $outputDir "search-index.json"
& node (Join-Path $here "tools\build-search-index.js") --output $searchIndexOutput
if ($LASTEXITCODE -ne 0) {
    Write-Warning "Search index generation failed - site will build without search."
} else {
    Write-StepDuration "Search index" $sw
}

# -- Step 9: Build Playground (Blazor WASM) ----------------------------------
Write-Host "`n[9/10] Building Playground..." -ForegroundColor Cyan
$sw = [System.Diagnostics.Stopwatch]::StartNew()
$playgroundProject = Join-Path $repoRoot "docs\playground\src\Corvus.Text.Json.Playground\Corvus.Text.Json.Playground.csproj"
$playgroundPublishDir = Join-Path $here ".playground-publish"
$playgroundOutputDir = Join-Path $outputDir "playground"

# Publish the Blazor WASM app
& dotnet publish $playgroundProject -c Release -o $playgroundPublishDir --nologo
if ($LASTEXITCODE -ne 0) { throw "Playground publish failed" }

# Copy the wwwroot output (the deployable Blazor app) to .output/playground/
$playgroundWwwroot = Join-Path $playgroundPublishDir "wwwroot"
if (!(Test-Path $playgroundWwwroot)) {
    throw "Playground wwwroot not found at $playgroundWwwroot"
}
Copy-Item -Path $playgroundWwwroot -Destination $playgroundOutputDir -Recurse -Force

# Rewrite <base href="/"> for subpath hosting
$playgroundIndex = Join-Path $playgroundOutputDir "index.html"
if (Test-Path $playgroundIndex) {
    $indexContent = [System.IO.File]::ReadAllText($playgroundIndex)
    $indexContent = $indexContent -replace '<base href="/" />', "<base href=`"$BasePathPrefix/playground/`" />"
    [System.IO.File]::WriteAllText($playgroundIndex, $indexContent)
    Write-Host "  Updated base href to $BasePathPrefix/playground/" -ForegroundColor Gray
}

# Clean up the intermediate publish directory
Remove-Item $playgroundPublishDir -Recurse -Force -ErrorAction SilentlyContinue

$playgroundSize = (Get-ChildItem $playgroundOutputDir -Recurse -File | Measure-Object -Property Length -Sum).Sum
Write-Host "  Playground: $([math]::Round($playgroundSize/1MB, 1)) MB" -ForegroundColor Gray
Write-StepDuration "Playground build" $sw

# -- Step 10: Rewrite root-relative paths for subpath hosting -----------------
if ($BasePathPrefix) {
    Write-Host "`n[10/10] Rewriting paths for base prefix '$BasePathPrefix'..." -ForegroundColor Cyan
    $sw = [System.Diagnostics.Stopwatch]::StartNew()

    # Rewrite HTML files (excluding playground which uses <base href>)
    $htmlFiles = Get-ChildItem $outputDir -Filter "*.html" -Recurse -File |
        Where-Object { $_.FullName -notlike "*\playground\*" }
    $rewriteCount = 0
    foreach ($htmlFile in $htmlFiles) {
        $content = [System.IO.File]::ReadAllText($htmlFile.FullName)
        $original = $content

        # Rewrite href="/..." and src="/..." and content="/..." and data-search-index="/..." (but not protocol-relative "//" URLs)
        $content = $content -replace '(href|src|content|data-search-index)="(/(?!/))', "`$1=`"$BasePathPrefix`$2"

        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($htmlFile.FullName, $content)
            $rewriteCount++
        }
    }
    Write-Host "  Rewrote paths in $rewriteCount of $($htmlFiles.Count) HTML files." -ForegroundColor Gray

    # Rewrite JS files that contain root-relative fetch URLs
    $jsFiles = Get-ChildItem (Join-Path $outputDir "assets\js") -Filter "*.js" -File -ErrorAction SilentlyContinue
    $jsRewriteCount = 0
    foreach ($jsFile in $jsFiles) {
        $content = [System.IO.File]::ReadAllText($jsFile.FullName)
        $original = $content

        # Rewrite string literals containing root-relative paths: '/api/', '/search-index.json', etc.
        $content = $content -replace "(['""])(/(?!/)(?:api|search|docs|examples|getting-started|assets|playground))", "`$1$BasePathPrefix`$2"

        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($jsFile.FullName, $content)
            $jsRewriteCount++
        }
    }
    if ($jsRewriteCount -gt 0) {
        Write-Host "  Rewrote paths in $jsRewriteCount JS files." -ForegroundColor Gray
    }

    # Rewrite search-index.json files — URLs inside must also carry the base prefix
    $searchJsonFiles = Get-ChildItem $outputDir -Filter "search-index.json" -Recurse -File -ErrorAction SilentlyContinue
    $jsonRewriteCount = 0
    foreach ($jsonFile in $searchJsonFiles) {
        $content = [System.IO.File]::ReadAllText($jsonFile.FullName)
        $original = $content

        # Rewrite "Url": "/api/..." and "Url": "/docs/..." etc.
        $content = $content -replace '("Url"\s*:\s*")(/(?!/)(?:api|docs|examples|getting-started))', "`$1$BasePathPrefix`$2"

        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($jsonFile.FullName, $content)
            $jsonRewriteCount++
        }
    }
    if ($jsonRewriteCount -gt 0) {
        Write-Host "  Rewrote URLs in $jsonRewriteCount search-index.json files." -ForegroundColor Gray
    }

    Write-StepDuration "Path rewriting" $sw
} else {
    Write-Host "`n[10/10] No base path prefix - skipping path rewriting." -ForegroundColor DarkGray
}

# -- Step 10b: Replace default GitHub URL with the one derived from git --------
# The Razor template has a hardcoded default; if the git-derived URL differs
# (e.g. building from a fork), rewrite all HTML references.
$defaultGitHubUrl = "https://github.com/corvus-dotnet/Corvus.JsonSchema"
if ($canonicalRepoUrl -ne $defaultGitHubUrl) {
    Write-Host "`n  Rewriting GitHub URLs: $defaultGitHubUrl -> $canonicalRepoUrl" -ForegroundColor Cyan
    $htmlFiles = Get-ChildItem $outputDir -Filter "*.html" -Recurse -File |
        Where-Object { $_.FullName -notlike "*\playground\*" }
    $ghRewriteCount = 0
    foreach ($htmlFile in $htmlFiles) {
        $content = [System.IO.File]::ReadAllText($htmlFile.FullName)
        $original = $content
        $content = $content.Replace($defaultGitHubUrl, $canonicalRepoUrl)
        if ($content -ne $original) {
            [System.IO.File]::WriteAllText($htmlFile.FullName, $content)
            $ghRewriteCount++
        }
    }
    if ($ghRewriteCount -gt 0) {
        Write-Host "  Rewrote GitHub URLs in $ghRewriteCount HTML files." -ForegroundColor Gray
    }
}

# Write robots.txt — allow indexing for production, block for preview/local
if ($env:GITHUB_ACTIONS -and -not $Preview) {
    # Production CI build — allow search engines
    $robotsTxt = @"
User-agent: *
Allow: /
"@
    [System.IO.File]::WriteAllText((Join-Path $outputDir "robots.txt"), $robotsTxt)
    Write-Host "  Created robots.txt (allow indexing)." -ForegroundColor Gray
} else {
    # Local dev or preview — block indexing
    $robotsTxt = @"
User-agent: *
Disallow: /
"@
    [System.IO.File]::WriteAllText((Join-Path $outputDir "robots.txt"), $robotsTxt)
    Write-Host "  Created robots.txt (noindex — local/preview build)." -ForegroundColor Gray
}

Write-Host "`nBuild complete! Output: $outputDir" -ForegroundColor Green

if ($Preview) {
    Write-Host "`nStarting preview server..." -ForegroundColor Cyan
    $previewArgs = @("content", "generate", "-t", (Join-Path $siteDir "site.yml"), "-o", $outputDir, "--preview")
    & $vellumCmd $previewArgs
}
