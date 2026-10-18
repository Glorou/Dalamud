using System.Diagnostics.CodeAnalysis;
using System.Numerics;


namespace Dalamud.Bindings.ImGui;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static unsafe partial class ImGui
{

    public static void Image(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector2 uv1)
    {
        ImGuiNative.Image(new(texId: texID), imageSize, uv0, uv1);
    }
    public static void Image(ImTextureID texID, Vector2 imageSize, Vector2 uv0)
    {
        ImGuiNative.Image(new(texId: texID), imageSize, uv0, (Vector2)(new Vector2(1,1)));
    }

    public static void Image(ImTextureID texID, Vector2 imageSize)
    {
        ImGuiNative.Image(new(texId: texID), imageSize, (Vector2)(new Vector2(0,0)), (Vector2)(new Vector2(1,1)));
    }


    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector2 uv1, Vector4 bgCol, Vector4 tintCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, uv1, bgCol, tintCol);
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector2 uv1, Vector4 bgCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, uv1, bgCol, (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector2 uv1)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, uv1, (Vector4)(new Vector4(0,0,0,0)), (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, (Vector2)(new Vector2(1,1)), (Vector4)(new Vector4(0,0,0,0)), (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, (Vector2)(new Vector2(0,0)), (Vector2)(new Vector2(1,1)), (Vector4)(new Vector4(0,0,0,0)), (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector4 bgCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, (Vector2)(new Vector2(1,1)), bgCol, (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector4 bgCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, (Vector2)(new Vector2(0,0)), (Vector2)(new Vector2(1,1)), bgCol, (Vector4)(new Vector4(1,1,1,1)));
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector2 uv0, Vector4 bgCol, Vector4 tintCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, uv0, (Vector2)(new Vector2(1,1)), bgCol, tintCol);
    }
    public static void ImageWithBg(ImTextureID texID, Vector2 imageSize, Vector4 bgCol, Vector4 tintCol)
    {
    	ImGuiNative.ImageWithBg(new(texId: texID), imageSize, (Vector2)(new Vector2(0,0)), (Vector2)(new Vector2(1,1)), bgCol, tintCol);
    }


}
