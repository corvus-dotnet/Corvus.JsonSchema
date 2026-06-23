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

/**
 * Unpack a workflow-package container — the inverse of {@link packWorkflowPackage} — into a map of entry name →
 * UTF-8 text (e.g. `workflow.json`, `sources/<name>.json`). Throws if the bytes are not a valid `AWP` container.
 * @param {ArrayBuffer|Uint8Array} container The package bytes.
 * @returns {Map<string, string>} Each entry's name → its decoded UTF-8 text.
 */
export function unpackWorkflowPackage(container) {
  const bytes = container instanceof Uint8Array ? container : new Uint8Array(container);
  if (bytes.length < 8 || bytes[0] !== 0x41 || bytes[1] !== 0x57 || bytes[2] !== 0x50) {
    throw new Error('Not a workflow-package container (bad AWP magic).');
  }
  if (bytes[3] !== PACKAGE_FORMAT_VERSION) {
    throw new Error(`Unsupported workflow-package format version ${bytes[3]}.`);
  }

  const dv = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  const dec = new TextDecoder();
  const entryCount = dv.getUint32(4, true);
  const entries = new Map();
  let pos = 8;
  for (let i = 0; i < entryCount; i++) {
    const nameLen = dv.getUint16(pos, true); pos += 2;
    const name = dec.decode(bytes.subarray(pos, pos + nameLen)); pos += nameLen;
    pos += 1; // per-entry encoding byte (0 = stored; the only encoding the packer writes)
    const dataLen = dv.getUint32(pos, true); pos += 4;
    entries.set(name, dec.decode(bytes.subarray(pos, pos + dataLen))); pos += dataLen;
  }
  return entries;
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
