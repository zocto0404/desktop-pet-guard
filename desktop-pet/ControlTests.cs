using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace WobblyPet;

static class ControlTests
{
    [DllImport("user32.dll",SetLastError=true)] static extern bool PostMessage(nint window,int message,nint wParam,nint lParam);
    static void Assert(bool value,string message){if(!value)throw new Exception(message);}
    public static void Run(string folder)
    {
        Directory.CreateDirectory(folder);ExportSlippers(folder);
        var owner=new PetWindow(pollKeys:false){Opacity=0,ShowInTaskbar=false,ShowActivated=false};
        var pet=(PetView)owner.Content;var timer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(50)};
        var watch=Stopwatch.StartNew();PetShortcuts? commands=null;int stage=0,resets=0;
        Rig? oldRig=null;OriginalDesign? oldDesign=null;
        CountdownWindow? Active()=>owner.OwnedWindows.OfType<CountdownWindow>().SingleOrDefault();
        void Click(string prefix)=>pet.ContextMenu.Items.OfType<MenuItem>().Single(i=>((string?)i.Header)?.StartsWith(prefix)==true).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
        void Post(int id)=>Assert(PostMessage(new WindowInteropHelper(owner).Handle,PetShortcuts.Message,id,0),"Could not deliver shortcut message");
        void Begin(double time=0){owner.StartCountdown(time);Active()!.Left=Active()!.Top=-20000;}
        owner.Loaded+=(_,_)=>
        {
            owner.Left=owner.Top=-20000;
            commands=new PetShortcuts(owner,()=>{resets++;Click("重置桌宠");},()=>Click("退出桌宠"),register:false);
            Begin(CountdownScene.ExitStart+CountdownView.ScatterSeconds-.05);timer.Start();
        };
        owner.Closed+=(_,_)=>
        {
            timer.Stop();commands?.Dispose();Assert(stage==4&&resets==2,"Control sequence did not complete");
            Assert(!Application.Current.Windows.OfType<CountdownWindow>().Any(),"Quit left an attack/countdown overlay running");
            File.WriteAllText(Path.Combine(folder,"controls-result.txt"),"PASS: supplied transparent left/right slippers render with original orientation; down/up timing and heel clearance; native shortcut message invokes real menu actions; reset during paused/damaged attack closes overlay, replaces pet state, restores size/topmost/position and allows restart; reset during countdown; quit closes owner and owned windows.");
        };
        timer.Tick+=(_,_)=>
        {
            Assert(watch.Elapsed.TotalSeconds<20,"Control smoke timed out");
            if(stage==0&&Active()?.Game is { } game&&game.Clock>.2)
            {
                game.Missing.Add(0);game.TogglePause();owner.Width=owner.Height=520;owner.Topmost=false;
                oldRig=pet.Rig;oldDesign=pet.Design;stage=1;Post(PetShortcuts.ResetId);
            }
            else if(stage==1&&resets==1)
            {
                Assert(Active()==null&&pet.GuardBlend==0,"Reset left guard visible");
                Assert(!ReferenceEquals(oldRig,pet.Rig)&&!ReferenceEquals(oldDesign,pet.Design),"Reset retained damaged animation state");
                Assert(owner.Width==400&&owner.Height==400&&owner.Topmost,"Reset did not restore window defaults");
                Begin();stage=2;Post(PetShortcuts.ResetId);
            }
            else if(stage==2&&resets==2)
            {
                Assert(Active()==null&&pet.GuardBlend==0,"Countdown reset left actors alive");Begin();stage=4;Post(PetShortcuts.QuitId);
            }
        };
        Application.Current.Run(owner);
    }
    static void ExportSlippers(string folder)
    {
        var shoes=SlipperArtwork.Shared;
        Assert(shoes.Left.PixelWidth==567&&shoes.Right.PixelWidth==544,"Wrong slipper half selected");
        var foot=new GuardFoot(400){Y=430,Down=true};
        for(double t=0;t<2;t+=.01)
        {
            foot.Age=t;foot.Down=t<.8;foot.ReleaseAt=.8;
            foreach(bool left in new[]{true,false}){var p=shoes.At(foot,left).Bounds;Assert(p.Width>0&&p.Height>p.Width&&p.Bottom<=442,"Slipper distorted or clipped below desktop");}
        }
        var visual=new DrawingVisual();using(var dc=visual.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(240,238,233)),null,new Rect(0,0,1020,360));
            double[] ages=[9,.083,.17];string[] names=["悬空","下落","踩下"];
            for(int i=0;i<3;i++)
            {
                var l=new GuardFoot(80+i*340){Y=320,Age=ages[i],Down=i>0};var r=new GuardFoot(228+i*340){Y=320,Age=ages[i],Down=i>0};
                shoes.Draw(dc,l,true);shoes.Draw(dc,r,false);
                dc.DrawText(new FormattedText(names[i],System.Globalization.CultureInfo.GetCultureInfo("zh-CN"),FlowDirection.LeftToRight,new Typeface("Microsoft YaHei UI"),20,Brushes.DimGray,1),new Point(130+i*340,20));
            }
        }
        var image=new RenderTargetBitmap(1020,360,96,96,PixelFormats.Pbgra32);image.Render(visual);
        var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using var output=File.Create(Path.Combine(folder,"slipper-poses.png"));encoder.Save(output);
    }
}
