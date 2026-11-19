using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace CupheadInputDisplay {
    public sealed class DisplayWindow : Form {
        readonly Component owner;bool applying;
        public Action ClosedByUser;
        public DisplayWindow(Component owner){this.owner=owner;Text="Cuphead Input Display";DoubleBuffered=true;ShowInTaskbar=true;StartPosition=FormStartPosition.Manual;MinimumSize=new Size(120,80);
            Move+=(s,e)=>SaveBounds();Resize+=(s,e)=>{SaveBounds();Invalidate();};
            FormClosing+=(s,e)=>{if(e.CloseReason==CloseReason.UserClosing&&ClosedByUser!=null)ClosedByUser();};
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        public void ApplySettings(){
            applying=true;try{bool title=Component.Flag(owner.Options,"windowTitleBar",true);var border=title?FormBorderStyle.Sizable:FormBorderStyle.None;if(FormBorderStyle!=border)FormBorderStyle=border;TopMost=Component.Flag(owner.Options,"windowTopMost",false);
                var size=new Size((int)Component.Number(owner.Options,"windowWidth",400,120,2000),(int)Component.Number(owner.Options,"windowHeight",170,80,1200));if(ClientSize!=size)ClientSize=size;
                int x=(int)Component.Number(owner.Options,"windowX",100,-30000,30000),y=(int)Component.Number(owner.Options,"windowY",100,-30000,30000);var bounds=new Rectangle(x,y,Width,Height);bool visible=false;foreach(var screen in Screen.AllScreens)if(screen.WorkingArea.IntersectsWith(bounds)){visible=true;break;}if(!visible){var area=Screen.PrimaryScreen.WorkingArea;x=area.X+50;y=area.Y+50;owner.Options["windowX"]=x;owner.Options["windowY"]=y;}if(Location!=new Point(x,y))Location=new Point(x,y);
            }finally{applying=false;}
        }
        void SaveBounds(){if(applying||WindowState!=FormWindowState.Normal||!Visible)return;owner.Options["windowX"]=Location.X;owner.Options["windowY"]=Location.Y;owner.Options["windowWidth"]=ClientSize.Width;owner.Options["windowHeight"]=ClientSize.Height;}
        protected override void OnPaintBackground(PaintEventArgs e){ }
        protected override void OnPaint(PaintEventArgs e){owner.Render(e.Graphics,ClientSize.Width,ClientSize.Height);}
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(FormBorderStyle==FormBorderStyle.None&&e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
    }
}
