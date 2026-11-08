using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Windows.Forms;

sealed class CounterWindow : Form {
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; public POINT(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential)] struct SIZE { public int X, Y; public SIZE(int x, int y) { X=x; Y=y; } }
    [StructLayout(LayoutKind.Sequential, Pack=1)] struct BLEND { public byte Op, Flags, Alpha, Format; }
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);
    [DllImport("user32.dll")] static extern IntPtr GetDC(IntPtr h);
    [DllImport("user32.dll")] static extern int ReleaseDC(IntPtr h, IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] static extern bool DeleteObject(IntPtr obj);
    [DllImport("user32.dll", SetLastError=true)] static extern bool UpdateLayeredWindow(IntPtr h, IntPtr dst, ref POINT point, ref SIZE size, IntPtr src, ref POINT origin, int key, ref BLEND blend, int flags);
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int n);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int n, int value);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
    [StructLayout(LayoutKind.Sequential)] struct RawDevice {
        public ushort Page, Usage;
        public uint Flags;
        public IntPtr Target;
    }
    [DllImport("user32.dll", SetLastError=true)] static extern bool RegisterRawInputDevices([In] RawDevice[] devices, uint count, uint size);
    [DllImport("user32.dll", SetLastError=true)] static extern uint GetRawInputData(IntPtr input, uint command, IntPtr data, ref uint size, uint headerSize);
    SideButtonInput sideButton = new SideButtonInput();
    IntPtr rawBuffer;
    readonly uint rawHeaderSize = (uint)(8 + IntPtr.Size * 2);
    readonly CounterStore store;
    readonly Bitmap chest;
    readonly NotifyIcon tray = new NotifyIcon();
    readonly System.Windows.Forms.Timer flash = new System.Windows.Forms.Timer();
    readonly ContextMenuStrip menu = new ContextMenuStrip();
    readonly string positionPath;
    readonly string settingsPath;
    AppSettings preferences;
    SettingsForm settingsWindow;
    ToolStripMenuItem moveItem;
    bool moving, highlighted;
    bool ready;
    AutoChestDetector autoDetector;
    bool automatic=true;
    ToolStripMenuItem automaticItem;
    DateTime lastManual=DateTime.MinValue, lastAutomatic=DateTime.MinValue;
    const int Layered=0x80000, NoActivate=0x08000000, ToolWindow=0x80, ClickThrough=0x20;
    protected override bool ShowWithoutActivation { get { return true; } }
    protected override CreateParams CreateParams {
        get { var p=base.CreateParams; p.ExStyle |= Layered | NoActivate | ClickThrough; return p; }
    }
    public CounterWindow(CounterStore s) {
        store=s;
        settingsPath=Path.Combine(Path.GetDirectoryName(store.Path),"ajustes.xml");
        preferences=AppSettings.Load(settingsPath); automatic=preferences.Automatic;
        using (var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("cofre.png")) {
            if (stream==null) throw new IOException("No se encuentra el PNG del cofre.");
            using (var original=new Bitmap(stream)) {
                int left=original.Width, top=original.Height, right=0, bottom=0;
                for(int y=0;y<original.Height;y++) for(int x=0;x<original.Width;x++) {
                    if(original.GetPixel(x,y).A>12) { left=Math.Min(left,x); top=Math.Min(top,y); right=Math.Max(right,x); bottom=Math.Max(bottom,y); }
                }
                if(right<left || bottom<top) throw new IOException("El PNG esta vacio.");
                chest=original.Clone(new Rectangle(left,top,right-left+1,bottom-top+1),PixelFormat.Format32bppArgb);
            }
        }
        Text="Mis cofres"; FormBorderStyle=FormBorderStyle.None; ShowInTaskbar=true; TopMost=true;
        AutoScaleMode=AutoScaleMode.None; ClientSize=new Size(156,150);
        StartPosition=FormStartPosition.Manual;
        var area=Screen.PrimaryScreen.WorkingArea;
        Location=new Point(area.Right-Width-4, area.Top+156);
        positionPath=Path.Combine(Path.GetDirectoryName(store.Path),"posicion.txt");
        try {
            if(File.Exists(positionPath)) {
                var parts=File.ReadAllText(positionPath).Split(',');
                var candidate=new Rectangle(Int32.Parse(parts[0]),Int32.Parse(parts[1]),Width,Height);
                foreach(var screen in Screen.AllScreens) if(screen.WorkingArea.Contains(candidate)) { Location=candidate.Location; break; }
            }
        } catch { }
        if(preferences.X!=int.MinValue && preferences.Y!=int.MinValue) Location=new Point(preferences.X,preferences.Y);
        preferences.X=Left; preferences.Y=Top;
        ClientSize=new Size(156*preferences.SizePercent/100,150*preferences.SizePercent/100);
        ClampPosition();
        sideButton.SelectedButton=preferences.AddBinding<=2 ? preferences.AddBinding : 0;
        try { Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }
        menu.Items.Add("Ajustes... · F11",null,delegate { OpenSettings(); });
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("+1 cofre · lateral superior",null,delegate { ManualAdd(); });
        automaticItem=new ToolStripMenuItem("Automatico local");
        automaticItem.Checked=automatic;
        automaticItem.Click+=delegate { var next=preferences.Copy(); next.Automatic=!automatic; ApplyPreferences(next); };
        menu.Items.Add(automaticItem);
        menu.Items.Add("-1 cofre · F9",null,delegate { Change(-1); });
        menu.Items.Add("Mostrar / ocultar · F10",null,delegate { Toggle(); });
        moveItem=new ToolStripMenuItem("Mover contador · Ctrl+F10");
        moveItem.Click+=delegate { SetMoving(!moving); }; menu.Items.Add(moveItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Reiniciar total...",null,delegate {
            if(MessageBox.Show("Tu total es de "+store.Count+" cofres.\nSolo esta accion reinicia el progreso. ¿Quieres ponerlo en cero?","Reiniciar total",MessageBoxButtons.YesNo,MessageBoxIcon.Warning,MessageBoxDefaultButton.Button2)==DialogResult.Yes) Save(0);
        });
        menu.Items.Add("Salir",null,delegate { Close(); });
        using(var small=new Bitmap(chest,32,32)) {
            IntPtr iconHandle=small.GetHicon();
            try { using(var temp=Icon.FromHandle(iconHandle)) tray.Icon=(Icon)temp.Clone(); }
            finally { DestroyIcon(iconHandle); }
        }
        tray.ContextMenuStrip=menu; tray.Visible=true; tray.DoubleClick+=delegate { OpenSettings(); };
        flash.Interval=180; flash.Tick+=delegate { highlighted=false; flash.Stop(); Render(); };
        UpdateTray();
    }
    protected override void OnHandleCreated(EventArgs e) {
        base.OnHandleCreated(e);
        bool b=RegisterHotKey(Handle,2,0x4000,0x78);
        bool c=RegisterHotKey(Handle,3,0x4000,0x79);
        bool d=RegisterHotKey(Handle,4,0x4002,0x79);
        bool eReady=RegisterHotKey(Handle,5,0x4000,0x7A);
        bool addReady=BindAdd(preferences.AddBinding);
        if(!(b&&c&&d&&eReady&&addReady)) MessageBox.Show("Otra aplicacion usa alguno de los atajos configurados. Puedes cambiar el boton para sumar en Ajustes o usar el icono junto al reloj.","Atajo ocupado");
        rawBuffer=Marshal.AllocHGlobal(128);
        var device=new RawDevice { Page=1, Usage=2, Flags=0x100, Target=Handle };
        bool rawReady=RegisterRawInputDevices(new RawDevice[] { device },1,(uint)Marshal.SizeOf(typeof(RawDevice)));
        try {
            string folder=Path.GetDirectoryName(store.Path);
            Directory.CreateDirectory(folder);
            string mapping=Path.Combine(folder,"boton.txt");
            if(!File.Exists(settingsPath) && File.Exists(mapping) && File.ReadAllText(mapping).Trim()=="1") { sideButton.SelectedButton=1; preferences.AddBinding=1; }
            File.WriteAllText(Path.Combine(folder,"entrada-estado.txt"),rawReady ? "RAW_INPUT_READY" : "RAW_INPUT_ERROR "+Marshal.GetLastWin32Error());
        } catch { }
        if(!rawReady) MessageBox.Show("No se pudo activar el boton lateral del mouse. Puedes sumar desde el icono junto al reloj.","Mouse no disponible");
    }
    protected override void OnShown(EventArgs e) {
        base.OnShown(e); ready=true; Render();
        if(autoDetector==null) autoDetector=new AutoChestDetector(Path.GetDirectoryName(store.Path),delegate {
            if(IsDisposed || !IsHandleCreated) return;
            try { BeginInvoke((MethodInvoker)delegate {
                if(!automatic || IsDisposed || DateTime.UtcNow-lastManual<TimeSpan.FromSeconds(2.5)) return;
                lastAutomatic=DateTime.UtcNow; Change(1);
            }); } catch(InvalidOperationException) { }
        });
        autoDetector.SetPreferences(preferences);
        if(Program.StartWithSettings) BeginInvoke((MethodInvoker)OpenSettings);
    }
    protected override void OnVisibleChanged(EventArgs e) { base.OnVisibleChanged(e); if(Visible&&ready) Render(); }
    protected override void OnHandleDestroyed(EventArgs e) {
        var device=new RawDevice { Page=1, Usage=2, Flags=1, Target=IntPtr.Zero };
        RegisterRawInputDevices(new RawDevice[] { device },1,(uint)Marshal.SizeOf(typeof(RawDevice)));
        if(rawBuffer!=IntPtr.Zero) { Marshal.FreeHGlobal(rawBuffer); rawBuffer=IntPtr.Zero; }
        for(int i=1;i<=5;i++) UnregisterHotKey(Handle,i);
        base.OnHandleDestroyed(e);
    }
    void ReadMouse(IntPtr input) {
        if(rawBuffer==IntPtr.Zero) return;
        uint size=128;
        uint read=GetRawInputData(input,0x10000003,rawBuffer,ref size,rawHeaderSize);
        if(read==UInt32.MaxValue || read<rawHeaderSize+24 || Marshal.ReadInt32(rawBuffer)!=0) return;
        ushort flags=(ushort)Marshal.ReadInt16(rawBuffer,(int)rawHeaderSize+4);
        if((flags & 0x140)!=0) {
            try { File.WriteAllText(Path.Combine(Path.GetDirectoryName(store.Path),"ultimo-lateral.txt"),DateTime.UtcNow.ToString("o")+" button="+(((flags&0x100)!=0) ? "2" : "1")); } catch { }
        }
        if(sideButton.ProcessRaw(flags)) ManualAdd();
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x00FF) ReadMouse(m.LParam);
        if(m.Msg==0x0312) {
            int id=m.WParam.ToInt32();
            if(id==1) ManualAdd(); else if(id==2) Change(-1); else if(id==3) Toggle(); else if(id==4) SetMoving(!moving); else if(id==5) OpenSettings();
        }
        if(m.Msg==0x802A) { BeginInvoke((MethodInvoker)OpenSettings); }
        if(m.Msg==0x0084 && moving) { m.Result=new IntPtr(2); return; }
        if(m.Msg==0x0232) SavePosition();
        base.WndProc(ref m);
    }
    void SetMoving(bool value) {
        moving=value; moveItem.Checked=value;
        int styles=GetWindowLong(Handle,-20);
        SetWindowLong(Handle,-20,value ? styles & ~ClickThrough : styles | ClickThrough);
        if(value&&!Visible) Show();
        if(!value) SavePosition();
        UpdateTray();
    }
    void SavePosition() {
        try { Directory.CreateDirectory(Path.GetDirectoryName(positionPath)); File.WriteAllText(positionPath,Left+","+Top); preferences.X=Left; preferences.Y=Top; preferences.Save(settingsPath); } catch { }
    }
    bool BindAdd(int binding) {
        UnregisterHotKey(Handle,1);
        return binding<=2 || RegisterHotKey(Handle,1,0x4000,(uint)binding);
    }
    void ClampPosition() {
        foreach(var screen in Screen.AllScreens) if(screen.WorkingArea.Contains(new Rectangle(Location,Size))) return;
        var area=Screen.FromPoint(Location).WorkingArea;
        Location=new Point(Math.Max(area.Left,Math.Min(area.Right-Width,Left)),Math.Max(area.Top,Math.Min(area.Bottom-Height,Top)));
        preferences.X=Left; preferences.Y=Top;
    }
    bool ApplyPreferences(AppSettings value) {
        value.Validate();
        var next=value.Copy();
        if(!BindAdd(next.AddBinding)) {
            BindAdd(preferences.AddBinding);
            MessageBox.Show(settingsWindow,"Ese atajo ya lo usa otra aplicacion. Elige otro boton.","Atajo ocupado"); return false;
        }
        try { next.Save(settingsPath); }
        catch(Exception ex) { BindAdd(preferences.AddBinding); MessageBox.Show(settingsWindow,"No se pudieron guardar los ajustes.\n"+ex.Message); return false; }
        preferences=next; automatic=next.Automatic; automaticItem.Checked=automatic;
        sideButton=new SideButtonInput { SelectedButton=next.AddBinding<=2 ? next.AddBinding : 0 };
        ClientSize=new Size(156*next.SizePercent/100,150*next.SizePercent/100);
        Location=new Point(next.X,next.Y); ClampPosition();
        if(autoDetector!=null) autoDetector.SetPreferences(next);
        UpdateTray(); Render(); return true;
    }
    void OpenSettings() {
        if(settingsWindow!=null&&!settingsWindow.IsDisposed) { settingsWindow.Activate(); return; }
        var current=preferences.Copy(); current.X=Left; current.Y=Top;
        settingsWindow=new SettingsForm(current,ApplyPreferences,CreateFrame);
        settingsWindow.Show(); settingsWindow.Activate();
    }
    void Toggle() { if(Visible) Hide(); else { Show(); Render(); } }
    void ManualAdd() {
        if(automatic && DateTime.UtcNow-lastAutomatic<TimeSpan.FromSeconds(2.5)) return;
        lastManual=DateTime.UtcNow; Change(1);
    }
    void Change(int delta) {
        if(delta>0 && store.Count==Int64.MaxValue) return;
        Save(Math.Max(0,store.Count+delta));
    }
    void Save(long value) {
        try { store.Set(value); highlighted=true; flash.Start(); UpdateTray(); Render(); }
        catch(Exception ex) { MessageBox.Show("No se pudo guardar. El total no cambio.\n"+ex.Message,"Error de guardado"); }
    }
    void UpdateTray() { tray.Text="Cofres: "+store.Count+ (moving ? " · mover activado" : automatic ? " · AUTO local" : " · lateral superior +1"); }
    public Bitmap CreateFrame() {
        return CreateFrame(preferences);
    }
    Bitmap CreateFrame(AppSettings style) {
        float scale=style.SizePercent/100f;
        var frame=new Bitmap((int)(156*scale),(int)(150*scale),PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(frame)) {
            g.Clear(Color.Transparent); g.SmoothingMode=SmoothingMode.AntiAlias;
            g.ScaleTransform(scale,scale);
            g.InterpolationMode=InterpolationMode.HighQualityBicubic; g.PixelOffsetMode=PixelOffsetMode.HighQuality;
            float ratio=Math.Min(112f/chest.Width,96f/chest.Height);
            float w=chest.Width*ratio, h=chest.Height*ratio;
            g.DrawImage(chest,(156-w)/2,6+(96-h)/2,w,h);
            string text=store.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            using(var path=new GraphicsPath()) using(var format=new StringFormat()) {
                format.Alignment=StringAlignment.Center; format.LineAlignment=StringAlignment.Center;
                float em=38;
                do {
                    path.Reset(); path.AddString(text,FontFamily.GenericSansSerif,(int)FontStyle.Bold,em,new RectangleF(4,100,148,46),format);
                    if(path.GetBounds().Width<=144) break;
                    em-=1;
                } while(em>7);
                using(var outline=new Pen(Color.FromArgb(235,10,10,12),4)) {
                    outline.LineJoin=LineJoin.Round; g.DrawPath(outline,path);
                }
                using(var fill=new SolidBrush(highlighted ? Color.FromArgb(255,215,92) : ColorTranslator.FromHtml(style.NumberColor))) g.FillPath(fill,path);
            }
        }
        return frame;
    }
    void Render() {
        if(!ready || !IsHandleCreated || IsDisposed) return;
        using(var frame=CreateFrame()) {
            IntPtr screen=GetDC(IntPtr.Zero), memory=IntPtr.Zero, bitmap=IntPtr.Zero, old=IntPtr.Zero;
            try {
                memory=CreateCompatibleDC(screen); bitmap=frame.GetHbitmap(Color.FromArgb(0)); old=SelectObject(memory,bitmap);
                var point=new POINT(Left,Top); var size=new SIZE(Width,Height); var origin=new POINT(0,0);
                var blend=new BLEND { Op=0, Flags=0, Alpha=(byte)(255*preferences.OpacityPercent/100), Format=1 };
                if(!UpdateLayeredWindow(Handle,screen,ref point,ref size,memory,ref origin,0,ref blend,2))
                    throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            } finally {
                if(old!=IntPtr.Zero) SelectObject(memory,old);
                if(bitmap!=IntPtr.Zero) DeleteObject(bitmap);
                if(memory!=IntPtr.Zero) DeleteDC(memory);
                if(screen!=IntPtr.Zero) ReleaseDC(IntPtr.Zero,screen);
            }
        }
    }
    protected override void Dispose(bool disposing) {
        if(disposing) { if(settingsWindow!=null&&!settingsWindow.IsDisposed) settingsWindow.Close(); if(autoDetector!=null) autoDetector.Dispose(); tray.Visible=false; if(tray.Icon!=null) tray.Icon.Dispose(); tray.Dispose(); flash.Dispose(); menu.Dispose(); chest.Dispose(); }
        base.Dispose(disposing);
    }
}
