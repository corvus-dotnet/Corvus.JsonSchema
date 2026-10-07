//go:build race

package ecmaregex

// raceEnabled says the race detector is on. It makes sync.Pool drop values at random, so pooled state is allocated
// again and the allocation test does not apply.
const raceEnabled = true
