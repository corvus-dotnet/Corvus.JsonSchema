// Compare validates the jsonschema-benchmark corpora
// (https://github.com/sourcemeta-research/jsonschema-benchmark) with corvus-json-schema and with
// github.com/santhosh-tekuri/jsonschema/v6, the Go validator the benchmark runs, in one process. The engines' timed
// passes are interleaved, so drift in the machine's speed affects them alike.
//
//	go run . --schemas ../../../jsonschema-benchmark/schemas [--only a,b] [--engines corvus,santhosh]
//	    [--budget-ms 1000]
//
// For each corpus and engine, the schema is compiled, every instance is validated once, and then whole passes over
// the instances are repeated for the time budget after a warm-up. The schema is the benchmark's
// schema-noformat.json when the checkout has made it, and otherwise schema.json with its format keywords removed,
// which is what that file is. Every instance in the corpora is valid, so an engine that rejects one is reported
// instead of timed. The table shows the median warm pass per engine.
//
// Each engine is used as the benchmark uses it. Corvus validates documents parsed by ParseDocument. The other
// engine validates values decoded by encoding/json, as implementations/go-jsonschema does.
package main

import (
	"bytes"
	"encoding/json"
	"errors"
	"flag"
	"fmt"
	"math"
	"os"
	"path/filepath"
	"slices"
	"sort"
	"strings"
	"time"

	jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
	santhosh "github.com/santhosh-tekuri/jsonschema/v6"
)

// prepared is one engine on one corpus, compiled and with its instances parsed. It validates every instance and
// returns how many were valid.
type prepared func() int

type engine struct {
	name    string
	prepare func(schema []byte, schemaFile string, lines [][]byte) (prepared, error)
}

func prepareCorvus(schema []byte, _ string, lines [][]byte) (prepared, error) {
	v, err := jsonschema.Compile(schema)
	if err != nil {
		return nil, err
	}
	documents := make([]*jsonschema.Document, len(lines))
	for i, line := range lines {
		if documents[i], err = jsonschema.ParseDocument(line); err != nil {
			return nil, err
		}
	}
	return func() int {
		valid := 0
		for _, d := range documents {
			if v.IsValid(d) {
				valid++
			}
		}
		return valid
	}, nil
}

func prepareSanthosh(schema []byte, schemaFile string, lines [][]byte) (prepared, error) {
	document, err := santhosh.UnmarshalJSON(bytes.NewReader(schema))
	if err != nil {
		return nil, err
	}
	url := "file://" + filepath.ToSlash(schemaFile)
	compiler := santhosh.NewCompiler()
	if err := compiler.AddResource(url, document); err != nil {
		return nil, err
	}
	compiled, err := compiler.Compile(url)
	if err != nil {
		return nil, err
	}
	instances := make([]any, len(lines))
	for i, line := range lines {
		if err := json.Unmarshal(line, &instances[i]); err != nil {
			return nil, err
		}
	}
	return func() int {
		valid := 0
		for _, instance := range instances {
			if compiled.Validate(instance) == nil {
				valid++
			}
		}
		return valid
	}, nil
}

var engines = []engine{{"corvus", prepareCorvus}, {"santhosh", prepareSanthosh}}

// stripFormat removes string-valued format members, as the benchmark's schema-noformat.json does.
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

// readSchema is a corpus's schema without format keywords, and the file it stands for.
func readSchema(dir string) ([]byte, string, error) {
	file, err := filepath.Abs(filepath.Join(dir, "schema-noformat.json"))
	if err != nil {
		return nil, "", err
	}
	if text, err := os.ReadFile(file); err == nil {
		return text, file, nil
	} else if !errors.Is(err, os.ErrNotExist) {
		return nil, "", err
	}
	text, err := os.ReadFile(filepath.Join(dir, "schema.json"))
	if err != nil {
		return nil, "", err
	}
	var schema any
	decoder := json.NewDecoder(bytes.NewReader(text))
	decoder.UseNumber()
	if err := decoder.Decode(&schema); err != nil {
		return nil, "", err
	}
	stripFormat(schema)
	text, err = json.Marshal(schema)
	return text, file, err
}

func readLines(file string) ([][]byte, error) {
	contents, err := os.ReadFile(file)
	if err != nil {
		return nil, err
	}
	var lines [][]byte
	for _, line := range bytes.Split(contents, []byte("\n")) {
		if len(bytes.TrimSpace(line)) != 0 {
			lines = append(lines, line)
		}
	}
	return lines, nil
}

func split(list string) []string {
	var out []string
	for _, item := range strings.Split(list, ",") {
		if item != "" {
			out = append(out, item)
		}
	}
	return out
}

func main() {
	schemas := flag.String("schemas", "../../../jsonschema-benchmark/schemas", "the benchmark's schemas directory")
	only := flag.String("only", "", "the corpora to run, separated by commas (default: all)")
	engineNames := flag.String("engines", "corvus,santhosh", "the engines to run, separated by commas, the first as the reference")
	budgetMs := flag.Int("budget-ms", 1000, "the time each engine warms up for, and is then timed for, on each corpus")
	flag.Parse()
	if flag.NArg() != 0 {
		fmt.Fprintln(os.Stderr, "unknown argument", flag.Arg(0))
		os.Exit(2)
	}
	var selected []engine
	for _, name := range split(*engineNames) {
		at := slices.IndexFunc(engines, func(e engine) bool { return e.name == name })
		if at < 0 {
			fmt.Fprintln(os.Stderr, "unknown engine", name)
			os.Exit(2)
		}
		selected = append(selected, engines[at])
	}
	entries, err := os.ReadDir(*schemas)
	if err != nil {
		fmt.Fprintln(os.Stderr, err)
		os.Exit(2)
	}
	budget := time.Duration(*budgetMs) * time.Millisecond
	corpora := split(*only)

	fmt.Printf("%-24s %8s", "corpus", "count")
	for _, e := range selected {
		fmt.Printf(" %14s", e.name)
	}
	fmt.Println()
	ratios := make([][]float64, len(selected))
	for _, entry := range entries {
		name := entry.Name()
		if !entry.IsDir() || (len(corpora) != 0 && !slices.Contains(corpora, name)) {
			continue
		}
		dir := filepath.Join(*schemas, name)
		schema, schemaFile, err := readSchema(dir)
		if err != nil {
			fmt.Fprintln(os.Stderr, name+":", err)
			os.Exit(2)
		}
		lines, err := readLines(filepath.Join(dir, "instances.jsonl"))
		if err != nil {
			fmt.Fprintln(os.Stderr, name+":", err)
			os.Exit(2)
		}
		passes := make([]prepared, len(selected))
		notes := make([]string, len(selected))
		for i, e := range selected {
			pass, err := e.prepare(schema, schemaFile, lines)
			if err != nil {
				notes[i] = "error"
				continue
			}
			if valid := pass(); valid != len(lines) {
				notes[i] = fmt.Sprintf("invalid %d", len(lines)-valid)
				continue
			}
			passes[i] = pass
		}
		// Warm up each engine for the budget, then interleave timed passes for the budget again.
		for _, pass := range passes {
			if pass != nil {
				end := time.Now().Add(budget)
				for n := 0; n < 1000 && time.Now().Before(end); n++ {
					pass()
				}
			}
		}
		samples := make([][]time.Duration, len(selected))
		end := time.Now().Add(budget * time.Duration(len(selected)))
		for rounds := 0; (time.Now().Before(end) || rounds < 5) && rounds < 2000; rounds++ {
			for i, pass := range passes {
				if pass != nil {
					start := time.Now()
					pass()
					samples[i] = append(samples[i], time.Since(start))
				}
			}
		}
		fmt.Printf("%-24s %8d", name, len(lines))
		reference := math.NaN()
		for i := range selected {
			if passes[i] == nil {
				fmt.Printf(" %14s", notes[i])
				continue
			}
			sort.Slice(samples[i], func(a, b int) bool { return samples[i][a] < samples[i][b] })
			median := float64(samples[i][len(samples[i])/2].Nanoseconds()) / 1000
			fmt.Printf(" %11.1f us", median)
			if i == 0 {
				reference = median
			} else if !math.IsNaN(reference) {
				ratios[i] = append(ratios[i], reference/median)
			}
		}
		fmt.Println()
	}
	for i := 1; i < len(selected); i++ {
		if len(ratios[i]) == 0 {
			continue
		}
		logs, faster := 0.0, 0
		for _, r := range ratios[i] {
			logs += math.Log(r)
			if r < 1 {
				faster++
			}
		}
		fmt.Printf("%s / %s: geomean %.3f, faster on %d of %d\n", selected[0].name, selected[i].name,
			math.Exp(logs/float64(len(ratios[i]))), faster, len(ratios[i]))
	}
}
