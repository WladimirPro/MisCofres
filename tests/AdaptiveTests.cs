using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

static class AdaptiveTests {
    public static void Run(string fixtures) {
        var matcher=new AdaptiveChestMatcher();
        using(var label=new Bitmap(Path.Combine(fixtures,"cofre-label.png")))
        using(var other=new Bitmap(Path.Combine(fixtures,"escudo-label.png"))) {
            foreach(double scale in new[]{1.0,1.25,1.5,1.75,2.0,2.5,3.0}) {
                using(var frame=Render(label,scale,640,360,320,140)) {
                    Rectangle location; double score=matcher.Score(frame,out location);
                    if(score<.80) throw new Exception("HUD scale "+scale+" missed: "+score);
                }
                using(var frame=Render(other,scale,640,360,130,100)) {
                    Rectangle location; double score=matcher.Score(frame,out location);
                    if(score>=.80) throw new Exception("Shield XP counted as chest: "+score);
                }
            }
            // Pixel dimensions model resolution changes independently of HUD scale.
            foreach(Size resolution in new[]{new Size(1920,1080),new Size(2560,1440),new Size(3840,2160),new Size(3440,1440)}) {
                double pixelScale=resolution.Height/1080.0;
                int width=(int)(resolution.Width*.60),height=(int)(resolution.Height*.48);
                using(var physical=Render(label,1.5*pixelScale,width,height,width/2,height/2))
                using(var normalized=new Bitmap((int)Math.Round(width/pixelScale),(int)Math.Round(height/pixelScale))) {
                    using(var g=Graphics.FromImage(normalized)) g.DrawImage(physical,0,0,normalized.Width,normalized.Height);
                    Rectangle box; double score=matcher.Score(normalized,out box);
                    if(score<.80) throw new Exception("Resolution "+resolution+" missed: "+score);
                }
            }
        }
        // This is a real HUD-70 raster, not a resized copy of the HUD-50 fixture.
        using(var hud70=new Bitmap(Path.Combine(fixtures,"cofre-hud70.png"))) {
            foreach(double scale in new[]{1.0,1.25,1.5,2.0}) using(var frame=Render(hud70,scale,640,360,250,130)) {
                Rectangle box; double score=matcher.Score(frame,out box);
                if(score<.80) throw new Exception("Real HUD 70 failed at scale "+scale+": "+score);
            }
        }
        var stable=new StableNotice(); DateTime now=DateTime.UtcNow;
        if(stable.Observe(true,now)) throw new Exception("Single noisy frame accepted");
        if(!stable.Observe(true,now.AddMilliseconds(200))) throw new Exception("Stable notice missed");
        if(stable.Observe(false,now.AddMilliseconds(300))) throw new Exception("Absent notice accepted");
        if(stable.Observe(true,now.AddSeconds(2))) throw new Exception("Old hit carried over after pause");
        Console.WriteLine("PASS: real HUD 50/70 rasters, scaled notices, 1080p/1440p/4K/ultrawide normalization, non-chest XP and stable-frame confirmation.");
    }
    static Bitmap Render(Bitmap label,double scale,int width,int height,int x,int y) {
        var frame=new Bitmap(width,height,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(frame)) {
            g.Clear(Color.FromArgb(35,15,60));
            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(label,x,y,(int)Math.Round(label.Width*scale),(int)Math.Round(label.Height*scale));
        }
        return frame;
    }
}
