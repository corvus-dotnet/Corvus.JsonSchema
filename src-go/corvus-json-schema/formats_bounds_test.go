package jsonschema

import (
	"math/rand"
	"strings"
	"testing"
	"time"
)

// The checks of the format keyword read strings of the instance, which may come from outside, and none of them is
// under the limit of a match. So each must take time proportional to the string. The check of an internationalized
// host name did not: it decoded and encoded Punycode, looked for a kana for every katakana middle dot and walked
// the neighbours of every joiner, each of which takes time that grows with the square of a label, and it did so for
// a label of any length. A host name of 100,000 katakana middle dots after one kana took 6 seconds to refuse, and
// one of 100,000 different letters took 3.

// TestALongStringIsNotAnInternationalizedHostName checks the strings that took seconds, at ten times the length.
// Each is refused at once. The bound is generous: the check reads nothing of a string that long.
func TestALongStringIsNotAnInternationalizedHostName(t *testing.T) {
	const n = 1000000
	var distinct strings.Builder
	for i := 0; i < n; i++ {
		distinct.WriteRune(rune(0x4e00 + i%20000))
	}
	for name, text := range map[string]string{
		"katakana middle dots after a kana": "ア" + strings.Repeat("・", n),
		"different letters":                 distinct.String(),
		"one letter":                        strings.Repeat("é", n),
		"joiners":                           "ب" + strings.Repeat("ً‌", n/2) + "ب",
		"a long A-label":                    "xn--" + strings.Repeat("a", n/2) + "-" + strings.Repeat("b9", n/4),
		"labels":                            strings.Repeat("a.", n/2) + "a",
	} {
		for _, check := range []struct {
			format string
			text   string
		}{{"idn-hostname", text}, {"idn-email", "a@" + text}} {
			start := time.Now()
			valid := formatKindOf(check.format, Draft202012).checkString([]byte(check.text), false)
			if took := time.Since(start); valid || took > 2*time.Second {
				t.Errorf("%s, %s: valid %v in %v, want not valid at once", check.format, name, valid, took)
			}
		}
	}
}

// TestNoInternationalizedHostNameIsOverTheBound checks what the bound rests on: every host name the check accepts
// has 253 characters at most, so 1,012 bytes at most. It builds names from labels of letters that take one to four
// bytes, around the bound and on both sides of it, and compares the check with the check that has no bound.
func TestNoInternationalizedHostNameIsOverTheBound(t *testing.T) {
	random := rand.New(rand.NewSource(20261011))
	letters := []rune{'a', 'z', '0', 0xe9, 0x3b1, 0x4e00, 0x30a2, 0x20000, 0x2a700, 0x10400 + 0x28}
	accepted, longest, over := 0, 0, 0
	for round := 0; round < 20000; round++ {
		var name strings.Builder
		// A label of one letter of four bytes is the most bytes a name can have for its length as an A-label, so
		// every fourth name is made of those.
		wide := round%4 == 0
		for labels := 1 + random.Intn(1+round%500); labels > 0; labels-- {
			if wide {
				name.WriteRune(letters[7+random.Intn(2)])
			} else {
				for length := 1 + random.Intn(3); length > 0; length-- {
					name.WriteRune(letters[random.Intn(len(letters))])
				}
			}
			if labels > 1 {
				name.WriteByte('.')
			}
		}
		s := name.String()
		bounded, unbounded := isIDNHostname(s), idnHostname(s)
		if bounded != unbounded {
			t.Fatalf("%q (%d bytes): the check says %v and the check with no bound says %v", s, len(s), bounded, unbounded)
		}
		if unbounded {
			accepted++
			longest = max(longest, len(s))
		}
		if len(s) > maxIDNHostnameBytes {
			over++
		}
	}
	t.Logf("%d names accepted, the longest of %d bytes, and %d names over the bound", accepted, longest, over)
	if accepted < 1000 || over < 1000 || longest > maxIDNHostnameBytes || longest < 100 {
		t.Errorf("%d names accepted, the longest of %d bytes, %d over the bound: want many of each, none accepted over %d", accepted, longest, over, maxIDNHostnameBytes)
	}
	// The longest names there are: 253 ASCII characters, and a name over that by one.
	label := strings.Repeat("a", 63)
	at := label + "." + label + "." + label + "." + strings.Repeat("a", 61)
	if len(at) != 253 || !isIDNHostname(at) || isIDNHostname(at+"a") {
		t.Errorf("a name of 253 characters is valid and one of 254 is not")
	}
}
