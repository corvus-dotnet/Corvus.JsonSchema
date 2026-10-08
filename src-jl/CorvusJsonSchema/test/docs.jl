# Runs the code samples of the documentation, so that what the pages show is what the package does. The samples are
# the ```julia blocks of the package's README.md and of docs/JsonSchemaForJulia.md in the repository.
#
# A block runs in a module of its own, after `using CorvusJsonSchema`. Two things in a block are checked:
#
#   - a statement on one line with a comment after it, such as `isvalid(validator, text)  # true`: the value of the
#     statement, as `string` writes it, is the comment;
#   - comment lines straight after code that printed something: they are what it printed.
#
# Any other comment is prose. A block that throws fails the test.

# The ```julia blocks of a Markdown file, each with the line it starts at.
function julia_blocks(path::String)
    blocks = Tuple{Int,Vector{String}}[]
    current = nothing
    for (number, line) in enumerate(eachline(path))
        if current === nothing
            strip(line) == "```julia" && (current = (number + 1, String[]))
        elseif strip(line) == "```"
            push!(blocks, current)
            current = nothing
        else
            push!(current[2], line)
        end
    end
    current === nothing || error("$path: a code block is not closed")
    return blocks
end

# A line that is one whole statement followed by a comment gives the statement and the comment. Anything else gives
# nothing.
function statement_with_comment(line::String)
    (isempty(line) || isspace(line[1]) || startswith(line, "#")) && return nothing
    for at in findall('#', line)
        code = strip(line[1:prevind(line, at)])
        isempty(code) && return nothing
        expression = Meta.parse(code; raise=false)
        (expression isa Expr && expression.head in (:incomplete, :error)) && continue
        return expression, String(strip(line[nextind(line, at):end]))
    end
    return nothing
end

# Evaluates code in the module and gives what it printed.
function printed(m::Module, code::String, origin::String)
    path, io = mktemp()
    try
        redirect_stdout(io) do
            include_string(m, code, origin)
        end
        flush(io)
        close(io)
        return read(path, String)
    finally
        isopen(io) && close(io)
        rm(path; force=true)
    end
end

# Runs a block and gives the checks that failed, and how many checks it made.
function run_block(lines::Vector{String}, origin::String)
    m = Module()
    Core.eval(m, :(using CorvusJsonSchema))
    failures = String[]
    checks = 0
    pending = String[]
    # Runs the code gathered so far and gives what it printed.
    function run_pending()
        code = join(pending, "\n")
        empty!(pending)
        return isempty(strip(code)) ? "" : printed(m, code, origin)
    end
    unsaid(output) = output == "" || push!(failures, "$origin: printed $(repr(output)), and the page does not say so")
    i = 1
    while i <= length(lines)
        line = lines[i]
        if startswith(line, "#")
            output = run_pending()
            if output != ""
                # The comment lines after code that printed are what it printed.
                expected = String[]
                while i <= length(lines) && startswith(lines[i], "#")
                    push!(expected, String(strip(lines[i][2:end])))
                    i += 1
                end
                checks += 1
                got = [String(strip(l)) for l in split(chomp(output), '\n')]
                got == expected || push!(failures, "$origin: printed $got, and the page says $expected")
                continue
            end
        else
            check = statement_with_comment(line)
            if check === nothing
                push!(pending, line)
            else
                unsaid(run_pending())
                expression, expected = check
                value = Core.eval(m, expression)
                checks += 1
                string(value) == expected ||
                    push!(failures, "$origin: `$line` gave $(string(value)), and the page says $expected")
            end
        end
        i += 1
    end
    unsaid(run_pending())
    return failures, checks
end

@testset "documentation samples" begin
    pages = [joinpath(dirname(dirname(pathof(CorvusJsonSchema))), "README.md")]
    page = joinpath(REPOSITORY_ROOT, "docs", "JsonSchemaForJulia.md")
    if isfile(page)
        push!(pages, page)
    else
        @info "outside the Corvus.JsonSchema repository: docs/JsonSchemaForJulia.md is not there to run"
    end
    for path in pages
        blocks = julia_blocks(path)
        @test !isempty(blocks)
        checks = 0
        for (line, lines) in blocks
            failures, made = run_block(lines, "$(basename(path)):$line")
            foreach(println, failures)
            @test isempty(failures)
            checks += made
        end
        # Every page checks something in most of its blocks.
        @test checks >= length(blocks)
        println(basename(path), ": ", length(blocks), " samples, ", checks, " checks")
    end
end
