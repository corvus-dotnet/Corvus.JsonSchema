/* The monotonic clock in nanoseconds, for corvus_ocaml_benchmark.ml. The OCaml standard library has no monotonic
   clock, and the benchmark harness may have a C stub where the library may not. */

#include <time.h>
#include <caml/mlvalues.h>

intnat corvus_benchmark_now(value unit)
{
  struct timespec t;
  (void)unit;
  clock_gettime(CLOCK_MONOTONIC, &t);
  return (intnat)t.tv_sec * 1000000000 + (intnat)t.tv_nsec;
}

value corvus_benchmark_now_byte(value unit)
{
  return Val_long(corvus_benchmark_now(unit));
}
