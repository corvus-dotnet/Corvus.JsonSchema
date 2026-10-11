package ecmaregex

import (
	"encoding/json"
	"os"
	"testing"
)

// A sizeCase is one pattern of testdata/size_cases.json with the size the parser counts for it (see MaxSize) under
// the u flag grammar and under the Annex B grammar, or -1 where that grammar does not accept it.
//
// The Object Pascal and OCaml ports of this engine read the same file. The size of a pattern decides whether it is
// refused as too large, so the three must count the same figure for every pattern, or a schema one port compiles
// would be refused by another. Run the tests with SIZE_CASES=write to write the file again from sizePatterns.
type sizeCase struct {
	P string `json:"p"`
	U int    `json:"u"`
	L int    `json:"l"`
}

// sizePatterns has a pattern for everything the size counts: terms, alternatives, groups of each kind, quantifiers,
// classes with single members, ranges, class escapes and properties, negation, case folding of each of those,
// negated properties, named groups under alternatives and groups of one name, and what only one grammar accepts.
var sizePatterns = []string{
	``, `a`, `abc`, `a|b`, `a|b|c|`, `|`, `(a)`, `(?:a)`, `(?:a|b)`, `((a))`, `(?=a)`, `(?!a)b`, `(?<=a)b`, `(?<!a)b`,
	`a*`, `a+?`, `a{2,3}`, `(?:ab){2}`, `(a|b)*c`, `^a$`, `\b\B`, `.`, `\d`, `\D\w\W\s\S`, `\n\t\x41\u0041\u{41}\cA\0`,
	`\u{0000000041}`, `\ud83d\ude00`, `\-`, `\-\ud83d\ude00`, `😀`, `\-😀`,
	`[a]`, `[abc]`, `[a-z]`, `[a-zA-Z0-9_]`, `[^a]`, `[^a-z0-9]`, `[]`, `[^]`, `[\d]`, `[\d\w\s]`, `[\D]`, `[a\-z]`,
	`[\u0100\u0102\u0104]`, `[a-a-a]`, `[\d-z]`, `[a-\d]`,
	`\p{L}`, `\P{L}`, `\p{Lu}`, `\p{Script=Greek}`, `\P{Script=Greek}`, `[\p{L}]`, `[\p{L}\p{N}]`, `[^\p{L}]`,
	`[\P{L}]`, `[\p{Lu}a-z]`, `\p{L}\p{L}\p{L}`, `\p{Any}`, `\P{Any}`, `\p{ASCII}`,
	`(?i:a)`, `(?i:k)`, `(?i:1)`, `(?i:abc)`, `(?i:[a])`, `(?i:[a-z])`, `(?i:[^a])`, `(?i:.)`, `(?i:\d)`, `(?i:\w)`,
	`(?i:\W)`, `(?i:\p{L})`, `(?i:\p{Lu})`, `(?i:\P{Lu})`, `(?i:[\p{Lu}])`, `(?i:[^\p{Lu}])`, `(?i:\u017f)`,
	`(?i:[\u0000-\u{10FFFF}])`, `(?i:(a)\1)`, `(?i:\b)`, `(?s:.)`, `(?m:^a$)`, `(?i-s:a.)`, `(?s:(?i:.))`,
	`(?<n>a)`, `(?<n>a)\k<n>`, `(?<n>a)|(?<n>b)`, `(?<n>a)|(?<n>b)|(?<n>c)`, `(?:(?<n>a)|(?<n>b))\k<n>`,
	`(?:x|(?:y|(?<n>a)))`, `(?:(?:(?<a>b)|(?<c>d))|(?<a>e))`, `(?<a>(?<b>c))`, `(?<a>x)(?<b>y)`,
	`(a)\1`, `\1(a)`, `(a)(b)\2\1`, `\k<n>`, `\8`, `a{`, `a{1`, `a]`, `}`, `\p{L`, `\pL`, `[\p{Nope}]`, `(?=a)*`,
	`(?=a){2}`, `(?<=a)*`, `\c`, `[\c]`, `\x4`, `\u{110000}`, `a**`, `(`, `)`, `[b-a]`, `(?<n>a)(?<n>b)`,
	`^(?:[a-z0-9]+(?:-[a-z0-9]+)*\.)+[a-z]{2,}$`, `^(?=.*\d)(?=.*[a-z])(?=.*[A-Z]).{8,}$`,
	`^\p{Lu}\p{Ll}+(?: \p{Lu}\p{Ll}+)*$`, `(?i:^[\p{L}'][ \p{L}'-]*[\p{L}]$)`,
}

// sizeOf returns the size the parser counts for a pattern under one grammar, or -1 when the grammar does not accept
// it.
func sizeOf(pattern string, unicode bool) int {
	_, ps, err := parse(pattern, unicode)
	if err != nil {
		return -1
	}
	return ps.size
}

// TestTheSizeOfAPattern checks the size the parser counts for each pattern against testdata/size_cases.json.
func TestTheSizeOfAPattern(t *testing.T) {
	const file = "testdata/size_cases.json"
	if os.Getenv("SIZE_CASES") == "write" {
		cases := make([]sizeCase, len(sizePatterns))
		for i, p := range sizePatterns {
			cases[i] = sizeCase{P: p, U: sizeOf(p, true), L: sizeOf(p, false)}
		}
		data, err := json.MarshalIndent(cases, "", " ")
		if err != nil {
			t.Fatal(err)
		}
		if err := os.WriteFile(file, append(data, '\n'), 0o644); err != nil {
			t.Fatal(err)
		}
	}
	data, err := os.ReadFile(file)
	if err != nil {
		t.Fatal(err)
	}
	var cases []sizeCase
	if err := json.Unmarshal(data, &cases); err != nil {
		t.Fatal(err)
	}
	if len(cases) != len(sizePatterns) {
		t.Fatalf("%s has %d patterns and sizePatterns has %d", file, len(cases), len(sizePatterns))
	}
	valid, legacy, invalid := 0, 0, 0
	for i, c := range cases {
		if c.P != sizePatterns[i] {
			t.Fatalf("pattern %d of %s is %q and sizePatterns has %q", i, file, c.P, sizePatterns[i])
		}
		if u, l := sizeOf(c.P, true), sizeOf(c.P, false); u != c.U || l != c.L {
			t.Errorf("%q: size %d with the u flag and %d without, the file says %d and %d", c.P, u, l, c.U, c.L)
		}
		switch {
		case c.U >= 0:
			valid++
		case c.L >= 0:
			legacy++
		default:
			invalid++
		}
		// A term is 64 bytes, so no pattern that is valid is smaller than that for each of its terms.
		if c.U == 0 && c.P != "" && c.P != "|" {
			t.Errorf("%q has no size", c.P)
		}
	}
	t.Logf("%d patterns: %d valid with the u flag, %d only without it, %d not valid", len(cases), valid, legacy, invalid)
	if valid < 90 || legacy < 10 || invalid < 5 {
		t.Errorf("the cases are not the ones this test is about")
	}
}
