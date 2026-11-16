using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace WobblyPet;

sealed class PetShortcuts:IDisposable
{
    [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(nint window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(nint window,int id);
    internal const int Message=0x0312,QuitId=0x5051,ResetId=0x5052;
    readonly nint handle;
    readonly HwndSource source;
    readonly Action reset,quit;
    public readonly bool ResetRegistered,QuitRegistered;
    public PetShortcuts(Window owner,Action reset,Action quit,bool register=true)
    {
        this.reset=reset;this.quit=quit;handle=new WindowInteropHelper(owner).Handle;
        source=HwndSource.FromHwnd(handle)!;source.AddHook(Receive);
        if(register){QuitRegistered=RegisterHotKey(handle,QuitId,0x4003,0x51);ResetRegistered=RegisterHotKey(handle,ResetId,0x4003,0x52);}
    }
    nint Receive(nint hwnd,int message,nint wParam,nint lParam,ref bool handled)
    {
        if(message!=Message)return 0;
        if(wParam==QuitId){handled=true;quit();}
        else if(wParam==ResetId){handled=true;reset();}
        return 0;
    }
    public void Dispose()
    {
        if(QuitRegistered)UnregisterHotKey(handle,QuitId);if(ResetRegistered)UnregisterHotKey(handle,ResetId);
        if(!source.IsDisposed)source.RemoveHook(Receive);
    }
}
