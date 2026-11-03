// The mapping step of UTS #46 (Unicode IDNA Compatibility Processing, section 4 step 1), which the idn-hostname and
// idn-email formats apply to a name before they check it.
//
// An "ignored" code point is removed and a "mapped" one is replaced by its mapping. Every other code point is kept:
// "valid" and "deviation" ones (nontransitional processing, so ZERO WIDTH JOINER and ZERO WIDTH NON-JOINER stay and meet
// the contextual rules of RFC 5892), and "disallowed" ones, which the IDNA2008 checks that follow refuse. ASCII is kept
// as it is: its mapping only lowercases the letters, which does not change whether a name is valid. The normalization
// step (step 2) is not applied: a name that is not in Normalization Form C is valid as a host name, as the
// JSON-Schema-Test-Suite expects. The table is the one the .NET engine and the Rust crate use.

import { UTS46_ENTRIES, UTS46_POOL } from './uts46-data.js';

const ENTRY_SIZE = 8;
const decoder = new TextDecoder();

/** The index of the entry for a code point outside ASCII, or -1 if it is neither ignored nor mapped. */
function find(c: number): number {
  let low = 0;
  let high = UTS46_ENTRIES.length / ENTRY_SIZE;
  while (low < high) {
    const middle = (low + high) >>> 1;
    const at = middle * ENTRY_SIZE;
    const start = (UTS46_ENTRIES[at] << 16) | (UTS46_ENTRIES[at + 1] << 8) | UTS46_ENTRIES[at + 2];
    const end = start + ((UTS46_ENTRIES[at + 3] << 8) | UTS46_ENTRIES[at + 4]);
    if (c < start) high = middle;
    else if (c > end) low = middle + 1;
    else return at;
  }
  return -1;
}

/** The mapped form of a name; the name itself when nothing in it is mapped or ignored. */
export function uts46Map(name: string): string {
  let changes = false;
  for (const ch of name) {
    const c = ch.codePointAt(0)!;
    if (c >= 0x80 && find(c) >= 0) {
      changes = true;
      break;
    }
  }
  if (!changes) return name;
  let mapped = '';
  for (const ch of name) {
    const c = ch.codePointAt(0)!;
    const entry = c >= 0x80 ? find(c) : -1;
    if (entry < 0) {
      mapped += ch;
      continue;
    }
    const offset = (UTS46_ENTRIES[entry + 5] << 8) | UTS46_ENTRIES[entry + 6];
    const length = UTS46_ENTRIES[entry + 7];
    if (length > 0) mapped += decoder.decode(UTS46_POOL.subarray(offset, offset + length));
  }
  return mapped;
}
