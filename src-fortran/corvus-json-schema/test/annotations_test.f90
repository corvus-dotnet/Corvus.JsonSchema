!> The JSON-Schema-Test-Suite annotation tests (annotations/tests), through a verbose collector, as the other ports
!> run them: every draft, groups filtered by "compatibility", and each assertion's expected annotations compared with
!> the collector's, grouped by instance location, keyword and schema location. Two JSON values are compared with the
!> library itself, by validating one against {"const": <the other>}: numbers by value, object members in any order.
!> Without the suite it says SKIPPED.
program annotations_test
  use corvus_json_schema
  use suite_support
  implicit none

  character(*), parameter :: drafts(5) = [character(12) :: "draft4", "draft6", "draft7", "draft2019-09", "draft2020-12"]
  character(*), parameter :: levels(5) = [character(4) :: "4", "6", "7", "2019", "2020"]
  integer, parameter :: dialects(5) = [DRAFT4, DRAFT6, DRAFT7, DRAFT2019_09, DRAFT2020_12]
  character(*), parameter :: order(6) = [character(4) :: "3", "4", "6", "7", "2019", "2020"]
  character(:), allocatable :: root, s, directory
  character(256), allocatable :: names(:)
  integer :: d, i, total, failed

  call suite_root(root)
  if (.not. allocated(root)) then
    print "(a)", "annotations: SKIPPED, the JSON-Schema-Test-Suite is not there (set JSON_SCHEMA_TEST_SUITE, or " // &
      "check out the submodule)"
    stop
  end if
  s = slash()
  call set_remotes(root // s // "remotes")
  directory = root // s // "annotations" // s // "tests"
  call list_json_files(directory, names)
  total = 0
  failed = 0
  do d = 1, size(drafts)
    do i = 1, size(names)
      call run_file(directory // s // trim(names(i)), trim(drafts(d)) // "/" // trim(names(i)), d)
    end do
  end do
  print "(i0,a,i0,a)", total - failed, "/", total, " annotation assertions passed"
  if (total == 0) then
    print "(a)", "FAIL: no annotation assertions ran"
    error stop 1
  end if
  if (failed > 0) error stop 1

contains

  integer function position(level)
    character(*), intent(in) :: level
    integer :: k
    position = 0
    do k = 1, size(order)
      if (trim(order(k)) == level) position = k
    end do
  end function position

  ! Whether a group marked with compatibility applies to the draft at level: "2019" means 2019-09 and later, and
  ! "<=7" means draft 7 and earlier.
  logical function compatible(level, compatibility)
    character(*), intent(in) :: level, compatibility
    integer :: limit
    if (len(compatibility) > 2) then
      if (compatibility(1:2) == "<=") then
        limit = position(compatibility(3:))
        compatible = limit > 0 .and. position(level) <= limit
        return
      end if
    end if
    limit = position(compatibility)
    compatible = limit > 0 .and. position(level) >= limit
  end function compatible

  subroutine run_file(path, label, d)
    character(*), intent(in) :: path, label
    integer, intent(in) :: d
    character(:), allocatable :: text, produced, description, errmsg, actual, location, keyword
    type(span_t), allocatable :: groups(:), tests(:), assertions(:)
    type(span_t) :: whole, compatibility, by_location, by_keyword, actual_span
    type(options_t) :: options
    type(validator_t) :: validator, same
    type(collector_t) :: collector
    integer :: g, t, a, stat
    logical :: valid

    call read_file(path, text)
    if (.not. allocated(text)) then
      print "(a)", "FAIL: cannot read " // path
      failed = failed + 1
      return
    end if
    call options%set_default_dialect(dialects(d))
    call options%set_resolver(remote_resolver)
    call collector%init(VERBOSE)
    whole%first = 1
    whole%last = len(text)
    call skip_ws(text, whole%first)
    call elements(text, member(text, whole, "suite"), groups)
    do g = 1, size(groups)
      compatibility = member(text, groups(g), "compatibility")
      if (has_member(compatibility)) then
        if (.not. compatible(trim(levels(d)), string_content(text, compatibility))) cycle
      end if
      description = string_content(text, member(text, groups(g), "description"))
      call validator%compile(raw(text, member(text, groups(g), "schema")), options, stat=stat, errmsg=errmsg)
      if (stat /= CJS_OK) then
        failed = failed + 1
        print "(a)", "FAIL: " // label // " [" // description // "]: " // errmsg
        cycle
      end if
      call elements(text, member(text, groups(g), "tests"), tests)
      do t = 1, size(tests)
        valid = validator%evaluate(raw(text, member(text, tests(t), "instance")), collector, stat=stat, errmsg=errmsg)
        if (stat /= CJS_OK) then
          failed = failed + 1
          print "(a)", "FAIL: " // label // " [" // description // "]: " // errmsg
          cycle
        end if
        produced = collector%annotations_json()
        call elements(text, member(text, tests(t), "assertions"), assertions)
        do a = 1, size(assertions)
          total = total + 1
          location = string_content(text, member(text, assertions(a), "location"))
          keyword = string_content(text, member(text, assertions(a), "keyword"))
          ! The collector's annotations of the keyword at the location: {schema location: value}, or {} for none.
          actual = "{}"
          by_location = member(produced, span_t(1, len(produced)), location)
          if (has_member(by_location)) then
            by_keyword = member(produced, by_location, keyword)
            if (has_member(by_keyword)) then
              actual_span = by_keyword
              actual = raw(produced, actual_span)
            end if
          end if
          call same%compile('{"const": ' // raw(text, member(text, assertions(a), "expected")) // '}', &
                            stat=stat, errmsg=errmsg)
          if (stat == CJS_OK) valid = same%is_valid(actual, stat=stat, errmsg=errmsg)
          if (stat /= CJS_OK .or. .not. valid) then
            failed = failed + 1
            print "(a)", "FAIL: " // label // " [" // description // "] '" // location // "' " // keyword // &
              ": expected " // raw(text, member(text, assertions(a), "expected")) // ", actual " // actual
          end if
        end do
      end do
    end do
  end subroutine run_file

end program annotations_test
