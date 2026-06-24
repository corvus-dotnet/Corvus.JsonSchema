// Proof for design §4.1: exact numeric validation on the number's ASCII/text token,
// mirroring the C# BigNumber approach, with ZERO third-party dependency (native BigInt).
// Demonstrates multipleOf / compare / equality that pass where the JS double path fails.
// Run: node number-exact.mjs

// Parse a JSON number literal into sign * mantissa * 10^exp (mantissa: BigInt >= 0).
function parseDecimal(s) {
  s = s.trim();
  let i = 0, sign = 1n;
  if (s[i] === '+') { i++; } else if (s[i] === '-') { sign = -1n; i++; }
  let intPart = '', fracPart = '', expPart = '';
  while (i < s.length && s[i] >= '0' && s[i] <= '9') { intPart += s[i++]; }
  if (s[i] === '.') { i++; while (i < s.length && s[i] >= '0' && s[i] <= '9') { fracPart += s[i++]; } }
  if (s[i] === 'e' || s[i] === 'E') {
    i++; let es = '';
    if (s[i] === '+' || s[i] === '-') { es = s[i++]; }
    while (i < s.length && s[i] >= '0' && s[i] <= '9') { es += s[i++]; }
    expPart = es;
  }
  const digits = (intPart + fracPart) || '0';
  const mantissa = BigInt(digits);
  const exp = (expPart ? parseInt(expPart, 10) : 0) - fracPart.length;
  return { sign, mantissa, exp };
}

// Exact multipleOf: value is a multiple of divisor (divisor must be > 0 per spec).
export function isMultipleOf(valueStr, divisorStr) {
  const v = parseDecimal(valueStr), d = parseDecimal(divisorStr);
  if (d.mantissa === 0n) { return false; }
  const e = Math.min(v.exp, d.exp);
  const vi = v.mantissa * 10n ** BigInt(v.exp - e);
  const di = d.mantissa * 10n ** BigInt(d.exp - e);
  return vi % di === 0n;
}

// Exact compare of mathematical value: -1 / 0 / 1.
export function cmp(aStr, bStr) {
  const a = parseDecimal(aStr), b = parseDecimal(bStr);
  const av = a.sign * a.mantissa, bv = b.sign * b.mantissa;
  const e = Math.min(a.exp, b.exp);
  const ai = av * 10n ** BigInt(a.exp - e);
  const bi = bv * 10n ** BigInt(b.exp - e);
  return ai < bi ? -1 : ai > bi ? 1 : 0;
}

// ---- tests ----
let pass = 0, fail = 0;
function check(label, got, want) {
  const ok = got === want;
  if (ok) { pass++; } else { fail++; console.log(`FAIL ${label}: got ${got}, want ${want}`); }
}

// multipleOf — exact decimals (the classic JS-double traps)
check('0.0075 % 0.0001', isMultipleOf('0.0075', '0.0001'), true);
check('1.1 % 0.1', isMultipleOf('1.1', '0.1'), true);     // JS: (1.1 % 0.1) !== 0  (naive fails)
check('4.5 % 1.5', isMultipleOf('4.5', '1.5'), true);
check('35 % 1.5', isMultipleOf('35', '1.5'), false);
check('0 % 5', isMultipleOf('0', '5'), true);
check('1e308 % 1e308', isMultipleOf('1e308', '1e308'), true);
check('10 % 1e1', isMultipleOf('10', '1e1'), true);

// equality / compare — across representations and beyond 2^53
check('1.0 == 1', cmp('1.0', '1'), 0);
check('10 == 1e1', cmp('10', '1e1'), 0);
check('big int distinct', cmp('9007199254740993', '9007199254740992'), 1); // JS Number collapses these
check('-2 < 3', cmp('-2', '3'), -1);

// show the JS double path WOULD be wrong on these
check('demo: JS 1.1%0.1 != 0 (so naive multipleOf is wrong)', (1.1 % 0.1) !== 0, true);
check('demo: JS Number collapses big ints', Number('9007199254740993') === Number('9007199254740992'), true);

console.log(`\n${pass} passed, ${fail} failed`);
if (fail > 0) { process.exit(1); }
console.log('OK — exact numeric validation on the text token, zero-dependency (native BigInt).');
