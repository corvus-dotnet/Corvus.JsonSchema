package jsonschema

import (
	"encoding/json"
	"fmt"
	"os"
	"path/filepath"
	"reflect"
	"sort"
	"strings"
	"sync"
	"testing"
)

// Runs the JSON-Schema-Test-Suite (the repository's submodule) against the evaluator, as the C# and Rust suite
// runners do: required and optional tests with format as an annotation, optional/format with format asserted. Every
// case runs fail-fast (as a document, as bytes and as a string) and through a results collector at each level.
//
// Set JSON_SCHEMA_TEST_SUITE to use a different checkout, SUITE_DRAFT and SUITE_FILTER to narrow the run.

var suiteDrafts = []struct {
	name    string
	dialect Dialect
}{
	{"draft4", Draft4}, {"draft6", Draft6}, {"draft7", Draft7}, {"draft2019-09", Draft201909},
	{"draft2020-12", Draft202012},
}

// Exclusions, matching the C# and Rust runners: zero-terminated floats.
var suiteExcludedFiles = map[string]bool{"draft4/optional/zeroTerminatedFloats.json": true}

func suiteRoot() string {
	if root := os.Getenv("JSON_SCHEMA_TEST_SUITE"); root != "" {
		return root
	}
	return filepath.Join("..", "..", "JSON-Schema-Test-Suite")
}

func jsonFiles(t *testing.T, dir string) []string {
	entries, err := os.ReadDir(dir)
	if err != nil {
		return nil
	}
	var out []string
	for _, entry := range entries {
		if !entry.IsDir() && strings.HasSuffix(entry.Name(), ".json") {
			out = append(out, filepath.Join(dir, entry.Name()))
		}
	}
	sort.Strings(out)
	return out
}

// remoteResolver resolves the suite's remotes (http://localhost:1234/...) from its remotes directory.
func remoteResolver(root string) DocumentResolver {
	remotes := filepath.Join(root, "remotes")
	var mu sync.Mutex
	cache := make(map[string]*Document)
	return func(uri string) *Document {
		rest, ok := strings.CutPrefix(uri, "http://localhost:1234/")
		if !ok {
			return nil
		}
		mu.Lock()
		defer mu.Unlock()
		if doc, ok := cache[rest]; ok {
			return doc
		}
		text, err := os.ReadFile(filepath.Join(remotes, filepath.FromSlash(rest)))
		if err != nil {
			return nil
		}
		doc, err := ParseDocument(text)
		if err != nil {
			return nil
		}
		cache[rest] = doc
		return doc
	}
}

type suiteGroup struct {
	Description string          `json:"description"`
	Schema      json.RawMessage `json:"schema"`
	Tests       []struct {
		Description string          `json:"description"`
		Data        json.RawMessage `json:"data"`
		Valid       bool            `json:"valid"`
	} `json:"tests"`
}

var resultsLevels = []ResultsLevel{Basic, Detailed, Verbose}

// runSuiteCase evaluates one instance every way the API offers and checks that they agree.
func runSuiteCase(v *Validator, data []byte) (result bool, err error) {
	defer func() {
		if r := recover(); r != nil {
			err = fmt.Errorf("panic during evaluation: %v", r)
		}
	}()
	document, err := ParseDocument(data)
	if err != nil {
		return false, fmt.Errorf("document: %w", err)
	}
	fast, err := v.Validate(document)
	if err != nil {
		return false, fmt.Errorf("fast: %w", err)
	}
	if v.IsValid(document) != fast {
		return false, fmt.Errorf("IsValid disagrees with Validate (%v)", fast)
	}
	var verbose []SchemaResult
	for _, level := range resultsLevels {
		c := NewResultsCollector(level)
		ok, err := v.Evaluate(document, c)
		if err != nil {
			return false, fmt.Errorf("%v: %w", level, err)
		}
		if ok != fast {
			return false, fmt.Errorf("%v returned %v, fast returned %v", level, ok, fast)
		}
		summary := false
		for _, r := range c.Results() {
			summary = summary || (r.EvaluationLocation == "" && r.DocumentEvaluationLocation == "" && r.IsMatch == ok)
		}
		if !summary {
			return false, fmt.Errorf("%v: no root summary row matching the result", level)
		}
		verbose = c.Results()
	}
	// The same instance straight from the text, in the validator's reused buffers.
	fromBytes, err := v.ValidateBytes(data)
	if err != nil {
		return false, fmt.Errorf("ValidateBytes: %w", err)
	}
	if fromBytes != fast || v.IsValidBytes(data) != fast {
		return false, fmt.Errorf("ValidateBytes returned %v, the document %v", fromBytes, fast)
	}
	fromString, err := v.ValidateString(string(data))
	if err != nil {
		return false, fmt.Errorf("ValidateString: %w", err)
	}
	if fromString != fast || v.IsValidString(string(data)) != fast {
		return false, fmt.Errorf("ValidateString returned %v, the document %v", fromString, fast)
	}
	c := NewResultsCollector(Verbose)
	if _, err := v.EvaluateBytes(data, c); err != nil {
		return false, fmt.Errorf("EvaluateBytes: %w", err)
	}
	if !reflect.DeepEqual(verbose, c.Results()) {
		return false, fmt.Errorf("EvaluateBytes's verbose results differ from the document's")
	}
	return fast, nil
}

type suiteRunner struct {
	resolver DocumentResolver
	filter   string
	total    int
	failures []string
	// Failing leap second cases of the format run, which are not counted as failures.
	skipped []string
	areas   []string
	passed  map[string]int
	counted map[string]int
}

func compileSuiteSchema(schema []byte, options ...Option) (v *Validator, err error) {
	defer func() {
		if r := recover(); r != nil {
			err = fmt.Errorf("panic during compilation: %v", r)
		}
	}()
	return Compile(schema, options...)
}

func (r *suiteRunner) runFile(t *testing.T, dialect Dialect, file, label, area string, assertFormat bool) {
	text, err := os.ReadFile(file)
	if err != nil {
		t.Fatalf("%s: %v", file, err)
	}
	var groups []suiteGroup
	if err := json.Unmarshal(text, &groups); err != nil {
		t.Fatalf("%s: %v", file, err)
	}
	if _, ok := r.counted[area]; !ok {
		r.areas = append(r.areas, area)
	}
	for _, group := range groups {
		if r.filter != "" && !strings.Contains(group.Description, r.filter) && !strings.Contains(label, r.filter) {
			continue
		}
		options := []Option{WithDefaultDialect(dialect), WithDocumentResolver(r.resolver)}
		if assertFormat {
			options = append(options, WithAssertFormat(true))
		}
		validator, compileErr := compileSuiteSchema(group.Schema, options...)
		for _, test := range group.Tests {
			r.total++
			r.counted[area]++
			var actual bool
			err := compileErr
			if err != nil {
				err = fmt.Errorf("compile error: %w", err)
			} else {
				actual, err = runSuiteCase(validator, test.Data)
			}
			if err == nil && actual == test.Valid {
				r.passed[area]++
				continue
			}
			// Leap seconds are skipped in the format run, as in the C# runner.
			if assertFormat && strings.Contains(strings.ToLower(test.Description), "leap second") {
				r.passed[area]++
				r.skipped = append(r.skipped, label+" ["+group.Description+"] "+test.Description)
				continue
			}
			got := fmt.Sprint(actual)
			if err != nil {
				got = err.Error()
			}
			r.failures = append(r.failures, fmt.Sprintf("%s [%s] %s: expected %v, got %s", label, group.Description,
				test.Description, test.Valid, got))
		}
	}
}

func TestJSONSchemaTestSuite(t *testing.T) {
	root := suiteRoot()
	tests := filepath.Join(root, "tests")
	if _, err := os.Stat(tests); err != nil {
		t.Fatalf("JSON-Schema-Test-Suite not found at %s (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)", root)
	}
	runner := &suiteRunner{
		resolver: remoteResolver(root), filter: os.Getenv("SUITE_FILTER"),
		passed: make(map[string]int), counted: make(map[string]int),
	}
	draftFilter := os.Getenv("SUITE_DRAFT")
	for _, draft := range suiteDrafts {
		if draftFilter != "" && draftFilter != draft.name {
			continue
		}
		dir := filepath.Join(tests, draft.name)
		for _, f := range jsonFiles(t, dir) {
			runner.runFile(t, draft.dialect, f, draft.name+"/"+filepath.Base(f), draft.name, false)
		}
		for _, f := range jsonFiles(t, filepath.Join(dir, "optional")) {
			label := draft.name + "/optional/" + filepath.Base(f)
			if !suiteExcludedFiles[label] {
				runner.runFile(t, draft.dialect, f, label, draft.name+"/optional", false)
			}
		}
		for _, f := range jsonFiles(t, filepath.Join(dir, "optional", "format")) {
			label := draft.name + "/optional/format/" + filepath.Base(f)
			runner.runFile(t, draft.dialect, f, label, draft.name+"/optional/format", true)
		}
	}
	for _, line := range runner.failures {
		t.Log(line)
	}
	for _, line := range runner.skipped {
		t.Log("skipped: " + line)
	}
	for _, area := range runner.areas {
		t.Logf("%-34s %5d/%d", area, runner.passed[area], runner.counted[area])
	}
	failed := len(runner.failures)
	fmt.Printf("JSON-Schema-Test-Suite: %d/%d passed\n", runner.total-failed, runner.total)
	if failed != 0 {
		t.Errorf("%d JSON-Schema-Test-Suite cases failed", failed)
	}
	if runner.total == 0 {
		t.Errorf("no JSON-Schema-Test-Suite cases ran")
	}
}
