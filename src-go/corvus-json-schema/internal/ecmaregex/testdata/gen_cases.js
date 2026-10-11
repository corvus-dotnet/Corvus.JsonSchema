// Writes v8_cases.json, the expectations of the tests of matching by UTF-16 code unit and of repetitions that cannot
// consume a character (cases_test.go here, pattern_test.go of the module, and TestEcmaRegex.pas of the Object Pascal
// port). Run it with "node gen_cases.js > v8_cases.json". It needs Node 24 or later. The tests do not run it. They
// read its output.
//
// A pattern is read with the u flag when that is valid and with no flag otherwise, which is what Compile does. Each
// set has its own texts, and every pattern of the set is matched against every one of them.
//
//   - units: patterns that are valid only without the u flag, which V8 matches a UTF-16 code unit at a time, against
//     texts with characters beyond the Basic Multilingual Plane. Some are not valid in either grammar.
//   - loops: repetitions whose body cannot consume a character, and backreferences inside the group they name. The
//     first thirteen took the matcher longer than anyone would wait before it rewrote them.
//   - fast: patterns of the shapes the module matches without the engine, valid only without the u flag.
'use strict';
const A = "\u{10400}", B = "\u{10428}", E = "\u{1F600}";
function ask(p, texts) {
  let u = null, l = null;
  try { u = new RegExp(p, "u"); } catch (e) {}
  try { l = new RegExp(p); } catch (e) {}
  const re = u || l;
  return { p, u: !!u, l: !!l, tests: re ? texts.map((t) => ({ t, want: re.test(t) })) : [] };
}
const unitTexts = ["", "a", "x", A, B, E, A + A, A + B, B + A, "a" + A, A + "a", "a" + A + "a", A + A + A, "aA", "k\u212a",
  "\u00e9", "\u00e9" + A, A + "\u00e9", "\ufffd", "\ufffd" + A, A + "\ufffd", E + ".8", "Co" + A, "\u2028" + A, A + "\n" + B, "ab", "aa",
  "\u017f" + "s", "sS"];
const unitPatterns = [
  "^\\-?.$", "\\-{0}^..$", "\\-{0}^.{2}$", "\\-{0}^.{4}$", "\\-{0}^.{3}$", "\\-{0}^[^a]{2}$", "^\\-?" + A + "*$", "^\\-?" + A + "+$",
  "^\\-?" + A + "?x$", "^\\-?" + A + "?a$", "^\\-?[" + A + "]$", "^\\-?[" + A + "]{2}$", "^\\-?[^" + A + "]$", "^\\-?[^" + A + "]{2}$",
  "\\-{0}\\udc00", "\\-{0}^\\udc00", "\\-{0}\\ud801", "\\-{0}\\ud801$", "\\-{0}^\\ud801\\udc00$", "\\-{0}^\\ud801\\udc00{2}$",
  "\\-{0}^(?:\\ud801\\udc00){2}$", "\\-{0}(?<=\\ud801)\\udc00", "\\-{0}(?<=\\udc00)\\ud801", "\\-{0}(?<=^.)\\udc00$",
  "\\-{0}(?<!\\ud801)\\udc00", "\\-{0}(?<=^..)$", "\\-{0}(?<=^.{3})a", "\\-{0}(?<=[^a]{2})a", "\\-{0}(?<=a.)[^a]",
  "\\-{0}^(.)(.)\\1\\2$", "\\-{0}^(.)\\1", "\\-{0}^(.).\\1", "\\-{0}^(..)\\1$", "\\-{0}^(.)(.)\\2", "\\-{0}(.)\\1",
  "\\-{0}^.(.).\\1$", "\\-{0}^..(?<=(.)\\1)", "\\-{0}(?<=\\1(.))$", "\\-{0}(?<=(.)\\1.)$", "\\-{0}^(?<n>.)\\k<n>",
  "(?i:\\-{0}^(.)\\1$)", "(?i:\\-{0}^(.).\\1)", "(?i:\\-{0}(?<=(.)\\1)$)", "(?i:\\-{0}^[^a]$)", "(?i:\\-{0}^\\W$)", "(?i:\\-{0}^k+$)",
  "(?i:\\-{0}^s+$)", "\\-{0}^.+?\\udc00$", "\\-{0}^[^]*?\\ud801.$", "\\-{0}^.*\\ud801", "\\-{0}^.*?\\udc28", "\\-{0}^.+\\udc00.",
  "\\-{0}\\B", "\\-{0}^.\\B.$", "\\-{0}^.\\b.$", "\\-{0}^\\W\\W$", "\\-{0}^\\W$", "\\-{0}^\\S{2}$", "\\-{0}^\\D{2,3}$",
  "\\-{0}^[\\ud800-\\udbff][\\udc00-\\udfff]$", "\\-{0}^[\\ud800-\\udbff]", "\\-{0}[\\udc00-\\udfff]$", "\\-{0}^[\\udc00-\\udfff]",
  "\\-{0}(?m:^.$)", "\\-{0}(?m:.$)", "\\-{0}(?s:^.$)", "\\-{0}a\\udc00", "\\-{0}\\udc00a", "\\-{0}a.{3}a", "\\-{0}a.{2}a", "\\-{0}a..a",
  "\\-{0}^\\ufffd", "\\-{0}\\ufffd$", "\\-{0}^[\\ufffd]+$", "\\-{0}^[^\\ufffd]+$", "\\-{0}\\u00e9.$", "\\-{0}^.\\u00e9$",
  "\\-{0}^(?:.{2})+$", "\\-{0}^(?:.|..){3}$", "\\-{0}^(?=.\\udc00).", "\\-{0}^(?!.\\udc00).", "\\-{0}(?=\\udc00)", "\\-{0}(?!.)(?<=\\udc28)",
  "\\-{0}^" + A + "$", "\\-{0}" + A + "{2}", "\\-{0}^" + A + B + "$", "\\-{0}^[" + A + B + "]{4}$", "\\-{0}^[" + A + B + "]{3}$",
  "\\-{0}^[" + A + "-\\udfff]+$", "^\\-?[" + A + "-\\udfff]$", "\\-{0}^[\\ud801-" + A + "]+$",
  "\\-{0}^[" + A + "-" + B + "]$", "\\-{0}[" + B + "-" + A + "]", "[" + A + "-" + B + "]", "^[" + A + "-" + B + "]$", "\\-[\\ud801\\udc00-\\ud801\\udc28]",
  "\\-{0}(?<" + A + ">a)\\k<" + A + ">", "(?<" + A + ">a)\\k<" + A + ">", "\\-{0}(?<\\ud801\\udc00>a)\\k<" + A + ">", "\\-{0}(?<\\u{10400}>.)\\k<" + A + ">$",
  "\\-{0}^\\u{2}$", "\\-{0}\\u{10400}", "\\-{0}^" + E + "?$", "\\-{0}^" + E + "{0}$",
];
const loopTexts = ["", "a", "x", "-", "ax", "xa", "aa", "xx", "a-", "-a", "xax", "aax", "xxa", "ab", "b", "aab", "abab", "aaa", "xxxxxxxxxxxx",
  "axaxaxaxaxax", "aaaaaaaaaaaa", "x-a-x-a-x-a-", "a-x", "-x", "x-"];
const loopPatterns = [
  "(?:((?=x)|()|){4,5}?){4}?$", "^(?:(?:$|\\b|(?:()|(|)){2,}(?=)){3})+$",
  "^((?:(?<!a)(|){4,}|-){4,5}(\\1){2,2}|(?=[^a]??[^a])){2}?x", "^([ax]\\B|){4}(?:(?:(?<=a)|\\B){4,4}(?<=a)|(?:(?=x)(?!$)|(|)|(?!$)){3,}){4,5}?x$",
  "(?:|(?<!(|a)|a())){3,3}(?:((?<!a)|(?=x)|){3}(?:)|x){4,6}a", "((?<!a)(?:(|){4}\\1|[^a]){3,}|\\b\\2){2,2}a$",
  "(?:-??-*?){1,}((?:(\\2){4}(?=x)|(|\\1){2,}){4,}(\\3x|-*?){0,}|($-*?|\\2\\b){2,3}?(?<=a)){1,2}a$",
  "(x|a(?=[ax]?|)|x?a){1,1}((?:(?!$)|(?:){2}?){4,}(?!(?<![^a]))|(?:(?:|\\1){1}-*){0,}?x?|\\2\\1){4,}x$",
  "(?:(?:x|((|)){4,}?\\1){4,6}(?:.(a|)){4,4}|((?=x)\\4|(?<!\\b|x)|a){3}?|)??(?:|(?<!(?!.)(?:[ax]-)??)$|x?){0,}x$",
  "(?:x|(?=x)(|$){3}?){1,}((|a)(|[ax]?){0}|()|(?:|\\B|(?:)){4,}(\\2|){3,}?){2,}?a",
  "^(?:((?:()|(?!$)){4,}?){3}()){4}?(a|(?:)){4,}a", "((?:)(|\\2|\\1\\2){3,}|[ax]){4}x$", "^((?:(?:)|(|){3,3}){4,}?|x){4}((?<=a)){0}$",
  // What a group holds after a repetition whose body cannot consume: the last iteration decides, and no iteration at
  // all when the minimum is zero.
  "^(?:(?=(a))|(?=(x))){2}\\1\\2", "^(?:(?=(a))|){3}\\1x", "^(?:(?=(a))|)*\\1x", "^(?:(?=(a))|)+\\1x", "^(?:(?=(a))){0,3}\\1x", "^(?:(?=(a)))?\\1a",
  "^(?:(?=(a))){1,3}\\1x", "^(?:(?=(a))){2}?\\1a$", "^(?:(?=(a))|(?=(x))|){2,}?(?:\\1\\2|x)$", "^(?:(?!(a))|(?=(a))){2}\\2a$",
  "^(?:(?:(?=(a))|(?=(x))){2}\\1|\\2.)+$", "^(?:(?=(.))(?:\\b|\\B){3}\\1)+$", "^(?:(?<=(a))|(?<!(a))){2}x\\1\\2$", "(?:^|(?<=(a))){2}\\1x",
  "^(?:(?:(a)|(x))(?:(?=\\1)|(?=\\2)|){2}){2}$", "(?:$|(?=(a))){3}\\1$", "^(?=(a)){2}\\1", "^a(?<=(a)){2}\\1", "^(?:\\b){2}a", "^(?:\\B)*a", "^(?:$)+",
  "(?<=(?:(?<=(a))|(?=(x))){2})\\1\\2", "^(?:(?:\\b|(?=(a)))+?\\1x)+$",
  // A backreference inside the group it names, alone and in a repetition.
  "^(a\\1)$", "^(a\\1)+$", "^(?:(a\\1b))+$", "^(\\1a){2}$", "^(?:(\\1|a\\1)){3}$", "^(?<n>a\\k<n>)+$", "^((a)\\1\\2)+$", "(?<=(\\1a))x", "(?<=(a\\1))x",
  "^(?:(a)|(\\2x))+$", "^(?:(?<n>a\\k<n>)|(?<n>x\\k<n>))+$", "^(a|\\1){4}$", "^(\\1){3}a$", "^((\\1|\\2|a){2}){2}$", "^(a(?=\\1)|x(?!\\1))+$",
];
const fastTexts = ["", "a", "-", "ab", "abc", "abcd", A, E, "a" + A, A + "a", "-" + A, E + ".8", "Co" + A, "a" + E + "b", "a," + A, A + "," + A, "x-" + A,
  "\u00e9", "a\u00e9b", E + E, "a,b", "é," + A];
const fastPatterns = [
  "^\\-?.$", "^[^\\%]{1,3}$", "^.\\&?$", "^[^a-z]\\&?$", "^\\W\\&?$", "^[\\W]\\&?$", "^[^k]\\&?$", "^.{2}\\-?$", "^\\-?[^,]+$", "^[^\\@]+(,[^\\@]+)*$",
  "^[^\\#]{2,4}$", "^a.\\-?b$", "^\\-?" + A + "$", "^\\-" + A + "?$", "^\\-?" + A + "+$", "^(\\-|" + A + ")$", "^a\\-?|" + A + "{2}$", "^[^\\-]$", "^[^\\-][^\\-]$",
  "^x\\-", "^\\_?.{1,2}$", "^.{3}\\_?$", "\\-?.{3}", "^\\-?[^a]$", "^[a\\-]?.$",
];
const out = {
  node: process.version,
  units: unitPatterns.map((p) => ask(p, unitTexts)),
  loops: loopPatterns.map((p) => ask(p, loopTexts)),
  fast: fastPatterns.map((p) => ask(p, fastTexts)),
};
process.stdout.write(JSON.stringify(out));
