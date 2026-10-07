package jsonschema

import (
	"math"
	"regexp"
	"strings"
	"sync"
	"unicode"
	"unicode/utf8"
	"unsafe"
)

// Format assertions, applied only when format is asserted (the format-assertion vocabulary or WithAssertFormat).
// They match the C# evaluator's format checks.

// formatKind is the format a dialect recognises for a format value.
type formatKind uint8

const (
	formatUnknown formatKind = iota
	formatDate
	formatTime
	formatDateTime
	formatDuration
	formatUUID
	formatIPv4
	formatIPv6
	formatHostname
	formatIDNHostname
	formatEmail
	formatIDNEmail
	formatURI
	formatURIReference
	formatIRI
	formatIRIReference
	formatURITemplate
	formatJSONPointer
	formatRelativeJSONPointer
	formatRegex
	// Numeric formats (a Corvus extension).
	formatByte
	formatUInt16
	formatUInt32
	formatUInt64
	formatUInt128
	formatSByte
	formatInt16
	formatInt32
	formatInt64
	formatInt128
	formatHalf
	formatSingle
	formatDouble
	formatDecimal
)

func formatKindOf(format string, dialect Dialect) formatKind {
	atLeast := func(d Dialect, k formatKind) formatKind {
		if dialect >= d {
			return k
		}
		return formatUnknown
	}
	switch format {
	case "float", "single":
		return formatSingle
	case "byte":
		return formatByte
	case "uint16":
		return formatUInt16
	case "uint32":
		return formatUInt32
	case "uint64":
		return formatUInt64
	case "uint128":
		return formatUInt128
	case "sbyte":
		return formatSByte
	case "int16":
		return formatInt16
	case "int32":
		return formatInt32
	case "int64":
		return formatInt64
	case "int128":
		return formatInt128
	case "half":
		return formatHalf
	case "double":
		return formatDouble
	case "decimal":
		return formatDecimal
	case "date-time":
		return formatDateTime
	case "email":
		return formatEmail
	case "hostname":
		return formatHostname
	case "ipv4":
		return formatIPv4
	case "ipv6":
		return formatIPv6
	case "uri":
		return formatURI
	case "uri-reference":
		return atLeast(Draft6, formatURIReference)
	case "uri-template":
		return atLeast(Draft6, formatURITemplate)
	case "json-pointer":
		return atLeast(Draft6, formatJSONPointer)
	case "date":
		return atLeast(Draft7, formatDate)
	case "time":
		return atLeast(Draft7, formatTime)
	case "regex":
		return atLeast(Draft7, formatRegex)
	case "relative-json-pointer":
		return atLeast(Draft7, formatRelativeJSONPointer)
	case "idn-email":
		return atLeast(Draft7, formatIDNEmail)
	case "idn-hostname":
		return atLeast(Draft7, formatIDNHostname)
	case "iri":
		return atLeast(Draft7, formatIRI)
	case "iri-reference":
		return atLeast(Draft7, formatIRIReference)
	case "duration":
		return atLeast(Draft201909, formatDuration)
	case "uuid":
		return atLeast(Draft201909, formatUUID)
	}
	return formatUnknown
}

func (k formatKind) isNumeric() bool {
	return k >= formatByte
}

var formatNames = [...]string{
	"unknown", "date", "time", "date-time", "duration", "uuid", "ipv4", "ipv6", "hostname", "idn-hostname", "email",
	"idn-email", "uri", "uri-reference", "iri", "iri-reference", "uri-template", "json-pointer",
	"relative-json-pointer", "regex", "byte", "uint16", "uint32", "uint64", "uint128", "sbyte", "int16", "int32",
	"int64", "int128", "half", "single", "double", "decimal",
}

// name is the canonical name, for messages.
func (k formatKind) name() string {
	return formatNames[k]
}

// message is the message for a string format failure (the C# evaluator's text), or "".
func (k formatKind) message() string {
	switch k {
	case formatDate:
		return "Expected an ISO8601 Date string."
	case formatDateTime:
		return "Expected an ISO8601 Offset DateTime string."
	case formatTime:
		return "Expected an ISO8601 Offset Time string."
	case formatDuration:
		return "Expected an ISO8601 Duration string."
	case formatEmail:
		return "Expected an RFC5321 Section-4.1.2 Email string."
	case formatIDNEmail:
		return "Expected an RFC6531 IDN Email string."
	case formatHostname:
		return "Expected an RFC1035 hostname."
	case formatIDNHostname:
		return "Expected an RFC5890 Section-2.3.2.3 IDN hostname."
	case formatIPv4:
		return "Expected an RFC2673 IP V4 address."
	case formatIPv6:
		return "Expected an RFC2373 IP V6 address."
	case formatURI:
		return "Expected an absolute URI."
	case formatURIReference:
		return "Expected a URI reference."
	case formatIRI:
		return "Expected an absolute IRI."
	case formatIRIReference:
		return "Expected an IRI reference."
	case formatUUID:
		return "Expected an RFC4122 UUID."
	case formatURITemplate:
		return "Expected an RFC6570 URI Template."
	case formatJSONPointer:
		return "Expected an RFC6901 JSON Pointer."
	case formatRelativeJSONPointer:
		return "Expected a Relative JSON Pointer. (https://json-schema.org/draft/2020-12/relative-json-pointer)."
	case formatRegex:
		return "Expected a regular expression specification."
	}
	return ""
}

// checkString asserts a string format. legacyHostname selects the RFC 1123 host name rules of draft 4 and 6.
func (k formatKind) checkString(b []byte, legacyHostname bool) bool {
	// The checks read the text and keep nothing of it.
	s := unsafe.String(unsafe.SliceData(b), len(b))
	switch k {
	case formatDate:
		return isDate(s)
	case formatTime:
		return isTime(s)
	case formatDateTime:
		return isDateTime(s)
	case formatDuration:
		return isDuration(s)
	case formatUUID:
		return isUUID(s)
	case formatIPv4:
		return isIPv4(s)
	case formatIPv6:
		return isIPv6(s)
	case formatHostname:
		if legacyHostname {
			return isLegacyHostname(s)
		}
		return isHostname(s)
	case formatIDNHostname:
		return isIDNHostname(s)
	case formatEmail:
		return isEmail(s, false)
	case formatIDNEmail:
		return isEmail(s, true)
	case formatURI:
		return uriRegexp().MatchString(s)
	case formatURIReference:
		return uriReferenceRegexp().MatchString(s)
	case formatIRI:
		return iriRegexp().MatchString(s)
	case formatIRIReference:
		return iriReferenceRegexp().MatchString(s)
	case formatURITemplate:
		return uriTemplateRegexp().MatchString(s)
	case formatJSONPointer:
		return isJSONPointer(s)
	case formatRelativeJSONPointer:
		return isRelativeJSONPointer(s)
	case formatRegex:
		return validRegex(string(b))
	}
	return true
}

// checkNumber asserts a numeric format.
func (k formatKind) checkNumber(d *Document, n int) bool {
	flag, data := d.flags(n), d.data(n)
	f := d.float(n)
	integral := func(lo, hi float64) bool {
		return !math.IsInf(f, 0) && f == math.Floor(f) && f >= lo && f <= hi
	}
	intRange := func(lo int64, hi uint64) bool {
		switch flag {
		case numInt:
			i := int64(data)
			return i >= lo && (i < 0 || uint64(i) <= hi)
		case numUint:
			return data <= hi
		}
		return integral(float64(lo), float64(hi))
	}
	magnitude := func(limit float64) bool {
		return !math.IsInf(f, 0) && math.Abs(f) <= limit
	}
	switch k {
	case formatByte:
		return intRange(0, 255)
	case formatUInt16:
		return intRange(0, 65535)
	case formatUInt32:
		return intRange(0, 4294967295)
	case formatUInt64:
		return intRange(0, math.MaxUint64)
	case formatUInt128:
		switch flag {
		case numInt:
			return int64(data) >= 0
		case numUint:
			return true
		}
		return integral(0, 3.402823669209385e38)
	case formatSByte:
		return intRange(-128, 127)
	case formatInt16:
		return intRange(-32768, 32767)
	case formatInt32:
		return intRange(-2147483648, 2147483647)
	case formatInt64:
		return intRange(math.MinInt64, math.MaxInt64)
	case formatInt128:
		return flag != numFloat || integral(-1.7014118346046923e38, 1.7014118346046923e38)
	case formatHalf:
		return magnitude(65504)
	case formatSingle:
		return magnitude(3.4028234663852886e38)
	case formatDouble:
		return magnitude(math.MaxFloat64)
	case formatDecimal:
		return magnitude(7.922816251426434e28)
	}
	return true
}

func isLeapYear(y int) bool {
	return y%4 == 0 && (y%100 != 0 || y%400 == 0)
}

// digitsValue is the value of a run of ASCII digits, or -1.
func digitsValue(s string) int {
	if s == "" {
		return -1
	}
	v := 0
	for i := 0; i < len(s); i++ {
		if !isASCIIDigit(s[i]) {
			return -1
		}
		v = v*10 + int(s[i]-'0')
	}
	return v
}

var monthDays = [...]int{0, 31, 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31}

func isDate(s string) bool {
	if len(s) != 10 || s[4] != '-' || s[7] != '-' {
		return false
	}
	y, m, d := digitsValue(s[0:4]), digitsValue(s[5:7]), digitsValue(s[8:10])
	if y < 0 || m < 1 || m > 12 || d < 1 {
		return false
	}
	if m == 2 && isLeapYear(y) {
		return d <= 29
	}
	return d <= monthDays[m]
}

func isTime(s string) bool {
	if len(s) < 9 || s[2] != ':' || s[5] != ':' {
		return false
	}
	h, mi, sec := digitsValue(s[0:2]), digitsValue(s[3:5]), digitsValue(s[6:8])
	if h < 0 || mi < 0 || sec < 0 {
		return false
	}
	i := 8
	if s[i] == '.' {
		i++
		start := i
		for i < len(s) && isASCIIDigit(s[i]) {
			i++
		}
		if i == start {
			return false
		}
	}
	if i >= len(s) {
		return false
	}
	oh, om, sign := 0, 0, 0
	switch s[i] {
	case 'z', 'Z':
		if i+1 != len(s) {
			return false
		}
	case '+', '-':
		if len(s)-i != 6 || s[i+3] != ':' {
			return false
		}
		sign = -1
		if s[i] == '-' {
			sign = 1
		}
		oh, om = digitsValue(s[i+1:i+3]), digitsValue(s[i+4:i+6])
		if oh < 0 || om < 0 || oh > 23 || om > 59 {
			return false
		}
	default:
		return false
	}
	if h > 23 || mi > 59 || sec > 60 {
		return false
	}
	if sec == 60 {
		// A leap second is only valid at 23:59:60 UTC.
		utc := (h*60 + mi + sign*(oh*60+om)) % 1440
		if utc < 0 {
			utc += 1440
		}
		return utc == 23*60+59
	}
	return true
}

func isDateTime(s string) bool {
	return len(s) > 11 && (s[10] == 'T' || s[10] == 't') && isDate(s[:10]) && isTime(s[11:])
}

// isDuration reads an ISO 8601 duration: P then weeks, or date parts in order then an optional time, or a time.
func isDuration(s string) bool {
	if len(s) < 2 || s[0] != 'P' {
		return false
	}
	i := 1
	// part reads digits and one of the designators from position `from` in order, returning the designator's index.
	part := func(designators string, from int) int {
		start := i
		for i < len(s) && isASCIIDigit(s[i]) {
			i++
		}
		if i == start || i >= len(s) {
			return -1
		}
		at := strings.IndexByte(designators[from:], s[i])
		if at < 0 {
			return -1
		}
		i++
		return from + at
	}
	// run reads one or more consecutive parts in designator order.
	run := func(designators string) bool {
		at := part(designators, 0)
		if at < 0 {
			return false
		}
		for at+1 < len(designators) && i < len(s) && isASCIIDigit(s[i]) {
			if next := part(designators, at+1); next != at+1 {
				return false
			} else {
				at = next
			}
		}
		return true
	}
	if s[i] == 'T' {
		i++
		return run("HMS") && i == len(s)
	}
	save := i
	if part("W", 0) == 0 {
		return i == len(s)
	}
	i = save
	if !run("YMD") {
		return false
	}
	if i < len(s) && s[i] == 'T' {
		i++
		return run("HMS") && i == len(s)
	}
	return i == len(s)
}

func isHexDigit(c byte) bool {
	return hexValue(c) >= 0
}

func isUUID(s string) bool {
	if len(s) != 36 {
		return false
	}
	for i := 0; i < len(s); i++ {
		if i == 8 || i == 13 || i == 18 || i == 23 {
			if s[i] != '-' {
				return false
			}
		} else if !isHexDigit(s[i]) {
			return false
		}
	}
	return true
}

func isIPv4(s string) bool {
	parts := 0
	for {
		p := s
		dot := strings.IndexByte(s, '.')
		if dot >= 0 {
			p = s[:dot]
		}
		parts++
		if parts > 4 || len(p) == 0 || len(p) > 3 || (len(p) > 1 && p[0] == '0') {
			return false
		}
		if v := digitsValue(p); v < 0 || v > 255 {
			return false
		}
		if dot < 0 {
			return parts == 4
		}
		s = s[dot+1:]
	}
}

// hexGroups is the number of colon-separated groups of one to four hex digits, or -1 when one is not valid.
func hexGroups(s string) int {
	if s == "" {
		return 0
	}
	n := 0
	for {
		p := s
		colon := strings.IndexByte(s, ':')
		if colon >= 0 {
			p = s[:colon]
		}
		if len(p) == 0 || len(p) > 4 {
			return -1
		}
		for i := 0; i < len(p); i++ {
			if !isHexDigit(p[i]) {
				return -1
			}
		}
		n++
		if colon < 0 {
			return n
		}
		s = s[colon+1:]
	}
}

func isIPv6(s string) bool {
	// A valid address is at most 51 characters (an IPv4 tail after six groups), so longer text fails without a copy.
	if len(s) < 2 || len(s) > 64 {
		return false
	}
	hasDot := false
	for i := 0; i < len(s); i++ {
		c := s[i]
		if !(isHexDigit(c) || c == ':' || c == '.') {
			return false
		}
		hasDot = hasDot || c == '.'
	}
	// An IPv4 tail counts as two groups: check it, then read the address with "0:0" in its place.
	var buf [67]byte
	tail := s
	if hasDot {
		lastColon := strings.LastIndexByte(s, ':')
		if lastColon < 0 || !isIPv4(s[lastColon+1:]) {
			return false
		}
		n := copy(buf[:], s[:lastColon+1])
		n += copy(buf[n:], "0:0")
		tail = unsafe.String(&buf[0], n)
	}
	if dbl := strings.Index(tail, "::"); dbl >= 0 {
		if strings.Contains(tail[dbl+1:], "::") {
			return false
		}
		l, r := hexGroups(tail[:dbl]), hexGroups(tail[dbl+2:])
		return l >= 0 && r >= 0 && l+r < 8
	}
	return hexGroups(tail) == 8
}

func isASCIIAlphanumeric(c byte) bool {
	return isASCIILetter(c) || isASCIIDigit(c)
}

func isLDHLabel(l string) bool {
	if len(l) == 0 || len(l) > 63 || !isASCIIAlphanumeric(l[0]) || !isASCIIAlphanumeric(l[len(l)-1]) {
		return false
	}
	for i := 0; i < len(l); i++ {
		if !isASCIIAlphanumeric(l[i]) && l[i] != '-' {
			return false
		}
	}
	return true
}

func startsWithXN(l string) bool {
	return len(l) >= 4 && (l[0]|0x20) == 'x' && (l[1]|0x20) == 'n' && l[2] == '-' && l[3] == '-'
}

func hostnameLabelOK(label string) bool {
	if !isLDHLabel(label) {
		return false
	}
	// "--" in the third and fourth positions is reserved for A-labels (xn--).
	return !(len(label) >= 4 && label[2] == '-' && label[3] == '-' && !startsWithXN(label))
}

// eachLabel calls ok for each dot-separated label, stopping at the first it refuses.
func eachLabel(s string, ok func(string) bool) bool {
	for {
		dot := strings.IndexByte(s, '.')
		if dot < 0 {
			return ok(s)
		}
		if !ok(s[:dot]) {
			return false
		}
		s = s[dot+1:]
	}
}

// isLegacyHostname checks RFC 1123 host names (draft 4 and 6): no IDNA rules for "--" or A-labels.
func isLegacyHostname(s string) bool {
	return len(s) != 0 && len(s) <= 253 && eachLabel(s, isLDHLabel)
}

func isHostname(s string) bool {
	if len(s) == 0 || len(s) > 253 {
		return false
	}
	return eachLabel(s, hostnameLabel)
}

func hostnameLabel(l string) bool {
	return hostnameLabelOK(l) && (!startsWithXN(l) || punycodeLabelOK(l[4:]))
}

func unassigned(r rune) bool {
	return !unicode.In(r, unicode.L, unicode.M, unicode.N, unicode.P, unicode.S, unicode.Z, unicode.Cc, unicode.Cf,
		unicode.Cs, unicode.Co)
}

func isDisallowedException(r rune) bool {
	switch r {
	case 0x06fd, 0x06fe, 0x0f0b, 0x00b7, 0x05f3, 0x05f4, 0x30fb:
		return true
	}
	return false
}

// disallowed reports code points IDNA2008 disallows that the tests exercise: controls, private use, unassigned,
// spaces, uppercase and titlecase letters (mapped away, never PVALID), symbols and punctuation.
func disallowed(label string) bool {
	for _, r := range label {
		if r == '-' || isDisallowedException(r) {
			continue
		}
		if unicode.In(r, unicode.Cc, unicode.Co, unicode.Zs, unicode.Zl, unicode.Zp, unicode.Lu, unicode.Lt,
			unicode.Sm, unicode.So, unicode.P) || unassigned(r) {
			return true
		}
	}
	return false
}

func punycodeLabelOK(encoded string) bool {
	decoded, ok := punycodeDecode(encoded)
	if !ok || len(decoded) == 0 {
		return false
	}
	ascii := true
	for _, r := range decoded {
		ascii = ascii && r < utf8.RuneSelf
	}
	if ascii {
		return false
	}
	// The encoding must be canonical: encoding the U-label again gives the same A-label.
	label := string(decoded)
	if punycodeEncode(label) != strings.ToLower(encoded) {
		return false
	}
	return idnLabelOK(label) && !disallowed(label) && bidiLabelOK(label, bidiDomain(label))
}

const (
	punyBase = 36
	punyTMin = 1
	punyTMax = 26
	punySkew = 38
	punyDamp = 700
)

func punyAdapt(delta, numPoints uint32, firstTime bool) uint32 {
	if firstTime {
		delta /= punyDamp
	} else {
		delta >>= 1
	}
	delta += delta / numPoints
	k := uint32(0)
	for delta > ((punyBase-punyTMin)*punyTMax)>>1 {
		delta /= punyBase - punyTMin
		k += punyBase
	}
	return k + ((punyBase-punyTMin+1)*delta)/(delta+punySkew)
}

func punyThreshold(k, bias uint32) uint32 {
	switch {
	case k <= bias:
		return punyTMin
	case k >= bias+punyTMax:
		return punyTMax
	}
	return k - bias
}

func saturatingAdd(a, b uint32) uint32 {
	if a > math.MaxUint32-b {
		return math.MaxUint32
	}
	return a + b
}

// punycodeEncode is the RFC 3492 encoding, for the canonical round trip and A-label length checks.
func punycodeEncode(input string) string {
	cps := []rune(input)
	var out []byte
	for _, c := range cps {
		if c < utf8.RuneSelf {
			out = append(out, byte(c))
		}
	}
	basicLength := uint32(len(out))
	h := basicLength
	if basicLength > 0 {
		out = append(out, '-')
	}
	n, delta, bias := uint32(128), uint32(0), uint32(72)
	digit := func(d uint32) byte {
		if d < 26 {
			return byte(d + 97)
		}
		return byte(d + 22)
	}
	for int(h) < len(cps) {
		m := uint32(math.MaxUint32)
		for _, c := range cps {
			if uint32(c) >= n && uint32(c) < m {
				m = uint32(c)
			}
		}
		step := uint64(m-n) * uint64(h+1)
		if step > math.MaxUint32 {
			step = math.MaxUint32
		}
		delta = saturatingAdd(delta, uint32(step))
		n = m
		for _, c := range cps {
			if uint32(c) < n {
				delta = saturatingAdd(delta, 1)
			}
			if uint32(c) == n {
				q := delta
				for k := uint32(punyBase); ; k += punyBase {
					t := punyThreshold(k, bias)
					if q < t {
						break
					}
					out = append(out, digit(t+(q-t)%(punyBase-t)))
					q = (q - t) / (punyBase - t)
				}
				out = append(out, digit(q))
				bias = punyAdapt(delta, h+1, h == basicLength)
				delta = 0
				h++
			}
		}
		delta++
		n++
	}
	return string(out)
}

// punycodeDecode is the RFC 3492 decoding, enough to validate A-labels.
func punycodeDecode(input string) ([]rune, bool) {
	n, i, bias := uint32(128), uint32(0), uint32(72)
	var output []rune
	basic := strings.LastIndexByte(input, '-')
	if basic < 0 {
		basic = 0
	}
	for j := 0; j < basic; j++ {
		if input[j] >= 0x80 {
			return nil, false
		}
		output = append(output, rune(input[j]))
	}
	index := 0
	if basic > 0 {
		index = basic + 1
	}
	for index < len(input) {
		oldi := i
		w := uint64(1)
		for k := uint32(punyBase); ; k += punyBase {
			if index >= len(input) {
				return nil, false
			}
			c := input[index]
			index++
			var digit uint32
			switch {
			case c >= '0' && c <= '9':
				digit = uint32(c) - 22
			case c >= 'A' && c <= 'Z':
				digit = uint32(c) - 65
			case c >= 'a' && c <= 'z':
				digit = uint32(c) - 97
			default:
				return nil, false
			}
			next := uint64(i) + uint64(digit)*w
			if next > math.MaxUint32 {
				return nil, false
			}
			i = uint32(next)
			t := punyThreshold(k, bias)
			if digit < t {
				break
			}
			if w *= uint64(punyBase - t); w > math.MaxUint32 {
				return nil, false
			}
		}
		length := uint32(len(output)) + 1
		bias = punyAdapt(i-oldi, length, oldi == 0)
		if uint64(n)+uint64(i/length) > math.MaxUint32 {
			return nil, false
		}
		n += i / length
		i %= length
		if n > unicode.MaxRune || (n >= 0xd800 && n <= 0xdfff) {
			return nil, false
		}
		output = append(output, 0)
		copy(output[i+1:], output[i:])
		output[i] = rune(n)
		i++
	}
	return output, true
}

func isArabicLike(r rune) bool {
	return unicode.In(r, unicode.Arabic, unicode.Syriac, unicode.Thaana, unicode.Nko)
}

type bidi uint8

const (
	bidiL bidi = iota
	bidiR
	bidiAL
	bidiAN
	bidiEN
	bidiNSM
	bidiON
)

// bidiClass approximates the Bidi classes of the RFC 5893 Bidi rule by script and general category.
func bidiClass(c rune) bidi {
	switch {
	case unicode.In(c, unicode.Mn, unicode.Me):
		return bidiNSM
	case (c >= 0x660 && c <= 0x669) || c == 0x66b || c == 0x66c:
		return bidiAN
	case (c >= 0x30 && c <= 0x39) || (c >= 0x6f0 && c <= 0x6f9):
		return bidiEN
	case unicode.Is(unicode.Hebrew, c):
		return bidiR
	case isArabicLike(c):
		return bidiAL
	case unicode.In(c, unicode.L, unicode.Mc):
		return bidiL
	}
	return bidiON
}

func bidiDomain(label string) bool {
	rtl, strong := false, false
	for _, c := range label {
		rtl = rtl || unicode.Is(unicode.Hebrew, c) || isArabicLike(c) || (c >= 0x660 && c <= 0x669) || c == 0x66b ||
			c == 0x66c
		class := bidiClass(c)
		strong = strong || class == bidiR || class == bidiAL || class == bidiAN
	}
	return rtl && strong
}

func bidiLabelOK(label string, isBidiDomain bool) bool {
	if !isBidiDomain {
		return true
	}
	var classes []bidi
	var has [bidiON + 1]bool
	for _, c := range label {
		class := bidiClass(c)
		classes = append(classes, class)
		has[class] = true
	}
	if len(classes) == 0 {
		return false
	}
	last := len(classes) - 1
	for last > 0 && classes[last] == bidiNSM {
		last--
	}
	end := classes[last]
	switch classes[0] {
	case bidiR, bidiAL:
		return !has[bidiL] && (end == bidiR || end == bidiAL || end == bidiEN || end == bidiAN) &&
			!(has[bidiEN] && has[bidiAN])
	case bidiL:
		return !has[bidiR] && !has[bidiAL] && !has[bidiAN] && (end == bidiL || end == bidiEN)
	}
	return false
}

func isVirama(r rune) bool {
	switch r {
	case 0x094d, 0x09cd, 0x0a4d, 0x0acd, 0x0b4d, 0x0bcd, 0x0c4d, 0x0ccd, 0x0d3b, 0x0d3c, 0x0d4d, 0x0dca, 0x0e3a,
		0x0eba, 0x0f84, 0x1039, 0x103a, 0x1714, 0x1734, 0x17d2, 0x1a60, 0x1b44, 0x1baa, 0x1bab, 0x1bf2, 0x1bf3,
		0x2d7f, 0xa806, 0xa8c4, 0xa953, 0xa9c0, 0xaaf6, 0xabed:
		return true
	}
	return false
}

// zwnjJoiningContext approximates (Joining_Type:{L,D})(Joining_Type:T)*ZWNJ(Joining_Type:T)*(Joining_Type:{R,D})
// with Arabic letters.
func zwnjJoiningContext(cps []rune, i int) bool {
	isJoiner := func(c rune) bool {
		return (c >= 0x0620 && c <= 0x064a) || (c >= 0x066e && c <= 0x06d3)
	}
	l := i - 1
	for l >= 0 && unicode.Is(unicode.Mn, cps[l]) {
		l--
	}
	r := i + 1
	for r < len(cps) && unicode.Is(unicode.Mn, cps[r]) {
		r++
	}
	return l >= 0 && r < len(cps) && isJoiner(cps[l]) && isJoiner(cps[r])
}

// idnLabelOK checks the contextual and disallowed code points from RFC 5892 that the test suite exercises.
func idnLabelOK(label string) bool {
	if label == "" || label[0] == '-' || label[len(label)-1] == '-' {
		return false
	}
	cps := []rune(label)
	if len(cps) >= 4 && cps[2] == '-' && cps[3] == '-' {
		return false
	}
	if unicode.Is(unicode.M, cps[0]) {
		return false
	}
	has := func(lo, hi rune) bool {
		for _, c := range cps {
			if c >= lo && c <= hi {
				return true
			}
		}
		return false
	}
	if has(0x660, 0x669) && has(0x6f0, 0x6f9) {
		return false
	}
	for i, c := range cps {
		switch {
		case c == 0x302e || c == 0x302f || c == 0x0640 || c == 0x07fa || (c >= 0x3031 && c <= 0x3035) || c == 0x303b:
			return false
		case c == 0x00b7:
			if !(i > 0 && i < len(cps)-1 && cps[i-1] == 'l' && cps[i+1] == 'l') {
				return false
			}
		case c == 0x0375:
			if !(i < len(cps)-1 && unicode.Is(unicode.Greek, cps[i+1])) {
				return false
			}
		case c == 0x05f3 || c == 0x05f4:
			if !(i > 0 && unicode.Is(unicode.Hebrew, cps[i-1])) {
				return false
			}
		case c == 0x30fb:
			found := false
			for _, d := range cps {
				found = found || (d != 0x30fb && unicode.In(d, unicode.Hiragana, unicode.Katakana, unicode.Han))
			}
			if !found {
				return false
			}
		case c == 0x200d:
			if i == 0 || !isVirama(cps[i-1]) {
				return false
			}
		case c == 0x200c:
			if (i == 0 || !isVirama(cps[i-1])) && !zwnjJoiningContext(cps, i) {
				return false
			}
		}
	}
	return true
}

func isLabelSeparator(r rune) bool {
	// Full stop, ideographic full stop, fullwidth full stop, halfwidth ideographic full stop.
	return r == '.' || r == 0x3002 || r == 0xff0e || r == 0xff61
}

func isIDNHostname(s string) bool {
	if s == "" {
		return false
	}
	labels := splitLabels(s)
	unicodeLabels := make([]string, len(labels))
	isBidi := false
	for i, l := range labels {
		unicodeLabels[i] = l
		if startsWithXN(l) {
			if decoded, ok := punycodeDecode(l[4:]); ok {
				unicodeLabels[i] = string(decoded)
			}
		}
		isBidi = isBidi || bidiDomain(unicodeLabels[i])
	}
	asciiLength := 0
	for i, label := range labels {
		if label == "" {
			return false
		}
		if isASCII([]byte(label)) {
			if !hostnameLabelOK(label) {
				return false
			}
			if startsWithXN(label) && !punycodeLabelOK(label[4:]) {
				return false
			}
			if !bidiLabelOK(unicodeLabels[i], isBidi) {
				return false
			}
			asciiLength += len(label) + 1
		} else {
			if !idnLabelOK(label) {
				return false
			}
			withoutJoiners := strings.Map(func(r rune) rune {
				if r == 0x200c || r == 0x200d {
					return -1
				}
				return r
			}, label)
			for _, r := range withoutJoiners {
				if unicode.In(r, unicode.Cc, unicode.Cf, unicode.Zs) || unassigned(r) {
					return false
				}
			}
			if disallowed(withoutJoiners) {
				return false
			}
			if !bidiLabelOK(label, isBidi) {
				return false
			}
			aLabelLength := 4 + len(punycodeEncode(label))
			if aLabelLength > 63 {
				return false
			}
			asciiLength += aLabelLength + 1
		}
	}
	return asciiLength-1 <= 253
}

// splitLabels splits a host name at every label separator, keeping empty labels.
func splitLabels(s string) []string {
	var labels []string
	start := 0
	for i, r := range s {
		if isLabelSeparator(r) {
			labels = append(labels, s[start:i])
			start = i + utf8.RuneLen(r)
		}
	}
	return append(labels, s[start:])
}

const emailAtoms = "[A-Za-z0-9!#$%&'*+/=?^_`{|}~-]+"
const idnEmailAtoms = "[\\p{L}\\p{M}\\p{N}!#$%&'*+/=?^_`{|}~-]+"
const emailQuoted = `"(?:[^"\\\r\n]|\\.)*"`

var (
	emailLocalRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(`^(?:` + emailAtoms + `(?:\.` + emailAtoms + `)*|` + emailQuoted + `)$`)
	})
	idnEmailLocalRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(`^(?:` + idnEmailAtoms + `(?:\.` + idnEmailAtoms + `)*|` + emailQuoted + `)$`)
	})
)

func isEmail(s string, idn bool) bool {
	at := strings.LastIndexByte(s, '@')
	if at <= 0 {
		return false
	}
	local, domain := s[:at], s[at+1:]
	localOK := false
	if idn {
		localOK = idnEmailLocalRegexp().MatchString(local)
	} else {
		localOK = emailLocalRegexp().MatchString(local)
	}
	if !localOK {
		return false
	}
	if len(domain) >= 2 && domain[0] == '[' && domain[len(domain)-1] == ']' {
		inner := domain[1 : len(domain)-1]
		if len(inner) >= 5 && strings.EqualFold(inner[:5], "IPv6:") {
			return isIPv6(inner[5:])
		}
		return isIPv4(inner)
	}
	if idn {
		return isIDNHostname(domain)
	}
	return isHostname(domain)
}

// uriRegexpSource builds the RFC 3986 (URI) and RFC 3987 (IRI) grammars.
func uriRegexpSource(iri, reference bool) string {
	const (
		hex        = "[0-9A-Fa-f]"
		pct        = "%" + hex + "{2}"
		sub        = `[!$&'()*+,;=]`
		unreserved = `[A-Za-z0-9\-._~]`
		ucschar    = `[\x{A0}-\x{D7FF}\x{F900}-\x{FDCF}\x{FDF0}-\x{FFEF}\x{10000}-\x{EFFFD}]`
		private    = `[\x{E000}-\x{F8FF}\x{F0000}-\x{FFFFD}\x{100000}-\x{10FFFD}]`
		decOctet   = "(?:25[0-5]|2[0-4][0-9]|1[0-9][0-9]|[1-9]?[0-9])"
		ipv4       = decOctet + `(?:\.` + decOctet + "){3}"
		h16        = hex + "{1,4}"
		ls32       = "(?:" + h16 + ":" + h16 + "|" + ipv4 + ")"
		scheme     = `[A-Za-z][A-Za-z0-9+\-.]*`
	)
	unres := unreserved
	if iri {
		unres = "(?:" + unreserved + "|" + ucschar + ")"
	}
	pchar := "(?:" + unres + "|" + pct + "|" + sub + "|[:@])"
	query := "(?:" + pchar + "|[/?])*"
	if iri {
		query = "(?:" + pchar + "|[/?]|" + private + ")*"
	}
	fragment := "(?:" + pchar + "|[/?])*"
	g := "(?:" + h16 + ":)"
	ipv6 := "(?:" + g + "{6}" + ls32 +
		"|::" + g + "{5}" + ls32 +
		"|(?:" + h16 + ")?::" + g + "{4}" + ls32 +
		"|(?:" + g + "{0,1}" + h16 + ")?::" + g + "{3}" + ls32 +
		"|(?:" + g + "{0,2}" + h16 + ")?::" + g + "{2}" + ls32 +
		"|(?:" + g + "{0,3}" + h16 + ")?::" + h16 + ":" + ls32 +
		"|(?:" + g + "{0,4}" + h16 + ")?::" + ls32 +
		"|(?:" + g + "{0,5}" + h16 + ")?::" + h16 +
		"|(?:" + g + "{0,6}" + h16 + ")?::)"
	ipLiteral := `\[(?:` + ipv6 + "|v" + hex + `+\.(?:` + unreserved + "|" + sub + `|:)+)\]`
	regName := "(?:" + unres + "|" + pct + "|" + sub + ")*"
	authority := "(?:(?:" + unres + "|" + pct + "|" + sub + "|:)*@)?(?:" + ipLiteral + "|" + ipv4 + "|" + regName +
		")(?::[0-9]*)?"
	segment := pchar + "*"
	segmentNZ := pchar + "+"
	segmentNZNC := "(?:" + unres + "|" + pct + "|" + sub + "|@)+"
	hierPart := "(?://" + authority + "(?:/" + segment + ")*|/(?:" + segmentNZ + "(?:/" + segment + ")*)?|" +
		segmentNZ + "(?:/" + segment + ")*|)"
	relativePart := "(?://" + authority + "(?:/" + segment + ")*|/(?:" + segmentNZ + "(?:/" + segment + ")*)?|" +
		segmentNZNC + "(?:/" + segment + ")*|)"
	absolute := scheme + ":" + hierPart + `(?:\?` + query + ")?(?:#" + fragment + ")?"
	relative := relativePart + `(?:\?` + query + ")?(?:#" + fragment + ")?"
	body := absolute
	if reference {
		body = absolute + "|" + relative
	}
	return "^(?:" + body + ")$"
}

const uriTemplateVar = `(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2})(?:\.?(?:[A-Za-z0-9_]|%[0-9A-Fa-f]{2}))*(?::[1-9][0-9]{0,3}|\*)?`

var (
	uriRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(uriRegexpSource(false, false))
	})
	uriReferenceRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(uriRegexpSource(false, true))
	})
	iriRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(uriRegexpSource(true, false))
	})
	iriReferenceRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(uriRegexpSource(true, true))
	})
	uriTemplateRegexp = sync.OnceValue(func() *regexp.Regexp {
		return regexp.MustCompile(`^(?:[^\x00-\x20"'<>\\^` + "`" + `{|}]|\{[+#./;?&=,!@|]?` + uriTemplateVar +
			`(?:,` + uriTemplateVar + `)*\})*$`)
	})
)

// isJSONPointer reads an RFC 6901 JSON pointer: segments that each start with "/", where "~" is followed by 0 or 1.
func isJSONPointer(s string) bool {
	if s != "" && s[0] != '/' {
		return false
	}
	for i := 0; i < len(s); i++ {
		if s[i] == '~' {
			if i+1 >= len(s) || (s[i+1] != '0' && s[i+1] != '1') {
				return false
			}
		}
	}
	return true
}

// isRelativeJSONPointer reads a non-negative integer without leading zeros, then "#" or a JSON pointer.
func isRelativeJSONPointer(s string) bool {
	i := 0
	for i < len(s) && isASCIIDigit(s[i]) {
		i++
	}
	if i == 0 || (i > 1 && s[0] == '0') {
		return false
	}
	return s[i:] == "#" || isJSONPointer(s[i:])
}
