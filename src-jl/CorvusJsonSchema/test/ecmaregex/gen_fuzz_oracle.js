// Writes v8_fuzz.json, the expectations of fuzz.jl, by asking V8 what each pattern does. Run it with
// "node gen_fuzz_oracle.js > v8_fuzz.json". It needs Node 20 or later. The tests do not run it. They read its output.
//
// v8_oracle.json, which the Java and Go ports share, holds patterns written by hand and short random strings of
// syntax characters, most of which are not patterns. This file holds patterns built from a grammar, so that every
// one is valid and puts groups, lookarounds, backreferences, classes and quantifiers inside one another, which is
// where an engine goes wrong. Each is matched with the u flag, alone or with the i, m or s flags, against every text
// of a fixed list.
//
// A pattern is searched for as ECMA-262 says (RegExpBuiltinExec): it is tried at each character of the text in turn,
// with the y flag. V8's own search also tries a pattern in the middle of a surrogate pair, where ECMA-262 has no
// position: it answers that /\B/u matches "a\u{1F600}b", in which every position between characters is a word
// boundary.
//
// Nothing here needs the ECMAScript 2025 additions (modifier groups and duplicate group names), which Node 20 lacks.
// The test wraps a pattern that was matched with flags in the modifier group that means the same.
'use strict';

// xorshift32, so that the file is the same on every run. Another seed, given after the number of patterns, writes
// other patterns to try the translator on ("node gen_fuzz_oracle.js 40000 99 > other.json").
let seed = Number(process.argv[3] || 0x1234abcd) >>> 0 || 1;
function next(n) {
  seed ^= seed << 13; seed >>>= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5; seed >>>= 0;
  return seed % n;
}
const pick = (items) => items[next(items.length)];

const textAlphabet = ['a', 'b', 'c', 'k', 'K', 's', 'S', 'é', '\u{1F600}', '1', '\n', ' ', 'K', 'ſ',
  '-', 'A', 'B', '_', 'σ', 'ς'];
const texts = ['', 'a', 'b', 'c', 'ab', 'ba', 'aa', 'abc', 'aab', 'abab', 'k', 'K', 'K', 's', 'ſ', '\n',
  'a\nb', '\u{1F600}', 'a\u{1F600}b', 'é'];
while (texts.length < 320) {
  let text = '';
  for (let k = next(9); k > 0; k--) {
    text += next(3) === 0 ? pick(textAlphabet) : pick(['a', 'b', 'c']);
  }
  if (!texts.includes(text)) {
    texts.push(text);
  }
}

// The character beyond the Basic Multilingual Plane is written as an escape. V8 11.3 misreads a pattern in which
// the character itself follows a reference to a group that comes later: it answers that /(?<=K\1\u{1F600})()c/u
// matches "acK\u{1F600}c" when the character is written as that escape, and that it does not when the character
// is written as itself.
const literals = ['a', 'b', 'c', 'a', 'b', 'k', 's', '\u00e9', '\\u{1F600}', '1', '\\n', ' ', '-', 'ab', 'ba', 'S'];
const classes = ['.', '[ab]', '[^a]', '[a-c]', '\\d', '\\w', '\\s', '\\W', '\\D', '\\S', '[\\w-]', '[^\\s\\d]',
  '\\p{L}', '\\p{Lu}', '\\P{L}', '[^]', '[k-s]', '[\\u{1F600}a]', '[^\\u{1F600}\\n]', '[\\p{Ll}1]'];
const assertions = ['^', '$', '\\b', '\\B'];
const quantifiers = ['*', '+', '?', '{2}', '{1,2}', '{0,3}', '{2,}', '*?', '+?', '??', '{1,2}?', '{0,3}?'];
const names = ['n', 'm'];

// The state of the pattern being built: how many groups it has, and which names are taken.
let groups;
let named;

function atom(depth) {
  const kind = next(depth > 0 ? 16 : 8);
  if (kind < 4) return pick(literals);
  if (kind < 7) return pick(classes);
  if (kind < 8) return pick(assertions);
  if (kind < 10) return '(?:' + disjunction(depth - 1) + ')';
  if (kind < 12) {
    groups++;
    if (next(4) === 0) {
      const free = names.filter((name) => !named.includes(name));
      if (free.length > 0) {
        named.push(free[0]);
        return '(?<' + free[0] + '>' + disjunction(depth - 1) + ')';
      }
    }
    return '(' + disjunction(depth - 1) + ')';
  }
  if (kind < 13) return pick(['(?=', '(?!']) + disjunction(depth - 1) + ')';
  if (kind < 15) return pick(['(?<=', '(?<!']) + disjunction(depth - 1) + ')';
  return '\\' + (1 + next(3));
}

function term(depth) {
  const a = atom(depth);
  // An assertion takes no quantifier with the u flag.
  if (assertions.includes(a) || a.startsWith('(?=') || a.startsWith('(?!') || a.startsWith('(?<=') ||
      a.startsWith('(?<!')) {
    return a;
  }
  const quantified = a === 'ab' || a === 'ba' ? '(?:' + a + ')' : a;
  return next(3) === 0 ? quantified + pick(quantifiers) : a;
}

function alternative(depth) {
  let out = '';
  for (let k = 1 + next(3); k > 0; k--) {
    out += term(depth);
  }
  return out;
}

function disjunction(depth) {
  let out = alternative(depth);
  for (let k = next(4) === 0 ? 1 + next(2) : 0; k > 0; k--) {
    out += '|' + alternative(depth);
  }
  return out;
}

// Whether a pattern with the u and y flags matches somewhere in the text: at some position between two characters.
function search(re, text) {
  for (let at = 0; at <= text.length; at++) {
    const c = text.charCodeAt(at);
    if (c >= 0xdc00 && c <= 0xdfff && at > 0) {
      continue;
    }
    re.lastIndex = at;
    if (re.test(text)) {
      return true;
    }
  }
  return false;
}

const patterns = [];
const seen = new Set();
const budget = Number(process.argv[2] || 6000);
let dropped = 0;
while (patterns.length < budget) {
  groups = 0;
  named = [];
  let p = disjunction(1 + next(3));
  // Some patterns end with a reference to a name, where there is one.
  if (named.length > 0 && next(2) === 0) {
    p += '\\k<' + pick(named) + '>';
  }
  const f = pick(['', '', '', 'i', 'i', 'm', 's', 'ims']);
  if (seen.has(f + ':' + p)) {
    continue;
  }
  seen.add(f + ':' + p);
  let re;
  try {
    re = new RegExp(p, 'uy' + f);
  } catch (e) {
    // A reference to a group the pattern does not have.
    dropped++;
    continue;
  }
  let bits = '';
  let nibble = 0;
  for (let i = 0; i < texts.length; i++) {
    nibble = (nibble << 1) | (search(re, texts[i]) ? 1 : 0);
    if (i % 4 === 3) {
      bits += nibble.toString(16);
      nibble = 0;
    }
  }
  patterns.push({ p, f, m: bits });
}

process.stdout.write(JSON.stringify({ node: process.version, v8: process.versions.v8,
  unicode: process.versions.unicode, texts, patterns }).replace(/[\u007f-￿]/g,
  (c) => '\\u' + c.charCodeAt(0).toString(16).padStart(4, '0')) + '\n');
process.stderr.write(patterns.length + ' patterns, ' + dropped + ' dropped\n');
