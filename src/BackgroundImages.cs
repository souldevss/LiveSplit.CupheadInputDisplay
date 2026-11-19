using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
namespace CupheadInputDisplay {
    public static class BackgroundImages {
        public static string Encode(Stream stream){
            using(var image=Image.FromStream(stream,true,true)){
                if((long)image.Width*image.Height>64L*1024*1024)throw new ArgumentException("Choose an image smaller than 64 megapixels.");
                if(Array.IndexOf(image.PropertyIdList,274)>=0){int orientation=image.GetPropertyItem(274).Value[0];RotateFlipType[] transforms={RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipNone,RotateFlipType.RotateNoneFlipX,RotateFlipType.Rotate180FlipNone,RotateFlipType.Rotate180FlipX,RotateFlipType.Rotate90FlipX,RotateFlipType.Rotate90FlipNone,RotateFlipType.Rotate270FlipX,RotateFlipType.Rotate270FlipNone};if(orientation>1&&orientation<transforms.Length)image.RotateFlip(transforms[orientation]);}
                double scale=Math.Min(1,2048.0/Math.Max(image.Width,image.Height));int width=Math.Max(1,(int)Math.Round(image.Width*scale)),height=Math.Max(1,(int)Math.Round(image.Height*scale));
                using(var bitmap=new Bitmap(width,height,PixelFormat.Format32bppPArgb))using(var g=Graphics.FromImage(bitmap))using(var output=new MemoryStream()){g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.DrawImage(image,new Rectangle(0,0,width,height));bitmap.Save(output,ImageFormat.Png);return Convert.ToBase64String(output.ToArray());}
            }
        }
        public static Bitmap Decode(string encoded){using(var stream=new MemoryStream(Convert.FromBase64String(encoded)))using(var original=Image.FromStream(stream)){var bitmap=new Bitmap(original.Width,original.Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bitmap))g.DrawImageUnscaled(original,0,0);return bitmap;}}
    }
}
