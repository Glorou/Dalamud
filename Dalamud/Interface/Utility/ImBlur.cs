using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Internal;

using FFXIVClientStructs;

using TerraFX.Interop.DirectX;

namespace Dalamud.Interface.Utility;


public unsafe partial class ImBlur
{


    public static ImTextureID Texture() => new ImTextureID(GetTexture());



    public static bool Setup()
    {
        var im = Service<InterfaceManager>.Get();
        var ret = BlurSetup(im.Backend.GetDevice(), im.Backend.GetDeviceContext());
        return ret;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "setup")]
    internal unsafe static partial bool BlurSetup(void* device, void* context);

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "process")]
    public unsafe static partial bool Process(ImDrawList* draw_list, int iterations, float offset, float noise, float scale);

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "render",
        StringMarshalling = StringMarshalling.Utf16)]
    public unsafe static partial void Render(ImDrawList* draw_list, Vector2* min,  Vector2* max, Int32 col, float rounding = 0.0f, ImDrawFlags draw_flags = 0);

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "garbage_collect",
        StringMarshalling = StringMarshalling.Utf16)]
    public unsafe static partial void GC();

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "destroy",
        StringMarshalling = StringMarshalling.Utf16)]
    public unsafe static partial void Destroy();

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "get_texture",
        StringMarshalling = StringMarshalling.Utf16)]
    internal unsafe static partial void* GetTexture();
}
