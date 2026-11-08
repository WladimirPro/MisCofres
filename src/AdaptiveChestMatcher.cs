using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Finds text-sized connected groups before normalizing the COFRE glyphs.
// Work depends on the captured pixels, rather than dozens of full-image scales.
sealed class AdaptiveChestMatcher {
    readonly bool[] reference=new bool[25*7];
    public AdaptiveChestMatcher() {
        string[] rows={
            "..##....##...#.#####..###...",
            ".#.##..#.##.######.######...",
            "##..#.##..#.##..##.####.....",
            "#.....#...#.##..##.#####....",
            "#.....#..##.###.###..#......",
            "#..##.#..##.#...#.##.#......",
            "####..####..#...#.##.###...."
        };
        for(int y=0;y<7;y++) for(int x=0;x<25;x++) reference[y*25+x]=rows[y][x]=='#';
    }
    public double Score(Bitmap image,out Rectangle location) {
        location=Rectangle.Empty;
        int width=image.Width,height=image.Height;
        var data=image.LockBits(new Rectangle(0,0,width,height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        byte[] pixels=new byte[width*height*4];
        try { for(int y=0;y<height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),pixels,y*width*4,width*4); }
        finally { image.UnlockBits(data); }
        var mask=new bool[width*height];
        for(int i=0;i<mask.Length;i++) mask[i]=pixels[i*4]>155&&pixels[i*4+1]>155&&pixels[i*4+2]>155;
        double best=0;
        foreach(int gap in new[]{2,4}) {
            foreach(var box in Groups(mask,width,height,gap)) {
                if(box.Height<4||box.Height>48||box.Width<box.Height*2.7||box.Width>box.Height*4.8) continue;
                double score=CompareAdjusted(mask,width,height,box);
                if(score>best) { best=score; location=box; }
            }
        }
        return best;
    }
    double CompareAdjusted(bool[] mask,int width,int height,Rectangle box) {
        double best=0;
        for(int left=-1;left<=1;left++) for(int top=-1;top<=1;top++)
            for(int right=-1;right<=1;right++) for(int bottom=-1;bottom<=1;bottom++) {
                var adjusted=Rectangle.FromLTRB(box.Left+left,box.Top+top,box.Right+right,box.Bottom+bottom);
                if(adjusted.Left<0||adjusted.Top<0||adjusted.Right>width||adjusted.Bottom>height||adjusted.Width<10||adjusted.Height<4) continue;
                best=Math.Max(best,Compare(mask,width,adjusted));
            }
        return best;
    }
    double Compare(bool[] mask,int stride,Rectangle box) {
        double intersection=0,observed=0,expected=0;
        for(int y=0;y<7;y++) for(int x=0;x<25;x++) {
            double left=box.Left+x*box.Width/25.0, right=box.Left+(x+1)*box.Width/25.0;
            double top=box.Top+y*box.Height/7.0,bottom=box.Top+(y+1)*box.Height/7.0;
            double white=0;
            for(int py=(int)Math.Floor(top);py<(int)Math.Ceiling(bottom);py++)
                for(int px=(int)Math.Floor(left);px<(int)Math.Ceiling(right);px++) {
                    double weight=(Math.Min(px+1,right)-Math.Max(px,left))*(Math.Min(py+1,bottom)-Math.Max(py,top));
                    if(mask[py*stride+px]) white+=weight;
                }
            double ink=white/((right-left)*(bottom-top));
            bool expectedInk=reference[y*25+x];
            if(expectedInk) { expected++; intersection+=ink; }
            observed+=ink;
        }
        return observed+expected==0 ? 0 : 2*intersection/(observed+expected);
    }
    static List<Rectangle> Groups(bool[] mask,int width,int height,int gap) {
        var found=new List<Rectangle>(); var visited=new bool[mask.Length]; var queue=new int[mask.Length];
        for(int start=0;start<mask.Length;start++) {
            if(!mask[start]||visited[start]) continue;
            int head=0,tail=1; queue[0]=start; visited[start]=true;
            int left=start%width,right=left,top=start/width,bottom=top;
            while(head<tail) {
                int point=queue[head++],x=point%width,y=point/width;
                left=Math.Min(left,x); right=Math.Max(right,x); top=Math.Min(top,y); bottom=Math.Max(bottom,y);
                for(int ny=Math.Max(0,y-1);ny<=Math.Min(height-1,y+1);ny++)
                    for(int nx=Math.Max(0,x-gap);nx<=Math.Min(width-1,x+gap);nx++) {
                        int n=ny*width+nx;
                        if(mask[n]&&!visited[n]) { visited[n]=true; queue[tail++]=n; }
                    }
            }
            if(tail>=10) found.Add(Rectangle.FromLTRB(left,top,right+1,bottom+1));
        }
        return found;
    }
}
