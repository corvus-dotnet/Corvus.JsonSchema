//! The part of R's C API the package uses, declared by hand (the package has no build-time dependency beyond the
//! corvus-json-schema crate). Every function here is in R's documented API ("Writing R Extensions").
//!
//! R's global values (`R_NilValue` and the like) are data exported by R's library; `init.c` passes them to
//! [`crate::cjsr_init`] once, when the package loads, so that nothing here imports data from a shared library.

#![allow(non_snake_case, non_camel_case_types, clippy::upper_case_acronyms)]

use std::os::raw::{c_char, c_double, c_int, c_uint, c_void};
use std::sync::atomic::{AtomicPtr, Ordering};

/// An R object. The package never looks inside one.
#[repr(C)]
pub struct SEXPREC {
    _private: [u8; 0],
}

pub type SEXP = *mut SEXPREC;
pub type R_xlen_t = isize;

pub const NILSXP: c_int = 0;
pub const LGLSXP: c_int = 10;
pub const INTSXP: c_int = 13;
pub const REALSXP: c_int = 14;
pub const STRSXP: c_int = 16;
pub const VECSXP: c_int = 19;

/// `cetype_t`: a string in the session's native encoding, or marked as UTF-8.
pub const CE_NATIVE: c_int = 0;
pub const CE_UTF8: c_int = 1;

/// `NA_integer_` and `NA` in a logical vector.
pub const NA_INTEGER: c_int = c_int::MIN;

unsafe extern "C" {
    pub fn TYPEOF(x: SEXP) -> c_int;
    pub fn Rf_xlength(x: SEXP) -> R_xlen_t;
    pub fn Rf_isObject(x: SEXP) -> c_int;
    pub fn Rf_inherits(x: SEXP, class: *const c_char) -> c_int;
    pub fn Rf_getAttrib(x: SEXP, name: SEXP) -> SEXP;
    pub fn Rf_setAttrib(x: SEXP, name: SEXP, value: SEXP) -> SEXP;

    pub fn LOGICAL_ELT(x: SEXP, i: R_xlen_t) -> c_int;
    pub fn INTEGER_ELT(x: SEXP, i: R_xlen_t) -> c_int;
    pub fn REAL_ELT(x: SEXP, i: R_xlen_t) -> c_double;
    pub fn STRING_ELT(x: SEXP, i: R_xlen_t) -> SEXP;
    pub fn VECTOR_ELT(x: SEXP, i: R_xlen_t) -> SEXP;
    pub fn SET_LOGICAL_ELT(x: SEXP, i: R_xlen_t, v: c_int);
    pub fn SET_STRING_ELT(x: SEXP, i: R_xlen_t, v: SEXP);
    pub fn SET_VECTOR_ELT(x: SEXP, i: R_xlen_t, v: SEXP) -> SEXP;

    pub fn R_CHAR(x: SEXP) -> *const c_char;
    pub fn Rf_getCharCE(x: SEXP) -> c_int;
    pub fn Rf_translateCharUTF8(x: SEXP) -> *const c_char;
    pub fn Rf_mkCharLenCE(s: *const c_char, len: c_int, encoding: c_int) -> SEXP;
    pub fn R_IsNA(x: c_double) -> c_int;

    pub fn Rf_allocVector(kind: c_uint, len: R_xlen_t) -> SEXP;
    pub fn Rf_ScalarLogical(x: c_int) -> SEXP;
    pub fn Rf_ScalarInteger(x: c_int) -> SEXP;
    pub fn Rf_ScalarReal(x: c_double) -> SEXP;
    pub fn Rf_protect(x: SEXP) -> SEXP;
    pub fn Rf_unprotect(n: c_int);

    pub fn Rf_lang2(f: SEXP, arg: SEXP) -> SEXP;
    pub fn Rf_eval(call: SEXP, env: SEXP) -> SEXP;

    pub fn R_MakeExternalPtr(p: *mut c_void, tag: SEXP, prot: SEXP) -> SEXP;
    pub fn R_ExternalPtrAddr(x: SEXP) -> *mut c_void;
    pub fn R_ExternalPtrTag(x: SEXP) -> SEXP;
    pub fn R_ClearExternalPtr(x: SEXP);
    pub fn R_RegisterCFinalizerEx(x: SEXP, finalizer: unsafe extern "C" fn(SEXP), on_exit: c_int);
}

static NIL: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());
static NAMES_SYMBOL: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());
static CLASS_SYMBOL: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());
static NA_STRING: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());
static GLOBAL_ENV: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());
static VALIDATOR_TAG: AtomicPtr<SEXPREC> = AtomicPtr::new(std::ptr::null_mut());

/// Keeps R's global values (see the module's documentation).
pub fn set_globals(
    nil: SEXP,
    names_symbol: SEXP,
    class_symbol: SEXP,
    na_string: SEXP,
    global_env: SEXP,
    validator_tag: SEXP,
) {
    VALIDATOR_TAG.store(validator_tag, Ordering::Relaxed);
    NIL.store(nil, Ordering::Relaxed);
    NAMES_SYMBOL.store(names_symbol, Ordering::Relaxed);
    CLASS_SYMBOL.store(class_symbol, Ordering::Relaxed);
    NA_STRING.store(na_string, Ordering::Relaxed);
    GLOBAL_ENV.store(global_env, Ordering::Relaxed);
}

#[inline(always)]
pub fn nil() -> SEXP {
    NIL.load(Ordering::Relaxed)
}

#[inline(always)]
pub fn names_symbol() -> SEXP {
    NAMES_SYMBOL.load(Ordering::Relaxed)
}

#[inline(always)]
pub fn class_symbol() -> SEXP {
    CLASS_SYMBOL.load(Ordering::Relaxed)
}

#[inline(always)]
pub fn na_string() -> SEXP {
    NA_STRING.load(Ordering::Relaxed)
}

#[inline(always)]
pub fn global_env() -> SEXP {
    GLOBAL_ENV.load(Ordering::Relaxed)
}

/// The symbol that tags the external pointers of this package's validators (a symbol is never collected).
#[inline(always)]
pub fn validator_tag() -> SEXP {
    VALIDATOR_TAG.load(Ordering::Relaxed)
}
