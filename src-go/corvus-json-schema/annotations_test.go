package jsonschema

import (
	"encoding/json"
	"os"
	"path/filepath"
	"slices"
	"testing"
)

// Runs the JSON-Schema-Test-Suite annotation tests (JSON-Schema-Test-Suite/annotations) through a verbose results
// collector, as the C# and Rust annotation tests do: every draft, cases filtered by "compatibility", and each
// assertion compared with the annotations grouped by instance location, keyword and schema location.

var compatibilityOrder = []string{"3", "4", "6", "7", "2019", "2020"}

func compatible(level, compatibility string) bool {
	at := slices.Index(compatibilityOrder, level)
	if len(compatibility) > 2 && compatibility[:2] == "<=" {
		limit := slices.Index(compatibilityOrder, compatibility[2:])
		return limit >= 0 && at <= limit
	}
	least := slices.Index(compatibilityOrder, compatibility)
	return least >= 0 && at >= least
}

type annotationSuite struct {
	Suite []struct {
		Description   string          `json:"description"`
		Compatibility string          `json:"compatibility"`
		Schema        json.RawMessage `json:"schema"`
		Tests         []struct {
			Instance   json.RawMessage `json:"instance"`
			Assertions []struct {
				Location string                     `json:"location"`
				Keyword  string                     `json:"keyword"`
				Expected map[string]json.RawMessage `json:"expected"`
			} `json:"assertions"`
		} `json:"tests"`
	} `json:"suite"`
}

// sameAnnotations compares the produced annotations of a keyword at a location with the expected ones: the same
// schema locations, each with an equal JSON value (numbers by value, objects unordered).
func sameAnnotations(actual map[string]string, expected map[string]json.RawMessage) bool {
	if len(actual) != len(expected) {
		return false
	}
	for location, want := range expected {
		got, ok := actual[location]
		if !ok {
			return false
		}
		a, errA := ParseDocumentString(got)
		b, errB := ParseDocument(want)
		if errA != nil || errB != nil || !valuesEqual(a, a.root, b, b.root) {
			return false
		}
	}
	return true
}

func TestAnnotationSuite(t *testing.T) {
	root := suiteRoot()
	dir := filepath.Join(root, "annotations", "tests")
	if _, err := os.Stat(dir); err != nil {
		t.Fatalf("annotation tests not found at %s (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)", dir)
	}
	resolver := remoteResolver(root)
	drafts := []struct {
		name    string
		dialect Dialect
		level   string
	}{
		{"draft4", Draft4, "4"}, {"draft6", Draft6, "6"}, {"draft7", Draft7, "7"},
		{"draft2019-09", Draft201909, "2019"}, {"draft2020-12", Draft202012, "2020"},
	}
	total, failed := 0, 0
	for _, draft := range drafts {
		for _, file := range jsonFiles(t, dir) {
			text, err := os.ReadFile(file)
			if err != nil {
				t.Fatal(err)
			}
			var suite annotationSuite
			if err := json.Unmarshal(text, &suite); err != nil {
				t.Fatalf("%s: %v", file, err)
			}
			name := draft.name + "/" + filepath.Base(file)
			for _, group := range suite.Suite {
				if group.Compatibility != "" && !compatible(draft.level, group.Compatibility) {
					continue
				}
				validator, err := Compile(group.Schema, WithDefaultDialect(draft.dialect), WithDocumentResolver(resolver))
				if err != nil {
					failed++
					t.Errorf("%s [%s]: compile error: %v", name, group.Description, err)
					continue
				}
				for _, test := range group.Tests {
					collector := NewResultsCollector(Verbose)
					if _, err := validator.EvaluateBytes(test.Instance, collector); err != nil {
						t.Fatalf("%s [%s]: %v", name, group.Description, err)
					}
					produced := collector.CollectAnnotations()
					for _, assertion := range test.Assertions {
						total++
						actual := produced[assertion.Location][assertion.Keyword]
						if !sameAnnotations(actual, assertion.Expected) {
							failed++
							t.Errorf("%s [%s] instance %s '%s' %s: expected %v, actual %v", name, group.Description,
								test.Instance, assertion.Location, assertion.Keyword, assertion.Expected, actual)
						}
					}
				}
			}
		}
	}
	t.Logf("%d/%d annotation assertions passed", total-failed, total)
	if total == 0 {
		t.Error("no annotation assertions ran")
	}
}
