using System.Globalization;
using System.Windows;
using System.Windows.Media;

namespace WobblyPet;

sealed class GuardScene
{
    public readonly GuardGame Game = new();
    public readonly GuardArtwork Art = new();
    static string N(double x) => x.ToString("0.###", CultureInfo.InvariantCulture);
    static double Ease(double t) { t = Math.Clamp(t, 0, 1); return t * t * (3 - 2 * t); }
    public void Draw(DrawingContext dc, double petOpacity = 1)
    {
        foreach (var a in Game.Actors.Where(a => a.Position.Y < 425).OrderBy(a => a.Position.Y)) DrawActor(dc, a);
        dc.PushOpacity(petOpacity); DrawPet(dc); dc.Pop();
        foreach (var job in Game.Jobs.Where(j => j.Status == "carry")) DrawCargo(dc, job);
        foreach (var a in Game.Actors.Where(a => a.Position.Y >= 425).OrderBy(a => a.Position.Y)) DrawActor(dc, a);
        foreach (var d in Game.Debris)
        {
            dc.PushTransform(new TranslateTransform(d.Position.X, d.Position.Y));
            dc.PushTransform(new ScaleTransform(d.Direction * .35, .35));
            dc.PushTransform(new TranslateTransform(-1300, -1200));
            dc.DrawDrawing(Art.Markup(Art.MinionFrame(d.Type, "crushed", d.Age)));
            dc.Pop(); dc.Pop(); dc.Pop();
        }
        foreach (var r in Game.Returns)
        {
            var b = Game.Parts[r.Part].Box; var q = Game.Collapsed ? Game.Scatter[r.Part] : null;
            double t = 1 - Math.Pow(1 - Math.Clamp(r.Age / r.Duration, 0, 1), 3);
            var target = q == null ? new Point(600 + (b[0] + b[2] / 2 - 206) * .72, 440 + (b[1] + b[3] / 2 - 398) * .72-Game.GroundLift) : new Point(600 + q[0], 440 + q[1]-Game.GroundLift);
            DrawPart(dc, r.Part, r.Position.X + (target.X - r.Position.X) * t, r.Position.Y + (target.Y - r.Position.Y) * t - Math.Sin(t * Math.PI) * 120, .35 + t * (.72 - .35), (1 - t) * 230 + (q?[2] ?? 0) * t);
        }
        if (Game.State is "playing" or "ending")
        { DrawFoot(dc, Game.LeftFoot, true); DrawFoot(dc, Game.RightFoot, false); }
        foreach (var r in Game.Rings)
        {
            double t = r.Age / .48;
            var shape = new StreamGeometry(); using (var c = shape.Open())
            {
                Point At(int i) { double a = i / 32d * Math.PI * 2; return new(r.Position.X + Math.Cos(a) * (i % 2 == 1 ? 68 : 103) * (1 + t * .8), r.Position.Y + Math.Sin(a) * (i % 2 == 1 ? 22 : 39) * (1 + t * .8)); }
                c.BeginFigure(At(0), false, true); for (int i = 1; i < 32; i++) c.LineTo(At(i), true, false);
            }
            dc.PushOpacity(Math.Pow(1 - t, 2)); dc.DrawGeometry(null, new Pen(r.Left ? Brushes.MediumSlateBlue : Brushes.SandyBrown, 2.8), shape); dc.Pop();
        }
        if (Game.State == "result")
        {
            double age = Game.ReactionAge;
            for (int i = 0; i < 3; i++)
            {
                dc.PushOpacity(Math.Clamp(1 - age / 2.5, 0, 1)); dc.PushTransform(new TranslateTransform(535 + i * 50, 170 - age * 28 - i * 10)); dc.PushTransform(new ScaleTransform(.42, .42));
                dc.DrawGeometry(Brushes.PaleVioletRed, null, Geometry.Parse("M0 4C-16-9-23 8 0 24C23 8 16-9 0 4")); dc.Pop(); dc.Pop(); dc.Pop();
            }
        }
    }
    void DrawPet(DrawingContext dc)
    {
        var g = Game; var job = g.Cursor < g.Jobs.Length ? g.Jobs[g.Cursor] : null;
        int stage = g.Missing.Count == 0 && job?.Status != "pull" ? 0 : g.Cursor;
        string frame;
        if (g.State is "result" or "complete" || g.EndRestore) frame = Art.PetFrame("idle", 0, g.Clock, true);
        else if (!g.Collapsed && stage >= 4) frame = Art.PetFrame("idle", 3, g.Clock, true);
        else if (g.Reaction == "detach" && g.ReactionAge < (g.ReactionPart == 3 ? 3 : 32d / 24)) frame = Art.PetFrame("after", g.ReactionPart, g.ReactionAge, false);
        else if (job?.Status == "pull") frame = Art.PetFrame(job.Pull < .65 ? "prepare" : "pull", job.Part, job.Pull < .65 ? job.Pull / .65 * 20 / 24 : (job.Pull - .65) * 20 / 24, false);
        else frame = Art.PetFrame("idle", stage, g.Clock, true);
        var hidden = new HashSet<int>(g.Missing.Concat(g.Returns.Where(r => r.Age < r.Duration * .98).Select(r => r.Part)));
        string mask = string.Join(' ', hidden.Select(i => "p" + i)) + (g.BodyIndices.Any(hidden.Contains) ? " body-whole" : " assembled-body-pieces");
        dc.PushTransform(new TranslateTransform(600 - 281 * .72, 440 - 513 * .72-g.GroundLift)); dc.PushTransform(new ScaleTransform(.72, .72));
        if (g.State == "result") dc.PushTransform(new RotateTransform(Math.Sin(Math.Min(1, g.ReactionAge / 1.2) * Math.PI) * -10, 240, 480));
        dc.DrawDrawing(Art.Markup(frame, mask)); if (g.State == "result") dc.Pop(); dc.Pop(); dc.Pop();
    }
    void DrawActor(DrawingContext dc, GuardActor a)
    {
        if (a.Dead) return;
        var job = a.Job >= 0 ? Game.Jobs[a.Job] : null; var part = job == null ? null : Game.Parts[job.Part];
        double y = a.Position.Y, x = a.Position.X;
        if (part?.Style == "stack" && job!.Members.IndexOf(a.Id) == 1 && a.Mode != "walk") y -= 46;
        if (job?.Status == "pull") { double q = job.Pull / 1.65; x += Math.Sin(q * Math.PI * 4) * 4; if (part?.Style is "stack" or "overhead") y -= Math.Sin(q * Math.PI) * 18; }
        dc.PushTransform(new TranslateTransform(x, y)); dc.PushTransform(new ScaleTransform(a.Scale * a.Face, a.Scale)); dc.PushTransform(new TranslateTransform(-115, -225));
        // Waiting minions stand on both feet. Walking and cargo use the same redcowork gait.
        double phase = a.Mode == "wait" ? 0 : Game.Clock * 9 / (2 * Math.PI) + a.Phase;
        dc.DrawDrawing(Art.Markup(Art.MinionFrame(a.Type, a.Mode == "carry" ? "carry" : "walk", phase)));
        dc.Pop(); dc.Pop(); dc.Pop();
    }
    void DrawCargo(DrawingContext dc, GuardJob job)
    {
        var members = Game.Actors.Where(a => job.Members.Contains(a.Id) && !a.Dead).ToArray(); if (members.Length == 0) return;
        var p = Game.Parts[job.Part]; double x = members.Average(a => a.Position.X), y = members.Average(a => a.Position.Y) - 60, scale = .52, angle = 0;
        switch (p.Style)
        {
            case "stack": y -= 48; scale = .47; break;
            case "chest": y += 25; scale = .7; break;
            case "wide": y -= 7; angle = 80; break;
            case "body": y -= 48; scale = .5; angle = 80; break;
            case "drag": y += 38; x -= members[0].Side * 33; angle = 82; scale = .57; break;
        }
        DrawPart(dc, job.Part, x, y + Math.Sin(Game.Clock * Math.PI * 4) * 2, scale, angle + Math.Sin(Game.Clock * Math.PI * 2) * 3, p.Chunk >= 0 ? Art.CarryBody(p.Chunk, Game.Clock) : null);
    }
    void DrawPart(DrawingContext dc, int i, double x, double y, double scale, double angle, string? shape = null)
    {
        var part = Game.Parts[i]; var b = part.Box;
        dc.PushTransform(new TranslateTransform(x, y)); dc.PushTransform(new RotateTransform(angle)); dc.PushTransform(new ScaleTransform(scale, scale)); dc.PushTransform(new TranslateTransform(-b[0] - b[2] / 2, -b[1] - b[3] / 2));
        dc.DrawDrawing(Art.Markup(shape ?? Art.PartMarkup(part))); dc.Pop(); dc.Pop(); dc.Pop(); dc.Pop();
    }
    static void DrawFoot(DrawingContext dc, GuardFoot f, bool left)
    {
        SlipperArtwork.Shared.Draw(dc,f,left);
    }
}
