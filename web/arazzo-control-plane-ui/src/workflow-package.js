// Arazzo Control Plane — in-browser workflow-package builder (no DOM, no deps).
//
// Produces the same "workflow document package" the .NET `WorkflowPackage.Pack` writes — a small length-prefixed
// (TLV) binary container (NOT a ZIP) bundling:
//   workflow.json        the Arazzo workflow document
//   sources/<name>.json  each referenced source document
// readable by `WorkflowPackage.Open` server-side. The catalog re-hashes the logical content on add, so the
// container need not be byte-identical to the .NET packer — only structurally compatible.
//
// Container layout (all multi-byte integers little-endian):
//   header : "AWP" (3 bytes) + formatVersion (1 byte) + entryCount (uint32)
//   entry  : nameLen (uint16) + name (UTF-8) + encoding (1 byte; 0 = stored) + dataLen (uint32) + data
// entries are written sorted by name.
//
//   import { packWorkflowPackage } from './workflow-package.js';
//   const blob = packWorkflowPackage(workflowJsonText, [{ name: 'petstore', content: petstoreJsonText }]);
//   await client.addCatalogVersion({ package: blob, owner, tags });

/** The package format version (the 4th byte of the container magic; mirrors WorkflowPackage.FormatVersion). */
export const PACKAGE_FORMAT_VERSION = 1;

/** The stored (uncompressed) per-entry encoding (mirrors WorkflowPackage's stored encoding byte). */
const ENCODING_STORED = 0;

/**
 * Build a workflow-package from a workflow document and its referenced source documents.
 * @param {string|Uint8Array} workflow The Arazzo workflow document (UTF-8 JSON text or bytes).
 * @param {Array<{ name: string, content: (string|Uint8Array) }>} [sources] The source documents, keyed by their
 *   `sourceDescriptions` name (the name used in `sources/<name>.json`).
 * @returns {Blob} The package as an `application/octet-stream` Blob.
 */
export function packWorkflowPackage(workflow, sources = []) {
  const enc = new TextEncoder();
  const toBytes = (v) => (typeof v === 'string' ? enc.encode(v) : v);

  const entries = [
    { name: 'workflow.json', data: toBytes(workflow) },
    ...[...sources].map((s) => ({ name: `sources/${String(s.name)}.json`, data: toBytes(s.content) })),
  ].sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));

  return new Blob([buildPackage(entries)], { type: 'application/octet-stream' });
}

/** Build the length-prefixed container bytes from `[{ name, data: Uint8Array }]` (entries already sorted by name). */
function buildPackage(entries) {
  const enc = new TextEncoder();
  const nameBytes = entries.map((e) => enc.encode(e.name));

  let total = 8; // header: magic (4) + entry count (4)
  for (let i = 0; i < entries.length; i++) {
    total += 2 + nameBytes[i].length + 1 + 4 + entries[i].data.length;
  }

  const out = new Uint8Array(total);
  const dv = new DataView(out.buffer);
  out[0] = 0x41; // 'A'
  out[1] = 0x57; // 'W'
  out[2] = 0x50; // 'P'
  out[3] = PACKAGE_FORMAT_VERSION;
  dv.setUint32(4, entries.length, true);

  let pos = 8;
  for (let i = 0; i < entries.length; i++) {
    const nb = nameBytes[i];
    const data = entries[i].data;
    dv.setUint16(pos, nb.length, true); pos += 2;
    out.set(nb, pos); pos += nb.length;
    out[pos++] = ENCODING_STORED;
    dv.setUint32(pos, data.length, true); pos += 4;
    out.set(data, pos); pos += data.length;
  }

  return out;
}
