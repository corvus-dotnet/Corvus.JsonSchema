//go:build corvus_memo_from_the_start

package ecmaregex

// With the build tag corvus_memo_from_the_start every match takes its memory of failed places before its first step
// (see memoFromTheStart). No program is built this way but the harnesses that run the pattern datasets through the
// module, which compare the answers of such a build with those of an ordinary one: remembering a failure must
// change no answer.
func init() { memoFromTheStart = true }
