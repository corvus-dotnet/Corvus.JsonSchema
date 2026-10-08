package ecmaregex

import (
	"sort"
	"sync"

	"github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema/internal/ucd"
)

// maxRune is the last Unicode code point.
const maxRune = ucd.MaxRune

// A charSet is an immutable set of code points. The ranges are sorted, inclusive, and neither overlap nor touch. The
// two words of ascii repeat the membership of the code points below 128 so that the common case is one bit test.
type charSet struct {
	ranges []rune
	ascii  [2]uint64
}

// newCharSet builds a set from inclusive pairs in any order, which may overlap. With negate it builds the complement.
func newCharSet(pairs []rune, negate bool) *charSet {
	n := len(pairs) / 2
	idx := make([]int, n)
	for i := range idx {
		idx[i] = i
	}
	sort.Slice(idx, func(a, b int) bool { return pairs[2*idx[a]] < pairs[2*idx[b]] })
	merged := make([]rune, 0, len(pairs))
	for _, i := range idx {
		lo, hi := pairs[2*i], pairs[2*i+1]
		if m := len(merged); m > 0 && lo <= merged[m-1]+1 {
			if hi > merged[m-1] {
				merged[m-1] = hi
			}
			continue
		}
		merged = append(merged, lo, hi)
	}
	if negate {
		inverse := make([]rune, 0, len(merged)+2)
		next := rune(0)
		for i := 0; i < len(merged); i += 2 {
			if merged[i] > next {
				inverse = append(inverse, next, merged[i]-1)
			}
			next = merged[i+1] + 1
		}
		if next <= maxRune {
			inverse = append(inverse, next, maxRune)
		}
		merged = inverse
	}
	s := &charSet{ranges: merged}
	for i := 0; i < len(merged) && merged[i] < 128; i += 2 {
		for c := merged[i]; c <= merged[i+1] && c < 128; c++ {
			s.ascii[c>>6] |= 1 << (uint(c) & 63)
		}
	}
	return s
}

// contains reports whether the set holds the code point.
func (s *charSet) contains(r rune) bool {
	if r < 128 {
		return s.ascii[r>>6]>>(uint(r)&63)&1 != 0
	}
	ranges := s.ranges
	lo, hi := 0, len(ranges)/2
	for lo < hi {
		mid := int(uint(lo+hi) >> 1)
		if r > ranges[2*mid+1] {
			lo = mid + 1
		} else if r < ranges[2*mid] {
			hi = mid
		} else {
			return true
		}
	}
	return false
}

// empty reports whether the set holds no code point.
func (s *charSet) empty() bool { return len(s.ranges) == 0 }

// The classes ECMA-262 defines. \d and \w are ASCII only, \s is WhiteSpace and LineTerminator, and . is everything
// but the four line terminators.
var (
	digitPairs = []rune{'0', '9'}
	wordPairs  = []rune{'0', '9', 'A', 'Z', '_', '_', 'a', 'z'}
	spacePairs = []rune{
		0x9, 0xD, 0x20, 0x20, 0xA0, 0xA0, 0x1680, 0x1680, 0x2000, 0x200A, 0x2028, 0x2029, 0x202F, 0x202F, 0x205F, 0x205F,
		0x3000, 0x3000, 0xFEFF, 0xFEFF,
	}
	lineTerminatorPairs = []rune{'\n', '\n', '\r', '\r', 0x2028, 0x2029}

	dotSet = newCharSet(lineTerminatorPairs, true)
)

// isWordByte reports whether a byte is an ECMA-262 word character. Every word character is ASCII, so a word boundary
// can be decided from the bytes on either side of a position.
func isWordByte(b byte) bool {
	return b >= 'a' && b <= 'z' || b >= 'A' && b <= 'Z' || b >= '0' && b <= '9' || b == '_'
}

// categoryNames maps the long names and the aliases of the General_Category values to their short names.
var categoryNames = map[string]string{
	"Letter": "L", "Lowercase_Letter": "Ll", "Uppercase_Letter": "Lu", "Titlecase_Letter": "Lt", "Cased_Letter": "LC",
	"Modifier_Letter": "Lm", "Other_Letter": "Lo", "Mark": "M", "Combining_Mark": "M", "Nonspacing_Mark": "Mn",
	"Spacing_Mark": "Mc", "Enclosing_Mark": "Me", "Number": "N", "Decimal_Number": "Nd", "digit": "Nd",
	"Letter_Number": "Nl", "Other_Number": "No", "Punctuation": "P", "punct": "P", "Connector_Punctuation": "Pc",
	"Dash_Punctuation": "Pd", "Open_Punctuation": "Ps", "Close_Punctuation": "Pe", "Initial_Punctuation": "Pi",
	"Final_Punctuation": "Pf", "Other_Punctuation": "Po", "Symbol": "S", "Math_Symbol": "Sm", "Currency_Symbol": "Sc",
	"Modifier_Symbol": "Sk", "Other_Symbol": "So", "Separator": "Z", "Space_Separator": "Zs", "Line_Separator": "Zl",
	"Paragraph_Separator": "Zp", "Other": "C", "Control": "Cc", "cntrl": "Cc", "Format": "Cf", "Surrogate": "Cs",
	"Private_Use": "Co", "Unassigned": "Cn",
	"L": "L", "Ll": "Ll", "Lu": "Lu", "Lt": "Lt", "LC": "LC", "Lm": "Lm", "Lo": "Lo", "M": "M", "Mn": "Mn", "Mc": "Mc",
	"Me": "Me", "N": "N", "Nd": "Nd", "Nl": "Nl", "No": "No", "P": "P", "Pc": "Pc", "Pd": "Pd", "Ps": "Ps", "Pe": "Pe",
	"Pi": "Pi", "Pf": "Pf", "Po": "Po", "S": "S", "Sm": "Sm", "Sc": "Sc", "Sk": "Sk", "So": "So", "Z": "Z", "Zs": "Zs",
	"Zl": "Zl", "Zp": "Zp", "C": "C", "Cc": "Cc", "Cf": "Cf", "Cs": "Cs", "Co": "Co", "Cn": "Cn",
}

// binaryNames maps the names and the aliases of the binary properties ECMA-262 lists to their canonical names.
var binaryNames = map[string]string{
	"ASCII": "ASCII", "ASCII_Hex_Digit": "ASCII_Hex_Digit", "AHex": "ASCII_Hex_Digit", "Alphabetic": "Alphabetic",
	"Alpha": "Alphabetic", "Any": "Any", "Assigned": "Assigned", "Bidi_Control": "Bidi_Control",
	"Bidi_C": "Bidi_Control", "Bidi_Mirrored": "Bidi_Mirrored", "Bidi_M": "Bidi_Mirrored",
	"Case_Ignorable": "Case_Ignorable", "CI": "Case_Ignorable", "Cased": "Cased",
	"Changes_When_Casefolded": "Changes_When_Casefolded",
	"CWCF":                    "Changes_When_Casefolded", "Changes_When_Casemapped": "Changes_When_Casemapped",
	"CWCM": "Changes_When_Casemapped", "Changes_When_Lowercased": "Changes_When_Lowercased",
	"CWL": "Changes_When_Lowercased", "Changes_When_NFKC_Casefolded": "Changes_When_NFKC_Casefolded",
	"CWKCF": "Changes_When_NFKC_Casefolded", "Changes_When_Titlecased": "Changes_When_Titlecased",
	"CWT": "Changes_When_Titlecased", "Changes_When_Uppercased": "Changes_When_Uppercased",
	"CWU": "Changes_When_Uppercased", "Dash": "Dash", "Default_Ignorable_Code_Point": "Default_Ignorable_Code_Point",
	"DI": "Default_Ignorable_Code_Point", "Deprecated": "Deprecated", "Dep": "Deprecated", "Diacritic": "Diacritic",
	"Dia": "Diacritic", "Emoji": "Emoji", "Emoji_Component": "Emoji_Component", "EComp": "Emoji_Component",
	"Emoji_Modifier": "Emoji_Modifier", "EMod": "Emoji_Modifier", "Emoji_Modifier_Base": "Emoji_Modifier_Base",
	"EBase": "Emoji_Modifier_Base", "Emoji_Presentation": "Emoji_Presentation", "EPres": "Emoji_Presentation",
	"Extended_Pictographic": "Extended_Pictographic", "ExtPict": "Extended_Pictographic", "Extender": "Extender",
	"Ext": "Extender", "Grapheme_Base": "Grapheme_Base", "Gr_Base": "Grapheme_Base",
	"Grapheme_Extend": "Grapheme_Extend", "Gr_Ext": "Grapheme_Extend", "Hex_Digit": "Hex_Digit", "Hex": "Hex_Digit",
	"IDS_Binary_Operator": "IDS_Binary_Operator", "IDSB": "IDS_Binary_Operator",
	"IDS_Trinary_Operator": "IDS_Trinary_Operator", "IDST": "IDS_Trinary_Operator", "ID_Continue": "ID_Continue",
	"IDC": "ID_Continue", "ID_Start": "ID_Start", "IDS": "ID_Start", "Ideographic": "Ideographic",
	"Ideo": "Ideographic", "Join_Control": "Join_Control", "Join_C": "Join_Control",
	"Logical_Order_Exception": "Logical_Order_Exception", "LOE": "Logical_Order_Exception", "Lowercase": "Lowercase",
	"Lower": "Lowercase", "Math": "Math", "Noncharacter_Code_Point": "Noncharacter_Code_Point",
	"NChar": "Noncharacter_Code_Point", "Pattern_Syntax": "Pattern_Syntax", "Pat_Syn": "Pattern_Syntax",
	"Pattern_White_Space": "Pattern_White_Space", "Pat_WS": "Pattern_White_Space", "Quotation_Mark": "Quotation_Mark",
	"QMark": "Quotation_Mark", "Radical": "Radical", "Regional_Indicator": "Regional_Indicator",
	"RI": "Regional_Indicator", "Sentence_Terminal": "Sentence_Terminal", "STerm": "Sentence_Terminal",
	"Soft_Dotted": "Soft_Dotted", "SD": "Soft_Dotted", "Terminal_Punctuation": "Terminal_Punctuation",
	"Term": "Terminal_Punctuation", "Unified_Ideograph": "Unified_Ideograph", "UIdeo": "Unified_Ideograph",
	"Uppercase": "Uppercase", "Upper": "Uppercase", "Variation_Selector": "Variation_Selector",
	"VS": "Variation_Selector", "White_Space": "White_Space", "space": "White_Space", "XID_Continue": "XID_Continue",
	"XIDC": "XID_Continue", "XID_Start": "XID_Start", "XIDS": "XID_Start",
}

// propertyCache holds the sets already built for property expressions. A property set can have hundreds of ranges,
// and schemas tend to repeat the same few.
var propertyCache sync.Map

// propertySet resolves the expression between the braces of \p{...} to its set. It reports false for a name or value
// that ECMA-262 does not define.
func propertySet(expr string) (*charSet, bool) {
	if cached, ok := propertyCache.Load(expr); ok {
		return cached.(*charSet), true
	}
	pairs, ok := propertyPairs(expr)
	if !ok {
		return nil, false
	}
	set := newCharSet(pairs, false)
	propertyCache.Store(expr, set)
	return set, true
}

// propertyPairs returns the ranges of a property expression. Every range comes from the tables of the ucd package,
// which hold one fixed version of Unicode, so the answer does not depend on the Go toolchain.
func propertyPairs(expr string) ([]rune, bool) {
	name, value, hasValue := expr, "", false
	for i := 0; i < len(expr); i++ {
		if expr[i] == '=' {
			name, value, hasValue = expr[:i], expr[i+1:], true
			break
		}
	}
	if hasValue {
		switch name {
		case "General_Category", "gc":
			return categoryPairs(value)
		case "Script", "sc":
			return tablePairs(ucd.Script(value))
		case "Script_Extensions", "scx":
			return tablePairs(ucd.ScriptExtensions(value))
		}
		return nil, false
	}
	if pairs, ok := categoryPairs(name); ok {
		return pairs, true
	}
	canonical, ok := binaryNames[name]
	if !ok {
		return nil, false
	}
	switch canonical {
	case "ASCII":
		return []rune{0, 0x7F}, true
	case "Any":
		return []rune{0, maxRune}, true
	}
	return tablePairs(ucd.Binary(canonical))
}

func categoryPairs(value string) ([]rune, bool) {
	short, ok := categoryNames[value]
	if !ok {
		return nil, false
	}
	return tablePairs(ucd.Category(short))
}

// tablePairs returns the ranges of a table as inclusive pairs. It reports false for no table.
func tablePairs(t *ucd.Table) ([]rune, bool) {
	if t == nil {
		return nil, false
	}
	return t.AppendRanges(nil), true
}
