!> The JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE, or the repository's submodule): every case of draft 4, 6, 7,
!> 2019-09 and 2020-12 (one tree, validation/tests, required and optional, each case's compatibility saying which
!> drafts it applies to and its tags whether format is asserted), as JSON text, as a parsed document, and at the
!> verbose level through a collector, which must all give the expected verdict. Each case's externalSchemas are
!> served to that case alone. As the other runners, draft4/optional/zero-terminated-floats.json is left out (1.0 and 1
!> cannot be told apart there), leap-second cases that assert format are skipped, and so are the high-precision
!> cases of optional/bignum.json, whose digits the C library's serde_json values do not hold. Without the suite it
!> says SKIPPED.
program suite_test
  use corvus_json_schema
  use suite_support
  implicit none

  character(*), parameter :: drafts(5) = [character(12) :: "draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"]
  integer, parameter :: dialects(5) = [DRAFT4, DRAFT6, DRAFT7, DRAFT2019_09, DRAFT2020_12]
  integer, parameter :: releases(5) = [4, 6, 7, 2019, 2020]
  character(*), parameter :: excluded = "draft4/optional/zero-terminated-floats.json"
  character(*), parameter :: excluded_cases(9) = [character(48) :: &
    "below the exclusive maximum is valid", "above the exclusive minimum is valid", &
    "just above the maximum is invalid", "just below the minimum is invalid", &
    "a slightly smaller decimal is invalid", "a slightly larger decimal is invalid", &
    "distinct high-precision decimals are unique", "arrays containing distinct decimals are unique", &
    "objects containing distinct decimals are unique"]
  character(:), allocatable :: root, s
  integer :: d, total, failed, skipped, files

  call suite_root(root)
  if (.not. allocated(root)) then
    print "(a)", "suite: SKIPPED, the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE, or check out " // &
      "the submodule)"
    stop
  end if
  s = slash()
  total = 0
  failed = 0
  skipped = 0
  files = 0
  do d = 1, size(drafts)
    call run_directory(trim(drafts(d)), "", dialects(d), releases(d))
    call run_directory(trim(drafts(d)), "optional", dialects(d), releases(d))
  end do
  print "(a,i0,a,i0,a,i0,a,i0,a)", "JSON-Schema-Test-Suite: ", total - failed, "/", total, " passed (", files, &
    " files, ", skipped, " leap-second cases skipped), as JSON text, as a document and through a collector"
  if (total < 8000) then
    print "(a)", "FAIL: fewer than 8000 cases ran"
    error stop 1
  end if
  if (failed > 0) error stop 1

contains

  subroutine run_directory(draft, sub, dialect, release)
    character(*), intent(in) :: draft, sub
    integer, intent(in) :: dialect, release
    character(256), allocatable :: names(:)
    character(:), allocatable :: directory, label, prefix
    integer :: i
    directory = root // s // "validation" // s // "tests"
    prefix = draft // "/"
    if (len(sub) > 0) then
      directory = directory // s // sub
      prefix = draft // "/" // translate_slash(sub) // "/"
    end if
    call list_json_files(directory, names)
    do i = 1, size(names)
      label = prefix // trim(names(i))
      if (label == excluded) cycle
      files = files + 1
      call run_file(directory // s // trim(names(i)), trim(names(i)), label, dialect, release)
    end do
  end subroutine run_directory

  function translate_slash(path) result(translated)
    character(*), intent(in) :: path
    character(len(path)) :: translated
    integer :: i
    translated = path
    do i = 1, len(path)
      if (translated(i:i) == "\") translated(i:i) = "/"
    end do
  end function translate_slash

  subroutine run_file(path, name, label, dialect, release)
    character(*), intent(in) :: path, name, label
    integer, intent(in) :: dialect, release
    character(:), allocatable :: text, description, test_description, what, errmsg, data
    type(span_t), allocatable :: groups(:), tests(:)
    type(span_t) :: whole
    type(validator_t) :: validator
    type(document_t) :: document
    type(collector_t) :: collector
    integer :: g, t, stat, stat_doc, stat_collected
    logical :: expected, by_text, by_document, collected, assert_format

    call read_file(path, text)
    if (.not. allocated(text)) then
      print "(a)", "FAIL: cannot read " // path
      failed = failed + 1
      return
    end if
    call collector%init(VERBOSE)
    whole%first = 1
    whole%last = len(text)
    call skip_ws(text, whole%first)
    call elements(text, member(text, whole, "tests"), groups)
    do g = 1, size(groups)
      if (.not. compatible(text, groups(g), release)) cycle
      ! The evaluator asserts format by default up to draft7, so a format-annotation case does not apply there.
      if (has_tag(text, groups(g), "format-annotation") .and. release <= 7) cycle
      assert_format = has_tag(text, groups(g), "format-assertion")
      description = string_content(text, member(text, groups(g), "description"))
      call set_case_schemas(text, groups(g))
      block
        type(options_t) :: options
        call options%set_default_dialect(dialect)
        if (assert_format) call options%set_assert_format(.true.)
        call options%set_resolver(case_resolver)
        call validator%compile(raw(text, member(text, groups(g), "schema")), options, stat=stat, errmsg=errmsg)
      end block
      call elements(text, member(text, groups(g), "tests"), tests)
      if (stat /= CJS_OK) then
        total = total + size(tests)
        failed = failed + size(tests)
        print "(a)", "FAIL: " // label // " [" // description // "]: " // errmsg
        cycle
      end if
      do t = 1, size(tests)
        test_description = string_content(text, member(text, tests(t), "description"))
        if (name == "bignum.json" .and. any(excluded_cases == test_description)) cycle
        what = label // " [" // description // "] " // test_description
        if (assert_format .and. index(lower(what), "leap second") > 0) then
          skipped = skipped + 1
          cycle
        end if
        total = total + 1
        data = raw(text, member(text, tests(t), "data"))
        expected = raw(text, member(text, tests(t), "valid")) == "true"
        by_text = validator%is_valid(data, stat=stat, errmsg=errmsg)
        if (stat /= CJS_OK) then
          call fail(what // ": " // errmsg)
          cycle
        end if
        call document%parse(data, stat=stat_doc, errmsg=errmsg)
        if (stat_doc /= CJS_OK) then
          call fail(what // ": " // errmsg)
          cycle
        end if
        by_document = validator%is_valid_document(document, stat=stat_doc, errmsg=errmsg)
        collected = validator%evaluate(data, collector, stat=stat_collected, errmsg=errmsg)
        if (stat_doc /= CJS_OK .or. stat_collected /= CJS_OK) then
          call fail(what // ": " // errmsg)
          cycle
        end if
        if (by_text .neqv. expected .or. by_document .neqv. expected .or. collected .neqv. expected) then
          call fail(what // ": expected " // merge("valid  ", "invalid", expected) // ", got " // &
                    verdicts(by_text, by_document, collected))
        end if
      end do
    end do
  end subroutine run_file

  subroutine fail(message)
    character(*), intent(in) :: message
    failed = failed + 1
    if (failed <= 40) print "(a)", "FAIL: " // message
  end subroutine fail

  function verdicts(a, b, c) result(text)
    logical, intent(in) :: a, b, c
    character(:), allocatable :: text
    text = trim(merge("valid  ", "invalid", a)) // " " // trim(merge("valid  ", "invalid", b)) // " " // &
           trim(merge("valid  ", "invalid", c))
  end function verdicts

  function lower(text) result(lowered)
    character(*), intent(in) :: text
    character(len(text)) :: lowered
    integer :: i, c
    lowered = text
    do i = 1, len(text)
      c = iachar(text(i:i))
      if (c >= iachar("A") .and. c <= iachar("Z")) lowered(i:i) = achar(c + 32)
    end do
  end function lower

end program suite_test
