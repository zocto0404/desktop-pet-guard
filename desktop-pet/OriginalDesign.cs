using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WobblyPet;

// Original artwork, independently authored left action and interruptible assembly.
sealed class OriginalDesign
{
    public readonly BitmapSource Original;
    readonly BitmapSource collar;
    readonly ApprovedAnimation approved=new();
    readonly Dictionary<int,BitmapSource> idleBases=new();
    bool singleRecovery,singleIntro;
    readonly Dictionary<string, Cutout> layers = new();
    readonly Dictionary<string, PartPose> current=new(), starts=new();
    readonly Dictionary<string,(double X,double Y,double Spin)> scatterVelocity=new();
    readonly Random scatterRandom=new(91);
    Point[] pinkShape=[],pinkStart=[];
    static readonly HashSet<string> LooseParts=["arm","cup","hat","bow","leftleg","rightleg","jacket0","jacket1","collar"];
    double transitionDuration;
    bool assembling;
    double slimeAmount,slimeStart;
    public double ActionTime { get; private set; }
    public double AssemblyProgress { get; private set; } = 1;
    public readonly record struct PartPose(double X,double Y,double Angle=0,double Sx=1,double Sy=1);
    BitmapSource noArm=null!;
    PetState previous = PetState.Normal;
    const double Scale = .6, Left = 60, Top = 85.4;
    public sealed class Cutout
    {
        public required BitmapSource Image;
        public Rect Bounds;
    }
    public OriginalDesign()
    {
        Original = new BitmapImage(new Uri("pack://application:,,,/Assets/reference/normal.png")); Original.Freeze();
        collar = new BitmapImage(new Uri("pack://application:,,,/Assets/reference/collar.png")); collar.Freeze();
        SplitOriginal();
        foreach(var (name,part) in layers) current[name]=Home(part);
        pinkShape=BodyOutline(current["body"]);pinkStart=(Point[])pinkShape.Clone();
    }
    void SplitOriginal()
    {
        var source=new FormatConvertedBitmap(Original,PixelFormats.Bgra32,null,0);
        int w=source.PixelWidth,h=source.PixelHeight,stride=w*4;
        byte[] data=new byte[stride*h]; source.CopyPixels(data,stride,0);
        string[] names=["body","jacket0","jacket1","hat","eye0","eye1","pupil0","pupil1","mouth","bow","arm","cup","leftleg","rightleg"];
        var maps=names.ToDictionary(n=>n,_=>new byte[data.Length]);
        for(int y=0;y<h;y++) for(int x=0;x<w;x++)
        {
            int i=y*stride+x*4; if(data[i+3]==0)continue;
            int b=data[i],g=data[i+1],r=data[i+2]; bool blue=b>135&&r<145&&g<145, white=r>235&&g>235&&b>235, gray=Math.Abs(r-g)<8&&Math.Abs(g-b)<8&&r>180;
            bool pupil=((x-218)*(x-218)+(y-289)*(y-289)<32*32||(x-294)*(x-294)+(y-289)*(y-289)<32*32)&&!(r==255&&g==255&&b==255);
            string name="body";
            // The supplied two-lobed collar is drawn as part of the jacket, below.
            if(x>=175&&x<=385&&y>=390&&y<=455&&white)
            {var target=maps["body"];target[i]=139;target[i+1]=88;target[i+2]=222;target[i+3]=255;continue;}
            if(y<218&&x>270&&(r!=222||g!=88||b!=139))name="hat";
            else if(x<120&&y>=390&&y<520&&blue)name="arm";
            else if(y>=390&&y<565&&((x>=650)||(x>=540&&blue)))name="cup";
            else if(y>=590&&blue)name=x<280?"leftleg":"rightleg";
            else if(x>=150&&x<=387&&y>=219&&y<=354&&(r!=222||g!=88||b!=139))name=pupil?(x<260?"pupil0":"pupil1"):(x<270?"eye0":"eye1");
            else if(x>=255&&x<=316&&y>=356&&y<=389&&(r!=222||g!=88||b!=139))name="mouth";
            else if(x>=228&&x<=332&&y>=420&&y<=479&&blue)name="bow";
            if(name=="body"&&y>=350&&(x<230||x>=335)&&r>180&&g>95&&b>130)name=x<280?"jacket0":"jacket1";
            Array.Copy(data,i,maps[name],i,4);
            if(name is "eye0" or "eye1" or "pupil0" or "pupil1" or "mouth" or "bow")
            {
                // The flat pink surface beneath detached facial cutouts.
                var target=maps["body"]; target[i]=139;target[i+1]=88;target[i+2]=222;target[i+3]=255;
                if(name.StartsWith("pupil")) { var eye=maps[x<270?"eye0":"eye1"]; eye[i]=eye[i+1]=eye[i+2]=eye[i+3]=255; }
            }
        }
        foreach(var (name,pixels) in maps)
        {
            int x0=w,y0=h,x1=0,y1=0;
            for(int y=0;y<h;y++) for(int x=0;x<w;x++)if(pixels[y*stride+x*4+3]>0){x0=Math.Min(x0,x);y0=Math.Min(y0,y);x1=Math.Max(x1,x);y1=Math.Max(y1,y);}
            if(x1<x0)continue;
            if(name=="eye0"){x0=152;y0=219;x1=285;y1=352;}
            if(name=="eye1"){x0=252;y0=219;x1=385;y1=352;}
            int cw=x1-x0+1,ch=y1-y0+1;byte[] crop=new byte[cw*ch*4];
            for(int y=0;y<ch;y++)Array.Copy(pixels,(y0+y)*stride+x0*4,crop,y*cw*4,cw*4);
            var bmp=BitmapSource.Create(cw,ch,96,96,PixelFormats.Bgra32,null,crop,cw*4);bmp.Freeze();
            layers[name]=new(){Image=bmp,Bounds=new Rect(x0,y0,cw,ch)};
        }
        byte[] noArmPixels=(byte[])data.Clone();
        for(int i=0;i<data.Length;i+=4)if(maps["arm"][i+3]>0)noArmPixels[i+3]=0;
        noArm=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,noArmPixels,stride);noArm.Freeze();
        idleBases[0]=Original;
        for(int flags=1;flags<8;flags++)
        {
            byte[] pixels=(byte[])data.Clone();
            for(int i=0;i<pixels.Length;i+=4)
            {
                if(((flags&1)!=0&&maps["arm"][i+3]>0)||((flags&2)!=0&&maps["cup"][i+3]>0))pixels[i+3]=0;
                if((flags&4)!=0&&(maps["eye0"][i+3]>0||maps["eye1"][i+3]>0||maps["pupil0"][i+3]>0||maps["pupil1"][i+3]>0))
                {pixels[i]=139;pixels[i+1]=88;pixels[i+2]=222;pixels[i+3]=255;}
            }
            var bitmap=BitmapSource.Create(w,h,96,96,PixelFormats.Bgra32,null,pixels,stride);bitmap.Freeze();idleBases[flags]=bitmap;
        }
        layers["collar"]=new(){Image=collar,Bounds=new Rect(182,398,191,58)};
    }
    static PartPose Home(Cutout p)=>new(p.Bounds.Left+p.Bounds.Width/2,p.Bounds.Top+p.Bounds.Height/2);
    static double Mix(double a,double b,double t)=>a+(b-a)*t;
    static Point Rotate(Point p,double degrees)
    {
        double r=degrees*Math.PI/180,c=Math.Cos(r),s=Math.Sin(r);return new(p.X*c-p.Y*s,p.X*s+p.Y*c);
    }
    static double Track(double time,params double[] values)
    {
        // Periodic Catmull-Rom tracks. The last segment joins the first at equal velocity.
        double phase=((time/3.2)%1+1)%1*values.Length;int j=(int)phase,n=values.Length;double u=phase-j;
        double a=values[(j+n-1)%n],b=values[j],c=values[(j+1)%n],d=values[(j+2)%n];
        return .5*((2*b)+(-a+c)*u+(2*a-5*b+4*c-d)*u*u+(-a+3*b-3*c+d)*u*u*u);
    }
    (double Angle,double X,double Y,double Sx,double Sy) Body(double t)=>
        (Track(t,-16,-22,-29,-33,-24,-15,-26,-31,-20,-15),Track(t,-42,-52,-65,-72,-54,-38,-60,-68,-48,-38),
         Track(t,6,16,32,40,20,4,24,36,12,3),1,1);
    Point World(Point source,double time)
    {
        var b=Body(time);var q=Rotate(new((source.X-330)*b.Sx,(source.Y-350)*b.Sy),b.Angle);return new(330+b.X+q.X,350+b.Y+q.Y);
    }
    (Point Root,Point Knee,Point Ankle,Point Toe) Support(double t)
    {
        var root=World(new(235+Track(t,0,-4,-9,-11,3,6,-7,-10,4,1),598+Track(t,0,2,5,6,-2,-3,4,5,-2,0)),t);
        var knee=new Point(Track(t,220,205,185,178,208,228,195,182,215,228),Track(t,680,688,704,712,687,675,695,707,682,675));
        return(root,knee,new(229,766),new(200,788));
    }
    (Point Root,Point Knee,Point Ankle,Point Toe) Kick(double t)
    {
        var root=World(new(375+Track(t,0,5,10,12,-3,-6,8,10,-4,0),605+Track(t,0,-2,-4,-6,2,3,-3,-5,2,0)),t);
        var v=Rotate(new(165,0),Track(t,30,10,-15,-25,15,32,0,-22,15,28));
        var bend=Rotate(new(0,Track(t,18,25,32,35,24,18,29,33,20,18)),Track(t,30,10,-15,-25,15,32,0,-22,15,28));
        var ankle=new Point(root.X+v.X,root.Y+v.Y);
        return(root,new(root.X+v.X*.5+bend.X,root.Y+v.Y*.5+bend.Y),ankle,new(ankle.X+24,ankle.Y-13));
    }
    Dictionary<string,PartPose> LeftPose(double t)
    {
        var result=new Dictionary<string,PartPose>();var body=Body(t);
        foreach(var (name,part) in layers)
        {
            var home=Home(part);var q=World(new(home.X,home.Y),t);result[name]=new(q.X,q.Y,body.Angle,body.Sx,body.Sy);
        }
        void Feature(string name,double dx,double dy,double angle,double sx=1,double sy=1)
        {
            var home=Home(layers[name]);var q=World(new(home.X+dx,home.Y+dy),t);result[name]=new(q.X,q.Y,body.Angle+angle,sx,sy);
        }
        // Features slide with different phase lags, instead of riding one rigid face plane.
        double e0x=Track(t,0,-4,-10,-13,3,7,-8,-12,5,2),e0y=Track(t,-2,-5,-10,-12,3,6,-8,-11,4,1);
        double e1x=Track(t,0,-2,-7,-9,5,8,-5,-8,6,3),e1y=Track(t,2,5,10,12,-4,-6,8,11,-3,-1);
        Feature("eye0",e0x,e0y,Track(t,-2,-4,-7,-9,3,5,-6,-8,4,1));
        Feature("eye1",e1x,e1y,Track(t,2,4,7,9,-3,-5,6,8,-4,-1));
        Feature("pupil0",e0x+Track(t,0,4,8,10,-4,-6,6,9,-5,-1),e0y+Track(t,0,2,4,5,-2,-3,3,4,-2,0),0);
        Feature("pupil1",e1x+Track(t,0,3,7,9,-3,-5,5,8,-4,0),e1y+Track(t,0,2,3,4,-2,-3,2,3,-2,0),0);
        Feature("mouth",0,0,Track(t,0,2,5,6,-2,-3,3,5,-2,0));
        Feature("bow",Track(t,0,-1,-3,-4,1,3,-2,-4,2,0),Track(t,0,1,2,2,-1,-2,1,2,-1,0),Track(t,-2,-5,-10,-12,5,9,-8,-11,7,0));
        void Limb(string name,Point root,Point sourceAnchor,double a,double sx=1,double sy=1)
        {
            var home=Home(layers[name]);var offset=Rotate(new((home.X-sourceAnchor.X)*sx,(home.Y-sourceAnchor.Y)*sy),a);result[name]=new(root.X+offset.X,root.Y+offset.Y,a,sx,sy);
        }
        Limb("arm",World(new(108+Track(t,0,-2,-5,-6,1,3,-4,-5,2,0),419+Track(t,0,-7,-15,-20,5,9,-12,-18,6,1)),t),new(108,419),body.Angle+Track(t,30,50,85,100,55,20,70,95,45,25));
        Limb("cup",World(new(548+Track(t,0,2,4,5,-1,-3,3,4,-2,0),416+Track(t,0,6,12,16,-4,-7,10,14,-5,-1)),t),new(548,416),Track(t,0,3,7,10,-5,-7,5,9,-5,0));
        Limb("hat",World(new(440+Track(t,0,4,12,16,-6,-12,9,15,-8,-2),175+Track(t,0,-2,-5,-7,2,4,-4,-6,3,1)),t),new(440,175),body.Angle+Track(t,4,9,16,22,-8,-14,12,20,-10,-2));
        var support=Support(t);var kick=Kick(t);
        void Leg(string name,Point start,Point end)
        {
            var part=layers[name];double length=(end-start).Length;double a=Math.Atan2(end.Y-start.Y,end.X-start.X)*180/Math.PI-90;
            result[name]=new((start.X+end.X)/2,(start.Y+end.Y)/2,a,1,length/part.Bounds.Height);
        }
        Leg("leftleg",support.Root,support.Toe);Leg("rightleg",kick.Root,kick.Toe);
        return result;
    }
    Dictionary<string,PartPose> RightPose(Rig rig)
    {
        var map=new Dictionary<string,string>{{"body","body"},{"hat","hat"},{"eye0","white0"},{"eye1","white1"},{"pupil0","pupil0"},{"pupil1","pupil1"},{"mouth","mouth"},{"bow","bow"},{"arm","arm"},{"cup","cup"},{"leftleg","kick"},{"rightleg","rlower"}};
        var result=new Dictionary<string,PartPose>();
        foreach(var (name,id) in map)
        {
            var p=rig.Current[id];var sprite=rig.Sprites[id];var corners=Rig.Corners(sprite,p);double cx=corners.Average(q=>q.X),cy=corners.Average(q=>q.Y);
            result[name]=new((cx-Left)/Scale,(cy-Top)/Scale,p.Angle,sprite.Width*Math.Abs(p.Sx)/(layers[name].Bounds.Width*Scale),sprite.Height*Math.Abs(p.Sy)/(layers[name].Bounds.Height*Scale));
            if(name=="leftleg")result[name]=result[name] with{Angle=p.Angle-90,Sx=.8,Sy=sprite.Width*Math.Abs(p.Sx)/(layers[name].Bounds.Height*Scale)};
            if(name=="rightleg")result[name]=result[name] with{Sx=.8};
        }
        var body=result["body"];var home=Home(layers["body"]);
        foreach(string name in new[]{"jacket0","jacket1","collar"})
        {
            var p=Home(layers[name]);var d=Rotate(new((p.X-home.X)*body.Sx,(p.Y-home.Y)*body.Sy),body.Angle);
            result[name]=new(body.X+d.X,body.Y+d.Y,body.Angle,body.Sx,body.Sy);
        }
        return result;
    }
    Point[] BodyOutline(PartPose pose)
    {
        Point[] points=[new(109,530),new(108,470),new(101,410),new(96,355),new(73,300),new(109,170),new(192,114),new(260,70),new(384,63),new(455,110),new(520,145),new(568,225),new(563,350),new(550,410),new(535,470),new(529,529),new(505,575),new(415,620),new(338,620),new(289,603),new(200,621),new(170,595),new(123,575),new(107,562),new(109,530)];
        var home=Home(layers["body"]);
        return points.Select(p=>{var d=Rotate(new((p.X-home.X)*pose.Sx,(p.Y-home.Y)*pose.Sy),pose.Angle);return new Point(pose.X+d.X,pose.Y+d.Y);}).ToArray();
    }
    static Point[] PuddleOutline(double time)
    {
        double breathe=Math.Sin(time*1.8),sway=Math.Sin(time*1.2);
        return [new(34,778),new(16,754),new(58,730),new(104,730),new(123,680+breathe*3),new(180,662),new(233,675+sway*4),new(268,635-breathe*4),new(338,631),new(374,665),new(435,651),new(482,681+sway*3),new(495,716),new(553,708),new(615,734),new(632,763),new(656,789),new(606,796),new(552,796),new(410,795),new(214,796),new(103,796),new(61,796),new(22,793),new(34,778)];
    }
    Dictionary<string,PartPose> SlimePose(double t)
    {
        double bob=Math.Sin(t*1.8)*3,look=Math.Sin(t*.65)*5;
        return layers.ToDictionary(p=>p.Key,p=>p.Key switch
        {
            "body"=>new PartPose(330,718,0,1.2,.23),
            "eye0"=>new PartPose(280,702+bob,0,.78,.78),
            "eye1"=>new PartPose(380,699-bob*.45,0,.78,.78),
            "pupil0"=>new PartPose(269+look,705+bob,0,.78,.78),
            "pupil1"=>new PartPose(369+look,702-bob*.45,0,.78,.78),
            "mouth"=>new PartPose(330,762+bob*.4,Math.Sin(t*1.4)*3,.85,.85),
            _=>new PartPose(330+(Home(p.Value).X-330)*.3,777,0,.015,.015)
        });
    }
    public void Step(Rig rig,double dt)
    {
        var state=rig.State;
        if(state!=previous)
        {
            bool fromScatter=previous==PetState.Slime;
            if(state is PetState.Left or PetState.Right){singleRecovery=fromScatter;singleIntro=previous==PetState.Normal;}
            slimeStart=slimeAmount;
            pinkStart=(Point[])pinkShape.Clone();
            starts.Clear();foreach(var pair in current)starts[pair.Key]=pair.Value;
            if(state==PetState.Slime)
            {
                assembling=false;transitionDuration=.95;AssemblyProgress=0;
                foreach(string name in LooseParts)
                {
                    var p=current[name];double destination=name switch{"jacket0"=>85,"jacket1"=>755,"hat"=>675,"cup"=>780,"collar"=>80,"bow"=>690,"arm"=>40,"leftleg"=>60,"rightleg"=>780,_=>700};
                    scatterVelocity[name]=((destination-p.X)/.65,-90-scatterRandom.NextDouble()*90,(scatterRandom.NextDouble()-.5)*230);
                }
            }
            else
            {
                assembling=fromScatter;transitionDuration=fromScatter?1.4:.35;AssemblyProgress=0;
            }
            previous=state;
        }
        AssemblyProgress=transitionDuration>0?Math.Clamp(rig.StateAge/transitionDuration,0,1):1;
        double fade=AssemblyProgress*AssemblyProgress*(3-2*AssemblyProgress);
        slimeAmount=Mix(slimeStart,state==PetState.Slime?1:0,fade);
        ActionTime=Math.Max(0,rig.StateAge-transitionDuration);
        var target=state==PetState.Slime?SlimePose(rig.StateAge):state==PetState.Left?LeftPose(ActionTime):state==PetState.Right?RightPose(rig):layers.ToDictionary(k=>k.Key,k=>Home(k.Value));
        foreach(var (name,to) in target)
        {
            if(state==PetState.Slime&&LooseParts.Contains(name))
            {
                var p=current[name];var velocity=scatterVelocity[name];velocity.Y+=1600*dt;
                double angle=p.Angle+velocity.Spin*dt,x=p.X+velocity.X*dt,y=p.Y+velocity.Y*dt;
                var b=layers[name].Bounds;double c=Math.Abs(Math.Cos(angle*Math.PI/180)),s=Math.Abs(Math.Sin(angle*Math.PI/180));
                double hx=(c*b.Width*Math.Abs(p.Sx)+s*b.Height*Math.Abs(p.Sy))/2,hy=(s*b.Width*Math.Abs(p.Sx)+c*b.Height*Math.Abs(p.Sy))/2;
                if(y+hy>796)
                {
                    double rest=name switch{"leftleg"=>-88,"rightleg"=>92,"jacket0"=>78,"jacket1"=>-74,"hat"=>-18,"cup"=>-52,"collar"=>12,"arm"=>-18,_=>-10};
                    double delta=(rest-angle+540)%360-180;angle+=delta*(1-Math.Exp(-dt*8));
                    c=Math.Abs(Math.Cos(angle*Math.PI/180));s=Math.Abs(Math.Sin(angle*Math.PI/180));
                    hx=(c*b.Width*Math.Abs(p.Sx)+s*b.Height*Math.Abs(p.Sy))/2;hy=(s*b.Width*Math.Abs(p.Sx)+c*b.Height*Math.Abs(p.Sy))/2;
                    y=796-hy;velocity.Y=Math.Abs(velocity.Y)<35?0:-velocity.Y*.24;velocity.X*=.8;velocity.Spin*=.7;
                }
                if(x-hx<0){x=hx;velocity.X=Math.Abs(velocity.X)*.3;}if(x+hx>825){x=825-hx;velocity.X=-Math.Abs(velocity.X)*.3;}
                if(rig.StateAge>3.5 )velocity=(0,0,0);
                current[name]=p with{X=x,Y=y,Angle=angle};scatterVelocity[name]=velocity;continue;
            }
            if(AssemblyProgress>=1){current[name]=to;continue;}
            var from=starts.GetValueOrDefault(name,current[name]);
            // A re-forming slime raises its face and clothes with the body as one
            // coordinated motion, rather than assembling separate floating organs.
            double delay=assembling?(name is "leftleg" or "rightleg"?0:.10):0;
            double u=Math.Clamp((rig.StateAge-delay)/(transitionDuration-delay),0,1),v=u-1;
            double ease=u==0?0:u==1?1:assembling?1+2.2*v*v*v+1.2*v*v:u*u*(3-2*u);
            double lift=assembling?Math.Sin(Math.PI*u)*(45+Math.Abs(to.X-from.X)*.12):0;
            double da=(to.Angle-from.Angle+540)%360-180;
            current[name]=new(Mix(from.X,to.X,ease),Mix(from.Y,to.Y,ease)-lift,from.Angle+da*ease,Mix(from.Sx,to.Sx,Math.Clamp(ease,0,1)),Mix(from.Sy,to.Sy,Math.Clamp(ease,0,1)));
        }
        var goal=state==PetState.Slime?PuddleOutline(rig.Clock):BodyOutline(target["body"]);
        double pinkEase=fade,pinkLift=0;
        if(assembling&&AssemblyProgress<1)
        {
            double u=Math.Clamp((rig.StateAge-.10)/(transitionDuration-.10),0,1),v=u-1;
            pinkEase=u==0?0:u==1?1:1+2.2*v*v*v+1.2*v*v;
            pinkLift=Math.Sin(Math.PI*u)*(45+Math.Abs(target["body"].X-starts["body"].X)*.12);
        }
        pinkShape=AssemblyProgress>=1?goal:pinkStart.Select((p,i)=>new Point(Mix(p.X,goal[i].X,pinkEase),Mix(p.Y,goal[i].Y,pinkEase)-pinkLift)).ToArray();
    }
    static void Curve(DrawingContext dc,(Point Root,Point Knee,Point Ankle,Point Toe) limb)
    {
        var path=new StreamGeometry();using(var c=path.Open()){c.BeginFigure(limb.Root,false,false);c.QuadraticBezierTo(limb.Knee,new Point((limb.Knee.X+limb.Ankle.X)/2,(limb.Knee.Y+limb.Ankle.Y)/2),true,false);c.QuadraticBezierTo(limb.Ankle,new Point(limb.Ankle.X,limb.Toe.Y),true,false);c.LineTo(limb.Toe,true,false);}path.Freeze();
        dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(57,58,200)),14){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round,LineJoin=PenLineJoin.Round},path);
    }
    void DrawPuddle(DrawingContext dc,double time)
    {
        // One solid silhouette changes shape; pink pigment never cross-fades.
        var shape=new StreamGeometry();using(var c=shape.Open())
        {
            c.BeginFigure(pinkShape[0],true,true);
            for(int i=1;i<pinkShape.Length;i+=3)c.BezierTo(pinkShape[i],pinkShape[i+1],pinkShape[i+2],true,false);
        }
        dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(222,88,139)),null,shape);
    }
    void DrawLooseParts(DrawingContext dc,bool foreground=false)
    {
        var order=foreground?new[]{"leftleg","rightleg","arm","bow"}:new[]{"jacket0","jacket1","collar","cup","hat"};
        foreach(string name in order)
        {
            var p=current[name];var layer=layers[name];
            dc.PushTransform(new TranslateTransform(p.X,p.Y));dc.PushTransform(new RotateTransform(p.Angle));dc.PushTransform(new ScaleTransform(p.Sx,p.Sy));
            dc.DrawImage(layer.Image,new Rect(-layer.Bounds.Width/2,-layer.Bounds.Height/2,layer.Bounds.Width,layer.Bounds.Height));
            dc.Pop();dc.Pop();dc.Pop();
        }
    }
    void DrawSlimeFace(DrawingContext dc,Rig rig)
    {
        double blink=CountdownScene.BlinkOpen(11,rig.Clock);
        double melted=slimeAmount*slimeAmount*(3-2*slimeAmount);
        double openness=blink*(1-.42*melted);
        for(int i=0;i<2;i++)
        {
            var eye=current["eye"+i];var pupil=current["pupil"+i];double rx=67*eye.Sx,ry=67*eye.Sy;
            if(blink<=.08)
            {
                var lid=new StreamGeometry();using(var c=lid.Open()){c.BeginFigure(new Point(eye.X-rx*.5,eye.Y),false,false);c.QuadraticBezierTo(new Point(eye.X,eye.Y+5),new Point(eye.X+rx*.5,eye.Y),true,false);}
                dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(45,131,197)),7){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},lid);
            }
            else
            {
                var clip=new EllipseGeometry(new Point(eye.X,eye.Y),rx,ry*openness);dc.DrawGeometry(Brushes.White,null,clip);dc.PushClip(clip);
                double dx=Math.Clamp(pupil.X-eye.X,-rx*.35,rx*.35),dy=Math.Clamp(pupil.Y-eye.Y,-ry*.3,ry*.3);
                dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(45,131,197)),null,new Point(eye.X+dx,eye.Y+(dy+9*melted)*openness),30*pupil.Sx,30*pupil.Sy*openness);dc.Pop();
            }
        }
        var p=current["mouth"];var mouth=layers["mouth"];dc.PushTransform(new TranslateTransform(p.X,p.Y));dc.PushTransform(new RotateTransform(p.Angle));dc.PushTransform(new ScaleTransform(p.Sx,p.Sy));
        if(melted<1){dc.PushOpacity(1-melted);dc.DrawImage(mouth.Image,new Rect(-mouth.Bounds.Width/2,-mouth.Bounds.Height/2,mouth.Bounds.Width,mouth.Bounds.Height));dc.Pop();}
        dc.PushOpacity(melted);
        var frown=new StreamGeometry();using(var c=frown.Open()){c.BeginFigure(new Point(-19,4),false,false);c.QuadraticBezierTo(new Point(0,-10),new Point(19,4),true,false);}
        dc.DrawGeometry(null,new Pen(Brushes.White,9){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},frown);dc.Pop();dc.Pop();dc.Pop();dc.Pop();
    }
    void DrawIdle(DrawingContext dc,Rig rig)
    {
        double blink=CountdownScene.BlinkOpen(17,rig.StateAge);
        double cupPhase=(rig.StateAge-3)%12.7;
        bool cup=rig.StateAge>=3&&cupPhase<1.9,wave=rig.WaveAge<2.8,eyes=blink<.99999;
        int flags=(wave?1:0)|(cup?2:0)|(eyes?4:0);
        dc.DrawImage(idleBases[flags],new Rect(0,0,825,796));
        if(wave){var arm=layers["arm"];dc.PushTransform(new RotateTransform(Math.Sin(Math.PI*rig.WaveAge/2.8)*(95+13*Math.Sin(rig.WaveAge*15)),109,419));dc.DrawImage(arm.Image,arm.Bounds);dc.Pop();}
        if(cup)
        {
            var part=layers["cup"];double angle=6*Math.Sin(Math.PI*cupPhase/1.9)*Math.Sin(cupPhase*8);
            dc.PushTransform(new RotateTransform(angle,548,416));dc.DrawImage(part.Image,part.Bounds);dc.Pop();
        }
        if(eyes)
        {
            for(int i=0;i<2;i++)
            {
                var eye=Home(layers["eye"+i]);var pupil=Home(layers["pupil"+i]);
                if(blink<=.08)
                {
                    var lid=new StreamGeometry();using(var c=lid.Open()){c.BeginFigure(new Point(eye.X-30,eye.Y),false,false);c.QuadraticBezierTo(new Point(eye.X,eye.Y+6),new Point(eye.X+30,eye.Y),true,false);}
                    dc.DrawGeometry(null,new Pen(new SolidColorBrush(Color.FromRgb(45,131,197)),6){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},lid);
                }
                else
                {
                    var clip=new EllipseGeometry(new Point(eye.X,eye.Y),67,67*blink);dc.DrawGeometry(Brushes.White,null,clip);dc.PushClip(clip);
                    dc.DrawEllipse(new SolidColorBrush(Color.FromRgb(45,131,197)),null,new Point(pupil.X,eye.Y+(pupil.Y-eye.Y)*blink),30,30*blink);dc.Pop();
                }
            }
        }
    }
    public void Draw(DrawingContext dc,Rig rig)
    {
        double finalBlend=AssemblyProgress>=1?1:Math.Clamp((AssemblyProgress-.78)/.22,0,1);
        bool single=rig.State is PetState.Left or PetState.Right;
        if(single&&(!singleRecovery||finalBlend>=1)){approved.Draw(dc,rig.State,singleRecovery?ActionTime:rig.StateAge,singleIntro&&!singleRecovery);return;}
        dc.PushTransform(new TranslateTransform(Left,Top));dc.PushTransform(new ScaleTransform(Scale,Scale));
        if(rig.State==PetState.Slime)DrawLooseParts(dc);
        if(slimeAmount>0)DrawPuddle(dc,rig.Clock);
        if(rig.State==PetState.Slime)DrawLooseParts(dc,true);
        if(rig.State==PetState.Normal&&AssemblyProgress>=1)
        {
            DrawIdle(dc,rig);
        }
        else
        {
            if(single)dc.PushOpacity(1-finalBlend);
            if(rig.State==PetState.Left&&finalBlend>0){dc.PushOpacity(finalBlend*(1-slimeAmount));Curve(dc,Support(ActionTime));Curve(dc,Kick(ActionTime));dc.Pop();}
            string[] order=["leftleg","rightleg","body","jacket0","jacket1","collar","arm","cup","eye0","eye1","pupil0","pupil1","mouth","bow","hat"];
            foreach(string name in order)
            {
                bool face=name is "eye0" or "eye1" or "pupil0" or "pupil1" or "mouth";
                if(face&&rig.State==PetState.Slime)continue;
                if(rig.State==PetState.Slime&&LooseParts.Contains(name))continue;
                if(name=="body"&&slimeAmount>0)continue;
                bool leg=name is "leftleg" or "rightleg";
                if(leg&&rig.State==PetState.Left&&finalBlend>=1)continue;
                var p=current[name];var layer=layers[name];
                if(leg&&rig.State==PetState.Left)dc.PushOpacity(1-finalBlend);
                dc.PushTransform(new TranslateTransform(p.X,p.Y));dc.PushTransform(new RotateTransform(p.Angle));dc.PushTransform(new ScaleTransform(p.Sx,p.Sy));
                if(name is "eye0" or "eye1")dc.DrawEllipse(Brushes.White,null,new Point(),67,67);
                else if(name=="mouth"&&rig.State==PetState.Left)
                {
                    double strain=Track(ActionTime,0,.25,.7,.95,.4,0,.6,.9,.2,0);
                    var expression=new StreamGeometry();using(var c=expression.Open()){c.BeginFigure(new Point(-20,2),false,false);c.QuadraticBezierTo(new Point(0,15-18*strain),new Point(20,-9+15*strain),true,false);}
                    dc.DrawGeometry(null,new Pen(Brushes.White,10){StartLineCap=PenLineCap.Round,EndLineCap=PenLineCap.Round},expression);
                }
                else
                {
                    if(name=="body")
                    {
                        // The cap hides this arc in the source. Continue the pink head under it
                        // so hat lag never exposes a transparent diagonal cut in the forehead.
                        var patch=Geometry.Parse("M280,80 C400,30 520,80 558,218 L548,240 L280,110 Z");
                        dc.PushTransform(new TranslateTransform(-Home(layer).X,-Home(layer).Y));dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(222,88,139)),null,patch);dc.Pop();
                    }
                    dc.DrawImage(layer.Image,new Rect(-layer.Bounds.Width/2,-layer.Bounds.Height/2,layer.Bounds.Width,layer.Bounds.Height));
                }
                dc.Pop();dc.Pop();dc.Pop();if(leg&&rig.State==PetState.Left)dc.Pop();
            }
            if(rig.State==PetState.Slime)DrawSlimeFace(dc,rig);
            if(single)dc.Pop();
        }
        dc.Pop();dc.Pop();
        if(single&&finalBlend>0){dc.PushOpacity(finalBlend);approved.Draw(dc,rig.State,ActionTime,false);dc.Pop();}
    }
    public void Export(string folder)
    {
        Directory.CreateDirectory(folder);var rig=new Rig();var evidence=new List<object>();
        void Save(string group,int frame)
        {
            var v=new DrawingVisual();using(var dc=v.RenderOpen())Draw(dc,rig);
            var bmp=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32);bmp.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
            var dir=Path.Combine(folder,group);Directory.CreateDirectory(dir);using var f=File.Create(Path.Combine(dir,$"frame-{frame:000}.png"));png.Save(f);
        }
        void Tick(double dt){rig.Step(dt);Step(rig,dt);}
        void Wait(int n){for(int i=0;i<n;i++)Tick(1/30d);}
        Save("normal",0);rig.Wave();for(int i=0;i<84;i++){rig.WaveAge=i/30d;Save("greeting",i);}
        rig.SetKeys(true,false);Wait(12);
        for(int i=0;i<96;i++)
        {
            double t=i/30d;var pose=LeftPose(t);foreach(var p in pose)current[p.Key]=p.Value;ActionTime=t;AssemblyProgress=1;Save("left-loop",i);
            var b=Body(t);var kick=Kick(t);if(b.Angle>-13||kick.Toe.Y>735)throw new Exception("Left loop contains a neutral/rest pose");
            foreach(string feature in new[]{"body","eye0","eye1","pupil0","pupil1","mouth","bow","arm","cup","hat"})if(Math.Abs(pose[feature].Sx-1)>1e-9||Math.Abs(pose[feature].Sy-1)>1e-9)throw new Exception("Changed design proportions: "+feature);
            var mouth=Home(layers["mouth"]);var anchor=World(new(mouth.X,mouth.Y),t);
            if((new Point(pose["mouth"].X,pose["mouth"].Y)-anchor).Length>1e-8)throw new Exception("Mouth drifted from fixed face anchor");
            evidence.Add(new{frame=i,bodyAngle=b.Angle,freeFootY=kick.Toe.Y,supportFoot=new[]{200,788},eye0=current["eye0"],eye1=current["eye1"],hat=current["hat"],bow=current["bow"]});
        }
        var first=LeftPose(0);var next=LeftPose(3.2);foreach(var name in first.Keys)if(Math.Abs(first[name].X-next[name].X)+Math.Abs(first[name].Y-next[name].Y)+Math.Abs(first[name].Angle-next[name].Angle)>1e-6)throw new Exception("Loop seam is discontinuous");
        foreach(var (l,r,name) in new[]{(true,true,"scatter-to-normal"),(true,false,"scatter-to-left"),(false,true,"scatter-to-right")})
        {
            rig.SetKeys(false,false);Wait(120);rig.SetKeys(l,r);
            for(int i=0;i<84;i++){Tick(1/30d);Save(name,i);if(current.Values.Any(p=>!double.IsFinite(p.X+p.Y+p.Sx+p.Sy)))throw new Exception("Invalid assembly pose");}
        }
        // Interrupt an assembly, re-scatter, then change the target twice.
        rig.SetKeys(false,false);Wait(60);rig.SetKeys(true,false);Wait(10);var before=current["body"];rig.SetKeys(false,false);Step(rig,0);
        if(current["body"]!=before)throw new Exception("Interrupt snapped the body");
        Wait(15);rig.SetKeys(false,true);Wait(10);rig.SetKeys(true,true);Wait(90);
        if(AssemblyProgress!=1)throw new Exception("Assembly did not complete");
        System.Text.Json.JsonSerializerOptions options=new(){WriteIndented=true};
        File.WriteAllText(Path.Combine(folder,"left-motion-evidence.json"),System.Text.Json.JsonSerializer.Serialize(evidence,options));
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: 96 left frames preserve body, face and accessory scales at 1:1; mouth has fixed face anchor; collar is attached to body; continuous imbalance and raised free foot; periodic seam equality; three assembly destinations; finite poses; interrupted assembly continuity; normal returns to original image.");
    }
    public void ExportSlime(string folder)
    {
        Directory.CreateDirectory(folder);var rig=new Rig();int opaqueChecks=0;
        void Save(string group,int frame)
        {
            var v=new DrawingVisual();using(var dc=v.RenderOpen())Draw(dc,rig);
            var bmp=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32);bmp.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
            string dir=Path.Combine(folder,group);Directory.CreateDirectory(dir);using var f=File.Create(Path.Combine(dir,$"frame-{frame:000}.png"));png.Save(f);
            if(group=="melt-and-idle")
            {
                var body=new DrawingVisual();using(var dc=body.RenderOpen()){dc.PushTransform(new TranslateTransform(Left,Top));dc.PushTransform(new ScaleTransform(Scale,Scale));DrawPuddle(dc,rig.Clock);dc.Pop();dc.Pop();}
                var raster=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32);raster.Render(body);byte[] pixels=new byte[640*640*4];raster.CopyPixels(pixels,640*4,0);bool solid=false;
                for(int i=0;i<pixels.Length;i+=4)if(pixels[i]==139&&pixels[i+1]==88&&pixels[i+2]==222&&pixels[i+3]==255){solid=true;break;}
                if(!solid)throw new Exception("Pink body changed color or opacity during melt");opaqueChecks++;
            }
        }
        void Tick(){rig.Step(1/30d);Step(rig,1/30d);if(!double.IsFinite(slimeAmount)||slimeAmount<0||slimeAmount>1||current.Values.Any(p=>!double.IsFinite(p.X+p.Y+p.Sx+p.Sy)))throw new Exception("Invalid slime pose");}
        Save("normal",0);rig.SetKeys(false,false);
        for(int i=0;i<120;i++){Tick();Save("melt-and-idle",i);}
        if(slimeAmount!=1||current["body"].Sy>.25||current["eye0"].Y<670)throw new Exception("Slime did not settle with visible face");
        if(opaqueChecks!=120||LooseParts.Any(name=>Math.Abs(current[name].Sx)<.5||Math.Abs(current[name].Sy)<.5))throw new Exception("Clothes/limbs vanished or pink opacity checks missing");
        foreach(var (l,r,name) in new[]{(true,true,"slime-to-normal"),(true,false,"slime-to-left"),(false,true,"slime-to-right")})
        {
            rig.SetKeys(false,false);for(int i=0;i<60;i++)Tick();rig.SetKeys(l,r);
            for(int i=0;i<75;i++){Tick();Save(name,i);}
            if(slimeAmount!=0||AssemblyProgress!=1)throw new Exception("Slime recovery did not finish");
        }
        rig.SetKeys(false,false);for(int i=0;i<6;i++)Tick();var body=current["body"];
        rig.SetKeys(true,false);Step(rig,0);if(current["body"]!=body)throw new Exception("Switching during melt snapped the body");
        for(int i=0;i<9;i++)Tick();rig.SetKeys(false,false);for(int i=0;i<8;i++)Tick();rig.SetKeys(true,true);for(int i=0;i<60;i++)Tick();
        if(slimeAmount!=0)throw new Exception("Interrupted recovery remained melted");
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: 120 melt/idle frames retain fully opaque original pink RGB; 9 loose clothing/limb/accessory pieces retain visible scale and scatter separately; tired face remains on slime; recovery to all 3 key states completes; rapid input changes preserve pose; finite transforms; original normal image retained.");
    }
    public void ExportEnhanced(string folder)
    {
        Directory.CreateDirectory(folder);approved.Verify();var rig=new Rig();
        void Save(string group,int frame)
        {
            var v=new DrawingVisual();using(var dc=v.RenderOpen())Draw(dc,rig);
            var bmp=new RenderTargetBitmap(640,640,96,96,PixelFormats.Pbgra32);bmp.Render(v);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bmp));
            var dir=Path.Combine(folder,group);Directory.CreateDirectory(dir);using var f=File.Create(Path.Combine(dir,$"frame-{frame:000}.png"));png.Save(f);
        }
        void Tick(){rig.Step(1/30d);Step(rig,1/30d);}
        Save("normal-baseline",0);
        for(int i=0;i<228;i++){Tick();Save("normal-idle",i);}
        foreach(var (l,r,name) in new[]{(true,false,"left-approved"),(false,true,"right-approved")})
        {
            rig.SetKeys(true,true);for(int i=0;i<15;i++)Tick();rig.SetKeys(l,r);
            for(int i=0;i<222;i++){Tick();Save(name,i);}
        }
        rig.SetKeys(false,false);for(int i=0;i<120;i++)Tick();rig.SetKeys(true,false);
        for(int i=0;i<90;i++){Tick();Save("slime-to-approved-left",i);}
        File.WriteAllText(Path.Combine(folder,"result.txt"),"PASS: approved left/right 48-pose sequences and 6050ms loops; onset once and no return to normal over 90s held key; all 105 frames have transparent background; idle base uses original image; blink and cup motion stay local; slime-to-approved playback exported.");
    }
}
