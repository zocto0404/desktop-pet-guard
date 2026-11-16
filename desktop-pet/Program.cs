using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WobblyPet;

enum PetState { Slime, Left, Right, Normal }
record Sprite(BitmapSource Image, double Width, double Ax = .5, double Ay = .5)
{
    public double Height => Width * Image.PixelHeight / Image.PixelWidth;
}
record struct Pose(double X, double Y, double Angle = 0, double Sx = 1, double Sy = 1);

sealed class Rig
{
    public readonly Dictionary<string, Sprite> Sprites = new();
    public readonly Dictionary<string, Pose> Current = new();
    readonly Dictionary<string, (double X, double Y, double Spin)> velocity = new();
    readonly JsonElement motion;
    readonly Random random = new(42);
    readonly Point[] contour = new Point[360];
    public PetState State { get; private set; } = PetState.Normal;
    public double Clock, StateAge, WaveAge = 99;
    double nextWave = 5;
    public Rig()
    {
        motion = JsonDocument.Parse(Application.GetResourceStream(new Uri("Assets/motion.json", UriKind.Relative))!.Stream).RootElement.Clone();
        BitmapSource Load(string n) { var b = new BitmapImage(new Uri("pack://application:,,,/Assets/parts/" + n + ".png")); b.Freeze(); return b; }
        void Add(string id, string asset, double width, double ax = .5, double ay = .5) => Sprites[id] = new(Load(asset), width, ax, ay);
        Add("body", "body", 1); Sprites["body"] = Sprites["body"] with { Width = 238.0 * Sprites["body"].Image.PixelWidth / Sprites["body"].Image.PixelHeight };
        Add("hat", "hat", 174, .48, .79);
        Add("white0", "eye-white", 51); Add("white1", "eye-white", 51);
        Add("pupil0", "pupil", 24); Add("pupil1", "pupil", 24);
        Add("mouth", "mouth", 32); Add("bow", "bowtie", 54);
        Add("arm", "free-arm", 105, 2.0 / Load("free-arm").PixelWidth, .88);
        Add("cup", "cup-arm", 144, 2.0 / Load("cup-arm").PixelWidth, .19);
        Add("kick", "lifted-leg", 105, 2.0 / Load("lifted-leg").PixelWidth, .87);
        var leg = Load("support-leg");
        var upper = new CroppedBitmap(leg, new Int32Rect(0, 0, leg.PixelWidth, (int)Math.Round(leg.PixelHeight * .65)));
        var lower = new CroppedBitmap(leg, new Int32Rect(0, (int)Math.Round(leg.PixelHeight * .26), leg.PixelWidth, leg.PixelHeight - (int)Math.Round(leg.PixelHeight * .26)));
        foreach (var side in new[] { "r", "l" })
        {
            Sprites[side + "upper"] = new(upper, upper.PixelWidth, .78, 1.0 / upper.PixelHeight);
            Sprites[side + "lower"] = new(lower, lower.PixelWidth, .78, 1.0 / lower.PixelHeight);
        }
        var body = Sprites["body"];
        var rgba = new FormatConvertedBitmap(body.Image, PixelFormats.Bgra32, null, 0);
        int stride = rgba.PixelWidth * 4; byte[] pixels = new byte[stride * rgba.PixelHeight]; rgba.CopyPixels(pixels, stride, 0);
        for (int degree=0; degree<360; degree++)
        {
            double dx=Math.Cos(Rad(degree)),dy=Math.Sin(Rad(degree)),last=0;
            for(int t=1;t<400;t++) { int x=(int)Math.Round((body.Width/2+dx*t)/body.Width*rgba.PixelWidth),y=(int)Math.Round((body.Height/2+dy*t)/body.Height*rgba.PixelHeight); if(x<0||x>=rgba.PixelWidth||y<0||y>=rgba.PixelHeight||pixels[y*stride+x*4+3]<128)break;last=t; }
            contour[degree]=new(dx*last,dy*last);
        }
        foreach (var pair in Target()) Current[pair.Key] = pair.Value;
    }
    public static PetState Resolve(bool l, bool r) => (l, r) switch { (true, true) => PetState.Normal, (true, false) => PetState.Left, (false, true) => PetState.Right, _ => PetState.Slime };
    public void SetKeys(bool l, bool r)
    {
        var state = Resolve(l, r);
        if (state == State) return;
        State = state; StateAge = 0; WaveAge = 99; nextWave = Clock + 4 + random.NextDouble() * 7;
        if (State == PetState.Slime)
            foreach (var id in Current.Keys)
            {
                double destination = id switch { "body" => 155, "hat" => 410, "cup" => 530, "white0" => 300, "white1" => 365, "pupil0" => 330, "pupil1" => 435, "bow" => 250, "mouth" => 480, _ => 80+random.NextDouble()*480 };
                velocity[id] = ((destination-Current[id].X)/.58, -90-random.NextDouble()*150, (random.NextDouble()-.5)*340);
            }
    }
    public void Wave() { if (State == PetState.Normal) WaveAge = 0; }
    public void Step(double dt)
    {
        Clock += dt; StateAge += dt; WaveAge += dt;
        if (State == PetState.Normal && Clock >= nextWave) { Wave(); nextWave = Clock + 10 + random.NextDouble() * 12; }
        if (State == PetState.Slime)
        {
            foreach (var id in Current.Keys.ToArray())
            {
                var p = Current[id]; var s = Sprites[id]; var v = velocity[id];
                v.Y += 1100 * dt; p.X += v.X * dt; p.Y += v.Y * dt; p.Angle += v.Spin * dt;
                // All four transformed corners collide with the same ground plane.
                var corners = Corners(s, p); double bottom = corners.Max(q => q.Y), left = corners.Min(q => q.X), right = corners.Max(q => q.X);
                if (bottom > 566) { p.Y -= bottom - 566; v.Y = Math.Abs(v.Y) < 32 ? 0 : -v.Y * .27; v.X *= .79; v.Spin *= .65; }
                if (left < 30) { p.X += 30 - left; v.X = Math.Abs(v.X) * .4; }
                if (right > 610) { p.X -= right - 610; v.X = -Math.Abs(v.X) * .4; }
                if (StateAge > 4) v = (0, 0, 0);
                Current[id] = p; velocity[id] = v;
            }
            return;
        }
        double k = 1 - Math.Exp(-dt * 12);
        foreach (var (id, t) in Target())
        {
            var p = Current[id]; double da = (t.Angle - p.Angle + 540) % 360 - 180;
            Current[id] = new(p.X + (t.X - p.X) * k, p.Y + (t.Y - p.Y) * k, p.Angle + da * k, p.Sx + (t.Sx - p.Sx) * k, p.Sy + (t.Sy - p.Sy) * k);
        }
    }
    static double Rad(double d) => d * Math.PI / 180;
    static Point Rot(double x, double y, double a) => new(x * Math.Cos(Rad(a)) - y * Math.Sin(Rad(a)), x * Math.Sin(Rad(a)) + y * Math.Cos(Rad(a)));
    public static Point[] Corners(Sprite s, Pose p) => new[] { new Point(0, 0), new Point(s.Width, 0), new Point(0, s.Height), new Point(s.Width, s.Height) }.Select(q => { var r = Rot((q.X - s.Ax * s.Width) * p.Sx, (q.Y - s.Ay * s.Height) * p.Sy, p.Angle); return new Point(r.X + p.X, r.Y + p.Y); }).ToArray();
    double Track(string name, double f)
    {
        var times = motion.GetProperty("TIMES").EnumerateArray().Select(e => e.GetDouble()).ToArray();
        var a = motion.GetProperty("TRACKS").GetProperty(name).EnumerateArray().Select(e => e.GetDouble()).ToArray();
        f = (f % 96 + 96) % 96; int j = 0; while (times[j + 1] <= f) j++;
        double ta = times[j], tb = times[j + 1], tp = j > 0 ? times[j - 1] : times[^2] - 96, tn = j + 2 < times.Length ? times[j + 2] : times[1] + 96;
        double vp = j > 0 ? a[j - 1] : a[^2], vn = j + 2 < a.Length ? a[j + 2] : a[1], u = (f - ta) / (tb - ta);
        double m0 = (a[j + 1] - vp) / (tb - tp) * (tb - ta), m1 = (vn - a[j]) / (tn - ta) * (tb - ta);
        return (2*u*u*u-3*u*u+1)*a[j]+(u*u*u-2*u*u+u)*m0+(-2*u*u*u+3*u*u)*a[j+1]+(u*u*u-u*u)*m1;
    }
    public Dictionary<string, Pose> Target()
    {
        bool single = State is PetState.Left or PetState.Right;
        double f = StateAge * 20, phase = f * Math.PI * 2 / 96;
        double angle = single ? Track("tilt", f) : .6 * Math.Sin(Clock * 1.4);
        double sy = single ? Track("squash", f) : 1 + .006 * Math.Sin(Clock * 2), sx = Math.Pow(sy, -.55);
        double cx = single ? 295 + Track("x", f) : 263, cy = single ? 305 + Track("y", f) : 295 + Math.Sin(Clock * 2);
        Point World(double x, double y) { var r = Rot(x * sx, y * sy, angle); return new(cx + r.X, cy + r.Y); }
        Point Border(double theta)
        {
            var p=contour[((int)Math.Round(theta)%360+360)%360]; return World(p.X,p.Y);
        }
        var result = new Dictionary<string, Pose>();
        void Put(string id, Point p, double a = 0, double x = 1, double y = 1) => result[id] = new(p.X, p.Y, a, x, y);
        void Link(string id, Point start, Point end, double ex, double ey)
        {
            var s = Sprites[id]; double vx = (ex - s.Ax) * s.Width, vy = (ey - s.Ay) * s.Height;
            double dx = end.X - start.X, dy = end.Y - start.Y, scale = Math.Sqrt((dx*dx+dy*dy)/(vx*vx+vy*vy));
            Put(id, start, (Math.Atan2(dy, dx) - Math.Atan2(vy, vx)) * 180 / Math.PI, scale, scale);
        }
        double compression = single ? Math.Clamp((angle - 18) / 46, 0, 1) : 0;
        var root = Border(69); var foot = new Point(294, 563);
        var knee = new Point((root.X + foot.X) / 2 * (1-compression) + (foot.X+75)*compression, (root.Y+foot.Y)/2*(1-compression)+(foot.Y-45)*compression);
        Link("rupper", root, knee, .78, 1); Link("rlower", knee, foot, .18, .95);
        if (!single)
        {
            var lr = Border(111); var lf = new Point(216, 563); var lk = new Point((lr.X+lf.X)/2-5, (lr.Y+lf.Y)/2);
            Link("lupper", lr, lk, .78, 1); Link("llower", lk, lf, .18, .95);
            // Keep the lifted leg invisible while two complete legs support the body.
            Put("kick", lr, 0, 0, 0);
        }
        else { Put("lupper", root, 0, 0, 0); Put("llower", root, 0, 0, 0); Put("kick", Border(Track("leg-root", f)), Track("kick", f), 1.12+.34*compression, 1.12+.34*compression); }
        double wave = WaveAge < 2.8 ? Math.Sin(Math.PI * WaveAge / 2.8) : 0;
        Put("arm", Border(single ? Track("free-root",f) : 164 - wave*22), single ? Track("arm-angle", f) : 171+wave*(110+18*Math.Sin(WaveAge*15)), single ? 1.12+.18*compression : 1, single ? 1.12+.18*compression : 1);
        Put("cup", Border(single ? Track("cup-root", f) : 16), single ? Track("cup-angle",f-2) : 0);
        Put("body", new(cx, cy), angle, sx, sy);
        for (int i=0; i<2; i++)
        {
            double inertia = single ? Track("tilt",f-3)-Track("tilt",f) : 0;
            double wx = (i==0?-32:32) + (single ? 9+inertia*.55+7*Math.Sin(phase*2+i*2.3) : 0), wy = -38+(single ? 9+compression*15+9*Math.Sin(phase*3+i*2.1) : 0);
            // Blinking only in the relaxed pose; pupils stay attached to their whites.
            double blink = !single && Clock % 5.7 < .12 ? .12 : 1;
            Put("white"+i,World(wx,wy),0,1,blink);
            Put("pupil"+i,World(wx+(single?17*Math.Sin(2*phase+i*1.8+.6)+inertia*.65+10:3*Math.Sin(Clock*.6)),wy+(single?20*Math.Sin(3*phase+i*2.6+.8)+compression*21+4:2)),0,1,blink);
        }
        Put("mouth",World(single?17+13*Math.Sin(2*phase+.5):0,20+(single?8+10*Math.Sin(3*phase):0)),angle+(single?14*Math.Sin(2*phase):0));
        Put("bow",World(single?14+29*Math.Sin(phase+1):0,54+(single?17*Math.Sin(3*phase+1):0)),angle+(single?24*Math.Sin(2*phase):0));
        Put("hat",World(18+(single?11*Math.Sin(2*phase+.6):0),-108),angle+(single?4*Math.Sin(3*phase+.3):0));
        return result;
    }
    static readonly string[] Order = ["rupper","rlower","lupper","llower","kick","arm","cup","body","white0","white1","pupil0","pupil1","mouth","bow","hat"];
    public void Draw(DrawingContext dc)
    {
        var order = State == PetState.Slime ? new[] { "body","rupper","rlower","lupper","llower","kick","arm","hat","cup","white0","white1","pupil0","pupil1","mouth","bow" } : Order;
        foreach (var id in order)
        {
            var p = Current[id]; var s = Sprites[id];
            dc.PushTransform(new TranslateTransform(p.X,p.Y)); dc.PushTransform(new RotateTransform(p.Angle)); dc.PushTransform(new ScaleTransform(p.Sx,p.Sy));
            dc.DrawImage(s.Image,new Rect(-s.Ax*s.Width,-s.Ay*s.Height,s.Width,s.Height));
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
}

sealed class PetView : FrameworkElement
{
    public Rig Rig = new();
    public OriginalDesign Design = new();
    public double HintUntil = 8;
    public double GuardBlend;
    protected override void OnRender(DrawingContext dc)
    {
        if(GuardBlend>=1)return;
        dc.PushOpacity(1-GuardBlend);
        dc.PushTransform(new ScaleTransform(ActualWidth/640,ActualHeight/640));
        Design.Draw(dc,Rig);
        if (Rig.Clock < HintUntil)
        {
            string message = Rig.State == PetState.Normal && Rig.WaveAge < 2.8 ? "嗨，陪你一起站稳！" : "按住 L + R 站稳 · 右键打开菜单";
            var text = new FormattedText(message,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),19,Brushes.White,1);
            var rect = new Rect(320-text.Width/2-18,34,text.Width+36,48);
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(235,48,45,70)),null,rect,18,18); dc.DrawText(text,new Point(rect.Left+18,44));
        }
        dc.Pop();
        dc.Pop();
    }
}

sealed class PetWindow : Window
{
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    readonly PetView pet = new();
    readonly DispatcherTimer timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
    readonly Stopwatch watch = Stopwatch.StartNew();
    double last;
    CountdownWindow? countdown;
    readonly PressEdge countdownKey=new(),quitKey=new(),resetKey=new();
    readonly bool pollKeys;
    PetShortcuts? shortcuts;
    internal void StartCountdown(double startAt=0)
    {
        if(countdown!=null)return;
        countdown=new CountdownWindow(this,blend=>{pet.GuardBlend=blend;pet.InvalidateVisual();},startAt){PollInput=pollKeys};countdown.Closed+=(_,_)=>countdown=null;countdown.Show();
    }
    void ResetPet()
    {
        countdown?.Close();countdown=null;
        pet.Rig=new Rig();pet.Design=new OriginalDesign();pet.GuardBlend=0;pet.HintUntil=8;
        Width=Height=400;Topmost=true;Position();watch.Restart();last=0;pet.InvalidateVisual();
    }
    public PetWindow(bool pollKeys=true)
    {
        this.pollKeys=pollKeys;
        Title="零件小人桌宠"; Width=400; Height=400; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true; Background=Brushes.Transparent; Topmost=true; ShowInTaskbar=true; ShowActivated=false;
        Content=pet;
        Loaded+=(_,_)=> { Position(); timer.Start(); };
        SourceInitialized+=(_,_)=>{if(pollKeys)shortcuts=new PetShortcuts(this,ResetPet,Close);};
        Closed+=(_,_)=>{timer.Stop();shortcuts?.Dispose();};
        MouseLeftButtonDown+=(_,e)=> { if(e.ClickCount==2) { pet.Rig.Wave(); pet.HintUntil=pet.Rig.Clock+3; } else DragMove(); };
        var menu = new ContextMenu();
        void Item(string title, Action action) { var item=new MenuItem { Header=title }; item.Click+=(_,_)=>action(); menu.Items.Add(item); }
        Item("操作说明：按住 L / R 支撑左右脚",()=>pet.HintUntil=pet.Rig.Clock+8);
        Item("打个招呼（双脚站稳时）",()=> { pet.Rig.Wave(); pet.HintUntil=pet.Rig.Clock+3; });
        Item("S · 小宠物倒计时",()=>StartCountdown());
        menu.Items.Add(new MenuItem{Header="守护：F/H 踩踏 · WASD/IJKL 移动 · Esc 暂停",IsEnabled=false});
        Item("小队散场 / 结束守护并归位",()=>countdown?.ScatterAndClose());
        menu.Items.Add(new Separator());
        foreach(double size in new[]{300d,400d,520d}) { var selected=size; Item($"大小 · {size/4:0}%",()=> { double ground=Top+Height*566/640; Width=Height=selected; Top=ground-Height*566/640; }); }
        Item("回到屏幕右下角",Position);
        Item("始终置顶 · 切换",()=>Topmost=!Topmost);
        menu.Items.Add(new Separator());Item("重置桌宠 · Ctrl+Alt+R",ResetPet);Item("退出桌宠 · Ctrl+Alt+Q",Close);pet.ContextMenu=menu;
        timer.Tick+=(_,_)=>
        {
            double now=watch.Elapsed.TotalSeconds,dt=Math.Min(.05,now-last);last=now;
            bool command=pollKeys&&(GetAsyncKeyState(0x11)&0x8000)!=0&&(GetAsyncKeyState(0x12)&0x8000)!=0;
            bool quit=quitKey.Sample(command&&(GetAsyncKeyState(0x51)&0x8000)!=0),reset=resetKey.Sample(command&&(GetAsyncKeyState(0x52)&0x8000)!=0);
            // Polling remains a fallback if another application already owns a chord.
            if(quit&&shortcuts?.QuitRegistered!=true){Close();return;}
            if(reset&&shortcuts?.ResetRegistered!=true){ResetPet();return;}
            if(countdownKey.Sample(pollKeys&&!command&&(GetAsyncKeyState(0x53)&0x8000)!=0))StartCountdown();
            pet.Rig.SetKeys(pollKeys&&!command&&(GetAsyncKeyState(0x4c)&0x8000)!=0,pollKeys&&!command&&(GetAsyncKeyState(0x52)&0x8000)!=0);
            pet.Rig.Step(dt);pet.Design.Step(pet.Rig,dt);pet.InvalidateVisual();
        };
        // Display changes cannot strand the pet on a disconnected monitor.
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=DisplayChanged;
        Closed+=(_,_)=>Microsoft.Win32.SystemEvents.DisplaySettingsChanged-=DisplayChanged;
    }
    void DisplayChanged(object? sender,EventArgs e)=>Dispatcher.InvokeAsync(Position);
    void Position() { var a=SystemParameters.WorkArea; Left=a.Right-Width-30; Top=a.Bottom-Height*566/640; }
}

static class Program
{
    [STAThread] static int Main(string[] args)
    {
        var app=new Application();
        if(args.Contains("--test-guard"))return GuardTests.Run(args.Length>1?args[1]:"guard-test");
        if(args.Contains("--smoke-guard")){CountdownWindow.SmokeGuard(args.Length>1?args[1]:"guard-test");return 0;}
        if(args.Contains("--smoke-flow")){GuardTests.SmokeFlow(args.Length>1?args[1]:"guard-test");return 0;}
        if(args.Contains("--test-controls")){ControlTests.Run(args.Length>1?args[1]:"controls-test");return 0;}
        if(args.Contains("--test-enhanced")){new OriginalDesign().ExportEnhanced(args.Length>1?args[1]:"enhanced-test");return 0;}
        if(args.Contains("--test-slime")){new OriginalDesign().ExportSlime(args.Length>1?args[1]:"slime-test");return 0;}
        if(args.Contains("--test-blink"))return CountdownScene.ExportBlink(args.Length>1?args[1]:"blink-test");
        if(args.Contains("--smoke-countdown")){CountdownWindow.SmokeTest(args.Length>1?args[1]:"countdown-smoke.txt");return 0;}
        if(args.Contains("--test-countdown"))
        {
            var key=new PressEdge();if(!key.Sample(true)||key.Sample(true)||key.Sample(false)||!key.Sample(true))throw new Exception("S hold/release edge detection failed");
            return CountdownScene.ExportAndTest(args.Length>1?args[1]:"countdown-test");
        }
        if(args.Contains("--export-v4")||args.Contains("--export-v3")||args.Contains("--export-v2")) { new OriginalDesign().Export(args.Length>1?args[1]:"animation-output");return 0; }
        if(args.Contains("--self-test")) return SelfTest(args.Length>1?args[1]:"test-output");
        using var mutex=new Mutex(true,"Local\\WobblyPet.Desktop.2026",out bool first);
        if(!first) return 0;
        app.DispatcherUnhandledException+=(_,e)=> { File.WriteAllText(Path.Combine(Path.GetTempPath(),"WobblyPet-error.log"),e.Exception.ToString()); MessageBox.Show("桌宠遇到错误，详情已保存到临时目录 WobblyPet-error.log。"); e.Handled=true; app.Shutdown(1); };
        app.Run(new PetWindow()); return 0;
    }
    static int SelfTest(string folder)
    {
        Directory.CreateDirectory(folder); var rig=new Rig();
        void Assert(bool b,string m) { if(!b) throw new Exception(m); }
        void Save(string name)
        {
            var visual=new DrawingVisual(); using(var dc=visual.RenderOpen()) { dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(239,237,231)),null,new Rect(0,0,640,640)); rig.Draw(dc); }
            var bmp=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32); bmp.Render(visual); var png=new PngBitmapEncoder(); png.Frames.Add(BitmapFrame.Create(bmp)); using var f=File.Create(Path.Combine(folder,name+".png")); png.Save(f);
        }
        foreach(var (l,r,state,name) in new[]{(true,true,PetState.Normal,"normal"),(false,true,PetState.Right,"right"),(true,false,PetState.Left,"left"),(false,false,PetState.Slime,"scatter")})
        {
            rig.SetKeys(l,r); for(int i=0;i<300;i++) rig.Step(1d/60);
            Assert(rig.State==state,"Input mapping failed"); Assert(rig.Current.Values.All(p=>double.IsFinite(p.X+p.Y+p.Angle+p.Sx+p.Sy)),"Invalid pose"); Save(name);
            if(state==PetState.Slime) foreach(var id in rig.Current.Keys) Assert(Rig.Corners(rig.Sprites[id],rig.Current[id]).Max(p=>p.Y)<=567,"Part below ground: "+id);
        }
        rig.SetKeys(true,true); for(int i=0;i<90;i++) rig.Step(1d/60); rig.Wave(); for(int i=0;i<60;i++)rig.Step(1d/60); Save("wave");
        // Input reversal at any point must preserve the current pose until the next frame.
        var before=rig.Current["body"]; rig.SetKeys(false,false); Assert(rig.Current["body"]==before,"Transition teleported"); rig.Step(.016); rig.SetKeys(true,false); rig.Step(.016); rig.SetKeys(false,true); rig.Step(.016); rig.SetKeys(true,true); for(int i=0;i<300;i++)rig.Step(1d/60); Save("reassembled");
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: four hold states, finite poses, scatter collisions, wave, interrupted transitions, reassembly."); return 0;
    }
}
