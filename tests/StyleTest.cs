using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using CupheadInputDisplay;
class StyleTest {
    static int count;static Component component;static string output;
    static void Assert(bool condition,string message){if(!condition)throw new Exception(message);count++;Console.WriteLine("PASS "+message);}
    static Dictionary<string,object> Defaults(){return Component.Map(component.Defaults(),"displaySettings");}
    static Bitmap Source(){var image=new Bitmap(66,66);using(var g=Graphics.FromImage(image))g.FillRectangle(Brushes.White,24,24,18,18);return image;}
    static Bitmap Render(Dictionary<string,object> d,bool pressed,Dictionary<string,object> element=null){var bitmap=new Bitmap(160,160);using(var source=Source())using(var fx=new SpriteEffects())using(var g=Graphics.FromImage(bitmap)){g.InterpolationMode=InterpolationMode.HighQualityBicubic;fx.Configure(component.Json.Serialize(d));fx.Draw(g,"fixture",source,new RectangleF(48,48,66,66),pressed,d,element??new Dictionary<string,object>(),true);}return bitmap;}
    static int Pixels(Bitmap bitmap){int n=0;for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)if(bitmap.GetPixel(x,y).A>10)n++;return n;}
    static Color Center(Bitmap bitmap){return bitmap.GetPixel(81,81);}
    [STAThread] static void Main(string[] args){output=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Components",new System.Reflection.AssemblyName(e.Name).Name+".dll");return File.Exists(path)?System.Reflection.Assembly.LoadFrom(path):null;};try{using(component=new Component(null))Run();}catch(Exception e){Console.WriteLine("FAIL "+e);Environment.ExitCode=1;}}
    static void Run(){
        var d=Defaults();Assert(Component.Number(d,"idleBrightness",0,0,1)==0.4&&Component.Number(d,"pressedBrightness",0,0,1)==1,"New default is dark inactive and bright pressed");
        using(var idle=Render(d,false))using(var pressed=Render(d,true)){Assert(Center(idle).R>=95&&Center(idle).R<=110&&Center(pressed).R>250,"Default rendering visibly darkens inactive inputs and lights pressed inputs");}
        d["invertStates"]=true;using(var idle=Render(d,false))using(var pressed=Render(d,true)){Assert(Center(idle).R>250&&Center(pressed).R<110,"Invert swaps the two generated visual states");}
        d=Defaults();d["pressedColor"]="#FFFF0000";d["tintMode"]="Solid";using(var image=Render(d,true)){Assert(Center(image).R>250&&Center(image).G<5&&Center(image).B<5,"Pressed sprite can be recolored solid red");}
        d["pressedTintAmount"]=50;using(var image=Render(d,true)){Assert(Center(image).R>250&&Center(image).G>120&&Center(image).G<135,"Color strength blends recoloring with original artwork");}
        d["pressedTintAmount"]=0;using(var image=Render(d,true)){Assert(Center(image).R>250&&Center(image).G>250&&Center(image).B>250,"Zero color strength preserves the original sprite colors");}
        d["pressedTintAmount"]=100;d["pressedOpacity"]=50;using(var image=Render(d,true)){Assert(Center(image).A>=120&&Center(image).A<=135,"Pressed sprite opacity is configurable");}
        d=Defaults();d["tintMode"]="Solid";var custom=new Dictionary<string,object>{{"customColors",true},{"pressedColor","#FF00FF00"}};using(var image=Render(d,true,custom)){Assert(Center(image).G>250&&Center(image).R<5,"Individual elements can override the global pressed color");}
        d=Defaults();d["pressedFillColor"]="#FF0000FF";d["pressedFillOpacity"]=100;using(var image=Render(d,true)){Color pixel=image.GetPixel(60,60);Assert(pixel.A==0,"Removed input backgrounds ignore legacy fill settings");}
        d=Defaults();d["borderEnabled"]=true;d["pressedBorderColor"]="#FFFF0000";d["pressedBorderWidth"]=3;int thin;using(var image=Render(d,true)){Color pixel=image.GetPixel(70,81);Assert(pixel.R>200&&pixel.G<20&&pixel.A>200,"Sprite outline uses configured border color and extends outside the sprite");thin=Pixels(image);}
        d["pressedBorderWidth"]=6;using(var image=Render(d,true)){Assert(Pixels(image)>thin,"Increasing border thickness increases the visible outline");}
        foreach(string shape in new[]{"Rounded rectangle","Rectangle"}){d["borderStyle"]=shape;using(var image=Render(d,true)){Assert(image.GetPixel(49,81).R>200,shape+" input border renders around the key bounds");}}
        d=Defaults();d["shadowEnabled"]=true;d["pressedShadowColor"]="#FF0000FF";d["shadowOpacity"]=100;d["shadowOffsetX"]=10;d["shadowOffsetY"]=0;d["shadowBlur"]=0;using(var image=Render(d,true)){Color pixel=image.GetPixel(97,81);Assert(pixel.B>250&&pixel.R<5&&pixel.A>250,"Drop shadow follows the configured color and offset");}
        d["shadowBlur"]=4;using(var image=Render(d,true)){Color pixel=image.GetPixel(102,81);Assert(pixel.A>0&&pixel.A<200,"Shadow blur produces a soft edge beyond the hard shadow");}
        d["shadowPressedOnly"]=true;d["shadowBlur"]=0;using(var image=Render(d,false)){Assert(image.GetPixel(97,81).A==0,"Pressed-only shadow is absent on inactive inputs");}
        component.Config=component.Defaults();var initial=component.Bounds();var display=Component.Map(component.Config,"displaySettings");display["shadowEnabled"]=true;display["shadowBlur"]=12;display["shadowOffsetX"]=30;display["borderEnabled"]=true;display["pressedBorderWidth"]=12;Assert(component.Bounds().Width>initial.Width&&component.Bounds().Height>initial.Height,"Automatic artwork bounds reserve room for thick borders and shadows");
        component.Config=component.Defaults();component.Options["backgroundMode"]="Solid";component.Options["backgroundColor"]="#FF8A2BE2";using(var image=new Bitmap(400,170))using(var g=Graphics.FromImage(image)){component.Render(g,400,170);Assert(image.GetPixel(0,0).ToArgb()==Color.FromArgb(138,43,226).ToArgb(),"Component background accepts any selected RGB color");}
        component.Options["backgroundOpacity"]=50;using(var image=new Bitmap(400,170))using(var g=Graphics.FromImage(image)){component.Render(g,400,170);Assert(image.GetPixel(0,0).A==255,"Legacy background-opacity values cannot make the background transparent");}
        component.Options["backgroundOpacity"]=100;component.Options["backgroundColor"]="#FFFF0000";component.Options["backgroundColor2"]="#FF0000FF";
        component.Load("{\"displaySettings\":{\"idleBrightness\":1,\"pressedBrightness\":0.4},\"liveSplit\":{\"greenScreen\":true}}");Assert(Component.Number(Component.Map(component.Config,"displaySettings"),"idleBrightness",0,0,1)==0.4&&Component.Text(component.Options,"backgroundColor","")=="#FF00FF00","Previous default brightness and green-screen settings migrate to the new style controls");
        var schema=component.Save();component.Load(schema);Assert(component.Save()==schema,"New style settings survive JSON round-trip without resetting user choices");
        var invalidator=new Invalidator();component.Update(invalidator,null,400,170,LiveSplit.UI.LayoutMode.Vertical);component.Update(invalidator,null,400,170,LiveSplit.UI.LayoutMode.Vertical);Assert(invalidator.Calls==1,"Unchanged input display avoids repeated LiveSplit redraw requests");component.Options["backgroundOpacity"]=42;Thread.Sleep(110);component.Update(invalidator,null,400,170,LiveSplit.UI.LayoutMode.Vertical);Assert(invalidator.Calls==2,"Appearance changes still invalidate the optimized display");
        TestBackgroundImages();Gallery();Benchmark();Console.WriteLine("SUCCESS "+count+" style assertions");
    }
    static void TestBackgroundImages(){
        string encoded;using(var image=new Bitmap(10,10))using(var g=Graphics.FromImage(image))using(var stream=new MemoryStream()){g.Clear(Color.Red);image.Save(stream,ImageFormat.Png);stream.Position=0;encoded=BackgroundImages.Encode(stream);}
        component.Config=component.Defaults();foreach(string name in Component.Elements)Component.Map(Component.Map(component.Config,"elementSettings"),name)["visible"]=false;
        component.Options["backgroundImage"]=encoded;component.Options["backgroundImageName"]="test.png";
        foreach(string mode in new[]{"Fill","Fit","Stretch","Center","Tile"}){component.Options["backgroundImageMode"]=mode;using(var frame=new Bitmap(400,200))using(var g=Graphics.FromImage(frame)){component.Render(g,400,200);Assert(frame.GetPixel(200,100).R>240,"Embedded image renders in "+mode+" mode");Assert(frame.GetPixel(0,0).A==255,"Background stays opaque in "+mode+" mode");if(mode=="Fit"||mode=="Center")Assert(frame.GetPixel(0,0).ToArgb()==Color.Black.ToArgb(),mode+" leaves uncovered areas in the chosen background color");}}
        var document=new System.Xml.XmlDocument();document.AppendChild(component.GetSettings(document));using(var restored=new Component(null)){restored.SetSettings(document.DocumentElement);Assert(Component.Text(restored.Options,"backgroundImage","")==encoded,"Background image is embedded in layout XML without an external file");}
        component.Options["backgroundImage"]="";component.Options["backgroundColor"]="#FF0000FF";using(var frame=new Bitmap(400,200))using(var g=Graphics.FromImage(frame)){component.Render(g,400,200);Assert(frame.GetPixel(200,100).B>240,"Removing the image restores the solid background");}
        component.Options["backgroundImage"]="not valid image data";using(var frame=new Bitmap(400,200))using(var g=Graphics.FromImage(frame)){component.Render(g,400,200);Assert(frame.GetPixel(0,0).A==255,"Invalid embedded image safely falls back to opaque color");}
        using(var image=new Bitmap(3000,20))using(var stream=new MemoryStream()){image.Save(stream,ImageFormat.Png);stream.Position=0;using(var normalized=BackgroundImages.Decode(BackgroundImages.Encode(stream)))Assert(normalized.Width==2048,"Large background images are normalized to a bounded size");}
    }
    sealed class Invalidator : LiveSplit.UI.IInvalidator {public int Calls;public Matrix Transform{get;set;}public void Invalidate(float a,float b,float c,float d){Calls++;}}
    static void Gallery(){using(var atlas=new Bitmap(1200,740))using(var g=Graphics.FromImage(atlas))using(var font=new Font("Segoe UI",10)){
        g.Clear(Color.FromArgb(20,24,32));string[] labels={"Default · Inactive","Default · Pressed","Color + Outline · Inactive","Color + Outline · Pressed","Color + Shadow · Inactive","Color + Shadow · Pressed","Rounded borders · Inactive","Rounded borders · Pressed","Inverted · Inactive","Inverted · Pressed","Green screen · Pressed","Custom solid background · Pressed"};
        for(int i=0;i<12;i++){component.Config=component.Defaults();component.Character="Cuphead";component.Plane=false;for(int j=0;j<10;j++)component.Pressed[j]=i%2==1||i>=10;var d=Component.Map(component.Config,"displaySettings");
            if(i>=2&&i<=5){d["tintMode"]="Colorize";d["idleColor"]="#FFFFC857";d["pressedColor"]="#FF40E0FF";d["idleBrightness"]=0.6;}
            if(i==2||i==3){d["borderEnabled"]=true;d["idleBorderColor"]="#FFFF8F40";d["pressedBorderColor"]="#FF70E7FF";d["pressedBorderWidth"]=2;}
            if(i==4||i==5){d["shadowEnabled"]=true;d["shadowBlur"]=3;d["pressedShadowColor"]="#FF04A8FF";d["idleShadowColor"]="#FFCC6E00";d["shadowOpacity"]=90;d["shadowSpread"]=1;}
            if(i==6||i==7){d["idleFillColor"]="#FF31243F";d["pressedFillColor"]="#FF234E59";d["idleFillOpacity"]=100;d["pressedFillOpacity"]=100;d["borderEnabled"]=true;d["borderStyle"]="Rounded rectangle";d["idleBorderColor"]="#FF70588A";d["pressedBorderColor"]="#FF55E4FF";d["pressedBorderWidth"]=1.5;}
            if(i==8||i==9)d["invertStates"]=true;
            if(i==10){component.Options["backgroundMode"]="Solid";component.Options["backgroundColor"]="#FF00FF00";}
            if(i==11){component.Options["backgroundMode"]="Solid";component.Options["backgroundColor"]="#FF341A52";}
            int x=i%2*600,y=i/2*123;g.DrawString(labels[i],font,Brushes.Gainsboro,x+12,y+4);using(var frame=new Bitmap(590,98))using(var fg=Graphics.FromImage(frame)){component.Render(fg,590,98);g.DrawImageUnscaled(frame,x+5,y+24);}
        }atlas.Save(Path.Combine(output,"styling-preview.png"));
    }}
    static void Benchmark(){component.Config=component.Defaults();var d=Component.Map(component.Config,"displaySettings");d["borderEnabled"]=true;d["shadowEnabled"]=true;d["shadowBlur"]=3;component.Pressed[0]=true;using(var frame=new Bitmap(400,170))using(var g=Graphics.FromImage(frame)){var timer=Stopwatch.StartNew();component.Render(g,400,170);timer.Stop();double cold=timer.Elapsed.TotalMilliseconds;timer.Restart();for(int i=0;i<300;i++){g.Clear(Color.Transparent);component.Render(g,400,170);}timer.Stop();string report="Cold effect creation: "+cold.ToString("F2")+" ms\r\nCached full-display render: "+(timer.Elapsed.TotalMilliseconds/300).ToString("F3")+" ms/frame over 300 frames\r\nMeasurement is from this machine's test process; not a hardware-independent guarantee.\r\n";File.WriteAllText(Path.Combine(output,"render-performance.txt"),report);Console.WriteLine(report);}}
}



