# The precompile workload runs while a package image is written, on whatever machine installs the package, and an
# error in it fails the installation. Its timed matches must not depend on how fast that machine is: 0.1.1 failed to
# precompile under emulation, where a match it expected to answer reached the workload's limit of 10 ms.

@testset "the precompile workload does not depend on the speed of the machine" begin
    # A limit that every timed match of the workload reaches at its first reading of the clock.
    @test CorvusJsonSchema.precompile_workload(pattern_match_limit=1e-9) === nothing
    @test CorvusJsonSchema.precompile_workload() === nothing
    # The workload leaves the package as it found it.
    @test isvalid(compile_schema("""{"patternProperties": {"fo+|ba+r": {"type": "integer"}}}"""), """{"foo": 1}""")
end
