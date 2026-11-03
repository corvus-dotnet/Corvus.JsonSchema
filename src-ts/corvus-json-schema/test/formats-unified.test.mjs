import assert from 'node:assert/strict';
import { test } from 'node:test';
import { formatValidators } from '../dist/formats.js';
import { uts46Map } from '../dist/uts46.js';
import { UTS46_UNICODE_VERSION } from '../dist/uts46-data.js';

// The cases of the unified JSON-Schema-Test-Suite that the format checks got wrong (the descriptions are the suite's).
const cases = [
  ['email', '"test\\\u00a9"@iana.org', false, 'a non-ASCII character in a quoted pair is not valid'],
  ['email', '"\\a"@iana.org', true, 'a quoted pair in the local part is valid'],
  ['idn-email', '\u0085@example.com', true, 'a C1 control in the local part is valid'],
  ['idn-email', '\uffff@example.com', true, 'a noncharacter in the local part is valid'],
  ['idn-hostname', 'a\u200bb', true, 'a zero width space is ignored by the mapping'],
  ['idn-hostname', '\uff41'.repeat(63), true, 'a label of 63 fullwidth characters is valid after mapping'],
  ['idn-hostname', '\uff58\uff4e--nxasmq6b', true, 'an ACE prefix produced by the mapping step is decoded as an A-label'],
  ['uri-template', "a'b", true, 'an apostrophe in a literal is valid'],
  ['uri-template', 'a%', false, 'a literal with a lone percent sign is invalid'],
  ['uri-template', 'a%4', false, 'a literal with an incomplete percent-encoded triplet is invalid'],
  ['uri-template', 'a%GG', false, 'a literal with non-hex percent encoding is invalid'],
  ['uri-template', 'a\u007fb', false, 'a delete character in a literal is invalid'],
];

test('the formats match the unified suite', () => {
  for (const [format, text, expected, description] of cases) {
    assert.equal(formatValidators[format](text), expected, `${format}: ${description}`);
  }
});

test('the UTS #46 mapping', () => {
  assert.equal(UTS46_UNICODE_VERSION, '17.0.0');
  assert.equal(uts46Map('a\u200bb'), 'ab');
  assert.equal(uts46Map('a\u200db'), 'a\u200db');
  // A name padded past the bound with ignored code points is not mapped down to a short name.
  assert.equal(formatValidators['idn-hostname']('\u200b'.repeat(400) + 'a'), false);
});
