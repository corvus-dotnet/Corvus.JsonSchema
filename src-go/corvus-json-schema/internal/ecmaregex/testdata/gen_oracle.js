// Writes v8_oracle.json, the expectations of oracle_test.go, by asking V8 what each pattern does. Run it with
// "node gen_oracle.js > v8_oracle.json". It needs Node 24 or later, whose V8 has the ECMAScript 2025 additions
// (modifier groups and duplicate group names). The tests do not run it. They read its output.
//
// A pattern is read with the u flag when that is valid and with no flag otherwise, which is what Compile does. V8
// matches UTF-16 code units for a pattern with no flag, and this package matches code points, so a pattern that is
// only valid with no flag is not asked about texts that have a character beyond the Basic Multilingual Plane.
//
// V8 13.6 answers some case-insensitive modifier groups differently from the same pattern with the i flag, which the
// specification does not allow. oracle_test.go lists those patterns and takes only their validity from here. The
// "flagged" patterns cover the same behaviour through the flags.
'use strict';

// The hand-written patterns of the Java port's EcmaRegexValidatorTest.
const javaHand = [
  "", "a", "^a$", "a|b", "(a)", "(?:a)", "(?=a)", "(?!a)", "(?<=a)", "(?<!a)", "(?<n>a)\\k<n>", "\\k<n>",
  "(?<n>a)(?<n>b)", "(?<$x_1>a)", "(?<1a>a)", "a{2}", "a{2,}", "a{2,3}", "a{3,2}", "a{", "a{,2}", "{", "}", "]",
  "[", "[]", "[^]", "[a-z]", "[z-a]", "[\\d-z]", "[a-\\d]", "[\\b]", "[\\-]", "\\-", "\\a", "\\c", "\\cA", "\\c1",
  "\\0", "\\01", "\\1", "(a)\\1", "(a)\\2", "\\x4", "\\x41", "\\u004", "\\u0041", "\\u{1F600}", "\\u{110000}",
  "\\ud83d\\ude00", "[\\ud83d\\ude00-\\ud83d\\ude4f]", "\\p{L}", "\\p{Letter}", "\\p{digit}", "\\p{Nope}",
  "\\p{gc=Lu}", "\\p{Script=Greek}", "\\p{sc=Grek}", "\\p{Script=Nope}", "\\P{ASCII}", "\\p", "\\p{",
  "a**", "a+?", "*a", "(?=a)*", "^*", "$+", "\\b+", "a)", "(a", "(?a)", "\\/", "\\.", "a\\", "[a", "x{1}{2}",
  "\\w+@\\w+\\.\\w+", "^[a-z][a-z0-9_]*$", "(?<a>.)\\k<a>", "[\\p{L}\\d]", "\\s\\S\\w\\W\\d\\D",
];

// The patterns of the Rust port's pattern tests.
const rustPatterns = [
  "",
  ".*",
  "^.*",
  ".*$",
  "[\\s\\S]*",
  "^[\\s\\S]*",
  "^[\\s\\S]*$",
  "^.*$",
  "^[@$_#]",
  "^[a-zA-Z0-9_\\.\\-\\|@#]*$",
  "^[a-zA-Z0-9_\\-]*$",
  ".+",
  "^\\{\\{[^\\W\\.\\-][\\w\\.\\-]*\\}\\}$",
  "^.{1,256}$",
  "^[A-Z0-9_\\-\\/]+$",
  "^[a-zA-Z0-9_\\.\\-]+[\\|]?[a-zA-Z0-9_\\.\\-]+$",
  "^([a-zA-Z_$][a-zA-Z0-9_$]{0,39}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,39})$",
  "^x-",
  "^[1-5](?:[0-9]{2}|XX)$",
  "(base64key|awskms)://(.*)",
  "^[A-F0-9]{1,32}$",
  "^[a-z][a-z0-9]{0,29}$",
  "^([t|T][o|O][p|P])|([c|C][e|E][n|N][t|T][e|E][r|R])$",
  "^[\\w\\*]{0,60}$",
  "^(?:@[0-9a-z-_.]+\\/)?[a-z][0-9a-z-_.]*$",
  "^[a-z][a-z0-9_]+$",
  "^\\d+[:-]\\d+$",
  "^#[0-9a-fA-F]{6}$",
  "^[^:]+:[^:]+$",
  "^(?=[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
  "^.*\\.(?:txt|trie)(?:\\.gz)?$",
  "^([-\\w_\\s]+)(,[-\\w_\\s]+)*$",
  "^(!?[-\\w_\\s]+)|(\\*)$",
  "^[0-9]+(ns|ms|us|µs|s|m|h)$",
  "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$",
  "^[^- @#$%^&()!]+$",
  "^((\\.(?!\\.)\\/)?\\w+\\/?)+$",
  "^[0-9]{1,}.[0-9]{1,}.[0-9]{1,}$",
  "\\{.*\\}",
  "^[a-z]{1,2}$",
  "^abc$",
  "^\\/",
  "^es$",
  "^(0|[1-9]\\d*)\\.(0|[1-9]\\d*)\\.(0|[1-9]\\d*)(?:-((?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\\.(?:0|[1-9]\\d*|\\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\\+([0-9a-zA-Z-]+(?:\\.[0-9a-zA-Z-]+)*))?$",
  "^[Ee][Ss]5|[Ee][Ss]6|[Ee][Ss]7$",
  "^[a-z]*a$",
  "^a*a",
  "^a+b?a",
  "^a+b?c*a$",
  "^[a-z]+-?[a-z]+$",
  "\\bfoo",
  "^\\p{L}+$",
  "\\u00e9",
  "\\ud83d\\ude00",
  "[^\\d]x",
  "^\\S+$",
  "a{2}b{1,}c{0,3}",
  "(?<name>ab)+",
  "^[\\b]",
  "\\x41",
  ".",
  "^.",
  "^.+",
  "(.*)",
  "^(.*)",
  "^.+$",
  "^.{1,3}$",
  "^.{2}$",
  "^.{2,}$",
  "^(ab|cd)$",
  "^(?:es|ES|x-|a\\.b)$",
  "^(a|b|c|d|e|f|g|h|i|j|z)$",
  "^ab|cd$",
  "^x-|es|ms$",
  "a|b",
  "^a\\$|b",
  "\\Bs",
  "a\\b",
  "^\\p{Lu}",
  "[\\p{L}\\d]+$",
  "^\\P{L}+$",
  "^(?=[^a-c\\n]+$)(?=(.*\\w)).+$",
  "^\\-a",
  "[{}[\\]]",
  "(.+)",
  "^(.+)$",
  "^(.*)$",
  "^a(bc)?$",
  "^(a|b)c|d(e|f)$",
  "^([a|A][u|U][t|T][o|O])|([n|N][o|O][n|N][e|E])$",
  "^[Ee][Ss]2015(\\.([Cc][Oo][Rr][Ee]|[Pp][Rr][Oo][Xx][Yy]))?$",
  "^[Ee][Ss]([356]|20(1[567]|2[02])|[Nn][Ee][Xx][Tt])$",
  "^([a-z]+|x)-$",
  "^a|[0-9]{2}$",
  "^(a|b)*$",
  "^(?=a)a|b$",
  "^/.*",
  "^a.*",
  "^a\\\\.*",
  "(^([0-9]+)\\.([0-9]+)$)|(^\\{[A-F0-9]{2}(-[A-F0-9]{1}){2}\\}$)",
  "^[0-9]{1,}.[0-9]{1,}$",
  "^3\\.1\\.\\d+(-.+)?$",
  "^([A-Za-z_][-A-Za-z0-9_.:]*)$",
  "^a.c$",
  "^[a-z].$",
  "x.y|^z",
  "^es|ms|x-$",
  "^(ab){2}$",
  "^(a|b){2}c$",
  "^([a-zA-Z0-9]{2,3})(-[a-zA-Z0-9]{1,6})*$",
  "^([a-z][a-z0-9]{0,3})(\\.[a-z][a-z0-9]{0,3})*$",
  "^([a-z_$][a-z0-9_$]{0,3}\\.)*([a-zA-Z_$][a-zA-Z0-9_$]{0,3})$",
  "^([a-z]+)(,[a-z]+)+$",
  "^(a,)*b$",
  "^(ab,)+a$",
  "^a(,a)*$",
  "^[a-z]*(-[a-z]*)*$",
  "^(a-)*a-b$",
  "^(é,)*a$",
  "^(?=!+[^!*,;{}[\\]~\\n]+$)(?=(.*\\w)).+$",
  "^(?=!+[^a]+$)(?=(.*\\w)).+$",
  "^\\/[^\\*\\?\\&\\%]*(\\/\\*)?$",
  "^[\\&\\@\\_]+$",
  "a\\&.b",
  "^.{2}$",
  "^[^\\%]{1,3}$",
  "\\uD83D\\uDE00|\\&",
];

// Patterns that exercise the backtracking matcher and the corners of the grammar.
const extra = [
  "(a)\\1", "^(?!foo)\\w+$", "(?<=\\$)\\d+", "(?<!\\$)\\b\\d+", "^(a|ab)(c|bcd)(d*)$", "^(?:(a)|b)*\\1$", "\\1(a)",
  "^(a\\1)$", "^(?<n>.)\\k<n>$", "^(?=.*\\d)(?=.*[a-z]).{4,}$", "^(a*)*$", "^(?:a|b)*?c$", "^a{2,3}?$",
  "^(ab){2,3}$", "(?<=(\\d+)(\\d+))$", "^(?<=^)a", "(?<=\\1(a))b", "(?<=(a)\\1)b", "^.$", "^[^a]$", "^\\u{1F600}$",
  "^[\\ud83d\\ude00-\\ud83d\\ude4f]+$", "^\\p{Script=Greek}+$", "^\\p{Lu}", "^(?=a)*a$", "^(?:(?=(a)))?\\1b",
  "^[]$", "^[^]$", "^(?:(?=(a)))?\\1a", "(?!(a))\\1b", "(?=(a))\\1", "^(?:a|ab)+?b$", "^(a+)+b", "(a|b)*\\1c",
  "^(?:(a)|(b))+\\1\\2$", "^(a)?\\1b$", "^(?:(a)|b){2}\\1$", "^(?:(a)|b){2,}?\\1$", "(?<!a)b", "(?<=a|bc)x",
  "(?<=(?:ab)+)x", "(?<=a{2})b", "(?<=^a*)b", "(?<!^a*)b", "(?<=\\b)a", "\\Ba\\B", "\\ba\\b", "(?<=(?=b)a)", "(?=a(?<=a))a",
  "(?<=a(?=b))b", "^(?:a?){3}$", "^(?:a?){3,5}b$", "^(?:a*)+$", "^(?:a*)+?b$", "^(?:|a)+b$", "^(?:a|){2,}b$",
  "(?:a|ab)(?:c|bcd)(?:d*)$", "^(.+)\\1$", "^(.+?)\\1+$", "^(?:(\\w)(?!\\1))+$", "^(?:(\\w)(?=\\1))+", "(\\d)(?<=\\1\\1)",
  "^((a)|(b))+$", "^(?:(a)\\1|b)+$", "^a.b$", "^a[\\s\\S]b$", "^\\s+$", "^\\S+$", "^\\D+$", "^\\W+$", "^[\\w-]+$",
  "^[^\\W\\d]+$", "^[\\D\\d]$", "^[^\\D\\d]$", "^\\cJ$", "^[\\cJ]$", "^\\x0a$", "^\\0$", "\\$", "^a$|^b$", "^(?:^a|b$)$",
  "a$b", "a^b", "$^", "^$", "(?:)", "()", "(|a)", "a||b", "|", "^(?:a{0})b", "^a{0,0}b", "^a{1}$", "^a{1,}?b",
  "^.{3}$", "^.{2,3}?$", "^[a-c]{2}[^a-c]{2}$", "^(?:ab|a)(?:bc|c)$", "^(a)(b)?\\2c$", "^\\p{Alphabetic}+$",
  "^\\p{Lowercase}+$", "^\\p{Uppercase}+$", "^\\p{White_Space}+$", "^\\p{ASCII}+$", "^\\P{ASCII}+$", "^\\p{Any}$",
  "^\\p{Assigned}+$", "^\\p{ID_Start}\\p{ID_Continue}*$", "^\\p{Emoji}$", "^\\p{Emoji_Presentation}$",
  "^\\p{Extended_Pictographic}$", "^\\p{scx=Grek}+$", "^\\p{Script_Extensions=Latin}+$", "^\\p{sc=Latn}+$",
  "^\\p{General_Category=Decimal_Number}+$", "^\\p{Nd}+$", "^\\p{P}+$", "^\\p{punct}+$", "^\\p{Sm}$", "^\\p{Math}$",
  "^\\p{Hex_Digit}+$", "^\\p{AHex}+$", "^[\\p{Lu}\\p{Nd}]+$", "^[^\\p{L}]+$", "^[\\P{L}a]+$", "\\p{Lu}{2}", "\\p{Cased}",
  "\\p{Case_Ignorable}", "\\p{CWL}", "\\p{CWU}", "\\p{Dash}", "\\p{Diacritic}", "\\p{Grapheme_Base}", "\\p{XIDS}",
  "\\p{Bidi_M}", "\\p{Zs}", "\\p{Cc}", "\\p{Other}", "\\p{Symbol}", "\\p{Sc}", "\\p{Lowercase_Letter}", "\\p{LC}",
  "\\p{L=x}", "\\p{gc=Greek}", "\\p{sc=L}", "\\p{}", "\\p{Lu", "\\pL", "\\P{Any}", "\\p{Script=Zyyy}", "\\p{sc=Zinh}",
  "\\k", "\\k<", "\\k<a", "(?<a>)\\k<b>", "(?<a>\\k<a>)", "\\k<a>(?<a>x)", "(?<a>a)|(?<b>b)\\k<a>", "(?<a b>a)",
  "(?<>a)", "(?<a", "(?<=a)+", "(?<!a)*", "(?!a)+", "(?=a){2}", "(?=a)?b", "a{1", "a{1,", "a{1,2", "a{1,2}{3}",
  "a{2147483648}", "a{0,2147483648}", "a{99999999999999999999}", "a{1,1}", "a{,}", "a{}", "{1}", "a|{1}", "a|*",
  "(*)", "(?:+)", "a??", "a*?", "a+?b", "a?+", "a{2}?", "a{2}??", "[a-]", "[-a]", "[a-b-c]", "[\\w-a]", "[a-\\w]",
  "[\\w-\\d]", "[--a]", "[a--]", "[\\--a]", "[\\]]", "[[]", "[]]", "[^]]", "[\\^]", "[a\\]b]+", "[\\0]", "[\\1]",
  "[\\8]", "[\\c]", "[\\ca]", "[\\c_]", "[\\x]", "[\\x4]", "[\\u]", "[\\u{61}]", "[\\k]", "[\\B]", "[\\b-\\t]",
  "\\8", "\\9", "\\08", "\\18", "\\377", "\\400", "\\777", "(a)\\10", "(a)\\01", "\\c!", "\\ca", "\\c", "a\\cb",
  "\\x", "\\xg1", "\\x1g", "\\u", "\\u{", "\\u{}", "\\u{g}", "\\u{0}", "\\u{10FFFF}", "\\u{0000000061}", "\\u12",
  "\\u{61", "\\ud83d", "\\ude00", "\\ud83dx", "[\\ud83d]", "^\\ud83d\\ude00+$", "^[\\ud83d\\ude00]$", "\\_", "\\&",
  "\\!", "\\ ", "\\-", "\\e", "\\z", "\\A", "\\Z", "\\G", "\\Q", "\\h", "\\N", "\\R", "\\X", "\\i", "\\<", "\\>", "\\'",
  "\\#", "\\%", "\\,", "\\:", "\;", "\\=", "\\@", "\\`", "\\~", "\\\"", "\\é", "é", "^é+$", "^[à-ÿ]+$", "😀", "^😀{2}$",
  "^[😀-😎]$", "^[^😀]$", "^.😀.$", "a{2,3}}", "a{2}}", "}{", "]]", "a]", "a}", "x{", "x{a}", "x{1a}", "x{1,a}",
  "x{ 1}", "(?:a", "(?:a))", "(()", "())", "((a)|(b))\\3", "(a)(b)(c)(d)(e)(f)(g)(h)(i)(j)\\10", "(?i)a", "(?i:a)",
  "(?<=a)(?<!b)c", "(?#c)", "(?'n'a)", "(?P<n>a)", "(?>a)", "a*+", "a++", "[[:alpha:]]", "[a&&b]", "[a~~b]",
  "\\p{L}*\\p{N}", "^(?:[a-z]+\\d)+$", "^(?:\\d+|\\w+)+!$", "^(a|b|c)+?\\1$", "^(?:(a+)|(b+))*c\\1\\2$",
  "^(?=(a+))a*b\\1$", "^(?!.*(.).*\\1)[a-c]+$", "(?<=([abc])+)x\\1", "^(?:(?<x>a)|(?<y>b))+\\k<x>\\k<y>$",
  "^.*😀.$", "^.*?é.+$", "^[^a]*a[^a]*$", "^.{2,4}?😀", "^\\S*\\s\\S*$", "^(?:.*,)*.*$", "^.*.*.*$", "^.+?.+?$",
  "(?<=é.*)a", "(?<=😀.?)a", "(?<!😀.*)a", "(?<=^.{2})a", "(?<=[^a]+)a", "(?<=\\S+?)a", "(?<=a{2,}?)b", "(?<=(a|b)+?)c\\1",
  "^(?=.*é)(?=.*😀)", "^(?!.*é).*a", "^(?:.(?!é))*$", "^(?:(?!😀).)*$", "^.*(?<!a)$", "^.*?(?<=a)$", "a.*?(?=b)",
  "(?=.*?(a))\\1", "^(.*)\\1$", "^(.*?)\\1$", "^(.+).*\\1$", "^(.{1,2})+\\1$", "^(é|😀)+\\1$", "^(?:(.)\\1)+$",
  "^(?:(.)\\1?)+?$", "^(a|b|é)*?(?=\\1)", "(?<=(.)\\1)a", "(?<=\\1(.))a", "(?<=(?<!b)a)a", "(?<!(?<=b)a)a",
  "(?=(?!a).)b", "(?!(?=a).)", "(?<=(?=é).)a", "^(?:a+?b*?)+?c", "^(?:a{1,2}b{0,2}){2,3}c", "^(?:[ab]{2}|.){1,3}?$",
  "^[é😀]{2,}", "^[^é😀]{1,3}?[é😀]", "é*?😀+", "(?:é|a)*?😀", "^a*?$", "^a*?b", "a+?$", "^.??a", "^(?:a??b)+$",
  "\\b.+?\\b(?=\\W)", "(?<=\\b\\w+)\\W", "(?<!\\B)a", "^\\w+(?=\\b)(?!\\w)", "(?<=\\d{2})(?<!\\d{3})x",
  "^(?:(?:a|b)(?=.))*.$", "^((a)|(b)|(c))+\\2\\3\\4$", "^(?:(a)(b)?)+\\2\\1$", "^((a)(?:\\2|b))+$",
];

// Patterns for the ECMAScript 2025 additions (modifier groups and duplicate group names) and for \u escapes in group
// names.
const es2025 = [
  "(?i:a)", "(?i)a", "(?i:a)b", "a(?i:b)c", "(?-i:a)", "(?i-:a)", "(?-:a)", "(?i-i:a)", "(?ii:a)", "(?im:a)",
  "(?ims:a)", "(?i-ms:a)", "(?s-i:a)", "(?x:a)", "(?i", "(?i:", "(?i:a", "(?-i-s:a)", "(?i:(?-i:a)b)", "(?i:a(?-i:b)c)",
  "^(?i:abc)$", "^(?i:[a-c]+)$", "^(?i:[^a-c]+)$", "^(?i:k)$", "^(?i:s)$", "^(?i:\\u212a)$", "^(?i:\\u017f)$",
  "^(?i:[a-z])$", "^(?i:[^a-z])$", "^(?i:\\w)$", "^(?i:\\W)$", "^(?i:[\\w])$", "^(?i:[^\\w])$", "^(?i:[\\W])$",
  "^(?i:[^\\W])$", "(?i:\\bk\\b)", "(?i:\\b)\\u212a", "(?i:\\B)\\u017f", "\\b\\u212a", "(?i:a\\b)", "(?i:\\Bs)",
  "^(?i:ß)$", "^(?i:\\u1e9e)$", "^(?i:σ)$", "^(?i:ς)$", "^(?i:Σ)$", "^(?i:İ)$", "^(?i:ı)$", "^(?i:i)$", "^(?i:I)$",
  "^(?i:ǆ)$", "^(?i:ǅ)$", "^(?i:Ǆ)$", "^(?i:𐐀)$", "^(?i:𐐨)$", "^(?i:[𐐀-𐐧])$", "^(?i:é)$", "^(?i:É)$", "^(?i:µ)$",
  "^(?i:\\u03bc)$", "^(?i:ᾀ)$", "^(?i:ᾈ)$", "^(?i:ǰ)$", "^(?i:ŉ)$", "^(?i:ﬀ)$", "^(?i:[à-þ])+$", "^(?i:\\p{Lu})+$",
  "^(?i:\\p{Ll})+$", "^(?i:\\P{Lu})+$", "^(?i:[^\\p{Lu}])+$", "^(?i:[\\P{Ll}])+$", "^(?i:\\d\\D\\s\\S)$",
  "^(?i:(a)\\1)$", "^(a)(?i:\\1)$", "^(?i:(a))\\1$", "^(?i:(.)\\1)$", "^(.)(?i:\\1)$", "(?<=(?i:\\1)(.))x",
  "(?i:(?<=\\1(k)))x", "^(?<n>.)(?i:\\k<n>)+$", "^(?i:(?<n>[a-z]+)-\\k<n>)$", "^(ß)(?i:\\1)$", "^(ς)(?i:\\1)$",
  "^(𐐀)(?i:\\1)$", "^(?i:x-|es)$", "(?i:foo|bar)", "^(?i:a{2,3}?)$", "^(?i:[a-f\\d]+)$", "(?i:\\x41\\u0042\\cJ)",
  "(?i:\\u{1F600}|\\u{10400})", "^(?i:.)$", "^(?i:[^])$", "^(?i:[])$",
  "(?s:.)", "^(?s:.)$", "^(?s:a.b)$", "^a(?s:.)b.c$", "^(?s-i:.)$", "^(?-s:.)$", "^(?s:(?-s:.).)$", "^(?s:[^\\n])$",
  "(?m:^a)", "(?m:a$)", "(?m:^)a", "a(?m:$)", "(?m:^a$)", "^(?m:a$)", "(?m:^)$", "(?m:$)^", "(?m:^$)", "(?m:^.+$)",
  "(?m:$).(?m:^)", "(?m-s:^.*$)b", "(?ms:^.*$)", "(?<=(?m:^))a", "(?=(?m:$))a?", "(?m:^)(?-m:^)a", "(?<!(?m:^))a",
  "(?m:^)\\b", "x(?m:$)(?s:.)(?m:^)y", "(?m:^)*a", "(?m:$)+",
  "(?<a>x)|(?<a>y)", "(?<a>x)(?<a>y)", "(?<a>x)|(?<a>y)|(?<a>z)", "(?:(?<a>x)|(?<a>y))\\k<a>",
  "^(?:(?<a>x)|(?<a>y))\\k<a>$", "^(?:(?<a>x)|(?<a>y))+\\k<a>$", "^(?:(?<a>x)|(?<a>y)|z)*\\k<a>$",
  "\\k<a>(?:(?<a>x)|(?<a>y))", "(?<a>x)|(?:(?<a>y))", "(?<a>x)|((?<a>y)(?<a>z))", "(?:(?<a>x)|(?<b>y))(?<a>z)",
  "((?<a>x)|(?<a>y))(?<b>z)|(?<b>w)\\k<a>", "(?:(?<a>x)|b)(?:(?<a>y)|c)", "(?<a>x)|(?=(?<a>y))",
  "(?<=(?<a>x)|(?<a>y))\\k<a>z", "^(?:(?<a>a)|(?<a>b))(?:(?<b>a)|(?<b>b))\\k<a>\\k<b>$", "(?<a>(?<a>x))",
  "(?<a>x|(?<a>y))", "(?:(?<a>x)|(?<a>y)){2}\\k<a>", "^(?i:(?<a>x)|(?<a>y))\\k<a>$",
  "(?<\\u0061>a)\\k<a>", "(?<a>a)\\k<\\u0061>", "(?<\\u{61}b>a)\\k<ab>", "(?<\\u{1d4d1}>a)", "(?<\\ud835\\udcd1>a)",
  "(?<a\\u0020>a)", "(?<\\u0030>a)", "(?<a\\u0030>a)\\k<a0>", "(?<\\x61>a)", "(?<\\u>a)", "(?<\\u{}>a)", "(?<é>a)\\k<é>",
  "(?<π>a)\\k<\\u03c0>", "(?<a>a)\\k<b>", "(?<a>a)\\k<a", "(?<a>a)\\k<>", "(?<a>a)\\k", "(?<a>a)\\ka", "(?<a\u200d>a)",
  "(?<\u200d>a)", "(?<$>a)\\k<$>", "(?<_>a)\\k<_>", "(?<a-b>a)", "(?<a.b>a)", "(?<😀>a)", "(?<𝒜>a)\\k<𝒜>",
  // Valid only with no flag, so these fold case as the code units of a pattern with no flag do.
  "^(?i:k)\\&?$", "^(?i:s)\\&?$", "^(?i:\\u212a)\\&?$", "^(?i:\\u017f)\\&?$", "^(?i:[a-z])\\&?$", "^(?i:[^a-z])\\&?$",
  "^(?i:\\w)\\&?$", "^(?i:\\W)\\&?$", "(?i:\\bk\\b)\\&?", "(?i:\\b)\\u212a\\&?", "^(?i:ß)\\&?$", "^(?i:\\u1e9e)\\&?$",
  "^(?i:σ)\\&?$", "^(?i:ς)\\&?$", "^(?i:İ)\\&?$", "^(?i:ı)\\&?$", "^(?i:i)\\&?$", "^(?i:ǆ)\\&?$", "^(?i:ǅ)\\&?$",
  "^(?i:é)\\&?$", "^(?i:µ)\\&?$", "^(?i:\\u03bc)\\&?$", "^(?i:ᾀ)\\&?$", "^(?i:ᾈ)\\&?$", "^(?i:ǰ)\\&?$", "^(?i:ﬀ)\\&?$",
  "^(?i:[à-þ])+\\&?$", "^(.)(?i:\\1)\\&?$", "^(?i:(.)\\1)\\&?$", "^(ß)(?i:\\1)\\&?$", "(?<=(?i:\\1)(.))x\\&?",
  "^(?s:.)\\&?$", "(?m:^a$)\\&?", "(?m:^)*a\\&?", "(?:(?<a>x)|(?<a>y))\\k<a>\\&?", "(?i:\\p)", "(?i:\\a\\e)",
];

const pieces = [
  "a", "b", "c", "d", "z", "A", "X", "Z", "0", "1", "5", "9", "_", "-", ".", ":", "/", "@", "#", "$", "*", "!", "{",
  "}", "|", " ", "\n", " ", "é", "µ", "😀", "😎", " ", "x-", "es", "ES", "ms", "txt", "Au", "to", "No",
  "ne", "2015", "Co", "re", "20", "15", "22", "2", ",", "a,", "ab,", "a-", "aa", "ab", "foo", "bar", "\t", "α", "Ω",
  "\\", "&", "(", ")", "[", "]", "^", "+", "?", "<", ">", "=", "%", "L", "k", "w", "x", "u", "p", "e", "\r", "\u0000",
  "\u0008", "8", "ÿ", "٣", "〆", "ǅ", "K", "K", "s", "S", "ſ", "ß", "ẞ", "σ", "ς", "Σ", "İ", "ı", "i",
  "I", "ǆ", "Ǆ", "𐐀", "𐐨", "É", "μ", "ᾀ", "ᾈ", "ǰ", "ﬀ", "y", " ", "a\nb", "b\ra", "xx", "KK", "ΐ", "ΐ", "ﬅ", "ﬆ",
];

// xorshift32, which oracle_test.go repeats to build the same patterns and texts.
let seed = 0x2545f491;
function next() {
  seed ^= seed << 13; seed >>>= 0;
  seed ^= seed >>> 17;
  seed ^= seed << 5; seed >>>= 0;
  return seed;
}

const inputs = ["", ...pieces];
for (let i = 0; i < 150; i++) {
  const n = 2 + next() % 7;
  let s = "";
  for (let k = 0; k < n; k++) s += pieces[next() % pieces.length];
  inputs.push(s);
}

function compile(p) {
  let u = null, l = null;
  try { u = new RegExp(p, "u"); } catch (e) {}
  try { l = new RegExp(p); } catch (e) {}
  return { u, l };
}

const beyondBmp = (s) => /[\ud800-\udfff]/.test(s);

// hexBits packs a string of 0s and 1s into hexadecimal digits, four to a digit, padded with 0s at the end.
function hexBits(bits) {
  let out = "";
  for (let i = 0; i < bits.length; i += 4) out += parseInt(bits.slice(i, i + 4).padEnd(4, "0"), 2).toString(16);
  return out;
}

// matches returns one bit per text, packed by hexBits. The bit of a text the pattern is not asked about is 0, and the
// test skips it by the same rule.
function matches(c, texts) {
  const re = c.u || c.l;
  let out = "";
  for (const s of texts) {
    out += !c.u && beyondBmp(s) ? "0" : re.test(s) ? "1" : "0";
  }
  return hexBits(out);
}

const hand = [];
const seen = new Set();
for (const p of [...javaHand, ...rustPatterns, ...extra, ...es2025]) {
  if (seen.has(p)) continue;
  seen.add(p);
  const c = compile(p);
  const item = { p, u: !!c.u, l: !!c.l };
  if (c.u || c.l) item.m = matches(c, inputs);
  hand.push(item);
}

// Patterns matched with the i, m and s flags set for the whole pattern. The test wraps each in the modifier group
// that means the same, such as (?i:...). The flags are the long-standing way to ask V8 for these behaviours, and the
// group is the only way to ask this package for them.
const flaggedPatterns = [
  "^k$", "^K$", "^\\u212a$", "^s$", "^S$", "^\\u017f$", "^ß$", "^\\u1e9e$", "^σ$", "^ς$", "^Σ$", "^İ$", "^ı$", "^i$",
  "^I$", "^ǆ$", "^ǅ$", "^Ǆ$", "^𐐀$", "^𐐨$", "^é$", "^É$", "^µ$", "^\\u03bc$", "^ᾀ$", "^ᾈ$", "^ǰ$", "^ŉ$", "^ﬀ$",
  "^\\u03bc\\&?$", "^k\\&?$", "^\\u212a\\&?$", "^\\u017f\\&?$", "^ß\\&?$", "^\\u1e9e\\&?$", "^ᾀ\\&?$", "^ᾈ\\&?$",
  "^ǅ\\&?$", "^𐐀\\&?$", "^σ\\&?$", "^ς\\&?$", "^İ\\&?$", "^ı\\&?$", "^i\\&?$", "^µ\\&?$", "^ǰ\\&?$", "^ﬀ\\&?$",
  "^[a-z]$", "^[^a-z]$", "^[k]$", "^[^k]$", "^[\\u212a]$", "^[^\\u212a]$", "^[à-þ]+$", "^[𐐀-𐐧]$", "^[^𐐀-𐐧]$",
  "^[a-f\\d]+$", "^[\\w]$", "^[^\\w]$", "^[\\W]$", "^[^\\W]$", "^\\w$", "^\\W$", "^[\\w-]+$", "^[^\\W\\d]+$",
  "^[a-z]\\&?$", "^[^a-z]\\&?$", "^\\w\\&?$", "^\\W\\&?$", "^[^\\W]\\&?$", "^[\\W]\\&?$", "^[\\u212a]\\&?$",
  "^[^k]\\&?$", "^[à-þ]+\\&?$", "\\bk\\b", "\\Bs", "\\b\\u212a", "\\B\\u017f", "a\\b", "\\b.\\b", "\\B.\\B",
  "\\bk\\b\\&?", "\\b\\u212a\\&?", "\\B.\\B\\&?", "\\Bs\\&?", "^\\p{Lu}+$", "^\\p{Ll}+$", "^\\P{Lu}+$",
  "^[^\\p{Lu}]+$", "^[\\P{Ll}]+$", "^\\p{L}$", "^\\P{L}$", "^(a)\\1$", "^(.)\\1$", "^(.+)\\1$", "(?<=\\1(.))x",
  "(?<=(.)\\1)x", "^(?<n>[a-z]+)-\\k<n>$", "^(ß)\\1$", "^(.)\\1\\&?$", "^(.+)\\1\\&?$", "(?<=\\1(.))x\\&?",
  "^(?:(.)\\1)+$", ".", "^.$", "^a.b$", "^.+$", "^.*$", "^a$", "^", "$", "^$", "^a", "a$", "^.+$\\&?", "a$\\&?",
  "^a\\&?", "$.^", "$[^]^", "^b", "\\n^", "$\\r", "^\\S+$", "(?<=^)a", "a(?=$)", "(?<!^)a", "a(?!$)", "^.\\&?$",
  "foo|bar", "^x-|es$", "^a{2,3}?$", "\\x41\\u0042\\cJ", "\\u{1F600}|\\u{10400}", "^[^]$", "^[]$", "^(?:a|B)+$",
  "é+", "^[é-ϋ]+$", "^\\d\\D\\s\\S$", "^[^a-c]+$", "^(?:x|\\u017f)+$", "^(a)(?:\\1|b)+$", "^\\u0390$",
  "^\\u1fd3$", "^\\ufb05$", "^[\\ufb06]$", "^[^\\u0390]$", "^\\u0390\\&?$", "^\\ufb05\\&?$", "^(.)\\1$\\&?",
];
const flagged = [];
for (const p of flaggedPatterns) {
  for (const f of ["i", "m", "s", "ims"]) {
    let u = null, l = null;
    try { u = new RegExp(p, f + "u"); } catch (e) {}
    try { l = new RegExp(p, f); } catch (e) {}
    if (u || l) flagged.push({ p, f, u: !!u, m: matches({ u, l }, inputs) });
  }
}

// Random patterns over the alphabet of the Java port's generated test. Each gets a verdict (0 not valid, 1 valid
// with the u flag, 2 valid only with no flag), and the first ones are also matched against short texts.
seed = 0x9e3779b9;
const alphabet = "ab0\\\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L";
const randomCount = 60000, randomMatched = 4000;
const shortInputs = inputs.slice(0, 48).concat(["ab", "ba", "aab", "abab", "a0", "a1b", "aL", "a b", "a-b", "a\nb",
  "a,b", "<a>", "=!", "abba", "x1", "kk", "pu", "(a)", "[b]", "a.b", "a|b", "^a", "bb", "aaa", "a\\b", "b1", "00",
  "a:b", "d1", "wsb", "a{1}", "LL"]);

let verdicts = "";
const randomMatches = [];
for (let i = 0; i < randomCount; i++) {
  const n = 1 + next() % 8;
  let p = "";
  for (let k = 0; k < n; k++) p += alphabet[next() % alphabet.length];
  const c = compile(p);
  verdicts += c.u ? "1" : c.l ? "2" : "0";
  if (i < randomMatched) randomMatches.push(c.u || c.l ? matches(c, shortInputs) : "");
}

// The output is ASCII, so that no tool on the way to the file has to agree about an encoding.
const json = JSON.stringify({
  node: process.version, unicode: process.versions.unicode, inputs, hand, flagged, shortInputs, verdicts,
  randomMatches,
});
process.stdout.write(json.replace(/[^\x00-\x7e]/g, (c) => "\\u" + c.charCodeAt(0).toString(16).padStart(4, "0")) + "\n");
