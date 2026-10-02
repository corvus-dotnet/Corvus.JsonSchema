# The C and C++ tests, built against a package from package.ps1 as a consumer would use it:
#
#   cmake -S tests/native -B build -G Ninja -DCMAKE_PREFIX_PATH=<package directory> [-DCJS_SANITIZE=ON]
#   cmake --build build && ctest --test-dir build --output-on-failure
#
# Each test is built against the shared and the static library.
cmake_minimum_required(VERSION 3.16)
project(corvus_json_schema_native_tests C CXX)

find_package(corvus_json_schema 0.1 CONFIG REQUIRED)

option(CJS_SANITIZE "Build with AddressSanitizer and UndefinedBehaviorSanitizer (Clang or GCC)" OFF)

if(MSVC)
  set(warnings /W4 /WX)
else()
  set(warnings -Wall -Wextra -Wpedantic -Werror)
endif()

enable_testing()
foreach(kind shared static)
  if(kind STREQUAL "shared")
    set(library corvus_json_schema::corvus_json_schema)
  else()
    set(library corvus_json_schema::corvus_json_schema_static)
  endif()

  add_executable(c_${kind} smoke.c)
  # The header is C99; MSVC has no C99 mode, so C11 there.
  if(MSVC)
    set_target_properties(c_${kind} PROPERTIES C_STANDARD 11 C_STANDARD_REQUIRED ON C_EXTENSIONS OFF)
  else()
    set_target_properties(c_${kind} PROPERTIES C_STANDARD 99 C_STANDARD_REQUIRED ON C_EXTENSIONS OFF)
  endif()
  add_executable(cpp_${kind} wrapper.cpp)
  set_target_properties(cpp_${kind} PROPERTIES CXX_STANDARD 17 CXX_STANDARD_REQUIRED ON CXX_EXTENSIONS OFF)

  foreach(test c_${kind} cpp_${kind})
    target_link_libraries(${test} PRIVATE ${library})
    target_compile_options(${test} PRIVATE ${warnings})
    if(CJS_SANITIZE)
      target_compile_options(${test} PRIVATE -fsanitize=address,undefined -fno-omit-frame-pointer)
      target_link_options(${test} PRIVATE -fsanitize=address,undefined)
    endif()
    if(CJS_SANITIZE AND CMAKE_SYSTEM_NAME STREQUAL "Linux")
      # Older AddressSanitizer runtimes (clang 14's among them) crash at random on kernels that randomise more address
      # bits (vm.mmap_rnd_bits above 28) before reporting anything; without address-space randomisation they run.
      add_test(NAME ${test} COMMAND setarch ${CMAKE_SYSTEM_PROCESSOR} -R $<TARGET_FILE:${test}>)
    else()
      add_test(NAME ${test} COMMAND ${test})
    endif()
    if(WIN32 AND kind STREQUAL "shared")
      # The DLL is found next to the test.
      add_custom_command(TARGET ${test} POST_BUILD
        COMMAND ${CMAKE_COMMAND} -E copy_if_different $<TARGET_FILE:${library}> $<TARGET_FILE_DIR:${test}>)
    endif()
  endforeach()
endforeach()
