/* Registers the package's native routines, which are written in Rust (src/rust), and passes R's global values to
   them: they are data exported by R's library, which the Rust code does not import. */

#include <R.h>
#include <Rinternals.h>
#include <R_ext/Rdynload.h>

void cjsr_init(SEXP nil, SEXP names, SEXP class, SEXP na_string, SEXP global_env, SEXP validator_tag);
SEXP cjsr_crate_version(void);
SEXP cjsr_compile(SEXP schema, SEXP dialect, SEXP assert_format, SEXP assert_format_in_legacy_drafts,
                  SEXP assert_content, SEXP formats, SEXP resolver, SEXP base_uri, SEXP entry_point, SEXP max_depth);
SEXP cjsr_is_valid(SEXP validator, SEXP value);
SEXP cjsr_is_valid_json(SEXP validator, SEXP texts);
SEXP cjsr_evaluate(SEXP validator, SEXP value, SEXP level);
SEXP cjsr_evaluate_json(SEXP validator, SEXP text, SEXP level);

static const R_CallMethodDef call_methods[] = {
    {"cjsr_crate_version", (DL_FUNC) &cjsr_crate_version, 0},
    {"cjsr_compile", (DL_FUNC) &cjsr_compile, 10},
    {"cjsr_is_valid", (DL_FUNC) &cjsr_is_valid, 2},
    {"cjsr_is_valid_json", (DL_FUNC) &cjsr_is_valid_json, 2},
    {"cjsr_evaluate", (DL_FUNC) &cjsr_evaluate, 3},
    {"cjsr_evaluate_json", (DL_FUNC) &cjsr_evaluate_json, 3},
    {NULL, NULL, 0}
};

void R_init_corvusjsonschema(DllInfo *dll) {
    cjsr_init(R_NilValue, R_NamesSymbol, R_ClassSymbol, NA_STRING, R_GlobalEnv, Rf_install("corvus_json_schema"));
    R_registerRoutines(dll, NULL, call_methods, NULL, NULL);
    R_useDynamicSymbols(dll, FALSE);
    R_forceSymbols(dll, TRUE);
}
