using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Internal;

using FFXIVClientStructs;

namespace Dalamud.Interface.Utility;

public unsafe partial class ImBlur
{

    public static bool Setup()
    {
        var im = Service<InterfaceManager>.Get();
        var ctx = ImGui.CreateContext();
        ImGui.SetCurrentContext(ctx);
        var ret = BlurSetup(im.Backend.GetDevice(), im.Backend.GetDeviceContext());
        ImGui.DestroyContext();
        return ret;
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "setup")]
    internal unsafe static partial bool BlurSetup(void* device, void* context);

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "process",
        StringMarshalling = StringMarshalling.Utf16)]
    public unsafe static partial void Process(ImDrawList* draw_list, ImGuiIO* io, int iterations, float offset, float noise, float scale);

    [LibraryImport(
        "ImGui-Blur.dll",
        EntryPoint = "blur_render",
        StringMarshalling = StringMarshalling.Utf16)]
    public unsafe static partial void Render(ImDrawList* draw_list, ImGuiIO* io, Vector2* min,  Vector2* max, Int32 col, float rounding = 0.0f, ImDrawFlags draw_flags = 0);

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
}
