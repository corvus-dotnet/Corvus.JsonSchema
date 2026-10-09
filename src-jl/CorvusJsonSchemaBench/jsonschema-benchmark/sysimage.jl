# Builds a Julia system image that holds CorvusJsonSchema and the benchmark program:
#
#     julia --project=<an environment with PackageCompiler> sysimage.jl <the program's project> <the image to write>
#
# The program's precompile workload runs the protocol while the image is built, so the image holds its compiled code.
using PackageCompiler

project, image = ARGS
workload = joinpath(mktempdir(), "workload.jl")
write(workload, """
    using CorvusJsonSchemaBenchmark
    CorvusJsonSchemaBenchmark.precompile_workload()
    """)
create_sysimage(["CorvusJsonSchema", "CorvusJsonSchemaBenchmark"]; project=project, sysimage_path=image,
    precompile_execution_file=workload)
