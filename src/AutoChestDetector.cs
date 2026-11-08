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
    readonly AdaptiveChestMatcher adaptiveMatcher=new AdaptiveChestMatcher();
    readonly StableNotice stable=new StableNotice();
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
        Status("LISTO: reconocimiento local de COFRE, con adaptacion de HUD. Sin IA ni conexion.");
        while(!stop.IsCancellationRequested) {
            int delay=130;
            try {
                Rectangle region; Size normalized;
                var settings=preferences;
                if(!Enabled || !GameRegion(settings,out region,out normalized)) {
                    stable.Clear();
                    delay=600;
                } else {
                    using(var capture=new Bitmap(region.Width,region.Height,PixelFormat.Format32bppArgb)) {
                        using(var graphics=Graphics.FromImage(capture)) graphics.CopyFromScreen(region.Location,Point.Empty,region.Size,CopyPixelOperation.SourceCopy);
                        int frameWidth=normalized.Width;
                        int frameHeight=normalized.Height;
                        using(var frame=new Bitmap(Math.Max(32,frameWidth),Math.Max(11,frameHeight),PixelFormat.Format32bppArgb)) {
                            using(var graphics=Graphics.FromImage(frame)) graphics.DrawImage(capture,0,0,frame.Width,frame.Height);
                            double score; Rectangle matched;
                            if(settings.AdaptiveHud) { score=adaptiveMatcher.Score(frame,out matched); }
                            else { Point location; score=matcher.Score(frame,out location); matched=new Rectangle(location,new Size(32,11)); }
                            DateTime now=DateTime.UtcNow;
                            bool opening=settings.AdaptiveHud ? stable.Observe(score>=.80,now) : score>=.84;
                            if(signal.Observe(opening,now)) {
                                File.WriteAllText(Path.Combine(folder,"automatico-ultima-senal.txt"),now.ToString("o")+" COFRE score="+score.ToString("F3"));
                                var evidence=Rectangle.Intersect(new Rectangle(0,0,frame.Width,frame.Height),Rectangle.FromLTRB(matched.Left-matched.Height*8,matched.Top-matched.Height*4,matched.Right+matched.Height*4,matched.Bottom+matched.Height*2));
                                if(evidence.Width>0&&evidence.Height>0) using(var preview=frame.Clone(evidence,PixelFormat.Format32bppArgb)) preview.Save(Path.Combine(folder,"automatico-ultima-senal.png"),ImageFormat.Png);
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
    static bool GameRegion(AppSettings settings,out Rectangle region,out Size normalized) {
        region=Rectangle.Empty; normalized=Size.Empty;
        IntPtr window=GetForegroundWindow(); uint pid;
        GetWindowThreadProcessId(window,out pid);
        using(var process=Process.GetProcessById((int)pid)) {
            if(!process.ProcessName.StartsWith("FortniteClient",StringComparison.OrdinalIgnoreCase)) return false;
        }
        RECT rect; if(!GetClientRect(window,out rect)) return false;
        var origin=new POINT(); if(!ClientToScreen(window,ref origin)) return false;
        int width=rect.Right-rect.Left, height=rect.Bottom-rect.Top;
        if(width<640||height<400) return false;
        if(settings.AdaptiveHud) {
            region=new Rectangle(origin.X+(int)(width*.35),origin.Y+(int)(height*.12),(int)(width*.60),(int)(height*.48));
            // Normalize by height, preserving glyph proportions on ultrawide monitors.
            normalized=new Size((int)Math.Round(region.Width*1080.0/height),(int)Math.Round(region.Height*1080.0/height));
        } else {
            region=new Rectangle(origin.X+(int)(width*(double)settings.ScanX/100),origin.Y+(int)(height*(double)settings.ScanY/100),(int)(width*(double)settings.ScanWidth/100),(int)(height*(double)settings.ScanHeight/100));
            normalized=new Size((int)Math.Round((double)settings.ScanWidth*19.2*100/settings.NoticeScalePercent),(int)Math.Round((double)settings.ScanHeight*10.8*100/settings.NoticeScalePercent));
        }
        return true;
    }
    public void Dispose() { stop.Cancel(); }
}
