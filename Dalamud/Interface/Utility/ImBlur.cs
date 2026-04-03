using System.Numerics;
using System.Runtime.InteropServices;

using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Internal;

using FFXIVClientStructs;

using TerraFX.Interop.DirectX;

namespace Dalamud.Interface.Utility;

internal unsafe class Framebuffer : IDisposable
{
    public ID3D11Texture2D* tex = null;
    public ID3D11RenderTargetView* rtv = null;
    public ID3D11ShaderResourceView* srv = null;
    public int width = 0, height = 0;

    public void Dispose()
    {
        if (tex != null)
            tex->Release(); this.tex=null;
        if (rtv != null)
            rtv->Release(); this.rtv=null;
        if (srv != null)
            srv->Release(); this.srv=null;
    }
}

public unsafe partial class ImBlur
{


    /*public unsafe static void Process(ImDrawList* draw_list, int iterations, float offset, float noise, float scale)
    {

    }*/

    internal unsafe static bool CreateFramebuffer(
        ID3D11Device* device, ref Framebuffer framebuffer, int width, int height)
    {
        D3D11_TEXTURE2D_DESC tex_desc = default(D3D11_TEXTURE2D_DESC);
        tex_desc.Width = (uint)width;
        tex_desc.Height = (uint)height;
        tex_desc.MipLevels = 1;
        tex_desc.ArraySize = 1;
        tex_desc.Format = DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM;
        tex_desc.SampleDesc.Count = 1;
        tex_desc.Usage = D3D11_USAGE.D3D11_USAGE_DEFAULT;
        tex_desc.BindFlags = (uint)(D3D11_BIND_FLAG.D3D11_BIND_RENDER_TARGET | D3D11_BIND_FLAG.D3D11_BIND_SHADER_RESOURCE);

        framebuffer.width = width;
        framebuffer.height = height;
        var tex = framebuffer.tex;
        if (device->CreateTexture2D(&tex_desc, null, &tex).FAILED)
            return false;
        var rtv = framebuffer.rtv;
        if (device->CreateRenderTargetView((ID3D11Resource*)framebuffer.tex, null, &rtv).FAILED)
            return false;

        var srv = framebuffer.srv;
        return device->CreateShaderResourceView((ID3D11Resource*)framebuffer.tex, null, &srv).SUCCEEDED;
    }


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
