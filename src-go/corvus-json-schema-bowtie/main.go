// A Bowtie (https://github.com/bowtie-json-schema/bowtie) harness for corvus-json-schema. It speaks IHOP (one JSON
// request per line on standard input, one response per line on standard output):
//
//   - start reports the implementation and its dialects;
//   - dialect sets the dialect for schemas without $schema;
//   - run compiles the case's schema with the case's registry as the document resolver and validates each instance
//     (for annotations output, through a verbose results collector, reporting each annotation with its instance
//     location and #... keyword location);
//   - stop exits.
//
// A compilation error, a panic, or an evaluation beyond the maximum depth is reported for that case or instance as
// an error, not a crash.
package main

import (
	"bufio"
	"encoding/json"
	"errors"
	"fmt"
	"io"
	"os"
	"runtime"
	"strings"

	jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
)

var dialects = []struct {
	uri     string
	dialect jsonschema.Dialect
}{
	{"https://json-schema.org/draft/2020-12/schema", jsonschema.Draft202012},
	{"https://json-schema.org/draft/2019-09/schema", jsonschema.Draft201909},
	{"http://json-schema.org/draft-07/schema#", jsonschema.Draft7},
	{"http://json-schema.org/draft-06/schema#", jsonschema.Draft6},
	{"http://json-schema.org/draft-04/schema#", jsonschema.Draft4},
}

type request struct {
	Cmd     string          `json:"cmd"`
	Version json.RawMessage `json:"version"`
	Dialect string          `json:"dialect"`
	Seq     json.RawMessage `json:"seq"`
	Output  json.RawMessage `json:"output"`
	Case    struct {
		Schema   json.RawMessage            `json:"schema"`
		Registry map[string]json.RawMessage `json:"registry"`
		Tests    []struct {
			Instance json.RawMessage `json:"instance"`
		} `json:"tests"`
	} `json:"case"`
}

type implementation struct {
	Language        string   `json:"language"`
	Name            string   `json:"name"`
	Version         string   `json:"version"`
	Homepage        string   `json:"homepage"`
	Documentation   string   `json:"documentation"`
	Issues          string   `json:"issues"`
	Source          string   `json:"source"`
	Dialects        []string `json:"dialects"`
	OS              string   `json:"os"`
	OSVersion       string   `json:"os_version"`
	LanguageVersion string   `json:"language_version"`
}

type started struct {
	Version        int            `json:"version"`
	Implementation implementation `json:"implementation"`
}

type dialectResponse struct {
	OK bool `json:"ok"`
}

type errorContext struct {
	Message string `json:"message"`
}

// erroredCase is the response to a run whose schema could not be compiled.
type erroredCase struct {
	Seq     json.RawMessage `json:"seq"`
	Errored bool            `json:"errored"`
	Context errorContext    `json:"context"`
}

type annotation struct {
	Keyword          string          `json:"keyword"`
	InstanceLocation string          `json:"instanceLocation"`
	KeywordLocation  string          `json:"keywordLocation"`
	Annotation       json.RawMessage `json:"annotation"`
}

// result is one test's result: valid (with annotations when they were asked for), or errored with its context.
type result struct {
	Valid       *bool         `json:"valid,omitempty"`
	Annotations *[]annotation `json:"annotations,omitempty"`
	Errored     bool          `json:"errored,omitempty"`
	Context     *errorContext `json:"context,omitempty"`
}

type ran struct {
	Seq     json.RawMessage `json:"seq"`
	Results []result        `json:"results"`
}

func errored(message string) result {
	return result{Errored: true, Context: &errorContext{Message: message}}
}

func stripFragment(uri string) string {
	base, _, _ := strings.Cut(uri, "#")
	return base
}

// unescapeToken is a JSON pointer token as the text it stands for.
func unescapeToken(token string) string {
	return strings.ReplaceAll(strings.ReplaceAll(token, "~1", "/"), "~0", "~")
}

type harness struct {
	started bool
	dialect jsonschema.Dialect
}

func (h *harness) start(r *request) (any, error) {
	if string(r.Version) != "1" {
		return nil, fmt.Errorf("unsupported IHOP version %s", r.Version)
	}
	h.started = true
	uris := make([]string, len(dialects))
	for i, d := range dialects {
		uris[i] = d.uri
	}
	// The kernel's release on Linux, which is where Bowtie runs harnesses.
	release, _ := os.ReadFile("/proc/sys/kernel/osrelease")
	return started{
		Version: 1,
		Implementation: implementation{
			Language:        "go",
			Name:            "corvus-jsonschema",
			Version:         jsonschema.Version,
			Homepage:        "https://github.com/corvus-dotnet/Corvus.JsonSchema",
			Documentation:   "https://github.com/corvus-dotnet/Corvus.JsonSchema/tree/main/src-go/corvus-json-schema",
			Issues:          "https://github.com/corvus-dotnet/Corvus.JsonSchema/issues",
			Source:          "https://github.com/corvus-dotnet/Corvus.JsonSchema",
			Dialects:        uris,
			OS:              runtime.GOOS,
			OSVersion:       strings.TrimSpace(string(release)),
			LanguageVersion: strings.TrimPrefix(runtime.Version(), "go"),
		},
	}, nil
}

func (h *harness) setDialect(r *request) any {
	for _, d := range dialects {
		if d.uri == r.Dialect {
			h.dialect = d.dialect
			return dialectResponse{OK: true}
		}
	}
	return dialectResponse{OK: false}
}

// compile compiles the case's schema. A panic is an error.
func (h *harness) compile(r *request) (validator *jsonschema.Validator, err error) {
	defer func() {
		if p := recover(); p != nil {
			err = fmt.Errorf("panic: %v", p)
		}
	}()
	registry := make(map[string]json.RawMessage, len(r.Case.Registry))
	for uri, schema := range r.Case.Registry {
		registry[stripFragment(uri)] = schema
	}
	resolver := func(uri string) *jsonschema.Document {
		schema, ok := registry[stripFragment(uri)]
		if !ok {
			return nil
		}
		document, err := jsonschema.ParseDocument(schema)
		if err != nil {
			return nil
		}
		return document
	}
	return jsonschema.Compile(r.Case.Schema, jsonschema.WithDefaultDialect(h.dialect),
		jsonschema.WithDocumentResolver(resolver))
}

// test evaluates one instance. A panic is an error.
func test(validator *jsonschema.Validator, instance json.RawMessage, annotations bool) (out result) {
	defer func() {
		if p := recover(); p != nil {
			out = errored(fmt.Sprintf("panic: %v", p))
		}
	}()
	describe := func(err error) result {
		if errors.Is(err, jsonschema.ErrDepthExceeded) {
			return errored("evaluation recursed beyond the maximum depth")
		}
		return errored(err.Error())
	}
	if !annotations {
		valid, err := validator.ValidateBytes(instance)
		if err != nil {
			return describe(err)
		}
		return result{Valid: &valid}
	}
	collector := jsonschema.NewResultsCollector(jsonschema.Verbose)
	valid, err := validator.EvaluateBytes(instance, collector)
	if err != nil {
		return describe(err)
	}
	found := []annotation{}
	for _, a := range collector.Annotations() {
		found = append(found, annotation{
			Keyword:          unescapeToken(a.Keyword),
			InstanceLocation: a.InstanceLocation,
			KeywordLocation:  jsonschema.SchemaLocationFragment(a.SchemaLocation + "/" + a.Keyword),
			Annotation:       json.RawMessage(a.Value),
		})
	}
	return result{Valid: &valid, Annotations: &found}
}

func (h *harness) run(r *request) any {
	seq := r.Seq
	if seq == nil {
		seq = json.RawMessage("null")
	}
	validator, err := h.compile(r)
	if err != nil {
		return erroredCase{Seq: seq, Errored: true, Context: errorContext{Message: err.Error()}}
	}
	annotations := string(r.Output) == `"annotations"`
	results := make([]result, len(r.Case.Tests))
	for i := range r.Case.Tests {
		results[i] = test(validator, r.Case.Tests[i].Instance, annotations)
	}
	return ran{Seq: seq, Results: results}
}

// handle is the response to one request. Done is set for stop.
func (h *harness) handle(line []byte) (response any, done bool, err error) {
	var r request
	if err := json.Unmarshal(line, &r); err != nil {
		return nil, false, err
	}
	if r.Cmd != "start" && !h.started {
		return nil, false, errors.New("not started")
	}
	switch r.Cmd {
	case "start":
		response, err = h.start(&r)
		return response, false, err
	case "dialect":
		return h.setDialect(&r), false, nil
	case "run":
		return h.run(&r), false, nil
	case "stop":
		return nil, true, nil
	}
	return nil, false, fmt.Errorf("unknown command %q", r.Cmd)
}

func main() {
	h := harness{dialect: jsonschema.Draft202012}
	in := bufio.NewReader(os.Stdin)
	out := bufio.NewWriter(os.Stdout)
	encoder := json.NewEncoder(out)
	encoder.SetEscapeHTML(false)
	for {
		line, err := in.ReadBytes('\n')
		if len(strings.TrimSpace(string(line))) != 0 {
			response, done, err := h.handle(line)
			if err != nil {
				fmt.Fprintln(os.Stderr, err)
				os.Exit(1)
			}
			if done {
				return
			}
			// One line for each response.
			if err := encoder.Encode(response); err != nil {
				fmt.Fprintln(os.Stderr, err)
				os.Exit(1)
			}
			if err := out.Flush(); err != nil {
				os.Exit(1)
			}
		}
		if err == io.EOF {
			return
		}
		if err != nil {
			fmt.Fprintln(os.Stderr, err)
			os.Exit(1)
		}
	}
}
