!> What the test programs share: reading files and listing a directory, finding the JSON-Schema-Test-Suite, a
!> resolver for its remotes, and a scanner that cuts JSON values out of a text as they are written (so that 1.0 stays
!> 1.0, which draft 4 does not count as an integer).
module suite_support
  use, intrinsic :: iso_fortran_env, only: error_unit
  implicit none
  private

  public :: span_t, read_file, list_json_files, suite_root, set_remotes, remote_resolver
  public :: skip_ws, skip_value, elements, member, has_member, raw, string_content, slash

  !> A JSON value within a text: its first and last characters.
  type :: span_t
    integer :: first = 0
    integer :: last = -1
  end type span_t

  character(:), allocatable, save :: remotes

contains

  !> The whole file as one string, or an unallocated string when it cannot be read.
  subroutine read_file(path, text)
    character(*), intent(in) :: path
    character(:), allocatable, intent(out) :: text
    integer :: unit, size, status
    open (newunit=unit, file=path, access="stream", form="unformatted", action="read", status="old", iostat=status)
    if (status /= 0) return
    inquire (unit=unit, size=size)
    allocate (character(size) :: text)
    if (size > 0) read (unit, iostat=status) text
    close (unit)
    if (status /= 0) deallocate (text)
  end subroutine read_file

  !> The path separator of the platform.
  function slash() result(separator)
    character(1) :: separator
    character(16) :: os
    integer :: length, status
    call get_environment_variable("OS", os, length, status)
    separator = "/"
    if (status == 0 .and. os(1:min(length, 10)) == "Windows_NT") separator = "\"
  end function slash

  !> The .json files of a directory, sorted by name (none when the directory does not exist).
  subroutine list_json_files(directory, names)
    character(*), intent(in) :: directory
    character(256), allocatable, intent(out) :: names(:)
    character(*), parameter :: listing = "corvus_json_schema_listing.tmp"
    character(256) :: line
    character(256), allocatable :: grown(:)
    integer :: unit, status, count
    if (slash() == "\") then
      call execute_command_line('dir /b /on "' // directory // '\*.json" > ' // listing // ' 2> nul', exitstat=status)
    else
      call execute_command_line('ls -1 "' // directory // '" 2> /dev/null | grep "\.json$" | LC_ALL=C sort > ' // &
                                listing, exitstat=status)
    end if
    allocate (names(0))
    open (newunit=unit, file=listing, action="read", status="old", iostat=status)
    if (status /= 0) return
    count = 0
    do
      read (unit, "(a)", iostat=status) line
      if (status /= 0) exit
      if (len_trim(line) == 0) cycle
      count = count + 1
      allocate (grown(count))
      grown(1:count - 1) = names
      grown(count) = adjustl(line)
      call move_alloc(grown, names)
    end do
    close (unit, status="delete")
  end subroutine list_json_files

  !> The JSON-Schema-Test-Suite: JSON_SCHEMA_TEST_SUITE, or the repository's submodule seen from the package directory.
  !> Unallocated when neither is there.
  subroutine suite_root(root)
    character(:), allocatable, intent(out) :: root
    character(4096) :: value
    character(:), allocatable :: probe
    integer :: length, status
    call get_environment_variable("JSON_SCHEMA_TEST_SUITE", value, length, status)
    if (status == 0 .and. length > 0) then
      root = value(1:length)
    else
      root = ".." // slash() // ".." // slash() // "JSON-Schema-Test-Suite"
    end if
    call read_file(root // slash() // "tests" // slash() // "draft2020-12" // slash() // "type.json", probe)
    if (.not. allocated(probe)) deallocate (root)
  end subroutine suite_root

  !> Sets the directory of the suite's remote documents, which remote_resolver serves.
  subroutine set_remotes(directory)
    character(*), intent(in) :: directory
    remotes = directory
  end subroutine set_remotes

  !> Serves http://localhost:1234/<path> from the suite's remotes directory.
  subroutine remote_resolver(uri, json, found)
    character(*), intent(in) :: uri
    character(:), allocatable, intent(out) :: json
    logical, intent(out) :: found
    character(*), parameter :: prefix = "http://localhost:1234/"
    character(len(uri)) :: path
    integer :: i
    found = .false.
    if (len(uri) <= len(prefix)) return
    if (uri(1:len(prefix)) /= prefix) return
    path = uri(len(prefix) + 1:)
    if (slash() == "\") then
      do i = 1, len(path)
        if (path(i:i) == "/") path(i:i) = "\"
      end do
    end if
    call read_file(remotes // slash() // trim(path), json)
    found = allocated(json)
  end subroutine remote_resolver

  ! ---- the scanner ----

  subroutine skip_ws(text, pos)
    character(*), intent(in) :: text
    integer, intent(inout) :: pos
    do while (pos <= len(text))
      select case (text(pos:pos))
      case (" ", achar(9), achar(10), achar(13))
        pos = pos + 1
      case default
        return
      end select
    end do
  end subroutine skip_ws

  !> Moves pos from the first character of a value to the character after it.
  recursive subroutine skip_value(text, pos)
    character(*), intent(in) :: text
    integer, intent(inout) :: pos
    integer :: depth
    character :: c
    call skip_ws(text, pos)
    if (pos > len(text)) return
    c = text(pos:pos)
    if (c == '"') then
      pos = pos + 1
      do while (pos <= len(text))
        if (text(pos:pos) == "\") then
          pos = pos + 2
        else if (text(pos:pos) == '"') then
          pos = pos + 1
          return
        else
          pos = pos + 1
        end if
      end do
    else if (c == "{" .or. c == "[") then
      depth = 0
      do while (pos <= len(text))
        c = text(pos:pos)
        if (c == '"') then
          call skip_value(text, pos)
          cycle
        end if
        if (c == "{" .or. c == "[") depth = depth + 1
        if (c == "}" .or. c == "]") depth = depth - 1
        pos = pos + 1
        if (depth == 0) return
      end do
    else
      do while (pos <= len(text))
        c = text(pos:pos)
        if (c == "," .or. c == "}" .or. c == "]" .or. c == " " .or. c == achar(9) .or. c == achar(10) &
            .or. c == achar(13)) return
        pos = pos + 1
      end do
    end if
  end subroutine skip_value

  !> The elements of the array at span.
  subroutine elements(text, array, items)
    character(*), intent(in) :: text
    type(span_t), intent(in) :: array
    type(span_t), allocatable, intent(out) :: items(:)
    type(span_t), allocatable :: grown(:)
    integer :: pos, count, first
    allocate (items(0))
    pos = array%first + 1
    count = 0
    do
      call skip_ws(text, pos)
      if (pos > array%last .or. text(pos:pos) == "]") return
      first = pos
      call skip_value(text, pos)
      count = count + 1
      allocate (grown(count))
      grown(1:count - 1) = items
      grown(count) = span_t(first, pos - 1)
      call move_alloc(grown, items)
      call skip_ws(text, pos)
      if (pos <= array%last) then
        if (text(pos:pos) == ",") pos = pos + 1
      end if
    end do
  end subroutine elements

  !> The value of the object member whose key is written as key (a span with last < first when there is none).
  function member(text, object, key) result(value)
    character(*), intent(in) :: text
    type(span_t), intent(in) :: object
    character(*), intent(in) :: key
    type(span_t) :: value
    integer :: pos, key_first, key_end
    pos = object%first + 1
    do
      call skip_ws(text, pos)
      if (pos > object%last .or. text(pos:pos) == "}") return
      key_first = pos
      call skip_value(text, pos)
      key_end = pos - 1
      call skip_ws(text, pos)
      pos = pos + 1
      call skip_ws(text, pos)
      value%first = pos
      call skip_value(text, pos)
      if (key_end - key_first - 1 == len(key)) then
        if (text(key_first + 1:key_end - 1) == key) then
          value%last = pos - 1
          return
        end if
      end if
      value%first = 0
      call skip_ws(text, pos)
      if (pos <= object%last) then
        if (text(pos:pos) == ",") pos = pos + 1
      end if
    end do
  end function member

  logical function has_member(value)
    type(span_t), intent(in) :: value
    has_member = value%last >= value%first .and. value%first > 0
  end function has_member

  !> The value's text, as it is written.
  function raw(text, value) result(written)
    character(*), intent(in) :: text
    type(span_t), intent(in) :: value
    character(:), allocatable :: written
    written = text(value%first:value%last)
  end function raw

  !> A string value's characters between its quotes, escapes left as they are written.
  function string_content(text, value) result(content)
    character(*), intent(in) :: text
    type(span_t), intent(in) :: value
    character(:), allocatable :: content
    content = text(value%first + 1:value%last - 1)
  end function string_content

end module suite_support
