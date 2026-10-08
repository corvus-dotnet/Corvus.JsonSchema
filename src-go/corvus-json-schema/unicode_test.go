package jsonschema

import (
	"go/ast"
	goparser "go/parser"
	"go/token"
	"io/fs"
	"path/filepath"
	"regexp"
	"strconv"
	"strings"
	"testing"
)

// BenchmarkUnicodeFormats checks the formats whose rules read Unicode properties.
func BenchmarkUnicodeFormats(b *testing.B) {
	b.Run("idn-hostname", func(b *testing.B) {
		b.ReportAllocs()
		for i := 0; i < b.N; i++ {
			if !isIDNHostname("ελληνικά.例え.بيروت.example") {
				b.Fatal("not a host name")
			}
		}
	})
	b.Run("idn-email", func(b *testing.B) {
		b.ReportAllocs()
		for i := 0; i < b.N; i++ {
			if !isEmail("δοκιμή.例え@παράδειγμα.example", true) {
				b.Fatal("not an address")
			}
		}
	})
}

// TestNoToolchainUnicodeData fails if a file of the module that is not a test takes Unicode data from the Go
// toolchain. That data is a different version of Unicode in different toolchains (Unicode 15 in Go 1.25 and 1.26,
// Unicode 17 in Go 1.27), so a result that rests on it changes with the toolchain. The module reads internal/ucd
// instead. The test refuses three things.
//
//   - An import of the unicode package. unicode/utf8 and unicode/utf16 hold no character data and are allowed.
//   - A Unicode class (\p or \P) or a case-insensitive flag in a string of a file that imports the regexp package,
//     since regexp reads both from the unicode package.
//   - The functions of strings and bytes that map or fold case, which do the same.
func TestNoToolchainUnicodeData(t *testing.T) {
	unicodeClass := regexp.MustCompile(`\\[pP][{A-Za-z]|\(\?[a-zA-Z]*i`)
	caseFunctions := map[string]bool{
		"ToLower": true, "ToUpper": true, "ToTitle": true, "Title": true, "EqualFold": true,
		"ToLowerSpecial": true, "ToUpperSpecial": true, "ToTitleSpecial": true,
	}
	files := 0
	err := filepath.WalkDir(".", func(path string, entry fs.DirEntry, err error) error {
		if err != nil {
			return err
		}
		if entry.IsDir() {
			if name := entry.Name(); path != "." && (name == "testdata" || strings.HasPrefix(name, ".")) {
				return filepath.SkipDir
			}
			return nil
		}
		if !strings.HasSuffix(path, ".go") || strings.HasSuffix(path, "_test.go") {
			return nil
		}
		files++
		set := token.NewFileSet()
		file, err := goparser.ParseFile(set, path, nil, goparser.SkipObjectResolution)
		if err != nil {
			return err
		}
		usesRegexp := false
		packages := map[string]string{}
		for _, spec := range file.Imports {
			imported, _ := strconv.Unquote(spec.Path.Value)
			switch imported {
			case "unicode":
				t.Errorf("%s imports the unicode package. Use internal/ucd.", set.Position(spec.Pos()))
			case "regexp", "regexp/syntax":
				usesRegexp = true
			case "strings", "bytes":
				name := imported
				if spec.Name != nil {
					name = spec.Name.Name
				}
				packages[name] = imported
			}
		}
		ast.Inspect(file, func(n ast.Node) bool {
			switch n := n.(type) {
			case *ast.BasicLit:
				if usesRegexp && n.Kind == token.STRING {
					if text, err := strconv.Unquote(n.Value); err == nil && unicodeClass.MatchString(text) {
						t.Errorf("%s: %s has a Unicode class or a case-insensitive flag in a file that uses regexp. "+
							"Write the ranges of internal/ucd out.", set.Position(n.Pos()), n.Value)
					}
				}
			case *ast.SelectorExpr:
				if x, ok := n.X.(*ast.Ident); ok && packages[x.Name] != "" && caseFunctions[n.Sel.Name] {
					t.Errorf("%s: %s.%s reads the case tables of the toolchain.", set.Position(n.Pos()), packages[x.Name],
						n.Sel.Name)
				}
			}
			return true
		})
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	if files < 20 {
		t.Fatalf("only %d files were read", files)
	}
}

// TestUnicodeFormatsUseUnicode17 checks format answers that differ between Unicode 15, which the unicode package of
// Go 1.25 and 1.26 holds, and the Unicode 17 of internal/ucd.
func TestUnicodeFormatsUseUnicode17(t *testing.T) {
	// U+10D4A and U+10D4B are Garay letters, assigned in Unicode 16. U+1C89 is an uppercase letter of Unicode 16, and
	// IDNA2008 disallows uppercase letters. U+2FFFF is a noncharacter in every version.
	for _, c := range []struct {
		host string
		want bool
	}{
		{"\U00010D4A\U00010D4B.example", true}, {"\u1C8A.example", true}, {"\u1C89.example", false},
		{"\U0002FFFF.example", false},
	} {
		if got := isIDNHostname(c.host); got != c.want {
			t.Errorf("isIDNHostname(%+q) = %v, want %v", c.host, got, c.want)
		}
	}
	for _, c := range []struct {
		address string
		want    bool
	}{
		{"\U00010D4A\U00010D4B@example.com", true}, {"\U00016EA0\U00016EBB@example.com", true},
		{"\U0002FFFF@example.com", false}, {"δοκιμή@example.com", true}, {"a b@example.com", false},
	} {
		if got := isEmail(c.address, true); got != c.want {
			t.Errorf("isEmail(%+q) = %v, want %v", c.address, got, c.want)
		}
	}
	// Only the letters A to Z are lowered when a URI is normalized.
	if got := asciiLower("HTTP://ÉXAMPLE.Com/İ"); got != "http://Éxample.com/İ" {
		t.Errorf("asciiLower gives %q", got)
	}
}
