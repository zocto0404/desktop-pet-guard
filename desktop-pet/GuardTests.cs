using System.IO;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WobblyPet;

static class GuardTests
{
    static void Assert(bool pass,string message){if(!pass)throw new Exception(message);}
    static void Save(string file,int width,int height,Action<DrawingContext> draw)
    {
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())draw(dc);
        var bitmap=new RenderTargetBitmap(width,height,96,96,PixelFormats.Pbgra32);bitmap.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using var output=File.Create(file);encoder.Save(output);
    }
    public static int Run(string folder)
    {
        Directory.CreateDirectory(folder);
        var view=new CountdownView { FormationBounds=new Rect(0,0,1280,720), StageMatrix=new Matrix(1,0,0,1,0,0) };
        for(double t=0;t<=CountdownView.ScatterSeconds;t+=.02)
        for(int i=0;i<CountdownScene.Count;i++)
        {
            var a=view.ScatterActor(i,t);Assert(a.Opacity==1,"Countdown member faded before attack");
            Assert(a.X>=30&&a.X<=1250&&a.Y>=35&&a.Y<=685,"Scatter left visible monitor");
        }
        foreach(var (w,h) in new[]{(1920d,1080d),(1280d,1024d),(2560d,1080d)})
        {
            view.FormationBounds=new Rect(-320,20,w,h);
            var spread=Enumerable.Range(0,60).Select(i=>view.ScatterActor(i,CountdownView.ScatterSeconds)).ToArray();
            Assert(spread.All(a=>a.Opacity==1),"Wide display scatter lost actor");
            Assert((spread.Max(a=>a.X)-spread.Min(a=>a.X))*view.FormationScale>w*.8&&
                (spread.Max(a=>a.Y)-spread.Min(a=>a.Y))*view.FormationScale>h*.7,"Opening was not spread across the monitor");
        }
        view.FormationBounds=new Rect(0,0,1280,720);
        var scene=new GuardScene();view.SeedGuard(scene);var game=scene.Game;
        Assert(game.Actors.Count==60&&game.Actors.Select(a=>a.Id).Distinct().Count()==60,"Handoff replaced countdown actors");
        Assert(Enumerable.Range(0,3).All(type=>game.Actors.Count(a=>a.Type==type)==20),"Type order changed");
        for(int i=0;i<60;i++)
        {
            var a=view.ScatterActor(i,CountdownView.ScatterSeconds);var actor=game.Actors.Single(actor=>actor.Id==i);
            Assert(Math.Abs(actor.Position.X-a.X)<.001,"Handoff moved actor horizontally");
            Assert(Math.Abs(actor.Position.Y-a.Y-a.Bob-new[]{146d,156d,153d}[i%3]*a.Scale/2)<.001,"Center-to-feet handoff jumped");
        }
        Assert(game.Actors.Count(a=>(a.Position-new Point(600,440)).Length<180)<10,"Opening actors crowd the pet");
        Save(Path.Combine(folder,"01-handoff.png"),1280,720,dc=>scene.Draw(dc));
        var opening=game.Actors.ToDictionary(a=>a.Id,a=>a.Position);
        var arrivals=new HashSet<int>();var edges=new HashSet<int>();
        double lastArrival=0;
        int captures=0;
        for(int frame=0;frame<70*60;frame++)
        {
            game.Step(1d/60);
            Assert(game.Actors.All(a=>double.IsFinite(a.Position.X+a.Position.Y+a.Scale)),"Invalid actor motion");
            if(frame<30)Assert(game.Actors.All(a=>a.Position==opening[a.Id]),"Opening group immediately rushed the pet");
            if(frame==10*60)Assert(game.Actors.Count(a=>a.Id<60&&(a.Position-new Point(600,440)).Length>375)>=15,
                "The opening crowd converged too early");
            foreach(var a in game.Actors.Where(a=>a.Id>=60&&arrivals.Add(a.Id)))
            {
                var p=a.Position;var v=game.Viewport;
                int edge=p.X<v.Left?0:p.X>v.Right?1:p.Y<v.Top?2:p.Y>v.Bottom?3:-1;
                Assert(edge>=0&&game.Elapsed>=3&&game.Elapsed<55,"Reinforcement popped into view or out of sequence");
                Assert(lastArrival==0||game.Elapsed-lastArrival is >.85 and <1.85,"Reinforcements formed an unintended batch or gap");
                edges.Add(edge);lastArrival=game.Elapsed;
            }
            if(new[]{3*60,10*60,17*60,30*60,45*60,59*60,64*60,68*60}.Contains(frame))
            {
                Save(Path.Combine(folder,$"event-{frame/60:00}.png"),1280,720,dc=>scene.Draw(dc));captures++;
            }
        }
        Assert(game.Complete&&game.Missing.Count==0&&game.Actors.Count==0,"Event did not restore/finish");
        Assert(edges.Count==4&&arrivals.Count>=30&&lastArrival>52,"Reinforcements did not continue across all four edges");
        Assert(game.Cursor==19,"Undefended event did not complete all 19 pickups: "+game.Cursor);
        // Contact happens at the bottom of the falling foot, never on initial press.
        var defense=new GuardGame();var victim=new GuardActor{Id=200,Type=1,Position=new(300,420),Speed=0};defense.Add(victim);
        defense.Aim(true,300,420);defense.Press(true);Assert(defense.Hits==0,"Premature foot contact");
        for(int i=0;i<8;i++)defense.Step(1d/60);Assert(defense.Hits==1,"Foot missed contact");
        defense.Press(true);Assert(defense.Hits==1,"Held pedal retriggered");
        double clock=defense.Clock;defense.TogglePause();defense.Step(.05);Assert(defense.Clock==clock,"Pause advanced simulation");defense.TogglePause();
        defense.Aim(true,1200,400);defense.Aim(false,0,400);Assert(defense.RightFoot.X-defense.LeftFoot.X>=148,"Feet crossed");
        defense.SetViewport(new Rect(-1000,-300,1828,1000));defense.Aim(false,800,420);
        Assert(defense.RightFoot.X+92<=defense.Viewport.Right,"Foot clipped at desktop edge");
        defense.SetViewport(new Rect(-1000,-300,1828,742));defense.Aim(false,800,500);
        Assert(defense.RightFoot.Y+12<=defense.Viewport.Bottom,"Foot clipped below desktop");
        var edgeVictim=new GuardActor{Id=201,Type=2,Position=new(750,420),Job=-1};defense.Hit(edgeVictim);
        Assert(defense.Debris.Last().Direction==-1,"Stomp debris flew out through the nearest screen edge");
        // Intercepting one member of a two-person carrying team returns the entire object once.
        var rescue=new GuardGame();rescue.Cursor=2;rescue.Missing.Add(1);rescue.Jobs[1].Status="carry";rescue.Jobs[1].Members.AddRange([1,2]);
        rescue.Actors.Add(new GuardActor{Id=1,Job=1,Position=new(300,420),Type=0});rescue.Actors.Add(new GuardActor{Id=2,Job=1,Position=new(340,420),Type=2});
        rescue.Hit(rescue.Actors[0]);Assert(!rescue.Missing.Contains(1)&&rescue.Rescued==1&&rescue.Returns.Count==1,"Cooperative cargo not rescued");
        rescue.Hit(rescue.Actors[0]);Assert(rescue.Rescued==1,"Cargo rescued twice");
        // Cancel at any damage stage must use the same return sequence.
        foreach(int stage in new[]{0,4,12,19})
        {
            var cancel=new GuardGame();cancel.Cursor=stage;foreach(int part in Enumerable.Range(0,stage))cancel.Missing.Add(part);
            cancel.BeginEnding();for(int f=0;f<10*60;f++)cancel.Step(1d/60);
            Assert(cancel.Complete&&cancel.Missing.Count==0,"Cancellation left missing parts");
        }
        // Use the real default bottom-right placement, rather than a centered camera.
        var edgeView=new CountdownView{FormationBounds=new Rect(0,0,1920,1080)};
        double edgeScale=400d/640*1.2/.72;
        edgeView.StageMatrix=new Matrix(edgeScale,0,0,edgeScale,1490+307.2*400/640-600*edgeScale,1080-400d*566/640+563d*400/640-440*edgeScale);
        var edgeScene=new GuardScene();edgeView.SeedGuard(edgeScene);var edgeInverse=edgeView.StageMatrix;edgeInverse.Invert();
        edgeScene.Game.SetViewport(new Rect(edgeInverse.Transform(new Point(0,0)),edgeInverse.Transform(new Point(1920,1080))));
        for(int f=0;f<70*60;f++)
        {
            edgeScene.Game.Step(1d/60);
            if(f==30*60||f==59*60)Save(Path.Combine(folder,$"desktop-bottom-right-{f/60}.png"),1920,1080,dc=>{dc.PushTransform(new MatrixTransform(edgeView.StageMatrix));edgeScene.Draw(dc);dc.Pop();});
            Assert(edgeScene.Game.LeftFoot.Y+12<=edgeScene.Game.Viewport.Bottom,"Bottom-right pedal escaped viewport");
        }
        Assert(edgeScene.Game.Complete&&edgeScene.Game.Missing.Count==0,"Bottom-right event did not recover");
        // Compile and render every imported pose. This also rejects unsupported SVG features.
        using(var stream=Application.GetResourceStream(new Uri("Assets/guard/sprites.json",UriKind.Relative))!.Stream)
        using(var atlas=System.Text.Json.JsonDocument.Parse(stream))
        {
            foreach(var phase in atlas.RootElement.GetProperty("pet").EnumerateObject())
            foreach(var clip in phase.Value.EnumerateArray())foreach(var pose in clip.EnumerateArray())scene.Art.Markup(pose.GetString()!);
            foreach(var minion in atlas.RootElement.GetProperty("minions").EnumerateArray())
            foreach(var phase in minion.EnumerateObject())foreach(var pose in phase.Value.EnumerateArray())scene.Art.Markup(pose.GetString()!);
        }
        double[] times=[0,.12,.28,.55,.85,1.2,1.7,2.2,2.8,3.2,3.6,4];
        Save(Path.Combine(folder,"minions-melt-contact.png"),1800,900,dc=>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240,238,233)),null,new Rect(0,0,1800,900));
            for(int type=0;type<3;type++)for(int i=0;i<times.Length;i++)
            {
                var pose=scene.Art.Markup(scene.Art.MinionFrame(type,"crushed",times[i]));var bounds=pose.Bounds;
                if(bounds.IsEmpty)continue;
                double s=Math.Min(.65,Math.Min(280/Math.Max(1,bounds.Width),135/Math.Max(1,bounds.Height)));
                dc.PushTransform(new TranslateTransform(i%6*300+150,type*300+i/6*150+75));dc.PushTransform(new ScaleTransform(s,s));dc.PushTransform(new TranslateTransform(-bounds.X-bounds.Width/2,-bounds.Y-bounds.Height/2));
                dc.DrawDrawing(pose);dc.Pop();dc.Pop();dc.Pop();
            }
        });
        for(int type=0;type<3;type++)
        {
            var puddle=scene.Art.Markup(scene.Art.MinionFrame(type,"crushed",2.5)).Bounds;
            var last=scene.Art.Markup(scene.Art.MinionFrame(type,"crushed",3.8)).Bounds;
            Assert(puddle.Width>180&&puddle.Height<70,"Slime did not spread into a low puddle");
            Assert(last.Height<puddle.Height/20&&last.Width<puddle.Width/5,"Puddle did not collapse through geometry");
            Assert(scene.Art.Markup(scene.Art.MinionFrame(type,"crushed",4)).Bounds.IsEmpty,"Final slime frame still contains geometry");
        }
        string motion=Path.Combine(folder,"melt-frames");Directory.CreateDirectory(motion);
        for(int frame=0;frame<96;frame++)Save(Path.Combine(motion,$"frame-{frame:000}.png"),900,600,dc=>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240,238,233)),null,new Rect(0,0,900,600));
            for(int type=0;type<3;type++)
            {
                dc.PushTransform(new TranslateTransform(190,175+type*195));dc.PushTransform(new ScaleTransform(.45,.45));dc.PushTransform(new TranslateTransform(-1300,-1200));
                dc.DrawDrawing(scene.Art.Markup(scene.Art.MinionFrame(type,"crushed",frame/24d)));dc.Pop();dc.Pop();dc.Pop();
            }
        });
        Save(Path.Combine(folder,"walk-directions.png"),1080,420,dc=>
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240,238,233)),null,new Rect(0,0,1080,420));
            for(int type=0;type<3;type++)for(int i=0;i<6;i++)
            {
                dc.PushTransform(new TranslateTransform(type*360+60+i%3*110,190+i/3*190));dc.PushTransform(new ScaleTransform(i<3?.7:-.7,.7));dc.PushTransform(new TranslateTransform(-115,-225));
                dc.DrawDrawing(scene.Art.Markup(scene.Art.MinionFrame(type,"walk",i%3*.17)));dc.Pop();dc.Pop();dc.Pop();
            }
        });
        File.WriteAllText(Path.Combine(folder,"result.txt"),$"PASS: 60 continuous/opaque actors; monitor-wide spread; staggered approach; {arrivals.Count} unique arrivals from all four edges; 3 type mappings; monitor aspect ratios; center-to-feet handoff; full native 19-part event; delayed impacts; hold edges; pause; 148px foot separation; two-carrier rescue; cancellation at 4 damage stages; recovery and completion; all recorded pet/minion SVG poses compile; legless slime puddle, geometric collapse and empty final frame; {captures} event renders and minion contact sheets.");
        return 0;
    }
    public static void SmokeFlow(string folder)
    {
        Directory.CreateDirectory(folder);string progress=Path.Combine(folder,"flow-progress.txt");File.WriteAllText(progress,"");
        var owner=new PetWindow(pollKeys:false){Opacity=0,ShowInTaskbar=false,ShowActivated=false};
        var watch=Stopwatch.StartNew();var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(100)};
        var seen=new HashSet<string>();string previous="";bool paused=false,resumed=false,stomped=false,released=false,restarted=false,arrived=false;
        double pauseAt=0,pauseElapsed=0,stompAt=0;int hitBefore=0;
        var pet=(PetView)owner.Content;
        MenuItem StartItem()=>pet.ContextMenu.Items.OfType<MenuItem>().Single(i=>(string?)i.Header=="S · 小宠物倒计时");
        CountdownWindow? Active()=>owner.OwnedWindows.OfType<CountdownWindow>().SingleOrDefault();
        void Record(string phase)
        {
            if(previous==phase)return;previous=phase;seen.Add(phase);File.AppendAllText(progress,$"{watch.Elapsed.TotalSeconds:0.0}s {phase}\n");
        }
        void Begin()
        {
            StartItem().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));var overlay=Active()!;
            overlay.Left=overlay.Top=-20000;overlay.PollInput=false;
            StartItem().RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));Assert(ReferenceEquals(overlay,Active()),"Repeated start created a second event");
        }
        owner.Loaded+=(_,_)=>{owner.Left=owner.Top=-20000;Record("daily");Begin();timer.Start();};
        owner.Closed+=(_,_)=>timer.Stop();
        timer.Tick+=(_,_)=>
        {
            Assert(watch.Elapsed.TotalSeconds<150,"Full desktop flow timed out");
            var overlay=Active();
            if(overlay==null)
            {
                Assert(pet.GuardBlend==0,"Daily pet remained hidden after event");
                if(!restarted)
                {
                    foreach(string phase in new[]{"countdown","scatter","playing","ending","result"})Assert(seen.Contains(phase),"Flow skipped "+phase);
                    Assert(paused&&resumed&&stomped&&released&&arrived,"Flow did not exercise defense/pause and live reinforcements");
                    Record("daily-restored");restarted=true;Begin();Assert(Active()!.Game==null,"Restart reused damaged guard scene");Active()!.ScatterAndClose();
                }
                else
                {
                    Record("ready-again");timer.Stop();
                    File.WriteAllText(Path.Combine(folder,"flow-result.txt"),"PASS: actual PetWindow menu starts one event; full 5-4-3-2-1 countdown; opaque monitor-wide scatter; continuous handoff and edge arrivals; main pet hides; F/H simulation lands a hit; pause/resume freezes time; 60-second event ends; all parts return; overlay closes; daily pet reappears; event restarts cleanly; early scatter closes and returns to ready.");owner.Close();
                }
                return;
            }
            Record(overlay.Phase);
            if(overlay.Game is not { } game||game.State!="playing"||restarted)return;
            arrived|=game.Actors.Any(a=>a.Id>=60);
            if(game.Elapsed>.6&&!paused)
            {
                Assert(game.Actors.Count==60,"Real countdown handoff lost actors");Assert(pet.GuardBlend>.99,"Main pet did not hide for guard");
                game.TogglePause();paused=true;pauseAt=watch.Elapsed.TotalSeconds;pauseElapsed=game.Elapsed;Record("paused");
            }
            if(paused&&!resumed&&watch.Elapsed.TotalSeconds-pauseAt>1)
            {Assert(game.Elapsed==pauseElapsed,"Paused native game advanced");game.TogglePause();resumed=true;Record("resumed");}
            if(resumed&&!stomped&&game.Elapsed>1.5)
            {
                foreach(var a in game.Actors.Where(a=>!a.Dead))
                {
                    bool left=a.Position.X<600;game.Aim(left,a.Position.X,a.Position.Y);var foot=left?game.LeftFoot:game.RightFoot;
                    if((a.Position-new Point(foot.X,foot.Y)).Length>15)continue;
                    hitBefore=game.Hits;game.Press(left);stomped=true;stompAt=watch.Elapsed.TotalSeconds;break;
                }
            }
            if(stomped&&!released&&watch.Elapsed.TotalSeconds-stompAt>.3)
            {game.Release(true);game.Release(false);Assert(game.Hits>hitBefore,"Real guard scene stomp did not hit");released=true;Record("defended");}
        };
        Application.Current.Run(owner);
    }
}
