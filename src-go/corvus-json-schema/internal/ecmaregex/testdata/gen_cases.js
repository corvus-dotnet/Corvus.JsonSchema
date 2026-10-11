// Writes v8_cases.json, the expectations of the tests of matching by UTF-16 code unit, of repetitions that cannot
// consume a character and of what is a pattern (cases_test.go here, pattern_test.go of the module, and
// TestEcmaRegex.pas of the Object Pascal port). Run it with "node gen_cases.js > v8_cases.json". It needs Node 24 or later. The tests do not run it. They
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
//   - needs: counted loops whose iterations can be empty, before a rest of the pattern that needs more text than is
//     left. The first three do not finish unless the matcher gives up at such a loop, as V8 does. The others have
//     such loops inside and around lookaheads and lookbehinds, where a wrong count of what is left would show.
//   - quick: choices, loops and lookarounds where the next character of the text rules a way out, which the matcher
//     looks at before it takes the way (setQuick). The first three took the matcher seconds before it did. The
//     others have each thing a way can end with or go through without a character: the end of the text and of a
//     line, a lookahead and a lookbehind, a backreference, a word boundary, an empty alternative.
//   - memo: repetitions inside repetitions, alternatives that are prefixes of each other and repetitions that follow
//     each other, with no backreference, in patterns the backtracking matcher runs. The matcher remembers the places
//     where a way has failed (see memo in vm.go). The tests run these, and every other set, with that memory on from
//     the first step. The texts are short, because V8 remembers nothing and takes time that doubles with each
//     character for some of them.
//   - classK: \k in a class, which is the letter k only in a pattern with no named group and no u flag.
//   - counts: the two counts of a quantifier, which are compared as the numbers written, whatever their size.
//   - properties: the names and the values of \p{...} where V8 and ECMA-262 differ.
//   - names: a group name used twice, which is valid only in separate alternatives of one disjunction.
//
// The answers are V8's, except in the places where V8 13.6 departs from ECMA-262. There a case has ECMA-262's
// answer, and "ecma262" names the departure (see departure below).
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
const needsPatterns = [
  "(?:\\1|[^a]|$(?:a?|(?<!^)){4}?){8,9}?(\\b|a)+?a$", "(?:\\1|x|$(?:x?|(?<!^)){5}){7,}(\\B|-)+?-$",
  "(?:(?:-|$(?:(?<!^)|x?){5})|\\1){6,}(a)x$",
  "(?:\\1|[^a]|$(?:a?|(?<!^)){4}?){4,6}?(\\b|a)+?a$",
  "(?:(?:a|x|){2,}){5}(?=(?:a|){3}x)ax", "(?:(?<=(a|)\\1)|[ax]|$(?:|\\b|\\B|a?){6}){6}(?:a|x)$",
  "^(?:a|){2,3}(?<=(?:a|){2,3}x?)$", "(?<=(?:x|a|){3,4}a)(?:-|){2,}$", "(?!(?:a|){2}xx)(?:x|){2}x", "(?=(?:a|){3}x)(?:a|x|){2}",
  "^(?:(?=(?:a|){2}(x))|a){2,}\\1", "(?<!(?:(?:a|){2}x){2})-$", "^(?:(a)|){2,}\\1\\1$", "(?:(?:a|){2}(?=(?:x|){2}-)){2}x",
  "(?<=(?=(?:x|){3}a)(?:a|x|){2})a$", "^(?:(?:a|){2}){2}$", "^(?:(?:a|){2,}?x){2}$", "(?:a|){3}(?<=^(?:a|){3})xx", "(?:(?:x|){2}(?!(?:a|){2}-)){2}a$",
  "^(?:(?:a|x|-|){3}){3}(?<=(?:a|){2}x)-", "(?:(?:a|){2}\\b){2}x-", "^(?:(?:(a)|){2}\\1){2}x$",
];
const quickTexts = (() => {
  const texts = [""];
  for (const a of ["a", "b", "x"]) {
    texts.push(a);
    for (const b of ["a", "b", "x"]) {
      texts.push(a + b);
      for (const c of ["a", "b", "x"]) texts.push(a + b + c);
    }
  }
  return texts.concat(["\n", "a\n", "\nb", "a\nb", "xa\nb", "ab\nx", "x\n", "\u00e9", "a\u00e9", "\u00e9x", "x\u00e9a", A, A + "a", "x" + A + "a", "a" + A, "k", "K", "\u212a",
    "ka", "Kx", "\u212aa", "ax", "AX", "bX", "-", "a-", "-a", "a b", "x a", "abxabx", "aaxaax", "bxbxbx", "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaab",
    "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaax", "xaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", "aaaaaaaaaaaaaaaxaaaaaaaaaaaaaaa", "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbxa", "abababababababababababababababx"]);
})();
const quickPatterns = [
  ".*(?=((?:a*){7})$)x\\1", ".*(?=((a*)(?:(a|)){2,8}(?:a?\\2){0,}?)$)x\\1x", ".*(?:(?=((?:(a|)(b|)){3,}?)x|(?=x))x\\1){2}",
  // The end of the text, and of a line.
  "(?:a|$)", "(?:$|a)b?", "^(?:a|b$|$)", "(?:x$|x)a", "(?:a|)$", "(?=$)|a", "(?:(?=a$)|(?=b))[ab]", "(?:a|b)$|x", "^(?:a|$)(?:b|$)",
  "(?m:(?:a|$)\\n?b)", "(?m:^(?:x|$))", "(?m:(?:$|a)$)", "(?m:(?:a|$)(?:\\n|x))", "(?m:^(?:b|^)x)",
  // Lookbehinds, whose bodies are matched right to left.
  "(?<=a|^)b", "(?<=(?:a|b)x?)a", "(?<!a|b)x", "(?<=(?=b)a?)b", "(?<=^|x)(?:a|b)", "(?<=(?:a|x)b)(?:a|x)", "(?<=a(?:b|))x", "(?<!(?:a|b)(?:a|x))(?:b|x)$",
  // Lookaheads: the body and what follows start at the same place.
  "(?=a)(?:a|b)", "(?=[ab])(?:b|x)", "(?=a|$)(?:a|)$", "(?!a)(?:a|b)", "(?=(?:a|b)*x)a", "(?=.*x)(?:a|b)+", "(?=a)", "(?=)a", "(?!)|a", "(?=a)(?=b)|x",
  "(?=a)(?!ab)(?:a|x)", "(?:(?=a)|(?=b))(?:b|x)", "(?=(?=a)[ab])(?:a|b)x", "(?!(?=a)a|b).", "(?=a*$)(?:a|b)", "(?=(a)|b)(?:\\1|b)x",
  // Backreferences, which may consume anything or nothing.
  "(a|b)\\1", "(a|)\\1(?:b|x)", "(?:(a)|b)(?:\\1|x)", "(a?)(?:\\1b|x)", "(?:(a)|(b))(?:\\1\\2|x)a", "(a|b)(?=\\1)(?:a|b)",
  // Loops.
  "(?:a|b)*x", "(?:a?b?)*x", "(?:a|){2,3}x", "(?:(?:a|b)x?){2,}?b", "a*?(?:b|x)", "a*(?:a|b)", "[ab]*?a{2}", "(?:a|b)+?(?:x|$)",
  "^a*b*x*$", "^a*?b+?x?$", "a{2,}?(?:a|b)x", "^(?:a|b){2}(?:x|$)", "(?:a+|b+)*x", "^[^x]*(?:a|x)$", "^.*?(?:ab|bx)", "(?:[ab]x?)*?(?:aa|bb)",
  // Assertions that consume nothing.
  "(?:\\b|a)x", "(?:\\B|b)a", "(?:^|a)b", "(?:a|^)(?:b|^)x", "\\b(?:a|x)\\b", "(?:\\b|\\B)(?:a|b)(?:\\b|x)", "(?i:(?:\\B|\\u212a)a)",
  // Characters that are not ASCII, in each grammar, and case-insensitive ways.
  "(?:\\u00e9|a)x", "(?:[^a]|b)\\u00e9", "(?:\\u{10400}|x)a", "\\-?(?:\\ud801|a)\\udc00?a", "\\-?(?:.|x)a$", "(?i:(?:a|B)x)", "(?i:[^k]|ka)$",
  // A way that can never be taken, and one that can always.
  "(?:[]|a)b", "(?:a|[^])b", "(?:a|[]|)x", "(?:|a)(?:|b)x",
];
const memoTexts = ["", "a", "b", "x", "ab", "ba", "aa", "ax", "xa", "aab", "aax", "abx", "aaaa", "aaab", "aaax", "abab", "abax", "aaaaaaaa", "aaaaaaab", "aaaaaaax",
  "abababab", "abababax", "bbbbbbba", "aaaaaaaaaaab", "aaaaaaaaaaax", "aaaaaaaaaaaa", "abababababab", "ababababababx", "xaaaaaaaaaaa", "bababababab",
  "ab ab ab!", "ab ab ab", "a b\nab a", "ab\nab ab!", "\u00e9", "a\u00e9a", "aaaa\u00e9", "\u00e9aaaaaaab", "aaaaaaab!", "ab ab ab ab"];
const memoPatterns = [
  // A repetition in a repetition, an alternation of prefixes, and a body of optional characters, behind a lookahead.
  "^(?=.)(?:a+)+$", "^(?=.)(?:a|aa)+$", "^(?=.)(?:a?b?)*x", "(?=a)(a*)*b", "^(?=.)(?:a*)*$", "(?=.)(?:a|b)*x", "^(?=.)(?:a+?)+?$", "^(?=.)(?:a|ab|b)*?x$",
  "^(?=.)(?:a|b|ab)*$", "(?=.)(?:a+b?)+x", "^(?=.)(?:(?:a|b)+x?)+$", "(?=[ab])(?:a*b*)*x", "(?=.)(?:a*|b)*x",
  // Repetitions whose bodies can be empty, one inside another, greedy and lazy, with a least count of none and of one.
  "^(?=.)(?:(?:a|b)*x?)*$", "^(?=.)(?:(?:a?b?)*x?)*$", "^(?=.)(?:(?:a*)+b?)+$", "(?=.)(?:a*?b*?)*?x$", "^(?=.)(?:(?:a?)+?b?)*?x", "^(?=.)(?:(?:(?:a?)*b?)*x?)*$",
  "^(?=.)(?:(?:a|)+(?:b|)+)+$", "^(?=.)(?:(?:a*b?)+|x)+$", "(?=.)(?:(?:a?|b)*?x?)+?$",
  // Counted repetitions, which the memory leaves alone, beside ones it does not.
  "^(?=.)(?:(?:a|aa){2,3}b?)*$", "^(?=.)(?:a{0,2}){0,5}(?:a+)+$", "^(?=.)(?:(?:a+)+b){1,2}$", "^(?=.)(?:(?:a|){2}b?)*$", "^(?=.)(?:a?){3}(?:a|aa)+$",
  // Lookarounds inside and around the repetitions, and negative ones.
  "^(?=.)(?:a(?=a|b|$)|b)*$", "^(?=.)(?:(?<=a)b|a)*$", "^(?!(?:a+)+x)a*b", "(?<=(?:a+)+)x", "(?<!(?:a|aa)+b)x$", "^(?:(?=(?:a+)+b)a|(?=(?:a|aa)+x)a)+", "^(?=.)(?:a(?!x)|b)*a",
  "(?=(?:a|aa)*$)(?=(?:a+)+$)a", "^(?=.)(?:(?=(?:a*)*b)a)*a?b$", "(?<=^(?:a|aa)*)b", "(?<=(?:a?b?)*)x$",
  // Single character repetitions that follow each other.
  "(?=.).*.*.*x", "(?=.)a*?a*?a*?b", "(?=.)[ab]*a[ab]*a[ab]*x", "^(?=.)a*a*a*a*$", "(?=.)a+?a+a+?b", "^(?=.)[^x]*[^b]*[^a]*$", "(?=.)a*(?:a|b)a*x",
  // Multiline anchors and case-insensitive word boundaries keep a pattern on the backtracking matcher.
  "(?i:\\b(?:\\w+\\W?)+\\b!)", "(?m:^(?:\\w+\\s?)+$)", "(?m:^(?:a|ab|b)+$)", "(?i:\\b(?:a|aa)+\\b ?b)", "(?m:^(?:a?b?)*$)x?",
  // A backreference turns the memory off for the pattern.
  "^(?:(a+)\\1?)+$", "^(?=.)(?:(a)|b)+\\1?x$", "(a*)*\\1b",
  // Characters that are not ASCII.
  "^(?:\\u00e9|a)+$", "^(?=.)(?:[^b]+)+$", "^(?=.)(?:a|\\u00e9a?)*b",
];
const syntaxTexts = ["", "a", "k", "kk", "aa", "aaa", "ak", "<a>", "x", "ax", "ab", "b", "-", "z", "p{WSpace}", " ", "\u00a0", "\u3042", "\u30ab", "\u30fc",
  "\u3042a", "p{sc=Hrkt}", "P{sc=Hrkt}", A, "aaaaaaaaaaaa"];
// \k in a class. It is the letter k only in a pattern with no named group and no u flag.
const classKPatterns = [
  "[\\k]", "^[\\k]+$", "^[\\k<a>]+$", "[^\\k]", "(a)[\\k]", "(?:a)[\\k]", "(?<=a)[\\k]", "(?<!a)[\\k]", "[(?<n>][\\k]", "\\(?<n>[\\k]", "[\\k-z]", "[a-\\k]",
  "(?<n>a)[\\k]", "[\\k](?<n>a)", "(?<n>a)[\\k<n>]", "[\\k<n>](?<n>a)", "(?<n>a)[^\\k]", "(?<n>a)[\\k-z]", "(?<n>a)[a-\\k]", "(?<n>a)[\\d-\\k]", "(?<n>a)|[\\k]",
  "(?<n>a)\\k<n>[k]", "(?<n>a)[k]", "\\k", "\\k<n>", "(?<n>a)\\k", "(?<n>a)\\k<n>", "(?<n>a)[\\b]", "(?<n>a)[\\-]", "(?<n>a)[\\a]", "(?<n>a)[\\c]", "(?<n>a)[\\_]",
];
// The counts of a quantifier are compared as the numbers written, whatever their size.
const countPatterns = [
  "a{2,1}", "a{1,2}", "^a{2,2}$", "a{10,9}", "a{9,10}", "a{010,9}", "a{9,010}", "a{2,00000000000000000000001}", "a{00000000000000000000001,2}", "\\-a{2,1}", "{2,1}",
  "a{2147483647,2147483647}", "a{2147483647,2147483648}", "a{2147483648,2147483647}", "a{2147483648,2147483648}", "a{2147483649,2147483648}",
  "a{4294967295,4294967296}", "a{4294967296,4294967295}", "a{4294967297,4294967296}", "a{99999999999999999999,1}", "a{1,99999999999999999999}",
  "a{99999999999999999999,99999999999999999998}", "a{99999999999999999998,99999999999999999999}", "a{99999999999999999999,99999999999999999999}",
  "a{100000000000000000000,99999999999999999999}", "a{99999999999999999999,100000000000000000000}", "^a{0,99999999999999999999}$", "^a{2,2147483648}$",
  "^a{3,}?$", "(?:a{2147483648,2147483647})", "(?=a{2147483648,2147483647})", "(?<=a{2147483648,2147483647})", "[a{2147483648,2147483647}]", "\\-{2147483648,2147483647}",
  "a{2147483648,2147483647}?", "(a){2147483648,2147483647}", "[a]{2147483648,2147483647}", "\\d{2147483648,2147483647}", "a{2147483648,2147483647", "a{2147483648,2147483647,}",
];
// The names and the values of \p{...}.
const propertyPatterns = [
  "\\p{WSpace}", "\\P{WSpace}", "[\\p{WSpace}]", "^\\p{White_Space}$", "^\\p{space}$", "\\p{sc=Hrkt}", "\\P{sc=Hrkt}", "^\\P{sc=Hrkt}$", "^[\\p{sc=Hrkt}]$", "^[^\\p{sc=Hrkt}]$",
  "^[\\P{sc=Hrkt}]$", "^[^\\P{sc=Hrkt}]$", "^[\\p{sc=Hrkt}a]+$", "^[^\\p{sc=Hrkt}a]+$", "\\p{Script=Hrkt}", "\\p{scx=Hrkt}", "\\p{Script_Extensions=Hrkt}",
  "\\p{sc=Katakana_Or_Hiragana}", "\\P{Script=Katakana_Or_Hiragana}", "^\\P{scx=Katakana_Or_Hiragana}$", "\\p{Script_Extensions=Katakana_Or_Hiragana}",
  "(?i:\\p{sc=Hrkt})", "(?i:^\\P{sc=Hrkt}$)", "(?i:^[^\\p{sc=Hrkt}]$)", "^\\p{sc=Hrkt}*$", "^(?:\\p{sc=Hrkt}|a)+$", "\\p{Hrkt}", "\\p{gc=Hrkt}", "\\p{sc=hrkt}",
  "\\p{sc=Katakana_or_Hiragana}", "^\\p{sc=Hira}$", "^\\p{sc=Kana}$", "^\\p{scx=Hira}$", "^\\p{scx=Kana}$",
];
// A group name used twice. The two groups must be in separate alternatives of one disjunction.
const namePatterns = [
  "(?<m>a)(?<m>b)", "(?<m>a)|(?<m>b)", "(?:(?<m>a)|(?<m>b))\\k<m>", "(?:(?<m>a)|b)(?<m>c)", "((?<m>a))|(?<m>b)", "(?<m>a|(?<m>b))", "(?<m>a)(?:x|(?<m>b))",
  "(?:(?<m>a)|(?<m>b))(?:(?<m>c)|d)", "(?:x|((?<m>a))(?<m>b))", "((?<m>a)|x)(?<m>b)", "(?:x|(?<m>a)(?<m>b))", "(x|((?<m>))(?<m>))", "(|((?<m>))(?<m>|))",
  "(|(?:(?<m>))(?<m>|))", "(|(?=(?<m>))(?<m>|))", "(|((?<m>))(?<m>))", "(((?<m>))(?<m>|))", "|((?<m>))(?<m>|)", "(?<m>)|(?<m>)(?<m>)", "(?<m>a)|(?:(?<m>b)|(?<m>k))",
  "(?:(?<m>a)|x)|(?:b(?<m>k))", "(?=(?<m>a))|(?!(?<m>b))", "^(?:(?<m>a)|(?<m>b)|(?<m>k))\\k<m>$", "(?<m>a)|(?<m>b)(?<m>k)", "(?:(?<m>a)|(?<n>b))(?:(?<n>a)|(?<m>k))",
  "(?<=(?<m>a)|(?<m>b))k", "(?<=(?<m>a)(?<m>b))k", "(?:(?<m>a)|(?:(?<m>b)|(?<m>k)))\\k<m>",
];
// departure names the place where V8 13.6 departs from ECMA-262 for a pattern, and gives ECMA-262's answer.
//   - count-order: V8 keeps a count as a number that stops at 2^31 - 1, so it takes a{2147483648,2147483647}. A
//     first count greater than the second is an error at any size (22.2.1.1 Early Errors of QuantifierPrefix).
//   - duplicate-name: V8 takes some patterns with one name on two groups that can both take part in a match, such as
//     (?<m>a)(?:x|(?<m>b)). The two must be in separate alternatives of one disjunction (MightBothParticipate).
//   - property-name: V8 takes \p{WSpace}. Table 66 of ECMA-262 has White_Space and space only, so with the u flag it
//     is an error, and the pattern is read with no flag.
//   - script-value: V8 refuses the Script value Katakana_Or_Hiragana (Hrkt), which PropertyValueAliases.txt has. No
//     character has it, so the answers are those of the same pattern with \P{Any} for \p and \p{Any} for \P.
const duplicateNames = new Set(["(?<m>a)(?:x|(?<m>b))", "(|((?<m>))(?<m>|))", "(|(?:(?<m>))(?<m>|))", "(|(?=(?<m>))(?<m>|))"]);
const HRKT = /\\([pP])\{(?:Script|sc|Script_Extensions|scx)=(?:Hrkt|Katakana_Or_Hiragana)\}/g;
function departure(p) {
  const v8 = ask(p, syntaxTexts);
  const notValid = (why) => {
    if (!v8.l) throw new Error("V8 does not take " + p);
    return { p, u: false, l: false, tests: [], ecma262: why };
  };
  if (duplicateNames.has(p)) return notValid("duplicate-name");
  // A count in a class is not a count.
  for (const m of p.replace(/\[[^\]]*\]/g, "[]").matchAll(/\{(\d+),(\d+)\}/g)) {
    if (BigInt(m[1]) > BigInt(m[2]) && (v8.u || v8.l)) return notValid("count-order");
  }
  if (/\\[pP]\{WSpace\}/.test(p)) {
    if (!v8.u) throw new Error("V8 does not take " + p);
    const l = new RegExp(p);
    return { p, u: false, l: true, tests: syntaxTexts.map((t) => ({ t, want: l.test(t) })), ecma262: "property-name" };
  }
  if (p.search(HRKT) >= 0) {
    if (v8.u) throw new Error("V8 takes " + p);
    const standIn = ask(p.replace(HRKT, (m, c) => (c === "p" ? "\\P{Any}" : "\\p{Any}")), syntaxTexts);
    return { p, u: standIn.u, l: standIn.l, tests: standIn.tests, ecma262: "script-value" };
  }
  return v8;
}
const out = {
  node: process.version,
  units: unitPatterns.map((p) => ask(p, unitTexts)),
  loops: loopPatterns.map((p) => ask(p, loopTexts)),
  fast: fastPatterns.map((p) => ask(p, fastTexts)),
  needs: needsPatterns.map((p) => ask(p, loopTexts)),
  quick: quickPatterns.map((p) => ask(p, quickTexts)),
  memo: memoPatterns.map((p) => ask(p, memoTexts)),
  classK: classKPatterns.map(departure),
  counts: countPatterns.map(departure),
  properties: propertyPatterns.map(departure),
  names: namePatterns.map(departure),
};
process.stdout.write(JSON.stringify(out));
