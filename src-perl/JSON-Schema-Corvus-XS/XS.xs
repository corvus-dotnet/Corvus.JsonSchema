/* JSON::Schema::Corvus::XS: the corvus-json-schema C library for Perl.
 *
 * Validators, documents and collectors are blessed scalar references holding the C library's handles, freed in
 * DESTROY. They are not cloned into new ithreads (CLONE_SKIP): there they are unblessed references to undef.
 *
 * Nothing here lets a Perl exception unwind through the C library. A custom format or a resolver written in Perl is
 * called under G_EVAL, and an exception it throws is kept (pending) and thrown once the library call that ran it has
 * returned and its resources are freed. Perl data is encoded to JSON text (encode_value) before the library is
 * called, so that a value that has no JSON form croaks before anything is allocated. */

#define PERL_NO_GET_CONTEXT
#include "EXTERN.h"
#include "perl.h"
#include "XSUB.h"

#include "corvus_json_schema.h"

#include <math.h>

#define ERROR_CLASS "JSON::Schema::Corvus::XS::Error"
#define VALIDATOR_CLASS "JSON::Schema::Corvus::XS::Validator"
#define DOCUMENT_CLASS "JSON::Schema::Corvus::XS::Document"
#define COLLECTOR_CLASS "JSON::Schema::Corvus::XS::ResultsCollector"

/* The deepest Perl structure encode_value writes, as JSON::PP's default max_depth. */
#define MAX_ENCODE_DEPTH 512

#define MY_CXT_KEY "JSON::Schema::Corvus::XS::_guts" XS_VERSION

typedef struct {
  /* An exception a Perl callback threw during the current library call, to throw when it returns. */
  SV *pending;
} my_cxt_t;

START_MY_CXT

/* The interpreter a callback belongs to, in a Perl with more than one (ithreads): the library calls back on the thread
 * that called it, and a callback met on another thread (which cannot happen while validators are not cloned into
 * new threads) answers without calling Perl. */
#ifdef MULTIPLICITY
#  define THIS_PERL aTHX
#  define ON_ANOTHER_THREAD(box) ((PerlInterpreter *)PERL_GET_THX != (box)->perl)
#else
#  define THIS_PERL NULL
#  define ON_ANOTHER_THREAD(box) 0
#endif

/* A Perl callback handed to the C library: a custom format or the resolver. */
typedef struct {
  PerlInterpreter *perl;
  SV *code;
} callback_box;

static void set_pending(pTHX_ SV *error) {
  dMY_CXT;
  if (!MY_CXT.pending) {
    MY_CXT.pending = newSVsv(error);
  }
}

static void clear_pending(pTHX) {
  dMY_CXT;
  if (MY_CXT.pending) {
    SvREFCNT_dec(MY_CXT.pending);
    MY_CXT.pending = NULL;
  }
}

/* Throws the exception a callback threw during the call just made, if any. */
static void throw_pending(pTHX) {
  dMY_CXT;
  SV *error = MY_CXT.pending;
  if (error) {
    MY_CXT.pending = NULL;
    croak_sv(sv_2mortal(error));
  }
}

/* The class of the exception for a status. */
static const char *error_class(cjs_status status) {
  switch (status) {
    case CJS_INVALID_UTF8:
    case CJS_INVALID_JSON:
      return ERROR_CLASS "::InvalidJson";
    case CJS_COMPILATION_FAILED:
      return ERROR_CLASS "::Compilation";
    case CJS_PATTERN_TOO_LARGE:
      return ERROR_CLASS "::PatternTooLarge";
    case CJS_DEPTH_EXCEEDED:
      return ERROR_CLASS "::Depth";
    case CJS_PATTERN_MATCH_LIMIT:
      return ERROR_CLASS "::PatternMatchLimit";
    default:
      return ERROR_CLASS;
  }
}

/* Throws the exception for a failed call: an object with the message, the status and, for invalid JSON or UTF-8, the
 * byte offset at which the text was found to be invalid. */
static void throw_status(pTHX_ cjs_status status) {
  cjs_str message = cjs_last_error_message();
  HV *error = newHV();
  (void)hv_stores(error, "message", newSVpvn_utf8(message.ptr, message.len, 1));
  (void)hv_stores(error, "status", newSVuv(status));
  if (status == CJS_INVALID_JSON || status == CJS_INVALID_UTF8) {
    (void)hv_stores(error, "offset", newSVuv(cjs_last_error_offset()));
  }
  croak_sv(sv_2mortal(sv_bless(newRV_noinc((SV *)error), gv_stashpv(error_class(status), GV_ADD))));
}

static void throw_usage(pTHX_ const char *message) {
  HV *error = newHV();
  (void)hv_stores(error, "message", newSVpv(message, 0));
  (void)hv_stores(error, "status", newSVuv(CJS_INVALID_ARGUMENT));
  croak_sv(sv_2mortal(sv_bless(newRV_noinc((SV *)error), gv_stashpv(ERROR_CLASS, GV_ADD))));
}

/* The bytes of a text argument: a string with the UTF8 flag gives its UTF-8, and one without gives its bytes, which
 * the library reads as UTF-8. */
static const char *text_of(pTHX_ SV *sv, STRLEN *len) {
  if (!SvOK(sv)) {
    throw_usage(aTHX_ "the text is undef");
  }
  return SvPV(sv, *len);
}

static void *handle_of(pTHX_ SV *sv, const char *class) {
  if (!sv_isobject(sv) || !sv_derived_from(sv, class)) {
    char message[200];
    snprintf(message, sizeof message, "expected a %s", class);
    throw_usage(aTHX_ message);
  }
  void *handle = INT2PTR(void *, SvIV(SvRV(sv)));
  if (!handle) {
    char message[200];
    snprintf(message, sizeof message, "the %s has been freed", class);
    throw_usage(aTHX_ message);
  }
  return handle;
}

static SV *wrap(pTHX_ void *handle, const char *class) {
  return sv_setref_pv(newSV(0), class, handle);
}

/* --- Encoding Perl data as JSON text, by JSON::PP's rules --- */

static void encode_value(pTHX_ SV *out, SV *value, int depth);

static void encode_string(pTHX_ SV *out, const char *p, STRLEN len) {
  static const char hex[] = "0123456789abcdef";
  STRLEN i, start = 0;
  sv_catpvs(out, "\"");
  for (i = 0; i < len; i++) {
    unsigned char c = (unsigned char)p[i];
    const char *escape = NULL;
    char unicode[7];
    if (c >= 0x20 && c != '"' && c != '\\') {
      continue;
    }
    switch (c) {
      case '"': escape = "\\\""; break;
      case '\\': escape = "\\\\"; break;
      case '\b': escape = "\\b"; break;
      case '\f': escape = "\\f"; break;
      case '\n': escape = "\\n"; break;
      case '\r': escape = "\\r"; break;
      case '\t': escape = "\\t"; break;
      default:
        unicode[0] = '\\'; unicode[1] = 'u'; unicode[2] = '0'; unicode[3] = '0';
        unicode[4] = hex[c >> 4]; unicode[5] = hex[c & 0xf]; unicode[6] = '\0';
        escape = unicode;
    }
    sv_catpvn(out, p + start, i - start);
    sv_catpv(out, escape);
    start = i + 1;
  }
  sv_catpvn(out, p + start, len - start);
  sv_catpvs(out, "\"");
}

/* A string value or a name, as UTF-8. */
static void encode_text_sv(pTHX_ SV *out, SV *sv) {
  STRLEN len;
  const char *p;
  SV *copy = sv_2mortal(newSVsv(sv));
  p = SvPVutf8(copy, len);
  encode_string(aTHX_ out, p, len);
}

static int compare_keys(const void *a, const void *b) {
  dTHX;
  SV *x = *(SV *const *)a;
  SV *y = *(SV *const *)b;
  STRLEN xl, yl;
  const char *xp = SvPVutf8(x, xl);
  const char *yp = SvPVutf8(y, yl);
  int c = memcmp(xp, yp, xl < yl ? xl : yl);
  return c ? c : (xl < yl ? -1 : xl > yl);
}

static void encode_hash(pTHX_ SV *out, HV *hv, int depth) {
  I32 count = hv_iterinit(hv);
  SV **keys;
  HE *entry;
  I32 i = 0;
  if (count == 0) {
    sv_catpvs(out, "{}");
    return;
  }
  /* Names in order of their UTF-8 bytes (JSON::PP's canonical order), so that the order of an evaluation's results
   * does not change with Perl's hash order. */
  Newx(keys, count, SV *);
  SAVEFREEPV(keys);
  while ((entry = hv_iternext(hv)) && i < count) {
    keys[i++] = sv_2mortal(newSVsv(hv_iterkeysv(entry)));
  }
  count = i;
  qsort(keys, count, sizeof(SV *), compare_keys);
  sv_catpvs(out, "{");
  for (i = 0; i < count; i++) {
    HE *he = hv_fetch_ent(hv, keys[i], 0, 0);
    if (i > 0) {
      sv_catpvs(out, ",");
    }
    encode_text_sv(aTHX_ out, keys[i]);
    sv_catpvs(out, ":");
    encode_value(aTHX_ out, he ? HeVAL(he) : &PL_sv_undef, depth + 1);
  }
  sv_catpvs(out, "}");
}

static void encode_array(pTHX_ SV *out, AV *av, int depth) {
  SSize_t i, last = av_len(av);
  sv_catpvs(out, "[");
  for (i = 0; i <= last; i++) {
    SV **item = av_fetch(av, i, 0);
    if (i > 0) {
      sv_catpvs(out, ",");
    }
    encode_value(aTHX_ out, item ? *item : &PL_sv_undef, depth + 1);
  }
  sv_catpvs(out, "]");
}

static void encode_number(pTHX_ SV *out, SV *value) {
  if (SvNOK(value) && !SvIOK(value)) {
    NV nv = SvNV(value);
    if (Perl_isinfnan(nv)) {
      croak("JSON::Schema::Corvus::XS: %s has no JSON form", Perl_isnan(nv) ? "NaN" : "an infinite number");
    }
  }
  /* As JSON::PP writes a number: Perl's own text for it. */
  {
    STRLEN len;
    SV *copy = sv_2mortal(newSVsv(value));
    const char *p = SvPV(copy, len);
    sv_catpvn(out, p, len);
  }
}

/* A Math::BigInt or Math::BigFloat, as the text its bstr method gives, which must be a JSON number. */
static void encode_bignum(pTHX_ SV *out, SV *value) {
  dSP;
  int count;
  STRLEN len, i = 0;
  const char *p;
  SV *text;
  bool digits = false;
  ENTER;
  SAVETMPS;
  PUSHMARK(SP);
  XPUSHs(value);
  PUTBACK;
  count = call_method("bstr", G_SCALAR);
  SPAGAIN;
  text = count == 1 ? newSVsv(POPs) : newSV(0);
  PUTBACK;
  FREETMPS;
  LEAVE;
  sv_2mortal(text);
  p = SvOK(text) ? SvPV(text, len) : (len = 0, "");
  /* -?digits(.digits)?([eE][+-]?digits)? */
  if (i < len && p[i] == '-') i++;
  while (i < len && isDIGIT(p[i])) { i++; digits = true; }
  if (digits && i < len && p[i] == '.') {
    bool fraction = false;
    i++;
    while (i < len && isDIGIT(p[i])) { i++; fraction = true; }
    digits = fraction;
  }
  if (digits && i < len && (p[i] == 'e' || p[i] == 'E')) {
    bool exponent = false;
    i++;
    if (i < len && (p[i] == '+' || p[i] == '-')) i++;
    while (i < len && isDIGIT(p[i])) { i++; exponent = true; }
    digits = exponent;
  }
  if (!digits || i != len) {
    croak("JSON::Schema::Corvus::XS: the %s %" SVf " has no JSON form", HvNAME(SvSTASH(SvRV(value))), SVfARG(text));
  }
  sv_catpvn(out, p, len);
}

static void encode_value(pTHX_ SV *out, SV *value, int depth) {
  U32 flags;
  if (depth > MAX_ENCODE_DEPTH) {
    croak("JSON::Schema::Corvus::XS: the data is nested more than %d deep (or holds a cycle)", MAX_ENCODE_DEPTH);
  }
  SvGETMAGIC(value);
  if (!SvOK(value)) {
    sv_catpvs(out, "null");
    return;
  }
#ifdef SvIsBOOL
  if (SvIsBOOL(value)) {
    if (SvTRUE_nomg(value)) sv_catpvs(out, "true"); else sv_catpvs(out, "false");
    return;
  }
#endif
  if (SvROK(value)) {
    SV *target = SvRV(value);
    if (SvOBJECT(target)) {
      HV *stash = SvSTASH(target);
      GV *to_json;
      if (sv_derived_from(value, "JSON::PP::Boolean")) {
        if (SvTRUE(value)) sv_catpvs(out, "true"); else sv_catpvs(out, "false");
        return;
      }
      /* Math::BigInt and Math::BigFloat are numbers, written exactly, as JSON::PP writes them with allow_bignum. */
      if (sv_derived_from(value, "Math::BigInt") || sv_derived_from(value, "Math::BigFloat")) {
        encode_bignum(aTHX_ out, value);
        return;
      }
      to_json = gv_fetchmethod_autoload(stash, "TO_JSON", 0);
      if (to_json) {
        dSP;
        int count;
        SV *result;
        ENTER;
        SAVETMPS;
        PUSHMARK(SP);
        XPUSHs(value);
        PUTBACK;
        count = call_sv(MUTABLE_SV(GvCV(to_json)), G_SCALAR);
        SPAGAIN;
        result = count == 1 ? newSVsv(POPs) : newSV(0);
        PUTBACK;
        FREETMPS;
        LEAVE;
        sv_2mortal(result);
        if (SvROK(result) && SvRV(result) == target) {
          croak("JSON::Schema::Corvus::XS: %s::TO_JSON returned the object itself", HvNAME(stash));
        }
        encode_value(aTHX_ out, result, depth + 1);
        return;
      }
      croak("JSON::Schema::Corvus::XS: an object of class %s has no JSON form (it has no TO_JSON method)",
        HvNAME(stash));
    }
    switch (SvTYPE(target)) {
      case SVt_PVHV:
        encode_hash(aTHX_ out, (HV *)target, depth);
        return;
      case SVt_PVAV:
        encode_array(aTHX_ out, (AV *)target, depth);
        return;
      default:
        /* \1 and \0 are true and false, as JSON::PP has them. */
        if (SvTYPE(target) < SVt_PVAV && !SvROK(target) && SvOK(target)) {
          STRLEN len;
          const char *p = SvPV(target, len);
          if (len == 1 && p[0] == '1') { sv_catpvs(out, "true"); return; }
          if (len == 1 && p[0] == '0') { sv_catpvs(out, "false"); return; }
        }
        croak("JSON::Schema::Corvus::XS: a reference that is not to a hash or an array has no JSON form "
          "(\\1 and \\0 are true and false)");
    }
  }
  /* As JSON::PP: a scalar with a number and no string is a number, and anything else is a string. */
  flags = SvFLAGS(value);
  if ((flags & (SVp_IOK | SVp_NOK)) && !(flags & SVp_POK)) {
    encode_number(aTHX_ out, value);
    return;
  }
  encode_text_sv(aTHX_ out, value);
}

/* Perl data as JSON text, a mortal SV of UTF-8 bytes. */
static SV *encode_data(pTHX_ SV *data) {
  SV *out = sv_2mortal(newSVpvs(""));
  encode_value(aTHX_ out, data, 0);
  return out;
}

/* --- Callbacks --- */

static callback_box *new_box(pTHX_ SV *code) {
  callback_box *box;
  Newx(box, 1, callback_box);
  box->perl = THIS_PERL;
  box->code = newSVsv(code);
  return box;
}

static void free_box(void *user_data) {
  callback_box *box = (callback_box *)user_data;
  if (box) {
    dTHXa(box->perl);
    SvREFCNT_dec(box->code);
    Safefree(box);
  }
}

static bool call_format(void *user_data, const char *value, size_t len) {
  callback_box *box = (callback_box *)user_data;
  bool result = false;
  int count;
  dTHXa(box->perl);
  if (ON_ANOTHER_THREAD(box)) {
    return false;
  }
  {
    dSP;
    ENTER;
    SAVETMPS;
    PUSHMARK(SP);
    XPUSHs(sv_2mortal(newSVpvn_utf8(value, len, 1)));
    PUTBACK;
    count = call_sv(box->code, G_SCALAR | G_EVAL);
    SPAGAIN;
    if (SvTRUE(ERRSV)) {
      set_pending(aTHX_ ERRSV);
      if (count > 0) (void)POPs;
    }
    else if (count == 1) {
      /* Popped first: before 5.32, SvTRUE evaluates its argument more than once. */
      SV *answer = POPs;
      result = SvTRUE(answer);
    }
    PUTBACK;
    FREETMPS;
    LEAVE;
  }
  return result;
}

static cjs_status call_resolver(void *user_data, const char *uri, size_t uri_len, cjs_resolved *out) {
  callback_box *box = (callback_box *)user_data;
  cjs_status status = CJS_OK;
  int count;
  dTHXa(box->perl);
  if (ON_ANOTHER_THREAD(box)) {
    static const char message[] = "the resolver was called on another thread";
    cjs_resolved_set_error(out, message, sizeof message - 1);
    return CJS_COMPILATION_FAILED;
  }
  {
    dSP;
    ENTER;
    SAVETMPS;
    PUSHMARK(SP);
    XPUSHs(sv_2mortal(newSVpvn_utf8(uri, uri_len, 1)));
    PUTBACK;
    count = call_sv(box->code, G_SCALAR | G_EVAL);
    SPAGAIN;
    if (SvTRUE(ERRSV)) {
      static const char message[] = "the resolver died";
      set_pending(aTHX_ ERRSV);
      if (count > 0) (void)POPs;
      cjs_resolved_set_error(out, message, sizeof message - 1);
      status = CJS_COMPILATION_FAILED;
    }
    else if (count == 1) {
      SV *document = POPs;
      PUTBACK;
      if (SvOK(document)) {
        STRLEN len;
        const char *text;
        if (SvROK(document) && !sv_isobject(document)) {
          /* Perl data, encoded under G_EVAL: data with no JSON form fails the compilation, and its message is
           * thrown when the compilation returns. */
          int encoded;
          SV *json;
          PUSHMARK(SP);
          XPUSHs(document);
          PUTBACK;
          encoded = call_pv("JSON::Schema::Corvus::XS::encode_json", G_SCALAR | G_EVAL);
          SPAGAIN;
          json = encoded == 1 ? POPs : &PL_sv_undef;
          PUTBACK;
          if (SvTRUE(ERRSV) || !SvOK(json)) {
            static const char message[] = "the resolver returned data that has no JSON form";
            set_pending(aTHX_ ERRSV);
            cjs_resolved_set_error(out, message, sizeof message - 1);
            status = CJS_COMPILATION_FAILED;
          }
          else {
            text = SvPV(json, len);
            status = cjs_resolved_set_json(out, text, len);
          }
        }
        else {
          text = SvPV(document, len);
          status = cjs_resolved_set_json(out, text, len);
        }
      }
      SPAGAIN;
    }
    PUTBACK;
    FREETMPS;
    LEAVE;
  }
  return status;
}

/* --- Options --- */

static SV *option(pTHX_ HV *options, const char *name) {
  SV **value = hv_fetch(options, name, strlen(name), 0);
  return value && SvOK(*value) ? *value : NULL;
}

/* Builds the C library's options from the hash the Perl layer has checked. Croaks (freeing them) on a bad value. */
static cjs_options *build_options(pTHX_ HV *hv) {
  cjs_options *options = cjs_options_new();
  cjs_status status = CJS_OK;
  SV *value;
  if (!options) {
    throw_status(aTHX_ CJS_PANIC);
  }
  if ((value = option(aTHX_ hv, "default_dialect")) && status == CJS_OK) {
    status = cjs_options_set_default_dialect(options, (cjs_dialect)SvUV(value));
  }
  if ((value = option(aTHX_ hv, "assert_format")) && status == CJS_OK) {
    status = cjs_options_set_assert_format(options, SvTRUE(value) ? CJS_TRUE : CJS_FALSE);
  }
  if ((value = option(aTHX_ hv, "assert_format_in_legacy_drafts")) && status == CJS_OK) {
    status = cjs_options_set_assert_format_in_legacy_drafts(options, SvTRUE(value));
  }
  if ((value = option(aTHX_ hv, "assert_content")) && status == CJS_OK) {
    status = cjs_options_set_assert_content(options, SvTRUE(value));
  }
  if ((value = option(aTHX_ hv, "base_uri")) && status == CJS_OK) {
    STRLEN len;
    const char *text = SvPVutf8(value, len);
    status = cjs_options_set_base_uri(options, text, len);
  }
  if ((value = option(aTHX_ hv, "entry_point")) && status == CJS_OK) {
    STRLEN len;
    const char *text = SvPVutf8(value, len);
    status = cjs_options_set_entry_point(options, text, len);
  }
  if ((value = option(aTHX_ hv, "max_depth")) && status == CJS_OK) {
    status = cjs_options_set_max_depth(options, (uint32_t)SvUV(value));
  }
  if ((value = option(aTHX_ hv, "pattern_match_limit_ms")) && status == CJS_OK) {
    status = cjs_options_set_pattern_match_limit_ms(options, (uint64_t)SvUV(value));
  }
  if ((value = option(aTHX_ hv, "formats")) && status == CJS_OK) {
    HV *formats = (HV *)SvRV(value);
    HE *entry;
    hv_iterinit(formats);
    while (status == CJS_OK && (entry = hv_iternext(formats))) {
      STRLEN len;
      SV *name = hv_iterkeysv(entry);
      const char *text = SvPVutf8(name, len);
      callback_box *box = new_box(aTHX_ HeVAL(entry));
      status = cjs_options_add_format(options, text, len, call_format, box, free_box);
      if (status != CJS_OK) {
        free_box(box);
      }
    }
  }
  if ((value = option(aTHX_ hv, "resolve_document")) && status == CJS_OK) {
    callback_box *box = new_box(aTHX_ value);
    status = cjs_options_set_resolver(options, call_resolver, box, free_box);
    if (status != CJS_OK) {
      free_box(box);
    }
  }
  if (status != CJS_OK) {
    cjs_options_free(options);
    throw_status(aTHX_ status);
  }
  return options;
}

static SV *compile(pTHX_ const char *text, STRLEN len, bool uri, HV *hv) {
  cjs_options *options = build_options(aTHX_ hv);
  cjs_validator *validator = NULL;
  cjs_status status;
  clear_pending(aTHX);
  status = uri ? cjs_compile_uri(text, len, options, &validator) : cjs_compile(text, len, options, &validator);
  cjs_options_free(options);
  if (status != CJS_OK) {
    cjs_validator_free(validator);
    throw_pending(aTHX);
    throw_status(aTHX_ status);
  }
  /* A format callback can run during compilation (a patternProperties pattern matched against declared names does
   * not, but stay safe): an exception it threw wins. */
  if (validator) {
    dMY_CXT;
    if (MY_CXT.pending) {
      cjs_validator_free(validator);
      throw_pending(aTHX);
    }
  }
  return wrap(aTHX_ validator, VALIDATOR_CLASS);
}

/* The result of a validation call: throws the pending exception or the status, or gives the verdict. */
static SV *verdict(pTHX_ cjs_status status, bool valid) {
  throw_pending(aTHX);
  if (status != CJS_OK) {
    throw_status(aTHX_ status);
  }
  return valid ? &PL_sv_yes : &PL_sv_no;
}

static SV *row_string(pTHX_ cjs_str s) {
  return newSVpvn_utf8(s.ptr ? s.ptr : "", s.len, 1);
}

MODULE = JSON::Schema::Corvus::XS  PACKAGE = JSON::Schema::Corvus::XS

PROTOTYPES: DISABLE

BOOT:
{
  MY_CXT_INIT;
  MY_CXT.pending = NULL;
}

void
CLONE(...)
  CODE:
    MY_CXT_CLONE;
    MY_CXT.pending = NULL;

unsigned int
library_version()
  CODE:
    RETVAL = cjs_version();
  OUTPUT:
    RETVAL

SV *
library_version_string()
  CODE:
    RETVAL = row_string(aTHX_ cjs_version_string());
  OUTPUT:
    RETVAL

SV *
_compile_text(SV *schema, HV *options)
  CODE:
    STRLEN len;
    const char *text = text_of(aTHX_ schema, &len);
    RETVAL = compile(aTHX_ text, len, false, options);
  OUTPUT:
    RETVAL

SV *
_compile_data(SV *schema, HV *options)
  CODE:
    STRLEN len;
    SV *json = encode_data(aTHX_ schema);
    const char *text = SvPV(json, len);
    RETVAL = compile(aTHX_ text, len, false, options);
  OUTPUT:
    RETVAL

SV *
_compile_uri(SV *uri, HV *options)
  CODE:
    STRLEN len;
    const char *text = SvPVutf8(uri, len);
    RETVAL = compile(aTHX_ text, len, true, options);
  OUTPUT:
    RETVAL

SV *
encode_json(SV *data)
  CODE:
    RETVAL = newSVsv(encode_data(aTHX_ data));
  OUTPUT:
    RETVAL

MODULE = JSON::Schema::Corvus::XS  PACKAGE = JSON::Schema::Corvus::XS::Validator

int
CLONE_SKIP(...)
  CODE:
    RETVAL = 1;
  OUTPUT:
    RETVAL

void
DESTROY(SV *self)
  CODE:
    if (SvROK(self)) {
      cjs_validator_free(INT2PTR(cjs_validator *, SvIV(SvRV(self))));
      sv_setiv(SvRV(self), 0);
    }

SV *
is_valid_json(SV *self, SV *json)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    STRLEN len;
    const char *text = text_of(aTHX_ json, &len);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_validate_json(validator, text, len, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

SV *
is_valid(SV *self, SV *data)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    STRLEN len;
    SV *json = encode_data(aTHX_ data);
    const char *text = SvPV(json, len);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_validate_json(validator, text, len, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

SV *
is_valid_document(SV *self, SV *document)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    cjs_document *doc = handle_of(aTHX_ document, DOCUMENT_CLASS);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_validate_document(validator, doc, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

SV *
evaluate_json(SV *self, SV *json, SV *collector)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    cjs_collector *results = handle_of(aTHX_ collector, COLLECTOR_CLASS);
    STRLEN len;
    const char *text = text_of(aTHX_ json, &len);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_evaluate_json(validator, text, len, results, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

SV *
evaluate(SV *self, SV *data, SV *collector)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    cjs_collector *results = handle_of(aTHX_ collector, COLLECTOR_CLASS);
    STRLEN len;
    SV *json = encode_data(aTHX_ data);
    const char *text = SvPV(json, len);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_evaluate_json(validator, text, len, results, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

SV *
evaluate_document(SV *self, SV *document, SV *collector)
  CODE:
    cjs_validator *validator = handle_of(aTHX_ self, VALIDATOR_CLASS);
    cjs_document *doc = handle_of(aTHX_ document, DOCUMENT_CLASS);
    cjs_collector *results = handle_of(aTHX_ collector, COLLECTOR_CLASS);
    bool valid = false;
    cjs_status status;
    clear_pending(aTHX);
    status = cjs_validator_evaluate_document(validator, doc, results, &valid);
    RETVAL = verdict(aTHX_ status, valid);
  OUTPUT:
    RETVAL

MODULE = JSON::Schema::Corvus::XS  PACKAGE = JSON::Schema::Corvus::XS::Document

int
CLONE_SKIP(...)
  CODE:
    RETVAL = 1;
  OUTPUT:
    RETVAL

SV *
parse(SV *class, SV *json)
  CODE:
    STRLEN len;
    const char *text = text_of(aTHX_ json, &len);
    cjs_document *document = NULL;
    cjs_status status = cjs_document_parse(text, len, &document);
    PERL_UNUSED_VAR(class);
    if (status != CJS_OK) {
      throw_status(aTHX_ status);
    }
    RETVAL = wrap(aTHX_ document, DOCUMENT_CLASS);
  OUTPUT:
    RETVAL

SV *
from_data(SV *class, SV *data)
  CODE:
    STRLEN len;
    SV *json = encode_data(aTHX_ data);
    const char *text = SvPV(json, len);
    cjs_document *document = NULL;
    cjs_status status = cjs_document_parse(text, len, &document);
    PERL_UNUSED_VAR(class);
    if (status != CJS_OK) {
      throw_status(aTHX_ status);
    }
    RETVAL = wrap(aTHX_ document, DOCUMENT_CLASS);
  OUTPUT:
    RETVAL

void
DESTROY(SV *self)
  CODE:
    if (SvROK(self)) {
      cjs_document_free(INT2PTR(cjs_document *, SvIV(SvRV(self))));
      sv_setiv(SvRV(self), 0);
    }

MODULE = JSON::Schema::Corvus::XS  PACKAGE = JSON::Schema::Corvus::XS::ResultsCollector

int
CLONE_SKIP(...)
  CODE:
    RETVAL = 1;
  OUTPUT:
    RETVAL

SV *
_new(SV *class, unsigned int level)
  CODE:
    cjs_collector *collector = cjs_collector_new((cjs_results_level)level);
    PERL_UNUSED_VAR(class);
    if (!collector) {
      throw_usage(aTHX_ "the level is not basic, detailed or verbose");
    }
    RETVAL = wrap(aTHX_ collector, COLLECTOR_CLASS);
  OUTPUT:
    RETVAL

void
DESTROY(SV *self)
  CODE:
    if (SvROK(self)) {
      cjs_collector_free(INT2PTR(cjs_collector *, SvIV(SvRV(self))));
      sv_setiv(SvRV(self), 0);
    }

void
clear(SV *self)
  CODE:
    cjs_collector_clear(handle_of(aTHX_ self, COLLECTOR_CLASS));

UV
count(SV *self)
  CODE:
    RETVAL = cjs_collector_count(handle_of(aTHX_ self, COLLECTOR_CLASS));
  OUTPUT:
    RETVAL

SV *
results(SV *self)
  CODE:
    cjs_collector *collector = handle_of(aTHX_ self, COLLECTOR_CLASS);
    size_t i, count = cjs_collector_count(collector);
    AV *rows = newAV();
    av_extend(rows, count);
    for (i = 0; i < count; i++) {
      HV *row = newHV();
      (void)hv_stores(row, "is_match", cjs_collector_is_match(collector, i) ? &PL_sv_yes : &PL_sv_no);
      (void)hv_stores(row, "message", row_string(aTHX_ cjs_collector_message(collector, i)));
      (void)hv_stores(row, "evaluation_location", row_string(aTHX_ cjs_collector_evaluation_location(collector, i)));
      (void)hv_stores(row, "schema_evaluation_location",
        row_string(aTHX_ cjs_collector_schema_location(collector, i)));
      (void)hv_stores(row, "document_evaluation_location",
        row_string(aTHX_ cjs_collector_instance_location(collector, i)));
      av_push(rows, newRV_noinc((SV *)row));
    }
    RETVAL = newRV_noinc((SV *)rows);
  OUTPUT:
    RETVAL

SV *
annotations_json(SV *self)
  CODE:
    cjs_collector *collector = handle_of(aTHX_ self, COLLECTOR_CLASS);
    cjs_str json = { NULL, 0 };
    cjs_status status = cjs_collector_annotations_json(collector, &json);
    if (status != CJS_OK) {
      throw_status(aTHX_ status);
    }
    RETVAL = row_string(aTHX_ json);
  OUTPUT:
    RETVAL
