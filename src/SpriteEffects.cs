using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
namespace CupheadInputDisplay {
    public sealed class SpriteEffects : IDisposable {
        const int Resolution=4;
        readonly Dictionary<string,StyledImage> cache=new Dictionary<string,StyledImage>();string signature;
        readonly ImageAttributes drawAttributes=new ImageAttributes();
        public SpriteEffects(){drawAttributes.SetWrapMode(WrapMode.TileFlipXY);}
        sealed class StyledImage : IDisposable {public Bitmap Image;public int Margin,Base;public readonly Dictionary<string,Bitmap> Sizes=new Dictionary<string,Bitmap>();public void Dispose(){Image.Dispose();foreach(var bitmap in Sizes.Values)bitmap.Dispose();}}
        public void Configure(string next){if(signature==next)return;Clear();signature=next;}
        public static Color ReadColor(Dictionary<string,object> settings,string key,Color fallback){string text=Component.Text(settings,key,"").Trim();try{if(text.StartsWith("#")){uint value;if(uint.TryParse(text.Substring(1),System.Globalization.NumberStyles.HexNumber,System.Globalization.CultureInfo.InvariantCulture,out value)){if(text.Length==7)value|=0xff000000;if(text.Length==7||text.Length==9)return Color.FromArgb(unchecked((int)value));}}else if(text.Length>0){Color color=Color.FromName(text);if(color.IsKnownColor||color.IsNamedColor&&color.ToArgb()!=0)return color;}}catch(ArgumentException){}return fallback;}
        public static float Padding(Dictionary<string,object> d){float border=Component.Flag(d,"borderEnabled",false)&&Component.Text(d,"borderStyle","Sprite outline")!="None"?(float)Math.Max(Component.Number(d,"idleBorderWidth",1,0,12),Component.Number(d,"pressedBorderWidth",2,0,12)):0;float shadow=Component.Flag(d,"shadowEnabled",false)?(float)(Component.Number(d,"shadowSpread",0,0,8)+2*Component.Number(d,"shadowBlur",3,0,12)+Math.Max(Math.Abs(Component.Number(d,"shadowOffsetX",3,-30,30)),Math.Abs(Component.Number(d,"shadowOffsetY",3,-30,30)))):0;return border+shadow>0?(float)Math.Ceiling(border+shadow+2):0;}
        public void Draw(Graphics g,string path,Bitmap source,RectangleF rect,bool physicalPressed,Dictionary<string,object> d,Dictionary<string,object> element,bool smooth){
            bool pressed=physicalPressed^Component.Flag(d,"invertStates",false);string prefix=pressed?"pressed":"idle";
            Color tint=ReadColor(Component.Flag(element,"customColors",false)?element:d,prefix+"Color",Color.White);
            string key=path+"|"+pressed+"|"+tint.ToArgb()+"|"+smooth;StyledImage styled;
            if(!cache.TryGetValue(key,out styled)){if(cache.Count>=128)Clear();styled=Build(source,path.IndexOf("Arrow_",StringComparison.OrdinalIgnoreCase)>=0?46:66,pressed,tint,d,smooth);cache[key]=styled;TrimCache(styled,null);}
            float unit=rect.Width/styled.Base;var destination=new RectangleF(rect.X-styled.Margin*unit,rect.Y-styled.Margin*unit,styled.Image.Width/(float)Resolution*unit,styled.Image.Height/(float)Resolution*unit);
            using(var transform=g.Transform)if(transform.Elements[1]==0&&transform.Elements[2]==0&&g.PageScale==1&&(g.PageUnit==GraphicsUnit.Pixel||g.PageUnit==GraphicsUnit.Display)){
                var points=new[]{new PointF(destination.Left,destination.Top),new PointF(destination.Right,destination.Bottom)};transform.TransformPoints(points);int width=(int)Math.Round(points[1].X-points[0].X),height=(int)Math.Round(points[1].Y-points[0].Y);
                if(width>0&&height>0&&width<=4096&&height<=4096){string size=width+"x"+height;Bitmap scaled;if(!styled.Sizes.TryGetValue(size,out scaled)){
                    if(styled.Sizes.Count>=8){foreach(var bitmap in styled.Sizes.Values)bitmap.Dispose();styled.Sizes.Clear();}
                    scaled=new Bitmap(width,height,PixelFormat.Format32bppPArgb);using(var target=Graphics.FromImage(scaled)){target.InterpolationMode=smooth?InterpolationMode.HighQualityBicubic:InterpolationMode.NearestNeighbor;target.PixelOffsetMode=PixelOffsetMode.HighQuality;target.DrawImage(styled.Image,new Rectangle(0,0,width,height),0,0,styled.Image.Width,styled.Image.Height,GraphicsUnit.Pixel,drawAttributes);}styled.Sizes[size]=scaled;TrimCache(styled,scaled);
                }var saved=g.Save();try{g.ResetTransform();g.DrawImageUnscaled(scaled,(int)Math.Round(points[0].X),(int)Math.Round(points[0].Y));}finally{g.Restore(saved);}return;}
            }
            g.DrawImage(styled.Image,new[]{new PointF(destination.Left,destination.Top),new PointF(destination.Right,destination.Top),new PointF(destination.Left,destination.Bottom)},new RectangleF(0,0,styled.Image.Width,styled.Image.Height),GraphicsUnit.Pixel,drawAttributes);
        }
        StyledImage Build(Bitmap source,int baseSize,bool pressed,Color tint,Dictionary<string,object> d,bool smooth){
            string p=pressed?"pressed":"idle";int margin=(int)Padding(d),edge=(baseSize+margin*2)*Resolution;var sprite=new Bitmap(edge,edge,PixelFormat.Format32bppArgb);var cell=new RectangleF(margin*Resolution,margin*Resolution,baseSize*Resolution,baseSize*Resolution);
            float brightness=(float)Component.Number(d,p+"Brightness",pressed?1:0.4,0,1),opacity=(float)Component.Number(d,p+"Opacity",100,0,100)/100;
            string mode=Component.Text(d,"tintMode","Multiply");float amount=(float)Component.Number(d,p+"TintAmount",100,0,100)/100;
            if(mode=="Original")amount=0;float[] channels={tint.R/255f,tint.G/255f,tint.B/255f};var matrix=new ColorMatrix();
            for(int output=0;output<3;output++)for(int input=0;input<3;input++)matrix[input,output]=brightness*((input==output?1-amount:0)+(mode=="Colorize"?amount*channels[output]*(input==0?0.2126f:input==1?0.7152f:0.0722f):mode=="Solid"?0:input==output?amount*channels[output]:0));
            if(mode=="Solid")for(int channel=0;channel<3;channel++)matrix[4,channel]=brightness*amount*channels[channel];matrix[3,3]=opacity*(1-amount+amount*tint.A/255f);
            using(var g=Graphics.FromImage(sprite))using(var attrs=new ImageAttributes()){
                g.InterpolationMode=smooth?InterpolationMode.HighQualityBicubic:InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.HighQuality;attrs.SetWrapMode(WrapMode.TileFlipXY);attrs.SetColorMatrix(matrix);
                float fit=Math.Min(cell.Width/source.Width,cell.Height/source.Height);var dest=new RectangleF(cell.X+(cell.Width-source.Width*fit)/2,cell.Y+(cell.Height-source.Height*fit)/2,source.Width*fit,source.Height*fit);
                g.DrawImage(source,new[]{new PointF(dest.Left,dest.Top),new PointF(dest.Right,dest.Top),new PointF(dest.Left,dest.Bottom)},new RectangleF(0,0,source.Width,source.Height),GraphicsUnit.Pixel,attrs);
            }
            var foreground=new Bitmap(edge,edge,PixelFormat.Format32bppArgb);float corner=(float)Component.Number(d,"cornerRadius",8,0,30)*Resolution;
            bool border=Component.Flag(d,"borderEnabled",false);float borderWidth=(float)Component.Number(d,p+"BorderWidth",pressed?2:1,0,12)*Resolution;string borderStyle=Component.Text(d,"borderStyle","Sprite outline");Color borderColor=ReadColor(d,p+"BorderColor",pressed?Color.White:Color.Black);float borderOpacity=(float)Component.Number(d,"borderOpacity",100,0,100)/100;
            using(var g=Graphics.FromImage(foreground)){
                g.SmoothingMode=SmoothingMode.AntiAlias;
                
                if(border&&borderWidth>0&&borderStyle=="Sprite outline"){
                    byte[] alpha=Alpha(sprite),expanded=Maximum(alpha,edge,edge,(int)Math.Ceiling(borderWidth));for(int i=0;i<alpha.Length;i++)expanded[i]=(byte)Math.Max(0,expanded[i]-alpha[i]);
                    using(var outline=Mask(expanded,edge,edge,borderColor,borderOpacity))g.DrawImageUnscaled(outline,0,0);
                }
                g.DrawImageUnscaled(sprite,0,0);
                if(border&&borderWidth>0&&borderStyle!="Sprite outline"&&borderStyle!="None")using(var path=Rounded(cell,borderStyle=="Rectangle"?0:corner))using(var pen=new Pen(Color.FromArgb((int)(borderOpacity*borderColor.A),borderColor),borderWidth))g.DrawPath(pen,path);
            }
            sprite.Dispose();
            if(Component.Flag(d,"shadowEnabled",false)&&(!Component.Flag(d,"shadowPressedOnly",false)||pressed)){
                var result=new Bitmap(edge,edge,PixelFormat.Format32bppArgb);var alpha=Alpha(foreground);int spread=(int)Math.Round(Component.Number(d,"shadowSpread",0,0,8)*Resolution);if(spread>0)alpha=Maximum(alpha,edge,edge,spread);
                int blur=(int)Math.Round(Component.Number(d,"shadowBlur",3,0,12)*Resolution/2);if(blur>0)for(int pass=0;pass<3;pass++)alpha=BoxBlur(alpha,edge,edge,blur);
                Color color=ReadColor(d,p+"ShadowColor",Color.Black);float strength=(float)Component.Number(d,"shadowOpacity",65,0,100)/100;
                using(var shadow=Mask(alpha,edge,edge,color,strength))using(var g=Graphics.FromImage(result)){g.DrawImageUnscaled(shadow,(int)Math.Round(Component.Number(d,"shadowOffsetX",3,-30,30)*Resolution),(int)Math.Round(Component.Number(d,"shadowOffsetY",3,-30,30)*Resolution));g.DrawImageUnscaled(foreground,0,0);}foreground.Dispose();foreground=result;
            }
            return new StyledImage{Image=foreground,Margin=margin,Base=baseSize};
        }
        static GraphicsPath Rounded(RectangleF rect,float radius){var path=new GraphicsPath();float diameter=Math.Min(Math.Min(rect.Width,rect.Height),radius*2);if(diameter<=0){path.AddRectangle(rect);return path;}path.AddArc(rect.Left,rect.Top,diameter,diameter,180,90);path.AddArc(rect.Right-diameter,rect.Top,diameter,diameter,270,90);path.AddArc(rect.Right-diameter,rect.Bottom-diameter,diameter,diameter,0,90);path.AddArc(rect.Left,rect.Bottom-diameter,diameter,diameter,90,90);path.CloseFigure();return path;}
        static byte[] Alpha(Bitmap bitmap){var bits=bitmap.LockBits(new Rectangle(0,0,bitmap.Width,bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppArgb);try{var pixels=new byte[bits.Stride*bitmap.Height];Marshal.Copy(bits.Scan0,pixels,0,pixels.Length);var alpha=new byte[bitmap.Width*bitmap.Height];for(int y=0;y<bitmap.Height;y++)for(int x=0;x<bitmap.Width;x++)alpha[y*bitmap.Width+x]=pixels[y*bits.Stride+x*4+3];return alpha;}finally{bitmap.UnlockBits(bits);}}
        static Bitmap Mask(byte[] alpha,int width,int height,Color color,float opacity){var bitmap=new Bitmap(width,height,PixelFormat.Format32bppArgb);var bits=bitmap.LockBits(new Rectangle(0,0,width,height),ImageLockMode.WriteOnly,PixelFormat.Format32bppArgb);try{var pixels=new byte[bits.Stride*height];for(int y=0;y<height;y++)for(int x=0;x<width;x++){int p=y*bits.Stride+x*4;pixels[p]=color.B;pixels[p+1]=color.G;pixels[p+2]=color.R;pixels[p+3]=(byte)Math.Round(alpha[y*width+x]*opacity*color.A/255);}Marshal.Copy(pixels,0,bits.Scan0,pixels.Length);}finally{bitmap.UnlockBits(bits);}return bitmap;}
        static byte[] Maximum(byte[] alpha,int width,int height,int radius){if(radius<=0)return alpha;var temp=new byte[alpha.Length];var result=new byte[alpha.Length];var queue=new int[Math.Max(width,height)+radius*2+1];
            for(int y=0;y<height;y++){int head=0,tail=0;for(int x=0;x<width+radius;x++){while(head<tail&&queue[head]<x-radius*2)head++;if(x<width){while(head<tail&&alpha[y*width+queue[tail-1]]<=alpha[y*width+x])tail--;queue[tail++]=x;}int center=x-radius;if(center>=0&&center<width)temp[y*width+center]=head<tail?alpha[y*width+queue[head]]:(byte)0;}}
            for(int x=0;x<width;x++){int head=0,tail=0;for(int y=0;y<height+radius;y++){while(head<tail&&queue[head]<y-radius*2)head++;if(y<height){while(head<tail&&temp[queue[tail-1]*width+x]<=temp[y*width+x])tail--;queue[tail++]=y;}int center=y-radius;if(center>=0&&center<height)result[center*width+x]=head<tail?temp[queue[head]*width+x]:(byte)0;}}return result;
        }
        static byte[] BoxBlur(byte[] alpha,int width,int height,int radius){var temp=new byte[alpha.Length];var result=new byte[alpha.Length];int divisor=radius*2+1;
            for(int y=0;y<height;y++){int sum=0;for(int x=0;x<=radius&&x<width;x++)sum+=alpha[y*width+x];for(int x=0;x<width;x++){temp[y*width+x]=(byte)(sum/divisor);int remove=x-radius,add=x+radius+1;if(remove>=0)sum-=alpha[y*width+remove];if(add<width)sum+=alpha[y*width+add];}}
            for(int x=0;x<width;x++){int sum=0;for(int y=0;y<=radius&&y<height;y++)sum+=temp[y*width+x];for(int y=0;y<height;y++){result[y*width+x]=(byte)(sum/divisor);int remove=y-radius,add=y+radius+1;if(remove>=0)sum-=temp[remove*width+x];if(add<height)sum+=temp[add*width+x];}}return result;
        }
        static long Bytes(StyledImage image){long bytes=(long)image.Image.Width*image.Image.Height*4;foreach(var scaled in image.Sizes.Values)bytes+=(long)scaled.Width*scaled.Height*4;return bytes;}
        void TrimCache(StyledImage keep,Bitmap active){const long budget=64L*1024*1024;long bytes=0;foreach(var image in cache.Values)bytes+=Bytes(image);if(bytes<=budget)return;foreach(string key in new List<string>(cache.Keys)){var image=cache[key];if(image==keep)continue;bytes-=Bytes(image);image.Dispose();cache.Remove(key);if(bytes<=budget)return;}foreach(string key in new List<string>(keep.Sizes.Keys)){var image=keep.Sizes[key];if(image==active)continue;bytes-=(long)image.Width*image.Height*4;image.Dispose();keep.Sizes.Remove(key);if(bytes<=budget)return;}}
        void Clear(){foreach(var item in cache.Values)item.Dispose();cache.Clear();}
        public void Dispose(){Clear();drawAttributes.Dispose();}
    }
}

