package ecmaregex

import (
	"bytes"
	"errors"
	"time"
	"unicode/utf8"
)

// ErrMatchTime reports a match that was abandoned because it ran for longer than its limit, and ErrMatchMemory one
// that was abandoned because its backtracking stack reached maxStack entries. Neither is an answer.
var (
	ErrMatchTime   = errors.New("ecmaregex: the match ran for longer than its limit")
	ErrMatchMemory = errors.New("ecmaregex: the match needed more backtracking memory than its limit")
)

const (
	// checkEvery is the number of steps the matcher takes between two looks at the clock and at the depth of its
	// stack. A step is one instruction, and eight characters of a single character loop or of a backreference. A
	// step takes a few nanoseconds, so the clock is read every ten to twenty microseconds, a match that ends
	// before its first check never reads it, and a match goes past its limit by that much at most.
	checkEvery = 1 << 12
	// maxStack is the number of entries the backtracking stack of a match with a limit may hold (24 bytes each).
	// A match that needs more is abandoned. A pattern that keeps a choice for every character of its text needs
	// one entry or a few for each, so this is room for a text of some hundred thousand characters, and a pattern
	// that pushes without consuming, such as (?:a?){2147483647}, reaches it in a few milliseconds. It is looked at
	// with the clock, so the stack can go past it by checkEvery entries.
	maxStack = 1 << 20
)

// Why a match was abandoned.
const (
	stopNone uint8 = iota
	stopTime
	stopMemory
)

// The kinds of entry on the backtracking stack. A choice entry says where to resume. An undo entry restores a value
// that was overwritten after the entries below it were pushed.
const (
	// eChoice resumes at pc with the position pos.
	eChoice uint8 = iota
	// eRep belongs to a greedy single character loop that stopped at pos. Resuming gives back one character, down to
	// the position a.
	eRep
	// eRepLazy belongs to a lazy single character loop that has taken a characters and stands at pos. Resuming takes
	// one more.
	eRepLazy
	// eLook marks the start of a positive lookaround and eLookNeg of a negative one. Both hold the position to
	// restore, and eLookNeg the instruction to continue at when its body has failed.
	eLook
	eLookNeg
	// eTmp restores the entry position of group pc to pos.
	eTmp
	// eCap restores the start and end of group pc to pos and a.
	eCap
	// eCounter restores the count and the iteration start of loop pc to pos and a.
	eCounter
)

type entry struct {
	kind uint8
	pc   int32
	pos  int
	a    int
}

// A state is the working memory of one match. It is pooled, so a match allocates only while the stack is still
// growing to the depth the pattern and the text need.
type state struct {
	stack []entry
	// caps holds the start and end of each tracked group, or -1, and tmp the position at which each was entered.
	caps []int
	tmp  []int
	// counts holds, for each loop, the number of iterations started and the position at which the last one started.
	counts []int
	// limit is the time the match may take, or zero for no limit. The clock starts at the first check, so deadline
	// is set when timed is.
	limit    time.Duration
	deadline time.Time
	timed    bool
	// steps is the number of steps left before the next check, and stop why the match was abandoned.
	steps int
	stop  uint8
}

// check is called every checkEvery steps. It reports whether the match may go on.
func (st *state) check(depth int) bool {
	if st.limit <= 0 {
		return true
	}
	if depth > maxStack {
		st.stop = stopMemory
		return false
	}
	now := time.Now()
	if !st.timed {
		st.timed, st.deadline = true, now.Add(st.limit)
		return true
	}
	if now.After(st.deadline) {
		st.stop = stopTime
		return false
	}
	return true
}

// maxRetainedStack is the largest stack kept in the pool between matches.
const maxRetainedStack = 1 << 16

// match reports whether the program matches anywhere in the text. With a limit above zero the match is abandoned
// when it has run for longer than that, or when its stack has reached maxStack, and the error says which.
func (p *program) match(in []byte, limit time.Duration) (bool, error) {
	st := p.pool.Get().(*state)
	for i := range st.caps {
		st.caps[i] = -1
	}
	st.limit, st.timed, st.steps, st.stop = limit, false, checkEvery, stopNone
	ok := p.search(st, in)
	stop := st.stop
	if cap(st.stack) > maxRetainedStack {
		st.stack = nil
	}
	p.pool.Put(st)
	switch stop {
	case stopTime:
		return false, ErrMatchTime
	case stopMemory:
		return false, ErrMatchMemory
	}
	return ok, nil
}

func (p *program) search(st *state, in []byte) bool {
	if len(in) < p.need {
		return false
	}
	if p.anchored {
		return p.run(st, in, 0)
	}
	if len(p.prefix) > 0 {
		for at := 0; ; at++ {
			i := bytes.Index(in[at:], p.prefix)
			if i < 0 {
				return false
			}
			at += i
			if len(in)-at < p.need {
				return false
			}
			if p.run(st, in, at) {
				return true
			}
			if st.stop != stopNone {
				return false
			}
		}
	}
	for at := 0; ; {
		if len(in)-at < p.need {
			return false
		}
		if at == len(in) {
			return p.first == nil && p.run(st, in, at)
		}
		r, w := rune(in[at]), 1
		if r >= utf8.RuneSelf {
			if p.units {
				r, w = unitAt(in, at)
			} else {
				r, w = utf8.DecodeRune(in[at:])
			}
		}
		if (p.first == nil || p.first.contains(r)) && p.run(st, in, at) {
			return true
		}
		if st.stop != stopNone {
			return false
		}
		at += w
	}
}

// run reports whether the program matches at the position start. It keeps its choices on an explicit stack, so the
// depth of the search is bounded by memory and not by the goroutine stack. When it fails, every group and counter is
// back to the value it had on entry. It counts its steps, and when a check says the match has reached a limit it
// returns false with the reason in st.stop.
func (p *program) run(st *state, in []byte, start int) bool {
	insts := p.insts
	stack := st.stack[:0]
	caps, counts := st.caps, st.counts
	// units says the text is matched by UTF-16 code unit (see units.go).
	units := p.units
	pc, pos := 0, start
	steps := st.steps
	for {
		steps--
		if steps < 0 {
			if !st.check(len(stack)) {
				st.stack, st.steps = stack, 0
				return false
			}
			steps = checkEvery
		}
		i := &insts[pc]
		switch i.op {
		case opChar:
			if pos < len(in) {
				if c := in[pos]; c < utf8.RuneSelf {
					if rune(c) == i.r {
						pos++
						pc++
						continue
					}
				} else {
					var r rune
					var w int
					if units {
						r, w = unitAt(in, pos)
					} else {
						r, w = utf8.DecodeRune(in[pos:])
					}
					if r == i.r {
						pos += w
						pc++
						continue
					}
				}
			}
		case opSet:
			if pos < len(in) {
				if c := in[pos]; c < utf8.RuneSelf {
					if i.set.ascii[c>>6]>>(c&63)&1 != 0 {
						pos++
						pc++
						continue
					}
				} else {
					var r rune
					var w int
					if units {
						r, w = unitAt(in, pos)
					} else {
						r, w = utf8.DecodeRune(in[pos:])
					}
					if i.set.contains(r) {
						pos += w
						pc++
						continue
					}
				}
			}
		case opLit:
			if len(in)-pos >= len(i.lit) && bytes.Equal(in[pos:pos+len(i.lit)], i.lit) {
				pos += len(i.lit)
				pc++
				continue
			}
		case opCharBack:
			if pos > 0 {
				r, w := rune(in[pos-1]), 1
				if r >= utf8.RuneSelf {
					if units {
						r, w = unitBefore(in, pos)
					} else {
						r, w = utf8.DecodeLastRune(in[:pos])
					}
				}
				if r == i.r {
					pos -= w
					pc++
					continue
				}
			}
		case opSetBack:
			if pos > 0 {
				r, w := rune(in[pos-1]), 1
				if r >= utf8.RuneSelf {
					if units {
						r, w = unitBefore(in, pos)
					} else {
						r, w = utf8.DecodeLastRune(in[:pos])
					}
				}
				if i.set.contains(r) {
					pos -= w
					pc++
					continue
				}
			}
		case opFail:
		case opBOL:
			if pos == 0 {
				pc++
				continue
			}
		case opEOL:
			if pos == len(in) {
				pc++
				continue
			}
		case opWordBoundary, opNotWordBoundary:
			before := pos > 0 && isWordByte(in[pos-1])
			after := pos < len(in) && isWordByte(in[pos])
			if (before != after) == (i.op == opWordBoundary) {
				pc++
				continue
			}
		case opLineStart:
			if pos == 0 || lineTerminatorBefore(in, pos) {
				pc++
				continue
			}
		case opLineEnd:
			if pos == len(in) || lineTerminatorAt(in, pos) {
				pc++
				continue
			}
		case opWordBoundaryFold, opNotWordBoundaryFold:
			before, after := false, false
			if pos > 0 {
				r, _ := utf8.DecodeLastRune(in[:pos])
				before = isFoldWordRune(r)
			}
			if pos < len(in) {
				r, _ := utf8.DecodeRune(in[pos:])
				after = isFoldWordRune(r)
			}
			if (before != after) == (i.op == opWordBoundaryFold) {
				pc++
				continue
			}
		case opJmp:
			pc = int(i.x)
			continue
		case opSplit:
			stack = append(stack, entry{kind: eChoice, pc: i.y, pos: pos})
			pc = int(i.x)
			continue
		case opSaveTmp:
			stack = append(stack, entry{kind: eTmp, pc: i.a, pos: st.tmp[i.a]})
			st.tmp[i.a] = pos
			pc++
			continue
		case opCapture:
			stack = append(stack, entry{kind: eCap, pc: i.a, pos: caps[2*i.a], a: caps[2*i.a+1]})
			if i.flag {
				caps[2*i.a], caps[2*i.a+1] = pos, st.tmp[i.a]
			} else {
				caps[2*i.a], caps[2*i.a+1] = st.tmp[i.a], pos
			}
			pc++
			continue
		case opClear:
			steps -= int(i.b - i.a)
			for g := i.a; g <= i.b; g++ {
				if caps[2*g] >= 0 {
					stack = append(stack, entry{kind: eCap, pc: g, pos: caps[2*g], a: caps[2*g+1]})
					caps[2*g], caps[2*g+1] = -1, -1
				}
			}
			pc++
			continue
		case opBackref:
			// A group that has not taken part matches the empty string.
			from, to := caps[2*i.a], caps[2*i.a+1]
			if from < 0 {
				pc++
				continue
			}
			if n := to - from; len(in)-pos >= n && bytes.Equal(in[pos:pos+n], in[from:to]) {
				steps -= n >> 3
				pos += n
				pc++
				continue
			}
		case opBackrefBack:
			from, to := caps[2*i.a], caps[2*i.a+1]
			if from < 0 {
				pc++
				continue
			}
			if n := to - from; pos >= n && bytes.Equal(in[pos-n:pos], in[from:to]) {
				steps -= n >> 3
				pos -= n
				pc++
				continue
			}
		case opBackrefFold, opBackrefUnits:
			// A character at a time: to fold case, or because a group matched by code unit can begin or end between
			// the halves of a character, where its bytes do not say what it holds.
			from, to := caps[2*i.a], caps[2*i.a+1]
			if from < 0 {
				pc++
				continue
			}
			steps -= (to - from) >> 3
			at, ok := pos, true
			for from < to && ok {
				want, w := decode(in, from, units)
				from += w
				if at >= len(in) {
					ok = false
					break
				}
				got, w := decode(in, at, units)
				at += w
				ok = got == want || i.op == opBackrefFold && canonicalize(got, i.flag) == canonicalize(want, i.flag)
			}
			if ok {
				pos = at
				pc++
				continue
			}
		case opBackrefFoldBack, opBackrefUnitsBack:
			from, to := caps[2*i.a], caps[2*i.a+1]
			if from < 0 {
				pc++
				continue
			}
			steps -= (to - from) >> 3
			at, ok := pos, true
			for from < to && ok {
				want, w := decodeLast(in, to, units)
				to -= w
				if at <= 0 {
					ok = false
					break
				}
				got, w := decodeLast(in, at, units)
				at -= w
				ok = got == want || i.op == opBackrefFoldBack && canonicalize(got, i.flag) == canonicalize(want, i.flag)
			}
			if ok {
				pos = at
				pc++
				continue
			}
		case opLook:
			kind := eLook
			if i.flag {
				kind = eLookNeg
			}
			stack = append(stack, entry{kind: kind, pc: i.x, pos: pos})
			pc++
			continue
		case opLookOK:
			// The body matched. A lookaround is atomic, so the choices made inside it are dropped. The undo entries
			// stay, because the groups it set remain set until something below this point is retried.
			f := len(stack) - 1
			for stack[f].kind != eLook {
				f--
			}
			pos = stack[f].pos
			keep := f
			for k := f + 1; k < len(stack); k++ {
				if stack[k].kind >= eTmp {
					stack[keep] = stack[k]
					keep++
				}
			}
			stack = stack[:keep]
			pc++
			continue
		case opLookFail:
			// The body of a negative lookaround matched, so the lookaround fails and everything it did is undone.
			for {
				e := stack[len(stack)-1]
				stack = stack[:len(stack)-1]
				if e.kind == eLookNeg {
					break
				}
				st.undo(e)
			}
		case opRep:
			at, n, floor := pos, int32(0), pos
			if set := i.set; set != nil {
				for n != i.max && at < len(in) {
					if c := in[at]; c < utf8.RuneSelf {
						if set.ascii[c>>6]>>(c&63)&1 == 0 {
							break
						}
						at++
					} else {
						var r rune
						var w int
						if units {
							r, w = unitAt(in, at)
						} else {
							r, w = utf8.DecodeRune(in[at:])
						}
						if !set.contains(r) {
							break
						}
						at += w
					}
					n++
					if n == i.min {
						floor = at
					}
				}
			} else {
				for n != i.max && at < len(in) {
					r, w := rune(in[at]), 1
					if r >= utf8.RuneSelf {
						if units {
							r, w = unitAt(in, at)
						} else {
							r, w = utf8.DecodeRune(in[at:])
						}
					}
					if r != i.r {
						break
					}
					at += w
					n++
					if n == i.min {
						floor = at
					}
				}
			}
			steps -= (at - pos) >> 3
			if n >= i.min {
				if at > floor {
					stack = append(stack, entry{kind: eRep, pc: int32(pc + 1), pos: at, a: floor})
				}
				pos = at
				pc++
				continue
			}
		case opRepLazy:
			at, n := pos, int32(0)
			for n < i.min {
				w := i.one(in, at, units)
				if w == 0 {
					break
				}
				at += w
				n++
			}
			steps -= int(n) >> 3
			if n == i.min {
				if n != i.max {
					stack = append(stack, entry{kind: eRepLazy, pc: int32(pc), pos: at, a: int(n)})
				}
				pos = at
				pc++
				continue
			}
		case opLoopInit:
			stack = append(stack, entry{kind: eCounter, pc: i.a, pos: counts[2*i.a], a: counts[2*i.a+1]})
			counts[2*i.a], counts[2*i.a+1] = 0, -1
			pc++
			continue
		case opLoop:
			n := counts[2*i.a]
			// Fewer bytes are left than any way from here to a match consumes: what follows the loop, and the
			// iterations it has still to make.
			if left := len(in) - pos; left < int(i.need) || n < int(i.min) && i.iter != 0 && int64(left)-int64(i.need) < int64(int(i.min)-n)*int64(i.iter) {
				break
			}
			// Once the minimum is met, ECMA-262 rejects an iteration that consumed nothing. That is what stops a
			// loop over a body that can be empty.
			if n > int(i.min) && pos == counts[2*i.a+1] {
				break
			}
			switch {
			case n < int(i.min):
				pc++
			case i.max >= 0 && n >= int(i.max):
				pc = int(i.x)
			case i.flag:
				stack = append(stack, entry{kind: eChoice, pc: int32(pc + 1), pos: pos})
				pc = int(i.x)
			default:
				stack = append(stack, entry{kind: eChoice, pc: i.x, pos: pos})
				pc++
			}
			continue
		case opLoopIter:
			stack = append(stack, entry{kind: eCounter, pc: i.a, pos: counts[2*i.a], a: counts[2*i.a+1]})
			counts[2*i.a]++
			counts[2*i.a+1] = pos
			pc++
			continue
		case opMatch:
			st.stack, st.steps = stack, steps
			return true
		}
		// The instruction failed. Take up the most recent choice, undoing what was recorded since.
	backtrack:
		for {
			if len(stack) == 0 {
				st.stack, st.steps = stack, steps
				return false
			}
			e := &stack[len(stack)-1]
			switch e.kind {
			case eChoice:
				pc, pos = int(e.pc), e.pos
				stack = stack[:len(stack)-1]
				break backtrack
			case eRep:
				// Give back the last character taken. The characters between a and pos were decoded going forward,
				// and decoding the last of them going backward finds the same boundary.
				w := 1
				if in[e.pos-1] >= utf8.RuneSelf {
					if units {
						_, w = unitBefore(in, e.pos)
					} else {
						_, w = utf8.DecodeLastRune(in[e.a:e.pos])
					}
				}
				e.pos -= w
				pc, pos = int(e.pc), e.pos
				if e.pos <= e.a {
					stack = stack[:len(stack)-1]
				}
				break backtrack
			case eRepLazy:
				rep := &insts[e.pc]
				w := rep.one(in, e.pos, units)
				if w == 0 {
					stack = stack[:len(stack)-1]
					continue
				}
				e.pos += w
				e.a++
				pc, pos = int(e.pc)+1, e.pos
				if int32(e.a) == rep.max {
					stack = stack[:len(stack)-1]
				}
				break backtrack
			case eLook:
				// The body of a positive lookaround has no way left to match.
				stack = stack[:len(stack)-1]
			case eLookNeg:
				// The body of a negative lookaround has no way to match, so the lookaround holds.
				pc, pos = int(e.pc), e.pos
				stack = stack[:len(stack)-1]
				break backtrack
			default:
				st.undo(*e)
				stack = stack[:len(stack)-1]
			}
		}
	}
}

// lineTerminatorBefore reports whether the character before the position is an ECMA-262 line terminator, and
// lineTerminatorAt whether the character at the position is one. U+2028 and U+2029 are E2 80 A8 and E2 80 A9.
func lineTerminatorBefore(in []byte, pos int) bool {
	switch in[pos-1] {
	case '\n', '\r':
		return true
	case 0xA8, 0xA9:
		return pos >= 3 && in[pos-3] == 0xE2 && in[pos-2] == 0x80
	}
	return false
}

func lineTerminatorAt(in []byte, pos int) bool {
	switch in[pos] {
	case '\n', '\r':
		return true
	case 0xE2:
		return pos+2 < len(in) && in[pos+1] == 0x80 && (in[pos+2] == 0xA8 || in[pos+2] == 0xA9)
	}
	return false
}

// undo applies one undo entry.
func (st *state) undo(e entry) {
	switch e.kind {
	case eTmp:
		st.tmp[e.pc] = e.pos
	case eCap:
		st.caps[2*e.pc], st.caps[2*e.pc+1] = e.pos, e.a
	case eCounter:
		st.counts[2*e.pc], st.counts[2*e.pc+1] = e.pos, e.a
	}
}

// one returns the width of the character at the position at when the instruction's character or class matches it,
// and 0 otherwise. The character is a code unit with units set.
func (i *inst) one(in []byte, at int, units bool) int {
	if at >= len(in) {
		return 0
	}
	r, w := rune(in[at]), 1
	if r >= utf8.RuneSelf {
		if units {
			r, w = unitAt(in, at)
		} else {
			r, w = utf8.DecodeRune(in[at:])
		}
	}
	if i.set != nil {
		if !i.set.contains(r) {
			return 0
		}
		return w
	}
	if r != i.r {
		return 0
	}
	return w
}
