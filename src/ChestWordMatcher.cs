using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

// Small fixed HUD label, calibrated from the user's real 1080p chest opening.
sealed class ChestWordMatcher {
    static readonly string[] Shape={
        "................................",
        "................................",
        "....##....##...#.#####..###.....",
        "...#.##..#.##.######.######.....",
        "..##..#.##..#.##..##.####.......",
        "..#.....#...#.##..##.#####......",
        "..#.....#..##.###.###..#........",
        "..#..##.#..##.#...#.##.#........",
        "..####..####..#...#.##.###......",
        "................................",
        "................................"
    };
    readonly uint[] template=new uint[11];
    readonly int expected;
    public ChestWordMatcher() {
        for(int y=0;y<11;y++) for(int x=0;x<32;x++) if(Shape[y][x]=='#') { template[y]|=1u<<x; expected++; }
    }
    static int Bits(uint v) {
        v=v-((v>>1)&0x55555555u);
        v=(v&0x33333333u)+((v>>2)&0x33333333u);
        return (int)((((v+(v>>4))&0x0F0F0F0Fu)*0x01010101u)>>24);
    }
    public double Score(Bitmap image,out Point position) {
        position=Point.Empty;
        if(image.Width<32||image.Height<11) return 0;
        var data=image.LockBits(new Rectangle(0,0,image.Width,image.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);
        byte[] bytes=new byte[image.Width*image.Height*4];
        try { for(int y=0;y<image.Height;y++) Marshal.Copy(IntPtr.Add(data.Scan0,y*data.Stride),bytes,y*image.Width*4,image.Width*4); }
        finally { image.UnlockBits(data); }
        int width=image.Width-31, height=image.Height;
        uint[] windows=new uint[width*height];
        for(int y=0;y<height;y++) {
            uint row=0;
            for(int x=image.Width-1;x>=0;x--) {
                int index=(y*image.Width+x)*4;
                bool white=bytes[index]>155 && bytes[index+1]>155 && bytes[index+2]>155;
                row=(row<<1)|(white?1u:0u);
                if(x<width) windows[y*width+x]=row;
            }
        }
        double best=0;
        for(int y=0;y<=height-11;y++) for(int x=0;x<width;x++) {
            // The first two glyph rows reject almost all scenery cheaply.
            if(Bits(windows[(y+2)*width+x]&template[2])<5 || Bits(windows[(y+3)*width+x]&template[3])<8) continue;
            int intersection=0,observed=0;
            for(int r=0;r<11;r++) {
                uint row=windows[(y+r)*width+x];
                intersection+=Bits(row&template[r]); observed+=Bits(row);
            }
            double score=2.0*intersection/(expected+observed);
            if(score>best) { best=score; position=new Point(x,y); }
        }
        return best;
    }
}
