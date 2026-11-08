using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using System.Threading;

static class Program {
    public static bool StartWithSettings;
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern IntPtr FindWindow(string name,string title);
    [DllImport("user32.dll")] static extern bool PostMessage(IntPtr window,uint message,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool SetProcessDpiAwarenessContext(IntPtr context);
    [DllImport("user32.dll")] static extern bool SetProcessDPIAware();
    [STAThread] static void Main(string[] args) {
        if (args.Length == 1 && args[0] == "--self-test") { Test(); return; }
        // Use physical client pixels on scaled/high-DPI and mixed-DPI screens.
        try { if(!SetProcessDpiAwarenessContext(new IntPtr(-4))) SetProcessDPIAware(); }
        catch(EntryPointNotFoundException) { SetProcessDPIAware(); }
        StartWithSettings=Array.IndexOf(args,"--settings")>=0;
        bool created;
        using (var mutex = new Mutex(true, "Local\\MisCofresCounterV1", out created)) {
            if (!created) { var window=FindWindow(null,"Mis cofres"); if(window!=IntPtr.Zero) PostMessage(window,0x802A,IntPtr.Zero,IntPtr.Zero); return; }
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            try {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MisCofres", "total.txt");
                Application.Run(new CounterWindow(new CounterStore(path)));
            } catch (Exception ex) { MessageBox.Show(ex.Message, "No se pudo abrir el contador"); }
        }
    }
    static void Test() {
        string dir = Path.Combine(Path.GetTempPath(), "MisCofres-test-" + Guid.NewGuid().ToString("N"));
        string path = Path.Combine(dir, "total.txt");
        try {
            var s = new CounterStore(path);
            if (s.Count != 0) throw new Exception("Initial count");
            s.Set(1); s.Set(2);
            if (new CounterStore(path).Count != 2) throw new Exception("Persistence");
            bool negativeRejected=false;
            try { s.Set(-1); } catch(ArgumentOutOfRangeException) { negativeRejected=true; }
            if(!negativeRejected || s.Count!=2) throw new Exception("Negative count protection");
            s.Reset();
            s.Set(25); s.Set(26);
            if (File.ReadAllText(path + ".bak") != "25") throw new Exception("Backup");
            File.Delete(path);
            if (new CounterStore(path).Count != 26) throw new Exception("Journal recovery");
            File.WriteAllText(path, "invalid");
            if(new CounterStore(path).Count!=26) throw new Exception("Corrupt snapshot recovery");
            File.WriteAllText(Path.Combine(dir, "PASSED"), "OK");
        } catch (Exception ex) {
            File.WriteAllText(Path.Combine(Path.GetTempPath(), "MisCofres-test-failure.txt"), ex.ToString());
            Environment.ExitCode = 1; return;
        }
        foreach(var file in Directory.GetFiles(dir)) File.Delete(file);
        Directory.Delete(dir);
    }
}
