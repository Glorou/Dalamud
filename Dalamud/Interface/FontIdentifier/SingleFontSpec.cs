using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using System.Text;

using Dalamud.Bindings.ImGui;

using Dalamud.Interface.Utility;

using Newtonsoft.Json;

namespace Dalamud.Interface.FontIdentifier;

/// <summary>
/// Represents a user's choice of a single font.
/// </summary>
[SuppressMessage(
    "StyleCop.CSharp.OrderingRules",
    "SA1206:Declaration keywords should follow order",
    Justification = "public required")]
public record SingleFontSpec : IFontSpec
{
    /// <summary>
    /// Gets the font id.
    /// </summary>
    [JsonProperty]
    public required IFontId FontId { get; init; }

    /// <inheritdoc/>
    [JsonProperty]
    public float SizePx { get; init; } = 16;

    /// <inheritdoc/>
    [JsonIgnore]
    public float SizePt
    {
        get => (this.SizePx * 3) / 4;
        init => this.SizePx = (value * 4) / 3;
    }

    /// <inheritdoc/>
    [JsonIgnore]
    public float LineHeightPx => MathF.Round(this.SizePx * this.LineHeight);

    /// <summary>
    /// Gets the line height ratio to the font size.
    /// </summary>
    [JsonProperty]
    public float LineHeight { get; init; } = 1f;

    /// <summary>
    /// Gets the glyph offset in pixels.
    /// </summary>
    [JsonProperty]
    public Vector2 GlyphOffset { get; init; }

    /// <summary>
    /// Gets the letter spacing in pixels.
    /// </summary>
    [JsonProperty]
    public float LetterSpacing { get; init; }

    /// <summary>
    /// Gets the glyph ranges.
    /// </summary>
    [JsonProperty]
    public ushort[]? GlyphRanges { get; init; }

    /// <inheritdoc/>
    [JsonProperty]
    public int FontNo { get; init; }

    /// <inheritdoc/>
    public string ToLocalizedString(string localeCode)
    {
        var sb = new StringBuilder();
        sb.Append(this.FontId.Family.GetLocalizedName(localeCode));
        sb.Append($"({this.FontId.GetLocalizedName(localeCode)}, {this.SizePt}pt");
        if (Math.Abs(this.LineHeight - 1f) > 0.000001f)
            sb.Append($", LH={this.LineHeight:0.##}");
        if (this.GlyphOffset != default)
            sb.Append($", O={this.GlyphOffset.X:0.##},{this.GlyphOffset.Y:0.##}");
        if (this.LetterSpacing != 0f)
            sb.Append($", LS={this.LetterSpacing:0.##}");
        sb.Append(')');
        return sb.ToString();
    }

    /// <inheritdoc/>
    public override string ToString() => this.ToLocalizedString("en");


}
