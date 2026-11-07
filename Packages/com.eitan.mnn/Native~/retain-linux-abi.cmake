# Keep existing official inline definitions required by the pinned GNU ABI.
# No new translation unit, source edit or wrapper is generated.
set_property(SOURCE
    "${CMAKE_CURRENT_SOURCE_DIR}/source/core/Tensor.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/express/Expr.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/transformers/llm/engine/src/llm.cpp"
    "${CMAKE_CURRENT_SOURCE_DIR}/transformers/llm/engine/src/omni.cpp"
    APPEND PROPERTY COMPILE_OPTIONS
    -fkeep-inline-functions -fno-inline-functions
    -fno-visibility-inlines-hidden -fvisibility=default)
