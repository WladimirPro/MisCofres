using System;
using System.Drawing;
using System.Windows.Forms;

sealed class SettingsForm : Form {
    readonly Func<AppSettings,bool> apply;
    readonly Func<AppSettings,Bitmap> render;
    readonly AppSettings original;
    readonly NumericUpDown size,opacity,x,y,notice,scanX,scanY,scanWidth,scanHeight;
    readonly CheckBox automatic,adaptive;
    readonly ComboBox binding;
    readonly PictureBox preview=new PictureBox();
    readonly Button colorButton;
    readonly Label feedback;
    Color numberColor;
    bool initialized;
    readonly int[] bindings={2,1,116,117,118,119,0};
    static readonly Color Surface=Color.FromArgb(28,35,51);
    public SettingsForm(AppSettings settings,Func<AppSettings,bool> onApply,Func<AppSettings,Bitmap> onRender) {
        original=settings.Copy(); apply=onApply; render=onRender;
        Text="Ajustes · Mis cofres"; ClientSize=new Size(620,550);
        Font=new Font("Segoe UI",10); BackColor=Color.FromArgb(17,23,36); ForeColor=Color.White;
        FormBorderStyle=FormBorderStyle.FixedDialog; MaximizeBox=false; MinimizeBox=false;
        StartPosition=FormStartPosition.CenterScreen; TopMost=true;
        var title=new Label { Text="Hazlo a tu gusto",Font=new Font("Segoe UI",20,FontStyle.Bold),AutoSize=true,Location=new Point(24,18) };
        var intro=new Label { Text="Tus ajustes se guardan en este PC. F11 abre este menu.",AutoSize=true,Location=new Point(26,61),ForeColor=Color.LightSteelBlue };
        Controls.Add(title); Controls.Add(intro);
        var tabs=new TabControl { Location=new Point(24,98),Size=new Size(572,365) };
        TabPage appearance=Page("Apariencia"), controls=Page("Controles"), detection=Page("Automatico");
        tabs.TabPages.AddRange(new TabPage[]{appearance,controls,detection}); Controls.Add(tabs);
        AddLabel(appearance,"Tamano del contador (%)",20,22);
        size=Number(appearance,50,200,settings.SizePercent,234,18,85);
        AddLabel(appearance,"Opacidad (%)",20,66);
        opacity=Number(appearance,25,100,settings.OpacityPercent,234,62,85);
        AddLabel(appearance,"Color del numero",20,110);
        numberColor=ColorTranslator.FromHtml(settings.NumberColor);
        colorButton=Button(appearance,"Elegir color",234,105,108);
        colorButton.BackColor=numberColor; colorButton.ForeColor=numberColor.GetBrightness()>.5?Color.Black:Color.White;
        colorButton.Click+=delegate {
            using(var picker=new ColorDialog { Color=numberColor,FullOpen=true }) if(picker.ShowDialog(this)==DialogResult.OK) {
                numberColor=picker.Color; colorButton.BackColor=numberColor; colorButton.ForeColor=numberColor.GetBrightness()>.5?Color.Black:Color.White; UpdatePreview();
            }
        };
        AddLabel(appearance,"Posicion en pantalla",20,158);
        AddLabel(appearance,"X",20,197); x=Number(appearance,-50000,50000,settings.X,44,191,100);
        AddLabel(appearance,"Y",160,197); y=Number(appearance,-50000,50000,settings.Y,185,191,100);
        var below=Button(appearance,"Debajo del minimapa",20,239,175);
        below.Click+=delegate { Place(0); };
        var left=Button(appearance,"Arriba izquierda",203,239,148);
        left.Click+=delegate { Place(1); };
        AddLabel(appearance,"Tambien puedes moverlo con Ctrl+F10 y arrastrar.",20,292);
        var previewLabel=new Label { Text="VISTA PREVIA",AutoSize=true,Location=new Point(378,24),ForeColor=Color.LightSteelBlue };
        appearance.Controls.Add(previewLabel);
        preview.SetBounds(365,55,175,175); preview.SizeMode=PictureBoxSizeMode.Zoom;
        preview.BackColor=Color.FromArgb(58,69,90); appearance.Controls.Add(preview);
        AddLabel(appearance,"Sin fondo al jugar",383,241);

        AddLabel(controls,"Boton para sumar un cofre",20,25);
        binding=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Location=new Point(20,57),Size=new Size(340,30) };
        binding.Items.AddRange(new object[]{"Lateral superior del mouse (Adelante)","Lateral inferior del mouse (Atras)","F5","F6","F7","F8","Desactivado"});
        binding.SelectedIndex=Array.IndexOf(bindings,settings.AddBinding); controls.Controls.Add(binding);
        AddLabel(controls,"Los botones no elegidos siguen libres dentro del juego.",20,104);
        AddLabel(controls,"F9        Restar un cofre",20,154);
        AddLabel(controls,"F10      Mostrar / ocultar",20,192);
        AddLabel(controls,"F11      Abrir ajustes",20,230);
        AddLabel(controls,"Ctrl+F10      Activar / terminar de mover",20,268);

        automatic=new CheckBox { Text="Contar automaticamente al abrir cofres",Checked=settings.Automatic,AutoSize=true,Location=new Point(20,20) }; detection.Controls.Add(automatic);
        adaptive=new CheckBox { Text="Adaptar automaticamente al HUD y a la resolucion",Checked=settings.AdaptiveHud,AutoSize=true,Location=new Point(20,52) }; detection.Controls.Add(adaptive);
        var help=new Label { Text="Busca texto de varios tamanos en una zona amplia. Avisos en espanol.\nSi no lo detecta, desactiva esta opcion para ajustar la zona a mano.",Location=new Point(20,83),Size=new Size(520,40),ForeColor=Color.LightSteelBlue }; detection.Controls.Add(help);
        AddLabel(detection,"Escala manual del aviso (100% = original)",20,138);
        notice=Number(detection,50,200,settings.NoticeScalePercent,390,132,100);
        AddLabel(detection,"Zona manual (% de la ventana del juego)",20,179);
        AddLabel(detection,"X",20,217); scanX=Number(detection,0,98,settings.ScanX,44,211,78);
        AddLabel(detection,"Y",145,217); scanY=Number(detection,0,98,settings.ScanY,169,211,78);
        AddLabel(detection,"Ancho",267,217); scanWidth=Number(detection,2,100,settings.ScanWidth,324,211,78);
        AddLabel(detection,"Alto",423,217); scanHeight=Number(detection,2,100,settings.ScanHeight,467,211,78);
        foreach(var n in new[]{scanX,scanY,scanWidth,scanHeight}) { n.DecimalPlaces=1; n.Increment=.5m; }
        var reset=Button(detection,"Restaurar detector original",20,251,230);
        reset.Click+=delegate { adaptive.Checked=false; notice.Value=100; scanX.Value=60; scanY.Value=25; scanWidth.Value=20; scanHeight.Value=15; };
        EventHandler updateManual=delegate { foreach(var n in new[]{notice,scanX,scanY,scanWidth,scanHeight}) n.Enabled=!adaptive.Checked; reset.Enabled=!adaptive.Checked; };
        adaptive.CheckedChanged+=updateManual; updateManual(null,EventArgs.Empty);
        var detail=new Label { Text="No cambia los ajustes de Fortnite. Los textos muy pequenos o\nlos avisos tapados pueden requerir una correccion manual.",Location=new Point(20,292),Size=new Size(520,38),ForeColor=Color.LightSteelBlue }; detection.Controls.Add(detail);

        feedback=new Label { Location=new Point(24,472),Size=new Size(570,20),ForeColor=Color.FromArgb(255,210,100) }; Controls.Add(feedback);
        var cancel=Button(this,"Cerrar",274,504,90); cancel.Click+=delegate { Close(); };
        var save=Button(this,"Aplicar",372,504,90); save.Click+=delegate { Save(false); };
        var done=Button(this,"Guardar y cerrar",470,504,126); done.BackColor=Color.FromArgb(247,195,63); done.ForeColor=Color.Black; done.Click+=delegate { Save(true); };
        CancelButton=cancel;
        foreach(var n in new[]{size,opacity}) n.ValueChanged+=delegate { UpdatePreview(); };
        initialized=true; UpdatePreview();
        FormClosed+=delegate { if(preview.Image!=null) preview.Image.Dispose(); };
    }
    TabPage Page(string text) { return new TabPage(text) { BackColor=Surface,ForeColor=Color.White }; }
    void AddLabel(Control parent,string text,int left,int top) { parent.Controls.Add(new Label { Text=text,AutoSize=true,Location=new Point(left,top) }); }
    NumericUpDown Number(Control parent,decimal min,decimal max,decimal value,int left,int top,int width) {
        var number=new NumericUpDown { Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,value)),Location=new Point(left,top),Size=new Size(width,30) }; parent.Controls.Add(number); return number;
    }
    Button Button(Control parent,string text,int left,int top,int width) {
        var button=new Button { Text=text,Location=new Point(left,top),Size=new Size(width,32),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(55,68,92),ForeColor=Color.White }; button.FlatAppearance.BorderSize=0; parent.Controls.Add(button); return button;
    }
    AppSettings Values() {
        var s=original.Copy();
        s.SizePercent=(int)size.Value; s.OpacityPercent=(int)opacity.Value; s.NumberColor=ColorTranslator.ToHtml(numberColor);
        s.X=(int)x.Value; s.Y=(int)y.Value; s.AddBinding=bindings[binding.SelectedIndex]; s.Automatic=automatic.Checked;
        s.AdaptiveHud=adaptive.Checked;
        s.NoticeScalePercent=(int)notice.Value; s.ScanX=scanX.Value; s.ScanY=scanY.Value; s.ScanWidth=scanWidth.Value; s.ScanHeight=scanHeight.Value;
        return s;
    }
    void UpdatePreview() {
        if(!initialized) return;
        var values=Values();
        var previous=preview.Image;
        using(var frame=render(values)) {
            var faded=new Bitmap(frame.Width,frame.Height);
            using(var graphics=Graphics.FromImage(faded)) using(var attributes=new System.Drawing.Imaging.ImageAttributes()) {
                var matrix=new System.Drawing.Imaging.ColorMatrix(); matrix.Matrix33=values.OpacityPercent/100f;
                attributes.SetColorMatrix(matrix);
                graphics.DrawImage(frame,new Rectangle(0,0,frame.Width,frame.Height),0,0,frame.Width,frame.Height,GraphicsUnit.Pixel,attributes);
            }
            preview.Image=faded;
        }
        if(previous!=null) previous.Dispose();
    }
    public void RefreshCountPreview() { UpdatePreview(); }
    void Place(int where) {
        var area=Screen.PrimaryScreen.WorkingArea; int width=(int)(156*size.Value/100);
        x.Value=where==0 ? area.Right-width-4 : area.Left+24;
        y.Value=where==0 ? area.Top+156 : area.Top+24;
    }
    void Save(bool close) {
        try {
            var settings=Values(); settings.Validate();
            if(!apply(settings)) { feedback.Text="No se aplicaron los cambios. Revisa el aviso."; return; }
            feedback.Text="Ajustes guardados.";
            if(close) Close();
        } catch(Exception ex) { feedback.Text=ex.Message; }
    }
}
