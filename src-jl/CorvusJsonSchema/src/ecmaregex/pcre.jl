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
const PCRE2_JIT_COMPLETE = 0x00000001
const PCRE2_ERROR_NOMATCH = Cint(-1)
const PCRE2_ERROR_JIT_STACKLIMIT = Cint(-46)
const PCRE2_ERROR_MATCHLIMIT = Cint(-47)
const PCRE2_ERROR_NOMEMORY = Cint(-48)
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
const COMPILE_OPTIONS = PCRE2_UTF | PCRE2_MATCH_INVALID_UTF | PCRE2_NO_START_OPTIMIZE
# A pattern with a callout is also compiled with this one. PCRE2 makes a repetition possessive when what follows it
# cannot match what it repeats, and it looks past a callout to see what follows. A callout of this module can fail
# where the pattern after it would match, and the repetition must then give back what it took.
const CALLOUT_COMPILE_OPTIONS = PCRE2_NO_AUTO_POSSESS
# The body of a lookbehind that is a callout is also compiled with this one, which has PCRE2 call out before every
# item of the pattern. PCRE2 limits the steps of one call of pcre2_match, and a lookbehind that is a callout is
# searched for by a call of its own each time the match reaches it, so the steps of a match would have no limit.
# The callouts count the items that the searches of one match run, and the match is an error past STEP_LIMIT.
const LOOKBEHIND_COMPILE_OPTIONS = PCRE2_AUTO_CALLOUT
# The number PCRE2 gives the callouts it adds.
const CALLOUT_AUTOMATIC = 255
# The most items the searches for the lookbehinds of one match may run. It is the number of steps PCRE2 allows one
# call of pcre2_match.
const STEP_LIMIT = 10_000_000

# Where the stack of the code the JIT compiler writes starts, and how far it grows. They are the sizes Julia gives
# its own.
const JIT_STACK_START = 32768
const JIT_STACK_MAX = 1048576
# The most memory the interpreter may take for one match, in kibibytes. It runs a match only when the JIT compiler
# did not take the pattern or its stack was too small for the match.
const HEAP_LIMIT_KIB = 65536

# Thrown when a match was given up. A pattern that backtracks without end on the text reaches the match limit of
# PCRE2, the limit of its memory, or the limit of this module on the search for lookbehinds (STEP_LIMIT). Whether
# the text matches is then not known, so it is never reported as no match.
struct MatchError <: Exception
    pattern::String
    code::Int
    message::String
end

Base.showerror(io::IO, e::MatchError) =
    print(io, "EcmaRegex.MatchError: matching the pattern ", repr(e.pattern), " failed: ", e.message, " (PCRE2 error ",
        e.code, ")")

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

# The version of the PCRE2 in use, and of its Unicode tables (which this module does not rely on).
pcreversion() = pcreconfig(PCRE2_CONFIG_VERSION)
pcreunicodeversion() = pcreconfig(PCRE2_CONFIG_UNICODE_VERSION)

# Whether the PCRE2 in use searches a class of many ranges by halves, which it does from 10.45 on. Before that it
# tries the ranges one after another.
function pcreclassesarefast()
    version = tryparse(VersionNumber, first(split(pcreversion(), ' ')))
    return version !== nothing && version >= v"10.45"
end

# Whether the case folding of the PCRE2 in use is of a Unicode version no later than that of the data, so that every
# pair of characters it holds equivalent is one the data holds too.
function pcrefoldingisknown()
    engine = tryparse(VersionNumber, pcreunicodeversion())
    data = tryparse(VersionNumber, UNICODE_VERSION)
    return engine !== nothing && data !== nothing && engine <= data
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
    startswith(pattern, LOOKBEHIND_PREFIX) && (options |= LOOKBEHIND_COMPILE_OPTIONS)
    return options
end

# Compiles one PCRE2 pattern, with the JIT compiler where PCRE2 has one, as Julia does by default. Returns the code,
# or the message of PCRE2 when it does not take the pattern.
function pcrecompile(pattern::String)::Union{Ptr{Cvoid},String}
    options = compileoptions(pattern)
    errorcode = Ref{Cint}(0)
    erroroffset = Ref{Csize_t}(0)
    compiling = compilelock()
    lock(compiling)
    try
        code = ccall((:pcre2_compile_8, PCRE_LIB), Ptr{Cvoid},
            (Ptr{UInt8}, Csize_t, UInt32, Ref{Cint}, Ref{Csize_t}, Ptr{Cvoid}),
            pattern, ncodeunits(pattern), options, errorcode, erroroffset, C_NULL)
        code == C_NULL && return pcremessage(errorcode[])
        # A pattern the JIT compiler does not take, or a PCRE2 built without it, is run by the interpreter. PCRE2
        # does not say the same thing in every version when it does not take one, so the result is not looked at.
        ccall((:pcre2_jit_compile_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), code, PCRE2_JIT_COMPLETE)
        return code
    finally
        unlock(compiling)
    end
end

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

    function Pattern(code::Ptr{Cvoid}, lookbehinds::Vector{Ptr{Cvoid}}, source::String, unicode::Bool,
            translation::Translation)
        return finalizer(freepattern, new(code, lookbehinds, source, unicode, translation))
    end
end

function freepattern(p::Pattern)
    p.code == C_NULL || pcrefree(p.code)
    p.code = C_NULL
    foreach(pcrefree, p.lookbehinds)
    empty!(p.lookbehinds)
    return nothing
end

Base.show(io::IO, p::Pattern) = print(io, "EcmaRegex.Pattern(", repr(p.source), ")")

# Compiles the patterns of a translation. Returns their codes, the one PCRE2 searches for first, or the message of
# PCRE2 when it does not take one of them.
function pcrecompile(t::Translation)::Union{Vector{Ptr{Cvoid}},String}
    codes = Ptr{Cvoid}[]
    for text in Iterators.flatten(((t.main,), t.lookbehinds))
        code = pcrecompile(text)
        if code isa String
            foreach(pcrefree, codes)
            return code
        end
        push!(codes, code)
    end
    return codes
end

# Compiles a translation. Returns the pattern, or the message of PCRE2 when it does not take some part of it.
function pcrecompile(t::Translation, source::String, unicode::Bool)::Union{Pattern,String}
    codes = pcrecompile(t)
    codes isa String && return codes
    return Pattern(codes[1], codes[2:end], source, unicode, t)
end

const RESTORE_LOCK = ReentrantLock()

# Compiles again a pattern that has lost its code.
@noinline function restore!(p::Pattern)
    lock(RESTORE_LOCK)
    try
        p.code == C_NULL || return nothing
        codes = pcrecompile(p.translation)
        codes isa String && throw(MatchError(p.source, 0, "PCRE2 does not compile the pattern again: " * codes))
        p.lookbehinds = codes[2:end]
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
    # The first frame of the thread, which counts the items the searches for the lookbehinds of a match have run.
    root::Ptr{Frame}
    steps::Csize_t
end

const FRAME_NEXT = fieldoffset(Frame, 3)
const FRAME_PATTERN = fieldoffset(Frame, 4)
const FRAME_TARGET = fieldoffset(Frame, 5)
const FRAME_OPTIONS = fieldoffset(Frame, 6)
const FRAME_ROOT = fieldoffset(Frame, 7)
const FRAME_STEPS = fieldoffset(Frame, 8)

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
# A lock a task may wait on: making the first frame of a thread evaluates the callout's definition.
const FRAME_LOCK = ReentrantLock()
# The callout as a C function, made when the first frame is.
const CALLOUT = Ref{Ptr{Cvoid}}(C_NULL)

# The pointers kept when the package was precompiled mean nothing in the process that loads it.
function __init__()
    @atomic FRAMES.first = Ptr{Frame}[]
    CALLOUT[] = C_NULL
    return nothing
end

@inline function pcrematch(code::Ptr{Cvoid}, subject::Ptr{UInt8}, len::Csize_t, options::UInt32, frame::Frame)
    return ccall((:pcre2_match_8, PCRE_LIB), Cint,
        (Ptr{Cvoid}, Ptr{UInt8}, Csize_t, Csize_t, UInt32, Ptr{Cvoid}, Ptr{Cvoid}),
        code, subject, len, 0, options, frame.matchdata, frame.context)
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
        counter = Ptr{Csize_t}(unsafe_load(Ptr{Ptr{Frame}}(frameptr + FRAME_ROOT)) + FRAME_STEPS)
        steps = unsafe_load(counter) + 1
        unsafe_store!(counter, steps)
        return steps > STEP_LIMIT ? PCRE2_ERROR_MATCHLIMIT : Cint(0)
    end
    b = unsafe_load(block)
    frame = unsafe_load(frameptr)
    if number == CALLOUT_AT_TARGET
        return b.current_position == frame.target ? Cint(0) : Cint(1)
    elseif number == CALLOUT_NOT_PAST_TARGET
        return b.current_position > frame.target ? PCRE2_ERROR_NOMATCH : Cint(0)
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
    ccall((:pcre2_set_heap_limit_8, PCRE_LIB), Cint, (Ptr{Cvoid}, UInt32), context, HEAP_LIMIT_KIB)
    ccall((:pcre2_set_callout_8, PCRE_LIB), Cint, (Ptr{Cvoid}, Ptr{Cvoid}, Ptr{Cvoid}), context, calloutfunction,
        frame)
    unsafe_store!(frame, Frame(matchdata, context, C_NULL, C_NULL, 0, 0, root == C_NULL ? frame : root, 0))
    return frame
end

# The first frame of the thread the task is running on.
@inline function threadframe()
    tid = Threads.threadid()
    frames = @atomic FRAMES.first
    if tid <= length(frames)
        frame = frames[tid]
        frame == C_NULL || return frame
    end
    return newthreadframe(tid)
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
            if CALLOUT[] == C_NULL
                CALLOUT[] = makecallout()
            end
            frame = newframe(CALLOUT[], Ptr{Frame}(C_NULL))
            frames[tid] = frame
        end
        return frame
    finally
        unlock(FRAME_LOCK)
    end
end

# PCRE2 does not take a null text, even an empty one, in every version.
const EMPTY_TEXT = UInt8[0]

# Whether the pattern matches anywhere in the bytes. The caller keeps the pattern and the bytes alive.
function unsafe_ismatch(p::Pattern, subject::Ptr{UInt8}, len::Int)::Bool
    if len == 0
        subject = pointer(EMPTY_TEXT)
    end
    p.code == C_NULL && restore!(p)
    frameptr = threadframe()
    isempty(p.lookbehinds) || setframe!(frameptr, pointer_from_objref(p), Csize_t(0), UInt32(0))
    rc = pcrematch(p.code, subject, Csize_t(len), UInt32(0), unsafe_load(frameptr))
    rc >= 0 && return true
    rc == PCRE2_ERROR_NOMATCH && return false
    return matchagain(p, subject, len, frameptr, rc)
end

# A match that ended in an error. JIT code that ran out of stack is not the last word. The interpreter keeps what
# it backtracks over on the heap, where it has more room. Any other error, and an error of the interpreter, is thrown.
@noinline function matchagain(p::Pattern, subject::Ptr{UInt8}, len::Int, frameptr::Ptr{Frame}, rc::Cint)::Bool
    if rc == PCRE2_ERROR_JIT_STACKLIMIT
        setframe!(frameptr, pointer_from_objref(p), Csize_t(0), PCRE2_NO_JIT)
        rc = pcrematch(p.code, subject, Csize_t(len), PCRE2_NO_JIT, unsafe_load(frameptr))
        rc >= 0 && return true
        rc == PCRE2_ERROR_NOMATCH && return false
    end
    throw(MatchError(p.source, rc, pcremessage(rc)))
end
