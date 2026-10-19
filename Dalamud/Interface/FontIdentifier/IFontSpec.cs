using Dalamud.Bindings.ImGui;


namespace Dalamud.Interface.FontIdentifier;

/// <summary>
/// Represents a user's choice of font(s).<br />
/// Not intended for plugins to implement.
/// </summary>
public interface IFontSpec
{
    /// <summary>
    /// Gets the font size in pixels.
    /// </summary>
    float SizePx { get; }

    /// <summary>
    /// Gets the font size in points.
    /// </summary>
    float SizePt { get; }

    /// <summary>
    /// Gets the line height in pixels.
    /// </summary>
    float LineHeightPx { get; }

    /// <summary>
    /// Gets the font face index within a TrueType Collection (TTC) file.
    /// </summary>
    /// <remarks>
    /// This property only applies to <see cref="DalamudAsset.NotoSansCjkRegular"/> and
    /// <see cref="DalamudAsset.NotoSansCjkMedium"/>, which are TTC fonts bundling
    /// multiple language-specific CJK glyph sets (Japanese, Traditional Chinese,
    /// Simplified Chinese, Korean) into a single file.
    ///
    /// The index corresponds to the font face order in the TTC:
    /// <list type="bullet">
    ///   <item><description>0 = Japanese</description></item>
    ///   <item><description>1 = Traditional Chinese</description></item>
    ///   <item><description>2 = Simplified Chinese</description></item>
    ///   <item><description>3 = Korean</description></item>
    /// </list>
    ///
    /// This value is ignored for all other <see cref="DalamudAsset"/> entries.
    /// Only one glyph set can be active at a time. In most cases, you can omit this—
    /// Dalamud automatically selects the appropriate face based on the UI language.
    /// </remarks>
    int FontNo { get; }


    /// <summary>
    /// Represents this font specification, preferrably in the requested locale.
    /// </summary>
    /// <param name="localeCode">The locale code. Must be in lowercase(invariant).</param>
    /// <returns>The value.</returns>
    string ToLocalizedString(string localeCode);
}
