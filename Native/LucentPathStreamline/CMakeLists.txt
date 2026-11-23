cmake_minimum_required(VERSION 3.20)

project(LucentPathStreamline LANGUAGES CXX)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED ON)
set(CMAKE_WINDOWS_EXPORT_ALL_SYMBOLS OFF)

set(STREAMLINE_SDK_ROOT "" CACHE PATH "NVIDIA Streamline SDK root")
set(UNITY_EDITOR_ROOT "" CACHE PATH "Unity Editor root used for PluginAPI headers")
set(LUCENTPATH_NATIVE_OUTPUT "${CMAKE_SOURCE_DIR}/../../Assets/LucentPath/Plugins/x86_64" CACHE PATH "Unity native plugin output directory")

add_library(LucentPathStreamline SHARED
    include/LucentPathStreamline.h
    src/LucentPathStreamline.cpp
)

target_include_directories(LucentPathStreamline PRIVATE
    include
    "${STREAMLINE_SDK_ROOT}/include"
    "${UNITY_EDITOR_ROOT}/Data/PluginAPI"
)

target_compile_definitions(LucentPathStreamline PRIVATE
    WIN32_LEAN_AND_MEAN
    NOMINMAX
)

target_link_libraries(LucentPathStreamline PRIVATE
    d3d12
    dxgi
    dxguid
    "${STREAMLINE_SDK_ROOT}/lib/x64/sl.interposer.lib"
)

if (MSVC)
    target_compile_options(LucentPathStreamline PRIVATE /W4 /permissive- /EHsc)
endif()

set_target_properties(LucentPathStreamline PROPERTIES
    OUTPUT_NAME "LucentPathStreamline"
    RUNTIME_OUTPUT_DIRECTORY "${LUCENTPATH_NATIVE_OUTPUT}"
    RUNTIME_OUTPUT_DIRECTORY_DEBUG "${LUCENTPATH_NATIVE_OUTPUT}"
    RUNTIME_OUTPUT_DIRECTORY_RELEASE "${LUCENTPATH_NATIVE_OUTPUT}"
)
