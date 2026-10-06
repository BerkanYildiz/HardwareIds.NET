cmake_minimum_required(VERSION 3.21)

# The publish workflow sets it from the version tag.
set(HARDWAREIDS_VERSION "2.2.0" CACHE STRING "Version of the library (major.minor.patch)")

project(HardwareIds
    VERSION ${HARDWAREIDS_VERSION}
    DESCRIPTION "Hardware identifiers of the local Windows computer, read straight from the native Windows APIs"
    HOMEPAGE_URL "https://github.com/BerkanYildiz/HardwareIds.NET"
    LANGUAGES C CXX)

if(NOT WIN32)
    message(FATAL_ERROR "HardwareIds reads Windows APIs and only builds for Windows.")
endif()

option(HARDWAREIDS_BUILD_SHARED "Build HardwareIds.dll, which exposes the C API" ON)
option(HARDWAREIDS_BUILD_TOOLS "Build the HardwareIdsSnapshot command line tool" ${PROJECT_IS_TOP_LEVEL})
option(HARDWAREIDS_BUILD_TESTS "Build the tests" ${PROJECT_IS_TOP_LEVEL})
option(HARDWAREIDS_INSTALL "Generate the install rules and the CMake package" ${PROJECT_IS_TOP_LEVEL})

set(HARDWAREIDS_SOURCES
    src/CApi.cpp
    src/Common.cpp
    src/Devices.cpp
    src/HardwareIds.cpp
    src/Json.cpp
    src/Network.cpp
    src/Smbios.cpp
    src/Storage.cpp
    src/System.cpp
    src/Wlan.cpp)

# Only system libraries. wlanapi.dll, bthprops.cpl and winbrand.dll are optional and loaded at runtime.
set(HARDWAREIDS_SYSTEM_LIBRARIES advapi32 bcrypt cfgmgr32 iphlpapi netapi32 user32 winspool ws2_32)

function(hardwareids_configure_target InTarget)
    target_compile_features(${InTarget} PUBLIC cxx_std_20)
    target_include_directories(${InTarget}
        PUBLIC
            $<BUILD_INTERFACE:${PROJECT_SOURCE_DIR}/include>
            $<INSTALL_INTERFACE:include>
        PRIVATE
            ${PROJECT_SOURCE_DIR}/src)
    target_compile_definitions(${InTarget} PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX _WIN32_WINNT=0x0A00)
    target_link_libraries(${InTarget} PRIVATE ${HARDWAREIDS_SYSTEM_LIBRARIES})

    if(MSVC)
        target_compile_options(${InTarget} PRIVATE /W4 /permissive- /utf-8 /EHsc)
    endif()
endfunction()

#
# Static library: the C++ API (HardwareIds.hpp) and the C API (HardwareIds.h).
#

add_library(HardwareIds STATIC ${HARDWAREIDS_SOURCES})
add_library(HardwareIds::HardwareIds ALIAS HardwareIds)
hardwareids_configure_target(HardwareIds)
target_compile_definitions(HardwareIds PUBLIC HARDWAREIDS_STATIC)
set_target_properties(HardwareIds PROPERTIES OUTPUT_NAME HardwareIdsStatic)

#
# HardwareIds.dll: the C API, for consumers that do not build with CMake or that link the C runtime differently.
#

if(HARDWAREIDS_BUILD_SHARED)
    add_library(HardwareIdsShared SHARED ${HARDWAREIDS_SOURCES})
    add_library(HardwareIds::Shared ALIAS HardwareIdsShared)
    hardwareids_configure_target(HardwareIdsShared)
    target_compile_definitions(HardwareIdsShared PRIVATE HARDWAREIDS_EXPORTS)
    set_target_properties(HardwareIdsShared PROPERTIES OUTPUT_NAME HardwareIds EXPORT_NAME Shared)
endif()

if(HARDWAREIDS_BUILD_TOOLS)
    add_executable(HardwareIdsSnapshot tools/Snapshot.cpp)
    target_link_libraries(HardwareIdsSnapshot PRIVATE HardwareIds)

    if(MSVC)
        target_compile_options(HardwareIdsSnapshot PRIVATE /W4 /permissive- /utf-8 /EHsc)
    endif()
endif()

if(HARDWAREIDS_BUILD_TESTS)
    enable_testing()

    add_executable(HardwareIdsTests tests/Tests.cpp)
    target_link_libraries(HardwareIdsTests PRIVATE HardwareIds)
    target_include_directories(HardwareIdsTests PRIVATE ${PROJECT_SOURCE_DIR}/src)
    target_compile_definitions(HardwareIdsTests PRIVATE UNICODE _UNICODE WIN32_LEAN_AND_MEAN NOMINMAX _WIN32_WINNT=0x0A00)

    if(MSVC)
        target_compile_options(HardwareIdsTests PRIVATE /W4 /permissive- /utf-8 /EHsc)
    endif()

    add_test(NAME HardwareIdsTests COMMAND HardwareIdsTests)
endif()

#
# Installation, with a CMake package: find_package(HardwareIds) then link HardwareIds::HardwareIds or HardwareIds::Shared.
#

if(NOT HARDWAREIDS_INSTALL)
    return()
endif()

include(GNUInstallDirs)
include(CMakePackageConfigHelpers)

set(HARDWAREIDS_INSTALL_TARGETS HardwareIds)

if(HARDWAREIDS_BUILD_SHARED)
    list(APPEND HARDWAREIDS_INSTALL_TARGETS HardwareIdsShared)
endif()

if(HARDWAREIDS_BUILD_TOOLS)
    list(APPEND HARDWAREIDS_INSTALL_TARGETS HardwareIdsSnapshot)
endif()

install(TARGETS ${HARDWAREIDS_INSTALL_TARGETS}
    EXPORT HardwareIdsTargets
    ARCHIVE DESTINATION ${CMAKE_INSTALL_LIBDIR}
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR})

install(DIRECTORY include/ DESTINATION ${CMAKE_INSTALL_INCLUDEDIR})

install(EXPORT HardwareIdsTargets
    NAMESPACE HardwareIds::
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/HardwareIds)

configure_package_config_file(cmake/HardwareIdsConfig.cmake.in
    ${PROJECT_BINARY_DIR}/HardwareIdsConfig.cmake
    INSTALL_DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/HardwareIds)

write_basic_package_version_file(${PROJECT_BINARY_DIR}/HardwareIdsConfigVersion.cmake COMPATIBILITY SameMajorVersion)

install(FILES
    ${PROJECT_BINARY_DIR}/HardwareIdsConfig.cmake
    ${PROJECT_BINARY_DIR}/HardwareIdsConfigVersion.cmake
    DESTINATION ${CMAKE_INSTALL_LIBDIR}/cmake/HardwareIds)
