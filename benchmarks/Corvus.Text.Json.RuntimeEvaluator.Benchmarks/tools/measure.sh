#!/bin/bash
# measure.sh: the four-axis comparison of the runtime evaluator against Blaze over the Sourcemeta corpora.
#
#   measure.sh publish            build the harness and publish the cold runner (ReadyToRun, native AOT, native AOT
#                                 with the generated models), write the program images and the Blaze templates
#   measure.sh warm <tag>         per corpus, each in its own process: the per-evaluation measurement (200 loops,
#                                 clock overhead subtracted, allocations) under JIT, R2R, AOT, generated JIT and
#                                 generated AOT, and a fresh Blaze run
#   measure.sh cold <tag>         per corpus: one fresh process end to end (median of 3) for Blaze (compile, and
#                                 from its template), the runtime evaluator (JIT, JIT from image, R2R, AOT, AOT from
#                                 image) and the generated models (JIT, AOT); Blaze's compile command; image loads
#   measure.sh table <tag>        assemble <out>/<tag>/summary.md and summary.csv (and corvus-vs-blaze.md) from the logs
#   measure.sh profile            collect an instrumented JIT trace of the warm run over every corpus and turn it into
#                                 <out>/corvus.mibc with dotnet-pgo (DOTNET_PGO=<path to dotnet-pgo.dll>, built from the
#                                 runtime repository's src/coreclr/tools/dotnet-pgo); then `publish` uses it for the
#                                 native AOT runner (the ColdMibc property), which recovers most of AOT's gap to the JIT
#
# Environment: JSONSCHEMA (the Blaze CLI binary, required), OUT (output root, default tools/out), CORES (pin set,
# default 0-11), CORPORA (space-separated subset, default all). Requires clang for the native AOT publish, pwsh is not
# needed. Run on a quiet, awake box: the harness's overhead column (13 to 15 ns on this machine) is the tell; the
# assembly step flags rows whose overhead is off.
set -u
HERE=$(cd "$(dirname "$0")" && pwd)
ROOT=$(cd "$HERE/../../.." && pwd)
B=$ROOT/benchmarks/Corvus.Text.Json.RuntimeEvaluator.Benchmarks
R=$ROOT/benchmarks/Corvus.Text.Json.RuntimeEvaluator.ColdRunner
H=$B/bin/Release/net10.0/Corvus.Text.Json.RuntimeEvaluator.Benchmarks.dll
C=$B/bin/Release/net10.0/sourcemeta
OUT=${OUT:-$HERE/out}
CORES=${CORES:-0-11}
RUNNERS=$OUT/runners
RUNNER=Corvus.Text.Json.RuntimeEvaluator.ColdRunner
export COLD_ROOT=$C
cmd=${1:-}; tag=${2:-}
ms() { python3 -c "import sys; print(f'{(float(sys.argv[2])-float(sys.argv[1]))*1000:.1f}')" "$@"; }
median3() { printf "%s\n" "$@" | sort -n | sed -n 2p; }
wall() { local t0=$EPOCHREALTIME; "$@" > /dev/null 2>&1; local t1=$EPOCHREALTIME; ms $t0 $t1; }
wall3() { local a=(); for i in 1 2 3; do a+=($(wall "$@")); done; median3 "${a[@]}"; }
corpora() {
  if [ -n "${CORPORA:-}" ]; then echo $CORPORA; else for f in $C/*-schema.json; do basename $f -schema.json; done; fi
}
case $cmd in
  profile)
    [ -n "${DOTNET_PGO:-}" ] || { echo "set DOTNET_PGO to the dotnet-pgo.dll built from the runtime repository"; exit 2; }
    mkdir -p $RUNNERS
    dotnet publish $R -c Release -r linux-x64 --self-contained false -m:4 -nr:false -p:UseSharedCompilation=false -o $RUNNERS/fd-jit || exit 1
    pkill -f "VBCSCompile[r]" 2>/dev/null
    DOTNET_TieredPGO=1 DOTNET_TC_QuickJitForLoops=1 DOTNET_TC_CallCountThreshold=10000 DOTNET_ReadyToRun=0 dotnet-trace collect --providers Microsoft-Windows-DotNETRuntime:0x1E000080018:5 -o $OUT/profile.nettrace -- dotnet $RUNNERS/fd-jit/$RUNNER.dll warm 50 $(corpora) || exit 1
    dotnet $DOTNET_PGO create-mibc --trace $OUT/profile.nettrace --output $OUT/corvus.mibc || exit 1
    echo "profile in $OUT/corvus.mibc"
    ;;
  publish)
    mkdir -p $RUNNERS
    MIBC=""; [ -f $OUT/corvus.mibc ] && MIBC="-p:ColdMibc=$OUT/corvus.mibc"
    dotnet build $B -c Release -f net10.0 -m:4 -nr:false -p:UseSharedCompilation=false || exit 1
    dotnet publish $R -c Release -r linux-x64 --self-contained -p:PublishReadyToRun=true -m:4 -nr:false -p:UseSharedCompilation=false -o $RUNNERS/r2r || exit 1
    dotnet publish $R -c Release -r linux-x64 -p:ColdAot=true $MIBC -m:4 -nr:false -p:UseSharedCompilation=false -o $RUNNERS/aot || exit 1
    dotnet publish $R -c Release -r linux-x64 -p:ColdAot=true -p:ColdGenerated=true $MIBC -m:4 -nr:false -p:UseSharedCompilation=false -o $RUNNERS/aot-gen || exit 1
    pkill -f "VBCSCompile[r]" 2>/dev/null
    dotnet $H cold prepare
    mkdir -p $OUT/blaze-templates
    for c in $(corpora); do $JSONSCHEMA compile $C/$c-schema.json --fast --minify > $OUT/blaze-templates/$c.json 2>/dev/null; done
    echo "published to $RUNNERS; images in $C/../cold-images; templates in $OUT/blaze-templates"
    ;;
  warm)
    D=$OUT/$tag; mkdir -p $D
    for k in jit r2r aot gen genaot; do : > $D/basis-$k.log; done
    first=1
    for c in $(corpora); do
      g=$([ $first = 1 ] && echo cat || echo "grep ^$c "); first=0
      taskset -c $CORES dotnet $H blazebasis 200 $c 2>/dev/null | $g >> $D/basis-jit.log
      taskset -c $CORES $RUNNERS/r2r/$RUNNER warm 200 $c 2>/dev/null | $g >> $D/basis-r2r.log
      taskset -c $CORES $RUNNERS/aot/$RUNNER warm 200 $c 2>/dev/null | $g >> $D/basis-aot.log
      taskset -c $CORES dotnet $H generated warm 200 $c 2>/dev/null | $g >> $D/basis-gen.log
      taskset -c $CORES $RUNNERS/aot-gen/$RUNNER generated warm 200 $c 2>/dev/null | $g >> $D/basis-genaot.log
    done
    python3 $HERE/blaze-compare.py --cli $JSONSCHEMA --corpus $C --quick $D/basis-jit.log --loop 200 --pin $CORES $(corpora) > $D/blaze-compare.log 2>&1
    echo "warm logs in $D"
    ;;
  cold)
    D=$OUT/$tag; mkdir -p $D; : > $D/cold.log
    base=$(wall3 taskset -c $CORES $JSONSCHEMA --version)
    for c in $(corpora); do
      s=$C/$c-schema.json; i=$C/$c-instances.jsonl
      bl=$(wall3 taskset -c $CORES $JSONSCHEMA validate $s $i --fast)
      bt=$(wall3 taskset -c $CORES $JSONSCHEMA validate $s $i --fast --template $OUT/blaze-templates/$c.json)
      bc=$(wall3 taskset -c $CORES $JSONSCHEMA compile $s --fast --minify)
      jc=$(wall3 taskset -c $CORES dotnet $H cold $c); ji=$(wall3 taskset -c $CORES dotnet $H cold $c image)
      rc=$(wall3 taskset -c $CORES $RUNNERS/r2r/$RUNNER $c)
      ac=$(wall3 taskset -c $CORES $RUNNERS/aot/$RUNNER $c); ai=$(wall3 taskset -c $CORES $RUNNERS/aot/$RUNNER $c image)
      gj=$(wall3 taskset -c $CORES dotnet $H generated cold $c); ga=$(wall3 taskset -c $CORES $RUNNERS/aot-gen/$RUNNER generated cold $c)
      jb=$(taskset -c $CORES dotnet $H cold $c 2>/dev/null); ab=$(taskset -c $CORES $RUNNERS/aot/$RUNNER $c 2>/dev/null)
      jl=$(taskset -c $CORES dotnet $H cold $c image 2>/dev/null | grep -o "load [0-9.]* ms" | awk '{print $2}')
      al=$(taskset -c $CORES $RUNNERS/aot/$RUNNER $c image 2>/dev/null | grep -o "load [0-9.]* ms" | awk '{print $2}')
      gjb=$(taskset -c $CORES dotnet $H generated cold $c 2>/dev/null); gab=$(taskset -c $CORES $RUNNERS/aot-gen/$RUNNER generated cold $c 2>/dev/null)
      echo "$c base $base blaze $bl blazetemplate $bt blazecompilecmd $bc jit $jc jitimage $ji r2r $rc aot $ac aotimage $ai genjit $gj genaot $ga jitload $jl aotload $al | $jb | $ab | $gjb | $gab" >> $D/cold.log
    done
    echo "cold log in $D"
    ;;
  table)
    python3 $HERE/assemble.py $OUT/$tag
    ;;
  *)
    sed -n 2,20p "$0"; exit 2;;
esac
