using System;
using System.Diagnostics;
using System.IO;
internal static class Launcher {
 [STAThread] private static int Main(string[] args) {
  string root=AppDomain.CurrentDomain.BaseDirectory;
  var start=new ProcessStartInfo(Path.Combine(root,"desktop-runtime.exe"));
  // A fixed, offline renderer. Compatibility with Windows AppContainer folders.
  start.Arguments="--no-sandbox"+(Array.IndexOf(args,"--qa")>=0?" --qa":"");
  start.WorkingDirectory=root; start.UseShellExecute=false; start.CreateNoWindow=true;
  using(var process=Process.Start(start)){process.WaitForExit();return process.ExitCode;}
 }
}
