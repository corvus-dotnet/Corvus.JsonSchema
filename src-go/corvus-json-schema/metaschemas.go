package jsonschema

import (
	"embed"
	"strings"
)

// The standard metaschemas, copied from src/Corvus.Text.Json/metaschema (a test keeps the copy current).
//
//go:embed metaschemas
var metaschemaFiles embed.FS

// metaschema is the text of a standard metaschema, by its canonical URI (no trailing empty fragment).
func metaschema(uri string) ([]byte, bool) {
	file, ok := metaschemaFile(uri)
	if !ok {
		return nil, false
	}
	text, err := metaschemaFiles.ReadFile("metaschemas/" + file)
	return text, err == nil
}

func metaschemaFile(uri string) (string, bool) {
	switch uri {
	case "http://json-schema.org/draft-04/schema":
		return "draft4/schema.json", true
	case "http://json-schema.org/draft-06/schema":
		return "draft6/schema.json", true
	case "http://json-schema.org/draft-07/schema":
		return "draft7/schema.json", true
	}
	const prefix = "https://json-schema.org/draft/"
	rest, ok := strings.CutPrefix(uri, prefix)
	if !ok {
		return "", false
	}
	draft, name, _ := strings.Cut(rest, "/")
	if draft != "2019-09" && draft != "2020-12" {
		return "", false
	}
	if name == "schema" {
		return "draft" + draft + "/schema.json", true
	}
	if vocabulary, ok := strings.CutPrefix(name, "meta/"); ok && vocabulary != "" && !strings.ContainsAny(vocabulary, "/.") {
		return "draft" + draft + "/meta/" + vocabulary + ".json", true
	}
	return "", false
}
