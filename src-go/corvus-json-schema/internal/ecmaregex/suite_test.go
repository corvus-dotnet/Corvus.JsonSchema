package ecmaregex

import (
	"encoding/json"
	"os"
	"path/filepath"
	"sort"
	"strings"
	"testing"
)

// suiteDir returns the tests directory of the JSON Schema Test Suite, and whether it is there. The suite is a
// submodule at the root of the repository, and CORVUS_JSON_SCHEMA_TEST_SUITE names another copy.
func suiteDir() (string, bool) {
	root := os.Getenv("CORVUS_JSON_SCHEMA_TEST_SUITE")
	if root == "" {
		root = filepath.Join("..", "..", "..", "..", "JSON-Schema-Test-Suite")
	}
	tests := filepath.Join(root, "tests")
	info, err := os.Stat(tests)
	return tests, err == nil && info.IsDir()
}

// suiteRoot returns the tests directory of the suite. It skips the test when there is none, which is the case for a
// copy of the module outside the repository.
func suiteRoot(t *testing.T) string {
	t.Helper()
	tests, ok := suiteDir()
	if !ok {
		t.Skipf("the JSON Schema Test Suite is not at %s", tests)
	}
	return tests
}

// optionalSuiteCorpus is suiteCorpus for a test that also has work to do without the suite.
func optionalSuiteCorpus(t *testing.T) []string {
	t.Helper()
	if _, ok := suiteDir(); !ok {
		return nil
	}
	return suiteCorpus(t)
}

// suiteFiles lists the files of the suite that exercise regular expressions, for every draft that has them.
func suiteFiles(t *testing.T) []string {
	t.Helper()
	root := suiteRoot(t)
	var files []string
	for _, name := range []string{
		filepath.Join("optional", "ecmascript-regex.json"), filepath.Join("optional", "format", "regex.json"),
		"pattern.json", "patternProperties.json", filepath.Join("optional", "non-bmp-regex.json"),
	} {
		found, err := filepath.Glob(filepath.Join(root, "*", name))
		if err != nil {
			t.Fatal(err)
		}
		files = append(files, found...)
	}
	sort.Strings(files)
	if len(files) == 0 {
		t.Fatalf("no regular expression files under %s", root)
	}
	return files
}

type suiteGroup struct {
	Description string `json:"description"`
	Schema      any    `json:"schema"`
	Tests       []struct {
		Description string `json:"description"`
		Data        any    `json:"data"`
		Valid       bool   `json:"valid"`
	} `json:"tests"`
}

// A suiteEvaluator evaluates the few keywords the regular expression files of the suite use, taking every pattern
// through Compile and Match and the regex format through Valid.
type suiteEvaluator struct {
	t        *testing.T
	patterns map[string]*Regexp
}

func (e *suiteEvaluator) pattern(p string) *Regexp {
	if re, ok := e.patterns[p]; ok {
		return re
	}
	re, err := Compile(p)
	if err != nil {
		e.t.Fatalf("Compile(%q): %v", p, err)
	}
	e.patterns[p] = re
	return re
}

func hasType(name string, data any) bool {
	switch name {
	case "string":
		_, ok := data.(string)
		return ok
	case "object":
		_, ok := data.(map[string]any)
		return ok
	case "array":
		_, ok := data.([]any)
		return ok
	case "boolean":
		_, ok := data.(bool)
		return ok
	case "null":
		return data == nil
	case "number":
		_, ok := data.(float64)
		return ok
	case "integer":
		f, ok := data.(float64)
		return ok && f == float64(int64(f))
	case "any":
		return true
	}
	return false
}

func (e *suiteEvaluator) valid(schema, data any) bool {
	if b, ok := schema.(bool); ok {
		return b
	}
	object, ok := schema.(map[string]any)
	if !ok {
		e.t.Fatalf("unexpected schema %v", schema)
	}
	properties, _ := data.(map[string]any)
	for keyword, value := range object {
		switch keyword {
		case "$schema":
		case "type":
			names, _ := value.([]any)
			if name, ok := value.(string); ok {
				names = []any{name}
			}
			matched := false
			for _, name := range names {
				matched = matched || hasType(name.(string), data)
			}
			if !matched {
				return false
			}
		case "maximum":
			if f, ok := data.(float64); ok && f > value.(float64) {
				return false
			}
		case "pattern":
			if s, ok := data.(string); ok && !e.pattern(value.(string)).Match([]byte(s)) {
				return false
			}
		case "format":
			if value != "regex" {
				e.t.Fatalf("unexpected format %v", value)
			}
			if s, ok := data.(string); ok && !Valid(s) {
				return false
			}
		case "patternProperties":
			for p, sub := range value.(map[string]any) {
				re := e.pattern(p)
				for name, property := range properties {
					if re.MatchString(name) && !e.valid(sub, property) {
						return false
					}
				}
			}
		case "additionalProperties":
			patterns, _ := object["patternProperties"].(map[string]any)
			for name, property := range properties {
				additional := true
				for p := range patterns {
					additional = additional && !e.pattern(p).MatchString(name)
				}
				if additional && !e.valid(value, property) {
					return false
				}
			}
		default:
			e.t.Fatalf("unexpected keyword %q", keyword)
		}
	}
	return true
}

// TestSuite runs every case of the suite's regular expression files through Compile, Match and Valid.
func TestSuite(t *testing.T) {
	e := &suiteEvaluator{t: t, patterns: map[string]*Regexp{}}
	cases := 0
	files := suiteFiles(t)
	for _, file := range files {
		data, err := os.ReadFile(file)
		if err != nil {
			t.Fatal(err)
		}
		var groups []suiteGroup
		if err := json.Unmarshal(data, &groups); err != nil {
			t.Fatalf("%s: %v", file, err)
		}
		for _, group := range groups {
			for _, test := range group.Tests {
				cases++
				if got := e.valid(group.Schema, test.Data); got != test.Valid {
					t.Errorf("%s: %s: %s: got %v, want %v", file, group.Description, test.Description, got, test.Valid)
				}
			}
		}
	}
	var engines [2]int
	for _, re := range e.patterns {
		engines[re.Engine()]++
	}
	t.Logf("%d cases in %d files, %d distinct patterns: %d on %v, %d on %v",
		cases, len(files), len(e.patterns), engines[EngineRE2], EngineRE2, engines[EngineBacktrack], EngineBacktrack)
}

// suiteCorpus collects, from every file of the suite, each pattern, each patternProperties name and each string
// instance (the instances of the regex format tests are patterns too, and the rest are arbitrary text to parse).
func suiteCorpus(t *testing.T) []string {
	t.Helper()
	root := suiteRoot(t)
	seen := map[string]bool{}
	var walk func(v any)
	walk = func(v any) {
		switch v := v.(type) {
		case []any:
			for _, item := range v {
				walk(item)
			}
		case map[string]any:
			for name, value := range v {
				if s, ok := value.(string); ok && (name == "pattern" || name == "data") {
					seen[s] = true
				}
				if object, ok := value.(map[string]any); ok && name == "patternProperties" {
					for p := range object {
						seen[p] = true
					}
				}
				walk(value)
			}
		}
	}
	err := filepath.WalkDir(root, func(path string, d os.DirEntry, err error) error {
		if err != nil || d.IsDir() || !strings.HasSuffix(path, ".json") {
			return err
		}
		data, err := os.ReadFile(path)
		if err != nil {
			return err
		}
		var v any
		if err := json.Unmarshal(data, &v); err != nil {
			return err
		}
		walk(v)
		return nil
	})
	if err != nil {
		t.Fatal(err)
	}
	corpus := make([]string, 0, len(seen))
	for s := range seen {
		corpus = append(corpus, s)
	}
	sort.Strings(corpus)
	return corpus
}
