using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WobblyPet;

sealed class ApprovedAnimation
{
    sealed record Timing(int[] Intro,int[] Loop,int[] IntroDurations,int[] LoopDurations,int Count);
    readonly Dictionary<string,Timing> timing=new();
    readonly Dictionary<string,BitmapSource> cache=new();
    public ApprovedAnimation()
    {
        using var stream=Application.GetResourceStream(new Uri("Assets/enhanced/timing.json",UriKind.Relative))!.Stream;
        using var doc=JsonDocument.Parse(stream);
        foreach(var item in doc.RootElement.EnumerateObject())
        {
            int[] Read(string name)=>item.Value.GetProperty(name).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            timing[item.Name]=new(Read("intro"),Read("loop"),Read("introDurations"),Read("loopDurations"),item.Value.GetProperty("count").GetInt32());
        }
    }
    static string Side(PetState state)=>state==PetState.Left?"left-support":"right-support";
    public int Index(PetState state,double seconds,bool intro)
    {
        var t=timing[Side(state)];double ms=Math.Max(0,seconds*1000);int[] sequence,durations;
        if(intro&&ms<t.IntroDurations.Sum()){sequence=t.Intro;durations=t.IntroDurations;}
        else {if(intro)ms-=t.IntroDurations.Sum();ms%=t.LoopDurations.Sum();sequence=t.Loop;durations=t.LoopDurations;}
        for(int i=0;i<durations.Length;i++){if(ms<durations[i])return sequence[i];ms-=durations[i];}
        return sequence[^1];
    }
    public BitmapSource Frame(PetState state,int index)
    {
        string key=$"{Side(state)}/frame-{index+1:00}.png";
        if(cache.TryGetValue(key,out var image))return image;
        var raw=new BitmapImage(new Uri("pack://application:,,,/Assets/enhanced/"+key));
        var source=new FormatConvertedBitmap(raw,PixelFormats.Bgra32,null,0);int stride=source.PixelWidth*4;byte[] pixels=new byte[stride*source.PixelHeight];source.CopyPixels(pixels,stride,0);
        // Runtime black matte removal. Original resource PNGs remain byte-identical.
        for(int i=0;i<pixels.Length;i+=4)
        {
            int peak=Math.Max(pixels[i],Math.Max(pixels[i+1],pixels[i+2]));
            if(peak<=12)pixels[i+3]=0;
            else if(peak<64)
            {
                int alpha=(peak-12)*255/52;pixels[i+3]=(byte)alpha;
                for(int c=0;c<3;c++)pixels[i+c]=(byte)Math.Min(255,pixels[i+c]*255/alpha);
            }
        }
        image=BitmapSource.Create(source.PixelWidth,source.PixelHeight,96,96,PixelFormats.Bgra32,null,pixels,stride);image.Freeze();cache[key]=image;return image;
    }
    public void Draw(DrawingContext dc,PetState state,double seconds,bool intro)
    {
        const double scale=1.2;double foot=state==PetState.Left?256:300,anchor=state==PetState.Left?216:248;
        dc.DrawImage(Frame(state,Index(state,seconds,intro)),new Rect(foot-anchor*scale,563-454*scale,512*scale,512*scale));
    }
    public void Verify()
    {
        foreach(var state in new[]{PetState.Left,PetState.Right})
        {
            var t=timing[Side(state)];
            if(t.Loop.Length!=48||t.IntroDurations.Sum()!=1080||t.LoopDurations.Sum()!=6050)throw new Exception("Approved timing mismatch");
            for(double time=1.081;time<90;time+=.037)if(!t.Loop.Contains(Index(state,time,true)))throw new Exception("Normal intro returned during held key");
            for(int i=0;i<t.Count;i++)
            {
                var f=Frame(state,i);byte[] pixel=new byte[4];f.CopyPixels(new Int32Rect(0,0,1,1),pixel,4,0);if(pixel[3]!=0)throw new Exception("Approved frame retained black background");
            }
        }
    }
}
