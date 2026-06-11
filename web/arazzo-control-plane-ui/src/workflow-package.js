// Arazzo Control Plane — in-browser workflow-package builder (no DOM, no deps).
//
// Produces the same "workflow document package" archive the .NET `WorkflowPackage.Pack` writes — a plain ZIP
// (store method, no compression) bundling:
//   manifest.json        { "formatVersion": 1, "workflow": "workflow.json", "sources": [ { "name", "path" } ] }
//   workflow.json        the Arazzo workflow document
//   sources/<name>.json  each referenced source document
// readable by `System.IO.Compression.ZipArchive` server-side. The catalog re-hashes the logical content on add,
// so the archive need not be byte-identical to the .NET packer — only structurally compatible.
//
//   import { packWorkflowPackage } from './workflow-package.js';
//   const blob = packWorkflowPackage(workflowJsonText, [{ name: 'petstore', content: petstoreJsonText }]);
//   await client.addCatalogVersion({ package: blob, owner, tags });

/** The package format version written into the manifest (mirrors WorkflowPackage.FormatVersion). */
export const PACKAGE_FORMAT_VERSION = 1;

/**
 * Build a workflow-package archive from a workflow document and its referenced source documents.
 * @param {string|Uint8Array} workflow The Arazzo workflow document (UTF-8 JSON text or bytes).
 * @param {Array<{ name: string, content: (string|Uint8Array) }>} [sources] The source documents, keyed by their
 *   `sourceDescriptions` name (the name used in `sources/<name>.json`).
 * @returns {Blob} The package archive as an `application/zip` Blob.
 */
export function packWorkflowPackage(workflow, sources = []) {
  const enc = new TextEncoder();
  const toBytes = (v) => (typeof v === 'string' ? enc.encode(v) : v);

  const ordered = [...sources]
    .map((s) => ({ name: String(s.name), data: toBytes(s.content) }))
    .sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));

  const manifest = JSON.stringify({
    formatVersion: PACKAGE_FORMAT_VERSION,
    workflow: 'workflow.json',
    sources: ordered.map((s) => ({ name: s.name, path: `sources/${s.name}.json` })),
  });

  const entries = [
    { name: 'manifest.json', data: enc.encode(manifest) },
    { name: 'workflow.json', data: toBytes(workflow) },
    ...ordered.map((s) => ({ name: `sources/${s.name}.json`, data: s.data })),
  ];

  return new Blob([buildZip(entries)], { type: 'application/zip' });
}

/** Build a store-method (uncompressed) ZIP from `[{ name, data: Uint8Array }]`. */
function buildZip(entries) {
  const enc = new TextEncoder();
  const locals = [];
  const central = [];
  let offset = 0;

  for (const e of entries) {
    const nameBytes = enc.encode(e.name);
    const crc = crc32(e.data);
    const size = e.data.length;

    const local = new Uint8Array(30 + nameBytes.length);
    const lv = new DataView(local.buffer);
    lv.setUint32(0, 0x04034b50, true); // local file header signature
    lv.setUint16(4, 20, true);         // version needed to extract (2.0)
    lv.setUint16(6, 0, true);          // general purpose flags
    lv.setUint16(8, 0, true);          // compression method: 0 = store
    lv.setUint16(10, 0, true);         // last mod file time
    lv.setUint16(12, 0x0021, true);    // last mod file date: 1980-01-01
    lv.setUint32(14, crc, true);
    lv.setUint32(18, size, true);      // compressed size (= size, stored)
    lv.setUint32(22, size, true);      // uncompressed size
    lv.setUint16(26, nameBytes.length, true);
    lv.setUint16(28, 0, true);         // extra field length
    local.set(nameBytes, 30);
    locals.push(local, e.data);

    const cd = new Uint8Array(46 + nameBytes.length);
    const cv = new DataView(cd.buffer);
    cv.setUint32(0, 0x02014b50, true); // central directory header signature
    cv.setUint16(4, 20, true);         // version made by
    cv.setUint16(6, 20, true);         // version needed
    cv.setUint16(8, 0, true);
    cv.setUint16(10, 0, true);
    cv.setUint16(12, 0, true);
    cv.setUint16(14, 0x0021, true);
    cv.setUint32(16, crc, true);
    cv.setUint32(20, size, true);
    cv.setUint32(24, size, true);
    cv.setUint16(28, nameBytes.length, true);
    cv.setUint16(30, 0, true);         // extra length
    cv.setUint16(32, 0, true);         // comment length
    cv.setUint16(34, 0, true);         // disk number start
    cv.setUint16(36, 0, true);         // internal attributes
    cv.setUint32(38, 0, true);         // external attributes
    cv.setUint32(42, offset, true);    // local header offset
    cd.set(nameBytes, 46);
    central.push(cd);

    offset += local.length + e.data.length;
  }

  const cdSize = central.reduce((n, c) => n + c.length, 0);
  const cdOffset = offset;

  const eocd = new Uint8Array(22);
  const ev = new DataView(eocd.buffer);
  ev.setUint32(0, 0x06054b50, true);   // end of central directory signature
  ev.setUint16(4, 0, true);            // disk number
  ev.setUint16(6, 0, true);            // disk with central directory
  ev.setUint16(8, entries.length, true);
  ev.setUint16(10, entries.length, true);
  ev.setUint32(12, cdSize, true);
  ev.setUint32(16, cdOffset, true);
  ev.setUint16(20, 0, true);           // comment length

  return concat([...locals, ...central, eocd]);
}

let CRC_TABLE;

function crc32(bytes) {
  if (!CRC_TABLE) {
    CRC_TABLE = new Uint32Array(256);
    for (let n = 0; n < 256; n++) {
      let c = n;
      for (let k = 0; k < 8; k++) c = (c & 1) ? (0xedb88320 ^ (c >>> 1)) : (c >>> 1);
      CRC_TABLE[n] = c >>> 0;
    }
  }
  let crc = 0xffffffff;
  for (let i = 0; i < bytes.length; i++) crc = CRC_TABLE[(crc ^ bytes[i]) & 0xff] ^ (crc >>> 8);
  return (crc ^ 0xffffffff) >>> 0;
}

function concat(chunks) {
  const total = chunks.reduce((n, c) => n + c.length, 0);
  const out = new Uint8Array(total);
  let pos = 0;
  for (const c of chunks) { out.set(c, pos); pos += c.length; }
  return out;
}
