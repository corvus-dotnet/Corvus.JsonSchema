package jsonschema

import (
	"strconv"
	"strings"
	"unicode/utf8"
)

// URI handling for schema identification and reference resolution (RFC 3986 section 5). Reference resolution is
// implemented directly so that opaque bases (urn:, tag:) resolve the same way in every port of the evaluator.

type uriParts struct {
	scheme, authority, path, query    string
	hasScheme, hasAuthority, hasQuery bool
}

// parseURI splits a URI into scheme, authority, path and query (the fragment must already be removed).
func parseURI(uri string) uriParts {
	var p uriParts
	rest := uri
	if colon := schemeEnd(rest); colon >= 0 {
		p.scheme, p.hasScheme = rest[:colon], true
		rest = rest[colon+1:]
	}
	if after, ok := strings.CutPrefix(rest, "//"); ok {
		end := strings.IndexAny(after, "/?")
		if end < 0 {
			end = len(after)
		}
		p.authority, p.hasAuthority = after[:end], true
		rest = after[end:]
	}
	if q := strings.IndexByte(rest, '?'); q >= 0 {
		p.path, p.query, p.hasQuery = rest[:q], rest[q+1:], true
	} else {
		p.path = rest
	}
	return p
}

// schemeEnd is the index of the ':' ending a URI scheme, or -1 if the text does not start with one.
func schemeEnd(s string) int {
	if s == "" || !isASCIILetter(s[0]) {
		return -1
	}
	for i := 1; i < len(s); i++ {
		b := s[i]
		if b == ':' {
			return i
		}
		if !(isASCIILetter(b) || isASCIIDigit(b) || b == '+' || b == '-' || b == '.') {
			return -1
		}
	}
	return -1
}

func isASCIILetter(b byte) bool {
	return (b|0x20) >= 'a' && (b|0x20) <= 'z'
}

func isASCIIDigit(b byte) bool {
	return b >= '0' && b <= '9'
}

// hasScheme reports whether the reference starts with a URI scheme.
func hasScheme(reference string) bool {
	return schemeEnd(reference) >= 0
}

// splitFragment splits a reference at its first '#'.
func splitFragment(reference string) (string, string) {
	if i := strings.IndexByte(reference, '#'); i >= 0 {
		return reference[:i], reference[i+1:]
	}
	return reference, ""
}

func (p uriParts) String() string {
	var s strings.Builder
	if p.hasScheme {
		s.WriteString(p.scheme)
		s.WriteByte(':')
	}
	if p.hasAuthority {
		s.WriteString("//")
		s.WriteString(p.authority)
	}
	s.WriteString(p.path)
	if p.hasQuery {
		s.WriteByte('?')
		s.WriteString(p.query)
	}
	return s.String()
}

func removeDotSegments(path string) string {
	if !strings.Contains(path, ".") {
		return path
	}
	input := strings.Split(path, "/")
	out := make([]string, 0, len(input))
	last := len(input) - 1
	for i, seg := range input {
		switch seg {
		case ".":
			if i == last {
				out = append(out, "")
			}
		case "..":
			if len(out) > 1 || (len(out) == 1 && out[0] != "") {
				out = out[:len(out)-1]
			}
			if i == last {
				out = append(out, "")
			}
		default:
			out = append(out, seg)
		}
	}
	return strings.Join(out, "/")
}

func mergePaths(base uriParts, refPath string) string {
	if base.hasAuthority && base.path == "" {
		return "/" + refPath
	}
	if i := strings.LastIndexByte(base.path, '/'); i >= 0 {
		return base.path[:i+1] + refPath
	}
	return refPath
}

func normalizeParts(p uriParts) string {
	if p.hasScheme {
		p.scheme = strings.ToLower(p.scheme)
	}
	p.path = removeDotSegments(p.path)
	if p.hasAuthority {
		a := strings.ToLower(p.authority)
		switch p.scheme {
		case "http":
			a = strings.TrimSuffix(a, ":80")
		case "https":
			a = strings.TrimSuffix(a, ":443")
		}
		p.authority = a
		if p.path == "" {
			p.path = "/"
		}
	}
	return p.String()
}

// normalizeURI normalises an absolute URI (dropping its fragment) so that equivalent spellings compare equal.
func normalizeURI(uri string) string {
	u, _ := splitFragment(uri)
	if !hasScheme(u) {
		return u
	}
	return normalizeParts(parseURI(u))
}

// resolveURI resolves a reference (without fragment) against a base URI, returning the normalised absolute URI.
func resolveURI(baseURI, reference string) string {
	if reference == "" {
		return baseURI
	}
	r := parseURI(reference)
	if r.hasScheme {
		return normalizeParts(r)
	}
	if baseURI == "" {
		return reference
	}
	b := parseURI(baseURI)
	t := uriParts{scheme: b.scheme, hasScheme: b.hasScheme}
	if r.hasAuthority {
		t.authority, t.hasAuthority = r.authority, true
		t.path = removeDotSegments(r.path)
		t.query, t.hasQuery = r.query, r.hasQuery
	} else {
		if r.path == "" {
			t.path = b.path
			if r.hasQuery {
				t.query, t.hasQuery = r.query, true
			} else {
				t.query, t.hasQuery = b.query, b.hasQuery
			}
		} else {
			if strings.HasPrefix(r.path, "/") {
				t.path = removeDotSegments(r.path)
			} else {
				t.path = removeDotSegments(mergePaths(b, r.path))
			}
			t.query, t.hasQuery = r.query, r.hasQuery
		}
		t.authority, t.hasAuthority = b.authority, b.hasAuthority
	}
	if b.hasScheme {
		return normalizeParts(t)
	}
	return t.String()
}

func hexValue(c byte) int {
	switch {
	case c >= '0' && c <= '9':
		return int(c - '0')
	case c >= 'a' && c <= 'f':
		return int(c-'a') + 10
	case c >= 'A' && c <= 'F':
		return int(c-'A') + 10
	}
	return -1
}

// decodeFragment percent-decodes a fragment (invalid escapes leave the text unchanged).
func decodeFragment(fragment string) string {
	if !strings.Contains(fragment, "%") {
		return fragment
	}
	out := make([]byte, 0, len(fragment))
	for i := 0; i < len(fragment); i++ {
		if fragment[i] == '%' {
			if i+2 >= len(fragment) {
				return fragment
			}
			h, l := hexValue(fragment[i+1]), hexValue(fragment[i+2])
			if h < 0 || l < 0 {
				return fragment
			}
			out = append(out, byte(h*16+l))
			i += 2
			continue
		}
		out = append(out, fragment[i])
	}
	if !utf8.Valid(out) {
		return fragment
	}
	return string(out)
}

// escapePointerToken escapes a JSON pointer token (~ as ~0, / as ~1).
func escapePointerToken(token string) string {
	if !strings.ContainsAny(token, "~/") {
		return token
	}
	return strings.ReplaceAll(strings.ReplaceAll(token, "~", "~0"), "/", "~1")
}

// resolvePointer resolves an RFC 6901 JSON pointer against a value of a document, returning the value and its
// normalised pointer, or -1.
func resolvePointer(d *Document, root int, pointer string) (int, string) {
	if pointer == "" {
		return root, ""
	}
	if pointer[0] != '/' {
		return -1, ""
	}
	current := root
	var path strings.Builder
	for _, raw := range strings.Split(pointer[1:], "/") {
		token := strings.ReplaceAll(strings.ReplaceAll(raw, "~1", "/"), "~0", "~")
		switch d.kind(current) {
		case kindArray:
			valid := token == "0" || (token != "" && token[0] != '0' && strings.Trim(token, "0123456789") == "")
			if !valid {
				return -1, ""
			}
			i, err := strconv.Atoi(token)
			if err != nil || i >= d.count(current) {
				return -1, ""
			}
			current = d.first(current) + i
			path.WriteByte('/')
			path.WriteString(token)
		case kindObject:
			current = d.property(current, token)
			if current < 0 {
				return -1, ""
			}
			path.WriteByte('/')
			path.WriteString(escapePointerToken(token))
		default:
			return -1, ""
		}
	}
	return current, path.String()
}
