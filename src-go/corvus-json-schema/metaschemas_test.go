package jsonschema

import (
	"bytes"
	"io/fs"
	"os"
	"path/filepath"
	"strings"
	"testing"
)

// Keeps the embedded metaschemas in step with those of Corvus.Text.Json (src/Corvus.Text.Json/metaschema). Run with
// UPDATE_METASCHEMAS=1 to copy them again.
func TestEmbeddedMetaschemasAreCurrent(t *testing.T) {
	source := filepath.Join("..", "..", "src", "Corvus.Text.Json", "metaschema")
	if _, err := os.Stat(source); err != nil {
		t.Skipf("outside the Corvus.JsonSchema repository: nothing to compare against (%v)", err)
	}
	update := os.Getenv("UPDATE_METASCHEMAS") != ""
	expected := make(map[string]bool)
	for _, draft := range []string{"draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"} {
		root := filepath.Join(source, draft)
		err := filepath.WalkDir(root, func(path string, entry fs.DirEntry, err error) error {
			if err != nil || entry.IsDir() {
				return err
			}
			relative, _ := filepath.Rel(source, path)
			name := filepath.ToSlash(relative)
			expected[name] = true
			want, err := os.ReadFile(path)
			if err != nil {
				return err
			}
			if update {
				target := filepath.Join("metaschemas", relative)
				if err := os.MkdirAll(filepath.Dir(target), 0o755); err != nil {
					return err
				}
				return os.WriteFile(target, want, 0o644)
			}
			got, err := metaschemaFiles.ReadFile("metaschemas/" + name)
			if err != nil || !bytes.Equal(got, want) {
				t.Errorf("metaschemas/%s is stale: run UPDATE_METASCHEMAS=1 go test -run TestEmbeddedMetaschemasAreCurrent", name)
			}
			return nil
		})
		if err != nil {
			t.Fatal(err)
		}
	}
	err := fs.WalkDir(metaschemaFiles, "metaschemas", func(path string, entry fs.DirEntry, err error) error {
		if err == nil && !entry.IsDir() && !expected[strings.TrimPrefix(path, "metaschemas/")] {
			t.Errorf("%s has no source in %s", path, source)
		}
		return err
	})
	if err != nil {
		t.Fatal(err)
	}
}

func TestEveryEmbeddedMetaschemaResolvesByItsURI(t *testing.T) {
	uris := []string{
		"http://json-schema.org/draft-04/schema", "http://json-schema.org/draft-06/schema",
		"http://json-schema.org/draft-07/schema", "https://json-schema.org/draft/2019-09/schema",
		"https://json-schema.org/draft/2020-12/schema",
	}
	for _, draft := range []string{"2019-09", "2020-12"} {
		entries, err := metaschemaFiles.ReadDir("metaschemas/draft" + draft + "/meta")
		if err != nil {
			t.Fatal(err)
		}
		for _, entry := range entries {
			uris = append(uris, "https://json-schema.org/draft/"+draft+"/meta/"+strings.TrimSuffix(entry.Name(), ".json"))
		}
	}
	if len(uris) != 21 {
		t.Errorf("%d metaschemas, want 21", len(uris))
	}
	for _, uri := range uris {
		text, ok := metaschema(uri)
		if !ok {
			t.Errorf("%s is not embedded", uri)
			continue
		}
		if _, err := ParseDocument(text); err != nil {
			t.Errorf("%s: %v", uri, err)
		}
		if strings.Contains(uri, "hyper-schema") {
			// The hyper-schema vocabulary refers to the links schema, which is not embedded.
			continue
		}
		v, err := CompileURI(uri)
		if err != nil {
			t.Errorf("%s: %v", uri, err)
			continue
		}
		if !v.IsValidString(`{"type": "object"}`) {
			t.Errorf("%s rejects a schema", uri)
		}
		// The root metaschemas (and the validation vocabularies) know that type is a name or a list of names.
		if (!strings.Contains(uri, "/meta/") || strings.HasSuffix(uri, "/meta/validation")) && v.IsValidString(`{"type": 12}`) {
			t.Errorf("%s accepts a schema with a numeric type", uri)
		}
	}
	for _, uri := range []string{"https://json-schema.org/draft/2020-12/meta/../schema", "https://example.com/schema", ""} {
		if _, ok := metaschema(uri); ok {
			t.Errorf("%q resolved", uri)
		}
	}
}
