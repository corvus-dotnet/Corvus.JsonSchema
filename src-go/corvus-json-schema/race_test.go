//go:build race

package jsonschema

// raceEnabled says the race detector is on. It makes sync.Pool drop values at random, so pooled buffers are
// allocated again and the allocation tests do not apply.
const raceEnabled = true
