!> JSON Schema validation (draft 4, 6, 7, 2019-09 and 2020-12) for Fortran: a binding to the corvus-json-schema C
!> library, through ISO_C_BINDING.
!>
!> A schema is compiled once into a validator_t, which validates JSON text or a parsed document_t any number of
!> times, from any number of threads. An evaluation into a collector_t gives the results (Basic, Detailed or Verbose)
!> and the annotations. Options (options_t) set the default dialect, format assertion, the base URI, an entry point,
!> the limits, custom formats and a resolver for referenced documents.
!>
!> Every handle frees what it holds when it is finalized. A validator_t may be assigned, which gives a new handle to
!> the same compiled schema; the other handles may not be copied.
!>
!> A procedure that can fail takes the optional arguments stat and errmsg, as the intrinsic statements do: stat is
!> CJS_OK or the status of the failure, and errmsg its message. When stat is absent, a failure stops the program
!> with the message (error stop).
module corvus_json_schema
  use, intrinsic :: iso_c_binding, only: c_bool, c_char, c_f_pointer, c_funloc, c_funptr, c_int32_t, c_int64_t, &
                                         c_loc, c_null_funptr, c_null_ptr, c_ptr, c_size_t, c_associated
  use, intrinsic :: iso_fortran_env, only: error_unit, int64, real64
  implicit none
  private

  public :: validator_t, options_t, document_t, collector_t
  public :: format_procedure, resolver_procedure
  public :: library_version, last_error_offset

  !> The version of this binding.
  character(*), parameter, public :: binding_version = "0.1.0"
  !> The oldest version of the C library this binding needs, as cjs_version() gives it: (major << 16) | (minor << 8)
  !> | patch.
  integer, parameter, public :: required_library_version = 0*65536 + 1*256 + 4

  ! The outcome of a call (cjs_status).
  integer, parameter, public :: CJS_OK = 0
  integer, parameter, public :: CJS_INVALID_ARGUMENT = 1
  integer, parameter, public :: CJS_INVALID_UTF8 = 2
  integer, parameter, public :: CJS_INVALID_JSON = 3
  integer, parameter, public :: CJS_COMPILATION_FAILED = 4
  integer, parameter, public :: CJS_DEPTH_EXCEEDED = 5
  integer, parameter, public :: CJS_PANIC = 6
  integer, parameter, public :: CJS_PATTERN_MATCH_LIMIT = 7
  integer, parameter, public :: CJS_PATTERN_TOO_LARGE = 8

  ! Dialects (cjs_dialect).
  integer, parameter, public :: DRAFT4 = 4
  integer, parameter, public :: DRAFT6 = 6
  integer, parameter, public :: DRAFT7 = 7
  integer, parameter, public :: DRAFT2019_09 = 2019
  integer, parameter, public :: DRAFT2020_12 = 2020

  ! What a collector records (cjs_results_level).
  !> Failures only, without message text (the lowest overhead).
  integer, parameter, public :: BASIC = 0
  !> Failures only, with message text.
  integer, parameter, public :: DETAILED = 1
  !> Every evaluation, passing and failing, with message text, and the annotations.
  integer, parameter, public :: VERBOSE = 2

  !> The time one match of a regular expression may take unless options_t%set_pattern_match_limit says otherwise, in
  !> seconds.
  real(real64), parameter, public :: default_pattern_match_limit = 1.0_real64

  integer(c_int32_t), parameter :: tristate_default = 0, tristate_true = 1, tristate_false = 2

  !> A view of a UTF-8 string the C library owns (cjs_str).
  type, bind(c) :: cjs_str
    type(c_ptr) :: ptr = c_null_ptr
    integer(c_size_t) :: len = 0
  end type cjs_str

  abstract interface
    !> A custom format assertion: whether the string (or, for a number, its JSON text) is valid. It runs during
    !> validation, on whichever thread validates.
    function format_procedure(value) result(valid)
      character(*), intent(in) :: value
      logical :: valid
    end function format_procedure

    !> A document resolver: for an absolute URI, found and the document's JSON text, or found = .false. for a document
    !> it does not know. It runs during compilation, on the compiling thread.
    subroutine resolver_procedure(uri, json, found)
      character(*), intent(in) :: uri
      character(:), allocatable, intent(out) :: json
      logical, intent(out) :: found
    end subroutine resolver_procedure
  end interface

  ! What a callback's user_data points to: the Fortran procedure, which a bind(c) trampoline calls.
  type :: format_box
    procedure(format_procedure), pointer, nopass :: call => null()
  end type format_box

  type :: resolver_box
    procedure(resolver_procedure), pointer, nopass :: call => null()
  end type resolver_box

  !> Compile options: a builder, created with its defaults (2020-12 for a schema without $schema, format as the
  !> vocabularies say, a pattern match limit of one second, and only the standard metaschemas). Options may be freed
  !> once the validators are compiled: they are unaffected.
  type :: options_t
    private
    type(c_ptr) :: handle = c_null_ptr
  contains
    procedure :: set_default_dialect => options_set_default_dialect
    procedure :: set_assert_format => options_set_assert_format
    procedure :: set_assert_format_in_legacy_drafts => options_set_assert_format_in_legacy_drafts
    procedure :: set_assert_content => options_set_assert_content
    procedure :: set_base_uri => options_set_base_uri
    procedure :: set_entry_point => options_set_entry_point
    procedure :: set_max_depth => options_set_max_depth
    procedure :: set_pattern_match_limit => options_set_pattern_match_limit
    procedure :: add_format => options_add_format
    procedure :: set_resolver => options_set_resolver
    procedure, private :: ensure => options_ensure
    generic :: assignment(=) => options_assign
    procedure, private :: options_assign
    final :: options_final
  end type options_t

  !> A compiled schema. Immutable once compiled: any number of threads may use one at once. Assigning one validator
  !> to another gives a new handle to the same compiled schema.
  type :: validator_t
    private
    type(c_ptr) :: handle = c_null_ptr
  contains
    procedure :: compile => validator_compile
    procedure :: compile_uri => validator_compile_uri
    procedure :: is_compiled => validator_is_compiled
    procedure :: is_valid => validator_is_valid
    procedure :: is_valid_document => validator_is_valid_document
    procedure :: evaluate => validator_evaluate
    procedure :: evaluate_document => validator_evaluate_document
    generic :: assignment(=) => validator_assign
    procedure, private :: validator_assign
    final :: validator_final
  end type validator_t

  !> Parsed JSON text, for an instance validated more than once. Immutable once parsed: it may be shared between
  !> threads.
  type :: document_t
    private
    type(c_ptr) :: handle = c_null_ptr
  contains
    procedure :: parse => document_parse
    procedure :: is_parsed => document_is_parsed
    generic :: assignment(=) => document_assign
    procedure, private :: document_assign
    final :: document_final
  end type document_t

  !> The results of an evaluation, at a level (BASIC, DETAILED or VERBOSE). Used by one thread at a time. Its rows are
  !> numbered from 1; at every level the root's own result is the last row.
  type :: collector_t
    private
    type(c_ptr) :: handle = c_null_ptr
    integer :: recorded = BASIC
  contains
    procedure :: init => collector_init
    procedure :: level => collector_level
    procedure :: clear => collector_clear
    procedure :: count => collector_count
    procedure :: is_match => collector_is_match
    procedure :: message => collector_message
    procedure :: evaluation_location => collector_evaluation_location
    procedure :: schema_location => collector_schema_location
    procedure :: instance_location => collector_instance_location
    procedure :: annotations_json => collector_annotations_json
    procedure, private :: ensure => collector_ensure
    generic :: assignment(=) => collector_assign
    procedure, private :: collector_assign
    final :: collector_final
  end type collector_t

  interface
    function cjs_last_error_message() bind(c, name="cjs_last_error_message") result(message)
      import :: cjs_str
      type(cjs_str) :: message
    end function cjs_last_error_message

    function cjs_last_error_offset() bind(c, name="cjs_last_error_offset") result(offset)
      import :: c_size_t
      integer(c_size_t) :: offset
    end function cjs_last_error_offset

    function cjs_version_string() bind(c, name="cjs_version_string") result(version)
      import :: cjs_str
      type(cjs_str) :: version
    end function cjs_version_string

    function cjs_resolved_set_json(out, json, len) bind(c, name="cjs_resolved_set_json") result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      type(c_ptr), value :: out
      character(kind=c_char), intent(in) :: json(*)
      integer(c_size_t), value :: len
      integer(c_int32_t) :: status
    end function cjs_resolved_set_json

    function cjs_options_new() bind(c, name="cjs_options_new") result(options)
      import :: c_ptr
      type(c_ptr) :: options
    end function cjs_options_new

    subroutine cjs_options_free(options) bind(c, name="cjs_options_free")
      import :: c_ptr
      type(c_ptr), value :: options
    end subroutine cjs_options_free

    function cjs_options_set_default_dialect(options, dialect) bind(c, name="cjs_options_set_default_dialect") &
        result(status)
      import :: c_ptr, c_int32_t
      type(c_ptr), value :: options
      integer(c_int32_t), value :: dialect
      integer(c_int32_t) :: status
    end function cjs_options_set_default_dialect

    function cjs_options_set_assert_format(options, mode) bind(c, name="cjs_options_set_assert_format") result(status)
      import :: c_ptr, c_int32_t
      type(c_ptr), value :: options
      integer(c_int32_t), value :: mode
      integer(c_int32_t) :: status
    end function cjs_options_set_assert_format

    function cjs_options_set_assert_format_in_legacy_drafts(options, enabled) &
        bind(c, name="cjs_options_set_assert_format_in_legacy_drafts") result(status)
      import :: c_ptr, c_bool, c_int32_t
      type(c_ptr), value :: options
      logical(c_bool), value :: enabled
      integer(c_int32_t) :: status
    end function cjs_options_set_assert_format_in_legacy_drafts

    function cjs_options_set_assert_content(options, enabled) bind(c, name="cjs_options_set_assert_content") &
        result(status)
      import :: c_ptr, c_bool, c_int32_t
      type(c_ptr), value :: options
      logical(c_bool), value :: enabled
      integer(c_int32_t) :: status
    end function cjs_options_set_assert_content

    function cjs_options_set_base_uri(options, uri, len) bind(c, name="cjs_options_set_base_uri") result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      type(c_ptr), value :: options
      character(kind=c_char), intent(in) :: uri(*)
      integer(c_size_t), value :: len
      integer(c_int32_t) :: status
    end function cjs_options_set_base_uri

    function cjs_options_set_entry_point(options, reference, len) bind(c, name="cjs_options_set_entry_point") &
        result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      type(c_ptr), value :: options
      character(kind=c_char), intent(in) :: reference(*)
      integer(c_size_t), value :: len
      integer(c_int32_t) :: status
    end function cjs_options_set_entry_point

    function cjs_options_set_max_depth(options, depth) bind(c, name="cjs_options_set_max_depth") result(status)
      import :: c_ptr, c_int32_t
      type(c_ptr), value :: options
      integer(c_int32_t), value :: depth
      integer(c_int32_t) :: status
    end function cjs_options_set_max_depth

    function cjs_options_set_pattern_match_limit_ms(options, milliseconds) &
        bind(c, name="cjs_options_set_pattern_match_limit_ms") result(status)
      import :: c_ptr, c_int64_t, c_int32_t
      type(c_ptr), value :: options
      integer(c_int64_t), value :: milliseconds
      integer(c_int32_t) :: status
    end function cjs_options_set_pattern_match_limit_ms

    function cjs_options_add_format(options, name, name_len, format, user_data, free_user_data) &
        bind(c, name="cjs_options_add_format") result(status)
      import :: c_ptr, c_char, c_size_t, c_funptr, c_int32_t
      type(c_ptr), value :: options
      character(kind=c_char), intent(in) :: name(*)
      integer(c_size_t), value :: name_len
      type(c_funptr), value :: format
      type(c_ptr), value :: user_data
      type(c_funptr), value :: free_user_data
      integer(c_int32_t) :: status
    end function cjs_options_add_format

    function cjs_options_set_resolver(options, resolve, user_data, free_user_data) &
        bind(c, name="cjs_options_set_resolver") result(status)
      import :: c_ptr, c_funptr, c_int32_t
      type(c_ptr), value :: options
      type(c_funptr), value :: resolve
      type(c_ptr), value :: user_data
      type(c_funptr), value :: free_user_data
      integer(c_int32_t) :: status
    end function cjs_options_set_resolver

    function cjs_compile(schema, len, options, out) bind(c, name="cjs_compile") result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      character(kind=c_char), intent(in) :: schema(*)
      integer(c_size_t), value :: len
      type(c_ptr), value :: options
      type(c_ptr), intent(out) :: out
      integer(c_int32_t) :: status
    end function cjs_compile

    function cjs_compile_uri(uri, len, options, out) bind(c, name="cjs_compile_uri") result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      character(kind=c_char), intent(in) :: uri(*)
      integer(c_size_t), value :: len
      type(c_ptr), value :: options
      type(c_ptr), intent(out) :: out
      integer(c_int32_t) :: status
    end function cjs_compile_uri

    function cjs_validator_clone(validator) bind(c, name="cjs_validator_clone") result(clone)
      import :: c_ptr
      type(c_ptr), value :: validator
      type(c_ptr) :: clone
    end function cjs_validator_clone

    subroutine cjs_validator_free(validator) bind(c, name="cjs_validator_free")
      import :: c_ptr
      type(c_ptr), value :: validator
    end subroutine cjs_validator_free

    function cjs_validator_validate_json(validator, json, len, valid) bind(c, name="cjs_validator_validate_json") &
        result(status)
      import :: c_ptr, c_char, c_size_t, c_bool, c_int32_t
      type(c_ptr), value :: validator
      character(kind=c_char), intent(in) :: json(*)
      integer(c_size_t), value :: len
      logical(c_bool), intent(out) :: valid
      integer(c_int32_t) :: status
    end function cjs_validator_validate_json

    function cjs_validator_validate_document(validator, document, valid) &
        bind(c, name="cjs_validator_validate_document") result(status)
      import :: c_ptr, c_bool, c_int32_t
      type(c_ptr), value :: validator
      type(c_ptr), value :: document
      logical(c_bool), intent(out) :: valid
      integer(c_int32_t) :: status
    end function cjs_validator_validate_document

    function cjs_validator_evaluate_json(validator, json, len, collector, valid) &
        bind(c, name="cjs_validator_evaluate_json") result(status)
      import :: c_ptr, c_char, c_size_t, c_bool, c_int32_t
      type(c_ptr), value :: validator
      character(kind=c_char), intent(in) :: json(*)
      integer(c_size_t), value :: len
      type(c_ptr), value :: collector
      logical(c_bool), intent(out) :: valid
      integer(c_int32_t) :: status
    end function cjs_validator_evaluate_json

    function cjs_validator_evaluate_document(validator, document, collector, valid) &
        bind(c, name="cjs_validator_evaluate_document") result(status)
      import :: c_ptr, c_bool, c_int32_t
      type(c_ptr), value :: validator
      type(c_ptr), value :: document
      type(c_ptr), value :: collector
      logical(c_bool), intent(out) :: valid
      integer(c_int32_t) :: status
    end function cjs_validator_evaluate_document

    function cjs_document_parse(json, len, out) bind(c, name="cjs_document_parse") result(status)
      import :: c_ptr, c_char, c_size_t, c_int32_t
      character(kind=c_char), intent(in) :: json(*)
      integer(c_size_t), value :: len
      type(c_ptr), intent(out) :: out
      integer(c_int32_t) :: status
    end function cjs_document_parse

    subroutine cjs_document_free(document) bind(c, name="cjs_document_free")
      import :: c_ptr
      type(c_ptr), value :: document
    end subroutine cjs_document_free

    function cjs_collector_new(level) bind(c, name="cjs_collector_new") result(collector)
      import :: c_ptr, c_int32_t
      integer(c_int32_t), value :: level
      type(c_ptr) :: collector
    end function cjs_collector_new

    subroutine cjs_collector_free(collector) bind(c, name="cjs_collector_free")
      import :: c_ptr
      type(c_ptr), value :: collector
    end subroutine cjs_collector_free

    subroutine cjs_collector_clear(collector) bind(c, name="cjs_collector_clear")
      import :: c_ptr
      type(c_ptr), value :: collector
    end subroutine cjs_collector_clear

    function cjs_collector_count(collector) bind(c, name="cjs_collector_count") result(count)
      import :: c_ptr, c_size_t
      type(c_ptr), value :: collector
      integer(c_size_t) :: count
    end function cjs_collector_count

    function cjs_collector_is_match(collector, i) bind(c, name="cjs_collector_is_match") result(is_match)
      import :: c_ptr, c_size_t, c_bool
      type(c_ptr), value :: collector
      integer(c_size_t), value :: i
      logical(c_bool) :: is_match
    end function cjs_collector_is_match

    function cjs_collector_message(collector, i) bind(c, name="cjs_collector_message") result(text)
      import :: c_ptr, c_size_t, cjs_str
      type(c_ptr), value :: collector
      integer(c_size_t), value :: i
      type(cjs_str) :: text
    end function cjs_collector_message

    function cjs_collector_evaluation_location(collector, i) bind(c, name="cjs_collector_evaluation_location") &
        result(text)
      import :: c_ptr, c_size_t, cjs_str
      type(c_ptr), value :: collector
      integer(c_size_t), value :: i
      type(cjs_str) :: text
    end function cjs_collector_evaluation_location

    function cjs_collector_schema_location(collector, i) bind(c, name="cjs_collector_schema_location") result(text)
      import :: c_ptr, c_size_t, cjs_str
      type(c_ptr), value :: collector
      integer(c_size_t), value :: i
      type(cjs_str) :: text
    end function cjs_collector_schema_location

    function cjs_collector_instance_location(collector, i) bind(c, name="cjs_collector_instance_location") &
        result(text)
      import :: c_ptr, c_size_t, cjs_str
      type(c_ptr), value :: collector
      integer(c_size_t), value :: i
      type(cjs_str) :: text
    end function cjs_collector_instance_location

    function cjs_collector_annotations_json(collector, out) bind(c, name="cjs_collector_annotations_json") &
        result(status)
      import :: c_ptr, cjs_str, c_int32_t
      type(c_ptr), value :: collector
      type(cjs_str), intent(out) :: out
      integer(c_int32_t) :: status
    end function cjs_collector_annotations_json
  end interface

contains

  !> The C library's version, such as "0.1.4".
  function library_version() result(version)
    character(:), allocatable :: version
    version = from_str(cjs_version_string())
  end function library_version

  !> The byte offset that goes with the most recent failure on this thread: where invalid UTF-8 or invalid JSON was
  !> found, counted from 0 (0 otherwise).
  function last_error_offset() result(offset)
    integer(int64) :: offset
    offset = int(cjs_last_error_offset(), int64)
  end function last_error_offset

  ! A copy of a string the C library owns.
  function from_str(text) result(value)
    type(cjs_str), intent(in) :: text
    character(:), allocatable :: value
    character(kind=c_char), pointer :: chars(:)
    integer :: i, n
    n = int(text%len)
    allocate (character(n) :: value)
    if (n == 0 .or. .not. c_associated(text%ptr)) return
    call c_f_pointer(text%ptr, chars, [n])
    do i = 1, n
      value(i:i) = chars(i)
    end do
  end function from_str

  ! A copy of a (pointer, length) string the C library passes to a callback.
  function from_chars(ptr, len) result(value)
    type(c_ptr), intent(in) :: ptr
    integer(c_size_t), intent(in) :: len
    character(:), allocatable :: value
    value = from_str(cjs_str(ptr, len))
  end function from_chars

  ! Reports a status through stat, and its message through message (empty for CJS_OK), or stops the program with the
  ! message when stat is absent. The caller copies message to its errmsg: an optional deferred-length character
  ! argument is never passed on to another procedure, which gfortran 11 does not do correctly.
  subroutine report(status, stat, message)
    integer(c_int32_t), intent(in) :: status
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out) :: message
    if (present(stat)) stat = int(status)
    message = ""
    if (status == CJS_OK) return
    message = from_str(cjs_last_error_message())
    if (len(message) == 0) message = status_name(int(status))
    if (.not. present(stat)) then
      write (error_unit, "(a)") "corvus_json_schema: " // message
      error stop 1
    end if
  end subroutine report

  ! A failure the binding finds itself (a handle that is not ready), reported as CJS_INVALID_ARGUMENT.
  subroutine report_misuse(what, stat, message)
    character(*), intent(in) :: what
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out) :: message
    if (present(stat)) stat = CJS_INVALID_ARGUMENT
    message = what
    if (.not. present(stat)) then
      write (error_unit, "(a)") "corvus_json_schema: " // what
      error stop 1
    end if
  end subroutine report_misuse

  function status_name(status) result(name)
    integer, intent(in) :: status
    character(:), allocatable :: name
    select case (status)
    case (CJS_INVALID_ARGUMENT); name = "invalid argument"
    case (CJS_INVALID_UTF8); name = "invalid UTF-8"
    case (CJS_INVALID_JSON); name = "invalid JSON"
    case (CJS_COMPILATION_FAILED); name = "compilation failed"
    case (CJS_DEPTH_EXCEEDED); name = "depth exceeded"
    case (CJS_PANIC); name = "internal error"
    case (CJS_PATTERN_MATCH_LIMIT); name = "pattern match limit"
    case (CJS_PATTERN_TOO_LARGE); name = "pattern too large"
    case default; name = "failure"
    end select
  end function status_name

  ! ---- options_t ----

  subroutine options_ensure(self)
    class(options_t), intent(inout) :: self
    if (.not. c_associated(self%handle)) self%handle = cjs_options_new()
  end subroutine options_ensure

  !> The dialect of a schema without $schema (DRAFT4 to DRAFT2020_12).
  subroutine options_set_default_dialect(self, dialect, stat, errmsg)
    class(options_t), intent(inout) :: self
    integer, intent(in) :: dialect
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    call report(cjs_options_set_default_dialect(self%handle, int(dialect, c_int32_t)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_default_dialect

  !> Whether format is asserted. Without enabled, format follows the vocabularies again (the 2020-12
  !> format-assertion vocabulary asserts, everything else annotates).
  subroutine options_set_assert_format(self, enabled, stat, errmsg)
    class(options_t), intent(inout) :: self
    logical, intent(in), optional :: enabled
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    integer(c_int32_t) :: mode
    mode = tristate_default
    if (present(enabled)) then
      mode = merge(tristate_true, tristate_false, enabled)
    end if
    call self%ensure()
    call report(cjs_options_set_assert_format(self%handle, mode), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_assert_format

  !> With format assertion left to the vocabularies, assert it in drafts 4 to 7 too.
  subroutine options_set_assert_format_in_legacy_drafts(self, enabled, stat, errmsg)
    class(options_t), intent(inout) :: self
    logical, intent(in) :: enabled
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    call report(cjs_options_set_assert_format_in_legacy_drafts(self%handle, logical(enabled, c_bool)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_assert_format_in_legacy_drafts

  !> Whether contentEncoding and contentMediaType are asserted in draft 7, the only draft that asserts them (by
  !> default they are).
  subroutine options_set_assert_content(self, enabled, stat, errmsg)
    class(options_t), intent(inout) :: self
    logical, intent(in) :: enabled
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    call report(cjs_options_set_assert_content(self%handle, logical(enabled, c_bool)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_assert_content

  !> The base URI of the root schema, when it has no $id (an empty string removes it).
  subroutine options_set_base_uri(self, uri, stat, errmsg)
    class(options_t), intent(inout) :: self
    character(*), intent(in) :: uri
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    call report(cjs_options_set_base_uri(self%handle, uri, len(uri, c_size_t)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_base_uri

  !> A reference to the subschema to validate against, such as "#/$defs/item" (an empty string: the root).
  subroutine options_set_entry_point(self, reference, stat, errmsg)
    class(options_t), intent(inout) :: self
    character(*), intent(in) :: reference
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    call report(cjs_options_set_entry_point(self%handle, reference, len(reference, c_size_t)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_entry_point

  !> The deepest the evaluator recurses in place on a cycle before it abandons the evaluation with
  !> CJS_DEPTH_EXCEEDED (128 by default).
  subroutine options_set_max_depth(self, depth, stat, errmsg)
    class(options_t), intent(inout) :: self
    integer, intent(in) :: depth
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    call self%ensure()
    if (depth < 0) then
      call report_misuse("the maximum depth is not a count", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    call report(cjs_options_set_max_depth(self%handle, int(depth, c_int32_t)), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_max_depth

  !> The seconds one match of a pattern or patternProperties regular expression against one string may take (one by
  !> default). A match that takes longer is abandoned, and the call it belongs to fails with CJS_PATTERN_MATCH_LIMIT:
  !> the instance is then neither valid nor not valid. Zero or less turns the limit off, for a caller who trusts both
  !> the schema and the instances (a match then has no limit on its memory either). The limit is counted in whole
  !> milliseconds, at least one.
  subroutine options_set_pattern_match_limit(self, seconds, stat, errmsg)
    class(options_t), intent(inout) :: self
    real(real64), intent(in) :: seconds
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    integer(c_int64_t) :: milliseconds
    if (seconds > 0.0_real64) then
      if (seconds >= 9.0e15_real64) then
        milliseconds = huge(milliseconds)
      else
        milliseconds = max(1_c_int64_t, int(ceiling(seconds * 1000.0_real64), c_int64_t))
      end if
    else
      milliseconds = 0
    end if
    call self%ensure()
    call report(cjs_options_set_pattern_match_limit_ms(self%handle, milliseconds), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_pattern_match_limit

  !> A custom format, taking precedence over the built-in one of that name.
  subroutine options_add_format(self, name, format, stat, errmsg)
    class(options_t), intent(inout) :: self
    character(*), intent(in) :: name
    procedure(format_procedure) :: format
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(format_box), pointer :: box
    integer(c_int32_t) :: status
    allocate (box)
    box%call => format
    call self%ensure()
    status = cjs_options_add_format(self%handle, name, len(name, c_size_t), c_funloc(format_trampoline), c_loc(box), &
                                    c_funloc(free_format_box))
    if (status /= CJS_OK) deallocate (box)
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_add_format

  !> The resolver for documents other than the standard metaschemas, replacing any earlier one.
  subroutine options_set_resolver(self, resolver, stat, errmsg)
    class(options_t), intent(inout) :: self
    procedure(resolver_procedure) :: resolver
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(resolver_box), pointer :: box
    integer(c_int32_t) :: status
    allocate (box)
    box%call => resolver
    call self%ensure()
    status = cjs_options_set_resolver(self%handle, c_funloc(resolver_trampoline), c_loc(box), &
                                      c_funloc(free_resolver_box))
    if (status /= CJS_OK) deallocate (box)
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine options_set_resolver

  subroutine options_assign(lhs, rhs)
    class(options_t), intent(inout) :: lhs
    type(options_t), intent(in) :: rhs
    if (c_associated(lhs%handle) .or. c_associated(rhs%handle)) then
      write (error_unit, "(a)") "corvus_json_schema: an options_t cannot be copied"
      error stop 1
    end if
  end subroutine options_assign

  subroutine options_final(self)
    type(options_t), intent(inout) :: self
    call cjs_options_free(self%handle)
    self%handle = c_null_ptr
  end subroutine options_final

  function format_trampoline(user_data, value, len) bind(c) result(valid)
    type(c_ptr), value :: user_data
    type(c_ptr), value :: value
    integer(c_size_t), value :: len
    logical(c_bool) :: valid
    type(format_box), pointer :: box
    call c_f_pointer(user_data, box)
    valid = logical(box%call(from_chars(value, len)), c_bool)
  end function format_trampoline

  subroutine free_format_box(user_data) bind(c)
    type(c_ptr), value :: user_data
    type(format_box), pointer :: box
    call c_f_pointer(user_data, box)
    deallocate (box)
  end subroutine free_format_box

  function resolver_trampoline(user_data, uri, uri_len, out) bind(c) result(status)
    type(c_ptr), value :: user_data
    type(c_ptr), value :: uri
    integer(c_size_t), value :: uri_len
    type(c_ptr), value :: out
    integer(c_int32_t) :: status
    type(resolver_box), pointer :: box
    character(:), allocatable :: json
    logical :: found
    call c_f_pointer(user_data, box)
    found = .false.
    call box%call(from_chars(uri, uri_len), json, found)
    status = CJS_OK
    if (found .and. allocated(json)) status = cjs_resolved_set_json(out, json, len(json, c_size_t))
  end function resolver_trampoline

  subroutine free_resolver_box(user_data) bind(c)
    type(c_ptr), value :: user_data
    type(resolver_box), pointer :: box
    call c_f_pointer(user_data, box)
    deallocate (box)
  end subroutine free_resolver_box

  ! ---- validator_t ----

  !> Compiles a schema from its JSON text, with the options (the defaults without them). A validator that was
  !> compiled before lets go of its schema first.
  subroutine validator_compile(self, schema, options, stat, errmsg)
    class(validator_t), intent(inout) :: self
    character(*), intent(in) :: schema
    type(options_t), intent(in), optional :: options
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(c_ptr) :: out
    integer(c_int32_t) :: status
    call cjs_validator_free(self%handle)
    self%handle = c_null_ptr
    status = cjs_compile(schema, len(schema, c_size_t), options_handle(options), out)
    if (status == CJS_OK) self%handle = out
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine validator_compile

  !> Compiles the schema document at an absolute URI, fetched through the options' resolver (or one of the standard
  !> metaschemas).
  subroutine validator_compile_uri(self, uri, options, stat, errmsg)
    class(validator_t), intent(inout) :: self
    character(*), intent(in) :: uri
    type(options_t), intent(in), optional :: options
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(c_ptr) :: out
    integer(c_int32_t) :: status
    call cjs_validator_free(self%handle)
    self%handle = c_null_ptr
    status = cjs_compile_uri(uri, len(uri, c_size_t), options_handle(options), out)
    if (status == CJS_OK) self%handle = out
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine validator_compile_uri

  function options_handle(options) result(handle)
    type(options_t), intent(in), optional :: options
    type(c_ptr) :: handle
    handle = c_null_ptr
    if (present(options)) handle = options%handle
  end function options_handle

  !> Whether the validator holds a compiled schema.
  function validator_is_compiled(self) result(compiled)
    class(validator_t), intent(in) :: self
    logical :: compiled
    compiled = c_associated(self%handle)
  end function validator_is_compiled

  !> Whether the JSON text is valid, evaluating only as far as the answer needs. The text is parsed into per-thread
  !> buffers and evaluated in place. A failure (text that is not JSON, CJS_DEPTH_EXCEEDED, CJS_PATTERN_MATCH_LIMIT)
  !> gives .false., which is then not an answer.
  function validator_is_valid(self, json, stat, errmsg) result(valid)
    class(validator_t), intent(in) :: self
    character(*), intent(in) :: json
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    logical :: valid
    logical(c_bool) :: answer
    integer(c_int32_t) :: status
    valid = .false.
    if (.not. c_associated(self%handle)) then
      call report_misuse("the validator has no compiled schema", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    status = cjs_validator_validate_json(self%handle, json, len(json, c_size_t), answer)
    if (status == CJS_OK) valid = answer
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end function validator_is_valid

  !> Whether the parsed document is valid.
  function validator_is_valid_document(self, document, stat, errmsg) result(valid)
    class(validator_t), intent(in) :: self
    type(document_t), intent(in) :: document
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    logical :: valid
    logical(c_bool) :: answer
    integer(c_int32_t) :: status
    valid = .false.
    if (.not. c_associated(self%handle)) then
      call report_misuse("the validator has no compiled schema", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    if (.not. c_associated(document%handle)) then
      call report_misuse("the document has not been parsed", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    status = cjs_validator_validate_document(self%handle, document%handle, answer)
    if (status == CJS_OK) valid = answer
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end function validator_is_valid_document

  !> Evaluates the JSON text, evaluating every keyword and replacing the collector's results with this evaluation's,
  !> at the collector's level. Whether it is valid.
  function validator_evaluate(self, json, collector, stat, errmsg) result(valid)
    class(validator_t), intent(in) :: self
    character(*), intent(in) :: json
    type(collector_t), intent(inout) :: collector
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    logical :: valid
    logical(c_bool) :: answer
    integer(c_int32_t) :: status
    valid = .false.
    if (.not. c_associated(self%handle)) then
      call report_misuse("the validator has no compiled schema", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    call collector%ensure()
    status = cjs_validator_evaluate_json(self%handle, json, len(json, c_size_t), collector%handle, answer)
    if (status == CJS_OK) valid = answer
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end function validator_evaluate

  !> validator_t%evaluate for a parsed document.
  function validator_evaluate_document(self, document, collector, stat, errmsg) result(valid)
    class(validator_t), intent(in) :: self
    type(document_t), intent(in) :: document
    type(collector_t), intent(inout) :: collector
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    logical :: valid
    logical(c_bool) :: answer
    integer(c_int32_t) :: status
    valid = .false.
    if (.not. c_associated(self%handle)) then
      call report_misuse("the validator has no compiled schema", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    if (.not. c_associated(document%handle)) then
      call report_misuse("the document has not been parsed", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    call collector%ensure()
    status = cjs_validator_evaluate_document(self%handle, document%handle, collector%handle, answer)
    if (status == CJS_OK) valid = answer
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end function validator_evaluate_document

  subroutine validator_assign(lhs, rhs)
    class(validator_t), intent(inout) :: lhs
    type(validator_t), intent(in) :: rhs
    type(c_ptr) :: clone
    clone = cjs_validator_clone(rhs%handle)
    call cjs_validator_free(lhs%handle)
    lhs%handle = clone
  end subroutine validator_assign

  subroutine validator_final(self)
    type(validator_t), intent(inout) :: self
    call cjs_validator_free(self%handle)
    self%handle = c_null_ptr
  end subroutine validator_final

  ! ---- document_t ----

  !> Parses JSON text into the document (the text is copied). A document that was parsed before lets go of it first.
  subroutine document_parse(self, json, stat, errmsg)
    class(document_t), intent(inout) :: self
    character(*), intent(in) :: json
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(c_ptr) :: out
    integer(c_int32_t) :: status
    call cjs_document_free(self%handle)
    self%handle = c_null_ptr
    status = cjs_document_parse(json, len(json, c_size_t), out)
    if (status == CJS_OK) self%handle = out
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine document_parse

  !> Whether the document holds parsed JSON.
  function document_is_parsed(self) result(parsed)
    class(document_t), intent(in) :: self
    logical :: parsed
    parsed = c_associated(self%handle)
  end function document_is_parsed

  subroutine document_assign(lhs, rhs)
    class(document_t), intent(inout) :: lhs
    type(document_t), intent(in) :: rhs
    if (c_associated(lhs%handle) .or. c_associated(rhs%handle)) then
      write (error_unit, "(a)") "corvus_json_schema: a document_t cannot be copied"
      error stop 1
    end if
  end subroutine document_assign

  subroutine document_final(self)
    type(document_t), intent(inout) :: self
    call cjs_document_free(self%handle)
    self%handle = c_null_ptr
  end subroutine document_final

  ! ---- collector_t ----

  !> Makes the collector record at the level (BASIC, DETAILED or VERBOSE), letting go of any results it holds. A
  !> collector that is not initialized records at BASIC.
  subroutine collector_init(self, level, stat, errmsg)
    class(collector_t), intent(inout) :: self
    integer, intent(in) :: level
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    type(c_ptr) :: handle
    if (level /= BASIC .and. level /= DETAILED .and. level /= VERBOSE) then
      call report_misuse("the level is not BASIC, DETAILED or VERBOSE", stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    handle = cjs_collector_new(int(level, c_int32_t))
    if (.not. c_associated(handle)) then
      call report(int(CJS_PANIC, c_int32_t), stat, message)
      if (present(errmsg)) errmsg = message
      return
    end if
    call cjs_collector_free(self%handle)
    self%handle = handle
    self%recorded = level
    call report(int(CJS_OK, c_int32_t), stat, message)
    if (present(errmsg)) errmsg = message
  end subroutine collector_init

  subroutine collector_ensure(self)
    class(collector_t), intent(inout) :: self
    if (.not. c_associated(self%handle)) self%handle = cjs_collector_new(int(self%recorded, c_int32_t))
  end subroutine collector_ensure

  !> The level the collector records at.
  function collector_level(self) result(level)
    class(collector_t), intent(in) :: self
    integer :: level
    level = self%recorded
  end function collector_level

  !> Removes the collector's results.
  subroutine collector_clear(self)
    class(collector_t), intent(inout) :: self
    if (c_associated(self%handle)) call cjs_collector_clear(self%handle)
  end subroutine collector_clear

  !> The number of result rows.
  function collector_count(self) result(count)
    class(collector_t), intent(in) :: self
    integer :: count
    count = int(cjs_collector_count(self%handle))
  end function collector_count

  !> Whether row i (from 1) is a match (.false. for a row out of range). The row functions read the results of the
  !> last evaluation into the collector.
  function collector_is_match(self, i) result(is_match)
    class(collector_t), intent(in) :: self
    integer, intent(in) :: i
    logical :: is_match
    is_match = .false.
    if (i < 1) return
    is_match = cjs_collector_is_match(self%handle, int(i - 1, c_size_t))
  end function collector_is_match

  !> Row i's message (empty when the level records none, or for a row out of range; raw JSON for an annotation row).
  function collector_message(self, i) result(text)
    class(collector_t), intent(in) :: self
    integer, intent(in) :: i
    character(:), allocatable :: text
    text = ""
    if (i < 1) return
    text = from_str(cjs_collector_message(self%handle, int(i - 1, c_size_t)))
  end function collector_message

  !> Row i's evaluation path: the keywords from the root schema, such as "/properties/name/type".
  function collector_evaluation_location(self, i) result(text)
    class(collector_t), intent(in) :: self
    integer, intent(in) :: i
    character(:), allocatable :: text
    text = ""
    if (i < 1) return
    text = from_str(cjs_collector_evaluation_location(self%handle, int(i - 1, c_size_t)))
  end function collector_evaluation_location

  !> Row i's schema location: the JSON pointer of the evaluated schema or keyword within its document.
  function collector_schema_location(self, i) result(text)
    class(collector_t), intent(in) :: self
    integer, intent(in) :: i
    character(:), allocatable :: text
    text = ""
    if (i < 1) return
    text = from_str(cjs_collector_schema_location(self%handle, int(i - 1, c_size_t)))
  end function collector_schema_location

  !> Row i's instance location: the JSON pointer of the evaluated value, such as "/name".
  function collector_instance_location(self, i) result(text)
    class(collector_t), intent(in) :: self
    integer, intent(in) :: i
    character(:), allocatable :: text
    text = ""
    if (i < 1) return
    text = from_str(cjs_collector_instance_location(self%handle, int(i - 1, c_size_t)))
  end function collector_instance_location

  !> The annotations of a VERBOSE evaluation as JSON text, by instance location, keyword and schema location:
  !> {"/name": {"title": {"#/properties/name": "Name"}}}.
  function collector_annotations_json(self, stat, errmsg) result(json)
    class(collector_t), intent(inout) :: self
    integer, intent(out), optional :: stat
    character(:), allocatable, intent(out), optional :: errmsg
    character(:), allocatable :: message
    character(:), allocatable :: json
    type(cjs_str) :: out
    integer(c_int32_t) :: status
    call self%ensure()
    status = cjs_collector_annotations_json(self%handle, out)
    json = ""
    if (status == CJS_OK) json = from_str(out)
    call report(status, stat, message)
    if (present(errmsg)) errmsg = message
  end function collector_annotations_json

  subroutine collector_assign(lhs, rhs)
    class(collector_t), intent(inout) :: lhs
    type(collector_t), intent(in) :: rhs
    if (c_associated(lhs%handle) .or. c_associated(rhs%handle)) then
      write (error_unit, "(a)") "corvus_json_schema: a collector_t cannot be copied"
      error stop 1
    end if
    lhs%recorded = rhs%recorded
  end subroutine collector_assign

  subroutine collector_final(self)
    type(collector_t), intent(inout) :: self
    call cjs_collector_free(self%handle)
    self%handle = c_null_ptr
  end subroutine collector_final

end module corvus_json_schema
