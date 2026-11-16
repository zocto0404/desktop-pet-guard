using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Xml.Linq;

namespace WobblyPet;

// Plays the imported, pre-recorded SVG poses through WPF's transparent renderer.
// Geometry references are shared; the bounded pose cache avoids retaining a whole movie.
sealed class GuardArtwork
{
    readonly JsonDocument atlas;
    readonly Dictionary<string, XElement> symbols;
    readonly Dictionary<XElement, GeometryDrawing> geometry = new();
    readonly Dictionary<string, DrawingGroup> cache = new();
    readonly Queue<string> order = new();
    public GuardArtwork()
    {
        using var stream = Application.GetResourceStream(new Uri("Assets/guard/sprites.json", UriKind.Relative))!.Stream;
        atlas = JsonDocument.Parse(stream);
        symbols = XElement.Parse("<root>" + atlas.RootElement.GetProperty("defs").GetString() + "</root>")
            .Descendants().Where(e => e.Attribute("id") != null).ToDictionary(e => (string)e.Attribute("id")!);
    }
    static double Number(string? value, double fallback = 0) => value == null ? fallback : double.Parse(value, CultureInfo.InvariantCulture);
    static Brush? Brush(string? value) => value == null || value == "none" ? null : (Brush)new BrushConverter().ConvertFromString(value)!;
    static readonly Regex TransformPattern = new(@"([a-zA-Z]+)\(([^)]*)\)", RegexOptions.Compiled);
    static readonly Regex NumberPattern = new(@"[-+]?(?:\d*\.\d+|\d+\.?\d*)(?:[eE][-+]?\d+)?", RegexOptions.Compiled);
    static TransformGroup Transforms(string source)
    {
        var group = new TransformGroup();
        // SVG applies the rightmost transform first. WPF's TransformGroup runs left to right.
        foreach (Match m in TransformPattern.Matches(source).Cast<Match>().Reverse())
        {
            var n = NumberPattern.Matches(m.Groups[2].Value).Select(x => Number(x.Value)).ToArray();
            Transform t = m.Groups[1].Value switch
            {
                "translate" => new TranslateTransform(n[0], n.Length > 1 ? n[1] : 0),
                "scale" => new ScaleTransform(n[0], n.Length > 1 ? n[1] : n[0]),
                "rotate" => new RotateTransform(n[0], n.Length > 2 ? n[1] : 0, n.Length > 2 ? n[2] : 0),
                "skewX" => new SkewTransform(n[0], 0),
                "skewY" => new SkewTransform(0, n[0]),
                "matrix" => new MatrixTransform(n[0], n[1], n[2], n[3], n[4], n[5]),
                _ => throw new InvalidDataException("Unsupported SVG transform: " + m.Groups[1].Value)
            };
            group.Children.Add(t);
        }
        return group;
    }
    void DrawNode(DrawingContext dc, XElement node, HashSet<string> hidden)
    {
        if (((string?)node.Attribute("class") ?? "").Split(' ').Any(hidden.Contains)) return;
        bool transform = node.Attribute("transform") is { Value.Length: > 0 };
        bool opacity = node.Attribute("opacity") != null;
        if (transform) dc.PushTransform(Transforms((string)node.Attribute("transform")!));
        if (opacity) dc.PushOpacity(Math.Clamp(Number((string?)node.Attribute("opacity"), 1), 0, 1));
        switch (node.Name.LocalName)
        {
            case "g": case "root":
                foreach (var child in node.Elements()) DrawNode(dc, child, hidden);
                break;
            case "use":
                var id = ((string?)node.Attribute("href"))?.TrimStart('#');
                if (id == null || !symbols.TryGetValue(id, out var symbol)) throw new InvalidDataException("Missing SVG symbol: " + id);
                DrawNode(dc, symbol, hidden);
                break;
            case "path":
                if (!geometry.TryGetValue(node, out var drawing))
                {
                    var shape = Geometry.Parse((string)node.Attribute("d")!);
                    var stroke = Brush((string?)node.Attribute("stroke"));
                    Pen? pen = stroke == null ? null : new Pen(stroke, Number((string?)node.Attribute("stroke-width"), 1))
                    { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round, LineJoin = PenLineJoin.Round };
                    drawing = new GeometryDrawing(Brush((string?)node.Attribute("fill")), pen, shape);
                    drawing.Freeze();
                    // Only retain reusable definition geometry, not thousands of per-frame liquid outlines.
                    if (node.Ancestors().Any(e => e.Attribute("id") != null)) geometry[node] = drawing;
                }
                dc.DrawDrawing(drawing);
                break;
            default: throw new InvalidDataException("Unsupported SVG element: " + node.Name);
        }
        if (opacity) dc.Pop();
        if (transform) dc.Pop();
    }
    public DrawingGroup Markup(string markup, string mask = "")
    {
        string key = mask + "|" + markup;
        if (cache.TryGetValue(key, out var found)) return found;
        var drawing = new DrawingGroup();
        using (var dc = drawing.Open()) DrawNode(dc, XElement.Parse("<root>" + markup + "</root>"), mask.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToHashSet());
        drawing.Freeze(); cache[key] = drawing; order.Enqueue(key);
        while (order.Count > 300) cache.Remove(order.Dequeue());
        return drawing;
    }
    public string PetFrame(string phase, int stage, double seconds, bool loop)
    {
        var frames = atlas.RootElement.GetProperty("pet").GetProperty(phase)[Math.Clamp(stage, 0, phase == "idle" ? 19 : 18)];
        int index = (int)Math.Max(0, Math.Floor(seconds * 24));
        return frames[loop ? index % frames.GetArrayLength() : Math.Min(frames.GetArrayLength() - 1, index)].GetString()!;
    }
    public string MinionFrame(int type, string phase, double seconds)
    {
        var frames = atlas.RootElement.GetProperty("minions")[type].GetProperty(phase);
        int index = (int)Math.Max(0, Math.Floor(seconds * 24));
        return frames[phase == "crushed" ? Math.Min(frames.GetArrayLength() - 1, index) : index % frames.GetArrayLength()].GetString()!;
    }
    public string PartMarkup(GuardPart part) => part.Symbol != null ? $"<use href=\"#{part.Symbol}\"/>" : string.Concat(part.Paths.Select(n => $"<use href=\"#pet-{n}\"/>"));
    public string CarryBody(int chunk, double seconds)
    {
        var frames = atlas.RootElement.GetProperty("bodyCarry")[chunk];
        return frames[(int)(Math.Max(0, seconds) * 24) % frames.GetArrayLength()].GetString()!;
    }
}
