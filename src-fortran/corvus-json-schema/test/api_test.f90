!> The binding's API: options, errors and their statuses, custom formats, the resolver, the limits, the collector
!> and its levels, annotations, validator assignment, and the library's version.
module api_formats
  implicit none
  private
  public :: is_even, everything_is_known, calls

  integer, save :: calls = 0

contains

  ! A custom format: an even number written as decimal digits.
  function is_even(value) result(valid)
    character(*), intent(in) :: value
    logical :: valid
    integer :: n, status
    calls = calls + 1
    read (value, *, iostat=status) n
    valid = status == 0 .and. mod(n, 2) == 0
  end function is_even

  ! A resolver that knows one document.
  subroutine everything_is_known(uri, json, found)
    character(*), intent(in) :: uri
    character(:), allocatable, intent(out) :: json
    logical, intent(out) :: found
    found = uri == "https://example.com/positive.json"
    if (found) json = '{"type": "integer", "minimum": 1}'
  end subroutine everything_is_known

end module api_formats

program api_test
  use, intrinsic :: iso_fortran_env, only: real64
  use corvus_json_schema
  use api_formats
  implicit none

  integer :: passed = 0, failed = 0

  call test_validate()
  call test_errors()
  call test_options()
  call test_formats()
  call test_resolver()
  call test_limits()
  call test_collector()
  call test_annotations()
  call test_assignment()
  call test_documents()
  call test_version()
  call test_const_comparison()

  print "(a,i0,a,i0,a)", "api: ", passed, " passed, ", failed, " failed"
  if (failed > 0) error stop 1

contains

  subroutine check(condition, what)
    logical, intent(in) :: condition
    character(*), intent(in) :: what
    if (condition) then
      passed = passed + 1
    else
      failed = failed + 1
      print "(a)", "FAIL: " // what
    end if
  end subroutine check

  subroutine test_validate()
    type(validator_t) :: validator
    integer :: stat
    call check(.not. validator%is_compiled(), "a validator starts without a schema")
    call validator%compile('{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}')
    call check(validator%is_compiled(), "compile gives a validator")
    call check(validator%is_valid('{"id": 3}'), "a valid instance")
    call check(.not. validator%is_valid('{"id": "3"}'), "an invalid instance")
    call check(.not. validator%is_valid('{}', stat=stat), "a missing property")
    call check(stat == CJS_OK, "an invalid instance is not a failure")
    call validator%compile('{"type": "string"}')
    call check(validator%is_valid('"a"') .and. .not. validator%is_valid('1'), "compiling again replaces the schema")
  end subroutine test_validate

  subroutine test_errors()
    type(validator_t) :: validator, empty
    character(:), allocatable :: errmsg
    integer :: stat
    logical :: valid
    call validator%compile('{"type": ', stat=stat, errmsg=errmsg)
    call check(stat == CJS_INVALID_JSON, "a schema that is not JSON")
    call check(len(errmsg) > 0, "with a message")
    call check(.not. validator%is_compiled(), "and no validator")
    call validator%compile('{"$ref": "#/$defs/missing"}', stat=stat, errmsg=errmsg)
    call check(stat == CJS_COMPILATION_FAILED, "an unresolvable reference does not compile: " // errmsg)
    call validator%compile('{"pattern": "("}', stat=stat, errmsg=errmsg)
    call check(stat == CJS_COMPILATION_FAILED, "a pattern that is not valid does not compile")
    call validator%compile('{"type": "integer"}')
    valid = validator%is_valid('[1, 2', stat=stat, errmsg=errmsg)
    call check(stat == CJS_INVALID_JSON .and. .not. valid, "an instance that is not JSON")
    call check(last_error_offset() > 0, "has the offset where it was found")
    valid = validator%is_valid('"' // char(255) // '"', stat=stat)
    call check(stat == CJS_INVALID_UTF8, "an instance that is not UTF-8")
    call check(last_error_offset() == 1, "at the offset of the first invalid byte")
    valid = empty%is_valid('1', stat=stat, errmsg=errmsg)
    call check(stat == CJS_INVALID_ARGUMENT .and. index(errmsg, "no compiled schema") > 0, &
               "a validator without a schema")
    call check(stat == CJS_INVALID_ARGUMENT, "is an invalid argument")
    valid = validator%is_valid('1', stat=stat, errmsg=errmsg)
    call check(stat == CJS_OK .and. errmsg == "", "a success clears errmsg")
  end subroutine test_errors

  subroutine test_options()
    type(options_t) :: options, legacy, content, entry, base, depth, bad
    type(validator_t) :: validator
    integer :: stat
    ! The default dialect: draft 4 reads exclusiveMinimum as a boolean.
    call options%set_default_dialect(DRAFT4)
    call validator%compile('{"minimum": 5, "exclusiveMinimum": true}', options)
    call check(.not. validator%is_valid('5') .and. validator%is_valid('6'), "set_default_dialect(DRAFT4)")
    call validator%compile('{"$schema": "https://json-schema.org/draft/2020-12/schema", "exclusiveMinimum": 5}', &
                           options)
    call check(.not. validator%is_valid('5'), "$schema wins over the default dialect")
    call bad%set_default_dialect(3, stat=stat)
    call check(stat == CJS_INVALID_ARGUMENT, "a dialect that is not one")
    ! format: annotation by default, asserted when asked, and back to the default.
    call validator%compile('{"format": "email"}')
    call check(validator%is_valid('"not an email"'), "format annotates by default")
    call options%set_assert_format(.true.)
    call validator%compile('{"format": "email"}', options)
    call check(.not. validator%is_valid('"not an email"'), "set_assert_format(.true.)")
    call options%set_assert_format()
    call validator%compile('{"format": "email"}', options)
    call check(validator%is_valid('"not an email"'), "set_assert_format() follows the vocabularies again")
    call legacy%set_default_dialect(DRAFT7)
    call legacy%set_assert_format_in_legacy_drafts(.true.)
    call validator%compile('{"format": "email"}', legacy)
    call check(.not. validator%is_valid('"not an email"'), "set_assert_format_in_legacy_drafts(.true.)")
    ! Content assertion in draft 7.
    call content%set_default_dialect(DRAFT7)
    call validator%compile('{"contentMediaType": "application/json"}', content)
    call check(.not. validator%is_valid('"{not json"'), "draft 7 asserts content by default")
    call content%set_assert_content(.false.)
    call validator%compile('{"contentMediaType": "application/json"}', content)
    call check(validator%is_valid('"{not json"'), "set_assert_content(.false.)")
    ! An entry point.
    call entry%set_entry_point("#/$defs/item")
    call validator%compile('{"$defs": {"item": {"type": "integer"}}, "type": "string"}', entry)
    call check(validator%is_valid('3') .and. .not. validator%is_valid('"a"'), "set_entry_point")
    ! A base URI, against which a relative reference is resolved.
    call base%set_base_uri("https://example.com/root.json")
    call base%set_resolver(everything_is_known)
    call validator%compile('{"$ref": "positive.json"}', base)
    call check(validator%is_valid('2') .and. .not. validator%is_valid('0'), "set_base_uri")
    ! The depth limit, on a schema that loops without consuming the instance.
    call depth%set_max_depth(16)
    call validator%compile('{"$defs": {"a": {"$ref": "#/$defs/b"}, "b": {"$ref": "#/$defs/a"}}, "$ref": "#/$defs/a"}', &
                           depth, stat=stat)
    if (stat == CJS_OK) then
      call check(.not. validator%is_valid('1', stat=stat), "a loop is not valid")
      call check(stat == CJS_DEPTH_EXCEEDED, "a loop exceeds the depth")
    else
      call check(stat == CJS_COMPILATION_FAILED, "a loop is refused when it is compiled")
    end if
    call depth%set_max_depth(-1, stat=stat)
    call check(stat == CJS_INVALID_ARGUMENT, "a depth that is not a count")
  end subroutine test_options

  subroutine test_formats()
    type(options_t) :: options
    type(validator_t) :: validator
    call options%set_assert_format(.true.)
    call options%add_format("even", is_even)
    call validator%compile('{"format": "even"}', options)
    calls = 0
    call check(validator%is_valid('"42"'), "a custom format accepts")
    call check(.not. validator%is_valid('"41"'), "a custom format rejects")
    call check(calls == 2, "the format procedure is called")
    call options%add_format("email", is_even)
    call validator%compile('{"format": "email"}', options)
    call check(validator%is_valid('"8"') .and. .not. validator%is_valid('"a@b.c"'), &
               "a custom format takes precedence over a built-in one")
  end subroutine test_formats

  subroutine test_resolver()
    type(options_t) :: options
    type(validator_t) :: validator
    integer :: stat
    character(:), allocatable :: errmsg
    call options%set_resolver(everything_is_known)
    call validator%compile('{"$ref": "https://example.com/positive.json"}', options)
    call check(validator%is_valid('1') .and. .not. validator%is_valid('-1'), "a document from the resolver")
    call validator%compile('{"$ref": "https://example.com/unknown.json"}', options, stat=stat, errmsg=errmsg)
    call check(stat == CJS_COMPILATION_FAILED, "a document the resolver does not know")
    call validator%compile_uri("https://example.com/positive.json", options)
    call check(validator%is_valid('5') .and. .not. validator%is_valid('0'), "compile_uri")
    call validator%compile_uri("https://json-schema.org/draft/2020-12/schema")
    call check(validator%is_valid('{"type": "string"}') .and. .not. validator%is_valid('{"type": 1}'), &
               "compile_uri of a standard metaschema")
  end subroutine test_resolver

  subroutine test_limits()
    type(options_t) :: limited, unlimited
    type(validator_t) :: validator
    integer :: stat, i
    character(:), allocatable :: errmsg, schema
    logical :: valid
    ! A pattern that backtracks without end on a string that almost matches: the backreference turns off the
    ! matcher's memory of the places where it has failed.
    call limited%set_pattern_match_limit(0.05_real64)
    call validator%compile('{"pattern": "^(?:(a+)\\1*)+$"}', limited)
    valid = validator%is_valid('"' // repeat("a", 40) // 'b"', stat=stat, errmsg=errmsg)
    call check(stat == CJS_PATTERN_MATCH_LIMIT .and. .not. valid, "a match is abandoned at its limit")
    call check(index(errmsg, "(a+)") > 0, "the message names the pattern: " // errmsg)
    call check(validator%is_valid('"aaaa"'), "a match within the limit is an answer")
    call unlimited%set_pattern_match_limit(0.0_real64)
    call validator%compile('{"pattern": "^(?:(a+)\\1*)+$"}', unlimited)
    call check(.not. validator%is_valid('"' // repeat("a", 12) // 'b"'), "zero turns the limit off")
    call check(default_pattern_match_limit == 1.0_real64, "the default limit is one second")
    ! Built in an allocated string: as an expression, a temporary of over 1 MB overflows the default stack of 1 MB on
    ! Windows (ifx puts temporaries on the stack).
    allocate (character(1048577 + 15) :: schema)
    schema(1:13) = '{"pattern": "'
    do i = 14, len(schema) - 2
      schema(i:i) = "a"
    end do
    schema(len(schema) - 1:) = '"}'
    call validator%compile(schema, stat=stat, errmsg=errmsg)
    call check(stat == CJS_PATTERN_TOO_LARGE, "a pattern over the limit of size does not compile")
  end subroutine test_limits

  subroutine test_collector()
    type(validator_t) :: validator
    type(collector_t) :: plain, detail, verbose_collector
    integer :: i, failures, stat
    logical :: valid, found
    call validator%compile('{"properties": {"name": {"type": "string"}}, "required": ["id"]}')
    call check(plain%level() == BASIC, "a collector records at BASIC unless it is initialized")
    valid = validator%evaluate('{"name": 1}', plain)
    call check(.not. valid, "evaluate gives the verdict")
    call check(plain%count() >= 2, "basic records the failures")
    call check(len(plain%message(1)) == 0, "basic has no messages")
    call check(.not. plain%is_match(plain%count()), "the root's own result is the last row")
    call detail%init(DETAILED)
    valid = validator%evaluate('{"name": 1}', detail)
    failures = 0
    found = .false.
    do i = 1, detail%count()
      if (.not. detail%is_match(i)) failures = failures + 1
      if (detail%instance_location(i) == "/name" .and. detail%evaluation_location(i) == "/properties/name/type") &
        found = .true.
    end do
    call check(failures == detail%count(), "detailed records failures only")
    call check(found, "a row has the instance and evaluation locations")
    call check(len(detail%message(1)) > 0, "detailed has messages")
    call check(len(detail%schema_location(1)) > 0, "a row has a schema location")
    call check(detail%message(0) == "" .and. detail%message(detail%count() + 1) == "", &
               "a row out of range is empty")
    call check(.not. detail%is_match(0), "a row out of range is not a match")
    call verbose_collector%init(VERBOSE)
    valid = validator%evaluate('{"id": 1, "name": "a"}', verbose_collector)
    call check(valid .and. verbose_collector%count() > 0, "verbose records passing results")
    call verbose_collector%clear()
    call check(verbose_collector%count() == 0, "clear removes the results")
    call verbose_collector%init(7, stat=stat)
    call check(stat == CJS_INVALID_ARGUMENT, "a level that is not one")
  end subroutine test_collector

  subroutine test_annotations()
    type(validator_t) :: validator, same
    type(collector_t) :: collector
    character(:), allocatable :: annotations
    logical :: valid
    call validator%compile('{"properties": {"name": {"title": "Name"}}}')
    call collector%init(VERBOSE)
    valid = validator%evaluate('{"name": "a"}', collector)
    annotations = collector%annotations_json()
    call same%compile('{"const": {"/name": {"title": {"#/properties/name": "Name"}}}}')
    call check(same%is_valid(annotations), "annotations_json: " // annotations)
  end subroutine test_annotations

  subroutine test_assignment()
    type(validator_t) :: original, copy
    call original%compile('{"type": "integer"}')
    copy = original
    call check(copy%is_compiled() .and. copy%is_valid('1') .and. .not. copy%is_valid('"1"'), &
               "an assigned validator validates")
    call original%compile('{"type": "string"}')
    call check(copy%is_valid('1') .and. original%is_valid('"1"'), "and is a handle of its own")
  end subroutine test_assignment

  subroutine test_documents()
    type(validator_t) :: validator
    type(document_t) :: document
    type(collector_t) :: collector
    integer :: stat
    logical :: valid
    call check(.not. document%is_parsed(), "a document starts empty")
    call document%parse('{"id": 1}')
    call check(document%is_parsed(), "parse")
    call validator%compile('{"required": ["id"]}')
    call check(validator%is_valid_document(document), "a valid document")
    call collector%init(DETAILED)
    call check(validator%evaluate_document(document, collector), "evaluate_document")
    call document%parse('{}')
    call check(.not. validator%is_valid_document(document), "a document parsed again")
    call document%parse('{', stat=stat)
    call check(stat == CJS_INVALID_JSON .and. .not. document%is_parsed(), "a document that is not JSON")
    valid = validator%is_valid_document(document, stat=stat)
    call check(stat == CJS_INVALID_ARGUMENT, "validating a document that is not parsed")
  end subroutine test_documents

  subroutine test_version()
    character(:), allocatable :: version
    integer :: major, minor, patch, first, second, status
    version = library_version()
    first = index(version, ".")
    second = index(version(first + 1:), ".") + first
    read (version(1:first - 1), *, iostat=status) major
    if (status == 0) read (version(first + 1:second - 1), *, iostat=status) minor
    if (status == 0) read (version(second + 1:), *, iostat=status) patch
    call check(status == 0, "library_version is major.minor.patch: " // version)
    call check(major * 65536 + minor * 256 + patch >= required_library_version, &
               "the C library is at least the version the binding needs: " // version)
    call check(binding_version == "0.1.0", "binding_version")
  end subroutine test_version

  ! The annotation test compares JSON values by validating one against {"const": <the other>}: it must tell them
  ! apart, and not only find them equal.
  subroutine test_const_comparison()
    type(validator_t) :: same
    call same%compile('{"const": {"#/a": 1, "#/b": [1, {"x": "y"}]}}')
    call check(same%is_valid('{"#/b": [1.0, {"x": "y"}], "#/a": 1}'), "const: numbers by value, members in any order")
    call check(.not. same%is_valid('{"#/a": 1}'), "const: a member missing")
    call check(.not. same%is_valid('{"#/a": 2, "#/b": [1, {"x": "y"}]}'), "const: a different value")
    call check(.not. same%is_valid('{}'), "const: no annotations")
    call same%compile('{"const": {}}')
    call check(same%is_valid('{}') .and. .not. same%is_valid('{"#": 1}'), "const: none expected")
  end subroutine test_const_comparison

end program api_test
