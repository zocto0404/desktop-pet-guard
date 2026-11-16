using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WobblyPet;

// Keep the supplied transparent PNG unchanged. Each foot uses its own half,
// including the original left/right ornament placement and toe direction.
sealed class SlipperArtwork
{
    public static readonly SlipperArtwork Shared=new();
    public readonly BitmapSource Left,Right;
    SlipperArtwork()
    {
        var original=new BitmapImage(new Uri("pack://application:,,,/Assets/guard/slippers.png"));original.Freeze();
        Left=new CroppedBitmap(original,new Int32Rect(162,12,567,1053));Left.Freeze();
        Right=new CroppedBitmap(original,new Int32Rect(774,12,544,1052));Right.Freeze();
    }
    public readonly record struct ShoePose(Rect Bounds);
    public ShoePose At(GuardFoot foot,bool left)
    {
        double y,wide,flat;
        double[][] down=[[-86,48,18],[-74,50,18],[-42,56,15],[0,70,10],[2,92,5],[1,86,7],[-8,76,9],[-18,67,11],[-9,72,9],[-1,78,8],[0,72,9],[-5,68,10],[-2,70,9],[0,70,9],[-1,70,9],[0,70,9],[0,70,9],[0,70,9]];
        if(foot.Age<.75||foot.Down){var p=down[Math.Clamp((int)(foot.Age*24),0,down.Length-1)];y=p[0];wide=p[1];flat=p[2];}
        else{double t=Math.Clamp((foot.Age-Math.Max(.75,foot.ReleaseAt))/.5,0,1),e=1-Math.Pow(1-t,3);y=-68*e;wide=70-22*e;flat=9+9*e;}
        var image=left?Left:Right;
        double width=90*Math.Sqrt(wide/48),height=width*image.PixelHeight/image.PixelWidth*(.68+.32*flat/18);
        // The heel stays close to the impact plane, so the full slipper remains
        // visible at the desktop's bottom edge without changing the hit point.
        return new(new Rect(foot.X-width/2,foot.Y+y-height*.96,width,height));
    }
    public void Draw(DrawingContext dc,GuardFoot foot,bool left)
    {
        dc.DrawImage(left?Left:Right,At(foot,left).Bounds);
    }
}
