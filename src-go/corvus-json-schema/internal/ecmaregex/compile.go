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
}

// A program is a pattern compiled for the backtracking matcher.
type program struct {
	insts []inst
	// groups is the number of capturing groups some backreference names, and loops the number of counted loops.
	groups, loops int
	// anchored says every match starts at the start of the text.
	anchored bool
	// prefix is literal text every match starts with.
	prefix []byte
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
}

func compileProgram(root *node, unicode bool) *program {
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
	c.emit(root, false)
	c.add(inst{op: opMatch})
	p := &program{insts: c.insts, groups: len(c.tracked), loops: c.loops}
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
			split := c.add(inst{op: opSplit})
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
		case back:
			op = opBackrefBack
		}
		// When several groups share the name, at most one has taken part, and the others match the empty string. So
		// matching each in turn matches the one that took part.
		for _, g := range n.groups {
			c.add(inst{op: op, a: c.tracked[g], flag: c.unicode})
		}
	case nLook:
		look := c.add(inst{op: opLook, flag: n.negative})
		c.emit(n.subs[0], n.behind)
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
		c.add(inst{op: op, r: body.r, set: body.set, min: min, max: max})
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
	split := func() int32 { return c.add(inst{op: opSplit}) }
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
	loop := c.add(inst{op: opLoop, a: k, min: min, max: max, flag: n.lazy})
	c.add(inst{op: opLoopIter, a: k})
	emitBody()
	c.add(inst{op: opJmp, x: loop})
	c.insts[loop].x = c.pc()
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
