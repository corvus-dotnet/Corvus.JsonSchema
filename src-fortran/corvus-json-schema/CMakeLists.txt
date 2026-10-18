# The Fortran binding to the corvus-json-schema C library, for CMake projects (fpm.toml builds it for fpm).
#
#   find_package(corvus_json_schema_fortran)  after installing, or add_subdirectory() on this directory
#   target_link_libraries(app PRIVATE corvus_json_schema::fortran)
#
# The C library comes from its own CMake package (a capi-v<version> release, 0.1.4 or later): give its directory in
# CMAKE_PREFIX_PATH or corvus_json_schema_DIR.
cmake_minimum_required(VERSION 3.16)
project(corvus_json_schema_fortran VERSION 0.1.0 LANGUAGES Fortran)

find_package(corvus_json_schema 0.1.4 REQUIRED)

add_library(corvus_json_schema_fortran src/corvus_json_schema.f90)
add_library(corvus_json_schema::fortran ALIAS corvus_json_schema_fortran)
set_target_properties(corvus_json_schema_fortran PROPERTIES
  Fortran_MODULE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/modules"
  EXPORT_NAME fortran)
target_include_directories(corvus_json_schema_fortran PUBLIC
  "$<BUILD_INTERFACE:${CMAKE_CURRENT_BINARY_DIR}/modules>"
  "$<INSTALL_INTERFACE:include/corvus_json_schema_fortran>")
target_link_libraries(corvus_json_schema_fortran PUBLIC corvus_json_schema::corvus_json_schema)

option(CORVUS_JSON_SCHEMA_FORTRAN_TESTS "Build the tests (ctest)" ${PROJECT_IS_TOP_LEVEL})
if(CORVUS_JSON_SCHEMA_FORTRAN_TESTS)
  enable_testing()
  add_library(corvus_json_schema_fortran_suite_support OBJECT test/suite_support.f90)
  set_target_properties(corvus_json_schema_fortran_suite_support PROPERTIES
    Fortran_MODULE_DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/test-modules")
  foreach(test suite_test annotations_test api_test)
    add_executable(${test} test/${test}.f90)
    target_link_libraries(${test} PRIVATE corvus_json_schema_fortran corvus_json_schema_fortran_suite_support)
    target_include_directories(${test} PRIVATE "${CMAKE_CURRENT_BINARY_DIR}/test-modules")
    # From the package directory, as fpm test runs them: the suite is found at ../../JSON-Schema-Test-Suite.
    add_test(NAME ${test} COMMAND ${test} WORKING_DIRECTORY "${CMAKE_CURRENT_SOURCE_DIR}")
  endforeach()
  add_executable(validate_example example/validate.f90)
  target_link_libraries(validate_example PRIVATE corvus_json_schema_fortran)
  add_test(NAME validate_example COMMAND validate_example)
endif()

include(GNUInstallDirs)
include(CMakePackageConfigHelpers)
install(TARGETS corvus_json_schema_fortran EXPORT corvus_json_schema_fortran_targets
  ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
  LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR})
install(DIRECTORY "${CMAKE_CURRENT_BINARY_DIR}/modules/" DESTINATION include/corvus_json_schema_fortran)
install(EXPORT corvus_json_schema_fortran_targets NAMESPACE corvus_json_schema::
  DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/corvus_json_schema_fortran)
configure_package_config_file(cmake/corvus_json_schema_fortranConfig.cmake.in
  "${CMAKE_CURRENT_BINARY_DIR}/corvus_json_schema_fortranConfig.cmake"
  INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/corvus_json_schema_fortran)
write_basic_package_version_file("${CMAKE_CURRENT_BINARY_DIR}/corvus_json_schema_fortranConfigVersion.cmake"
  COMPATIBILITY SameMinorVersion)
install(FILES "${CMAKE_CURRENT_BINARY_DIR}/corvus_json_schema_fortranConfig.cmake"
  "${CMAKE_CURRENT_BINARY_DIR}/corvus_json_schema_fortranConfigVersion.cmake"
  DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/corvus_json_schema_fortran)
