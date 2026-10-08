package jsonschema

import (
	"bytes"
	"encoding/json"
	"os"
	"path/filepath"
	"sort"
	"strconv"
	"strings"
	"testing"
)

// Differential testing of the fail-fast plans (fused objects, type dispatch, name tables and the rest) against the
// collecting evaluator, which walks the general keyword-by-keyword path: every instance of the jsonschema-benchmark
// corpora, and mutations of it (a property removed, given another type, nulled or added, an array item changed, at
// every depth), must get the same verdict from both. A port of the Rust crate's tests/differential.rs.
//
// Set JSONSCHEMA_BENCHMARK to a checkout of https://github.com/sourcemeta-research/jsonschema-benchmark. The test is
// skipped without it. DIFF_ONLY narrows the corpora, DIFF_INSTANCES caps the instances of each corpus (default 60).
// It takes a minute or two.

// replacements are values of every JSON type, tried in place of a value.
func replacements() []any {
	return []any{nil, true, 0.0, -1.5, "", "x", []any{}, map[string]any{}, []any{1.0, "a"}}
}

func sortedKeys(o map[string]any) []string {
	keys := make([]string, 0, len(o))
	for k := range o {
		keys = append(keys, k)
	}
	sort.Strings(keys)
	return keys
}

func withProperty(o map[string]any, name string, value any, remove bool) map[string]any {
	out := make(map[string]any, len(o)+1)
	for k, v := range o {
		out[k] = v
	}
	if remove {
		delete(out, name)
	} else {
		out[name] = value
	}
	return out
}

func withItem(a []any, i int, value any) []any {
	out := append([]any(nil), a...)
	out[i] = value
	return out
}

// mutations are mutations of v: at the root and, recursively, inside it (bounded at each level to keep the count
// sane).
func mutations(v any, depth int) []any {
	var out []any
	if depth > 6 {
		return out
	}
	switch v := v.(type) {
	case map[string]any:
		for i, k := range sortedKeys(v) {
			if i >= 12 {
				break
			}
			out = append(out, withProperty(v, k, nil, true))
			all := replacements()
			for j := i % 3; j < len(all); j += 3 {
				out = append(out, withProperty(v, k, all[j], false))
			}
			inner := mutations(v[k], depth+1)
			for j := 0; j < len(inner) && j < 40; j++ {
				out = append(out, withProperty(v, k, inner[j], false))
			}
		}
		out = append(out, withProperty(v, "zzUnknownProperty", 1.0, false))
	case []any:
		for i := 0; i < len(v) && i < 4; i++ {
			all := replacements()
			for j := i % 2; j < len(all); j += 2 {
				out = append(out, withItem(v, i, all[j]))
			}
			inner := mutations(v[i], depth+1)
			for j := 0; j < len(inner) && j < 40; j++ {
				out = append(out, withItem(v, i, inner[j]))
			}
		}
		if len(v) != 0 {
			out = append(out, append(append([]any(nil), v...), v[0]))
		}
	default:
		out = append(out, replacements()...)
	}
	return out
}

func stripFormat(v any) {
	switch v := v.(type) {
	case map[string]any:
		if _, ok := v["format"].(string); ok {
			delete(v, "format")
		}
		for _, child := range v {
			stripFormat(child)
		}
	case []any:
		for _, child := range v {
			stripFormat(child)
		}
	}
}

func TestPlansAgreeWithTheGeneralEvaluatorOnTheBenchmarkCorpora(t *testing.T) {
	root := os.Getenv("JSONSCHEMA_BENCHMARK")
	if root == "" {
		t.Skip("set JSONSCHEMA_BENCHMARK to a checkout of jsonschema-benchmark to run the differential test")
	}
	dirs, err := os.ReadDir(filepath.Join(root, "schemas"))
	if err != nil {
		t.Fatalf("jsonschema-benchmark not found at %s: %v", root, err)
	}
	only := os.Getenv("DIFF_ONLY")
	limit := 60
	if s := os.Getenv("DIFF_INSTANCES"); s != "" {
		if limit, err = strconv.Atoi(s); err != nil {
			t.Fatal(err)
		}
	}
	checked := 0
	for _, dir := range dirs {
		name := dir.Name()
		if only != "" && !strings.Contains(","+only+",", ","+name+",") {
			continue
		}
		text, err := os.ReadFile(filepath.Join(root, "schemas", name, "schema.json"))
		if err != nil {
			continue
		}
		var schema any
		if err := json.Unmarshal(text, &schema); err != nil {
			t.Fatalf("%s: %v", name, err)
		}
		stripFormat(schema)
		stripped, _ := json.Marshal(schema)
		v, err := Compile(stripped)
		if err != nil {
			t.Errorf("%s: compile error %v", name, err)
			continue
		}
		instances, err := os.ReadFile(filepath.Join(root, "schemas", name, "instances.jsonl"))
		if err != nil {
			t.Fatalf("%s: %v", name, err)
		}
		failures, lines := 0, 0
		for _, line := range bytes.Split(instances, []byte("\n")) {
			if len(bytes.TrimSpace(line)) == 0 {
				continue
			}
			if lines++; lines > limit {
				break
			}
			var x any
			if err := json.Unmarshal(line, &x); err != nil {
				t.Fatalf("%s: %v", name, err)
			}
			if !v.IsValidBytes(line) {
				t.Errorf("%s: instance %d is not valid", name, lines)
			}
			for _, c := range append([]any{x}, mutations(x, 0)...) {
				checked++
				text, err := json.Marshal(c)
				if err != nil {
					t.Fatal(err)
				}
				fast := v.IsValidBytes(text)
				collected, err := v.EvaluateBytes(text, NewResultsCollector(Basic))
				if err == nil && fast == collected {
					continue
				}
				if failures++; failures <= 3 {
					t.Errorf("%s: fail-fast %v, collecting %v (%v) on %.400s", name, fast, collected, err, text)
				}
			}
		}
		if failures > 3 {
			t.Errorf("%s: %d disagreements in all", name, failures)
		}
	}
	t.Logf("%d instances checked", checked)
}
