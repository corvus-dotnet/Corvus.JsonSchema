# The entry of the Bowtie harness. The harness is the CorvusJsonSchemaBowtie package of this directory, which Julia
# compiles when the image is built.
using CorvusJsonSchemaBowtie

exit(CorvusJsonSchemaBowtie.main(stdin, stdout))
