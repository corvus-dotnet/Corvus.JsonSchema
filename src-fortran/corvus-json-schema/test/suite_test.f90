!> The JSON-Schema-Test-Suite (JSON_SCHEMA_TEST_SUITE, or the repository's submodule): every case of draft 4, 6, 7,
!> 2019-09 and 2020-12, required, optional and optional/format, as JSON text, as a parsed document, and at the
!> verbose level through a collector, which must all give the expected verdict. Remote references are served from
!> the suite's remotes. As the other runners, draft4/optional/zeroTerminatedFloats.json is left out (1.0 and 1
!> cannot be told apart there), and leap-second format cases are skipped. Without the suite it says SKIPPED.
program suite_test
  use corvus_json_schema
  use suite_support
  implicit none

  character(*), parameter :: drafts(5) = [character(12) :: "draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"]
  integer, parameter :: dialects(5) = [DRAFT4, DRAFT6, DRAFT7, DRAFT2019_09, DRAFT2020_12]
  character(*), parameter :: excluded = "draft4/optional/zeroTerminatedFloats.json"
  character(:), allocatable :: root, s
  integer :: d, total, failed, skipped, files

  call suite_root(root)
  if (.not. allocated(root)) then
    print "(a)", "suite: SKIPPED, the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE, or check out " // &
      "the submodule)"
    stop
  end if
  s = slash()
  call set_remotes(root // s // "remotes")
  total = 0
  failed = 0
  skipped = 0
  files = 0
  do d = 1, size(drafts)
    call run_directory(trim(drafts(d)), "", dialects(d), .false.)
    call run_directory(trim(drafts(d)), "optional", dialects(d), .false.)
    call run_directory(trim(drafts(d)), "optional" // s // "format", dialects(d), .true.)
  end do
  print "(a,i0,a,i0,a,i0,a,i0,a)", "JSON-Schema-Test-Suite: ", total - failed, "/", total, " passed (", files, &
    " files, ", skipped, " leap-second cases skipped), as JSON text, as a document and through a collector"
  if (total < 7000) then
    print "(a)", "FAIL: fewer than 7000 cases ran"
    error stop 1
  end if
  if (failed > 0) error stop 1

contains

  subroutine run_directory(draft, sub, dialect, assert_format)
    character(*), intent(in) :: draft, sub
    integer, intent(in) :: dialect
    logical, intent(in) :: assert_format
    character(256), allocatable :: names(:)
    character(:), allocatable :: directory, label, prefix
    integer :: i
    directory = root // s // "tests" // s // draft
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
      call run_file(directory // s // trim(names(i)), label, dialect, assert_format)
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

  subroutine run_file(path, label, dialect, assert_format)
    character(*), intent(in) :: path, label
    integer, intent(in) :: dialect
    logical, intent(in) :: assert_format
    character(:), allocatable :: text, description, what, errmsg, data
    type(span_t), allocatable :: groups(:), tests(:)
    type(span_t) :: whole
    type(options_t) :: options
    type(validator_t) :: validator
    type(document_t) :: document
    type(collector_t) :: collector
    integer :: g, t, stat, stat_doc, stat_collected
    logical :: expected, by_text, by_document, collected

    call read_file(path, text)
    if (.not. allocated(text)) then
      print "(a)", "FAIL: cannot read " // path
      failed = failed + 1
      return
    end if
    call options%set_default_dialect(dialect)
    if (assert_format) call options%set_assert_format(.true.)
    call options%set_resolver(remote_resolver)
    call collector%init(VERBOSE)
    whole%first = 1
    whole%last = len(text)
    call skip_ws(text, whole%first)
    call elements(text, whole, groups)
    do g = 1, size(groups)
      description = string_content(text, member(text, groups(g), "description"))
      call validator%compile(raw(text, member(text, groups(g), "schema")), options, stat=stat, errmsg=errmsg)
      call elements(text, member(text, groups(g), "tests"), tests)
      if (stat /= CJS_OK) then
        total = total + size(tests)
        failed = failed + size(tests)
        print "(a)", "FAIL: " // label // " [" // description // "]: " // errmsg
        cycle
      end if
      do t = 1, size(tests)
        what = label // " [" // description // "] " // string_content(text, member(text, tests(t), "description"))
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
