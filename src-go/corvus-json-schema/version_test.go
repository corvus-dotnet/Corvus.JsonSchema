package jsonschema

import (
	"os"
	"regexp"
	"strings"
	"testing"
)

// The release workflow reads Version from version.go with a regular expression and publishes it as a tag, so the
// constant has to be a plain semantic version on a line of its own, with its entry in VERSIONHISTORY.md.
func TestVersionIsReleasable(t *testing.T) {
	if !regexp.MustCompile(`^\d+\.\d+\.\d+(-[0-9A-Za-z.-]+)?$`).MatchString(Version) {
		t.Fatalf("Version %q is not a semantic version", Version)
	}
	source, err := os.ReadFile("version.go")
	if err != nil {
		t.Fatal(err)
	}
	if !strings.Contains(string(source), "\nconst Version = \""+Version+"\"\n") &&
		!strings.Contains(string(source), "\nconst Version = \""+Version+"\"\r\n") {
		t.Errorf("version.go does not declare Version on a line of its own, as the release workflow reads it")
	}
	history, err := os.ReadFile("VERSIONHISTORY.md")
	if err != nil {
		t.Fatal(err)
	}
	if !regexp.MustCompile(`(?m)^## V` + regexp.QuoteMeta(Version) + `\r?$`).Match(history) {
		t.Errorf("VERSIONHISTORY.md has no V%s section", Version)
	}
}
