package ecmaregex

import (
	"encoding/json"
	"os"
	"strings"
	"testing"
)

// The oracle holds what V8 answers for a set of patterns and texts. testdata/gen_oracle.js writes it.
type oracle struct {
	Node          string   `json:"node"`
	Inputs        []string `json:"inputs"`
	ShortInputs   []string `json:"shortInputs"`
	Verdicts      string   `json:"verdicts"`
	RandomMatches []string `json:"randomMatches"`
	Hand          []struct {
		P string `json:"p"`
		// U and L say whether the pattern is valid with the u flag and with no flag.
		U bool `json:"u"`
		L bool `json:"l"`
		// M has one bit per input, set for a match, written as hexadecimal digits of four bits each.
		M string `json:"m"`
	} `json:"hand"`
	Flagged []struct {
		P string `json:"p"`
		// F holds the flags the pattern was matched with, out of i, m and s.
		F string `json:"f"`
		U bool   `json:"u"`
		M string `json:"m"`
	} `json:"flagged"`
}

func loadOracle(t *testing.T) *oracle {
	t.Helper()
	data, err := os.ReadFile("testdata/v8_oracle.json")
	if err != nil {
		t.Fatal(err)
	}
	var o oracle
	if err := json.Unmarshal(data, &o); err != nil {
		t.Fatal(err)
	}
	return &o
}

// bit returns bit k of a string written by the oracle's hexBits.
func bit(hex string, k int) bool {
	digit := hex[k/4]
	if digit >= 'a' {
		digit = digit - 'a' + 10
	} else {
		digit -= '0'
	}
	return digit>>(3-uint(k)%4)&1 == 1
}

// beyondBMP reports whether the text has a character beyond the Basic Multilingual Plane. V8 matches a pattern with
// no flag by UTF-16 code unit and this package matches by code point, so the oracle is not asked about such a text
// for a pattern that is valid only with no flag.
func beyondBMP(s string) bool {
	for _, r := range s {
		if r > 0xFFFF {
			return true
		}
	}
	return false
}

// v8ModifierBugs lists the patterns for which V8 13.6 answers a case-insensitive modifier group differently from the
// same pattern with the i flag. ECMA-262 gives the two the same meaning. Only the validity of these patterns is taken
// from the oracle. TestOracleFlags checks the behaviour of each through the i flag, where V8 agrees with this package.
var v8ModifierBugs = map[string]bool{
	`^(?i:\u212a)$`: true, `^(?i:\u017f)$`: true, `^(?i:\u1e9e)$`: true, `^(?i:\u03bc)$`: true,
	`^(?i:\u03bc)\&?$`: true, `(?i:\Bs)`: true,
}

// checkVerdict compares the validity of a pattern with V8's. It returns the pattern compiled for each back end, or
// nil when the pattern is not valid.
func checkVerdict(t *testing.T, p string, validUnicode, validLegacy bool) (re, backtrack *Regexp) {
	t.Helper()
	if got := Valid(p); got != validUnicode {
		t.Errorf("Valid(%q) = %v, V8 says %v", p, got, validUnicode)
	}
	re, err := Compile(p)
	if (err == nil) != (validUnicode || validLegacy) {
		t.Errorf("Compile(%q) error %v, V8 valid with u %v, with no flag %v", p, err, validUnicode, validLegacy)
		return nil, nil
	}
	if err != nil {
		return nil, nil
	}
	if re.Unicode() != validUnicode {
		t.Errorf("Compile(%q).Unicode() = %v, V8 says %v", p, re.Unicode(), validUnicode)
	}
	backtrack, err = compile(p, true)
	if err != nil {
		t.Fatalf("compile(%q) for the backtracking matcher: %v", p, err)
	}
	return re, backtrack
}

// checkAnswers compares the answers of both back ends with the oracle's on every text. It returns the number of
// answers compared and the number of those that are a match.
func checkAnswers(t *testing.T, p string, re, backtrack *Regexp, inputs []string, bits string) (answers, matched int) {
	t.Helper()
	for i, s := range inputs {
		if !re.Unicode() && beyondBMP(s) {
			continue
		}
		want := bit(bits, i)
		answers++
		if want {
			matched++
		}
		if got := re.MatchString(s); got != want {
			t.Errorf("%q on %q (%v): got %v, V8 says %v", p, s, re.Engine(), got, want)
		}
		if got := backtrack.Match([]byte(s)); got != want {
			t.Errorf("%q on %q (forced backtrack): got %v, V8 says %v", p, s, got, want)
		}
	}
	return answers, matched
}

// TestOracleHandWritten checks the hand-written patterns (the Java port's validator cases, the Rust port's pattern
// cases and this package's own) against V8, for validity and for the answer on every text, on both back ends.
func TestOracleHandWritten(t *testing.T) {
	o := loadOracle(t)
	patterns, answers, matched := 0, 0, 0
	for _, h := range o.Hand {
		patterns++
		re, backtrack := checkVerdict(t, h.P, h.U, h.L)
		if re == nil || v8ModifierBugs[h.P] {
			continue
		}
		a, m := checkAnswers(t, h.P, re, backtrack, o.Inputs, h.M)
		answers, matched = answers+a, matched+m
	}
	t.Logf("%d patterns, %d answers per back end (%d of them a match), from Node %s", patterns, answers, matched, o.Node)
}

// TestOracleFlags checks case-insensitive, multiline and dot-all matching. V8 matched each pattern with flags set
// for the whole pattern, and here the pattern is wrapped in the modifier group that means the same.
func TestOracleFlags(t *testing.T) {
	o := loadOracle(t)
	answers, matched := 0, 0
	for _, f := range o.Flagged {
		p := "(?" + f.F + ":" + f.P + ")"
		re, backtrack := checkVerdict(t, p, f.U, !f.U)
		if re == nil {
			continue
		}
		a, m := checkAnswers(t, p, re, backtrack, o.Inputs, f.M)
		answers, matched = answers+a, matched+m
	}
	t.Logf("%d patterns with flags, %d answers per back end (%d of them a match)", len(o.Flagged), answers, matched)
}

// xorshift is the generator gen_oracle.js uses, so the test builds the patterns the oracle's verdicts are about.
type xorshift uint32

func (x *xorshift) next() uint32 {
	s := uint32(*x)
	s ^= s << 13
	s ^= s >> 17
	s ^= s << 5
	*x = xorshift(s)
	return s
}

// TestOracleGenerated ports the Java port's generated test, which reads random patterns over an alphabet of syntax
// characters. Each one's validity is checked against V8, and the first few thousand are also matched on both back
// ends.
func TestOracleGenerated(t *testing.T) {
	o := loadOracle(t)
	const alphabet = `ab0\\^$.*+?()[]{}|-,:=!<>dwsbpkuxc1L`
	seed := xorshift(0x9e3779b9)
	var counts [3]int
	answers, matched := 0, 0
	for i := 0; i < len(o.Verdicts); i++ {
		n := 1 + int(seed.next()%8)
		var b strings.Builder
		for k := 0; k < n; k++ {
			b.WriteByte(alphabet[seed.next()%uint32(len(alphabet))])
		}
		p := b.String()
		verdict := o.Verdicts[i]
		counts[verdict-'0']++
		re, backtrack := checkVerdict(t, p, verdict == '1', verdict == '2')
		if re == nil || i >= len(o.RandomMatches) {
			continue
		}
		a, m := checkAnswers(t, p, re, backtrack, o.ShortInputs, o.RandomMatches[i])
		answers, matched = answers+a, matched+m
		if t.Failed() && i > 200 {
			t.FailNow()
		}
	}
	t.Logf("%d patterns (%d not valid, %d valid with u, %d valid only with no flag), %d answers per back end (%d of them a match)",
		len(o.Verdicts), counts[0], counts[1], counts[2], answers, matched)
}
