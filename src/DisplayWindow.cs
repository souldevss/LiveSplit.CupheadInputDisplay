using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
namespace CupheadInputDisplay {
    public sealed class DisplayWindow : Form {
        readonly Component owner;bool applying,transparent;LayeredSurface surface;
        public string LastTransparencyError {get;private set;}
        public Action ClosedByUser;
        public DisplayWindow(Component owner){this.owner=owner;Text="Cuphead Input Display";DoubleBuffered=true;ShowInTaskbar=true;StartPosition=FormStartPosition.Manual;MinimumSize=new Size(120,80);
            Move+=(s,e)=>SaveBounds();Resize+=(s,e)=>{SaveBounds();if(!applying)RefreshDisplay();};
            FormClosing+=(s,e)=>{if(e.CloseReason==CloseReason.UserClosing&&ClosedByUser!=null)ClosedByUser();};
        }
        protected override bool ShowWithoutActivation {get{return true;}}
        protected override CreateParams CreateParams {get{var value=base.CreateParams;if(transparent)value.ExStyle|=0x80000;return value;}}
        protected override void OnShown(EventArgs e){base.OnShown(e);RefreshDisplay();}
        public void ApplySettings(){
            applying=true;try{bool desired=Component.Flag(owner.Options,"windowTransparent",false);bool changed=transparent!=desired;transparent=desired;if(changed&&surface!=null){surface.Dispose();surface=null;}bool title=!transparent&&Component.Flag(owner.Options,"windowTitleBar",true);var border=title?FormBorderStyle.Sizable:FormBorderStyle.None;if(FormBorderStyle!=border)FormBorderStyle=border;if(changed&&IsHandleCreated)RecreateHandle();TopMost=Component.Flag(owner.Options,"windowTopMost",false);
                var size=new Size((int)Component.Number(owner.Options,"windowWidth",400,120,2000),(int)Component.Number(owner.Options,"windowHeight",170,80,1200));if(ClientSize!=size)ClientSize=size;
                int x=(int)Component.Number(owner.Options,"windowX",100,-30000,30000),y=(int)Component.Number(owner.Options,"windowY",100,-30000,30000);var bounds=new Rectangle(x,y,Width,Height);bool visible=false;foreach(var screen in Screen.AllScreens)if(screen.WorkingArea.IntersectsWith(bounds)){visible=true;break;}if(!visible){var area=Screen.PrimaryScreen.WorkingArea;x=area.X+50;y=area.Y+50;owner.Options["windowX"]=x;owner.Options["windowY"]=y;}if(Location!=new Point(x,y))Location=new Point(x,y);
            }finally{applying=false;}
        }
        void SaveBounds(){if(applying||WindowState!=FormWindowState.Normal||!Visible)return;owner.Options["windowX"]=Location.X;owner.Options["windowY"]=Location.Y;owner.Options["windowWidth"]=ClientSize.Width;owner.Options["windowHeight"]=ClientSize.Height;}
        public void RefreshDisplay(){if(applying||IsDisposed)return;if(!transparent){Invalidate();return;}if(!Visible||!IsHandleCreated||WindowState==FormWindowState.Minimized)return;try{if(surface==null)surface=new LayeredSurface();surface.Present(Handle,new Rectangle(Location,ClientSize),owner);LastTransparencyError=null;}catch(Exception ex){LastTransparencyError=ex.Message;System.Diagnostics.Trace.WriteLine("Cuphead transparent window: "+ex.Message);}}
        protected override void OnPaintBackground(PaintEventArgs e){ }
        protected override void OnPaint(PaintEventArgs e){if(!transparent)owner.Render(e.Graphics,ClientSize.Width,ClientSize.Height);}
        [DllImport("user32.dll")]static extern bool ReleaseCapture();
        [DllImport("user32.dll")]static extern IntPtr SendMessage(IntPtr window,int message,IntPtr wParam,IntPtr lParam);
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(FormBorderStyle==FormBorderStyle.None&&e.Button==MouseButtons.Left){ReleaseCapture();SendMessage(Handle,0xa1,new IntPtr(2),IntPtr.Zero);}}
        protected override void Dispose(bool disposing){if(disposing&&surface!=null){surface.Dispose();surface=null;}base.Dispose(disposing);}
    }
}
