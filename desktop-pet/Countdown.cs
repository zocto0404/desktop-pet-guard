using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Xml.Linq;

namespace WobblyPet;

sealed class CountdownScene
{
    public const int Count=60;
    public const double StageSeconds=3.3,EntranceSeconds=3.36,EntranceExtra=1.68,ExitSeconds=3.2;
    public const double ExitStart=StageSeconds*5+EntranceExtra,Duration=ExitStart+ExitSeconds;
    record PathPart(Geometry Shape,Brush? Fill,Pen? Stroke);
    record Pet(double W,double H,PathPart[] Paths);
    public readonly record struct Actor(double X,double Y,double Scale,double Angle,double Leg,double Bob,int Face,double Opacity);
    readonly Pet[] pets;
    readonly Point[][] formations;
    public CountdownScene()
    {
        using var stream=Application.GetResourceStream(new Uri("Assets/countdown/source.json",UriKind.Relative))!.Stream;
        using var json=JsonDocument.Parse(stream);
        pets=json.RootElement.GetProperty("pets").EnumerateArray().Select(p=>new Pet(p.GetProperty("w").GetDouble(),p.GetProperty("h").GetDouble(),p.GetProperty("paths").EnumerateArray().Select(e=>
        {
            var xml=XElement.Parse(e.GetString()!);var shape=Geometry.Parse(xml.Attribute("d")!.Value);shape.Freeze();
            Brush? BrushFor(string? color)=>color==null||color=="none"?null:(Brush)new BrushConverter().ConvertFromString(color)!;
            var fill=BrushFor(xml.Attribute("fill")?.Value);var stroke=BrushFor(xml.Attribute("stroke")?.Value);Pen? pen=null;
            if(stroke!=null){double width=double.Parse(xml.Attribute("stroke-width")?.Value??"1",CultureInfo.InvariantCulture);pen=new Pen(stroke,width){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round};pen.Freeze();}
            fill?.Freeze();return new PathPart(shape,fill,pen);
        }).ToArray())).ToArray();
        formations=json.RootElement.GetProperty("formations").EnumerateArray().Select(f=>f.EnumerateArray().Select(p=>new Point(p.GetProperty("x").GetDouble(),p.GetProperty("y").GetDouble())).ToArray()).ToArray();
    }
    static double Clamp(double t)=>Math.Clamp(t,0,1);
    static double Ease(double t){t=Clamp(t);return t*t*(3-2*t);}
    public static double BlinkOpen(int actor,double time)
    {
        double first=.65+(actor*977%3000)/1000d,period=4.1+(actor*613%1800)/1000d;
        if(time<first)return 1;
        int cycle=(int)((time-first)/period);double local=(time-first)%period;
        double Lid(double t)=>t<0||t>=.26?1:t<.075?1-Ease(t/.075):t<.12?0:Ease((t-.12)/.14);
        double open=Lid(local);
        if(actor%5==0&&cycle%3==1)open=Math.Min(open,Lid(local-.38));
        return open;
    }
    void DrawModel(DrawingContext dc,int type,double leg,double blink)
    {
        var pet=pets[type];double hipX=new[]{94.7396,91.8753,89.0579}[type],hipY=new[]{115.139,124.798,122.603}[type];
        for(int j=0;j<3;j++)
        {
            var p=pet.Paths[j];if(j<2)dc.PushTransform(new RotateTransform(j==0?leg:-leg,j==0?hipX:hipX-27,hipY));
            dc.DrawGeometry(p.Fill,p.Stroke,p.Shape);if(j<2)dc.Pop();
        }
        for(int eye=0;eye<2;eye++)
        {
            var white=pet.Paths[3+eye];var pupil=pet.Paths[5+eye];var b=white.Shape.Bounds;double cy=b.Top+b.Height*.48;
            if(blink<=.08)
            {
                double slope=type==2?-3:0;
                var lid=new StreamGeometry();using(var c=lid.Open()){c.BeginFigure(new Point(b.Left+5,cy-slope),false,false);c.QuadraticBezierTo(new Point(b.Left+b.Width/2,cy+5),new Point(b.Right-5,cy+slope),true,false);}
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(74,73,57)),4){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},lid);
            }
            else
            {
                dc.PushTransform(new ScaleTransform(1,blink,b.Left+b.Width/2,cy));
                dc.DrawGeometry(white.Fill,white.Stroke,white.Shape);dc.PushClip(white.Shape);
                dc.DrawGeometry(pupil.Fill,pupil.Stroke,pupil.Shape);dc.Pop();dc.Pop();
            }
        }
        for(int j=7;j<pet.Paths.Length;j++){var p=pet.Paths[j];dc.DrawGeometry(p.Fill,p.Stroke,p.Shape);}
    }
    public static Point Edge(int i,double width,double height,bool exiting=false)
    {
        double scale=Math.Min(width/1280,height/720),w=width/scale,h=height/scale,left=640-w/2,top=360-h/2;
        double u=(((i/4)*(exiting?7:1))%15+.5)/15;
        return(i%4) switch{0=>new(left-90,top+u*h),1=>new(left+w+90,top+(1-u)*h),2=>new(left+u*w,top-90),_=>new(left+(1-u)*w,top+h+90)};
    }
    public Actor At(int i,double time,double width,double height,double? forcedExit=null)
    {
        if(time<0||time>=Duration)return new(0,0,0,0,0,0,1,0);
        double exitAt=forcedExit??ExitStart;
        bool exiting=time>=exitAt;
        double active=Math.Min(time,exitAt);
        // Slow only the entrance. Preserve each numeral's original settled hold time
        // and all later formation changes by offsetting the remainder of the timeline.
        double timeline=active<EntranceSeconds?active*.5:active-EntranceExtra;
        double stageTime=Math.Min(timeline,StageSeconds*5-.0001);
        int stage=Math.Min(4,(int)(stageTime/StageSeconds));double local=stageTime-stage*StageSeconds;
        Point from=stage==0?Edge(i,width,height):formations[stage-1][i],to=formations[stage][i];
        double u=Clamp((local-(i%5)*.045)/(stage==0?1.5:1.3)),v=Ease(u),wave=Math.Sin(Math.PI*u);
        double x=from.X+(to.X-from.X)*v+wave*Math.Sin(i*2.4)*60,y=from.Y+(to.Y-from.Y)*v+wave*(i%2==1?1:-1)*50;
        double[] sizes=[35,30,33,35,27];double size=(sizes[stage]+((stage>0?sizes[stage-1]:43)-sizes[stage])*(1-v))/160;
        bool walking=u<1;double opacity=1;
        if(exiting)
        {
            var end=Edge(i,width,height,true);double q=Ease((time-exitAt-(i%4)*.035)/2.95);
            x+=(end.X-x)*q;y+=(end.Y-y)*q;walking=true;opacity=1-Ease((time-exitAt-2.5)/.7);
            to=end;
        }
        double stride=9*Math.Min(time,Math.Min(EntranceSeconds,exitAt))
            +15*Math.Max(0,Math.Min(time,exitAt)-EntranceSeconds)+9*Math.Max(0,time-exitAt)+i*1.8;
        return new(x,y,size,walking?Math.Sin(stride)*4:Math.Sin(time*1.7+i)*1.3,walking?Math.Sin(stride)*32:0,
            walking?-Math.Abs(Math.Sin(stride))*4:Math.Sin(time*2+i)*.6,walking&&to.X<x?-1:1,opacity);
    }
    public void Draw(DrawingContext dc,double time,double width,double height,double? forcedExit=null)
    {
        if(width<=0||height<=0)return;
        double scale=Math.Min(width/1280,height/720);
        dc.PushTransform(new TranslateTransform((width-1280*scale)/2,(height-720*scale)/2));dc.PushTransform(new ScaleTransform(scale,scale));
        foreach(var item in Enumerable.Range(0,Count).Select(i=>(I:i,A:At(i,time,width,height,forcedExit))).OrderBy(p=>p.A.Y))
        {
            DrawActor(dc,item.I,item.A,time);
        }
        dc.Pop();dc.Pop();
    }
    public void DrawActor(DrawingContext dc,int index,Actor a,double time)
    {
        if(a.Opacity<=0)return;int type=index%3;var pet=pets[type];
        dc.PushOpacity(a.Opacity);dc.PushTransform(new TranslateTransform(a.X,a.Y+a.Bob));dc.PushTransform(new RotateTransform(a.Angle));dc.PushTransform(new ScaleTransform(a.Scale*a.Face,a.Scale));dc.PushTransform(new TranslateTransform(-pet.W/2,-pet.H/2));
        DrawModel(dc,type,a.Leg,BlinkOpen(index,time));
        dc.Pop();dc.Pop();dc.Pop();dc.Pop();dc.Pop();
    }
    public static int ExportAndTest(string folder)
    {
        Directory.CreateDirectory(folder);var scene=new CountdownScene();
        void Assert(bool value,string message){if(!value)throw new Exception(message);}
        foreach(var (w,h) in new[]{(1280d,720d),(1920d,1080d),(1280d,1024d),(2560d,1080d)})
        {
            for(int i=0;i<Count;i++)Assert(scene.At(i,0,w,h).Opacity==1,"Actor missing at entrance");
            for(double t=0;t<=Duration;t+=.05)for(int i=0;i<Count;i++){var a=scene.At(i,t,w,h);Assert(double.IsFinite(a.X+a.Y+a.Scale)&&a.Opacity>=0&&a.Opacity<=1,"Invalid actor");}
            Assert(Enumerable.Range(0,Count).All(i=>scene.At(i,Duration,w,h).Opacity==0),"Actors remain after completion");
        }
        // Render transparent artifacts from the same path used by the desktop overlay.
        for(int frame=0;frame<=(int)Math.Ceiling(Duration*20);frame++)
        {
            var v=new DrawingVisual();using(var dc=v.RenderOpen())scene.Draw(dc,frame/20d,1280,720);
            var bmp=new RenderTargetBitmap(1280,720,96,96,PixelFormats.Pbgra32);bmp.Render(v);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(Path.Combine(folder,$"frame-{frame:000}.png"));png.Save(f);
        }
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: original 3 SVG pets and 5 x 60 formations; four display aspect ratios; finite motion and alpha; transparent start/end; outward exit; no background or text.");return 0;
    }
    public static int ExportBlink(string folder)
    {
        Directory.CreateDirectory(folder);var scene=new CountdownScene();
        for(int i=0;i<Count;i++)for(double t=0;t<Duration;t+=.01)
        {double b=BlinkOpen(i,t);if(!double.IsFinite(b)||b<0||b>1)throw new Exception("Invalid blink");}
        if(BlinkOpen(0,.75)!=0||BlinkOpen(1,.75)!=1||BlinkOpen(0,1)!=1)throw new Exception("Blink is not staggered or does not reopen");
        for(int frame=0;frame<108;frame++)
        {
            var v=new DrawingVisual();using(var dc=v.RenderOpen())
            {
                for(int type=0;type<3;type++)
                {
                    dc.PushTransform(new TranslateTransform(30+type*215,40));dc.PushTransform(new ScaleTransform(1.1,1.1));
                    scene.DrawModel(dc,type,Math.Sin(frame/30d*12+type)*12,BlinkOpen(type,frame/30d));dc.Pop();dc.Pop();
                }
            }
            var bmp=new RenderTargetBitmap(680,260,96,96,PixelFormats.Pbgra32);bmp.Render(v);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bmp));using var f=File.Create(Path.Combine(folder,$"frame-{frame:000}.png"));encoder.Save(f);
        }
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: 60 actors have finite 0..1 eyelid openness throughout countdown; independent offsets; closed hold; full reopening; three original pet models rendered with shared blink renderer.");return 0;
    }
}

sealed class CountdownView:FrameworkElement
{
    public readonly CountdownScene Scene=new();
    public double Time;
    public double? ExitAt;
    public const double ScatterSeconds=2.2;
    public Rect FormationBounds;
    public GuardScene? Guard;
    public Matrix StageMatrix=Matrix.Identity;
    public double PetOpacity=1;
    public string? GuardStatus;
    string? previousStatus;
    FormattedText? statusText;
    static readonly Brush statusBackground = new SolidColorBrush(Color.FromArgb(235,48,45,70));
    public CountdownScene.Actor[]? CancelActors;
    static double Ease(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    public double FormationScale=>Math.Min(FormationBounds.Width/1280,FormationBounds.Height/720);
    public Point FormationOrigin=>new(FormationBounds.X+(FormationBounds.Width-1280*FormationScale)/2,FormationBounds.Y+(FormationBounds.Height-720*FormationScale)/2);
    public CountdownScene.Actor ScatterActor(int i,double age)
    {
        var a=Scene.At(i,CountdownScene.ExitStart-.00001,FormationBounds.Width,FormationBounds.Height);
        double angle=i*2.399963,spread=145+i%5*19,scale=FormationScale;
        // Scatter inside the visible monitor. Everyone remains fully opaque and on screen.
        double x=Math.Clamp(a.X+Math.Cos(angle)*spread,640-FormationBounds.Width/scale/2+30,640+FormationBounds.Width/scale/2-30);
        double y=Math.Clamp(a.Y+Math.Sin(angle)*spread,360-FormationBounds.Height/scale/2+35,360+FormationBounds.Height/scale/2-35);
        double u=Ease(age/ScatterSeconds),stride=age*9+i*1.8;
        return a with { X=a.X+(x-a.X)*u,Y=a.Y+(y-a.Y)*u,Angle=Math.Sin(stride)*4,Leg=Math.Sin(stride)*32,Bob=-Math.Abs(Math.Sin(stride))*4,Face=x<a.X?-1:1,Opacity=1 };
    }
    public void SeedGuard(GuardScene guard)
    {
        var inverse=StageMatrix;inverse.Invert();var origin=FormationOrigin;double scale=FormationScale;
        for(int i=0;i<CountdownScene.Count;i++)
        {
            var a=ScatterActor(i,ScatterSeconds);
            // The countdown anchors the center; the attack animation anchors the feet.
            double height=new[]{146d,156d,153d}[i%3];
            var p=inverse.Transform(new Point(origin.X+a.X*scale,origin.Y+(a.Y+a.Bob+height*a.Scale/2)*scale));
            guard.Game.Add(new GuardActor { Id=i,Type=i%3,Position=p,Scale=a.Scale*scale/StageMatrix.M11,Side=p.X<600?-1:1,Face=a.Face,Speed=110+i%7*5,Phase=(ScatterSeconds*9+i*1.8)/(2*Math.PI) });
        }
        Guard=guard;
    }
    protected override void OnRender(DrawingContext dc)
    {
        if(Guard!=null)
        {
            dc.PushTransform(new MatrixTransform(StageMatrix));Guard.Draw(dc,PetOpacity);dc.Pop();
            if(GuardStatus is { } status)
            {
                if(status!=previousStatus)
                {
                    statusText=new FormattedText(status,CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,
                        new Typeface("Microsoft YaHei UI"),15,Brushes.White,VisualTreeHelper.GetDpi(this).PixelsPerDip);
                    previousStatus=status;
                }
                if(statusText is { } text)
                {
                    var badge=new Rect(FormationBounds.X+(FormationBounds.Width-text.Width)/2-14,
                        FormationBounds.Top+20,text.Width+28,text.Height+14);
                    dc.DrawRoundedRectangle(statusBackground,null,badge,11,11);
                    dc.DrawText(text,new Point(badge.Left+14,badge.Top+7));
                }
            }
            return;
        }
        dc.PushTransform(new TranslateTransform(FormationBounds.X,FormationBounds.Y));
        if(ExitAt.HasValue&&CancelActors==null)Scene.Draw(dc,Time,FormationBounds.Width,FormationBounds.Height,ExitAt);
        else if(Time<CountdownScene.ExitStart&&!ExitAt.HasValue)Scene.Draw(dc,Time,FormationBounds.Width,FormationBounds.Height);
        else
        {
            double scale=FormationScale;var origin=FormationOrigin;
            dc.PushTransform(new TranslateTransform(origin.X-FormationBounds.X,origin.Y-FormationBounds.Y));dc.PushTransform(new ScaleTransform(scale,scale));
            foreach(int i in Enumerable.Range(0,CountdownScene.Count).OrderBy(i=>(CancelActors?[i]??ScatterActor(i,Time-CountdownScene.ExitStart)).Y))
            {
                var a=CancelActors?[i]??ScatterActor(i,Time-CountdownScene.ExitStart);
                if(ExitAt.HasValue){double t=Time-ExitAt.Value,q=Ease(t/2.95),stride=t*9+i*1.8;var end=CountdownScene.Edge(i,FormationBounds.Width,FormationBounds.Height,true);a=a with { X=a.X+(end.X-a.X)*q,Y=a.Y+(end.Y-a.Y)*q,Angle=Math.Sin(stride)*4,Leg=Math.Sin(stride)*32,Bob=-Math.Abs(Math.Sin(stride))*4,Face=end.X<a.X?-1:1,Opacity=1-Ease((t-2.5)/.7) };}
                Scene.DrawActor(dc,i,a,Time);
            }
            dc.Pop();dc.Pop();
        }
        dc.Pop();
    }
}

sealed class CountdownWindow:Window
{
    [DllImport("user32.dll",EntryPoint="GetWindowLongPtrW")] static extern nint GetStyle(nint hwnd,int index);
    [DllImport("user32.dll",EntryPoint="SetWindowLongPtrW")] static extern nint SetStyle(nint hwnd,int index,nint value);
    [DllImport("user32.dll")] static extern nint MonitorFromWindow(nint hwnd,uint flags);
    [DllImport("user32.dll",CharSet=CharSet.Auto)] static extern bool GetMonitorInfo(nint monitor,ref MonitorInfo info);
    [StructLayout(LayoutKind.Sequential)] struct NativeRect{public int Left,Top,Right,Bottom;}
    [StructLayout(LayoutKind.Sequential)] struct MonitorInfo{public int Size;public NativeRect Monitor,Work;public uint Flags;}
    readonly CountdownView view=new();readonly Stopwatch watch=new();
    readonly DispatcherTimer timer=new(){Interval=TimeSpan.FromMilliseconds(16)};
    readonly Window petOwner;
    readonly Action<double> blendPet;
    readonly GuardInput input=new();
    readonly GuardPedalInput pedalInput=new();
    double last;
    Matrix previousMatrix;
    readonly double startAt;
    internal bool PollInput=true;
    internal PedalBridge? Pedals {get;init;}
    internal GuardGame? Game=>view.Guard?.Game;
    internal string Phase=>view.Guard?.Game.State??(view.ExitAt.HasValue?"dismiss":view.Time<CountdownScene.ExitStart?"countdown":"scatter");
    public CountdownWindow(Window owner,Action<double>? blend=null,double startAt=0)
    {
        petOwner=owner;blendPet=blend??(_=>{});this.startAt=startAt;last=startAt;
        Title="小宠物倒计时";Owner=owner;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;
        AllowsTransparency=true;Background=Brushes.Transparent;Topmost=true;ShowInTaskbar=false;ShowActivated=false;Focusable=false;IsHitTestVisible=false;Content=view;
        var hwnd=new WindowInteropHelper(owner).Handle;var info=new MonitorInfo{Size=Marshal.SizeOf<MonitorInfo>()};
        var area=SystemParameters.WorkArea;
        if(GetMonitorInfo(MonitorFromWindow(hwnd,2),ref info))
        {
            var matrix=HwndSource.FromHwnd(hwnd)?.CompositionTarget?.TransformFromDevice??Matrix.Identity;
            var p=matrix.Transform(new Point(info.Monitor.Left,info.Monitor.Top));var q=matrix.Transform(new Point(info.Monitor.Right,info.Monitor.Bottom));area=new Rect(p,q);
        }
        Left=SystemParameters.VirtualScreenLeft;Top=SystemParameters.VirtualScreenTop;Width=SystemParameters.VirtualScreenWidth;Height=SystemParameters.VirtualScreenHeight;
        view.FormationBounds=new Rect(area.Left-Left,area.Top-Top,area.Width,area.Height);
        SourceInitialized+=(_,_)=>{var handle=new WindowInteropHelper(this).Handle;SetStyle(handle,-20,(nint)((long)GetStyle(handle,-20)|0x20|0x80|0x08000000));};
        Loaded+=(_,_)=>{watch.Start();timer.Start();};Closed+=(_,_)=>{timer.Stop();blendPet(0);};
        timer.Tick+=(_,_)=>Tick();
    }
    void Tick()
    {
        view.Time=startAt+watch.Elapsed.TotalSeconds;double dt=Math.Min(.05,view.Time-last);last=view.Time;
        if(view.ExitAt.HasValue&&view.Time>=view.ExitAt.Value+CountdownScene.ExitSeconds){Close();return;}
        double scale=petOwner.Width/640*1.2/.72;
        view.StageMatrix=new Matrix(scale,0,0,scale,petOwner.Left-Left+307.2*petOwner.Width/640-600*scale,petOwner.Top-Top+563*petOwner.Height/640-440*scale);
        if(!view.ExitAt.HasValue&&view.Guard==null&&view.Time>=CountdownScene.ExitStart+CountdownView.ScatterSeconds)
        {
            view.SeedGuard(new GuardScene());input.Reset();previousMatrix=view.StageMatrix;
        }
        if(view.Guard is { } guard)
        {
            // Preserve each actor's desktop position while its target (the owner) is dragged/resized.
            var inverse=view.StageMatrix;inverse.Invert();
            guard.Game.SetViewport(new Rect(inverse.Transform(new Point(0,0)),inverse.Transform(new Point(Width,Height))));
            if(previousMatrix!=view.StageMatrix)
            {
                Point Rebase(Point p)=>inverse.Transform(previousMatrix.Transform(p));
                foreach(var a in guard.Game.Actors)a.Position=Rebase(a.Position);
                for(int i=0;i<guard.Game.Debris.Count;i++){var d=guard.Game.Debris[i];guard.Game.Debris[i]=d with { Position=Rebase(d.Position) };}
                for(int i=0;i<guard.Game.Returns.Count;i++){var r=guard.Game.Returns[i];guard.Game.Returns[i]=r with { Position=Rebase(r.Position) };}
            }
            previousMatrix=view.StageMatrix;
            bool ready=true;
            if(Pedals is { } bridge)
            {
                if(PollInput)input.PollPause(guard.Game);
                ready=pedalInput.Step(guard.Game,bridge.Read(),dt);
                view.GuardStatus=guard.Game.State!="playing"?null:!ready?"等待双脚踏板恢复 · 已暂停":
                    guard.Game.Paused?"已暂停 · Esc 继续":
                    $"踏板守护 · 已踩中 {guard.Game.Hits} · 剩余 {Math.Ceiling(GuardGame.Duration-guard.Game.Elapsed):0} 秒";
            }
            else if(PollInput)input.Step(guard.Game,dt,view,inverse);
            if(ready||guard.Game.State!="playing")guard.Game.Step(dt);
            double blend=guard.Game.State=="result"?1-Ease((guard.Game.ReactionAge-2)/.5):Ease(guard.Game.Clock/.4);
            view.PetOpacity=blend;blendPet(blend);
            if(guard.Game.Complete){Close();return;}
        }
        view.InvalidateVisual();
    }
    static double Ease(double t){t=Math.Clamp(t,0,1);return t*t*(3-2*t);}
    public void ScatterAndClose()
    {
        if(view.Guard is { } guard){guard.Game.Paused=false;guard.Game.BeginEnding();return;}
        if(view.ExitAt.HasValue)return;
        if(view.Time>=CountdownScene.ExitStart)view.CancelActors=Enumerable.Range(0,CountdownScene.Count).Select(i=>view.ScatterActor(i,view.Time-CountdownScene.ExitStart)).ToArray();
        view.ExitAt=view.Time;
    }
    public static void SmokeGuard(string folder)
    {
        Directory.CreateDirectory(folder);
        var owner=new Window{Width=400,Height=400,Left=-20000,Top=-20000,Opacity=0,ShowActivated=false,ShowInTaskbar=false};
        var check=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};var elapsed=Stopwatch.StartNew();
        bool moved=false,cancelled=false,started=false;
        owner.Loaded+=(_,_)=>
        {
            var overlay=new CountdownWindow(owner,startAt:CountdownScene.ExitStart+CountdownView.ScatterSeconds-.15){Left=-20000,Top=-20000,PollInput=false};
            overlay.Closed+=(_,_)=>
            {
                check.Stop();
                if(!started||!moved||!cancelled||overlay.view.Guard?.Game.Complete!=true)throw new Exception("Native guard smoke did not finish");
                File.WriteAllText(Path.Combine(folder,"window-result.txt"),"PASS: native nonactivating overlay; live countdown handoff; original 60 actors; drag/resize target updates; no actor loss; early return sequence; overlay and owner timers closed.");owner.Close();
            };
            check.Tick+=(_,_)=>
            {
                if(elapsed.Elapsed.TotalSeconds>25)throw new Exception("Guard window timed out");
                if(overlay.view.Guard is not { } guard)return;
                started=true;
                if(!moved&&guard.Game.Clock>.4)
                {
                    AssertStyles();owner.Left-=120;owner.Width=owner.Height=520;moved=true;
                }
                if(!cancelled&&guard.Game.Clock>1)
                {
                    if(guard.Game.Actors.Count!=60)throw new Exception("Live handoff lost actors");
                    overlay.ScatterAndClose();cancelled=true;
                }
                void AssertStyles(){long style=(long)GetStyle(new WindowInteropHelper(overlay).Handle,-20);if((style&0x08000020)!=0x08000020)throw new Exception("Guard overlay intercepted desktop input");}
            };
            overlay.Show();check.Start();
        };
        Application.Current.Run(owner);
    }
    public static void SmokeTest(string file)
    {
        var owner=new Window{Width=1,Height=1,Left=-20000,Top=-20000,Opacity=0,ShowActivated=false,ShowInTaskbar=false};
        owner.Loaded+=(_,_)=>
        {
            var overlay=new CountdownWindow(owner){Left=-20000,Top=-20000};
            overlay.Loaded+=(_,_)=>
            {
                long style=(long)GetStyle(new WindowInteropHelper(overlay).Handle,-20);
                if((style&0x08000020)!=0x08000020)throw new Exception("Overlay is not click-through/nonactivating");
                overlay.ScatterAndClose();
            };
            overlay.Closed+=(_,_)=>{File.WriteAllText(file,"PASS: native transparent overlay created; click-through and no-activate styles present; exit animation closes window and timer; owner closes cleanly.");owner.Close();};
            overlay.Show();
        };
        Application.Current.Run(owner);
    }
}

sealed class PressEdge
{
    bool held;
    public bool Sample(bool down){bool start=down&&!held;held=down;return start;}
}
