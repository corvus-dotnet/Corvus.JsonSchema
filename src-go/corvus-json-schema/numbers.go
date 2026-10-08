package jsonschema

import (
	"math"
	"math/big"
)

// Exact numeric comparisons over the parser's number representations (an int64, a uint64 beyond an int64, or a
// float64). Integers stay exact and floats compare by value, so 1 == 1.0 and 9007199254740993 > 9007199254740992.0.
// multipleOf is decided exactly on decimal forms, so 0.0075 is a multiple of 0.0001.

const (
	two63 = 0x1p63
	two64 = 0x1p64
)

// compareNumbers compares two numbers exactly: negative, zero or positive.
func compareNumbers(fa uint8, a uint64, fb uint8, b uint64) int {
	switch fa {
	case numInt:
		switch fb {
		case numInt:
			return compareInt(int64(a), int64(b))
		case numFloat:
			return compareIntFloat(int64(a), math.Float64frombits(b))
		}
		return -1
	case numFloat:
		x := math.Float64frombits(a)
		switch fb {
		case numFloat:
			y := math.Float64frombits(b)
			switch {
			case x < y:
				return -1
			case x > y:
				return 1
			}
			return 0
		case numInt:
			return -compareIntFloat(int64(b), x)
		}
		return -compareUintFloat(b, x)
	}
	// A uint64 beyond an int64.
	switch fb {
	case numUint:
		switch {
		case a < b:
			return -1
		case a > b:
			return 1
		}
		return 0
	case numInt:
		return 1
	}
	return compareUintFloat(a, math.Float64frombits(b))
}

func compareInt(a, b int64) int {
	switch {
	case a < b:
		return -1
	case a > b:
		return 1
	}
	return 0
}

// compareIntFloat compares an int64 with a (finite) float64 exactly.
func compareIntFloat(a int64, d float64) int {
	if d >= two63 {
		return -1
	}
	if d < -two63 {
		return 1
	}
	floor := math.Floor(d)
	if c := compareInt(a, int64(floor)); c != 0 {
		return c
	}
	if d > floor {
		return -1
	}
	return 0
}

// compareUintFloat compares a uint64 at or above 2^63 with a (finite) float64 exactly.
func compareUintFloat(u uint64, d float64) int {
	if d >= two64 {
		return -1
	}
	if d < two63 {
		return 1
	}
	// Floats in [2^63, 2^64) are integers.
	v := uint64(d-two63) | 1<<63
	switch {
	case u < v:
		return -1
	case u > v:
		return 1
	}
	return 0
}

// isIntegerNumber reports whether a number is an integer (what the integer type accepts).
func isIntegerNumber(flag uint8, v uint64) bool {
	if flag != numFloat {
		return true
	}
	d := math.Float64frombits(v)
	return d == math.Floor(d) && !math.IsInf(d, 0)
}

// divisor is a multipleOf divisor, as the decimal its JSON text writes: a significand without trailing zeros and an
// exponent. x is a multiple when x / divisor is an integer, decided exactly on the decimal digits of x's own text,
// with integer arithmetic only (no allocation): the C# evaluator's decimal semantics.
type divisor struct {
	isInt bool
	value int64
	// The significand (at most 18 digits), or -1 when the divisor's digits do not fit.
	significand int64
	exponent    int
	// The exact value, for divisors and instances the integer arithmetic cannot decide.
	rational *big.Rat
}

func newDivisor(d *Document, n int) *divisor {
	v := &divisor{isInt: d.flags(n) == numInt, value: int64(d.data(n)), significand: -1}
	if m, e, ok := decimalOf(d.source, d.count(n)); ok {
		v.significand, v.exponent = m, e
	}
	v.rational, _ = new(big.Rat).SetString(string(d.numberText(n)))
	return v
}

// divides is the exact multipleOf of the number value x of a document.
func (v *divisor) divides(d *Document, x int) bool {
	if v.isInt && d.flags(x) == numInt {
		return v.value != 0 && int64(d.data(x))%v.value == 0
	}
	if v.significand == 0 {
		return false
	}
	if v.significand > 0 {
		if r := dividesText(d.source, d.count(x), uint64(v.significand), v.exponent); r >= 0 {
			return r == 1
		}
	}
	// A divisor of more than 18 significant digits, or an exponent out of range.
	n, ok := new(big.Rat).SetString(string(d.numberText(x)))
	if !ok || v.rational == nil || v.rational.Sign() == 0 {
		return false
	}
	return n.Sign() == 0 || n.Quo(n, v.rational).IsInt()
}

// decimalOf reads the decimal of the number text at start: the significand without trailing zeros (at most 18
// digits) and the exponent. Not ok when the significand does not fit or the exponent is out of range.
func decimalOf(b []byte, start int) (m int64, exp int, ok bool) {
	j := start
	if b[j] == '-' {
		j++
	}
	digits := 0
	exponent := int64(0)
	pendingZeros := 0
	fraction := false
	for ; j < len(b); j++ {
		c := b[j]
		if c == '.' {
			fraction = true
			continue
		}
		if c < '0' || c > '9' {
			break
		}
		if fraction {
			exponent--
		}
		if c == '0' {
			// Trailing zeros are held back, so that they move into the exponent if no other digit follows.
			if digits > 0 {
				pendingZeros++
			}
			continue
		}
		for ; pendingZeros > 0; pendingZeros-- {
			if digits >= 18 {
				return 0, 0, false
			}
			m *= 10
			digits++
		}
		if digits >= 18 {
			return 0, 0, false
		}
		m = m*10 + int64(c-'0')
		digits++
	}
	exponent += int64(pendingZeros)
	e, _ := textExponent(b, j)
	exponent += e
	if exponent > math.MaxInt32/2 || exponent < math.MinInt32/2 {
		return 0, 0, false
	}
	return m, int(exponent), true
}

// textExponent reads the exponent part of a number's text at j, if there is one.
func textExponent(b []byte, j int) (int64, int) {
	if j >= len(b) || (b[j] != 'e' && b[j] != 'E') {
		return 0, j
	}
	j++
	negative := false
	if b[j] == '+' || b[j] == '-' {
		negative = b[j] == '-'
		j++
	}
	e := int64(0)
	for ; j < len(b) && b[j] >= '0' && b[j] <= '9'; j++ {
		if e < 1_000_000_000 {
			e = e*10 + int64(b[j]-'0')
		}
	}
	if negative {
		e = -e
	}
	return e, j
}

// dividesText reports whether the number text at start is a multiple of dm * 10^de (dm positive, no trailing
// zeros): 1 or 0, or -1 when the exponents are out of range. The text's digits are streamed modulo what remains of
// the divisor, so the text may have any number of digits.
func dividesText(b []byte, start int, dm uint64, de int) int {
	// x = xm * 10^xe with xm the digit string (trailing zeros moved into xe). x / d is an integer exactly when dm
	// divides xm * 10^(xe - de).
	j := start
	if b[j] == '-' {
		j++
	}
	digitsStart := j
	exponent := int64(0)
	lastNonZero := -1
	fraction := false
	end := j
	for ; end < len(b); end++ {
		c := b[end]
		if c == '.' {
			fraction = true
			continue
		}
		if c < '0' || c > '9' {
			break
		}
		if fraction {
			exponent--
		}
		if c != '0' {
			lastNonZero = end
		}
	}
	if lastNonZero < 0 {
		// Zero is a multiple of everything.
		return 1
	}
	e, _ := textExponent(b, end)
	exponent += e
	// Digits after the last non-zero one are trailing zeros: they move into the exponent.
	for t := lastNonZero + 1; t < end; t++ {
		if b[t] != '.' {
			exponent++
		}
	}
	shift := exponent - int64(de)
	if shift < 0 {
		// dm * 10^-shift must divide xm, which has no trailing zero, so is not a multiple of 10.
		return 0
	}
	if shift > math.MaxInt32 {
		return -1
	}
	// Remove from dm the factors of 2 and 5 that 10^shift supplies. What remains must divide xm.
	rest := dm
	for twos := int64(0); twos < shift && rest&1 == 0; twos++ {
		rest >>= 1
	}
	for fives := int64(0); fives < shift && rest%5 == 0; fives++ {
		rest /= 5
	}
	if rest == 1 {
		return 1
	}
	r := uint64(0)
	for t := digitsStart; t <= lastNonZero; t++ {
		c := b[t]
		if c == '.' {
			continue
		}
		// r < rest <= 10^18, so r * 10 + 9 < 2^64.
		r = (r*10 + uint64(c-'0')) % rest
	}
	if r == 0 {
		return 1
	}
	return 0
}
