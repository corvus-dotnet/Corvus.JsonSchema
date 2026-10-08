package jsonschema

import (
	"encoding/json"
	"fmt"
	"math"
	"strconv"
	"strings"
	"testing"
)

func mustParse(t testing.TB, json string) *Document {
	t.Helper()
	d, err := ParseDocumentString(json)
	if err != nil {
		t.Fatalf("parse %q: %v", json, err)
	}
	return d
}

func TestParseRoundTrips(t *testing.T) {
	for _, json := range []string{
		`null`, `true`, `false`, `0`, `-0`, `1.5e3`, `""`, `"a"`, `[]`, `{}`, `[1,[2,[3]],{"a":null}]`,
		`{"a":1,"b":[true,false],"c":{"d":"e"}}`, `"café \n \"q\" \\ 😀"`, `18446744073709551615`,
		`-9223372036854775808`, `123456789012345678901234567890`,
	} {
		d := mustParse(t, json)
		again := mustParse(t, d.String())
		if !valuesEqual(d, d.root, again, again.root) {
			t.Errorf("%s did not round trip: %s", json, d.String())
		}
	}
	if got := mustParse(t, " { \"a\" : [ 1 , 2 ] } ").String(); got != `{"a":[1,2]}` {
		t.Errorf("compact text: %s", got)
	}
	if got := mustParse(t, `"\u0001\t\/"`).String(); got != `"\u0001\t/"` {
		t.Errorf("escapes: %s", got)
	}
}

func TestParseRejectsInvalidJSON(t *testing.T) {
	for _, json := range []string{
		``, ` `, `{`, `[`, `[1,]`, `{"a":1,}`, `{"a"}`, `{a:1}`, `01`, `1.`, `.5`, `-`, `1e`, `+1`, `tru`, `nul`,
		`"abc`, `"\x"`, `"\u12"`, `"\ud800"`, `"\udc00"`, `"\ud800A"`, "\"a\nb\"", `1 2`, `[1] x`, `1e999`,
		"\"\xff\"", "\"\xc0\x80\"", "\"\xed\xa0\x80\"", `{"a":1 "b":2}`, `[1 2]`, `]`,
	} {
		if _, err := ParseDocumentString(json); err == nil {
			t.Errorf("%q parsed", json)
		}
		if new(parser).isValid([]byte(json)) {
			t.Errorf("%q is valid to the syntax check", json)
		}
	}
	deep := strings.Repeat("[", MaxDepth) + strings.Repeat("]", MaxDepth)
	if _, err := ParseDocumentString(deep); err != nil {
		t.Errorf("depth %d: %v", MaxDepth, err)
	}
	if _, err := ParseDocumentString("[" + deep + "]"); err == nil {
		t.Errorf("depth %d parsed", MaxDepth+1)
	}
	var pe *ParseError
	_, err := ParseDocumentString(`[1, x]`)
	if e, ok := err.(*ParseError); !ok || e.Offset != 4 {
		t.Errorf("error %v (%T), want a *ParseError at offset 4, like %T", err, err, pe)
	}
}

func TestDuplicateKeysKeepTheLastValueAtTheFirstPosition(t *testing.T) {
	if got := mustParse(t, `{"a":1,"b":2,"a":3,"c":4,"b":5}`).String(); got != `{"a":3,"b":5,"c":4}` {
		t.Errorf("small object: %s", got)
	}
	var sb strings.Builder
	sb.WriteString("{")
	for i := 0; i < 40; i++ {
		fmt.Fprintf(&sb, `"k%d":%d,`, i, i)
	}
	sb.WriteString(`"k7":"seven","k3":"three"}`)
	d := mustParse(t, sb.String())
	if d.count(d.root) != 40 {
		t.Fatalf("count %d", d.count(d.root))
	}
	if v := d.property(d.root, "k7"); string(d.str(v)) != "seven" {
		t.Errorf("k7: %s", d.appendJSON(nil, v))
	}
	if v := d.property(d.root, "k3"); string(d.str(v)) != "three" {
		t.Errorf("k3: %s", d.appendJSON(nil, v))
	}
	if k := d.first(d.root) + 2*7; string(d.str(k)) != "k7" {
		t.Errorf("position 7 holds %s", d.str(k))
	}
}

func TestNumbers(t *testing.T) {
	number := func(json string) (uint8, uint64) {
		d := mustParse(t, json)
		return d.flags(d.root), d.data(d.root)
	}
	for _, c := range []struct {
		json string
		flag uint8
	}{
		{"0", numInt}, {"-1", numInt}, {"9223372036854775807", numInt}, {"-9223372036854775808", numInt},
		{"9223372036854775808", numUint}, {"18446744073709551615", numUint}, {"18446744073709551616", numFloat},
		{"-9223372036854775809", numFloat}, {"-0", numFloat}, {"1.0", numFloat}, {"1e2", numFloat},
	} {
		if flag, _ := number(c.json); flag != c.flag {
			t.Errorf("%s has representation %d, want %d", c.json, flag, c.flag)
		}
	}
	for _, json := range []string{
		"0.1", "1e22", "1e23", "123456789012345678.0", "0.000001", "5e-324", "1.7976931348623157e308",
		"2.2250738585072014e-308", "9007199254740993.0", "0.30000000000000004", "1e-400",
		"123456789012345678901234567890.123456789",
	} {
		_, v := number(json)
		var want float64
		fmt.Sscanf(json, "%g", &want)
		if math.Float64frombits(v) != want {
			t.Errorf("%s parsed as %v, want %v", json, math.Float64frombits(v), want)
		}
	}
	compare := func(a, b string) int {
		fa, va := number(a)
		fb, vb := number(b)
		return compareNumbers(fa, va, fb, vb)
	}
	for _, c := range []struct {
		a, b string
		want int
	}{
		{"1", "1.0", 0}, {"-1", "-0.5", -1}, {"9007199254740993", "9007199254740992.0", 1}, {"0", "-0", 0},
		{"18446744073709551615", "1.8446744073709552e19", -1}, {"9223372036854775808", "9223372036854775808.0", 0},
		{"9223372036854775807", "9223372036854775808", -1}, {"1e30", "18446744073709551615", 1},
		{"-1e30", "-9223372036854775808", -1}, {"2", "1.5", 1}, {"1", "1.5", -1},
	} {
		if got := compare(c.a, c.b); got != c.want {
			t.Errorf("compare(%s, %s) = %d, want %d", c.a, c.b, got, c.want)
		}
		if got := compare(c.b, c.a); got != -c.want {
			t.Errorf("compare(%s, %s) = %d, want %d", c.b, c.a, got, -c.want)
		}
	}
}

func TestMultipleOfIsExact(t *testing.T) {
	for _, c := range []struct {
		x, d string
		want bool
	}{
		{"0.0075", "0.0001", true}, {"0.00751", "0.0001", false}, {"4.5", "1.5", true}, {"35", "1.5", false},
		{"10", "5", true}, {"1e308", "0.123456789", false}, {"1e308", "0.5", true}, {"0", "0.3", true},
		{"1e-300", "1e-7", false}, {"7", "2", false}, {"-8", "2", true}, {"1.0", "1", true}, {"3.0", "1.5", true},
		{"12391239123", "0.01", true}, {"1.23456789012345678901234567890", "0.00000000000000000000000000001", true},
		{"1.5", "0.1234567890123456789012", false}, {"0.2469135780246913578024", "0.1234567890123456789012", true},
		{"18446744073709551615", "5", true}, {"1", "0", false}, {"0.0", "0.0", false},
	} {
		x, d := mustParse(t, c.x), mustParse(t, c.d)
		if got := newDivisor(d, d.root).divides(x, x.root); got != c.want {
			t.Errorf("%s multipleOf %s = %v, want %v", c.x, c.d, got, c.want)
		}
	}
}

func TestEqualityAndHashing(t *testing.T) {
	equal := [][]string{
		{`1`, `1.0`, `1e0`, `10e-1`},
		{`{"a":1,"b":[1,2]}`, `{"b":[1.0,2],"a":1}`},
		{`"ab"`, `"ab"`},
		{`[]`, `[ ]`},
		{`0`, `-0`, `0.0`},
		{`18446744073709551615`, `18446744073709551615`},
		{`9223372036854775808`, `9223372036854775808.0`},
	}
	for _, group := range equal {
		a := mustParse(t, group[0])
		for _, other := range group[1:] {
			b := mustParse(t, other)
			if !valuesEqual(a, a.root, b, b.root) || !valuesEqual(b, b.root, a, a.root) {
				t.Errorf("%s != %s", group[0], other)
			}
			if valueHash(a, a.root) != valueHash(b, b.root) {
				t.Errorf("hash(%s) != hash(%s)", group[0], other)
			}
		}
	}
	distinct := []string{`1`, `"1"`, `[1]`, `{"a":1}`, `{"a":2}`, `{"b":1}`, `null`, `false`, `true`, `1.5`, `[1,2]`, `[2,1]`}
	for i, x := range distinct {
		for j, y := range distinct {
			a, b := mustParse(t, x), mustParse(t, y)
			if got := valuesEqual(a, a.root, b, b.root); got != (i == j) {
				t.Errorf("equal(%s, %s) = %v", x, y, got)
			}
		}
	}
}

func TestAllUnique(t *testing.T) {
	var scratch []uint64
	unique := func(json string) bool {
		d := mustParse(t, json)
		return allUnique(d, d.root, &scratch)
	}
	for json, want := range map[string]bool{
		`[]`: true, `[1]`: true, `[1,2]`: true, `[1,1.0]`: false, `["a","b","a"]`: false, `["a","b","ab"]`: true,
		`[{"a":1,"b":2},{"b":2,"a":1}]`: false, `[[1],[1,2],[2,1]]`: true, `[1,"1",true,null,[1],{"1":1}]`: true,
	} {
		if got := unique(json); got != want {
			t.Errorf("unique(%s) = %v", json, got)
		}
	}
	var items []string
	for i := 0; i < 100; i++ {
		items = append(items, fmt.Sprintf(`{"n":%d,"s":"%d"}`, i, i%7), fmt.Sprint(i), fmt.Sprintf(`"%d"`, i))
	}
	if !unique("[" + strings.Join(items, ",") + "]") {
		t.Error("300 distinct items are not unique")
	}
	if unique("[" + strings.Join(items, ",") + `,{"s":"3","n":3.0}]`) {
		t.Error("a duplicate among 301 items was not found")
	}
}

func TestStrHashReadsEveryByte(t *testing.T) {
	base := []byte("abcdefghijklmnopqrstuvwxyz")
	for n := 0; n <= len(base); n++ {
		h := strHash(base[:n])
		for i := 0; i < n; i++ {
			changed := append([]byte(nil), base[:n]...)
			changed[i] ^= 1
			if strHash(changed) == h {
				t.Errorf("length %d: byte %d does not affect the hash", n, i)
			}
		}
	}
}

// xorshift is a small deterministic generator for the tests that compare against a reference.
type xorshift uint64

func (x *xorshift) next(n int) int {
	*x ^= *x << 13
	*x ^= *x >> 7
	*x ^= *x << 17
	return int(uint64(*x) % uint64(n))
}

func TestStringsAgreeWithEncodingJSON(t *testing.T) {
	pieces := []string{
		"a", "b", "z", " ", "0", "/", "é", "日", "😀", `\n`, `\t`, `\"`, `\\`, `\/`, `\u0041`, `\u00e9`, `\ud83d\ude00`,
		`\b`, `\f`, `\r`, "abcdefgh", "ABCDEFGHIJKLMNOP", "\u007f",
	}
	breakers := []string{"\n", "\x00", "\x1f", `\x`, `\u12G`, "\xff", "\xc3", `\ud800`, `\udc00x`}
	seed := xorshift(0x2545f4914f6cdd1d)
	for i := 0; i < 20000; i++ {
		var sb strings.Builder
		sb.WriteByte('"')
		for n := seed.next(24); n > 0; n-- {
			sb.WriteString(pieces[seed.next(len(pieces))])
		}
		broken := seed.next(8) == 0
		if broken {
			sb.WriteString(breakers[seed.next(len(breakers))])
			for n := seed.next(12); n > 0; n-- {
				sb.WriteString(pieces[seed.next(len(pieces))])
			}
		}
		sb.WriteByte('"')
		text := sb.String()
		d, err := ParseDocumentString(text)
		if broken {
			if err == nil {
				t.Fatalf("%q parsed", text)
			}
			if new(parser).isValid([]byte(text)) {
				t.Fatalf("%q is valid to the syntax check", text)
			}
			continue
		}
		var want string
		if jsonErr := json.Unmarshal([]byte(text), &want); jsonErr != nil {
			t.Fatalf("the reference rejects %q: %v", text, jsonErr)
		}
		if err != nil {
			t.Fatalf("%q: %v", text, err)
		}
		if got := string(d.str(d.root)); got != want {
			t.Fatalf("%q read as %q, want %q", text, got, want)
		}
		if ascii := d.strASCII(d.root); ascii != (strings.IndexFunc(want, func(r rune) bool { return r >= 0x80 }) < 0) {
			t.Fatalf("%q: ASCII flag %v", text, ascii)
		}
	}
}

func TestNumbersAgreeWithStrconv(t *testing.T) {
	seed := xorshift(0x9e3779b97f4a7c15)
	digits := func(n int) string {
		b := make([]byte, n)
		for i := range b {
			b[i] = byte('0' + seed.next(10))
		}
		return string(b)
	}
	for i := 0; i < 50000; i++ {
		var sb strings.Builder
		if seed.next(3) == 0 {
			sb.WriteByte('-')
		}
		if seed.next(6) == 0 {
			sb.WriteByte('0')
		} else {
			sb.WriteByte(byte('1' + seed.next(9)))
			sb.WriteString(digits(seed.next(24)))
		}
		floating := false
		if seed.next(2) == 0 {
			floating = true
			sb.WriteByte('.')
			sb.WriteString(digits(1 + seed.next(24)))
		}
		if seed.next(3) == 0 {
			floating = true
			sb.WriteByte("eE"[seed.next(2)])
			sb.WriteString([]string{"", "+", "-"}[seed.next(3)])
			sb.WriteString(strconv.Itoa(seed.next(40)))
		}
		text := sb.String()
		d := mustParse(t, " "+text+" ")
		flag, data := d.flags(d.root), d.data(d.root)
		if got := string(d.numberText(d.root)); got != text {
			t.Fatalf("%s: text %s", text, got)
		}
		if !floating {
			if v, err := strconv.ParseInt(text, 10, 64); err == nil && text != "-0" {
				if flag != numInt || int64(data) != v {
					t.Fatalf("%s: representation %d, value %d", text, flag, int64(data))
				}
				continue
			}
			if v, err := strconv.ParseUint(text, 10, 64); err == nil {
				if flag != numUint || data != v {
					t.Fatalf("%s: representation %d, value %d", text, flag, data)
				}
				continue
			}
		}
		want, err := strconv.ParseFloat(text, 64)
		if err != nil {
			t.Fatalf("the reference rejects %s: %v", text, err)
		}
		if flag != numFloat || data != math.Float64bits(want) {
			t.Fatalf("%s: representation %d, value %v, want %v", text, flag, math.Float64frombits(data), want)
		}
	}
}
