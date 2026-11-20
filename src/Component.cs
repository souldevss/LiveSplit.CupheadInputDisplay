using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

using System.Reflection;
using System.Runtime.InteropServices;
using System.Web.Script.Serialization;
using System.Windows.Forms;
using System.Xml;
using LiveSplit.Model;
using LiveSplit.UI;
using LiveSplit.UI.Components;

[assembly: AssemblyTitle("Cuphead Input Display")]
[assembly: AssemblyVersion("1.4.0.0")]
[assembly: ComponentFactory(typeof(CupheadInputDisplay.Factory))]
namespace CupheadInputDisplay {
    public sealed class Factory : IComponentFactory {
        public string ComponentName {get{return "Cuphead Input Display";}}
        public string Description {get{return "Cuphead input artwork, full layout editor, automatic character and plane detection.";}}
        public ComponentCategory Category {get{return ComponentCategory.Other;}}
        public IComponent Create(LiveSplitState state){return new Component(state);}
        public string UpdateName {get{return ComponentName;}}
        public string UpdateURL {get{return "";}}
        public string XMLURL {get{return "";}}
        public Version Version {get{return new Version(1,4,0,0);}}
    }
    public sealed class Component : IComponent {
        public readonly JavaScriptSerializer Json=new JavaScriptSerializer {MaxJsonLength=32*1024*1024};
        public Dictionary<string,object> Config;
        public readonly GameReader Reader=new GameReader();
        public string Character="Cuphead";
        public bool Plane;
        public readonly bool[] Pressed=new bool[11];
        public bool[] PadButtons=new bool[128];
        public string PadSource="No controller";
        readonly StarAnimation star=new StarAnimation();
        readonly GenericPad genericPad=new GenericPad();
        readonly Dictionary<string,Bitmap> images=new Dictionary<string,Bitmap>(StringComparer.OrdinalIgnoreCase);
        readonly SpriteEffects effects=new SpriteEffects();

        readonly System.Windows.Forms.Timer timer;
        readonly bool[] lastPressed=new bool[11];bool dirty=true,lastPlane,lastWin;string lastCharacter,lastConfig,lastImageSignature;long lastConfigCheck;float lastWidth,lastHeight;LayoutMode lastMode;
        public event Action Changed;
        long lastGamePoll; bool disposed; Editor settings;
        DisplayWindow displayWindow;string windowSignature;bool windowDirty=true;
        public bool Detached {get{return Flag(Options,"separateWindow",false);}}
        public static readonly string[] Actions={"jump","shoot","dash","ex","lock","swap","up","down","left","right"};
        public static readonly string[] Elements={"lock","swap","jump","shoot","dash","ex","dpad","star"};
        public static readonly PointF[] Positions={new PointF(0,0),new PointF(0,74),new PointF(78,0),new PointF(158,0),new PointF(78,80),new PointF(158,80),new PointF(270,21),new PointF(165,48)};
        public Component(LiveSplitState state){
            Config=Defaults();
            ContextMenuControls=null;
            timer=new System.Windows.Forms.Timer {Interval=16}; timer.Tick+=(s,e)=>Tick();timer.Start();
        }
        public Dictionary<string,object> Defaults(){
            var keys=new Dictionary<string,object>();string[] codes={"KeyZ","KeyX","KeyC","KeyV","KeyA","KeyS","ArrowUp","ArrowDown","ArrowLeft","ArrowRight"};for(int i=0;i<Actions.Length;i++)keys[Actions[i]]=codes[i];
            var pads=new Dictionary<string,object>{{"jump",0},{"shoot",2},{"dash",1},{"ex",3},{"lock",4},{"swap",5}};
            var display=new Dictionary<string,object>{{"styleVersion",2},{"idleBrightness",0.4},{"pressedBrightness",1.0},{"scaleOnPress",false},{"pressScale",100},{"invertStates",false},{"tintMode","Multiply"},{"borderEnabled",false},{"borderStyle","Sprite outline"},{"borderOpacity",100},{"cornerRadius",8},{"shadowEnabled",false},{"shadowPressedOnly",false},{"shadowOpacity",65},{"shadowOffsetX",3},{"shadowOffsetY",3},{"shadowBlur",3},{"shadowSpread",0}};
            foreach(string prefix in new[]{"idle","pressed"}){display[prefix+"Color"]="#FFFFFFFF";display[prefix+"Opacity"]=100;display[prefix+"TintAmount"]=100;display[prefix+"BorderColor"]=prefix=="pressed"?"#FFFFFFFF":"#FF000000";display[prefix+"BorderWidth"]=prefix=="pressed"?2:1;display[prefix+"ShadowColor"]="#FF000000";}
            var options=new Dictionary<string,object>{{"player",0},{"controller",0},{"deadzone",0.3},{"height",170},{"width",400},{"smooth",true},{"inputSource","Auto"},{"backgroundMode","Solid"},{"backgroundColor","#FF000000"},{"backgroundOpacity",100},{"backgroundImage",""},{"backgroundImageName",""},{"backgroundImageMode","Fill"}};
            options["separateWindow"]=false;options["windowWidth"]=400;options["windowHeight"]=170;options["windowX"]=100;options["windowY"]=100;options["windowTopMost"]=false;options["windowTitleBar"]=true;options["windowTransparent"]=false;
            var elements=new Dictionary<string,object>();foreach(string name in Elements)elements[name]=new Dictionary<string,object>{{"scale",1.0},{"offsetX",0},{"offsetY",0},{"visible",name!="star"},{"customColors",false},{"idleColor","#FFFFFFFF"},{"pressedColor","#FFFFFFFF"}};
            return new Dictionary<string,object>{{"keybinds",keys},{"gamepadBinds",pads},{"displaySettings",display},{"liveSplit",options},{"elementSettings",elements}};
        }
        public static Dictionary<string,object> Map(Dictionary<string,object> d,string key){object value;return d.TryGetValue(key,out value)&&value is Dictionary<string,object>?(Dictionary<string,object>)value:new Dictionary<string,object>();}
        public static double Number(Dictionary<string,object> d,string key,double fallback,double min,double max){object v;double n;return d.TryGetValue(key,out v)&&double.TryParse(Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out n)&&!double.IsNaN(n)&&!double.IsInfinity(n)?Math.Max(min,Math.Min(max,n)):fallback;}
        public static bool Flag(Dictionary<string,object> d,string key,bool fallback){object v;return d.TryGetValue(key,out v)&&v is bool?(bool)v:fallback;}
        public static string Text(Dictionary<string,object> d,string key,string fallback){object v;return d.TryGetValue(key,out v)&&v is string?(string)v:fallback;}
        public Dictionary<string,object> Options {get{return Map(Config,"liveSplit");}}
        static void Merge(Dictionary<string,object> target,Dictionary<string,object> incoming){foreach(var kv in incoming){if(kv.Value is Dictionary<string,object> && target.ContainsKey(kv.Key) && target[kv.Key] is Dictionary<string,object>)Merge((Dictionary<string,object>)target[kv.Key],(Dictionary<string,object>)kv.Value);else target[kv.Key]=kv.Value;}}
        public void Load(string json){var incoming=Json.Deserialize<Dictionary<string,object>>(json);if(incoming==null)throw new FormatException("Expected a config object");var next=Defaults();Merge(next,incoming);Config=next;Options.Remove("autoCharacter");Options.Remove("autoPlane");Options.Remove("character");var original=Map(incoming,"liveSplit");if(!original.ContainsKey("backgroundMode")&&Flag(original,"greenScreen",false)){Options["backgroundMode"]="Solid";Options["backgroundColor"]="#FF00FF00";}Options.Remove("greenScreen");Options.Remove("backgroundColor2");Options["backgroundMode"]="Solid";Options["backgroundOpacity"]=100;var saved=Map(incoming,"displaySettings");var current=Map(Config,"displaySettings");if(!saved.ContainsKey("styleVersion")&&Number(saved,"idleBrightness",0.4,0,1)==1&&Number(saved,"pressedBrightness",1,0,1)==0.4){current["idleBrightness"]=0.4;current["pressedBrightness"]=1.0;}foreach(string key in new[]{"idleFillColor","idleFillOpacity","pressedFillColor","pressedFillOpacity"})current.Remove(key);current["styleVersion"]=2;if(Changed!=null)Changed();}
        public string Save(){return Json.Serialize(Config);}
        [DllImport("user32.dll")] static extern short GetAsyncKeyState(int key);


        [DllImport("xinput1_4.dll",EntryPoint="XInputGetState")] static extern uint XInputGetState(uint index,out PadState state);
        [StructLayout(LayoutKind.Sequential)] struct PadState {public uint packet;public ushort buttons;public byte lt,rt;public short lx,ly,rx,ry;}
        static readonly int[] PadMasks={0x1000,0x2000,0x4000,0x8000,0x100,0x200,0,0,0x20,0x10,0x40,0x80,1,2,4,8};
        static bool PadButton(PadState p,int index){if(index==6)return p.lt>30;if(index==7)return p.rt>30;return index>=0&&index<PadMasks.Length&&(p.buttons&PadMasks[index])!=0;}
        public static int VirtualKey(string code){if(code.StartsWith("Key")&&code.Length==4)return code[3];if(code.StartsWith("Digit")&&code.Length==6)return code[5];if(code.StartsWith("Numpad")&&code.Length==7&&char.IsDigit(code[6]))return 0x60+code[6]-'0';int f;if(code.StartsWith("F")&&int.TryParse(code.Substring(1),out f)&&f>=1&&f<=24)return 0x6f+f;
            switch(code){case "ArrowUp":return 38;case "ArrowDown":return 40;case "ArrowLeft":return 37;case "ArrowRight":return 39;case "Space":return 32;case "Tab":return 9;case "Enter":return 13;case "Escape":return 27;case "Backspace":return 8;case "ShiftLeft":return 160;case "ShiftRight":return 161;case "ControlLeft":return 162;case "ControlRight":return 163;case "AltLeft":return 164;case "AltRight":return 165;case "CapsLock":return 20;case "Insert":return 45;case "Delete":return 46;case "Home":return 36;case "End":return 35;case "PageUp":return 33;case "PageDown":return 34;case "Minus":return 189;case "Equal":return 187;case "BracketLeft":return 219;case "BracketRight":return 221;case "Backslash":return 220;case "Semicolon":return 186;case "Quote":return 222;case "Comma":return 188;case "Period":return 190;case "Slash":return 191;case "Backquote":return 192;case "NumpadAdd":return 107;case "NumpadSubtract":return 109;case "NumpadMultiply":return 106;case "NumpadDivide":return 111;case "NumpadDecimal":return 110;case "NumpadEnter":return 13;default:return 0;}}
        static bool Key(string code){int vk=VirtualKey(code);return vk>0&&(GetAsyncKeyState(vk)&0x8000)!=0;}
        public bool StarEnabled {get{return Flag(Map(Map(Config,"elementSettings"),"star"),"visible",false);}}
        public bool StarOnly {get{return StarEnabled&&Reader.OnWinScreen;}}
        public bool ElementVisible(int i){return StarOnly?i==7:i!=7&&Flag(Map(Map(Config,"elementSettings"),Elements[i]),"visible",true);}
        public static bool StarInput(bool z,bool controllerA){return z||controllerA;}
        void Tick(){if(disposed)return;bool hardwareNeeded=StarOnly||!Reader.HasInputs||(settings!=null&&settings.NeedsFallbackInput);
            var keys=Map(Config,"keybinds");var pads=Map(Config,"gamepadBinds"); PadState pad=new PadState();bool connected=false;string source=Text(Options,"inputSource","Auto");int controller=(int)Number(Options,"controller",0,0,3);
            if(hardwareNeeded&&source!="DirectInput")try{connected=XInputGetState((uint)controller,out pad)==0;}catch(DllNotFoundException){}
            double deadzone=Number(Options,"deadzone",0.3,0.05,0.95);
            bool direct=hardwareNeeded&&!connected&&source!="XInput"&&genericPad.Poll(controller);Array.Clear(PadButtons,0,PadButtons.Length);PadSource=!hardwareNeeded?"In-game input":connected?"XInput":direct?"DirectInput":"No controller";
            if(connected)for(int i=0;i<17;i++)PadButtons[i]=PadButton(pad,i);else if(direct)Array.Copy(genericPad.Buttons,PadButtons,Math.Min(genericPad.Buttons.Length,PadButtons.Length));
            for(int i=0;i<Actions.Length;i++){bool p=hardwareNeeded&&Key(Text(keys,Actions[i],""));if(i<6)p|=PadButtons[(int)Number(pads,Actions[i],0,0,127)];else if(connected){if(i==6)p|=PadButton(pad,12)||pad.ly/32768.0>deadzone;else if(i==7)p|=PadButton(pad,13)||pad.ly/32768.0< -deadzone;else if(i==8)p|=PadButton(pad,14)||pad.lx/32768.0< -deadzone;else p|=PadButton(pad,15)||pad.lx/32768.0>deadzone;}else if(direct){if(i==6)p|=genericPad.Up||genericPad.Y< -deadzone;else if(i==7)p|=genericPad.Down||genericPad.Y>deadzone;else if(i==8)p|=genericPad.Left||genericPad.X< -deadzone;else p|=genericPad.Right||genericPad.X>deadzone;}Pressed[i]=p;}
            long now=Environment.TickCount&0x7fffffff;if(now-lastGamePoll>=100||now<lastGamePoll){lastGamePoll=now;string character;bool plane;if(Reader.Poll((int)Number(Options,"player",0,0,1),out character,out plane)){Character=character;Plane=plane;}}
            Reader.TryInputs(Pressed);
            Pressed[10]=StarOnly&&StarInput(Key("KeyZ"),PadButtons[0]);
            if(StarOnly){if(star.Advance(Pressed[10],System.Diagnostics.Stopwatch.GetTimestamp()*1000/System.Diagnostics.Stopwatch.Frequency)){dirty=true;windowDirty=true;}}else star.Reset();
            if(Character!=lastCharacter||Plane!=lastPlane||Reader.OnWinScreen!=lastWin){dirty=true;windowDirty=true;lastCharacter=Character;lastPlane=Plane;lastWin=Reader.OnWinScreen;}for(int i=0;i<Pressed.Length;i++)if(Pressed[i]!=lastPressed[i]){dirty=true;windowDirty=true;lastPressed[i]=Pressed[i];}
            ObserveRenderConfig();SyncDisplayWindow();if(displayWindow!=null&&windowDirty){displayWindow.RefreshDisplay();windowDirty=false;}
        }
        public void SyncDisplayWindow(){if(disposed)return;if(!Detached){if(displayWindow!=null){var previous=displayWindow;displayWindow=null;previous.ClosedByUser=null;previous.Dispose();}windowSignature=null;return;}
            if(displayWindow==null||displayWindow.IsDisposed){displayWindow=new DisplayWindow(this);windowDirty=true;displayWindow.ClosedByUser=()=>{Options["separateWindow"]=false;dirty=true;windowSignature=null;if(Changed!=null)Changed();};windowSignature=null;}
            string signature=Number(Options,"windowWidth",400,120,2000)+"|"+Number(Options,"windowHeight",170,80,1200)+"|"+Number(Options,"windowX",100,-30000,30000)+"|"+Number(Options,"windowY",100,-30000,30000)+"|"+Flag(Options,"windowTopMost",false)+"|"+Flag(Options,"windowTitleBar",true)+"|"+Flag(Options,"windowTransparent",false);if(signature!=windowSignature){displayWindow.ApplySettings();windowSignature=signature;windowDirty=true;displayWindow.RefreshDisplay();}if(!displayWindow.Visible)displayWindow.Show();
        }
        Bitmap Image(string relative){Bitmap bitmap;if(images.TryGetValue(relative,out bitmap))return bitmap;using(var stream=AssetStore.Open(relative)){if(stream==null)return null;using(var original=new Bitmap(stream)){bool starIdle=relative.StartsWith("assets/Display/Star/Main/");bitmap=new Bitmap(starIdle?50:original.Width,starIdle?50:original.Height,PixelFormat.Format32bppPArgb);using(var g=Graphics.FromImage(bitmap))g.DrawImageUnscaled(original,(bitmap.Width-original.Width)/2,(bitmap.Height-original.Height)/2);}}images[relative]=bitmap;return bitmap;}
        public RectangleF ElementBounds(int i){var s=Map(Map(Config,"elementSettings"),Elements[i]);float scale=(float)Number(s,"scale",1,0.3,2.5);float w=i==6?110:i==7?50:66;return new RectangleF(Positions[i].X+(float)Number(s,"offsetX",0,-10000,10000)+(w-w*scale)/2,Positions[i].Y+(float)Number(s,"offsetY",0,-10000,10000)+(w-w*scale)/2,w*scale,w*scale);}
        public RectangleF Bounds(){if(StarOnly)return RectangleF.Union(new RectangleF(0,0,380,146),ElementBounds(7));RectangleF bounds=RectangleF.Empty;var d=Map(Config,"displaySettings");float extra=SpriteEffects.Padding(d);float pressScale=Flag(d,"scaleOnPress",false)?(float)Math.Max(1,Number(d,"pressScale",100,20,150)/100):1;for(int i=0;i<Elements.Length;i++)if(ElementVisible(i)){var r=ElementBounds(i);float pad=extra*r.Width/(i==6?110:66)*pressScale;r.Inflate(r.Width*(pressScale-1)/2+pad,r.Height*(pressScale-1)/2+pad);bounds=bounds.IsEmpty?r:RectangleF.Union(bounds,r);}return bounds.IsEmpty?new RectangleF(0,0,380,146):bounds;}
        void Sprite(Graphics g,string path,RectangleF rect,bool pressed,Dictionary<string,object> element){var image=Image(path);if(image!=null)effects.Draw(g,path,image,rect,pressed,Map(Config,"displaySettings"),element,Flag(Options,"smooth",true));}
        Bitmap backgroundImage,backgroundLayer;string backgroundImageData,backgroundLayerKey,backgroundLayerImage;
        string RenderSignature(){var snapshot=new Dictionary<string,object>(Config);var options=new Dictionary<string,object>(Options);string image=Text(options,"backgroundImage","");if(!object.ReferenceEquals(image,lastImageSignature)){lastImageSignature=image;dirty=true;}options.Remove("backgroundImage");snapshot["liveSplit"]=options;return Json.Serialize(snapshot);}
        void Background(Graphics g,float width,float height){if(width<=0||height<=0)return;string image=Text(Options,"backgroundImage","");string key=width+"|"+height+"|"+Text(Options,"backgroundColor","#FF000000")+"|"+Text(Options,"backgroundImageMode","Fill")+"|"+Flag(Options,"smooth",true);if(backgroundLayer==null||backgroundLayerKey!=key||!object.ReferenceEquals(backgroundLayerImage,image)){if(backgroundLayer!=null)backgroundLayer.Dispose();backgroundLayer=new Bitmap(Math.Max(1,(int)Math.Ceiling(width)),Math.Max(1,(int)Math.Ceiling(height)),PixelFormat.Format32bppPArgb);using(var target=Graphics.FromImage(backgroundLayer))DrawBackgroundSurface(target,width,height);backgroundLayerKey=key;backgroundLayerImage=image;}var saved=g.Save();try{g.SetClip(new RectangleF(0,0,width,height),CombineMode.Intersect);g.DrawImageUnscaled(backgroundLayer,0,0);}finally{g.Restore(saved);}}
        void DrawBackgroundSurface(Graphics g,float width,float height){
            if(width<=0||height<=0)return;Color color=SpriteEffects.ReadColor(Options,"backgroundColor",Color.Black);using(var brush=new SolidBrush(Color.FromArgb(255,color)))g.FillRectangle(brush,0,0,width,height);
            string data=Text(Options,"backgroundImage","");if(!object.ReferenceEquals(data,backgroundImageData)){backgroundImageData=data;if(backgroundImage!=null)backgroundImage.Dispose();backgroundImage=null;if(data.Length>0)try{backgroundImage=BackgroundImages.Decode(data);}catch(Exception ex){System.Diagnostics.Trace.WriteLine("Cuphead background image: "+ex.Message);}}
            if(backgroundImage==null)return;var saved=g.Save();try{g.SetClip(new RectangleF(0,0,width,height),CombineMode.Intersect);string mode=Text(Options,"backgroundImageMode","Fill");
                if(mode=="Tile"){using(var brush=new TextureBrush(backgroundImage,WrapMode.Tile))g.FillRectangle(brush,0,0,width,height);return;}
                float drawWidth=width,drawHeight=height;if(mode=="Center"){drawWidth=backgroundImage.Width;drawHeight=backgroundImage.Height;}else if(mode!="Stretch"){float scale=mode=="Fit"?Math.Min(width/backgroundImage.Width,height/backgroundImage.Height):Math.Max(width/backgroundImage.Width,height/backgroundImage.Height);drawWidth=backgroundImage.Width*scale;drawHeight=backgroundImage.Height*scale;}
                g.InterpolationMode=Flag(Options,"smooth",true)?InterpolationMode.HighQualityBicubic:InterpolationMode.NearestNeighbor;using(var attributes=new ImageAttributes()){attributes.SetWrapMode(WrapMode.TileFlipXY);g.DrawImage(backgroundImage,new[]{new PointF((width-drawWidth)/2,(height-drawHeight)/2),new PointF((width+drawWidth)/2,(height-drawHeight)/2),new PointF((width-drawWidth)/2,(height+drawHeight)/2)},new RectangleF(0,0,backgroundImage.Width,backgroundImage.Height),GraphicsUnit.Pixel,attributes);}
            }finally{g.Restore(saved);}
        }
        public void Render(Graphics g,float width,float height){Render(g,width,height,Bounds());}
        public void Render(Graphics g,float width,float height,RectangleF b){Render(g,width,height,b,Pressed);}
        public void Render(Graphics g,float width,float height,RectangleF b,bool[] inputs){Render(g,width,height,b,inputs,true);}
        public void Render(Graphics g,float width,float height,RectangleF b,bool[] inputs,bool drawBackground){var saved=g.Save();try{if(drawBackground)Background(g,width,height);effects.Configure(Json.Serialize(Map(Config,"displaySettings")));float fit=Math.Min((width-12)/b.Width,(height-12)/b.Height);if(fit<=0)return;g.TranslateTransform((width-b.Width*fit)/2-b.X*fit,(height-b.Height*fit)/2-b.Y*fit);g.ScaleTransform(fit,fit);g.InterpolationMode=Flag(Options,"smooth",true)?InterpolationMode.HighQualityBicubic:InterpolationMode.NearestNeighbor;g.PixelOffsetMode=PixelOffsetMode.HighQuality;var d=Map(Config,"displaySettings");
            for(int i=0;i<Elements.Length;i++){var s=Map(Map(Config,"elementSettings"),Elements[i]);if(!ElementVisible(i))continue;var rect=ElementBounds(i);string el=Elements[i];int action=Array.IndexOf(Actions,el);bool pressed=i==6?(inputs[6]||inputs[7]||inputs[8]||inputs[9]):(el=="star"?inputs.Length>10&&inputs[10]:inputs[action]);if(el!="star"&&(pressed^Flag(d,"invertStates",false))&&Flag(d,"scaleOnPress",false)){float scale=(float)Number(d,"pressScale",100,20,150)/100;rect=new RectangleF(rect.X+rect.Width*(1-scale)/2,rect.Y+rect.Height*(1-scale)/2,rect.Width*scale,rect.Height*scale);}
                if(i==6){string[] dirs={"up","down","left","right"};PointF[] points={new PointF(32,0),new PointF(32,64),new PointF(0,32),new PointF(64,32)};for(int k=0;k<4;k++)Sprite(g,"assets/Display/Misc/Arrow_"+dirs[k]+".png",new RectangleF(rect.X+points[k].X*rect.Width/110,rect.Y+points[k].Y*rect.Height/110,46*rect.Width/110,46*rect.Height/110),inputs[6+k],s);}
                else if(el=="star"){var image=Image(star.Path);if(image!=null)g.DrawImage(image,rect);}
                else {string name=el=="ex"?"EX":char.ToUpper(el[0])+el.Substring(1);string path=i<2?"assets/Display/Misc/"+name+".png":"assets/Display/"+(Plane?"Plane":"Normal")+"/"+Character+"/"+name+".png";Sprite(g,path,rect,pressed,s);}
            }
        }finally{g.Restore(saved);}}
        public string ComponentName{get{return "Cuphead Input Display";}}
        public IDictionary<string,Action> ContextMenuControls{get;private set;}
        public float VerticalHeight{get{return Detached?0:(float)Number(Options,"height",170,50,1000);}}
        public float HorizontalWidth{get{return Detached?0:(float)Number(Options,"width",400,80,2000);}}
        public float MinimumWidth{get{return Detached?0:80;}}public float MinimumHeight{get{return Detached?0:50;}}
        public float PaddingTop{get{return 0;}}public float PaddingBottom{get{return 0;}}public float PaddingLeft{get{return 0;}}public float PaddingRight{get{return 0;}}
        public void DrawVertical(Graphics g,LiveSplitState s,float width,Region clip){if(!Detached)Render(g,width,VerticalHeight);}
        public void DrawHorizontal(Graphics g,LiveSplitState s,float height,Region clip){if(!Detached)Render(g,HorizontalWidth,height);}
        void ObserveRenderConfig(){long now=Environment.TickCount&0x7fffffff;if(now-lastConfigCheck>=100||now<lastConfigCheck){lastConfigCheck=now;string current=RenderSignature();if(current!=lastConfig){lastConfig=current;dirty=true;windowDirty=true;}}}
        public void Update(IInvalidator invalidator,LiveSplitState s,float width,float height,LayoutMode mode){ObserveRenderConfig();if(width!=lastWidth||height!=lastHeight||mode!=lastMode){dirty=true;lastWidth=width;lastHeight=height;lastMode=mode;}if(dirty&&invalidator!=null){invalidator.Invalidate(0,0,width,height);dirty=false;}}
        public Control GetSettingsControl(LayoutMode mode){if(settings==null||settings.IsDisposed)settings=new Editor(this);return settings;}
        public XmlNode GetSettings(XmlDocument document){var root=document.CreateElement("Settings");var c=document.CreateElement("Config");c.InnerText=Save();root.AppendChild(c);return root;}
        public void SetSettings(XmlNode node){var c=node.SelectSingleNode("Config");if(c!=null)try{Load(c.InnerText);}catch(Exception ex){System.Diagnostics.Trace.WriteLine("Cuphead input settings: "+ex.Message);}}
        public void Dispose(){if(disposed)return;disposed=true;timer.Stop();timer.Dispose();if(displayWindow!=null){displayWindow.ClosedByUser=null;displayWindow.Dispose();displayWindow=null;}if(settings!=null)settings.Dispose();Reader.Dispose();genericPad.Dispose();effects.Dispose();if(backgroundImage!=null)backgroundImage.Dispose();if(backgroundLayer!=null)backgroundLayer.Dispose();foreach(var image in images.Values)image.Dispose();images.Clear();}
    }
}
