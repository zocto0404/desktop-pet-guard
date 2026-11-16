using System.IO;
using System.Text.Json;
using System.Windows;

namespace WobblyPet;

sealed record GuardPart(string Name, int[] Paths, int People, double[] Box, string Style, string? Symbol, int Chunk, double NotBefore);
sealed class GuardActor
{
    public int Id, Type, Job = -1, Side, Face = 1;
    public Point Position;
    public double Speed, Phase, Scale = .35, DeathAge;
    public string Mode = "walk";
    public bool Dead;
}
sealed class GuardJob(int part)
{
    public int Part = part;
    public List<int> Members = new();
    public string Status = "waiting";
    public double Pull;
}
sealed class GuardFoot(double x)
{
    public double X = x, Y = 450, Age = 9, ReleaseAt, Cooldown;
    public bool Down, Pending;
}
sealed record GuardDebris(int Type, Point Position, int Direction) { public double Age; }
sealed record GuardReturn(int Part, Point Position, double Duration) { public double Age; }
sealed record GuardRing(Point Position, bool Left) { public double Age; }

// Native port of desktop-pet-guard's input-independent 60-second simulation.
// The countdown supplies all 60 living actors; none are replaced or respawned.
sealed class GuardGame
{
    public readonly GuardPart[] Parts;
    public readonly double[][] Scatter;
    public readonly int[] BodyIndices;
    public readonly GuardJob[] Jobs;
    public readonly List<GuardActor> Actors = new();
    public readonly List<GuardDebris> Debris = new();
    public readonly List<GuardReturn> Returns = new();
    public readonly List<GuardRing> Rings = new();
    public readonly HashSet<int> Missing = new();
    public readonly GuardFoot LeftFoot = new(400), RightFoot = new(800);
    public string State = "playing", Reaction = "idle";
    public double Clock, Elapsed, EndTime, ReactionAge = 99;
    public int Cursor, ReactionPart = -1, Hits, Rescued;
    public bool Collapsed, Paused, EndRestore;
    public Rect Viewport = new(0,0,1200,720);
    public double GroundLift;
    public const double Duration = 60, DebrisDuration = 4;
    Queue<int> restore = new(); double restoreIn;
    public bool Complete => State == "complete";
    public GuardGame()
    {
        using var stream = Application.GetResourceStream(new Uri("Assets/guard/parts.json", UriKind.Relative))!.Stream;
        using var data = JsonDocument.Parse(stream);
        Parts = data.RootElement.GetProperty("parts").EnumerateArray().Select(p => new GuardPart(
            p.GetProperty("name").GetString()!, p.GetProperty("paths").EnumerateArray().Select(n => n.GetInt32()).ToArray(),
            p.GetProperty("people").GetInt32(), p.GetProperty("box").EnumerateArray().Select(n => n.GetDouble()).ToArray(), p.GetProperty("style").GetString()!,
            p.TryGetProperty("symbol", out var symbol) ? symbol.GetString() : null,
            p.TryGetProperty("chunk", out var chunk) ? chunk.GetInt32() : -1,
            p.TryGetProperty("notBefore", out var time) ? time.GetDouble() : 0)).ToArray();
        Scatter = data.RootElement.GetProperty("scatter").EnumerateArray().Select(p => new[] { p.GetProperty("x").GetDouble(), p.GetProperty("y").GetDouble(), p.GetProperty("angle").GetDouble(), p.TryGetProperty("scale", out var s) ? s.GetDouble() : 1, p.TryGetProperty("flatten", out var f) ? f.GetDouble() : 1 }).ToArray();
        BodyIndices = data.RootElement.GetProperty("bodyIndices").EnumerateArray().Select(n => n.GetInt32()).ToArray();
        Jobs = Enumerable.Range(0, Parts.Length).Select(i => new GuardJob(i)).ToArray();
    }
    public void Add(GuardActor actor)
    {
        var job = Jobs.FirstOrDefault(j => j.Status == "waiting" && j.Members.Count < Parts[j.Part].People);
        actor.Job = job?.Part ?? -1; job?.Members.Add(actor.Id); Actors.Add(actor);
    }
    public Point Target(GuardActor a)
    {
        Point Fit(double x,double y)=>new(Math.Clamp(x,Viewport.Left+35,Viewport.Right-35),Math.Min(y,Viewport.Bottom-8));
        if (a.Job < 0)
        {
            // Extra countdown members visibly encircle the pet and take over interrupted jobs.
            double angle = a.Id * 2.399963;
            return Fit(600 + Math.Cos(angle) * (155 + a.Id % 3 * 20), 434 + Math.Sin(angle) * 58-GroundLift);
        }
        var job = Jobs[a.Job]; var p = Parts[a.Job]; int i = job.Members.IndexOf(a.Id);
        if (a.Job > Cursor + 1) return Fit(600 + a.Side * (160 + a.Id % 4 * 36), 465 + a.Id % 3 * 10-GroundLift);
        if (Collapsed) return Fit(600 + Scatter[a.Job][0] + (i - (p.People - 1) / 2d) * 42, 450 + Scatter[a.Job][1] + i % 2 * 10-GroundLift);
        if (p.Style == "wide") return Fit(600 + (a.Job == 5 ? -75 : 63) + (i - .5) * 58, 414 + i * 16);
        if (p.Style == "stack") return Fit(609, 414);
        return Fit(Math.Clamp(600 + (p.Box[0] + p.Box[2] / 2 - 206) * .72, 485, 700), 414 + a.Id % 3 * 12);
    }
    public void Aim(bool left, double x, double y)
    {
        var f = left ? LeftFoot : RightFoot;
        double low=left?38:674,high=left?526:1162;
        double visibleLow=Math.Max(low,Viewport.Left+92),visibleHigh=Math.Min(high,Viewport.Right-92);
        if(visibleLow<=visibleHigh){low=visibleLow;high=visibleHigh;}
        double top=Math.Max(312,Viewport.Top+104),bottom=Math.Min(500,Viewport.Bottom-12);
        f.X = Math.Clamp(x, low, high); f.Y = top<=bottom?Math.Clamp(y,top,bottom):Math.Clamp(y,312,500);
    }
    public void SetViewport(Rect area)
    {
        Viewport=area;Aim(true,LeftFoot.X,LeftFoot.Y);Aim(false,RightFoot.X,RightFoot.Y);
    }
    public void Move(bool left, double x, double y, double dt)
    {
        var f = left ? LeftFoot : RightFoot; Aim(left, f.X + x * 290 * dt, f.Y + y * 190 * dt);
    }
    public void Press(bool left)
    {
        var f = left ? LeftFoot : RightFoot;
        if (Paused || State != "playing" || f.Down || f.Cooldown > 0) return;
        f.Down = true; f.Pending = true; f.Age = f.ReleaseAt = 0; f.Cooldown = .25;
    }
    public void Release(bool left)
    {
        var f = left ? LeftFoot : RightFoot; f.Down = false; f.ReleaseAt = f.Age;
    }
    public void TogglePause()
    {
        Paused = !Paused; Release(true); Release(false); LeftFoot.Pending = RightFoot.Pending = false;
    }
    void Impact(bool left)
    {
        var f = left ? LeftFoot : RightFoot; f.Pending = false;
        Rings.Add(new(new(f.X, f.Y), left));
        if (State != "playing") return;
        foreach (var a in Actors.Where(a => !a.Dead && Math.Sqrt(Math.Pow((a.Position.X - f.X) / 1.2, 2) + Math.Pow(a.Position.Y - f.Y, 2)) < 63).ToArray()) Hit(a);
    }
    public void Hit(GuardActor a)
    {
        if (a.Dead) return;
        a.Dead = true; a.DeathAge = 0; Hits++;
        int direction=a.Position.X<600?-1:1;
        double room=direction<0?a.Position.X-Viewport.Left:Viewport.Right-a.Position.X;
        double opposite=direction<0?Viewport.Right-a.Position.X:a.Position.X-Viewport.Left;
        if(room<475&&opposite>room)direction=-direction;
        Debris.Add(new(a.Type, a.Position, direction));
        if (a.Job < 0) return;
        var j = Jobs[a.Job];
        if (j.Status == "carry")
        {
            Missing.Remove(j.Part); j.Status = "recovered"; Rescued++;
            Returns.Add(new(j.Part, new(a.Position.X, a.Position.Y - 45), 1));
            foreach (var member in Actors.Where(m => j.Members.Contains(m.Id) && !m.Dead)) { member.Job = -1; member.Mode = "retreat"; member.Side = member.Position.X < 600 ? -1 : 1; }
            Reaction = "happy"; ReactionAge = 0;
        }
        else { j.Members.Remove(a.Id); j.Pull = 0; j.Status = "waiting"; }
    }
    void Detach(GuardJob j)
    {
        j.Status = "carry"; j.Pull = 0; Missing.Add(j.Part); Cursor++;
        Collapsed = Missing.Contains(2) && Missing.Contains(3);
        Reaction = "detach"; ReactionAge = 0; ReactionPart = j.Part;
        var members = Actors.Where(a => j.Members.Contains(a.Id) && !a.Dead).ToArray();
        int side = members[0].Side; double speed = members.Min(a => a.Speed);
        foreach (var a in members) { a.Mode = "carry"; a.Side = side; a.Speed = speed; }
    }
    public void BeginEnding()
    {
        if (State != "playing") return;
        State = "ending"; Release(true); Release(false);
        foreach (var a in Actors.Where(a => !a.Dead)) { a.Mode = "retreat"; a.Side = a.Position.X < 600 ? -1 : 1; }
    }
    public void Step(double dt)
    {
        dt = Math.Clamp(dt, 0, .05); if (Paused || Complete) return;
        Clock += dt; ReactionAge += dt;
        double lift=Collapsed?Math.Max(0,490-Viewport.Bottom+8):0;
        GroundLift+=(lift-GroundLift)*(1-Math.Exp(-dt*10));
        foreach (bool left in new[] { true, false })
        {
            var f = left ? LeftFoot : RightFoot; f.Age += dt; f.Cooldown = Math.Max(0, f.Cooldown - dt);
            if (f.Pending && f.Age >= .125) Impact(left);
        }
        foreach (var d in Debris) d.Age += dt; Debris.RemoveAll(d => d.Age >= DebrisDuration);
        foreach (var r in Returns) r.Age += dt; Returns.RemoveAll(r => r.Age >= r.Duration);
        foreach (var r in Rings) r.Age += dt; Rings.RemoveAll(r => r.Age >= .48);
        if (State == "playing")
        {
            Elapsed += dt; if (Elapsed >= Duration) { Elapsed = Duration; BeginEnding(); return; }
            // Surviving members, rather than newly spawned replacements, fill vacant tasks.
            foreach (var a in Actors.Where(a => !a.Dead && a.Job < 0 && a.Mode != "retreat"))
            {
                var vacancy = Jobs.FirstOrDefault(j => j.Part >= Cursor && j.Status == "waiting" && j.Members.Count < Parts[j.Part].People);
                if (vacancy != null) { a.Job = vacancy.Part; vacancy.Members.Add(a.Id); }
            }
            foreach (var a in Actors)
            {
                if (a.Dead) { a.DeathAge += dt; continue; }
                var old = a.Position;
                if (a.Mode is "carry" or "retreat") a.Position = new(a.Position.X + a.Side * a.Speed * (a.Mode == "retreat" ? 1.9 : .72) * dt, a.Position.Y);
                else
                {
                    var target = Target(a); var delta = target - a.Position; double distance = delta.Length;
                    if (distance > 2) { a.Position += delta / distance * Math.Min(distance, a.Speed * dt); a.Mode = "walk"; }
                    else a.Mode = "wait";
                }
                if (Math.Abs(a.Position.X - old.X) > .02) a.Face = a.Position.X < old.X ? -1 : 1;
                a.Scale += (.35 - a.Scale) * (1 - Math.Exp(-dt * 2.8));
            }
            if (Cursor < Jobs.Length)
            {
                var j = Jobs[Cursor]; var members = Actors.Where(a => j.Members.Contains(a.Id) && !a.Dead).ToArray();
                bool ready = members.Length == Parts[j.Part].People && members.All(a => (a.Position - Target(a)).Length < 5);
                if (ready && Elapsed >= Parts[j.Part].NotBefore && !(Reaction == "detach" && ReactionPart == 3 && ReactionAge < 3))
                { j.Status = "pull"; j.Pull += dt; if (j.Pull > 1.65) Detach(j); }
                else if (!ready) { j.Pull = 0; j.Status = "waiting"; }
            }
            foreach (var j in Jobs.Where(j => j.Status == "carry"))
                if (Actors.Where(a => j.Members.Contains(a.Id) && !a.Dead).All(a => a.Position.X < Viewport.Left-85 || a.Position.X > Viewport.Right+85)) j.Status = "escaped";
        }
        else if (State == "ending")
        {
            EndTime += dt;
            foreach (var a in Actors) if (!a.Dead) { a.Position = new(a.Position.X + a.Side * a.Speed * 2 * dt, a.Position.Y); a.Face = a.Side; } else a.DeathAge += dt;
            if (EndTime > 2 && !EndRestore) { EndRestore = true; Collapsed = false; restore = new(Missing.Reverse()); }
            if (EndRestore)
            {
                restoreIn -= dt;
                if (restoreIn <= 0 && restore.TryDequeue(out int part))
                { Missing.Remove(part); Returns.Add(new(part, new(part % 2 == 0 ? -140 : 1300, 300 + part % 5 * 30), 1.3)); restoreIn = .13; }
            }
            if (EndTime > 6.5) { State = "result"; Missing.Clear(); Actors.Clear(); Reaction = "happy"; ReactionAge = 0; }
        }
        else if (State == "result" && ReactionAge > 2.5) State = "complete";
        Actors.RemoveAll(a => a.Dead && a.DeathAge > .14 || a.Mode is "carry" or "retreat" && (a.Position.X < Viewport.Left-160 || a.Position.X > Viewport.Right+160));
    }
}
