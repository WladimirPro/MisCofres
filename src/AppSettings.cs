using System;
using System.IO;
using System.Xml.Serialization;

public sealed class AppSettings {
    public int SizePercent=100, OpacityPercent=100;
    public string NumberColor="#FFFFFF";
    public int X=int.MinValue,Y=int.MinValue;
    // 2/1 = side mouse buttons, 0 = disabled, 116..119 = F5..F8.
    public int AddBinding=2;
    public bool Automatic=true;
    public bool AdaptiveHud=true;
    public int NoticeScalePercent=100;
    public decimal ScanX=60,ScanY=25,ScanWidth=20,ScanHeight=15;
    public AppSettings Copy() { return (AppSettings)MemberwiseClone(); }
    public void Validate() {
        if(SizePercent<50||SizePercent>200 || OpacityPercent<25||OpacityPercent>100)
            throw new ArgumentException("El tamano debe ser de 50 a 200% y la opacidad de 25 a 100%.");
        if(AddBinding!=0&&AddBinding!=1&&AddBinding!=2&&(AddBinding<116||AddBinding>119))
            throw new ArgumentException("Elige un boton valido para sumar.");
        if(NoticeScalePercent<50||NoticeScalePercent>200 || ScanX<0||ScanY<0||ScanWidth<2||ScanHeight<2||ScanX+ScanWidth>100||ScanY+ScanHeight>100)
            throw new ArgumentException("La zona de deteccion debe quedar dentro de la pantalla y medir al menos 2%.");
        try { System.Drawing.ColorTranslator.FromHtml(NumberColor); } catch { throw new ArgumentException("Color no valido."); }
    }
    public static AppSettings Load(string path) {
        if(!File.Exists(path)) return new AppSettings();
        try {
            using(var stream=File.OpenRead(path)) {
                var settings=(AppSettings)new XmlSerializer(typeof(AppSettings)).Deserialize(stream);
                settings.Validate(); return settings;
            }
        } catch { return new AppSettings(); }
    }
    public void Save(string path) {
        Validate(); Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        using(var stream=new FileStream(temp,FileMode.Create,FileAccess.Write,FileShare.None)) {
            new XmlSerializer(typeof(AppSettings)).Serialize(stream,this); stream.Flush(true);
        }
        if(File.Exists(path)) File.Replace(temp,path,path+".bak"); else File.Move(temp,path);
    }
}
