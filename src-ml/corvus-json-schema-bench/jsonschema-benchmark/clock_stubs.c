/* The monotonic clock in nanoseconds, for corvus_ocaml_benchmark.ml. The OCaml standard library has no monotonic
   clock, and the benchmark harness may have a C stub where the library may not.

   On Apple platforms CLOCK_MONOTONIC counts whole microseconds, so a pass shorter than one (the two instances of
   the workflow's check) measured 0. clock_gettime_nsec_np(CLOCK_UPTIME_RAW) counts nanoseconds, from the clock Go's
   runtime reads there. */

#include <time.h>
#include <caml/mlvalues.h>

intnat corvus_benchmark_now(value unit)
{
  (void)unit;
#if defined(__APPLE__)
  return (intnat)clock_gettime_nsec_np(CLOCK_UPTIME_RAW);
#else
  struct timespec t;
  clock_gettime(CLOCK_MONOTONIC, &t);
  return (intnat)t.tv_sec * 1000000000 + (intnat)t.tv_nsec;
#endif
}

value corvus_benchmark_now_byte(value unit)
{
  return Val_long(corvus_benchmark_now(unit));
}
