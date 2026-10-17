!> Compiles a schema, validates two instances, and prints why the second is not valid.
program validate
  use corvus_json_schema
  implicit none
  type(validator_t) :: validator
  type(collector_t) :: collector
  integer :: i
  logical :: valid

  call validator%compile('{"type": "object", "required": ["id"], "properties": {"id": {"type": "integer"}}}')
  print "(a,l1)", "{""id"": 3} is valid: ", validator%is_valid('{"id": 3}')

  call collector%init(DETAILED)
  valid = validator%evaluate('{"id": "3"}', collector)
  print "(a,l1)", "{""id"": ""3""} is valid: ", valid
  do i = 1, collector%count()
    if (.not. collector%is_match(i)) then
      print "(a)", "  " // collector%instance_location(i) // ": " // collector%message(i)
    end if
  end do
  if (valid) error stop 1
end program validate
