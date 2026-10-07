package jsonschema

import "testing"

func TestSmoke(t *testing.T) {
	v, err := CompileString(`{"type":"object","properties":{"id":{"type":"integer","minimum":1}},"required":["id"]}`)
	if err != nil {
		t.Fatal(err)
	}
	for json, want := range map[string]bool{`{"id": 3}`: true, `{"id": 0}`: false, `{}`: false, `[]`: false} {
		if got := v.IsValidString(json); got != want {
			t.Errorf("%s: %v", json, got)
		}
		c := NewResultsCollector(Verbose)
		got, err := v.EvaluateString(json, c)
		if err != nil || got != want {
			t.Errorf("%s: evaluate %v %v", json, got, err)
		}
	}
}
