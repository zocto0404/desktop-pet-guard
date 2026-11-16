using System;
using System.IO;
using System.Diagnostics;
internal static class ClosePet {
 [STAThread] static int Main(){int failures=0;foreach(var p in Process.GetProcessesByName("desktop-runtime")){try{string root=Path.GetDirectoryName(p.MainModule.FileName);string manifest=Path.Combine(root,"resources","app","package.json");if(File.Exists(manifest)&&File.ReadAllText(manifest).Contains("desktop-pet")){p.Kill();p.WaitForExit(3000);}}catch{failures++;}finally{p.Dispose();}}return failures>0?1:0;}
}
