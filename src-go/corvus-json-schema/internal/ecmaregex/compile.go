package ecmaregex

import (
	"sync"
	"unicode/utf8"
)

type opcode uint8

const (
	// opChar and opSet consume one code point going forward. The Back forms consume the one before the position,
	// which is how the body of a lookbehind runs.
	opChar opcode = iota
	opCharBack
	opSet
	opSetBack
	// opLit consumes a run of literal bytes going forward.
	opLit
	opFail
	opBOL
	opEOL
	opWordBoundary
	opNotWordBoundary
	// opLineStart and opLineEnd are ^ and $ with the m modifier. The Fold forms of the word boundaries and of the
	// backreferences belong to a case-insensitive group.
	opLineStart
	opLineEnd
	opWordBoundaryFold
	opNotWordBoundaryFold
	opJmp
	// opSplit continues at x and leaves y as the alternative.
	opSplit
	// opSaveTmp notes where a group was entered. opCapture records the group once it has closed.
	opSaveTmp
	opCapture
	// opClear resets the groups a to b, as ECMA-262 does at the start of each iteration of a quantified body.
	opClear
	opBackref
	opBackrefBack
	opBackrefFold
	opBackrefFoldBack
	// opBackrefUnits and opBackrefUnitsBack compare a group a code unit at a time, for a pattern matched by code
	// unit.
	opBackrefUnits
	opBackrefUnitsBack
	// opLook opens a lookaround. opLookOK ends the body of a positive one and opLookFail the body of a negative one.
	opLook
	opLookOK
	opLookFail
	// opRep and opRepLazy repeat one character or class without entering the general loop.
	opRep
	opRepLazy
	// opLoopInit, opLoop and opLoopIter run a counted or possibly empty quantified body.
	opLoopInit
	opLoop
	opLoopIter
	opMatch
)

// An inst is one instruction of the backtracking matcher.
type inst struct {
	op opcode
	// flag marks a lazy opLoop, a backward opCapture, a negative opLook, and an opBackrefFold that folds case as the
	// u flag grammar does.
	flag bool
	r    rune
	set  *charSet
	lit  []byte
	// x and y are jump targets.
	x, y int32
	// a and b are a group, a loop or a range of groups, depending on the instruction.
	a, b int32
	// min and max bound a repetition. A max of -1 is no bound.
	min, max int32
	// need is the least number of bytes of text that every way from this instruction to a match consumes going
	// forward (see setNeeds). It is set for an opLoop, and counts none of the iterations the loop has still to make.
	need int32
	// iter is the least number of bytes one iteration of the body of an opLoop consumes going forward.
	iter int32
	// m is the number of the instruction's memo point, counted from 1, or 0 when it has none (see memoPoint). It is
	// set for an opSplit, an opLoop, an opRep and an opRepLazy.
	m int32
}

// A memoPoint is a place where the matcher makes a choice and can remember that the choice has failed: a split, a
// loop that may be entered or left, and what follows a single character loop that gives characters back or takes
// more. Whether a way from such a place leads to a match depends on the place and the position alone when nothing
// else the matcher keeps can change what follows. So a pattern with a backreference has no memo points (what a
// group holds changes what follows), and neither has a place inside a loop that counts its iterations up to a
// bound. Inside a loop with no upper bound and a least count of zero or one, one thing more decides what follows:
// whether the loop would refuse the iteration as empty if it ended here. That is one bit for each such loop the
// place is in (loops, innermost last), and a place in more than maxMemoLoops of them has no memo point.
type memoPoint struct {
	loops [maxMemoLoops]int32
	mins  [maxMemoLoops]int32
}

// maxMemoLoops is the number of loops whose state a memo point can tell apart.
const maxMemoLoops = 2

// A loopContext is one counted loop that the instruction being written is inside of: its number, its least count,
// and whether its state is one bit to a memo point (no upper bound, and a least count of zero or one).
type loopContext struct {
	loop, min int32
	simple    bool
}

// A program is a pattern compiled for the backtracking matcher.
type program struct {
	insts []inst
	// groups is the number of capturing groups some backreference names, and loops the number of counted loops.
	groups, loops int
	// units says the pattern was read without the u flag, so the text is matched by UTF-16 code unit (see units.go).
	units bool
	// anchored says every match starts at the start of the text.
	anchored bool
	// prefix is literal text every match starts with.
	prefix []byte
	// need is the least number of bytes a match consumes (see setNeeds).
	need int
	// points holds the memo points of the program (see memoPoint).
	points []memoPoint
	// quick says, for each instruction, which characters can come next on a way from it to the end of its match (see
	// setQuick).
	quick []quick
	// first holds every code point a match can start with. It is nil when a match can be empty or when the set is
	// not known.
	first *charSet
	pool  sync.Pool
}

type compiler struct {
	insts []inst
	// tracked maps a capturing group's number to its slot, for the groups some backreference names. The others
	// are not recorded at all.
	tracked map[int]int32
	loops   int
	// unicode says the pattern was read with the u flag grammar, which decides how a backreference folds case.
	unicode bool
	// memo says the pattern has no backreference, so its choices can have memo points. open lists the counted loops
	// around the instruction being written, outermost first, inside the innermost lookaround: what a lookaround's
	// body does depends on nothing outside it. points collects the memo points.
	memo   bool
	open   []loopContext
	points []memoPoint
}

// point returns the number of a new memo point for an instruction written here, or 0 when a choice here cannot be
// remembered.
func (c *compiler) point() int32 {
	if !c.memo || len(c.open) > maxMemoLoops {
		return 0
	}
	var p memoPoint
	for k := range p.loops {
		p.loops[k] = -1
	}
	for k, l := range c.open {
		if !l.simple {
			return 0
		}
		p.loops[k], p.mins[k] = l.loop, l.min
	}
	c.points = append(c.points, p)
	return int32(len(c.points))
}

func compileProgram(root *node, unicode bool) *program {
	root = simplify(root, nil)
	c := &compiler{tracked: map[int]int32{}, unicode: unicode}
	var referenced []int
	collectBackrefs(root, &referenced)
	// Slots follow group order, so the groups inside any one subtree take a contiguous range of slots.
	maxGroup := 0
	seen := map[int]bool{}
	for _, g := range referenced {
		seen[g] = true
		if g > maxGroup {
			maxGroup = g
		}
	}
	for g := 1; g <= maxGroup; g++ {
		if seen[g] {
			c.tracked[g] = int32(len(c.tracked))
		}
	}
	c.memo = len(c.tracked) == 0
	c.emit(root, false)
	c.add(inst{op: opMatch})
	p := &program{insts: c.insts, groups: len(c.tracked), loops: c.loops, units: !unicode, points: c.points}
	p.need = setNeeds(p.insts)
	p.quick = setQuick(p.insts)
	p.anchored = startsAtBOL(root)
	if !p.anchored {
		p.prefix = literalPrefix(root, nil)
		if len(p.prefix) == 0 {
			if pairs, nullable, ok := firstChars(root, nil); ok && !nullable {
				p.first = newCharSet(pairs, false)
			}
		}
	}
	p.pool.New = func() any {
		return &state{caps: make([]int, 2*p.groups), tmp: make([]int, p.groups), counts: make([]int, 2*p.loops)}
	}
	return p
}

// simplify returns the tree with two rewrites, both of which V8 makes. Neither changes what the pattern matches or
// what any group holds. Without them a pattern can take time exponential in its counts for no reason that ECMA-262
// gives. The tree is shared, so a node that changes is copied.
//
// A backreference to a group it is inside of is the empty string. The group is not set until it closes, and the
// repetition around a group clears it at the start of each iteration, so inside the group it is never set.
//
// A repetition whose body cannot consume a character, such as (?:\b|(?=a)|){4,5}, is the body once when its minimum
// is one or more, and nothing when its minimum is zero. Every iteration of such a body is empty. ECMA-262 refuses an
// empty iteration after the minimum, so the iterations are exactly the minimum. Each of them starts with the groups
// of the body cleared and at the same position, so what the last one can do does not depend on the ones before it,
// and the ways through the last one come in the same order whatever the others chose. Run as written, the matcher
// would try every combination of the ways through every iteration.
func simplify(n *node, open []int) *node {
	if n.kind == nBackref {
		for _, g := range n.groups {
			inside := false
			for _, o := range open {
				inside = inside || o == g
			}
			if !inside {
				return n
			}
		}
		return &node{kind: nEmpty}
	}
	if len(n.subs) == 0 {
		return n
	}
	if n.kind == nGroup && n.group > 0 {
		open = append(open, n.group)
	}
	out := n
	for i, sub := range n.subs {
		s := simplify(sub, open)
		if s == sub {
			continue
		}
		if out == n {
			c := *n
			c.subs = append([]*node(nil), n.subs...)
			out = &c
		}
		out.subs[i] = s
	}
	if out.kind == nRepeat && out.max != 0 && !consumes(out.subs[0]) {
		if out.min == 0 {
			return &node{kind: nEmpty}
		}
		return out.subs[0]
	}
	return out
}

// consumes reports whether n can match one character or more. It errs towards true.
func consumes(n *node) bool {
	switch n.kind {
	case nChar, nSet, nBackref:
		return true
	case nCat, nAlt, nGroup:
		for _, sub := range n.subs {
			if consumes(sub) {
				return true
			}
		}
	case nRepeat:
		return n.max != 0 && consumes(n.subs[0])
	}
	return false
}

func collectBackrefs(n *node, out *[]int) {
	if n.kind == nBackref {
		*out = append(*out, n.groups...)
	}
	for _, sub := range n.subs {
		collectBackrefs(sub, out)
	}
}

func (c *compiler) add(i inst) int32 {
	c.insts = append(c.insts, i)
	return int32(len(c.insts) - 1)
}

func (c *compiler) pc() int32 { return int32(len(c.insts)) }

// groupRange returns the first and last slot of the tracked groups inside n, or ok false when it has none.
func (c *compiler) groupRange(n *node) (lo, hi int32, ok bool) {
	lo, hi = -1, -1
	var walk func(n *node)
	walk = func(n *node) {
		if n.kind == nGroup && n.group > 0 {
			if t, tracked := c.tracked[n.group]; tracked {
				if lo < 0 || t < lo {
					lo = t
				}
				if t > hi {
					hi = t
				}
			}
		}
		for _, sub := range n.subs {
			walk(sub)
		}
	}
	walk(n)
	return lo, hi, lo >= 0
}

// emit writes the instructions for n. With back set the instructions match right to left, as the body of a
// lookbehind must.
func (c *compiler) emit(n *node, back bool) {
	switch n.kind {
	case nEmpty:
	case nChar:
		if back {
			c.add(inst{op: opCharBack, r: n.r})
		} else {
			c.add(inst{op: opChar, r: n.r})
		}
	case nSet:
		switch {
		case n.set.empty():
			c.add(inst{op: opFail})
		case back:
			c.add(inst{op: opSetBack, set: n.set})
		default:
			c.add(inst{op: opSet, set: n.set})
		}
	case nCat:
		if back {
			for i := len(n.subs) - 1; i >= 0; i-- {
				c.emit(n.subs[i], true)
			}
			return
		}
		for i := 0; i < len(n.subs); i++ {
			// A run of literal characters becomes one comparison of bytes.
			j := i
			var lit []byte
			for j < len(n.subs) && n.subs[j].kind == nChar && literalRune(n.subs[j].r) {
				lit = utf8.AppendRune(lit, n.subs[j].r)
				j++
			}
			if j-i >= 2 {
				c.add(inst{op: opLit, lit: lit})
				i = j - 1
				continue
			}
			c.emit(n.subs[i], false)
		}
	case nAlt:
		var jumps []int32
		for i, sub := range n.subs {
			if i == len(n.subs)-1 {
				c.emit(sub, back)
				break
			}
			split := c.add(inst{op: opSplit, m: c.point()})
			c.insts[split].x = c.pc()
			c.emit(sub, back)
			jumps = append(jumps, c.add(inst{op: opJmp}))
			c.insts[split].y = c.pc()
		}
		for _, j := range jumps {
			c.insts[j].x = c.pc()
		}
	case nGroup:
		t, tracked := c.tracked[n.group]
		if n.group == 0 || !tracked {
			c.emit(n.subs[0], back)
			return
		}
		c.add(inst{op: opSaveTmp, a: t})
		c.emit(n.subs[0], back)
		c.add(inst{op: opCapture, a: t, flag: back})
	case nBOL:
		c.add(inst{op: opBOL})
	case nEOL:
		c.add(inst{op: opEOL})
	case nLineStart:
		c.add(inst{op: opLineStart})
	case nLineEnd:
		c.add(inst{op: opLineEnd})
	case nWordBoundary:
		if n.fold {
			c.add(inst{op: opWordBoundaryFold})
		} else {
			c.add(inst{op: opWordBoundary})
		}
	case nNotWordBoundary:
		if n.fold {
			c.add(inst{op: opNotWordBoundaryFold})
		} else {
			c.add(inst{op: opNotWordBoundary})
		}
	case nBackref:
		op := opBackref
		switch {
		case n.fold && back:
			op = opBackrefFoldBack
		case n.fold:
			op = opBackrefFold
		case back && !c.unicode:
			op = opBackrefUnitsBack
		case back:
			op = opBackrefBack
		case !c.unicode:
			op = opBackrefUnits
		}
		// When several groups share the name, at most one has taken part, and the others match the empty string. So
		// matching each in turn matches the one that took part.
		for _, g := range n.groups {
			c.add(inst{op: op, a: c.tracked[g], flag: c.unicode})
		}
	case nLook:
		look := c.add(inst{op: opLook, flag: n.negative})
		// Whether the body matches depends on nothing outside it.
		outer := c.open
		c.open = nil
		c.emit(n.subs[0], n.behind)
		c.open = outer
		if n.negative {
			c.add(inst{op: opLookFail})
		} else {
			c.add(inst{op: opLookOK})
		}
		c.insts[look].x = c.pc()
	case nRepeat:
		c.emitRepeat(n, back)
	}
}

func (c *compiler) emitRepeat(n *node, back bool) {
	body := n.subs[0]
	if n.max == 0 {
		return
	}
	min, max := int32(n.min), int32(n.max)
	if !back && (body.kind == nChar || body.kind == nSet && !body.set.empty()) {
		op := opRep
		if n.lazy {
			op = opRepLazy
		}
		c.add(inst{op: op, r: body.r, set: body.set, min: min, max: max, m: c.point()})
		return
	}
	clearLo, clearHi, clear := c.groupRange(body)
	emitBody := func() {
		if clear {
			c.add(inst{op: opClear, a: clearLo, b: clearHi})
		}
		c.emit(body, back)
	}
	// split writes a choice between entering the body and leaving the loop, in the order the quantifier prefers.
	split := func() int32 { return c.add(inst{op: opSplit, m: c.point()}) }
	setSplit := func(at, enter, exit int32) {
		if n.lazy {
			c.insts[at].x, c.insts[at].y = exit, enter
		} else {
			c.insts[at].x, c.insts[at].y = enter, exit
		}
	}
	// A body that always consumes something needs no check for empty iterations, so the unbounded forms and the
	// optional form are plain choices.
	if !nullable(body) {
		switch {
		case n.min == 0 && n.max == unbounded:
			at := split()
			enter := c.pc()
			emitBody()
			c.add(inst{op: opJmp, x: at})
			setSplit(at, enter, c.pc())
			return
		case n.min == 1 && n.max == unbounded:
			enter := c.pc()
			emitBody()
			at := split()
			setSplit(at, enter, c.pc())
			return
		case n.min == 0 && n.max == 1:
			at := split()
			enter := c.pc()
			emitBody()
			setSplit(at, enter, c.pc())
			return
		}
	}
	k := int32(c.loops)
	c.loops++
	c.add(inst{op: opLoopInit, a: k})
	iter := 0
	if !back {
		iter = minLength(body)
	}
	simple := max == unbounded && min <= 1
	point := int32(0)
	if simple {
		// The loop is left or entered from here, and what follows either depends on the loops around this one.
		point = c.point()
	}
	loop := c.add(inst{op: opLoop, a: k, min: min, max: max, flag: n.lazy, iter: int32(iter), m: point})
	c.add(inst{op: opLoopIter, a: k})
	c.open = append(c.open, loopContext{loop: k, min: min, simple: simple})
	emitBody()
	c.open = c.open[:len(c.open)-1]
	c.add(inst{op: opJmp, x: loop})
	c.insts[loop].x = c.pc()
}

// noMatch is the need of an instruction from which no way leads to a match. Every need is capped at it.
const noMatch = 1 << 30

// minLength returns the least number of bytes a match of n consumes, capped at noMatch. Like the needs, it counts a
// character as its UTF-8 bytes when the matcher compares bytes for it and as one byte otherwise, so it can only be
// too small.
func minLength(n *node) int {
	switch n.kind {
	case nChar:
		if literalRune(n.r) {
			return utf8.RuneLen(n.r)
		}
		return 1
	case nSet:
		if n.set.empty() {
			return noMatch
		}
		return 1
	case nCat:
		total := 0
		for _, sub := range n.subs {
			if total += minLength(sub); total >= noMatch {
				return noMatch
			}
		}
		return total
	case nAlt:
		least := noMatch
		for _, sub := range n.subs {
			if l := minLength(sub); l < least {
				least = l
			}
		}
		return least
	case nGroup:
		return minLength(n.subs[0])
	case nRepeat:
		return timesNeed(n.min, minLength(n.subs[0]))
	}
	return 0
}

// timesNeed returns count times need, capped at noMatch.
func timesNeed(count, need int) int {
	if count == 0 || need == 0 {
		return 0
	}
	if need >= noMatch || count >= noMatch/need {
		return noMatch
	}
	return count * need
}

// setNeeds works out, for every instruction, the least number of bytes of text that any way from it to a match
// consumes going forward, and writes it into the counted loops. It returns the need of the whole program. The
// matcher gives up at a counted loop when fewer bytes than that are left, as V8 does at every choice. A loop whose
// iterations can be empty would otherwise try every combination of its ways at a position from which the rest of
// the pattern cannot match at all.
//
// A counted loop that is entered needs its least count of iterations, each of them the least its body consumes
// (inst.iter), and then what follows it. The loop instruction itself is also reached after each iteration, so its
// own need counts only what follows, and the matcher adds the iterations it has still to make.
//
// A character is counted as one byte, a literal run as its bytes and a single character loop as its minimum, so the
// figure can only be too small. The body of a lookaround ends at its own last instruction and the instruction that
// opens one goes straight to what follows it, so a body is counted by itself. An instruction that matches right to
// left, which is the body of a lookbehind, counts nothing.
func setNeeds(insts []inst) int {
	need, _ := needs(insts)
	return need
}

// needs is setNeeds, and also returns the number of passes over the instructions it made, the last of which changed
// nothing.
func needs(insts []inst) (programNeed, passes int) {
	need := make([]int32, len(insts))
	for pc := range need {
		need[pc] = noMatch
	}
	// The needs only fall, and nearly every way goes forward, so going through the instructions from the last to the
	// first settles all but the loops in one pass.
	for changed := true; changed; {
		changed = false
		passes++
		for pc := len(insts) - 1; pc >= 0; pc-- {
			i := &insts[pc]
			n := int32(noMatch)
			// Both figures can be noMatch, so the sum is made in 64 bits: in 32 it would wrap to a negative need.
			next := func(to int32, weight int32) {
				if v := int64(need[to]) + int64(weight); v < int64(n) {
					n = int32(v)
				}
			}
			switch i.op {
			case opMatch, opLookOK, opLookFail:
				n = 0
			case opFail:
			case opChar, opSet:
				next(int32(pc+1), 1)
			case opLit:
				next(int32(pc+1), int32(len(i.lit)))
			case opRep, opRepLazy:
				next(int32(pc+1), i.min)
			case opJmp:
				// The one way that goes back is the jump at the end of the body of a loop, to the instruction at its
				// head, which this pass has not reached. The need of that instruction is the need of what follows
				// the loop, since its other way, through the body, comes back to it and cannot need less. So the
				// jump takes what follows the loop, which this pass has settled. Taking the need of the head would
				// settle one loop of a nest for each pass: a thousand passes for loops a thousand deep.
				switch head := &insts[i.x]; {
				case int(i.x) < pc && head.op == opLoop:
					next(head.x, 0)
				case int(i.x) < pc && head.op == opSplit:
					next(head.x, 0)
					next(head.y, 0)
				default:
					next(i.x, 0)
				}
			case opLook:
				next(i.x, 0)
			case opSplit:
				next(i.x, 0)
				next(i.y, 0)
			case opLoop:
				next(int32(pc+1), 0)
				next(i.x, 0)
			case opLoopInit:
				loop := &insts[pc+1]
				next(int32(pc+1), int32(timesNeed(int(loop.min), int(loop.iter))))
			default:
				next(int32(pc+1), 0)
			}
			if n > noMatch {
				n = noMatch
			}
			if n < need[pc] {
				need[pc], changed = n, true
			}
		}
	}
	for pc := range insts {
		if insts[pc].op == opLoop {
			insts[pc].need = need[pc]
		}
	}
	return int(need[0]), passes
}

// A quick says what can come next on a way from an instruction to the end of its match, which is the end of the
// pattern or of the lookaround the instruction is in. The matcher looks at it before it takes a way: when the next
// character of the text is none that the way can start with, and the way cannot end without one, the way fails, so
// it is not taken, and where it was one of two no choice is kept for it. V8 makes the same check ("quick check")
// at its choices. It is what keeps a choice between ways that need different characters from being a choice.
type quick struct {
	// ascii holds the characters below 128 a way can consume next, and other says it can consume a character that
	// is not ASCII.
	ascii [2]uint64
	other bool
	// mid says a way reaches the end of its match consuming nothing more while text is left, and end that one does
	// at the end of the text.
	mid, end bool
}

// allows reports whether a way can go on from the position.
func (q *quick) allows(in []byte, pos int) bool {
	if pos >= len(in) {
		return q.end
	}
	if q.mid {
		return true
	}
	if c := in[pos]; c < utf8.RuneSelf {
		return q.ascii[c>>6]>>(c&63)&1 != 0
	}
	return q.other
}

func (q *quick) addRune(r rune) {
	if r < utf8.RuneSelf {
		q.ascii[r>>6] |= 1 << (uint(r) & 63)
	} else {
		q.other = true
	}
}

func (q *quick) addSet(s *charSet) {
	q.ascii[0] |= s.ascii[0]
	q.ascii[1] |= s.ascii[1]
	if n := len(s.ranges); n > 0 && s.ranges[n-1] >= utf8.RuneSelf {
		q.other = true
	}
}

func (q *quick) union(o quick) {
	q.ascii[0] |= o.ascii[0]
	q.ascii[1] |= o.ascii[1]
	q.other = q.other || o.other
	q.mid = q.mid || o.mid
	q.end = q.end || o.end
}

// setQuick works out the quick of every instruction. Every figure is a set that can only be too large: an
// assertion is taken to hold, a backreference and an instruction that matches right to left (the body of a
// lookbehind) to allow anything. A positive lookahead needs both its body and what follows it to go on from the
// same position, so it allows what both allow.
func setQuick(insts []inst) []quick {
	q, _ := quicks(insts)
	return q
}

// quicks is setQuick, and also returns the number of passes over the instructions it made, the last of which changed
// nothing.
func quicks(insts []inst) (q []quick, passes int) {
	q = make([]quick, len(insts))
	all := quick{ascii: [2]uint64{^uint64(0), ^uint64(0)}, other: true, mid: true, end: true}
	// The sets only grow, and nearly every way goes forward, so going through the instructions from the last to the
	// first settles all but the loops in one pass.
	for changed := true; changed; {
		changed = false
		passes++
		for pc := len(insts) - 1; pc >= 0; pc-- {
			i := &insts[pc]
			var n quick
			switch i.op {
			case opMatch, opLookOK, opLookFail:
				n = all
			case opFail:
			case opChar:
				n.addRune(i.r)
			case opSet:
				n.addSet(i.set)
			case opLit:
				n.addRune(rune(i.lit[0]))
			case opRep, opRepLazy:
				if i.set != nil {
					n.addSet(i.set)
				} else {
					n.addRune(i.r)
				}
				if i.min == 0 {
					n.union(q[pc+1])
				}
			case opJmp:
				// The jump at the end of the body of a loop goes back to the head of the loop, which this pass has
				// not reached. The head allows what follows the loop, which this pass has settled, and what the body
				// starts with. So the jump takes what follows the loop too, and the first pass settles the head of
				// every loop, the second every instruction, and the third finds nothing to change, however deep
				// the loops nest.
				n = q[i.x]
				if head := &insts[i.x]; int(i.x) < pc && head.op == opLoop {
					n.union(q[head.x])
				} else if int(i.x) < pc && head.op == opSplit {
					n.union(q[head.x])
					n.union(q[head.y])
				}
			case opSplit:
				n = q[i.x]
				n.union(q[i.y])
			case opLoop:
				n = q[pc+1]
				n.union(q[i.x])
			case opEOL:
				n.end = q[pc+1].end
			case opLook:
				n = q[i.x]
				if !i.flag {
					b := q[pc+1]
					n.ascii[0] &= b.ascii[0]
					n.ascii[1] &= b.ascii[1]
					n.other, n.mid, n.end = n.other && b.other, n.mid && b.mid, n.end && b.end
				}
			case opCharBack, opSetBack, opBackref, opBackrefBack, opBackrefFold, opBackrefFoldBack, opBackrefUnits, opBackrefUnitsBack:
				n = all
			default:
				n = q[pc+1]
			}
			if n != q[pc] {
				q[pc], changed = n, true
			}
		}
	}
	return q, passes
}

// literalRune reports whether a code point can be matched by comparing its UTF-8 bytes. A surrogate has no encoding,
// and U+FFFD is also what a malformed byte of the text decodes to.
func literalRune(r rune) bool { return utf8.ValidRune(r) && r != utf8.RuneError }

// nullable reports whether n can match without consuming anything. It errs towards true.
func nullable(n *node) bool {
	switch n.kind {
	case nChar, nSet:
		return false
	case nCat:
		for _, sub := range n.subs {
			if !nullable(sub) {
				return false
			}
		}
		return true
	case nAlt:
		for _, sub := range n.subs {
			if nullable(sub) {
				return true
			}
		}
		return false
	case nGroup:
		return nullable(n.subs[0])
	case nRepeat:
		return n.min == 0 || nullable(n.subs[0])
	}
	return true
}

// startsAtBOL reports whether every way through n begins with ^.
func startsAtBOL(n *node) bool {
	switch n.kind {
	case nBOL:
		return true
	case nCat:
		return startsAtBOL(n.subs[0])
	case nAlt:
		for _, sub := range n.subs {
			if !startsAtBOL(sub) {
				return false
			}
		}
		return true
	case nGroup:
		return startsAtBOL(n.subs[0])
	case nRepeat:
		return n.min >= 1 && startsAtBOL(n.subs[0])
	}
	return false
}

// literalPrefix appends the literal text every match of n starts with.
func literalPrefix(n *node, prefix []byte) []byte {
	switch n.kind {
	case nChar:
		if literalRune(n.r) {
			return utf8.AppendRune(prefix, n.r)
		}
	case nCat:
		for _, sub := range n.subs {
			if sub.kind != nChar || !literalRune(sub.r) {
				if len(prefix) == 0 && sub.kind == nGroup {
					return literalPrefix(sub, prefix)
				}
				break
			}
			prefix = utf8.AppendRune(prefix, sub.r)
		}
	case nGroup:
		return literalPrefix(n.subs[0], prefix)
	}
	return prefix
}

// firstChars appends, as inclusive pairs, the code points a match of n can start with. It also reports whether n can
// match the empty string, and ok false when the set is not known. Assertions are treated as matching the empty
// string, which can only make the set larger than it need be.
func firstChars(n *node, pairs []rune) (out []rune, empty, ok bool) {
	switch n.kind {
	case nChar:
		return append(pairs, n.r, n.r), false, true
	case nSet:
		return append(pairs, n.set.ranges...), false, true
	case nCat:
		for _, sub := range n.subs {
			var subEmpty bool
			pairs, subEmpty, ok = firstChars(sub, pairs)
			if !ok {
				return nil, false, false
			}
			if !subEmpty {
				return pairs, false, true
			}
		}
		return pairs, true, true
	case nAlt:
		for _, sub := range n.subs {
			var subEmpty bool
			pairs, subEmpty, ok = firstChars(sub, pairs)
			if !ok {
				return nil, false, false
			}
			empty = empty || subEmpty
		}
		return pairs, empty, true
	case nGroup:
		return firstChars(n.subs[0], pairs)
	case nRepeat:
		if n.max == 0 {
			return pairs, true, true
		}
		pairs, empty, ok = firstChars(n.subs[0], pairs)
		return pairs, empty || n.min == 0, ok
	case nBackref:
		return nil, false, false
	}
	return pairs, true, true
}
