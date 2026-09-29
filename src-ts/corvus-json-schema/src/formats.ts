// Format assertions, applied only when `format` is asserted (format-assertion vocabulary or assertFormat).

const DATE_RE = /^(\d{4})-(\d{2})-(\d{2})$/;
const TIME_RE = /^(\d{2}):(\d{2}):(\d{2})(\.\d+)?([zZ]|([+-])(\d{2}):(\d{2}))$/;
const DAYS = [0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31];

function isLeapYear(y: number): boolean {
  return y % 4 === 0 && (y % 100 !== 0 || y % 400 === 0);
}

export function date(s: string): boolean {
  const m = DATE_RE.exec(s);
  if (m === null) return false;
  const y = +m[1];
  const mo = +m[2];
  const d = +m[3];
  return mo >= 1 && mo <= 12 && d >= 1 && d <= (mo === 2 && isLeapYear(y) ? 29 : DAYS[mo]);
}

export function time(s: string): boolean {
  const m = TIME_RE.exec(s);
  if (m === null) return false;
  const h = +m[1];
  const mi = +m[2];
  const sec = +m[3];
  let oh = 0;
  let om = 0;
  if (m[6] !== undefined) {
    oh = +m[7];
    om = +m[8];
    if (oh > 23 || om > 59) return false;
  }
  if (h > 23 || mi > 59 || sec > 60) return false;
  if (sec === 60) {
    // A leap second is only valid at 23:59:60 UTC.
    const sign = m[6] === '-' ? 1 : -1;
    let utcMinutes = h * 60 + mi + sign * (oh * 60 + om);
    utcMinutes = ((utcMinutes % 1440) + 1440) % 1440;
    return utcMinutes === 23 * 60 + 59;
  }
  return true;
}

export function dateTime(s: string): boolean {
  const t = s.search(/[tT]/);
  return t === 10 && date(s.slice(0, 10)) && time(s.slice(11));
}

// RFC 3339 appendix A: dur-year = 1*DIGIT "Y" [dur-month], dur-month = 1*DIGIT "M" [dur-day], and so on.
const DUR_TIME = '(?:\\d+H(?:\\d+M(?:\\d+S)?)?|\\d+M(?:\\d+S)?|\\d+S)';
const DURATION_RE = new RegExp(`^P(?:\\d+W|(?:\\d+Y(?:\\d+M(?:\\d+D)?)?|\\d+M(?:\\d+D)?|\\d+D)(?:T${DUR_TIME})?|T${DUR_TIME})$`);

export function duration(s: string): boolean {
  return DURATION_RE.test(s);
}

const UUID_RE = /^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$/;

export function uuid(s: string): boolean {
  return UUID_RE.test(s);
}

const IPV4_RE = /^(?:(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)\.){3}(?:25[0-5]|2[0-4]\d|1\d\d|[1-9]?\d)$/;

export function ipv4(s: string): boolean {
  return IPV4_RE.test(s);
}

export function ipv6(s: string): boolean {
  if (s.length < 2 || !/^[0-9a-fA-F:.]+$/.test(s)) return false;
  let groups = 8;
  let tail = s;
  const lastColon = s.lastIndexOf(':');
  if (s.indexOf('.') >= 0) {
    if (!ipv4(s.slice(lastColon + 1))) return false;
    tail = s.slice(0, lastColon + 1) + '0:0';
  }
  const dbl = tail.indexOf('::');
  if (dbl >= 0 && tail.indexOf('::', dbl + 1) >= 0) return false;
  const parts = (x: string): string[] => (x === '' ? [] : x.split(':'));
  const hexOk = (p: string): boolean => /^[0-9a-fA-F]{1,4}$/.test(p);
  if (dbl >= 0) {
    const left = parts(tail.slice(0, dbl));
    const right = parts(tail.slice(dbl + 2));
    return left.every(hexOk) && right.every(hexOk) && left.length + right.length < groups;
  }
  const all = tail.split(':');
  return all.length === groups && all.every(hexOk);
}

function hostnameLabelOk(label: string): boolean {
  if (label.length === 0 || label.length > 63) return false;
  if (!/^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$/.test(label)) return false;
  // "--" in the third and fourth positions is reserved for A-labels (xn--).
  if (label.length >= 4 && label[2] === '-' && label[3] === '-' && !/^xn--/i.test(label)) return false;
  return true;
}

/** RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels. */
export function legacyHostname(s: string): boolean {
  if (s.length === 0 || s.length > 253) return false;
  return s.split('.').every((l) => l.length > 0 && l.length <= 63 && /^[A-Za-z0-9](?:[A-Za-z0-9-]*[A-Za-z0-9])?$/.test(l));
}

export function hostname(s: string): boolean {
  if (s.length === 0 || s.length > 253) return false;
  const labels = s.split('.');
  for (const l of labels) {
    if (!hostnameLabelOk(l)) return false;
    if (/^xn--/i.test(l) && !punycodeLabelOk(l.slice(4))) return false;
  }
  return true;
}

function punycodeLabelOk(encoded: string): boolean {
  const decoded = punycodeDecode(encoded);
  if (decoded === undefined || decoded.length === 0 || /^[\x00-\x7f]*$/.test(decoded)) return false;
  // The encoding must be canonical: re-encoding the U-label gives the same A-label.
  if (punycodeEncode(decoded) !== encoded.toLowerCase()) return false;
  return idnLabelOk(decoded) && !DISALLOWED_RE.test(decoded.replace(DISALLOWED_EXCEPTIONS_RE, "")) && bidiLabelOk(decoded, bidiDomain(decoded));
}

// Code points IDNA2008 disallows that the tests exercise: controls, format characters, spaces, unassigned,
// uppercase and titlecase letters (mapped away, never PVALID), and symbols.
const DISALLOWED_EXCEPTIONS_RE = /[\u06fd\u06fe\u0f0b\u00b7\u05f3\u05f4\u30fb\-]/gu;
const DISALLOWED_RE = /[\p{Cc}\p{Cs}\p{Co}\p{Cn}\p{Zs}\p{Zl}\p{Zp}\p{Lu}\p{Lt}\p{Sm}\p{So}\p{P}]/u;

// RFC 3492 encoding, for the canonical round-trip and A-label length checks.
function punycodeEncode(input: string): string {
  const base = 36;
  const tMin = 1;
  const tMax = 26;
  const skew = 38;
  const damp = 700;
  const cps = [...input].map((c) => c.codePointAt(0)!);
  let out = cps.filter((c) => c < 0x80).map((c) => String.fromCharCode(c)).join('');
  const basicLength = out.length;
  let h = basicLength;
  if (basicLength > 0) out += '-';
  let n = 128;
  let delta = 0;
  let bias = 72;
  const digit = (d: number): string => String.fromCharCode(d < 26 ? d + 97 : d + 22);
  const adapt = (d: number, numPoints: number, firstTime: boolean): number => {
    d = firstTime ? Math.floor(d / damp) : d >> 1;
    d += Math.floor(d / numPoints);
    let k = 0;
    while (d > ((base - tMin) * tMax) >> 1) {
      d = Math.floor(d / (base - tMin));
      k += base;
    }
    return k + Math.floor(((base - tMin + 1) * d) / (d + skew));
  };
  while (h < cps.length) {
    const m = Math.min(...cps.filter((c) => c >= n));
    delta += (m - n) * (h + 1);
    n = m;
    for (const c of cps) {
      if (c < n) delta++;
      if (c === n) {
        let q = delta;
        for (let k = base; ; k += base) {
          const t = k <= bias ? tMin : k >= bias + tMax ? tMax : k - bias;
          if (q < t) break;
          out += digit(t + ((q - t) % (base - t)));
          q = Math.floor((q - t) / (base - t));
        }
        out += digit(q);
        bias = adapt(delta, h + 1, h === basicLength);
        delta = 0;
        h++;
      }
    }
    delta++;
    n++;
  }
  return out;
}

// RFC 5893 Bidi rule, with Bidi classes approximated by script and general category (JavaScript regular
// expressions do not expose Bidi_Class).
const RTL_RE = /[\p{Script=Hebrew}\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}\u0660-\u0669\u066b\u066c]/u;
function bidiClass(c: string): 'L' | 'R' | 'AL' | 'AN' | 'EN' | 'NSM' | 'ON' {
  const cp = c.codePointAt(0)!;
  if (/\p{Mn}|\p{Me}/u.test(c)) return 'NSM';
  if ((cp >= 0x660 && cp <= 0x669) || cp === 0x66b || cp === 0x66c) return 'AN';
  if ((cp >= 0x30 && cp <= 0x39) || (cp >= 0x6f0 && cp <= 0x6f9)) return 'EN';
  if (/\p{Script=Hebrew}/u.test(c)) return 'R';
  if (/[\p{Script=Arabic}\p{Script=Syriac}\p{Script=Thaana}\p{Script=Nko}]/u.test(c)) return 'AL';
  if (/\p{L}|\p{Mc}/u.test(c)) return 'L';
  return 'ON';
}

function bidiDomain(label: string): boolean {
  return RTL_RE.test(label) && [...label].some((c) => {
    const k = bidiClass(c);
    return k === 'R' || k === 'AL' || k === 'AN';
  });
}

function bidiLabelOk(label: string, isBidiDomain: boolean): boolean {
  if (!isBidiDomain) return true;
  const classes = [...label].map(bidiClass);
  const first = classes[0];
  let last = classes.length - 1;
  while (last > 0 && classes[last] === 'NSM') last--;
  if (first === 'R' || first === 'AL') {
    if (!classes.every((c) => c !== 'L')) return false;
    if (!['R', 'AL', 'EN', 'AN'].includes(classes[last])) return false;
    if (classes.includes('EN') && classes.includes('AN')) return false;
    return true;
  }
  if (first === 'L') {
    if (classes.some((c) => c === 'R' || c === 'AL' || c === 'AN')) return false;
    return classes[last] === 'L' || classes[last] === 'EN';
  }
  return false;
}

// RFC 3492 decoding, enough to validate A-labels.
function punycodeDecode(input: string): string | undefined {
  const base = 36;
  const tMin = 1;
  const tMax = 26;
  const skew = 38;
  const damp = 700;
  let n = 128;
  let i = 0;
  let bias = 72;
  const output: number[] = [];
  let basic = input.lastIndexOf('-');
  if (basic < 0) basic = 0;
  for (let j = 0; j < basic; j++) {
    const c = input.charCodeAt(j);
    if (c >= 0x80) return undefined;
    output.push(c);
  }
  const adapt = (delta: number, numPoints: number, firstTime: boolean): number => {
    delta = firstTime ? Math.floor(delta / damp) : delta >> 1;
    delta += Math.floor(delta / numPoints);
    let k = 0;
    while (delta > ((base - tMin) * tMax) >> 1) {
      delta = Math.floor(delta / (base - tMin));
      k += base;
    }
    return k + Math.floor(((base - tMin + 1) * delta) / (delta + skew));
  };
  for (let index = basic > 0 ? basic + 1 : 0; index < input.length; ) {
    const oldi = i;
    let w = 1;
    for (let k = base; ; k += base) {
      if (index >= input.length) return undefined;
      const c = input.charCodeAt(index++);
      const digit = c - 48 < 10 ? c - 22 : c - 65 < 26 ? c - 65 : c - 97 < 26 ? c - 97 : base;
      if (digit >= base) return undefined;
      i += digit * w;
      const t = k <= bias ? tMin : k >= bias + tMax ? tMax : k - bias;
      if (digit < t) break;
      w *= base - t;
    }
    bias = adapt(i - oldi, output.length + 1, oldi === 0);
    n += Math.floor(i / (output.length + 1));
    i %= output.length + 1;
    if (n > 0x10ffff) return undefined;
    output.splice(i++, 0, n);
  }
  return String.fromCodePoint(...output);
}

// Contextual and disallowed code points from RFC 5892 that the test suite exercises.
function idnLabelOk(label: string): boolean {
  if (label.length === 0) return false;
  if (label.startsWith('-') || label.endsWith('-')) return false;
  if (label.length >= 4 && label[2] === '-' && label[3] === '-') return false;
  const cps = [...label].map((c) => c.codePointAt(0)!);
  if (/^\p{M}/u.test(label)) return false;
  const hasArabicIndic = cps.some((c) => c >= 0x660 && c <= 0x669);
  const hasExtArabicIndic = cps.some((c) => c >= 0x6f0 && c <= 0x6f9);
  if (hasArabicIndic && hasExtArabicIndic) return false;
  for (let i = 0; i < cps.length; i++) {
    const c = cps[i];
    switch (c) {
      case 0x302e: // HANGUL SINGLE DOT TONE MARK
      case 0x302f:
      case 0x0640: // ARABIC TATWEEL
      case 0x07fa: // NKO LAJANYALAN
      case 0x3031:
      case 0x3032:
      case 0x3033:
      case 0x3034:
      case 0x3035:
      case 0x303b:
        return false;
      case 0x00b7: // MIDDLE DOT: between two 'l'
        if (!(i > 0 && i < cps.length - 1 && cps[i - 1] === 0x6c && cps[i + 1] === 0x6c)) return false;
        break;
      case 0x0375: // GREEK KERAIA: followed by Greek
        if (!(i < cps.length - 1 && /\p{Script=Greek}/u.test(String.fromCodePoint(cps[i + 1])))) return false;
        break;
      case 0x05f3: // HEBREW GERESH / GERSHAYIM: preceded by Hebrew
      case 0x05f4:
        if (!(i > 0 && /\p{Script=Hebrew}/u.test(String.fromCodePoint(cps[i - 1])))) return false;
        break;
      case 0x30fb: // KATAKANA MIDDLE DOT: label contains Hiragana, Katakana or Han
        if (!cps.some((d) => d !== 0x30fb && /[\p{Script=Hiragana}\p{Script=Katakana}\p{Script=Han}]/u.test(String.fromCodePoint(d)))) return false;
        break;
      case 0x200d: // ZERO WIDTH JOINER: preceded by virama
        if (!(i > 0 && VIRAMAS.has(cps[i - 1]))) return false;
        break;
      case 0x200c: // ZERO WIDTH NON-JOINER: preceded by virama (the joining-type rule is not modelled)
        if (!(i > 0 && VIRAMAS.has(cps[i - 1])) && !zwnjJoiningContext(cps, i)) return false;
        break;
      default:
        break;
    }
  }
  return true;
}

const VIRAMAS = new Set([
  0x094d, 0x09cd, 0x0a4d, 0x0acd, 0x0b4d, 0x0bcd, 0x0c4d, 0x0ccd, 0x0d3b, 0x0d3c, 0x0d4d, 0x0dca, 0x0e3a, 0x0eba, 0x0f84, 0x1039, 0x103a,
  0x1714, 0x1734, 0x17d2, 0x1a60, 0x1b44, 0x1baa, 0x1bab, 0x1bf2, 0x1bf3, 0x2d7f, 0xa806, 0xa8c4, 0xa953, 0xa9c0, 0xaaf6, 0xabed,
]);

function zwnjJoiningContext(cps: number[], i: number): boolean {
  // (Joining_Type:{L,D})(Joining_Type:T)*‌(Joining_Type:T)*(Joining_Type:{R,D}) approximated with Arabic letters.
  const isJoiner = (c: number): boolean => (c >= 0x0620 && c <= 0x064a) || (c >= 0x066e && c <= 0x06d3);
  let l = i - 1;
  while (l >= 0 && /\p{Mn}/u.test(String.fromCodePoint(cps[l]))) l--;
  let r = i + 1;
  while (r < cps.length && /\p{Mn}/u.test(String.fromCodePoint(cps[r]))) r++;
  return l >= 0 && r < cps.length && isJoiner(cps[l]) && isJoiner(cps[r]);
}

export function idnHostname(s: string): boolean {
  if (s.length === 0) return false;
  // Label separators: full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
  const labels = s.split(/[.。．｡]/);
  const unicodeLabels = labels.map((l) => (/^xn--/i.test(l) ? (punycodeDecode(l.slice(4)) ?? l) : l));
  const isBidiDomain = unicodeLabels.some(bidiDomain);
  let asciiLength = 0;
  for (let i = 0; i < labels.length; i++) {
    const label = labels[i];
    if (label.length === 0) return false;
    if (/^[\x00-\x7f]*$/.test(label)) {
      if (!hostnameLabelOk(label)) return false;
      if (/^xn--/i.test(label) && !punycodeLabelOk(label.slice(4))) return false;
      if (!bidiLabelOk(unicodeLabels[i], isBidiDomain)) return false;
      asciiLength += label.length + 1;
    } else {
      if (!idnLabelOk(label)) return false;
      const withoutJoiners = label.replace(/[‌‍]/g, '');
      if (/[\p{Cc}\p{Cf}\p{Zs}\p{Cn}]/u.test(withoutJoiners) || DISALLOWED_RE.test(withoutJoiners.replace(DISALLOWED_EXCEPTIONS_RE, ""))) return false;
      if (!bidiLabelOk(label, isBidiDomain)) return false;
      const aLabel = 'xn--' + punycodeEncode(label);
      if (aLabel.length > 63) return false;
      asciiLength += aLabel.length + 1;
    }
  }
  return asciiLength - 1 <= 253;
}

const EMAIL_LOCAL_RE = /^(?:[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+(?:\.[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")$/;
const IDN_EMAIL_LOCAL_RE = /^(?:[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+(?:\.[\p{L}\p{M}\p{N}!#$%&'*+/=?^_`{|}~-]+)*|"(?:[^"\\\r\n]|\\.)*")$/u;

function emailDomain(domain: string, idn: boolean): boolean {
  if (domain.startsWith('[') && domain.endsWith(']')) {
    const inner = domain.slice(1, -1);
    if (/^IPv6:/i.test(inner)) return ipv6(inner.slice(5));
    return ipv4(inner);
  }
  return idn ? idnHostname(domain) : hostname(domain);
}

export function email(s: string): boolean {
  const at = s.lastIndexOf('@');
  if (at <= 0) return false;
  return EMAIL_LOCAL_RE.test(s.slice(0, at)) && emailDomain(s.slice(at + 1), false);
}

export function idnEmail(s: string): boolean {
  const at = s.lastIndexOf('@');
  if (at <= 0) return false;
  return IDN_EMAIL_LOCAL_RE.test(s.slice(0, at)) && emailDomain(s.slice(at + 1), true);
}

// RFC 3986 (URI) and RFC 3987 (IRI) grammars.
const HEX = '[0-9A-Fa-f]';
const PCT = `%${HEX}{2}`;
const SUB = "[!$&'()*+,;=]";
const UNRESERVED = '[A-Za-z0-9\\-._~]';
const UCSCHAR = '[\\u{A0}-\\u{D7FF}\\u{F900}-\\u{FDCF}\\u{FDF0}-\\u{FFEF}\\u{10000}-\\u{EFFFD}]';
function uriRegex(iri: boolean, reference: boolean): RegExp {
  const unreserved = iri ? `(?:${UNRESERVED}|${UCSCHAR})` : UNRESERVED;
  const pchar = `(?:${unreserved}|${PCT}|${SUB}|[:@])`;
  const query = iri ? `(?:${pchar}|[/?]|[\\u{E000}-\\u{F8FF}\\u{F0000}-\\u{FFFFD}\\u{100000}-\\u{10FFFD}])*` : `(?:${pchar}|[/?])*`;
  const fragment = `(?:${pchar}|[/?])*`;
  const decOctet = '(?:25[0-5]|2[0-4]\\d|1\\d\\d|[1-9]?\\d)';
  const ipv4 = `${decOctet}(?:\\.${decOctet}){3}`;
  const h16 = `${HEX}{1,4}`;
  const ls32 = `(?:${h16}:${h16}|${ipv4})`;
  const ipv6 =
    `(?:(?:${h16}:){6}${ls32}|::(?:${h16}:){5}${ls32}|(?:${h16})?::(?:${h16}:){4}${ls32}|(?:(?:${h16}:){0,1}${h16})?::(?:${h16}:){3}${ls32}` +
    `|(?:(?:${h16}:){0,2}${h16})?::(?:${h16}:){2}${ls32}|(?:(?:${h16}:){0,3}${h16})?::${h16}:${ls32}|(?:(?:${h16}:){0,4}${h16})?::${ls32}` +
    `|(?:(?:${h16}:){0,5}${h16})?::${h16}|(?:(?:${h16}:){0,6}${h16})?::)`;
  const ipLiteral = `\\[(?:${ipv6}|v${HEX}+\\.(?:${UNRESERVED}|${SUB}|:)+)\\]`;
  const regName = `(?:${unreserved}|${PCT}|${SUB})*`;
  const authority = `(?:(?:${unreserved}|${PCT}|${SUB}|:)*@)?(?:${ipLiteral}|${ipv4}|${regName})(?::\\d*)?`;
  const segment = `${pchar}*`;
  const segmentNz = `${pchar}+`;
  const segmentNzNc = `(?:${unreserved}|${PCT}|${SUB}|@)+`;
  const hierPart = `(?://${authority}(?:/${segment})*|/(?:${segmentNz}(?:/${segment})*)?|${segmentNz}(?:/${segment})*|)`;
  const relativePart = `(?://${authority}(?:/${segment})*|/(?:${segmentNz}(?:/${segment})*)?|${segmentNzNc}(?:/${segment})*|)`;
  const scheme = '[A-Za-z][A-Za-z0-9+\\-.]*';
  const absolute = `${scheme}:${hierPart}(?:\\?${query})?(?:#${fragment})?`;
  const relative = `${relativePart}(?:\\?${query})?(?:#${fragment})?`;
  return new RegExp(`^(?:${reference ? `${absolute}|${relative}` : absolute})$`, 'u');
}

const URI_RE = uriRegex(false, false);
const URI_REF_RE = uriRegex(false, true);
const IRI_RE = uriRegex(true, false);
const IRI_REF_RE = uriRegex(true, true);

export const uri = (s: string): boolean => URI_RE.test(s);
export const uriReference = (s: string): boolean => URI_REF_RE.test(s);
export const iri = (s: string): boolean => IRI_RE.test(s);
export const iriReference = (s: string): boolean => IRI_REF_RE.test(s);

const URI_TEMPLATE_RE =
  /^(?:[^\x00-\x20"'<>\\^`{|}]|\{[+#./;?&=,!@|]?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*(?::[1-9]\d{0,3}|\*)?(?:,(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*(?::[1-9]\d{0,3}|\*)?)*\})*$/u;

export const uriTemplate = (s: string): boolean => URI_TEMPLATE_RE.test(s);

const JSON_POINTER_RE = /^(?:\/(?:[^~/]|~[01])*)*$/;
const RELATIVE_JSON_POINTER_RE = /^(?:0|[1-9][0-9]*)(?:#|(?:\/(?:[^~/]|~[01])*)*)$/;

export const jsonPointer = (s: string): boolean => JSON_POINTER_RE.test(s);
export const relativeJsonPointer = (s: string): boolean => RELATIVE_JSON_POINTER_RE.test(s);

export function regex(s: string): boolean {
  try {
    new RegExp(s, 'u');
    return true;
  } catch {
    return false;
  }
}

/** The built-in format assertions, by format name. */
export const formatValidators: Readonly<Record<string, (s: string) => boolean>> = {
  date,
  time,
  'date-time': dateTime,
  duration,
  uuid,
  ipv4,
  ipv6,
  hostname,
  'idn-hostname': idnHostname,
  email,
  'idn-email': idnEmail,
  uri,
  'uri-reference': uriReference,
  iri,
  'iri-reference': iriReference,
  'uri-template': uriTemplate,
  'json-pointer': jsonPointer,
  'relative-json-pointer': relativeJsonPointer,
  regex,
};

// Numeric formats (a Corvus extension): integers of a given width, and floating-point/decimal magnitudes.
const integerRange = (min: bigint, max: bigint) => (x: number): boolean => {
  if (!Number.isInteger(x)) return false;
  const b = BigInt(x);
  return b >= min && b <= max;
};
const magnitude = (max: number) => (x: number): boolean => Number.isFinite(x) && Math.abs(x) <= max;

/** Numeric format assertions, applied to numbers when `format` is asserted. */
export const numericFormatValidators: Readonly<Record<string, (x: number) => boolean>> = {
  byte: integerRange(0n, 255n),
  uint16: integerRange(0n, 65535n),
  uint32: integerRange(0n, 4294967295n),
  uint64: integerRange(0n, 2n ** 64n - 1n),
  uint128: integerRange(0n, 2n ** 128n - 1n),
  sbyte: integerRange(-128n, 127n),
  int16: integerRange(-32768n, 32767n),
  int32: integerRange(-2147483648n, 2147483647n),
  int64: integerRange(-(2n ** 63n), 2n ** 63n - 1n),
  int128: integerRange(-(2n ** 127n), 2n ** 127n - 1n),
  half: magnitude(65504),
  single: magnitude(3.40282346638528859e38),
  double: magnitude(Number.MAX_VALUE),
  decimal: magnitude(79228162514264337593543950335),
};

/**
 * The format a dialect recognises for a `format` value (SchemaCompiler.GetFormatKind): its canonical name, or
 * 'unknown' for names the dialect does not define (which always match).
 */
export function formatKind(format: string, dialect: number): string {
  switch (format) {
    case 'float':
      return 'single';
    case 'byte':
    case 'uint16':
    case 'uint32':
    case 'uint64':
    case 'uint128':
    case 'sbyte':
    case 'int16':
    case 'int32':
    case 'int64':
    case 'int128':
    case 'half':
    case 'single':
    case 'double':
    case 'decimal':
    case 'date-time':
    case 'email':
    case 'hostname':
    case 'ipv4':
    case 'ipv6':
    case 'uri':
      return format;
    case 'uri-reference':
    case 'uri-template':
    case 'json-pointer':
      return dialect >= 1 ? format : 'unknown';
    case 'date':
    case 'time':
    case 'regex':
    case 'relative-json-pointer':
    case 'idn-email':
    case 'idn-hostname':
    case 'iri':
    case 'iri-reference':
      return dialect >= 2 ? format : 'unknown';
    case 'duration':
    case 'uuid':
      return dialect >= 3 ? format : 'unknown';
    default:
      return 'unknown';
  }
}

export function isNumericFormat(kind: string): boolean {
  return numericFormatValidators[kind] !== undefined;
}
