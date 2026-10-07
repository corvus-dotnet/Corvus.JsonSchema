// corvus-json-schema's implementation of the jsonschema-benchmark protocol
// (https://github.com/sourcemeta-research/jsonschema-benchmark):
//
//	corvus_go_benchmark <schema.json> <instances.jsonl>
//
// It parses every instance, compiles the schema, validates every instance once cold, warms up, and validates once
// warm. It prints one line, cold,warm,compile,parse in nanoseconds, and exits 1 if an instance is invalid.
//
// The benchmark defines warm as steady state. The warm-up follows a rule that is the same for every engine whatever
// its runtime: validation passes for a fixed time (warmupTime), and at least minWarmupPasses passes. The warm figure
// is the last of those passes.
package main

import (
	"bytes"
	"fmt"
	"os"
	"time"

	jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
)

const (
	warmupTime      = 2 * time.Second
	minWarmupPasses = 100
)

func validateAll(v *jsonschema.Validator, documents []*jsonschema.Document) bool {
	valid := true
	for _, d := range documents {
		if !v.IsValid(d) {
			valid = false
		}
	}
	return valid
}

func fail(err error) {
	fmt.Fprintln(os.Stderr, err)
	os.Exit(2)
}

func main() {
	if len(os.Args) != 3 {
		fmt.Fprintln(os.Stderr, "Usage: corvus_go_benchmark <schema> <instances>")
		os.Exit(2)
	}
	schema, err := os.ReadFile(os.Args[1])
	if err != nil {
		fail(err)
	}
	contents, err := os.ReadFile(os.Args[2])
	if err != nil {
		fail(err)
	}
	var texts [][]byte
	for _, line := range bytes.Split(contents, []byte("\n")) {
		if len(bytes.TrimSpace(line)) != 0 {
			texts = append(texts, line)
		}
	}

	parseStart := time.Now()
	documents := make([]*jsonschema.Document, len(texts))
	for i, text := range texts {
		if documents[i], err = jsonschema.ParseDocument(text); err != nil {
			fail(err)
		}
	}
	parse := time.Since(parseStart)

	// The benchmark's schema-noformat.json has no format keywords, and the defaults leave format as an annotation.
	compileStart := time.Now()
	v, err := jsonschema.Compile(schema)
	compile := time.Since(compileStart)
	if err != nil {
		fail(err)
	}

	coldStart := time.Now()
	valid := validateAll(v, documents)
	cold := time.Since(coldStart)
	if !valid {
		os.Exit(1)
	}

	// The warm pass is the last pass of the warm-up loop, timed at the same call site as the passes before it, as in
	// the harnesses of the runtimes that compile while they run.
	deadline := time.Now().Add(warmupTime)
	var warm time.Duration
	for i := 0; i < minWarmupPasses || time.Now().Before(deadline); i++ {
		start := time.Now()
		valid = validateAll(v, documents) && valid
		warm = time.Since(start)
	}
	if !valid {
		os.Exit(1)
	}

	fmt.Printf("%d,%d,%d,%d\n", cold.Nanoseconds(), warm.Nanoseconds(), compile.Nanoseconds(), parse.Nanoseconds())
}
