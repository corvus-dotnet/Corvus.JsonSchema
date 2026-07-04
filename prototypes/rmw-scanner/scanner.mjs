// Dual-offset UTF-8 JSON scanner — Model C prototype (§13 of the design doc).
//
// Scans a top-level JSON object in a Uint8Array, recording each member's
// name/value spans in BOTH byte offsets and UTF-16 code-unit offsets, in one
// pass, into a PRE-ALLOCATED Int32Array (no per-scan allocation).
//
// Layout per member slot (8 int32): see F below.
// Structural advances are ASCII (+1 byte / +1 u16); only string *content* bytes
// can be multibyte, where the per-lead-byte lockstep rule applies.

export const SLOT = 8;
export const F = Object.freeze({
  NAME_BS: 0, NAME_BE: 1, VAL_BS: 2, VAL_BE: 3, // byte spans
  NAME_US: 4, NAME_UE: 5, VAL_US: 6, VAL_UE: 7, // utf-16 code-unit spans
});

const QUOTE = 0x22, BACKSLASH = 0x5c, COLON = 0x3a, COMMA = 0x2c;
const LBRACE = 0x7b, RBRACE = 0x7d, LBRACK = 0x5b, RBRACK = 0x5d, U = 0x75;

// Module-level cursor — reused across all calls so the scan allocates nothing.
let cb = 0; // byte offset
let cu = 0; // utf-16 code-unit offset

function isWs(b) { return b === 0x20 || b === 0x09 || b === 0x0a || b === 0x0d; }

function skipWs(buf, len) {
  while (cb < len && isWs(buf[cb])) { cb++; cu++; }
}

// assumes buf[cb] === '"'. Consumes the whole string token, escapes included,
// WITHOUT decoding to a JS string. Advances cb/cu in lockstep.
function scanString(buf, len) {
  cb++; cu++; // opening quote
  while (cb < len) {
    const b = buf[cb];
    if (b === QUOTE) { cb++; cu++; return; }
    if (b === BACKSLASH) {
      cb++; cu++;            // the backslash (ASCII)
      const e = buf[cb];
      cb++; cu++;            // the escape letter (ASCII)
      if (e === U) { cb += 4; cu += 4; } // \uXXXX — 4 ASCII hex digits (source width)
      continue;
    }
    // ordinary content byte — the lockstep rule (the one subtle bit):
    if (b < 0x80) { cb++; cu++; }          // ASCII
    else if (b < 0xe0) { cb += 2; cu++; }  // 2-byte → 1 u16
    else if (b < 0xf0) { cb += 3; cu++; }  // 3-byte → 1 u16
    else { cb += 4; cu += 2; }             // 4-byte → surrogate pair (2 u16)
  }
  throw new Error('unterminated string');
}

// Skip a value: string / object / array / number / true / false / null.
function skipValue(buf, len) {
  const b = buf[cb];
  if (b === QUOTE) { scanString(buf, len); return; }
  if (b === LBRACE || b === LBRACK) { skipContainer(buf, len); return; }
  // scalar (number/true/false/null) — all ASCII; advance to a delimiter.
  while (cb < len) {
    const c = buf[cb];
    if (c === COMMA || c === RBRACE || c === RBRACK || isWs(c)) break;
    cb++; cu++;
  }
}

// Skip a {...} or [...] container, depth-counted, string-aware so braces inside
// strings don't miscount. Multibyte only ever appears inside strings (handled).
function skipContainer(buf, len) {
  let depth = 0;
  for (;;) {
    if (cb >= len) throw new Error('unterminated container');
    const b = buf[cb];
    if (b === QUOTE) { scanString(buf, len); continue; }
    if (b === LBRACE || b === LBRACK) { depth++; cb++; cu++; continue; }
    if (b === RBRACE || b === RBRACK) { depth--; cb++; cu++; if (depth === 0) return; continue; }
    cb++; cu++; // structure/number/ws outside strings — ASCII
  }
}

// Scan a top-level object into `table` (a pre-allocated Int32Array of at least
// maxMembers*8). Returns the member count. Throws on malformed input.
export function scanInto(buf, table) {
  const len = buf.length;
  cb = 0; cu = 0;
  if (len >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) { cb = 3; cu = 1; } // BOM
  skipWs(buf, len);
  if (buf[cb] !== LBRACE) throw new Error('expected {');
  cb++; cu++;
  let slot = 0;
  for (;;) {
    skipWs(buf, len);
    if (buf[cb] === RBRACE) { cb++; cu++; break; }
    const base = slot * SLOT;
    table[base + F.NAME_BS] = cb; table[base + F.NAME_US] = cu;
    scanString(buf, len);
    table[base + F.NAME_BE] = cb; table[base + F.NAME_UE] = cu;
    skipWs(buf, len);
    if (buf[cb] !== COLON) throw new Error('expected :');
    cb++; cu++;
    skipWs(buf, len);
    table[base + F.VAL_BS] = cb; table[base + F.VAL_US] = cu;
    skipValue(buf, len);
    table[base + F.VAL_BE] = cb; table[base + F.VAL_UE] = cu;
    slot++;
    skipWs(buf, len);
    const s = buf[cb];
    if (s === COMMA) { cb++; cu++; continue; }
    if (s === RBRACE) { cb++; cu++; break; }
    throw new Error('expected , or }');
  }
  return slot;
}

// ---- UTF-16 string scanner (the jsonc-parser analog; char offsets only) ----
// Used by the string-splice baseline so it isolates byte-vs-string fairly
// (same algorithm, operating on a JS string with charCodeAt).
let sp = 0;
function sIsWs(c) { return c === 0x20 || c === 0x09 || c === 0x0a || c === 0x0d; }
function sSkipWs(s, len) { while (sp < len && sIsWs(s.charCodeAt(sp))) sp++; }
function sScanString(s, len) {
  sp++; // open quote
  while (sp < len) {
    const c = s.charCodeAt(sp);
    if (c === QUOTE) { sp++; return; }
    if (c === BACKSLASH) { sp++; const e = s.charCodeAt(sp); sp++; if (e === U) sp += 4; continue; }
    sp++;
  }
  throw new Error('unterminated string');
}
function sSkipValue(s, len) {
  const c = s.charCodeAt(sp);
  if (c === QUOTE) { sScanString(s, len); return; }
  if (c === LBRACE || c === LBRACK) { sSkipContainer(s, len); return; }
  while (sp < len) { const x = s.charCodeAt(sp); if (x === COMMA || x === RBRACE || x === RBRACK || sIsWs(x)) break; sp++; }
}
function sSkipContainer(s, len) {
  let depth = 0;
  for (;;) {
    if (sp >= len) throw new Error('unterminated container');
    const c = s.charCodeAt(sp);
    if (c === QUOTE) { sScanString(s, len); continue; }
    if (c === LBRACE || c === LBRACK) { depth++; sp++; continue; }
    if (c === RBRACE || c === RBRACK) { depth--; sp++; if (depth === 0) return; continue; }
    sp++;
  }
}
// ---- Optimized wire-path scanner: byte-only, early-stop, indexOf-accelerated ----
// Locates ONLY the edited members' value byte-spans, scanning just far enough to find
// them all; the unscanned tail is copied verbatim by applyEditsBytes (never parsed).
// No UTF-16 tracking (not needed on the wire path). Uses Uint8Array.indexOf (native)
// to skip string interiors instead of a JS per-byte loop.

function isWsB(b) { return b === 0x20 || b === 0x09 || b === 0x0a || b === 0x0d; }

// buf[i] === '"'. Returns index just past the closing quote, honouring escapes, via indexOf.
function skipStringFrom(buf, i, len) {
  i++; // opening quote
  for (;;) {
    const q = buf.indexOf(QUOTE, i);
    if (q < 0) throw new Error('unterminated string');
    let b = q - 1, bs = 0;
    while (b >= 0 && buf[b] === BACKSLASH) { bs++; b--; }
    i = q + 1;
    if ((bs & 1) === 0) return i; // even (incl. zero) backslashes → real closing quote
  }
}

function skipContainerFrom(buf, i, len) {
  let depth = 0;
  for (;;) {
    if (i >= len) throw new Error('unterminated container');
    const b = buf[i];
    if (b === QUOTE) { i = skipStringFrom(buf, i, len); continue; }
    if (b === LBRACE || b === LBRACK) { depth++; i++; continue; }
    if (b === RBRACE || b === RBRACK) { depth--; i++; if (depth === 0) return i; continue; }
    i++;
  }
}

function skipValueFrom(buf, i, len) {
  const b = buf[i];
  if (b === QUOTE) return skipStringFrom(buf, i, len);
  if (b === LBRACE || b === LBRACK) return skipContainerFrom(buf, i, len);
  while (i < len) { const c = buf[i]; if (c === COMMA || c === RBRACE || c === RBRACK || isWsB(c)) break; i++; }
  return i;
}

function eqName(buf, start, end, name) {
  if (end - start !== name.length) return false;
  for (let k = 0; k < name.length; k++) if (buf[start + k] !== name[k]) return false;
  return true;
}

// targets: array of { name: Uint8Array(raw name bytes), vbs, vbe }. Fills vbs/vbe for each;
// stops as soon as all are located. Returns true if all found.
export function scanTargets(buf, targets) {
  const len = buf.length;
  let i = 0, remaining = targets.length;
  for (let k = 0; k < targets.length; k++) { targets[k].vbs = -1; }
  if (len >= 3 && buf[0] === 0xef && buf[1] === 0xbb && buf[2] === 0xbf) i = 3;
  while (i < len && isWsB(buf[i])) i++;
  i++; // '{'
  while (remaining > 0 && i < len) {
    while (i < len && isWsB(buf[i])) i++;
    if (buf[i] === RBRACE) break;
    const ns = i + 1;
    i = skipStringFrom(buf, i, len);
    const ne = i - 1; // closing-quote index
    let hit = -1;
    for (let k = 0; k < targets.length; k++) {
      if (targets[k].vbs === -1 && eqName(buf, ns, ne, targets[k].name)) { hit = k; break; }
    }
    while (i < len && isWsB(buf[i])) i++;
    i++; // ':'
    while (i < len && isWsB(buf[i])) i++;
    const vs = i;
    i = skipValueFrom(buf, i, len);
    if (hit >= 0) { targets[hit].vbs = vs; targets[hit].vbe = i; remaining--; }
    while (i < len && isWsB(buf[i])) i++;
    if (buf[i] === COMMA) { i++; continue; }
    if (buf[i] === RBRACE) break;
  }
  return remaining === 0;
}

// Scan into `table` storing [valStart,valEnd] per slot (2 ints). Returns count.
export const SSLOT = 2;
export function scanStringInto(s, table) {
  const len = s.length;
  sp = 0;
  sSkipWs(s, len);
  if (s.charCodeAt(sp) !== LBRACE) throw new Error('expected {');
  sp++;
  let slot = 0;
  for (;;) {
    sSkipWs(s, len);
    if (s.charCodeAt(sp) === RBRACE) { sp++; break; }
    sScanString(s, len); // name (ignored)
    sSkipWs(s, len);
    if (s.charCodeAt(sp) !== COLON) throw new Error('expected :');
    sp++;
    sSkipWs(s, len);
    table[slot * SSLOT] = sp;
    sSkipValue(s, len);
    table[slot * SSLOT + 1] = sp;
    slot++;
    sSkipWs(s, len);
    const c = s.charCodeAt(sp);
    if (c === COMMA) { sp++; continue; }
    if (c === RBRACE) { sp++; break; }
    throw new Error('expected , or }');
  }
  return slot;
}
