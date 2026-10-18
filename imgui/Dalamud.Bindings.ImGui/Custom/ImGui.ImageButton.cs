

using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace Dalamud.Bindings.ImGui;

[SuppressMessage("ReSharper", "InconsistentNaming")]
public static unsafe partial class ImGui
{
    public static byte ImageButton(
        ImU8String strId, ImTextureRef texRef, Vector2 imageSize, Vector2 uv0, Vector2 uv1, Vector4 bgCol, Vector4 tintCol)
    {
        fixed (byte* strIdPtr = &strId.GetPinnableNullTerminatedReference())
        {
            var r = ImGuiNative.ImageButton(strIdPtr, texRef, imageSize, uv0, uv1, bgCol, tintCol);;
            strId.Recycle();
            return r;
        }
    }


    public static byte ImageButton(
        ImU8String strId, ImTextureRef texRef, Vector2 imageSize, Vector2 uv0, Vector2 uv1, Vector4 bgCol)
    {
        fixed (byte* strIdPtr = &strId.GetPinnableNullTerminatedReference())
        {
            var r = ImGuiNative.ImageButton(strIdPtr, texRef, imageSize, uv0, uv1, bgCol,(new Vector4(1,1,1,1)));;
            strId.Recycle();
            return r;
        }
    }

    public static bool ImageButton(ImU8String strId, ImTextureID userTextureId, Vector2 size)
    {
        fixed (byte* strIdPtr = strId)
        {
            byte ret = ImGuiNative.ImageButton(
                strIdPtr,
                new(texId:userTextureId),
                size,
                (Vector2)(new Vector2(0, 0)),
                (Vector2)(new Vector2(1, 1)),
                (Vector4)(new Vector4(0, 0, 0, 0)),
                (Vector4)(new Vector4(1, 1, 1, 1)));
            return ret != 0;
        }
    }
}
