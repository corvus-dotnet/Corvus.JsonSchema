package main

import (
	"bufio"
	"encoding/json"
	"fmt"
	"io"
	"os"
	"os/exec"
	"path/filepath"
	"reflect"
	"slices"
	"sort"
	"strconv"
	"strings"
	"testing"

	jsonschema "github.com/corvus-dotnet/Corvus.JsonSchema/src-go/corvus-json-schema"
)

// Drives the harness over IHOP as Bowtie does (start, dialect, run with each case's externalSchemas as the registry, stop),
// in a separate process: every required case of the JSON-Schema-Test-Suite, and the annotation suite's assertions,
// compared with the expected results. No containers.

// The harness process is this test binary run again with harnessEnv set.
const harnessEnv = "CORVUS_BOWTIE_HARNESS"

func TestMain(m *testing.M) {
	if os.Getenv(harnessEnv) != "" {
		main()
		os.Exit(0)
	}
	os.Exit(m.Run())
}

var drafts = []struct {
	directory, dialect string
	release            int
}{
	{"draft4", "http://json-schema.org/draft-04/schema#", 4},
	{"draft6", "http://json-schema.org/draft-06/schema#", 6},
	{"draft7", "http://json-schema.org/draft-07/schema#", 7},
	{"draft2019-09", "https://json-schema.org/draft/2019-09/schema", 2019},
	{"draft2020-12", "https://json-schema.org/draft/2020-12/schema", 2020},
}

// harnessProcess is the harness in its own process.
type harnessProcess struct {
	t       *testing.T
	command *exec.Cmd
	input   io.WriteCloser
	output  *bufio.Reader
}

func startHarness(t *testing.T) *harnessProcess {
	t.Helper()
	executable, err := os.Executable()
	if err != nil {
		t.Fatal(err)
	}
	command := exec.Command(executable)
	command.Env = append(os.Environ(), harnessEnv+"=1")
	command.Stderr = os.Stderr
	input, err := command.StdinPipe()
	if err != nil {
		t.Fatal(err)
	}
	stdout, err := command.StdoutPipe()
	if err != nil {
		t.Fatal(err)
	}
	if err := command.Start(); err != nil {
		t.Fatal(err)
	}
	h := &harnessProcess{t: t, command: command, input: input, output: bufio.NewReaderSize(stdout, 1<<20)}
	var response struct {
		Version        int `json:"version"`
		Implementation struct {
			Language        string   `json:"language"`
			Name            string   `json:"name"`
			Version         string   `json:"version"`
			Dialects        []string `json:"dialects"`
			LanguageVersion string   `json:"language_version"`
		} `json:"implementation"`
	}
	h.send(map[string]any{"cmd": "start", "version": 1}, &response)
	implementation := response.Implementation
	if response.Version != 1 || implementation.Language != "go" || implementation.Name != "corvus-jsonschema" ||
		implementation.Version != jsonschema.Version || len(implementation.Dialects) != len(drafts) ||
		implementation.LanguageVersion == "" {
		t.Fatalf("unexpected start response: %+v", response)
	}
	return h
}

// send writes one request and reads its response, one line each.
func (h *harnessProcess) send(request any, response any) {
	h.t.Helper()
	line, err := json.Marshal(request)
	if err != nil {
		h.t.Fatal(err)
	}
	if _, err := h.input.Write(append(line, '\n')); err != nil {
		h.t.Fatal(err)
	}
	answer, err := h.output.ReadBytes('\n')
	if err != nil {
		h.t.Fatalf("no response to %.200s: %v", line, err)
	}
	if err := json.Unmarshal(answer, response); err != nil {
		h.t.Fatalf("%v: %s", err, answer)
	}
}

func (h *harnessProcess) setDialect(dialect string) {
	h.t.Helper()
	var response struct {
		OK bool `json:"ok"`
	}
	h.send(map[string]any{"cmd": "dialect", "dialect": dialect}, &response)
	if !response.OK {
		h.t.Fatalf("the harness refused the dialect %s", dialect)
	}
}

func (h *harnessProcess) stop() {
	h.t.Helper()
	if _, err := h.input.Write([]byte("{\"cmd\":\"stop\"}\n")); err != nil {
		h.t.Fatal(err)
	}
	if err := h.command.Wait(); err != nil {
		h.t.Fatalf("the harness did not exit cleanly: %v", err)
	}
}

func suiteRoot(t *testing.T) string {
	t.Helper()
	root := os.Getenv("JSON_SCHEMA_TEST_SUITE")
	if root == "" {
		root = filepath.Join("..", "..", "JSON-Schema-Test-Suite")
	}
	if _, err := os.Stat(filepath.Join(root, "validation", "tests")); err != nil {
		t.Fatalf("JSON-Schema-Test-Suite not found at %s (set JSON_SCHEMA_TEST_SUITE, or check out the submodule)", root)
	}
	return root
}

func jsonFiles(t *testing.T, dir string) []string {
	t.Helper()
	entries, err := os.ReadDir(dir)
	if err != nil {
		t.Fatal(err)
	}
	var files []string
	for _, entry := range entries {
		if !entry.IsDir() && strings.HasSuffix(entry.Name(), ".json") {
			files = append(files, filepath.Join(dir, entry.Name()))
		}
	}
	sort.Strings(files)
	return files
}

func readJSON(t *testing.T, file string, into any) {
	t.Helper()
	text, err := os.ReadFile(file)
	if err != nil {
		t.Fatal(err)
	}
	if err := json.Unmarshal(text, into); err != nil {
		t.Fatalf("%s: %v", file, err)
	}
}

// registry is Bowtie's registry for a test case: its externalSchemas, keyed by their retrieval URIs.
func registry(externalSchemas json.RawMessage) json.RawMessage {
	if len(externalSchemas) == 0 {
		return json.RawMessage("{}")
	}
	return externalSchemas
}

type ihopTest struct {
	Description string          `json:"description"`
	Instance    json.RawMessage `json:"instance"`
}

type ihopCase struct {
	Description string          `json:"description"`
	Schema      json.RawMessage `json:"schema"`
	Registry    json.RawMessage `json:"registry"`
	Tests       []ihopTest      `json:"tests"`
}

type ihopRun struct {
	Cmd    string   `json:"cmd"`
	Seq    int      `json:"seq"`
	Output string   `json:"output,omitempty"`
	Case   ihopCase `json:"case"`
}

type ihopAnnotation struct {
	Keyword          string          `json:"keyword"`
	InstanceLocation string          `json:"instanceLocation"`
	KeywordLocation  string          `json:"keywordLocation"`
	Annotation       json.RawMessage `json:"annotation"`
}

type ihopResponse struct {
	Seq     int  `json:"seq"`
	Errored bool `json:"errored"`
	Results []struct {
		Valid       *bool            `json:"valid"`
		Errored     bool             `json:"errored"`
		Annotations []ihopAnnotation `json:"annotations"`
	} `json:"results"`
}

func TestRequiredSuiteOverIHOP(t *testing.T) {
	root := suiteRoot(t)
	h := startHarness(t)
	seq, total := 0, 0
	var failures []string
	for _, draft := range drafts {
		h.setDialect(draft.dialect)
		for _, file := range jsonFiles(t, filepath.Join(root, "validation", "tests")) {
			var suite struct {
				Tests []struct {
					Description     string          `json:"description"`
					Compatibility   string          `json:"compatibility"`
					Tags            []string        `json:"tags"`
					Schema          json.RawMessage `json:"schema"`
					ExternalSchemas json.RawMessage `json:"externalSchemas"`
					Tests           []struct {
						Description string          `json:"description"`
						Data        json.RawMessage `json:"data"`
						Valid       bool            `json:"valid"`
					} `json:"tests"`
				} `json:"tests"`
			}
			readJSON(t, file, &suite)
			for _, group := range suite.Tests {
				// The harness treats format as an annotation in every dialect, as Bowtie expects.
				if !compatible(group.Compatibility, draft.release) || slices.Contains(group.Tags, "format-assertion") {
					continue
				}
				seq++
				request := ihopRun{Cmd: "run", Seq: seq, Case: ihopCase{
					Description: group.Description, Schema: group.Schema, Registry: registry(group.ExternalSchemas),
				}}
				for _, test := range group.Tests {
					request.Case.Tests = append(request.Case.Tests, ihopTest{test.Description, test.Data})
				}
				var response ihopResponse
				h.send(request, &response)
				if response.Seq != seq {
					t.Fatalf("the response to request %d has seq %d", seq, response.Seq)
				}
				for i, test := range group.Tests {
					total++
					if response.Errored || i >= len(response.Results) || response.Results[i].Valid == nil ||
						*response.Results[i].Valid != test.Valid {
						failures = append(failures, fmt.Sprintf("%s/%s: %s / %s", draft.directory,
							filepath.Base(file), group.Description, test.Description))
					}
				}
			}
		}
	}
	h.stop()
	if len(failures) != 0 {
		t.Fatalf("%d of %d failed:\n%s", len(failures), total, strings.Join(failures, "\n"))
	}
	if total <= 5000 {
		t.Fatalf("only %d tests ran", total)
	}
	t.Logf("%d tests in %d cases", total, seq)
}

// compatible reports whether a test case's compatibility (a comma-separated list of minimums, <= maximums and =
// releases, from the smallest release to the largest) admits the release, by the algorithm of the suite's
// validation/README.md.
func compatible(compatibility string, release int) bool {
	if compatibility == "" {
		return true
	}
	valid := true
	for _, constraint := range strings.Split(compatibility, ",") {
		op, version := "", constraint
		if v, ok := strings.CutPrefix(constraint, "<="); ok {
			op, version = "<=", v
		} else if v, ok := strings.CutPrefix(constraint, "="); ok {
			op, version = "=", v
		}
		number, err := strconv.Atoi(strings.TrimSpace(version))
		if err != nil {
			panic("compatibility " + compatibility + ": " + err.Error())
		}
		switch op {
		case "<=":
			valid = release <= number
		case "=":
			valid = release == number
		default:
			valid = release >= number
		}
		if release <= number {
			break
		}
	}
	return valid
}

// sameJSON is JSON equality with numbers compared by value and objects whatever their members' order.
func sameJSON(a, b json.RawMessage) bool {
	var x, y any
	return json.Unmarshal(a, &x) == nil && json.Unmarshal(b, &y) == nil && reflect.DeepEqual(x, y)
}

func TestAnnotationSuiteOverIHOP(t *testing.T) {
	root := suiteRoot(t)
	dir := filepath.Join(root, "annotations", "tests")
	if _, err := os.Stat(dir); err != nil {
		t.Fatalf("annotation tests not found at %s", dir)
	}
	h := startHarness(t)
	seq, total := 0, 0
	var failures []string
	for _, draft := range drafts {
		h.setDialect(draft.dialect)
		for _, file := range jsonFiles(t, dir) {
			var suite struct {
				Suite []struct {
					Description     string          `json:"description"`
					Compatibility   string          `json:"compatibility"`
					Schema          json.RawMessage `json:"schema"`
					ExternalSchemas json.RawMessage `json:"externalSchemas"`
					Tests           []struct {
						Instance   json.RawMessage `json:"instance"`
						Assertions []struct {
							Location string                     `json:"location"`
							Keyword  string                     `json:"keyword"`
							Expected map[string]json.RawMessage `json:"expected"`
						} `json:"assertions"`
					} `json:"tests"`
				} `json:"suite"`
			}
			readJSON(t, file, &suite)
			for _, group := range suite.Suite {
				if !compatible(group.Compatibility, draft.release) {
					continue
				}
				seq++
				request := ihopRun{Cmd: "run", Seq: seq, Output: "annotations", Case: ihopCase{
					Description: group.Description, Schema: group.Schema, Registry: registry(group.ExternalSchemas),
				}}
				for _, test := range group.Tests {
					request.Case.Tests = append(request.Case.Tests, ihopTest{"", test.Instance})
				}
				var response ihopResponse
				h.send(request, &response)
				if response.Errored || len(response.Results) != len(group.Tests) {
					t.Fatalf("%s (%s): %s: the case errored", filepath.Base(file), draft.dialect, group.Description)
				}
				for i, test := range group.Tests {
					for _, assertion := range test.Assertions {
						total++
						// The annotations for this instance location and keyword, by the schema's location.
						actual := make(map[string]json.RawMessage)
						for _, a := range response.Results[i].Annotations {
							if a.InstanceLocation == assertion.Location && a.Keyword == assertion.Keyword {
								actual[strings.TrimSuffix(a.KeywordLocation, "/"+a.Keyword)] = a.Annotation
							}
						}
						same := len(actual) == len(assertion.Expected)
						for location, expected := range assertion.Expected {
							got, ok := actual[location]
							same = same && ok && sameJSON(got, expected)
						}
						if !same {
							failures = append(failures, fmt.Sprintf("%s (%s): %s %s: expected %s got %s",
								filepath.Base(file), draft.dialect, assertion.Location, assertion.Keyword,
								assertion.Expected, actual))
						}
					}
				}
			}
		}
	}
	h.stop()
	if len(failures) != 0 {
		t.Fatalf("%d of %d failed:\n%s", len(failures), total, strings.Join(failures, "\n"))
	}
	if total == 0 {
		t.Fatal("no annotation assertions ran")
	}
	t.Logf("%d annotation assertions in %d cases", total, seq)
}
