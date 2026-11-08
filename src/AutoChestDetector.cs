using System;
using System.IO;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

sealed class AutoChestDetector : IDisposable {
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left,Top,Right,Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X,Y; }
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,out uint process);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr window,out RECT rect);
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr window,ref POINT point);
    readonly CancellationTokenSource stop=new CancellationTokenSource();
    readonly string folder;
    readonly Action detected;
    readonly ChestSignal signal=new ChestSignal();
    readonly ChestWordMatcher matcher=new ChestWordMatcher();
    public volatile bool Enabled=true;
    volatile AppSettings preferences=new AppSettings();
    public void SetPreferences(AppSettings settings) { preferences=settings.Copy(); Enabled=settings.Automatic; }
    DateTime lastDebug=DateTime.MinValue;
    public AutoChestDetector(string dataFolder,Action onDetected) {
        folder=dataFolder; detected=onDetected;
        Task.Run((Func<Task>)Run);
    }
    void Status(string text) {
        try { Directory.CreateDirectory(folder); File.WriteAllText(Path.Combine(folder,"automatico-estado.txt"),text); } catch { }
    }
    async Task Run() {
        Status("LISTO: reconocimiento local del aviso COFRE. Sin IA, OCR ni conexion.");
        while(!stop.IsCancellationRequested) {
            int delay=90;
            try {
                Rectangle region;
                var settings=preferences;
                if(!Enabled || !GameRegion(settings,out region)) {
                    delay=600;
                } else {
                    using(var capture=new Bitmap(region.Width,region.Height,PixelFormat.Format32bppArgb)) {
                        using(var graphics=Graphics.FromImage(capture)) graphics.CopyFromScreen(region.Location,Point.Empty,region.Size,CopyPixelOperation.SourceCopy);
                        int frameWidth=(int)Math.Round((double)settings.ScanWidth*19.2*100/settings.NoticeScalePercent);
                        int frameHeight=(int)Math.Round((double)settings.ScanHeight*10.8*100/settings.NoticeScalePercent);
                        using(var frame=new Bitmap(Math.Max(32,frameWidth),Math.Max(11,frameHeight),PixelFormat.Format32bppArgb)) {
                            using(var graphics=Graphics.FromImage(frame)) graphics.DrawImage(capture,0,0,frame.Width,frame.Height);
                            Point location; double score=matcher.Score(frame,out location);
                            bool opening=score>=.84;
                            DateTime now=DateTime.UtcNow;
                            if(signal.Observe(opening,now)) {
                                File.WriteAllText(Path.Combine(folder,"automatico-ultima-senal.txt"),now.ToString("o")+" COFRE score="+score.ToString("F3"));
                                frame.Save(Path.Combine(folder,"automatico-ultima-senal.png"),ImageFormat.Png);
                                detected();
                            }
                            if(now-lastDebug>TimeSpan.FromSeconds(1)) {
                                lastDebug=now;
                                File.WriteAllText(Path.Combine(folder,"automatico-lectura.txt"),"COFRE score="+score.ToString("F3"));
                            }
                        }
                    }
                }
            } catch(Exception ex) { Status("ERROR: "+ex.Message); delay=1500; }
            try { await Task.Delay(delay,stop.Token); } catch(OperationCanceledException) { break; }
        }
    }
    static bool GameRegion(AppSettings settings,out Rectangle region) {
        region=Rectangle.Empty;
        IntPtr window=GetForegroundWindow(); uint pid;
        GetWindowThreadProcessId(window,out pid);
        using(var process=Process.GetProcessById((int)pid)) {
            if(!process.ProcessName.StartsWith("FortniteClient",StringComparison.OrdinalIgnoreCase)) return false;
        }
        RECT rect; if(!GetClientRect(window,out rect)) return false;
        var origin=new POINT(); if(!ClientToScreen(window,ref origin)) return false;
        int width=rect.Right-rect.Left, height=rect.Bottom-rect.Top;
        if(width<640||height<400) return false;
        region=new Rectangle(origin.X+(int)(width*(double)settings.ScanX/100),origin.Y+(int)(height*(double)settings.ScanY/100),(int)(width*(double)settings.ScanWidth/100),(int)(height*(double)settings.ScanHeight/100));
        return true;
    }
    public void Dispose() { stop.Cancel(); }
}
