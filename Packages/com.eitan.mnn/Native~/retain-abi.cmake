# Retain official inline APIs used by the pinned managed ABI in two existing
# translation units. All compute kernels and the remaining runtime keep -O3
# inlining. No source, function body or custom export is generated.
set_property(SOURCE
    "${CMAKE_CURRENT_SOURCE_DIR}/source/core/Tensor.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/transformers/llm/engine/src/omni.cpp"
    APPEND PROPERTY COMPILE_OPTIONS -fno-inline-functions)
