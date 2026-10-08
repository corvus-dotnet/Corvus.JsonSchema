package ecmaregex

// A deterministic automaton for the patterns the regexp package runs, over ASCII text.
//
// The regexp package matches a pattern of this kind by simulating its automaton state set by state set (or with its
// one-pass or bit-state matchers), which costs tens of nanoseconds per byte of text. The strings a schema tests are
// short and nearly always ASCII, and most patterns in schemas are small. For such a pattern every state set the text
// can lead to is worked out once, and a match is then one table read per byte.
//
// The automaton reads bytes below 0x80 only. A text with any other byte is handed back to the regexp package from
// the start, so nothing here needs to know UTF-8 or the Unicode classes: a class is its ASCII members. A pattern
// with a word boundary, or whose automaton has more states than a byte can number, has no automaton and keeps to
// the regexp package. The automaton is built on the first match, not when the pattern is compiled, so a schema pays
// nothing for the patterns its instances never reach.

const (
	// The states every automaton has. dfaDead: no match can follow. dfaAccept: a match was found. dfaBail: the text
	// is not ASCII.
	dfaDead = iota
	dfaAccept
	dfaBail
	dfaFirst
)

const (
	// An automaton has at most this many states (they are numbered in a byte), built from at most this many states
	// of the pattern, in at most this many steps (a step is one state of the pattern looked at while a set is made).
	// The steps bound what a pattern too large for an automaton costs before that is known, to about a millisecond.
	maxDFAStates = 256
	maxNFAStates = 512
	maxDFASteps  = 400000
)

type nfaKind uint8

const (
	// nfaSet reads one byte of its set and goes to out.
	nfaSet nfaKind = iota
	// nfaSplit goes to out and to alt, reading nothing.
	nfaSplit
	// nfaBOL and nfaEOL go to out at the start and at the end of the text.
	nfaBOL
	nfaEOL
	nfaMatch
)

type nfaState struct {
	kind     nfaKind
	set      [2]uint64
	out, alt int
}

type nfaBuilder struct {
	states []nfaState
	// The pattern has something an automaton over bytes cannot decide, or is too large.
	failed bool
}

func (b *nfaBuilder) add(s nfaState) int {
	if len(b.states) >= maxNFAStates {
		b.failed = true
		return 0
	}
	b.states = append(b.states, s)
	return len(b.states) - 1
}

// compile adds the states of a node that go on to next, and returns the state they start at.
func (b *nfaBuilder) compile(n *node, next int) int {
	if b.failed {
		return 0
	}
	switch n.kind {
	case nEmpty:
		return next
	case nChar:
		var set [2]uint64
		if n.r < 128 {
			set[n.r>>6] = 1 << (uint(n.r) & 63)
		}
		return b.add(nfaState{kind: nfaSet, set: set, out: next})
	case nSet:
		return b.add(nfaState{kind: nfaSet, set: n.set.ascii, out: next})
	case nCat:
		for i := len(n.subs) - 1; i >= 0; i-- {
			next = b.compile(n.subs[i], next)
		}
		return next
	case nAlt:
		start := b.compile(n.subs[len(n.subs)-1], next)
		for i := len(n.subs) - 2; i >= 0; i-- {
			start = b.add(nfaState{kind: nfaSplit, out: b.compile(n.subs[i], next), alt: start})
		}
		return start
	case nGroup:
		return b.compile(n.subs[0], next)
	case nRepeat:
		sub := n.subs[0]
		if n.max == unbounded {
			// The loop, then the copies that must match before it.
			loop := b.add(nfaState{kind: nfaSplit, alt: next})
			if b.failed {
				return 0
			}
			b.states[loop].out = b.compile(sub, loop)
			next = loop
		} else {
			// The copies that may match, innermost last.
			for i := n.min; i < n.max && !b.failed; i++ {
				next = b.add(nfaState{kind: nfaSplit, out: b.compile(sub, next), alt: next})
			}
		}
		for i := 0; i < n.min && !b.failed; i++ {
			next = b.compile(sub, next)
		}
		return next
	case nBOL:
		return b.add(nfaState{kind: nfaBOL, out: next})
	case nEOL:
		return b.add(nfaState{kind: nfaEOL, out: next})
	}
	// A word boundary, or anything the regexp package is not given either.
	b.failed = true
	return 0
}

// dfa is the automaton. A state is a byte, a byte of the text is one of a few classes (bytes the pattern does not
// tell apart), and trans[state<<shift|class] is the state after the byte.
type dfa struct {
	class [256]uint8
	trans []uint8
	shift uint
	start uint8
	// For each state: the pattern matches if the text ends there.
	atEnd []bool
	// The pattern matches the empty text.
	empty bool
}

// match reports whether the pattern matches the text. Not ok when the text is not ASCII, which this automaton does
// not decide.
func (d *dfa) match(text []byte) (matched, ok bool) {
	if len(text) == 0 {
		return d.empty, true
	}
	s := d.start
	if s == dfaAccept {
		return true, true
	}
	trans, shift := d.trans, d.shift
	for _, c := range text {
		s = trans[int(s)<<shift|int(d.class[c])]
		if s < dfaFirst {
			return s == dfaAccept, s != dfaBail
		}
	}
	return d.atEnd[s], true
}

// dfaBuilder makes the automaton's states from sets of the pattern's states.
type dfaBuilder struct {
	nfa []nfaState
	// Scratch for closure: the states visited, by generation, and the stack.
	mark  []uint32
	gen   uint32
	stack []int
	// The steps left (see maxDFASteps).
	steps int
}

// closure adds to set (and returns it) every state that reading nothing reaches from the states in from, keeping
// the states that read a byte, the end anchors still to pass, and the match. A start anchor is passed only atStart,
// and an end anchor only atEnd.
func (b *dfaBuilder) closure(set []int, from []int, atStart, atEnd bool) []int {
	b.gen++
	b.stack = append(b.stack[:0], from...)
	for len(b.stack) > 0 {
		i := b.stack[len(b.stack)-1]
		b.stack = b.stack[:len(b.stack)-1]
		b.steps--
		if b.mark[i] == b.gen {
			continue
		}
		b.mark[i] = b.gen
		switch s := &b.nfa[i]; s.kind {
		case nfaSplit:
			b.stack = append(b.stack, s.out, s.alt)
		case nfaBOL:
			if atStart {
				b.stack = append(b.stack, s.out)
			}
		case nfaEOL:
			if atEnd {
				b.stack = append(b.stack, s.out)
			} else {
				set = append(set, i)
			}
		default:
			set = append(set, i)
		}
	}
	return set
}

func (b *dfaBuilder) hasMatch(set []int) bool {
	for _, i := range set {
		if b.nfa[i].kind == nfaMatch {
			return true
		}
	}
	return false
}

// key is a set as a map key: its states in increasing order.
func setKey(set []int) string {
	// Insertion sort: the sets are small.
	for i := 1; i < len(set); i++ {
		for j := i; j > 0 && set[j] < set[j-1]; j-- {
			set[j], set[j-1] = set[j-1], set[j]
		}
	}
	key := make([]byte, 0, 2*len(set))
	for _, s := range set {
		key = append(key, byte(s), byte(s>>8))
	}
	return string(key)
}

// buildDFA builds the automaton of a pattern for an unanchored search, or nil when it has none.
func buildDFA(root *node) *dfa {
	nb := &nfaBuilder{}
	match := nb.add(nfaState{kind: nfaMatch})
	start := nb.compile(root, match)
	if nb.failed {
		return nil
	}
	nfa := nb.states
	b := &dfaBuilder{nfa: nfa, mark: make([]uint32, len(nfa)), steps: maxDFASteps}

	// The classes of bytes: two bytes are in one class when every set of the pattern has both or neither. Class 0 is
	// the bytes that are not ASCII.
	d := &dfa{}
	classes := 1
	signatures := map[string]uint8{}
	signature := make([]byte, 0, len(nfa)/8+1)
	for c := 0; c < 128; c++ {
		signature = signature[:0]
		var acc, n byte
		for i := range nfa {
			if nfa[i].kind != nfaSet {
				continue
			}
			acc = acc<<1 | byte(nfa[i].set[c>>6]>>(uint(c)&63)&1)
			if n++; n == 8 {
				signature, acc, n = append(signature, acc), 0, 0
			}
		}
		signature = append(signature, acc)
		class, ok := signatures[string(signature)]
		if !ok {
			class = uint8(classes)
			classes++
			signatures[string(signature)] = class
		}
		d.class[c] = class
	}
	for 1<<d.shift < classes {
		d.shift++
	}
	// One byte of each class, to move a set by.
	var sample [129]int
	for c := 127; c >= 0; c-- {
		sample[d.class[c]] = c
	}

	// The search is unanchored: a match may start at any position, so after every byte the states the pattern
	// starts in (not at the start of the text) join the set.
	restart := b.closure(nil, []int{start}, false, false)
	d.empty = b.hasMatch(b.closure(nil, []int{start}, true, true))

	ids := map[string]uint8{}
	var sets [][]int
	stride := 1 << d.shift
	d.trans = make([]uint8, dfaFirst*stride)
	d.atEnd = make([]bool, dfaFirst)
	for class := 0; class < stride; class++ {
		d.trans[dfaAccept*stride+class] = dfaAccept
		d.trans[dfaBail*stride+class] = dfaBail
	}
	sets = append(sets, nil, nil, nil)
	// intern gives a set its state. Not ok when there are too many.
	intern := func(set []int) (uint8, bool) {
		if b.hasMatch(set) {
			return dfaAccept, true
		}
		if len(set) == 0 {
			return dfaDead, true
		}
		key := setKey(set)
		if id, ok := ids[key]; ok {
			return id, true
		}
		if len(sets) >= maxDFAStates {
			return 0, false
		}
		id := uint8(len(sets))
		ids[key] = id
		sets = append(sets, append([]int(nil), set...))
		d.trans = append(d.trans, make([]uint8, stride)...)
		// At the end of the text the end anchors are passed.
		var ends []int
		for _, i := range set {
			if nfa[i].kind == nfaEOL {
				ends = append(ends, nfa[i].out)
			}
		}
		d.atEnd = append(d.atEnd, len(ends) != 0 && b.hasMatch(b.closure(nil, ends, false, true)))
		return id, true
	}
	first, ok := intern(b.closure(nil, []int{start}, true, false))
	if !ok {
		return nil
	}
	d.start = first
	var moved, next []int
	for id := dfaFirst; id < len(sets); id++ {
		for class := 0; class < stride; class++ {
			if class == 0 || class >= classes {
				// Not ASCII (or no such class).
				d.trans[id*stride+class] = dfaBail
				continue
			}
			c := sample[class]
			moved = moved[:0]
			for _, i := range sets[id] {
				if s := &nfa[i]; s.kind == nfaSet && s.set[c>>6]>>(uint(c)&63)&1 != 0 {
					moved = append(moved, s.out)
				}
			}
			next = b.closure(next[:0], append(moved, restart...), false, false)
			to, ok := intern(next)
			if !ok || b.steps < 0 {
				return nil
			}
			d.trans[id*stride+class] = to
		}
	}
	return d
}
