# The tests of the ECMA-262 regular expression module. The package's test/runtests.jl includes this file, and it
# also runs alone:
#
#     julia --startup-file=no -t 4 test/ecmaregex/runtests.jl
module EcmaRegexTests

using Test

# The module under test is the package's when the package is loaded, and the source file otherwise.
if isdefined(Main, :CorvusJsonSchema) && isdefined(Main.CorvusJsonSchema, :EcmaRegex)
    const EcmaRegex = Main.CorvusJsonSchema.EcmaRegex
else
    include(joinpath(@__DIR__, "..", "..", "src", "ecmaregex", "EcmaRegex.jl"))
end

include("json.jl")
include("support.jl")

@testset verbose = true "EcmaRegex" begin
    println("[ecmaregex] Julia ", VERSION, ", PCRE2 ", EcmaRegex.pcreversion(), " (its Unicode ",
        EcmaRegex.pcreunicodeversion(), "), data Unicode ", EcmaRegex.UNICODE_VERSION, ", ", Threads.nthreads(),
        " threads")
    include("oracle.jl")
    include("fuzz.jl")
    include("suite.jl")
    include("regextests.jl")
    include("validatortests.jl")
    include("unicodetests.jl")
    include("runtimetests.jl")
end

end
