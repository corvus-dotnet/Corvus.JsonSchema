# PCRE2, through the library Julia bundles and loads for its own Regex. Julia's Regex is not used, as `occursin` and
# `match` allocate the data of a match, and Julia compiles with options of its own (UCP among them). The functions
# called here are those of the PCRE2 API of version 10.30 and later, which do not change. Base.PCRE supplies the name
# of the library and the lock Julia compiles under.

const PCRE_LIB = Base.PCRE.PCRE_LIB

const PCRE2_UTF = 0x00080000
const PCRE2_MATCH_INVALID_UTF = 0x04000000
const PCRE2_AUTO_CALLOUT = 0x00000004
const PCRE2_NO_AUTO_POSSESS = 0x00004000
const PCRE2_NO_START_OPTIMIZE = 0x00010000
const PCRE2_NO_JIT = 0x00002000
const PCRE2_USE_OFFSET_LIMIT = 0x00800000
const PCRE2_ANCHORED = 0x80000000
const PCRE2_INFO_ALLOPTIONS = 0
const PCRE2_INFO_JITSIZE = 10
const PCRE2_INFO_SIZE = 22
const PCRE2_UNSET = typemax(Csize_t)
const PCRE2_JIT_COMPLETE = 0x00000001
const PCRE2_ERROR_NOMATCH = Cint(-1)
const PCRE2_ERROR_JIT_STACKLIMIT = Cint(-46)
const PCRE2_ERROR_MATCHLIMIT = Cint(-47)
const PCRE2_ERROR_NOMEMORY = Cint(-48)
const PCRE2_ERROR_DEPTHLIMIT = Cint(-53)
const PCRE2_ERROR_HEAPLIMIT = Cint(-63)
const PCRE2_CONFIG_UNICODE_VERSION = 10
const PCRE2_CONFIG_VERSION = 11

# The options every pattern is compiled with. The first two are the Unicode notes of EcmaRegex.jl.
#
# PCRE2_NO_START_OPTIMIZE turns off what PCRE2 works out about where a match can start (its first character, a
# character it must hold, its least length) to skip positions before it tries them. Each of the two PCRE2 versions
# the module was written against works that out wrongly for some patterns, and then says "no match" of a text that
# matches. PCRE2 10.42 (Julia 1.10) does for a pattern that starts with a lookahead, so `(?=b)a?b` does not match
# "b". PCRE2 10.46 (Julia 1.13) does for one that starts with a lazy optional item, so `b??a[ab]{0,3}?` does not
# match "a".
# Julia's own Regex gives both answers. Without the skipping a search of a long text is slower (about half a
# nanosecond a byte for a literal, where the skip takes a tenth of that), and a lookbehind that is a callout is
# searched for at every position of the text the pattern is tried at. A pattern anchored with ^ loses nothing.
#
# PCRE2_USE_OFFSET_LIMIT lets a match be told the last place it may start at, which a search of a long text in
# parts needs (see `searchinparts`). It changes nothing of a match that is not told one.
const COMPILE_OPTIONS = PCRE2_UTF | PCRE2_MATCH_INVALID_UTF | PCRE2_NO_START_OPTIMIZE | PCRE2_USE_OFFSET_LIMIT
# A pattern with a callout is also compiled with this one. PCRE2 makes a repetition possessive when what follows it
# cannot match what it repeats, and it looks past a callout to see what follows. A callout of this module can fail
# where the pattern after it would match, and the repetition must then give back what it took.
const CALLOUT_COMPILE_OPTIONS = PCRE2_NO_AUTO_POSSESS
# The body of a lookbehind that is a callout is also compiled with this one, which has PCRE2 call out before every
# item of the pattern, and so is the code of a pattern that a match is timed with (see The limit on the time of a
# match). The callouts count the items a match runs, and read the clock.
const TIMED_COMPILE_OPTIONS = PCRE2_AUTO_CALLOUT
# The number PCRE2 gives the callouts it adds.
const CALLOUT_AUTOMATIC = 255

# The limit on the time of a match.
#
# A schema and an instance may both come from outside, and a pattern of a few characters can take longer than the
# life of the process on a text of a few dozen. So a match has a limit on its time, which is one second unless the
# caller gives another, and a match that reaches it is a MatchError. The caller may turn the limit off.
#
# PCRE2 has no clock. It counts steps, where a step is roughly one place it may come back to, and gives a match up
# past a number of them (its match limit). The number does not bound the time. PCRE2 counts the steps of each place
# a match is tried from apart, so a search of a text of n characters has n times the limit. And one step may read
# the whole text: 10,000 steps of `^(?:a*b|a)*c` on 100,000 characters take 200 ms, and a loop around a lookahead
# that reads to the end of the text, three deep, runs for hours on them within a few million steps. The time of
# 500 million steps, which was the limit before, was measured from 0.7 seconds to more than the patience of the
# measurement.
#
# What PCRE2 does have is the callout: with PCRE2_AUTO_CALLOUT it calls a function before every item of the
# pattern, and the function can read a clock and end the match. That is exact, and it costs: the JIT code of a
# pattern with automatic callouts was measured 8 to 30 times slower on patterns that repeat a group, and 1.1 to 3
# times on others. So a match is run in two stages.
#
# 1. As before, by the code with no callouts and with no clock, with a number of steps that cannot take long
#    whatever the pattern and the text are. Between two steps PCRE2 goes forwards through the pattern, so the most
#    it can do there is known from the pattern and the length of the text (see `stepcost`): FREE_WORK units of
#    work, divided by that and by the number of places the match is tried from, is the number of steps of this
#    stage. Nearly every match of a pattern that is not hostile ends here, so it costs what it did.
# 2. A match that used those steps, or filled the stack of JIT code, is run again from the start by code with
#    automatic callouts (compiled when a pattern first needs it), which read the clock every CLOCK_INTERVAL items.
#    It ends with its answer or, at the limit, with the error.
#
# The search of a long text for a pattern that is not anchored is tried from too many places for the first stage to
# be given even a few steps for each. It is run in parts, each from so many places that a part cannot take long,
# with the clock read between two parts (see `searchinparts`).
#
# The units of work the first stage of a match may do for each second of its limit. A unit is a character read or
# an item of the pattern run, and was measured at 0.2 to 2.5 ns in JIT code, so the stage ends within a twentieth
# to a half of the limit and is not timed. The interpreter, which runs a pattern the JIT compiler does not take,
# is given a sixteenth.
const FREE_WORK = 200_000_000
const INTERPRETED_WORK_SHARE = 16
# The most steps of the first stage, and the fewest for each place of a pattern that is tried from every place of
# the text. A search that would be given fewer for a place is run in parts.
const MOST_FREE_STEPS = 10_000_000
const FEWEST_FREE_STEPS = 16
# The steps the context of a thread is left with, which a match of a text of at most `freelength` bytes is run
# with as it is, when its limit is a second or more or it has none: such a match sets nothing and works nothing
# out. One that uses them is given the steps of its first stage in full (see `slowmatch`).
const QUICK_STEPS = UInt32(1024)
# The automatic callouts between two readings of the clock. One item of a pattern reads the whole text at most, so
# on 100,000 characters this many take a few milliseconds at worst.
const CLOCK_INTERVAL = 64
# The limit on the time of a match when the caller gives none, in nanoseconds. Zero is no limit.
const DEFAULT_MATCH_LIMIT = UInt64(1_000_000_000)
const NO_MATCH_LIMIT = UInt64(0)
# The most steps PCRE2 can be told to allow. It is what a match is given once it is timed, and when the caller has
# turned the limit on time off, as PCRE2 cannot be told to count no steps.
const MOST_STEPS = typemax(UInt32)
# How deep the interpreter may nest what it backtracks over. It is not a limit of its own: the memory of the
# interpreter is limited by HEAP_LIMIT_KIB, which a match reaches long before this depth.
const DEPTH_LIMIT = MOST_STEPS
# What a callout answers to end a match at the limit on its time. PCRE2 gives a negative answer of a callout back as
# the result of the match, and has no result of this number.
const ERROR_TIME_LIMIT = Cint(-1001)

# Where the stack of the code the JIT compiler writes starts, and how far it grows. They are the sizes Julia gives
# its own.
const JIT_STACK_START = 32768
const JIT_STACK_MAX = 1048576
# The most memory the interpreter may take for one match, in kibibytes. It runs a match only when the JIT compiler
# did not take the pattern or its stack was too small for the match. `^(?:ab|cd)*$` fills it on a text of half a
# million characters, and the match is then a MatchError.
const HEAP_LIMIT_KIB = 65536

"""
    MatchError

Thrown when a match was given up, because it reached a limit. Whether the text matches is then not known, so it is
never reported as no match. `limit` says which limit:

- `:time`: the match ran for the limit on its time, which is one second unless the caller set another;
- `:memory`: the match needed more memory than the 64 MiB the interpreter of PCRE2 may take (`HEAP_LIMIT_KIB`),
  which a pattern that repeats a group reaches on a text of some hundreds of thousands of characters;
- `:steps`: with the limit on time turned off, the match took the 4,294,967,295 steps that are the most PCRE2 can
  be told to allow;
- `:engine`: any other error of PCRE2.

`pattern` is the ECMA-262 pattern, `code` the error of PCRE2 (0 for the limit on time, which is not PCRE2's) and
`message` names the limit and its value.
"""
struct MatchError <: Exception
    pattern::String
    code::Int
    message::String
    limit::Symbol
end

Base.showerror(io::IO, e::MatchError) =
    print(io, "EcmaRegex.MatchError: matching the pattern ", repr(e.pattern), " failed: ", e.message)

# The number of nanoseconds of a limit given in seconds, or with nothing no limit. A limit of no time, or of less
# than none, is not one, so it is an error and not "no limit".
function matchlimit(seconds::Union{Nothing,Real})::UInt64
    seconds === nothing && return NO_MATCH_LIMIT
    (isfinite(seconds) && seconds > 0) ||
        throw(ArgumentError("the limit on the time of a match is a positive number of seconds, or nothing for no limit"))
    return UInt64(clamp(ceil(Float64(seconds) * 1e9), 1.0, 9.0e18))
end

# A limit as the message of an error says it.
function limittext(limit::UInt64)
    seconds = limit / 1e9
    seconds == 1 && return "1 second"
    return string(isinteger(seconds) ? string(Int(seconds)) : string(seconds), " seconds")
end

function pcremessage(code::Integer)
    buffer = Vector{UInt8}(undef, 256)
    n = ccall((:pcre2_get_error_message_8, PCRE_LIB), Cint, (Cint, Ptr{UInt8}, Csize_t), code, buffer, 256)
    return n > 0 ? String(buffer[1:n]) : "PCRE2 error $code"
end

function pcreconfig(what::Integer)
    buffer = Vector{UInt8}(undef, 64)
    n = ccall((:pcre2_config_8, PCRE_LIB), Cint, (UInt32, Ptr{UInt8}), what, buffer)
    return n > 1 ? String(buffer[1:(n - 1)]) : ""
end

# The version of the PCRE2 in use, and of its Unicode tables. The module relies on those for one thing, which is
# the comparison of a case-insensitive backreference (see `comparesignoringcase`).
pcreversion() = pcreconfig(PCRE2_CONFIG_VERSION)
pcreunicodeversion() = pcreconfig(PCRE2_CONFIG_UNICODE_VERSION)

# Whether the PCRE2 in use searches a class of many ranges by halves, which it does from 10.45 on. Before that it
# tries the ranges one after another.
function pcreclassesarefast()
    version = tryparse(VersionNumber, first(split(pcreversion(), ' ')))
    return version !== nothing && version >= v"10.45"
end

# Julia compiles its own patterns under a lock, and this module compiles under the same one, as PCRE2's JIT compiler
# is shared with them. It is looked up at each use, as Base makes it when Julia starts.
const OWN_COMPILE_LOCK = Threads.SpinLock()

function compilelock()
    if isdefined(Base.PCRE, :PCRE_COMPILE_LOCK)
        lock = Base.PCRE.PCRE_COMPILE_LOCK
        lock isa Threads.SpinLock && return lock
    end
    return OWN_COMPILE_LOCK
end

# The options a translation is compiled with, from what it holds.
function compileoptions(pattern::String)
    options = COMPILE_OPTIONS
    occursin("(?C", pattern) && (options |= CALLOUT_COMPILE_OPTIONS)
    startswith(pattern, LOOKBEHIND_PREFIX) && (options |= TIMED_COMPILE_OPTIONS)
    return options
end

# What PCRE2 said of a pattern it does not compile: the number of its error and the message.
struct Refusal
    code::Int
    message::String
end

# Compiles one PCRE2 pattern, with the JIT compiler where PCRE2 has one, as Julia does by default. Returns the code,
# or what PCRE2 said when it does not take the pattern.
function pcrecompile(pattern::String, more::UInt32=UInt32(0))::Union{Ptr{Cvoid},Refusal}
    options = compileoptions(pattern) | more
    errorcode = Ref{Cint}(0)
    erroroffset = Ref{Csize_t}(0)
    compiling = compilelock()
    lock(compiling)
    try
        code = ccall((:pcre2_compile_8, PCRE_LIB), Ptr{Cvoid},
            (Ptr{UInt8}, Csize_t, UInt32, Ref{Cint}, Ref{Csize_t}, Ptr{Cvoid}),
            pattern, ncodeunits(pattern), options, errorcode, erroroffset, C_NULL)
        code == C_NULL && return Refusal(errorcode[], pcremessage(errorcode[]))
        # A pattern the JIT compiler does not take, or a PCRE2 built without it, is run by the interpreter. PCRE2
        # does not say the same thing in every version when it does not take one, so the result is not looked at
        # (see `hasjitcode`).
        ccall((:pcre2_jit_compile_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), code, PCRE2_JIT_COMPLETE)
        return code
    finally
        unlock(compiling)
    end
end

function patterninfo(code::Ptr{Cvoid}, what::Integer, ::Type{T}) where {T}
    value = Ref{T}(0)
    ccall((:pcre2_pattern_info_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32, Ref{T}), code, what, value)
    return value[]
end

# Whether the JIT compiler took a pattern, and whether PCRE2 tries it from the start of the text only.
hasjitcode(code::Ptr{Cvoid}) = patterninfo(code, PCRE2_INFO_JITSIZE, Csize_t) > 0
isanchored(code::Ptr{Cvoid}) = patterninfo(code, PCRE2_INFO_ALLOPTIONS, UInt32) & PCRE2_ANCHORED != 0
# The bytes PCRE2 keeps for a compiled pattern: its code and its JIT code.
codesize(code::Ptr{Cvoid}) =
    Int(patterninfo(code, PCRE2_INFO_SIZE, Csize_t)) + Int(patterninfo(code, PCRE2_INFO_JITSIZE, Csize_t))

pcrefree(code::Ptr{Cvoid}) = ccall((:pcre2_code_free_8, PCRE_LIB), Cvoid, (Ptr{Cvoid},), code)

# A compiled pattern. It may be matched from several tasks at once. PCRE2 does not write to compiled code, and what
# a match writes to belongs to the thread (see `Frame`).
#
# A pattern that was compiled while a package was precompiled comes back with no code, as Julia does not keep a
# pointer in what it saves. It is compiled again from its translation the first time it is matched (see `restore!`).
mutable struct Pattern
    # The pattern PCRE2 searches for, or C_NULL.
    code::Ptr{Cvoid}
    # The body of each lookbehind that is a callout, by its callout number less CALLOUT_FIRST_LOOKBEHIND.
    lookbehinds::Vector{Ptr{Cvoid}}
    # The ECMA-262 pattern, and whether the grammar of the u flag accepted it.
    source::String
    unicode::Bool
    translation::Translation
    # The pattern compiled again with automatic callouts, for a match that is timed, or C_NULL until a match needs
    # it (see `timedcode`).
    timed::Ptr{Cvoid}
    # The most work PCRE2 does between two steps of a match of the pattern: so much, and so much for each byte of
    # the text (see `stepcost`).
    fixedcost::Int
    textcost::Int
    # Whether PCRE2 tries the pattern from the start of the text only, and whether the JIT compiler took it.
    anchored::Bool
    jit::Bool
    # The longest text, in bytes, that QUICK_STEPS are within the first stage of a match of, or -1.
    freelength::Int

    function Pattern(code::Ptr{Cvoid}, lookbehinds::Vector{Ptr{Cvoid}}, source::String, unicode::Bool,
            translation::Translation)
        fixedcost, textcost = stepcost(translation.main)
        anchored, jit = isanchored(code), hasjitcode(code)
        return finalizer(freepattern, new(code, lookbehinds, source, unicode, translation, C_NULL, fixedcost,
            textcost, anchored, jit, freelength(fixedcost, textcost, anchored, jit)))
    end
end

function freepattern(p::Pattern)
    p.code == C_NULL || pcrefree(p.code)
    p.code = C_NULL
    p.timed == C_NULL || pcrefree(p.timed)
    p.timed = C_NULL
    foreach(pcrefree, p.lookbehinds)
    empty!(p.lookbehinds)
    return nothing
end

Base.show(io::IO, p::Pattern) = print(io, "EcmaRegex.Pattern(", repr(p.source), ")")

# Compiles the patterns of a translation. Returns their codes, the one PCRE2 searches for first, or what PCRE2 said
# when it does not take one of them.
function pcrecompile(t::Translation)::Union{Vector{Ptr{Cvoid}},Refusal}
    codes = Ptr{Cvoid}[]
    for text in Iterators.flatten(((t.main,), t.lookbehinds))
        code = pcrecompile(text)
        if code isa Refusal
            foreach(pcrefree, codes)
            return code
        end
        push!(codes, code)
    end
    return codes
end

# Compiles a translation. Returns the pattern, or what PCRE2 said when it does not take some part of it.
function pcrecompile(t::Translation, source::String, unicode::Bool)::Union{Pattern,Refusal}
    codes = pcrecompile(t)
    codes isa Refusal && return codes
    return Pattern(codes[1], codes[2:end], source, unicode, t)
end

const RESTORE_LOCK = ReentrantLock()

# The code of a pattern with automatic callouts, which a match that is timed runs. It is compiled when a match of
# the pattern first needs it, so a pattern whose matches all end in their first stage keeps one code. A pattern
# PCRE2 compiled without callouts and does not compile with them (they add to the size of the code, which has a
# limit) cannot be timed, and the match that needed it is an error.
@noinline function timedcode(p::Pattern)::Ptr{Cvoid}
    lock(RESTORE_LOCK)
    try
        if p.timed == C_NULL
            code = pcrecompile(p.translation.main, TIMED_COMPILE_OPTIONS)
            code isa Refusal && throw(MatchError(p.source, code.code,
                "the match used the steps it has before it is timed, and PCRE2 does not compile the pattern with the callouts that time it: " *
                code.message, :engine))
            p.timed = code
        end
        return p.timed
    finally
        unlock(RESTORE_LOCK)
    end
end

# The codes a pattern has been compiled to for timing, for the tests and the probes.
timedcodes(p::Pattern) = p.timed == C_NULL ? Ptr{Cvoid}[] : Ptr{Cvoid}[p.timed]

# Compiles again a pattern that has lost its code.
@noinline function restore!(p::Pattern)
    lock(RESTORE_LOCK)
    try
        p.code == C_NULL || return nothing
        codes = pcrecompile(p.translation)
        codes isa Refusal && throw(MatchError(p.source, codes.code,
            "PCRE2 does not compile the pattern again: " * codes.message, :engine))
        p.lookbehinds = codes[2:end]
        p.anchored = isanchored(codes[1])
        p.jit = hasjitcode(codes[1])
        p.freelength = freelength(p.fixedcost, p.textcost, p.anchored, p.jit)
        p.code = codes[1]
        return nothing
    finally
        unlock(RESTORE_LOCK)
    end
end

# What one call of pcre2_match writes to. That is the data of the match, and the context that holds the limits, the
# stack of JIT code and the callout. Each thread has a list of frames. The first is for the pattern, and the next is
# for a lookbehind that is a callout, whose body may hold another, and so on. A match runs to its end on the thread
# that started it without yielding, so a frame is never used by two tasks at once. Frames are C memory and are kept
# for the life of the process.
struct Frame
    matchdata::Ptr{Cvoid}
    context::Ptr{Cvoid}
    next::Ptr{Frame}
    # The pattern being matched, for a callout to find the bodies of its lookbehinds.
    pattern::Ptr{Cvoid}
    # The position of the lookbehind whose body this frame is matching.
    target::Csize_t
    # The options of pcre2_match for the match and for every lookbehind under it.
    options::UInt32
    # The first frame of the thread, which counts the items that the code with automatic callouts has run in a
    # match, in the pattern and in the searches for its lookbehinds.
    root::Ptr{Frame}
    steps::Csize_t
    # In the first frame: the time on the clock at which the match is given up, or 0 while it is not timed, and
    # the most items it may run while it is not.
    deadline::UInt64
    mostitems::Csize_t
    # The callout the frame's context calls, or C_NULL for a frame that has only matched patterns with no callout
    # (see calloutframe).
    callout::Ptr{Cvoid}
end

const FRAME_NEXT = fieldoffset(Frame, 3)
const FRAME_PATTERN = fieldoffset(Frame, 4)
const FRAME_TARGET = fieldoffset(Frame, 5)
const FRAME_OPTIONS = fieldoffset(Frame, 6)
const FRAME_ROOT = fieldoffset(Frame, 7)
const FRAME_STEPS = fieldoffset(Frame, 8)
const FRAME_DEADLINE = fieldoffset(Frame, 9)
const FRAME_MOSTITEMS = fieldoffset(Frame, 10)
const FRAME_CALLOUT = fieldoffset(Frame, 11)
const FRAME_MATCHDATA = fieldoffset(Frame, 1)

# The first fields of pcre2_callout_block, which are the same in every version of it.
struct CalloutBlock
    version::UInt32
    callout_number::UInt32
    capture_top::UInt32
    capture_last::UInt32
    offset_vector::Ptr{Csize_t}
    mark::Ptr{UInt8}
    subject::Ptr{UInt8}
    subject_length::Csize_t
    start_match::Csize_t
    current_position::Csize_t
end

const CALLOUT_NUMBER = fieldoffset(CalloutBlock, 2)

# The first frame of each thread, by the number of the thread. The vector is replaced when it grows.
mutable struct Frames
    @atomic first::Vector{Ptr{Frame}}
end

const FRAMES = Frames(Ptr{Frame}[])
# A lock a task may wait on: making the callout evaluates its definition.
const FRAME_LOCK = ReentrantLock()
# The callout as a C function, made when a pattern that has a callout is first matched (see calloutframe).
const CALLOUT = Ref{Ptr{Cvoid}}(C_NULL)

# The pointers kept when the package was precompiled mean nothing in the process that loads it.
function __init__()
    @atomic FRAMES.first = Ptr{Frame}[]
    CALLOUT[] = C_NULL
    return nothing
end

@inline function pcrematch(code::Ptr{Cvoid}, subject::Ptr{UInt8}, len::Csize_t, options::UInt32, frame::Frame,
        from::Csize_t=Csize_t(0))
    return ccall((:pcre2_match_8, PCRE_LIB), Cint,
        (Ptr{Cvoid}, Ptr{UInt8}, Csize_t, Csize_t, UInt32, Ptr{Cvoid}, Ptr{Cvoid}),
        code, subject, len, from, options, frame.matchdata, frame.context)
end

# The most steps PCRE2 allows each place a match is tried from, and the last place it may be tried from, in the
# context of a frame.
@inline setsteps!(frame::Frame, steps::UInt32) =
    ccall((:pcre2_set_match_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), frame.context, steps)
@inline setlaststart!(frame::Frame, offset::Csize_t) =
    ccall((:pcre2_set_offset_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, Csize_t), frame.context, offset)

# Says when the match that the first frame of a thread runs is given up (0 while it is not timed), and how many
# items its code with automatic callouts may run until it is.
@inline function settiming!(frame::Ptr{Frame}, deadline::UInt64, mostitems::Csize_t)
    unsafe_store!(Ptr{UInt64}(frame + FRAME_DEADLINE), deadline)
    unsafe_store!(Ptr{Csize_t}(frame + FRAME_MOSTITEMS), mostitems)
    return nothing
end

# Says which pattern a frame is matching, and with which options. In the first frame of a thread it starts a match.
@inline function setframe!(frame::Ptr{Frame}, pattern::Ptr{Cvoid}, target::Csize_t, options::UInt32)
    unsafe_store!(Ptr{Ptr{Cvoid}}(frame + FRAME_PATTERN), pattern)
    unsafe_store!(Ptr{Csize_t}(frame + FRAME_TARGET), target)
    unsafe_store!(Ptr{UInt32}(frame + FRAME_OPTIONS), options)
    unsafe_store!(Ptr{Csize_t}(frame + FRAME_STEPS), Csize_t(0))
    return nothing
end

# What PCRE2 calls at a callout of a pattern (see `lookbehind` in emitter.jl). It answers 0 for the match to go on,
# 1 for it to fail at this point and try another way, and a negative number to end it with that result.
function callout(block::Ptr{CalloutBlock}, frameptr::Ptr{Frame})::Cint
    number = unsafe_load(Ptr{UInt32}(block + CALLOUT_NUMBER))
    if number == CALLOUT_AUTOMATIC
        root = unsafe_load(Ptr{Ptr{Frame}}(frameptr + FRAME_ROOT))
        counter = Ptr{Csize_t}(root + FRAME_STEPS)
        steps = unsafe_load(counter) + 1
        unsafe_store!(counter, steps)
        deadline = unsafe_load(Ptr{UInt64}(root + FRAME_DEADLINE))
        if deadline == 0
            # The first stage of a match, which is not timed: the searches for its lookbehinds run so many items.
            return steps > unsafe_load(Ptr{Csize_t}(root + FRAME_MOSTITEMS)) ? PCRE2_ERROR_MATCHLIMIT : Cint(0)
        end
        return steps % CLOCK_INTERVAL == 0 && time_ns() >= deadline ? ERROR_TIME_LIMIT : Cint(0)
    end
    b = unsafe_load(block)
    frame = unsafe_load(frameptr)
    if number == CALLOUT_AT_TARGET
        return b.current_position == frame.target ? Cint(0) : Cint(1)
    elseif number == CALLOUT_AT_START
        return b.current_position > 0 ? PCRE2_ERROR_NOMATCH : Cint(0)
    elseif number == CALLOUT_BEFORE_TARGET
        return b.current_position < frame.target ? Cint(0) : Cint(1)
    end
    # The callout of a lookbehind. Its body is searched for in the whole text, with the next frame.
    pattern = unsafe_pointer_to_objref(frame.pattern)::Pattern
    next = frame.next
    if next == C_NULL
        # A callout is running, so the C function it is called through exists.
        next = try
            newframe(CALLOUT[], frame.root)
        catch
            return PCRE2_ERROR_NOMEMORY
        end
        unsafe_store!(Ptr{Ptr{Frame}}(frameptr + FRAME_NEXT), next)
    end
    setframe!(next, frame.pattern, b.current_position, frame.options)
    code = pattern.lookbehinds[number - CALLOUT_FIRST_LOOKBEHIND + 1]
    rc = pcrematch(code, b.subject, b.subject_length, frame.options, unsafe_load(next))
    rc >= 0 && return Cint(0)
    rc == PCRE2_ERROR_NOMATCH && return Cint(1)
    return rc
end

# The callout as a C function. In the process that runs it is made by evaluating, when it is first needed, the
# expression that @cfunction stands for, and not by writing @cfunction in a method. A @cfunction in a method of a
# package is compiled with the package, and the C function Julia 1.10 then makes calls the callout through dispatch,
# which allocates at every call. Evaluated in the process that runs, it calls the compiled method. The expression
# names nothing and defines nothing, and it is evaluated in a module made for it, which nothing else can see, so it
# touches neither Main nor the names of anyone's code.
#
# While a package is being precompiled (this one, or one that uses it and matches a pattern as it loads) Julia lets
# nothing be evaluated into a module that is not part of that package. The @cfunction of a method serves there: what
# it allocates on Julia 1.10 does not matter in that process, and __init__ drops the pointer when the package loads.
function makecallout()
    if ccall(:jl_generating_output, Cint, ()) != 0
        return @cfunction(callout, Cint, (Ptr{CalloutBlock}, Ptr{Frame}))
    end
    arguments = Core.svec(Ptr{CalloutBlock}, Ptr{Frame})
    expression = Expr(:cfunction, Ptr{Cvoid}, QuoteNode(callout), Cint, arguments, QuoteNode(:ccall))
    return Core.eval(Module(:EcmaRegexCallout, false, false), expression)::Ptr{Cvoid}
end

function newframe(calloutfunction::Ptr{Cvoid}, root::Ptr{Frame})
    matchdata = ccall((:pcre2_match_data_create_8, PCRE_LIB), Ptr{Cvoid}, (UInt32, Ptr{Cvoid}), 1, C_NULL)
    context = ccall((:pcre2_match_context_create_8, PCRE_LIB), Ptr{Cvoid}, (Ptr{Cvoid},), C_NULL)
    stack = ccall((:pcre2_jit_stack_create_8, PCRE_LIB), Ptr{Cvoid}, (Csize_t, Csize_t, Ptr{Cvoid}),
        JIT_STACK_START, JIT_STACK_MAX, C_NULL)
    frame = Ptr{Frame}(Libc.malloc(sizeof(Frame)))
    if matchdata == C_NULL || context == C_NULL || frame == C_NULL
        throw(OutOfMemoryError())
    end
    # A PCRE2 with no JIT compiler has no stack to give.
    if stack != C_NULL
        ccall((:pcre2_jit_stack_assign_8, PCRE_LIB), Cvoid, (Ptr{Cvoid}, Ptr{Cvoid}, Ptr{Cvoid}), context, C_NULL,
            stack)
    end
    # The limits on depth and memory are set once, in the context a frame keeps. A match sets the steps of its
    # first frame. The search for a lookbehind, which the other frames run, is limited by the items its callouts
    # count and by the clock they read, and has every step PCRE2 can give it.
    ccall((:pcre2_set_match_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), context, MOST_STEPS)
    ccall((:pcre2_set_depth_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), context, DEPTH_LIMIT)
    ccall((:pcre2_set_heap_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), context, HEAP_LIMIT_KIB)
    ccall((:pcre2_set_callout_8, PCRE_LIB), Cint, (Ptr{Cvoid}, Ptr{Cvoid}, Ptr{Cvoid}), context, calloutfunction,
        frame)
    unsafe_store!(frame, Frame(matchdata, context, C_NULL, C_NULL, 0, 0, root == C_NULL ? frame : root, 0, 0,
        typemax(Csize_t), calloutfunction))
    return frame
end

# The first frame of the thread the task is running on. Making a thread's frame takes a lock, which a task may wait
# on and then be resumed on another thread, so the frame is read again afterwards: the one returned was read with
# no wait since the thread's number was.
@inline function threadframe()
    while true
        tid = Threads.threadid()
        frames = @atomic FRAMES.first
        if tid <= length(frames)
            frame = frames[tid]
            frame == C_NULL || return frame
        end
        newthreadframe(tid)
    end
end

@noinline function newthreadframe(tid::Int)
    lock(FRAME_LOCK)
    try
        frames = @atomic FRAMES.first
        if tid > length(frames)
            grown = fill(Ptr{Frame}(C_NULL), max(tid, Threads.maxthreadid()))
            copyto!(grown, frames)
            frames = grown
            @atomic FRAMES.first = grown
        end
        frame = frames[tid]
        if frame == C_NULL
            # With no callout: see calloutframe.
            frame = newframe(Ptr{Cvoid}(C_NULL), Ptr{Frame}(C_NULL))
            setsteps!(unsafe_load(frame), QUICK_STEPS)
            frames[tid] = frame
        end
        return frame
    finally
        unlock(FRAME_LOCK)
    end
end

# The first frame of the thread the task is running on, with the callout set in its context.
#
# Only a pattern with a lookbehind of no fixed length has callouts, and the C function they are called through
# takes Julia several milliseconds to make, in every process. It is made when such a pattern is first matched, and
# set in a thread's frame when the thread first matches one. Before, it was made with the first frame, so the first
# match of any pattern in a process paid for it, which was most of the first validation pass of a schema with a
# pattern on the engine.
@noinline function calloutframe()::Ptr{Frame}
    if CALLOUT[] == C_NULL
        lock(FRAME_LOCK)
        try
            if CALLOUT[] == C_NULL
                CALLOUT[] = makecallout()
            end
        finally
            unlock(FRAME_LOCK)
        end
    end
    # The frame of the thread the task is on now. Nothing waits between this read and the match.
    frame = threadframe()
    if unsafe_load(Ptr{Ptr{Cvoid}}(frame + FRAME_CALLOUT)) == C_NULL
        ccall((:pcre2_set_callout_8, PCRE_LIB), Cint, (Ptr{Cvoid}, Ptr{Cvoid}, Ptr{Cvoid}), unsafe_load(frame).context,
            CALLOUT[], frame)
        unsafe_store!(Ptr{Ptr{Cvoid}}(frame + FRAME_CALLOUT), CALLOUT[])
    end
    return frame
end

# The work of a match between two steps.
#
# PCRE2 counts a step where a match may come back to: a place the pattern is tried from, a turn of a repetition it
# may give back, an alternative, a lookaround, a call. Between two steps it goes forwards through the pattern. Going
# forwards it runs each item once, a counted repetition its count of times, and a repetition with no upper count as
# often as there is text for it, since each turn takes a character at least. So the most it does between two steps
# is so much, and so much for each byte of the text. `stepcost` reads the two numbers off a translation: every item
# is a unit, a class is more by its length (PCRE2 before 10.45 tries the ranges of a class one after another, and
# PCRE2 since then halves them), a call is CALL_COST, a body under a count is its cost times the count, and a body under a repetition with no upper
# count is its cost once and once more for each byte. The cost of reading one byte, which is that of the dearest
# item, is added for each byte.
#
# The numbers are bounds for the first stage of a match and decide nothing else: a pattern whose cost is put too
# high is timed sooner than it need be, and one whose cost is put too low has a first stage that takes longer.
const CALL_COST = 16
const MOST_COST = 1 << 40
# The bytes of a class that cost a unit more, where PCRE2 tries its ranges one after another.
const CLASS_BYTES_A_UNIT = 48
# Whether it does. With nothing, it is asked of the PCRE2 in use. The tests set it to try both.
const SLOW_CLASSES = Ref{Union{Nothing,Bool}}(nothing)

function classcost(bytes::Int)
    something(SLOW_CLASSES[], !pcreclassesarefast()) && return 1 + bytes ÷ CLASS_BYTES_A_UNIT
    return 1 + (8 * sizeof(Int) - leading_zeros(bytes ÷ CLASS_BYTES_A_UNIT))
end

mutable struct CostReader
    const bytes::Base.CodeUnits{UInt8,String}
    at::Int
    # The cost of the dearest item that takes a character.
    dearest::Int
    depth::Int
    failed::Bool
end

function stepcost(translation::String)
    reader = CostReader(codeunits(translation), 1, 1, 0, false)
    fixed, text = sequencecost(reader)
    if reader.failed || reader.at <= length(reader.bytes)
        # Not what the emitter writes. Every byte of it is taken for an item under a repetition.
        n = min(ncodeunits(translation) + 1, MOST_COST)
        return n, n
    end
    return min(fixed + 1, MOST_COST), min(text + reader.dearest, MOST_COST)
end

costsum(a::Int, b::Int) = min(a + b, MOST_COST)
costtimes(a::Int, n::Int) = a == 0 || n == 0 ? 0 : (a > MOST_COST ÷ n ? MOST_COST : a * n)

# The cost of a sequence of items and alternatives, up to the parenthesis that closes it or the end. Alternatives
# are added, as if a match went through each.
function sequencecost(r::CostReader)
    fixed = 0
    text = 0
    bytes = r.bytes
    while r.at <= length(bytes) && !r.failed
        b = bytes[r.at]
        if b == UInt8(')')
            break
        elseif b == UInt8('|')
            r.at += 1
            continue
        end
        itemfixed, itemtext = itemcost(r)
        itemfixed, itemtext = repeatedcost(r, itemfixed, itemtext)
        fixed = costsum(fixed, itemfixed)
        text = costsum(text, itemtext)
    end
    return fixed, text
end

# Moves past the next byte `b` at or after the reader, or fails.
function skippast!(r::CostReader, b::UInt8)
    at = findnext(==(b), r.bytes, r.at)
    if at === nothing
        r.failed = true
        r.at = length(r.bytes) + 1
    else
        r.at = at + 1
    end
    return nothing
end

peek(r::CostReader, ahead::Int=0) = r.at + ahead <= length(r.bytes) ? r.bytes[r.at + ahead] : 0x00

# The cost of one item, which the reader stands at.
function itemcost(r::CostReader)
    bytes = r.bytes
    b = bytes[r.at]
    if b == UInt8('(')
        return groupcost(r)
    elseif b == UInt8('[')
        start = r.at
        r.at += 1
        peek(r) == UInt8('^') && (r.at += 1)
        peek(r) == UInt8(']') && (r.at += 1)
        while r.at <= length(bytes) && bytes[r.at] != UInt8(']')
            r.at += bytes[r.at] == UInt8('\\') ? 2 : 1
        end
        r.at > length(bytes) && (r.failed = true)
        r.at += 1
        cost = classcost(r.at - start)
        r.dearest = max(r.dearest, cost)
        return cost, 0
    elseif b == UInt8('\\')
        r.at += 1
        c = peek(r)
        r.at += 1
        if c == UInt8('k') || c == UInt8('g')
            # A backreference. What it compares is text it takes.
            peek(r) == UInt8('<') ? skippast!(r, UInt8('>')) : peek(r) == UInt8('{') && skippast!(r, UInt8('}'))
            return 2, 0
        elseif UInt8('1') <= c <= UInt8('9')
            while UInt8('0') <= peek(r) <= UInt8('9')
                r.at += 1
            end
            return 2, 0
        elseif (c == UInt8('x') || c == UInt8('p') || c == UInt8('P') || c == UInt8('o') || c == UInt8('N')) &&
               peek(r) == UInt8('{')
            skippast!(r, UInt8('}'))
        elseif c == UInt8('Q')
            r.failed = true
        end
        return 1, 0
    end
    # A character, with the bytes that continue it.
    r.at += 1
    while peek(r) & 0xC0 == 0x80
        r.at += 1
    end
    return 1, 0
end

# The cost of what stands in parentheses, which the reader stands at.
function groupcost(r::CostReader)
    r.depth += 1
    if r.depth > 1000
        r.failed = true
        return 0, 0
    end
    r.at += 1
    fixed = 1
    text = 0
    if peek(r) == UInt8('*')
        # A verb.
        skippast!(r, UInt8(')'))
        r.depth -= 1
        return 1, 0
    elseif peek(r) == UInt8('?')
        r.at += 1
        c = peek(r)
        if c == UInt8('&') || c == UInt8('C') || c == UInt8('R') || c == UInt8('P') && peek(r, 1) == UInt8('>')
            # A call of a group, which costs what a class of many ranges does, or a callout.
            skippast!(r, UInt8(')'))
            r.depth -= 1
            c == UInt8('C') && return 1, 0
            r.dearest = max(r.dearest, 2 * CALL_COST)
            return CALL_COST, 0
        elseif c == UInt8('(')
            # A conditional group: its condition, then its alternatives.
            fixed, text = groupcost(r)
        elseif c == UInt8('<') && (peek(r, 1) == UInt8('=') || peek(r, 1) == UInt8('!'))
            r.at += 2
        elseif c == UInt8('<') || c == UInt8('P') && peek(r, 1) == UInt8('<')
            skippast!(r, UInt8('>'))
        elseif c == UInt8('=') || c == UInt8('!') || c == UInt8('>') || c == UInt8('|') || c == UInt8(':')
            r.at += 1
        else
            # Options, as in (?s:.), or with nothing after them.
            while r.at <= length(r.bytes) && peek(r) != UInt8(':') && peek(r) != UInt8(')')
                r.at += 1
            end
            peek(r) == UInt8(':') && (r.at += 1)
        end
    end
    bodyfixed, bodytext = sequencecost(r)
    peek(r) == UInt8(')') || (r.failed = true)
    r.at += 1
    r.depth -= 1
    return costsum(fixed, bodyfixed), costsum(text, bodytext)
end

# The cost of an item under the quantifier the reader stands at, if it stands at one.
function repeatedcost(r::CostReader, fixed::Int, text::Int)
    b = peek(r)
    least = 0
    most = -1
    if b == UInt8('*')
        r.at += 1
    elseif b == UInt8('+')
        least = 1
        r.at += 1
    elseif b == UInt8('?')
        most = 1
        r.at += 1
    elseif b == UInt8('{')
        at = r.at + 1
        digits = at
        while at <= length(r.bytes) && UInt8('0') <= r.bytes[at] <= UInt8('9')
            least = min(least * 10 + Int(r.bytes[at] - UInt8('0')), MOST_COST)
            at += 1
        end
        at == digits && return fixed, text
        if at <= length(r.bytes) && r.bytes[at] == UInt8(',')
            at += 1
            digits = at
            most = 0
            while at <= length(r.bytes) && UInt8('0') <= r.bytes[at] <= UInt8('9')
                most = min(most * 10 + Int(r.bytes[at] - UInt8('0')), MOST_COST)
                at += 1
            end
            at == digits && (most = -1)
        else
            most = least
        end
        (at <= length(r.bytes) && r.bytes[at] == UInt8('}')) || return fixed, text
        r.at = at + 1
    else
        return fixed, text
    end
    # Lazy or possessive.
    (peek(r) == UInt8('?') || peek(r) == UInt8('+')) && (r.at += 1)
    most >= 0 && return costtimes(fixed, most), costtimes(text, most)
    return costsum(costtimes(fixed, least), fixed), costsum(costsum(costtimes(text, least), text), fixed)
end

# The units of work the first stage of a match of a pattern may do, for a limit in nanoseconds: FREE_WORK for a
# second, and in proportion for another limit (to the millisecond, and for at most a thousand seconds).
function freework(jit::Bool, limit::UInt64)
    work = (FREE_WORK ÷ 1000) * Int(min(limit ÷ 1_000_000, UInt64(1_000_000)))
    return jit ? work : work ÷ INTERPRETED_WORK_SHARE
end

# The steps of the first stage of a match of a text of `len` bytes for each place it is tried from, which may be
# none: the work of the stage, by the most work between two steps, by the places.
function freesteps(fixedcost::Int, textcost::Int, anchored::Bool, jit::Bool, len::Int, limit::UInt64)
    cost = fixedcost + costtimes(textcost, len)
    steps = freework(jit, limit) ÷ cost
    anchored || (steps ÷= len + 1)
    return min(steps, MOST_FREE_STEPS)
end

# The longest text QUICK_STEPS are within the first stage of a match of, when the limit is a second, or -1 when
# they are not for any.
function freelength(fixedcost::Int, textcost::Int, anchored::Bool, jit::Bool)
    freesteps(fixedcost, textcost, anchored, jit, 0, DEFAULT_MATCH_LIMIT) >= QUICK_STEPS || return -1
    low = 0
    # A longer match of a pattern the interpreter runs goes the way that gives back its memory (see `freshdata!`).
    high = jit ? 1 << 31 : DATA_KEPT_LENGTH
    while low < high
        middle = (low + high + 1) >>> 1
        if freesteps(fixedcost, textcost, anchored, jit, middle, DEFAULT_MATCH_LIMIT) >= QUICK_STEPS
            low = middle
        else
            high = middle - 1
        end
    end
    return low
end

# The items the searches for the lookbehinds of a match may run in its first stage. An item reads the text once at
# most.
freeitems(len::Int, limit::UInt64) = Csize_t(freework(true, limit) ÷ (len + 1))

# PCRE2 does not take a null text, even an empty one, in every version.
const EMPTY_TEXT = UInt8[0]

# Whether the pattern matches anywhere in the bytes, within the limit on the time of a match, in nanoseconds
# (NO_MATCH_LIMIT for none). The caller keeps the pattern and the bytes alive.
function unsafe_ismatch(p::Pattern, subject::Ptr{UInt8}, len::Int, limit::UInt64=DEFAULT_MATCH_LIMIT)::Bool
    if len == 0
        subject = pointer(EMPTY_TEXT)
    end
    p.code == C_NULL && restore!(p)
    # A limit of a second or more, or none (which is 0, and one less than it the largest number there is).
    (len <= p.freelength && limit - 1 >= DEFAULT_MATCH_LIMIT - 1) || return slowmatch(p, subject, len, limit, Cint(0))
    frameptr = threadframe()
    if !isempty(p.lookbehinds)
        if unsafe_load(Ptr{Ptr{Cvoid}}(frameptr + FRAME_CALLOUT)) == C_NULL
            frameptr = calloutframe()
        end
        setframe!(frameptr, pointer_from_objref(p), Csize_t(0), UInt32(0))
        settiming!(frameptr, UInt64(0), freeitems(len, DEFAULT_MATCH_LIMIT))
    end
    # The context of the thread has QUICK_STEPS, which every match leaves it with.
    rc = pcrematch(p.code, subject, Csize_t(len), UInt32(0), unsafe_load(frameptr))
    rc >= 0 && return true
    rc == PCRE2_ERROR_NOMATCH && return false
    return slowmatch(p, subject, len, limit, rc)
end

# The error of a match that ended in `rc`.
@noinline function matcherror(p::Pattern, rc::Cint, limit::UInt64)
    if rc == ERROR_TIME_LIMIT
        return MatchError(p.source, 0, string("the match ran for the limit on its time, which is ", limittext(limit)),
            :time)
    elseif rc == PCRE2_ERROR_HEAPLIMIT
        return MatchError(p.source, rc, string("the match needed more memory than the limit of ",
            HEAP_LIMIT_KIB ÷ 1024, " MiB (PCRE2 error ", rc, ", ", pcremessage(rc), ")"), :memory)
    elseif rc == PCRE2_ERROR_MATCHLIMIT
        return MatchError(p.source, rc, string("the match took the ", MOST_STEPS,
            " steps that are the most PCRE2 allows (PCRE2 error ", rc, ", ", pcremessage(rc), ")"), :steps)
    end
    return MatchError(p.source, rc, string(pcremessage(rc), " (PCRE2 error ", rc, ")"), :engine)
end

# One run of the code that has no automatic callouts, from the start of the text, with so many steps for each
# place it is tried from. The context of the thread is left with QUICK_STEPS.
function plainmatch(p::Pattern, subject::Ptr{UInt8}, len::Int, steps::UInt32, items::Csize_t, options::UInt32)::Cint
    frameptr = threadframe()
    if !isempty(p.lookbehinds)
        frameptr = calloutframe()
        setframe!(frameptr, pointer_from_objref(p), Csize_t(0), options)
        settiming!(frameptr, UInt64(0), items)
    end
    frame = unsafe_load(frameptr)
    setsteps!(frame, steps)
    rc = pcrematch(p.code, subject, Csize_t(len), options, frame)
    setsteps!(frame, QUICK_STEPS)
    (options == PCRE2_NO_JIT || !p.jit) && len > DATA_KEPT_LENGTH && freshdata!(frameptr)
    return rc
end

# The interpreter keeps what it backtracks over in memory that the data of a match holds until it is freed, which
# is up to HEAP_LIMIT_KIB. A frame is kept for the life of the process, so after a match of a long text that the
# interpreter ran its data is made again, and a deep match does not leave its memory with the thread. (Making it
# again was measured at one to three milliseconds after a match of 100,000 characters. A text of at most
# DATA_KEPT_LENGTH bytes has the interpreter keep a megabyte or so at most, which stays.)
const DATA_KEPT_LENGTH = 4096

function freshdata!(frameptr::Ptr{Frame})
    # The frame of the match, and those of the searches for lookbehinds under it.
    while frameptr != C_NULL
        matchdata = ccall((:pcre2_match_data_create_8, PCRE_LIB), Ptr{Cvoid}, (UInt32, Ptr{Cvoid}), 1, C_NULL)
        matchdata == C_NULL && return nothing
        ccall((:pcre2_match_data_free_8, PCRE_LIB), Cvoid, (Ptr{Cvoid},), unsafe_load(frameptr).matchdata)
        unsafe_store!(Ptr{Ptr{Cvoid}}(frameptr + FRAME_MATCHDATA), matchdata)
        frameptr = unsafe_load(Ptr{Ptr{Frame}}(frameptr + FRAME_NEXT))
    end
    return nothing
end

# A match that is not one run of QUICK_STEPS: of a longer text, or one that used them (`rc` is then how that run
# ended, and otherwise 0).
@noinline function slowmatch(p::Pattern, subject::Ptr{UInt8}, len::Int, limit::UInt64, rc::Cint)::Bool
    rc == Cint(0) || rc == PCRE2_ERROR_MATCHLIMIT || rc == PCRE2_ERROR_JIT_STACKLIMIT || throw(matcherror(p, rc, limit))
    limit == NO_MATCH_LIMIT && return untimedmatch(p, subject, len, rc)
    # JIT code that ran out of stack is not the last word. The interpreter keeps what it backtracks over on the
    # heap, where it has more room, and its match is timed.
    interpreted = rc == PCRE2_ERROR_JIT_STACKLIMIT
    if !interpreted
        steps = freesteps(p.fixedcost, p.textcost, p.anchored, p.jit, len, limit)
        # A pattern that is tried from one place has a first stage of any number of steps, as few of them do no
        # more work than many.
        if steps >= (p.anchored ? 1 : FEWEST_FREE_STEPS)
            if rc == Cint(0) || steps > QUICK_STEPS
                rc = plainmatch(p, subject, len, steps % UInt32, freeitems(len, limit), UInt32(0))
                rc >= 0 && return true
                rc == PCRE2_ERROR_NOMATCH && return false
                rc == PCRE2_ERROR_MATCHLIMIT || rc == PCRE2_ERROR_JIT_STACKLIMIT || throw(matcherror(p, rc, limit))
                interpreted = rc == PCRE2_ERROR_JIT_STACKLIMIT
            end
        elseif !p.anchored
            return searchinparts(p, subject, len, limit)
        end
    end
    return timedmatch(p, subject, len, Csize_t(0), time_ns() + limit, limit, interpreted)
end

# A match with the limit on time turned off. It has the most steps PCRE2 can be told to allow, and a match that
# takes them is an error.
function untimedmatch(p::Pattern, subject::Ptr{UInt8}, len::Int, rc::Cint)::Bool
    if rc != PCRE2_ERROR_JIT_STACKLIMIT
        rc = plainmatch(p, subject, len, MOST_STEPS, typemax(Csize_t), UInt32(0))
        rc >= 0 && return true
        rc == PCRE2_ERROR_NOMATCH && return false
    end
    if rc == PCRE2_ERROR_JIT_STACKLIMIT
        rc = plainmatch(p, subject, len, MOST_STEPS, typemax(Csize_t), PCRE2_NO_JIT)
        rc >= 0 && return true
        rc == PCRE2_ERROR_NOMATCH && return false
    end
    throw(matcherror(p, rc, NO_MATCH_LIMIT))
end

# The search of a long text for a pattern that is not anchored.
#
# PCRE2 tries the pattern from each place of the text, with the steps of the match for each, so the first stage of
# such a search would be given fewer than FEWEST_FREE_STEPS for a place. It is run in parts. A part tries the
# pattern from so many places, with FEWEST_FREE_STEPS for each, that it does the work of a first stage at most (it
# is a call of pcre2_match that is told the last place it may start from, and it sees the whole text, behind and
# ahead). The clock is read between two parts, and the search is given up at the limit. A part in which some place
# uses its steps is where the timed match starts from.
#
# A part starts at a character. The byte after the last place of the part before is stepped back from, while it is
# one that continues a character, so no place PCRE2 would try is passed over. Where that comes back to the start of
# the part before (a part of fewer bytes than a character has) it is stepped forwards from instead, to the next
# character.
function searchinparts(p::Pattern, subject::Ptr{UInt8}, len::Int, limit::UInt64)::Bool
    deadline = time_ns() + limit
    cost = costsum(p.fixedcost, costtimes(p.textcost, len))
    places = freework(p.jit, limit) ÷ FEWEST_FREE_STEPS ÷ cost
    places >= 1 || return timedmatch(p, subject, len, Csize_t(0), deadline, limit, false)
    from = 0
    while true
        last = min(from + places - 1, len)
        rc = partmatch(p, subject, len, from, last, limit)
        rc >= 0 && return true
        if rc != PCRE2_ERROR_NOMATCH
            rc == PCRE2_ERROR_MATCHLIMIT || rc == PCRE2_ERROR_JIT_STACKLIMIT || throw(matcherror(p, rc, limit))
            return timedmatch(p, subject, len, Csize_t(from), deadline, limit, rc == PCRE2_ERROR_JIT_STACKLIMIT)
        end
        last >= len && return false
        next = last + 1
        while next > from && next < len && unsafe_load(subject, next + 1) & 0xC0 == 0x80
            next -= 1
        end
        if next == from
            next = last + 1
            while next < len && unsafe_load(subject, next + 1) & 0xC0 == 0x80
                next += 1
            end
        end
        from = next
        time_ns() >= deadline && throw(matcherror(p, ERROR_TIME_LIMIT, limit))
    end
end

# One part of a search: the pattern tried from the places `from` to `last`.
function partmatch(p::Pattern, subject::Ptr{UInt8}, len::Int, from::Int, last::Int, limit::UInt64)::Cint
    frameptr = threadframe()
    if !isempty(p.lookbehinds)
        frameptr = calloutframe()
        setframe!(frameptr, pointer_from_objref(p), Csize_t(0), UInt32(0))
        settiming!(frameptr, UInt64(0), freeitems(len, limit))
    end
    frame = unsafe_load(frameptr)
    setsteps!(frame, UInt32(FEWEST_FREE_STEPS))
    setlaststart!(frame, Csize_t(last))
    rc = pcrematch(p.code, subject, Csize_t(len), UInt32(0), frame, Csize_t(from))
    setlaststart!(frame, PCRE2_UNSET)
    setsteps!(frame, QUICK_STEPS)
    return rc
end

# The second stage of a match: the code with automatic callouts, tried from the place `from` on, until the clock
# reads `deadline`. JIT code runs it, and the interpreter when the stack of JIT code is too small for the match.
function timedmatch(p::Pattern, subject::Ptr{UInt8}, len::Int, from::Csize_t, deadline::UInt64, limit::UInt64,
        interpreted::Bool)::Bool
    # Compiling takes a lock, which a task may wait on and be resumed on another thread. The frame is read after.
    code = timedcode(p)
    rc = timedrun(p, code, subject, len, from, deadline, interpreted ? PCRE2_NO_JIT : UInt32(0))
    if rc == PCRE2_ERROR_JIT_STACKLIMIT && !interpreted
        rc = timedrun(p, code, subject, len, from, deadline, PCRE2_NO_JIT)
    end
    rc >= 0 && return true
    rc == PCRE2_ERROR_NOMATCH && return false
    throw(matcherror(p, rc, limit))
end

function timedrun(p::Pattern, code::Ptr{Cvoid}, subject::Ptr{UInt8}, len::Int, from::Csize_t, deadline::UInt64,
        options::UInt32)::Cint
    frameptr = calloutframe()
    setframe!(frameptr, pointer_from_objref(p), Csize_t(0), options)
    settiming!(frameptr, deadline, typemax(Csize_t))
    frame = unsafe_load(frameptr)
    setsteps!(frame, MOST_STEPS)
    rc = pcrematch(code, subject, Csize_t(len), options, frame, from)
    setsteps!(frame, QUICK_STEPS)
    settiming!(frameptr, UInt64(0), typemax(Csize_t))
    options == PCRE2_NO_JIT && len > DATA_KEPT_LENGTH && freshdata!(frameptr)
    return rc
end
