using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using System.Xml;
using System.Runtime.InteropServices;
using CupheadInputDisplay;
class NativeTest {
    static int count;static string output;static Component component;static Editor editor;static Form form;
    static void Assert(bool condition,string label){if(!condition)throw new Exception(label);count++;Console.WriteLine("PASS "+label);}
    static Control Find(Control root,string name){if(root.Name==name)return root;foreach(Control c in root.Controls){var found=Find(c,name);if(found!=null)return found;}return null;}
    static void Invoke(object target,string method,object arg){target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(target,arg==null?null:new[]{arg});}
    static void Press(Button button,Keys key){typeof(Control).GetMethod("OnKeyDown",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(button,new object[]{new KeyEventArgs(key)});}
    [STAThread] static void Main(string[] args){output=args[0];AppDomain.CurrentDomain.AssemblyResolve+=(s,e)=>{var p=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Components",new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};try{Run();}catch(Exception ex){Console.WriteLine("FAIL "+ex);Environment.ExitCode=1;}}
    static Bitmap StarSample(bool pressed){var image=new Bitmap(400,170);var inputs=new bool[11];inputs[10]=pressed;using(var g=Graphics.FromImage(image))component.Render(g,400,170,component.StarBounds(),inputs,false,true);return image;}
    static void TestStar(){
        Assert(!component.StarEnabled,"Star Skip is optional and off by default");
        Assert(typeof(Component).Assembly.GetType("CupheadInputDisplay.StarAnimation")==null,"Star animation code has been removed");
        Assert(typeof(Component).Assembly.GetManifestResourceNames().Count(n=>n.Contains("/Star/"))==1,"Only one static star sprite is embedded");
        Assert(GameReader.IsWinScene("scene_win")&&!GameReader.IsWinScene("scene_level_flower")&&!GameReader.IsWinScene(null),"Win-screen detection excludes normal levels");
        Assert(Component.StarInput(true,false)&&Component.StarInput(false,true)&&!Component.StarInput(false,false),"Fixed Z and controller A activate Star Skip");
        var star=Component.Map(Component.Map(component.Config,"elementSettings"),"star");star["visible"]=true;
        Assert(!component.ElementVisible(7)&&component.ElementVisible(0),"Star stays hidden outside win screen");
        typeof(GameReader).GetProperty("OnWinScreen").SetValue(component.Reader,true,null);
        Assert(component.StarOnly&&component.ElementVisible(7)&&Enumerable.Range(0,7).All(i=>!component.ElementVisible(i)),"Win screen shows only the star");
        Assert(component.Bounds().Width==380&&component.ElementBounds(7).Width==50,"Star retains its small centered canvas");
        using(var idle=StarSample(false))using(var pressed=StarSample(true)){Assert(idle.GetPixel(200,85)==pressed.GetPixel(200,85)&&idle.GetPixel(200,85).R>200,"Defaults retain original star colors in both states");idle.Save(Path.Combine(output,"star-skip-preview.png"));}
        star["tintMode"]="Solid";star["idleColor"]="#FF0000FF";star["pressedColor"]="#FFFF0000";
        using(var idle=StarSample(false))using(var pressed=StarSample(true)){Assert(idle.GetPixel(200,85).B>250&&idle.GetPixel(200,85).R<5,"Inactive star supports its own blue color");Assert(pressed.GetPixel(200,85).R>250&&pressed.GetPixel(200,85).B<5,"Pressed star supports its own red color");}
        star["pressedTintAmount"]=0;using(var image=StarSample(true)){Assert(image.GetPixel(200,85).G>100,"Zero star color strength restores original gold artwork");}
        star["pressedTintAmount"]=100;star["pressedBrightness"]=0.5;using(var image=StarSample(true)){Assert(image.GetPixel(200,85).R>120&&image.GetPixel(200,85).R<135,"Star brightness is independently adjustable");}
        string saved=component.Save();component.Load(saved);star=Component.Map(Component.Map(component.Config,"elementSettings"),"star");Assert(component.StarEnabled&&Component.Text(star,"pressedColor","")=="#FFFF0000"&&Component.Number(star,"pressedBrightness",1,0,1)==0.5,"Star colors, brightness and visibility survive layout round-trip");
        typeof(GameReader).GetProperty("OnWinScreen").SetValue(component.Reader,false,null);Assert(!component.StarOnly&&component.ElementVisible(0),"Leaving results restores normal inputs");
        using(var preview=StarSample(true)){Assert(preview.GetPixel(200,85).A>200&&!component.Reader.OnWinScreen,"Star preview renders outside win screen without modifying game state");}
        component.Config=component.Defaults();component.Reader.Dispose();
    }
    static void Run(){
        var asm=typeof(Component).Assembly;
        Assert(!asm.GetReferencedAssemblies().Any(a=>a.Name.IndexOf("WebView",StringComparison.OrdinalIgnoreCase)>=0),"Component has no WebView dependency");
        Assert(!asm.GetManifestResourceNames().Any(n=>n.EndsWith(".html")||n.EndsWith(".js")||n.EndsWith(".css")),"Single DLL embeds artwork only; no browser resources");
        Assert(new Factory().Category==LiveSplit.UI.Components.ComponentCategory.Other,"Component appears under Other");
        component=new Component(null);
        TestStar();
        foreach(var character in new[]{"Cuphead","Mugman","Chalice"})foreach(var mode in new[]{"Normal","Plane"})foreach(var action in new[]{"Jump","Shoot","Dash","EX"})using(var stream=AssetStore.Open("assets/Display/"+mode+"/"+character+"/"+action+".png")){Assert(stream!=null,character+" "+mode+" "+action+" embedded sprite");}
        component.Load("{\"liveSplit\":{\"autoCharacter\":false,\"autoPlane\":false,\"character\":\"Mugman\",\"greenScreen\":true,\"height\":210},\"elementSettings\":{\"shoot\":{\"offsetX\":-300,\"scale\":2.5},\"lock\":{\"visible\":false}},\"keybinds\":{\"jump\":\"Space\"}}");
        Assert(!component.Options.ContainsKey("autoCharacter")&&!component.Options.ContainsKey("autoPlane")&&!component.Options.ContainsKey("character"),"Imported manual switches cannot disable automatic character or plane detection");
        Assert(component.Character=="Cuphead","Import cannot override the detected character");
        var doc=new XmlDocument();doc.AppendChild(component.GetSettings(doc));using(var restored=new Component(null)){restored.SetSettings(doc.DocumentElement);Assert(restored.Save()==component.Save(),"XML settings round-trip preserves original bindings, artwork and new background option");Assert(restored.Bounds().Left<0,"Negative artwork offsets included in render bounds");}
        using(var bitmap=new Bitmap(400,170))using(var g=Graphics.FromImage(bitmap)){component.Render(g,400,170);Assert(bitmap.GetPixel(0,0).ToArgb()==Color.Lime.ToArgb(),"Green-screen option fills component background with exact #00FF00");}
        component.Options["backgroundMode"]="Transparent";using(var bitmap=new Bitmap(400,170))using(var g=Graphics.FromImage(bitmap)){component.Render(g,400,170);Assert(bitmap.GetPixel(0,0).A==255,"Background remains opaque even with a legacy transparent setting");}
        component.Config=component.Defaults();
        TestInputStates();
        form=new Form{ClientSize=new Size(480,560),ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-2000,-2000)};
        editor=(Editor)component.GetSettingsControl(LiveSplit.UI.LayoutMode.Vertical);editor.Dock=DockStyle.Fill;form.Controls.Add(editor);
        Assert(component.ContextMenuControls==null&&component.GetType().GetMethod("OpenEditor")==null,"Settings are hosted exclusively in LiveSplit Layout Settings");
        form.Shown+=(s,e)=>{try{
            var tabs=editor.Controls.OfType<TabControl>().Single();Assert(tabs.TabPages.Count==5,"Five native settings tabs are present");
            var starToggle=(CheckBox)Find(editor,"ShowStarSkip");starToggle.Checked=true;Assert(component.StarEnabled,"General toggle enables optional Star Skip");
            var selector=(ComboBox)Find(editor,"SelectedElement");selector.SelectedIndex=7;Assert(((CheckBox)Find(editor,"ElementVisible")).Checked,"Layout exposes Star Skip visibility and placement");
            tabs.SelectedIndex=3;
            Assert(Find(editor,"StarStyleGroup").Visible&&Find(editor,"ElementIdleColor").Enabled&&Find(editor,"ElementPressedColor").Enabled,"Selecting Star Skip exposes enabled native color pickers and independent style controls");
            ((ComboBox)Find(editor,"Star_tintMode")).SelectedItem="Solid";
            var starSettings=Component.Map(Component.Map(component.Config,"elementSettings"),"star");starSettings["idleColor"]="#FF0000FF";starSettings["pressedColor"]="#FFFF0000";editor.RefreshSettings();
            ((NumericUpDown)Find(editor,"Star_pressedTintAmount")).Value=75;Assert(Component.Number(starSettings,"pressedTintAmount",0,0,100)==75,"Star color strength changes through native settings");((NumericUpDown)Find(editor,"Star_pressedTintAmount")).Value=100;
            var starPreview=(Preview)Find(editor,"ArtworkPreview");var state=(ComboBox)Find(editor,"PreviewMode_Layout");
            for(int mode=1;mode<=2;mode++){state.SelectedIndex=mode;using(var image=new Bitmap(starPreview.Width,starPreview.Height))using(var g=Graphics.FromImage(image)){Invoke(starPreview,"OnPaint",new PaintEventArgs(g,new Rectangle(Point.Empty,image.Size)));var center=image.GetPixel(image.Width/2,image.Height/2);Assert(mode==1?center.B>250&&center.R<5:center.R>250&&center.B<5,"Settings shows the correct star color outside Cuphead in preview mode "+mode);}}
            Assert(!component.Reader.OnWinScreen&&!component.StarOnly,"Settings preview never forces the real display into win-screen mode");
            using(var shot=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"star-settings-preview.png"));}
            state.SelectedIndex=0;tabs.SelectedIndex=0;
            ((ComboBox)Find(editor,"PreviewContent_Appearance")).SelectedIndex=1;Assert(((Preview)Find(editor,"StylePreview_Appearance")).ShowStar,"Appearance has a Star Skip preview selector");((ComboBox)Find(editor,"PreviewContent_Appearance")).SelectedIndex=0;
            ((CheckBox)Find(editor,"ElementVisible")).Checked=false;editor.RefreshSettings();Assert(!starToggle.Checked&&!component.StarEnabled,"Layout visibility synchronizes with General toggle");selector.SelectedIndex=0;
            TestLiveSplitColorPicker();
            TestSeparateWindow();
            form.PerformLayout();var general=tabs.TabPages[0];Assert(!general.VerticalScroll.Visible&&!general.HorizontalScroll.Visible,"Compact inline General page fits without scrolling at 480 by 560");
            Assert(Find(editor,"GeneralDisplayGroup") is GroupBox&&Find(editor,"GeneralBackgroundGroup") is GroupBox,"General groups controls into compact native sections");
            var detached=(CheckBox)Find(editor,"separateWindow");detached.Checked=true;form.PerformLayout();Assert(!general.VerticalScroll.Visible,"General still fits without scrolling with window controls expanded");
            var transparent=(CheckBox)Find(editor,"windowTransparent");transparent.Checked=true;Assert(!Find(editor,"GeneralBackgroundGroup").Visible,"Transparent-window mode hides unused background settings");transparent.Checked=false;Assert(Find(editor,"GeneralBackgroundGroup").Visible,"Opaque mode restores background controls");detached.Checked=false;
            Assert(!Find(editor,"WindowOptions").Visible,"Inline mode hides separate-window controls");
            Assert(((ComboBox)Find(editor,"PreviewMode_Appearance")).SelectedIndex==0&&((ComboBox)Find(editor,"PreviewMode_Effects")).SelectedIndex==0,"Appearance and effects previews default to actual live inputs");
            TestPreview();
            var sections=(TabControl)Find(editor,"EffectSections");Assert(sections.TabPages.Cast<TabPage>().Select(t=>t.Text).SequenceEqual(new[]{"Borders","Shadow"}),"Effects are separated into two focused sections");
            Assert(Find(editor,"idleFillColor")==null&&Find(editor,"pressedFillOpacity")==null,"Input-background controls are completely removed");
            Assert(Find(editor,"ChooseBackgroundImage") is Button&&Find(editor,"RemoveBackgroundImage") is Button&&Find(editor,"backgroundImageMode") is ComboBox,"Component background image controls are available");
            foreach(string color in new[]{"backgroundColor","idleColor","pressedColor","idleBorderColor","pressedBorderColor","idleShadowColor","pressedShadowColor","ElementIdleColor","ElementPressedColor"})Assert(Find(editor,color+"_picker") is Button,"Explicit Windows color picker for "+color);
            tabs.SelectedIndex=2;sections.SelectedIndex=0;Assert(!Find(editor,"BorderDetails").Visible,"Disabled border settings are collapsed");((CheckBox)Find(editor,"borderEnabled")).Checked=true;Assert(Find(editor,"BorderDetails").Visible,"Enabling borders reveals their settings");
            sections.SelectedIndex=1;Assert(!Find(editor,"ShadowDetails").Visible,"Disabled shadow settings are collapsed");((CheckBox)Find(editor,"shadowEnabled")).Checked=true;Assert(Find(editor,"ShadowDetails").Visible&&!Find(editor,"ShadowAdvanced").Visible,"Shadow starts with basic controls only");((Button)Find(editor,"ToggleShadowAdvanced")).PerformClick();Assert(Find(editor,"ShadowAdvanced").Visible,"More shadow options expands advanced controls");((NumericUpDown)Find(editor,"shadowBlur")).Value=7;((Button)Find(editor,"ToggleShadowAdvanced")).PerformClick();Assert(!Find(editor,"ShadowAdvanced").Visible&&Component.Number(Component.Map(component.Config,"displaySettings"),"shadowBlur",0,0,12)==7,"Collapsing advanced options preserves effect settings");
            ((CheckBox)Find(editor,"borderEnabled")).Checked=false;((CheckBox)Find(editor,"shadowEnabled")).Checked=false;sections.SelectedIndex=0;
            using(var shot=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"effects-preview.png"));}
            tabs.SelectedIndex=0;
            Assert(Find(editor,"backgroundMode")==null&&Find(editor,"backgroundOpacity")==null,"Transparent background and background-opacity options are removed");
            Assert(Find(editor,"idleTintAmount") is NumericUpDown&&Find(editor,"pressedTintAmount") is NumericUpDown,"Inactive and pressed color strengths have independent native controls");
            Assert(Find(editor,"pressedBorderWidth") is NumericUpDown&&Find(editor,"pressedBorderColor") is Button&&Find(editor,"shadowBlur") is NumericUpDown,"Border color, thickness and drop-shadow settings are available");
            Assert(Find(editor,"autoCharacter")==null&&Find(editor,"autoPlane")==null,"Automatic character and plane toggles are absent");
            Assert(Find(editor,"backgroundColor") is Button&&Find(editor,"height") is NumericUpDown,"Background options use native dropdowns, color selectors and numeric controls");
            Assert(Find(editor,"GreenScreenPreset")==null,"Green-screen preset has been removed");
            component.Options["backgroundColor"]="#FF000000";
            ((NumericUpDown)Find(editor,"height")).Value=237;Assert(component.VerticalHeight==237,"Native component dimension control updates renderer");
            tabs.SelectedIndex=1;
            ((NumericUpDown)Find(editor,"idleBrightness")).Value=73;Assert(Component.Number(Component.Map(component.Config,"displaySettings"),"idleBrightness",0,0,1)==0.73,"Native brightness percentage updates normalized setting");
            ((NumericUpDown)Find(editor,"pressedTintAmount")).Value=37;Assert(Component.Number(Component.Map(component.Config,"displaySettings"),"pressedTintAmount",0,0,100)==37,"Native color-strength control saves the selected percentage");
            ((CheckBox)Find(editor,"invertStates")).Checked=true;Assert(Component.Flag(Component.Map(component.Config,"displaySettings"),"invertStates",false),"Native invert toggle swaps inactive and pressed style selection");
            ((CheckBox)Find(editor,"scaleOnPress")).Checked=true;Assert(Find(editor,"pressScale").Enabled,"Press-size control enables with press scaling");
            ((CheckBox)Find(editor,"scaleOnPress")).Checked=false;Assert(!Find(editor,"pressScale").Enabled,"Press-size control disables without press scaling");
            tabs.SelectedIndex=3;
            ((ComboBox)Find(editor,"SelectedElement")).SelectedIndex=3;((NumericUpDown)Find(editor,"ElementX")).Value=-41;((NumericUpDown)Find(editor,"ElementY")).Value=27;((NumericUpDown)Find(editor,"ElementScale")).Value=135;
            var settings=Component.Map(Component.Map(component.Config,"elementSettings"),"shoot");Assert(Component.Number(settings,"offsetX",0,-10000,10000)==-41&&Component.Number(settings,"scale",0,0,3)==1.35,"Per-element native position and size controls update the selected artwork");
            ((CheckBox)Find(editor,"ElementVisible")).Checked=false;Assert(!Component.Flag(settings,"visible",true),"Per-element visibility is editable natively");((CheckBox)Find(editor,"ElementVisible")).Checked=true;
            ((Button)Find(editor,"ResetPosition")).PerformClick();Assert(Component.Number(settings,"offsetX",1,-10000,10000)==0&&Component.Number(settings,"offsetY",1,-10000,10000)==0,"Reset position restores original offsets");
            ((ComboBox)Find(editor,"SelectedElement")).SelectedIndex=2;var preview=(Preview)Find(editor,"ArtworkPreview");var b=component.Bounds();float fit=Math.Min((preview.ClientSize.Width-12)/b.Width,(preview.ClientSize.Height-12)/b.Height);var r=component.ElementBounds(2);int x=(int)((preview.ClientSize.Width-b.Width*fit)/2+(r.X+r.Width/2-b.X)*fit),y=(int)((preview.ClientSize.Height-b.Height*fit)/2+(r.Y+r.Height/2-b.Y)*fit);
            Invoke(preview,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,x,y,0));Invoke(preview,"OnMouseMove",new MouseEventArgs(MouseButtons.Left,0,x+30,y+10,0));Invoke(preview,"OnMouseUp",new MouseEventArgs(MouseButtons.Left,1,x+30,y+10,0));
            Assert(Component.Number(Component.Map(Component.Map(component.Config,"elementSettings"),"jump"),"offsetX",0,-10000,10000)>0,"Native artwork preview supports dragging");
            ((CheckBox)Find(editor,"ElementCustomColors")).Checked=true;Assert(Find(editor,"ElementIdleColor").Enabled&&Find(editor,"ElementPressedColor").Enabled,"Selected elements can enable their own inactive and pressed colors");
            using(var shot=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"native-layout-preview.png"));}
            tabs.SelectedIndex=4;
            foreach(string action in Component.Actions)Assert(Find(editor,"Key_"+action) is Button,"Native keyboard binding for "+action);
            var jump=(Button)Find(editor,"Key_jump");jump.PerformClick();Press(jump,Keys.Q);Assert(Component.Text(Component.Map(component.Config,"keybinds"),"jump","")=="KeyQ","Keyboard binding capture saves selected key");jump.PerformClick();Press(jump,Keys.Escape);Assert(Component.Text(Component.Map(component.Config,"keybinds"),"jump","x")=="","Escape clears keyboard binding");
            Assert(Editor.KeyCode(Keys.Up)=="ArrowUp"&&Editor.KeyCode(Keys.F12)=="F12"&&Editor.KeyCode(Keys.NumPad5)=="Numpad5","Native key capture maps arrows, function keys and numpad keys");
            ((Button)Find(editor,"Pad_jump")).PerformClick();Array.Clear(component.PadButtons,0,component.PadButtons.Length);Invoke(editor,"RefreshRuntime",null);component.PadButtons[17]=true;Invoke(editor,"RefreshRuntime",null);
            Assert(Component.Number(Component.Map(component.Config,"gamepadBinds"),"jump",0,0,127)==17,"Native controller rebinding waits for release and captures raw buttons");
            ((ComboBox)Find(editor,"inputSource")).SelectedItem="DirectInput";Assert(Component.Text(component.Options,"inputSource","")=="DirectInput","Native controller source selection persists");
            Assert(!tabs.TabPages.Cast<TabPage>().Any(t=>t.Text=="Config")&&Find(editor,"ImportConfig")==null,"Config tab and its controls have been removed");
            component.Load("{\"keybinds\":{\"jump\":\"Space\"},\"displaySettings\":{\"idleBrightness\":0.7},\"liveSplit\":{\"autoCharacter\":false,\"autoPlane\":false,\"greenScreen\":true,\"height\":205}}");
            Assert(((NumericUpDown)Find(editor,"height")).Value==205&&Component.Text(component.Options,"backgroundMode","")=="Solid"&&jump.Text=="Space","Original JSON config import refreshes native controls");
            Assert(!component.Options.ContainsKey("autoCharacter")&&!component.Options.ContainsKey("autoPlane"),"Automatic detection remains mandatory after importing legacy config");
            tabs.SelectedIndex=0;
            using(var shot=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"native-settings-preview.png"));}
            Console.WriteLine("SUCCESS "+count+" assertions");
        }catch(Exception ex){Console.WriteLine("FAIL "+ex);Environment.ExitCode=1;}finally{form.Close();component.Dispose();}};
        Application.Run(form);
    }
    static void TestInputStates(){
        IntPtr block=Marshal.AllocHGlobal(256);try{Marshal.Copy(new byte[256],0,block,256);using(var reader=new GameReader()){
            var type=typeof(GameReader);type.GetField("handle",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(reader,new IntPtr(-1));
            string[] fields={"buttonAddresses","axis1Addresses","axis2Addresses","deadzoneAddresses"};int[] starts={0,32,64,96};
            for(int j=0;j<4;j++){var addresses=(long[])type.GetField(fields[j],BindingFlags.Instance|BindingFlags.NonPublic).GetValue(reader);for(int i=0;i<8;i++)addresses[i]=block.ToInt64()+starts[j]+(j==0?i:i*4);}
            for(int i=0;i<8;i++)Marshal.Copy(BitConverter.GetBytes(0.5f),0,IntPtr.Add(block,96+i*4),4);
            Marshal.WriteByte(block,0,1);Marshal.Copy(BitConverter.GetBytes(0.8f),0,IntPtr.Add(block,32+4),4);Marshal.Copy(BitConverter.GetBytes(-0.8f),0,IntPtr.Add(block,32+6*4),4);Marshal.Copy(BitConverter.GetBytes(0.8f),0,IntPtr.Add(block,32+7*4),4);
            type.GetField("inputReady",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(reader,true);var state=new bool[10];Assert(reader.TryInputs(state),"Read-only in-game action-state sampler reads native memory");
            Assert(state[0]&&state[1]&&!state[2],"In-game action states map Jump and Shoot independently of keyboard bindings");
            Assert(state[6]&&!state[7]&&state[8]&&!state[9],"In-game diagonal axes map Up and Left correctly");
            Marshal.Copy(BitConverter.GetBytes(float.NaN),0,IntPtr.Add(block,32),4);var fallback=new bool[10];fallback[2]=true;Assert(!reader.TryInputs(fallback)&&fallback[2],"Invalid game input falls back without corrupting fallback button states");
        }}finally{Marshal.FreeHGlobal(block);}
    }
    static void TestLiveSplitColorPicker(){
        var swatch=(Button)Find(editor,"backgroundColor");Assert(swatch.Size==new Size(23,23)&&swatch.MaximumSize==new Size(23,23),"Color swatch is fixed at LiveSplit's compact 23-pixel size");
        Assert(Find(editor,"BackgroundImageThumbnail").Size==new Size(23,23),"Background-image thumbnail uses LiveSplit's compact button size");
        Exception failure=null;bool opened=false;using(var closer=new System.Windows.Forms.Timer{Interval=50}){closer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.GetType().FullName=="Fetze.WinFormsColor.ColorPickerDialog");if(dialog==null)return;closer.Stop();try{opened=true;Assert(Find(dialog,"btnPickColor") is Button,"LiveSplit's real color dialog contains its screen eyedropper");dialog.GetType().GetProperty("SelectedColor").SetValue(dialog,Color.CornflowerBlue,null);dialog.DialogResult=DialogResult.OK;}catch(Exception ex){failure=ex;dialog.DialogResult=DialogResult.Cancel;}finally{dialog.Close();}};closer.Start();((Button)Find(editor,"backgroundColor_picker")).PerformClick();}
        if(failure!=null)throw failure;Assert(opened,"Choose opens the actual LiveSplit color picker rather than the default Windows dialog");var selected=SpriteEffects.ReadColor(component.Options,"backgroundColor",Color.Black);Assert(Math.Abs(selected.R-100)<=1&&Math.Abs(selected.G-149)<=1&&Math.Abs(selected.B-237)<=1,"Accepting a picked color updates the saved component color");
        Assert(swatch.BackColor.ToArgb()==selected.ToArgb()&&Find(editor,"backgroundColor_hex").Text.StartsWith("#"),"Compact swatch and hex label update to the picked color");
        string accepted=Component.Text(component.Options,"backgroundColor","");using(var closer=new System.Windows.Forms.Timer{Interval=50}){closer.Tick+=(s,e)=>{var dialog=Application.OpenForms.Cast<Form>().FirstOrDefault(f=>f.GetType().FullName=="Fetze.WinFormsColor.ColorPickerDialog");if(dialog==null)return;closer.Stop();dialog.GetType().GetProperty("SelectedColor").SetValue(dialog,Color.Red,null);dialog.DialogResult=DialogResult.Cancel;dialog.Close();};closer.Start();swatch.PerformClick();}
        Assert(Component.Text(component.Options,"backgroundColor","")==accepted,"Cancel restores the previously accepted color");component.Config=component.Defaults();editor.RefreshSettings();
    }
    static void TestSeparateWindow(){
        var toggle=(CheckBox)Find(editor,"separateWindow");Assert(toggle!=null&&!toggle.Checked,"Separate display is an optional native General setting");
        toggle.Checked=true;var window=Application.OpenForms.OfType<DisplayWindow>().Single();Assert(window.Visible,"Enabling the setting opens the display window");
        using(var shot=new Bitmap(form.ClientSize.Width,form.ClientSize.Height)){form.DrawToBitmap(shot,new Rectangle(Point.Empty,shot.Size));shot.Save(Path.Combine(output,"separate-window-settings.png"));}
        Assert(component.VerticalHeight==0&&component.HorizontalWidth==0&&component.MinimumWidth==0&&component.MinimumHeight==0,"Separate-window mode reserves no space in either LiveSplit layout orientation");
        using(var bitmap=new Bitmap(400,200))using(var g=Graphics.FromImage(bitmap)){component.DrawVertical(g,null,400,null);component.DrawHorizontal(g,null,200,null);Assert(bitmap.GetPixel(200,100).A==0,"Detached component does not draw a duplicate in the splits");}
        ((NumericUpDown)Find(editor,"windowWidth")).Value=620;((NumericUpDown)Find(editor,"windowHeight")).Value=230;((CheckBox)Find(editor,"windowTopMost")).Checked=true;((CheckBox)Find(editor,"windowTitleBar")).Checked=false;component.SyncDisplayWindow();
        Assert(window.ClientSize==new Size(620,230)&&window.TopMost&&window.FormBorderStyle==FormBorderStyle.None,"Window size, always-on-top and title-bar settings are applied");
        window.Location=new Point(210,180);Assert(Component.Number(component.Options,"windowX",0,-30000,30000)==210,"Moving the display saves its position");
        window.ClientSize=new Size(610,220);Assert(Component.Number(component.Options,"windowWidth",0,0,2000)==610&&Component.Number(component.Options,"windowHeight",0,0,1200)==220,"Resizing the display saves its client dimensions");
        var document=new XmlDocument();document.AppendChild(component.GetSettings(document));using(var restored=new Component(null)){restored.SetSettings(document.DocumentElement);Assert(restored.Detached&&restored.VerticalHeight==0&&Component.Number(restored.Options,"windowWidth",0,0,2000)==610,"Separate-window mode and geometry persist in the LiveSplit layout");}
        using(var sample=new Bitmap(610,220))using(var expected=new Bitmap(610,220))using(var a=Graphics.FromImage(sample))using(var b=Graphics.FromImage(expected)){Invoke(window,"OnPaint",new PaintEventArgs(a,new Rectangle(Point.Empty,sample.Size)));component.Render(b,610,220);bool equal=true;for(int y=0;y<220&&equal;y++)for(int x=0;x<610;x++)if(sample.GetPixel(x,y).ToArgb()!=expected.GetPixel(x,y).ToArgb()){equal=false;break;}Assert(equal,"Separate window uses the same input/artwork/background renderer");}
        var transparent=(CheckBox)Find(editor,"windowTransparent");Assert(transparent!=null,"Transparent background is a separate-window setting");transparent.Checked=true;Component.Map(component.Config,"displaySettings")["shadowEnabled"]=true;component.SyncDisplayWindow();window.RefreshDisplay();
        Assert(string.IsNullOrEmpty(window.LastTransparencyError),"Windows accepts the per-pixel alpha surface: "+window.LastTransparencyError);
        Assert(window.FormBorderStyle==FormBorderStyle.None&&window.TransparencyKey.IsEmpty&&window.Opacity==1,"Transparent mode uses alpha without a color key or whole-window fading");
        var surface=(LayeredSurface)typeof(DisplayWindow).GetField("surface",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(window);Assert(surface!=null&&surface.Bitmap!=null,"Transparent window has a native layered surface");
        Assert(surface.Bitmap.GetPixel(0,0).A==0,"Empty window areas are fully transparent");
        bool opaque=false,soft=false,black=false;for(int y=0;y<surface.Bitmap.Height;y++)for(int x=0;x<surface.Bitmap.Width;x++){var c=surface.Bitmap.GetPixel(x,y);opaque|=c.A==255;soft|=c.A>0&&c.A<255;black|=c.A==255&&c.R==0&&c.G==0&&c.B==0;}Assert(opaque&&soft,"Artwork and anti-aliased shadows retain their individual alpha values");Assert(black,"Black artwork pixels remain opaque instead of being keyed out");
        var bits=surface.Bitmap.LockBits(new Rectangle(0,0,surface.Bitmap.Width,surface.Bitmap.Height),ImageLockMode.ReadOnly,PixelFormat.Format32bppPArgb);bool premultiplied=true;try{var bytes=new byte[bits.Stride*surface.Bitmap.Height];Marshal.Copy(bits.Scan0,bytes,0,bytes.Length);for(int p=0;p<bytes.Length;p+=4)if(bytes[p]>bytes[p+3]+1||bytes[p+1]>bytes[p+3]+1||bytes[p+2]>bytes[p+3]+1){premultiplied=false;break;}}finally{surface.Bitmap.UnlockBits(bits);}Assert(premultiplied,"Layered surface uses the premultiplied alpha format required by Windows");
        transparent.Checked=false;component.SyncDisplayWindow();Assert(window.FormBorderStyle==FormBorderStyle.None,"Disabling transparency preserves the user's borderless preference");((CheckBox)Find(editor,"windowTitleBar")).Checked=true;component.SyncDisplayWindow();Assert(window.FormBorderStyle==FormBorderStyle.Sizable,"Opaque mode can restore the regular title bar");
        transparent.Checked=true;component.SyncDisplayWindow();window.RefreshDisplay();Assert(window.FormBorderStyle==FormBorderStyle.None&&string.IsNullOrEmpty(window.LastTransparencyError)&&!Find(editor,"windowTitleBar").Enabled,"A titled window switches to working per-pixel transparency");transparent.Checked=false;component.SyncDisplayWindow();Assert(window.FormBorderStyle==FormBorderStyle.Sizable&&Find(editor,"windowTitleBar").Enabled,"Leaving transparent mode restores the saved title-bar setting");
        window.Close();component.SyncDisplayWindow();Assert(!component.Detached&&component.VerticalHeight==170&&window.IsDisposed,"Closing the window returns to the inline display and restores its height");
        toggle.Checked=true;window=Application.OpenForms.OfType<DisplayWindow>().Single();toggle.Checked=false;Assert(window.IsDisposed&&!component.Detached,"Disabling the setting closes the separate window");
        using(var temporary=new Component(null)){temporary.Options["separateWindow"]=true;temporary.Options["windowX"]=30000;temporary.Options["windowY"]=30000;temporary.SyncDisplayWindow();window=Application.OpenForms.OfType<DisplayWindow>().Single();Assert(Screen.AllScreens.Any(s=>s.WorkingArea.IntersectsWith(window.Bounds)),"An off-screen saved position is recovered onto an available monitor");temporary.Dispose();Assert(window.IsDisposed,"Removing the component disposes its display window");}
        component.Config=component.Defaults();editor.RefreshSettings();
    }
    static void TestPreview(){
        var preview=(Preview)Find(editor,"StylePreview_Appearance");preview.Size=new Size(420,150);
        var actual=new bool[component.Pressed.Length];actual[0]=true;actual[3]=true;actual[6]=true;Array.Copy(actual,component.Pressed,actual.Length);
        var d=Component.Map(component.Config,"displaySettings");d["tintMode"]="Solid";d["idleColor"]="#FFFF3000";d["pressedColor"]="#FF00CCFF";d["pressedTintAmount"]=65;
        for(int mode=0;mode<3;mode++){
            preview.InputPreviewMode=mode;bool[] expected=(bool[])actual.Clone();if(mode>0)for(int i=0;i<expected.Length;i++)expected[i]=mode==2;
            using(var sample=new Bitmap(preview.ClientSize.Width,preview.ClientSize.Height))using(var display=new Bitmap(sample.Width,sample.Height))using(var a=Graphics.FromImage(sample))using(var b=Graphics.FromImage(display)){
                Invoke(preview,"OnPaint",new PaintEventArgs(a,new Rectangle(Point.Empty,sample.Size)));component.Render(b,display.Width,display.Height,component.Bounds(),expected);
                bool equal=true;for(int y=0;y<sample.Height&&equal;y++)for(int x=0;x<sample.Width;x++)if(sample.GetPixel(x,y).ToArgb()!=display.GetPixel(x,y).ToArgb()){equal=false;break;}
                Assert(equal,"Preview colors and input states match the display renderer exactly in mode "+mode);
                Assert(component.Pressed.SequenceEqual(actual),"Preview mode "+mode+" never changes live input state");
            }
        }
        preview.InputPreviewMode=0;component.Config=component.Defaults();editor.RefreshSettings();
    }
}
