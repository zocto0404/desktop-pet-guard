using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;

namespace WobblyPet;

// Polling preserves the desktop pet's nonactivating, click-through overlay.
sealed class GuardInput
{
    [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);
    [DllImport("user32.dll")] static extern bool GetCursorPos(out NativePoint point);
    [StructLayout(LayoutKind.Sequential)] struct NativePoint { public int X,Y; }
    readonly PressEdge left=new(),right=new(),pause=new();
    bool leftHeld,rightHeld; Point cursor;
    static bool Down(int key)=>(GetAsyncKeyState(key)&0x8000)!=0;
    public void Reset()
    {
        leftHeld=Down(0x46);rightHeld=Down(0x48);left.Sample(leftHeld);right.Sample(rightHeld);pause.Sample(Down(0x1b));
        if(GetCursorPos(out var p))cursor=new(p.X,p.Y);
    }
    public void Step(GuardGame game,double dt,FrameworkElement surface,Matrix inverse)
    {
        if(pause.Sample(Down(0x1b)))game.TogglePause();
        bool l=Down(0x46),r=Down(0x48),lp=left.Sample(l),rp=right.Sample(r);
        if(!game.Paused)
        {
            if(GetCursorPos(out var p))
            {
                var screen=new Point(p.X,p.Y);
                if(screen!=cursor)
                {
                    var at=inverse.Transform(surface.PointFromScreen(screen));game.Aim(at.X<600,at.X,at.Y);cursor=screen;
                }
            }
            game.Move(true,(Down(0x44)?1:0)-(Down(0x41)?1:0),(Down(0x53)?1:0)-(Down(0x57)?1:0),dt);
            game.Move(false,(Down(0x4c)?1:0)-(Down(0x4a)?1:0),(Down(0x4b)?1:0)-(Down(0x49)?1:0),dt);
            if(lp)game.Press(true);if(rp)game.Press(false);
        }
        if(!l&&leftHeld)game.Release(true);if(!r&&rightHeld)game.Release(false);leftHeld=l;rightHeld=r;
    }
}
