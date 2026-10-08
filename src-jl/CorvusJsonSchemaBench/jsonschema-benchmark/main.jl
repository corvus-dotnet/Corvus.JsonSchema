# The entry of the jsonschema-benchmark protocol program. The program is the CorvusJsonSchemaBenchmark package of
# this directory, which Julia compiles when the image is built.
using CorvusJsonSchemaBenchmark

exit(CorvusJsonSchemaBenchmark.main(ARGS))
