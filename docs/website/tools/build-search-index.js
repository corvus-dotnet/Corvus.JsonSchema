#!/usr/bin/env node

/**
 * build-search-index.js
 *
 * Reads all taxonomy YAML pages and their referenced markdown content,
 * merges with the API search-index.json produced by XmlDocToMarkdown,
 * and writes a single search-index.json for Lunr-based client-side search.
 *
 * Usage: node tools/build-search-index.js [--output <path>]
 *   --output  Path to write the merged search-index.json (default: .output/search-index.json)
 */

const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml');

const ROOT = path.resolve(__dirname, '..');

// ---------------------------------------------------------------------------
// CLI arguments
// ---------------------------------------------------------------------------
let outputPath = path.join(ROOT, '.output', 'search-index.json');

for (let i = 2; i < process.argv.length - 1; i++) {
  if (process.argv[i] === '--output') {
    outputPath = path.resolve(process.argv[++i]);
  }
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/** Recursively find files matching a predicate. */
function walk(dir, predicate, results = []) {
  if (!fs.existsSync(dir)) return results;
  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full, predicate, results);
    } else if (predicate(entry.name, full)) {
      results.push(full);
    }
  }
  return results;
}

/** Strip YAML front-matter (--- ... ---) from markdown and return body text. */
function stripFrontmatter(md) {
  const match = md.match(/^---\r?\n[\s\S]*?\r?\n---\r?\n?/);
  return match ? md.slice(match[0].length) : md;
}

/** Crude markdown-to-plain-text: strip headings, links, images, fences, etc. */
function stripMarkdown(md) {
  return md
    .replace(/^```[\s\S]*?^```/gm, '')       // fenced code blocks
    .replace(/`[^`]+`/g, '')                  // inline code
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')     // images
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')  // links → text
    .replace(/^#{1,6}\s+/gm, '')              // heading markers
    .replace(/[*_~]{1,3}/g, '')               // bold/italic/strikethrough
    .replace(/^\s*[-*+]\s+/gm, '')            // unordered list bullets
    .replace(/^\s*\d+\.\s+/gm, '')            // ordered list numbers
    .replace(/^\s*>\s+/gm, '')                // blockquotes
    .replace(/\|/g, ' ')                      // table pipes
    .replace(/---+/g, '')                     // horizontal rules
    .replace(/\n{2,}/g, '\n')                 // collapse blank lines
    .trim();
}

// ---------------------------------------------------------------------------
// Parse taxonomy YAML files into search entries
// ---------------------------------------------------------------------------

function parseTaxonomyEntry(filePath) {
  const raw = fs.readFileSync(filePath, 'utf8');
  let doc;
  try {
    doc = yaml.load(raw);
  } catch {
    return null;
  }
  if (!doc || !doc.Navigation || !doc.Navigation.Url) return null;

  // Skip 404 and homepage (homepage has no content body worth indexing separately)
  const url = doc.Navigation.Url;
  if (url === '/404.html') return null;

  const title = doc.Title || doc.Navigation.Title || '';
  const description = (doc.MetaData && doc.MetaData.Description) || '';
  const keywords = Array.isArray(doc.MetaData && doc.MetaData.Keywords)
    ? doc.MetaData.Keywords.join(' ')
    : '';

  // Resolve content blocks to markdown body text
  const blocks = doc.ContentBlocks || [];
  const bodyParts = [];

  for (const block of blocks) {
    const spec = block && block.Spec;
    if (!spec || !spec.Path) continue;
    const mdPath = path.resolve(path.dirname(filePath), spec.Path);
    if (fs.existsSync(mdPath)) {
      const md = fs.readFileSync(mdPath, 'utf8');
      bodyParts.push(stripMarkdown(stripFrontmatter(md)));
    }
  }

  return {
    url,
    title,
    description,
    keywords,
    body: bodyParts.join('\n').substring(0, 5000), // cap body size per entry
  };
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

function main() {
  const entries = [];

  // 1. Content pages from taxonomy (skip API per-type/member pages — they get
  //    richer entries from the dedicated API search index in step 2)
  const taxonomyDir = path.join(ROOT, 'site', 'taxonomy');
  const ymlFiles = walk(taxonomyDir, (name) => name.endsWith('.yml'));

  console.log(`Found ${ymlFiles.length} taxonomy file(s).`);

  // Collect API taxonomy URLs to deduplicate against API search index
  const apiTaxonomyDir = path.join(taxonomyDir, 'api');

  for (const ymlFile of ymlFiles) {
    // Skip per-type and per-member API taxonomy files — the API search index
    // provides richer entries with XML doc summaries and keyword metadata.
    // Only keep the namespace overview and index pages from the API section.
    if (ymlFile.startsWith(apiTaxonomyDir)) {
      const baseName = path.basename(ymlFile, '.yml');
      // Keep index and namespace overviews (no dots or double-hyphens in slug after prefix)
      // Skip per-type (e.g., corvus-text-json-jsonelement) and per-member (e.g., corvus-text-json-jsonelement.clone)
      const parts = baseName.split('-');
      if (baseName !== 'index' && parts.length > 2 && baseName.indexOf('.') < 0) {
        continue; // per-type page — skip, covered by API search index
      }
      if (baseName.indexOf('.') >= 0) {
        continue; // per-member page — skip, covered by API search index
      }
    }

    const entry = parseTaxonomyEntry(ymlFile);
    if (entry) {
      entries.push(entry);
    }
  }
  console.log(`  → ${entries.length} content search entries.`);

  // 2. API search index produced by XmlDocToMarkdown
  //    API entries use PascalCase keys (Url, Title, …); normalise to lowercase
  //    to match content entries and the Lunr field names used by search.js.
  const apiIndexPath = path.join(ROOT, 'site', 'content', 'Api', 'search-index.json');
  if (fs.existsSync(apiIndexPath)) {
    try {
      const apiEntries = JSON.parse(fs.readFileSync(apiIndexPath, 'utf8'));
      if (Array.isArray(apiEntries)) {
        for (const e of apiEntries) {
          entries.push({
            url:         e.Url         || e.url         || '',
            title:       e.Title       || e.title       || '',
            description: e.Description || e.description || '',
            keywords:    e.Keywords    || e.keywords    || '',
            body:        (e.Body       || e.body        || '').substring(0, 5000),
          });
        }
        console.log(`  → ${apiEntries.length} API search entries merged.`);
      }
    } catch (e) {
      console.warn(`Warning: could not parse ${apiIndexPath}:`, e.message);
    }
  } else {
    console.log('  (no API search-index.json found — skipping API entries)');
  }

  // 3. Write merged index
  const outDir = path.dirname(outputPath);
  if (!fs.existsSync(outDir)) {
    fs.mkdirSync(outDir, { recursive: true });
  }

  fs.writeFileSync(outputPath, JSON.stringify(entries, null, 2), 'utf8');
  console.log(`Wrote ${entries.length} total entries to ${outputPath}`);
}

main();
