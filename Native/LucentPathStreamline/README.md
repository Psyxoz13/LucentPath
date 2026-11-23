# Streamline bridge for Unity

Build on Windows with the NVIDIA Streamline SDK and Unity Editor plugin headers.

    cmake -S Native/LucentPathStreamline -B NativeBuild -A x64 -DSTREAMLINE_SDK_ROOT=C:/SDK/Streamline -DUNITY_EDITOR_ROOT=C:/Unity/Editor
    cmake --build NativeBuild --config Release

The library is placed in Assets/LucentPath/Plugins/x86_64. Install the required NVIDIA runtime DLLs separately from the Streamline SDK.
