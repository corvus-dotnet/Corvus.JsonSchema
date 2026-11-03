package jsonschema

import (
	"strings"
	"testing"
)

// TestUnifiedSuiteFormats holds the cases of the unified JSON-Schema-Test-Suite that the checks got wrong (the
// descriptions are the suite's).
func TestUnifiedSuiteFormats(t *testing.T) {
	for _, c := range []struct {
		format      string
		text        string
		want        bool
		description string
	}{
		{"email", "\"test\\\u00a9\"@iana.org", false, "a non-ASCII character in a quoted pair is not valid"},
		{"email", "\"\\a\"@iana.org", true, "a quoted pair in the local part is valid"},
		{"idn-email", "\u0085@example.com", true, "a C1 control in the local part is valid"},
		{"idn-email", "\uffff@example.com", true, "a noncharacter in the local part is valid"},
		{"idn-hostname", "a\u200bb", true, "a zero width space is ignored by the mapping"},
		{"idn-hostname", strings.Repeat("\uff41", 63), true, "a label of 63 fullwidth characters is valid after mapping"},
		{"idn-hostname", "\uff58\uff4e--nxasmq6b", true, "an ACE prefix produced by the mapping step is decoded as an A-label"},
		{"uri-template", "a'b", true, "an apostrophe in a literal is valid"},
		{"uri-template", "a%", false, "a literal with a lone percent sign is invalid"},
		{"uri-template", "a%4", false, "a literal with an incomplete percent-encoded triplet is invalid"},
		{"uri-template", "a%GG", false, "a literal with non-hex percent encoding is invalid"},
		{"uri-template", "a\u007fb", false, "a delete character in a literal is invalid"},
	} {
		var got bool
		switch c.format {
		case "email":
			got = isEmail(c.text, false)
		case "idn-email":
			got = isEmail(c.text, true)
		case "idn-hostname":
			got = isIDNHostname(c.text)
		case "uri-template":
			got = uriTemplateRegexp().MatchString(c.text)
		}
		if got != c.want {
			t.Errorf("%s: %s: got %v, want %v", c.format, c.description, got, c.want)
		}
	}
}

func TestUts46Map(t *testing.T) {
	for in, want := range map[string]string{
		"example.com": "example.com", "a\u200bb": "ab", "\uff41\uff42": "ab", "ex\u00adample": "example",
		"a\u200db": "a\u200db",
	} {
		if got := uts46Map(in); got != want {
			t.Errorf("uts46Map(%+q) = %+q, want %+q", in, got, want)
		}
	}
	if uts46UnicodeVersion != "17.0.0" {
		t.Errorf("uts46UnicodeVersion = %s", uts46UnicodeVersion)
	}
}
