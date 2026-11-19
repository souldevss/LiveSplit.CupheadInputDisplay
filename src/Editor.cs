using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CupheadInputDisplay {
    public sealed class Editor : UserControl {
        readonly Component owner;
        readonly TabControl tabs=new TabControl {Dock=DockStyle.Fill,Multiline=true};
        readonly Label status=new Label {Dock=DockStyle.Bottom,Height=42,AutoEllipsis=true,Padding=new Padding(5)};
        readonly System.Windows.Forms.Timer ticker=new System.Windows.Forms.Timer {Interval=33};
        readonly List<Action> refreshers=new List<Action>();
        readonly Dictionary<string,Button> keyButtons=new Dictionary<string,Button>();
        readonly Dictionary<string,Button> padButtons=new Dictionary<string,Button>();
        readonly ToolTip tips=new ToolTip();
        Preview preview;
        readonly List<Preview> stylePreviews=new List<Preview>();
        ComboBox element;
        NumericUpDown elementSize,elementX,elementY;
        CheckBox elementVisible;
        CheckBox elementCustom;Button elementIdleColor,elementPressedColor;
        bool refreshing,padReleased,disposed;
        string waitingKey,waitingPad;
        public bool NeedsFallbackInput {get{return Visible&&tabs.SelectedTab!=null&&tabs.SelectedTab.Text=="Fallback input";}}
        public Editor(Component owner){
            this.owner=owner;Name="CupheadInputSettings";Size=new Size(480,560);MinimumSize=new Size(340,390);Font=SystemFonts.MessageBoxFont;
            Controls.Add(tabs);Controls.Add(status);
            BuildGeneral();BuildAppearance();BuildEffects();BuildLayout();BuildBindings();
            tabs.SelectedIndexChanged+=(s,e)=>CancelCapture();
            ticker.Tick+=(s,e)=>RefreshRuntime();ticker.Start();owner.Changed+=RefreshSettings;
            RefreshSettings();
        }
        TableLayoutPanel Page(string text){var page=new TabPage(text){UseVisualStyleBackColor=true,AutoScroll=true};tabs.TabPages.Add(page);return PageTable(page);}
        TableLayoutPanel PageTable(TabPage page){
            var table=new TableLayoutPanel{Dock=DockStyle.Top,AutoSize=true,ColumnCount=2,Padding=new Padding(10)};
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));page.Controls.Add(table);return table;
        }
        void Row(TableLayoutPanel table,string label,Control control){int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));var caption=new Label{Text=label,AutoSize=true,Anchor=AnchorStyles.Left,Margin=new Padding(3,8,6,8)};control.Anchor=AnchorStyles.Left|AnchorStyles.Right;control.Margin=new Padding(3,5,3,5);table.Controls.Add(caption,0,row);table.Controls.Add(control,1,row);}
        void Wide(TableLayoutPanel table,Control control){int row=table.RowCount++;table.RowStyles.Add(new RowStyle(SizeType.AutoSize));control.Margin=new Padding(3,6,3,8);control.Anchor=AnchorStyles.Left|AnchorStyles.Right;table.Controls.Add(control,0,row);table.SetColumnSpan(control,2);}
        void Note(TableLayoutPanel table,string text){Wide(table,new Label{Text=text,AutoSize=true,MaximumSize=new Size(420,0),ForeColor=SystemColors.GrayText});}
        void Heading(TableLayoutPanel table,string text){Wide(table,new Label{Text=text,AutoSize=true,Font=new Font(Font,FontStyle.Bold),Margin=new Padding(3,12,3,4)});}
        NumericUpDown Numeric(decimal min,decimal max,int decimals){return new NumericUpDown {Minimum=min,Maximum=max,DecimalPlaces=decimals,Increment=decimals==0?1:0.05m,Width=100};}
        NumericUpDown Number(TableLayoutPanel table,string label,string group,string key,decimal fallback,decimal min,decimal max,int decimals,decimal multiplier){
            var field=Numeric(min,max,decimals);field.Name=key;Row(table,label,field);
            refreshers.Add(()=>field.Value=(decimal)Component.Number(Component.Map(owner.Config,group),key,(double)(fallback/multiplier),(double)(min/multiplier),(double)(max/multiplier))*multiplier);
            field.ValueChanged+=(s,e)=>{if(!refreshing){Component.Map(owner.Config,group)[key]=(double)(field.Value/multiplier);if(preview!=null)preview.Invalidate();}};return field;
        }
        CheckBox Check(TableLayoutPanel table,string label,string group,string key,bool fallback){
            var check=new CheckBox{Text=label,AutoSize=true,Name=key};Wide(table,check);
            refreshers.Add(()=>check.Checked=Component.Flag(Component.Map(owner.Config,group),key,fallback));
            check.CheckedChanged+=(s,e)=>{if(!refreshing){Component.Map(owner.Config,group)[key]=check.Checked;if(preview!=null)preview.Invalidate();}};return check;
        }
        ComboBox Choice(TableLayoutPanel table,string label,string key,string[] choices,bool numeric,string group="liveSplit"){
            var combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name=key};combo.Items.AddRange(choices);Row(table,label,combo);
            refreshers.Add(()=>{var options=Component.Map(owner.Config,group);if(numeric)combo.SelectedIndex=(int)Component.Number(options,key,0,0,choices.Length-1);else {combo.SelectedItem=Component.Text(options,key,choices[0]);if(combo.SelectedIndex<0)combo.SelectedIndex=0;}});
            combo.SelectedIndexChanged+=(s,e)=>{if(!refreshing){Component.Map(owner.Config,group)[key]=numeric?(object)combo.SelectedIndex:combo.SelectedItem.ToString();}};return combo;
        }
        void ColorButtonStyle(Button button,Color color){button.BackColor=Color.FromArgb(255,color);button.ForeColor=color.GetBrightness()<0.5?Color.White:Color.Black;button.Text="#"+color.R.ToString("X2")+color.G.ToString("X2")+color.B.ToString("X2");}
        Button ColorField(TableLayoutPanel table,string label,string key,Color fallback,Func<Dictionary<string,object>> settings,string controlName=null){
            var button=new Button{AutoSize=true,Name=controlName??key,UseVisualStyleBackColor=false,Dock=DockStyle.Fill};var picker=new Button{Text="Choose…",AutoSize=true,Name=(controlName??key)+"_picker",Dock=DockStyle.Fill};
            var selection=new TableLayoutPanel{AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,ColumnCount=2,Dock=DockStyle.Fill,Margin=Padding.Empty};selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));selection.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));selection.Controls.Add(button,0,0);selection.Controls.Add(picker,1,0);Row(table,label,selection);
            refreshers.Add(()=>ColorButtonStyle(button,SpriteEffects.ReadColor(settings(),key,fallback)));
            Action choose=()=>{using(var dialog=new ColorDialog{FullOpen=true,Color=SpriteEffects.ReadColor(settings(),key,fallback)})if(dialog.ShowDialog(this)==DialogResult.OK){settings()[key]="#"+unchecked((uint)dialog.Color.ToArgb()).ToString("X8");ColorButtonStyle(button,dialog.Color);}};
            button.Click+=(s,e)=>choose();picker.Click+=(s,e)=>choose();button.EnabledChanged+=(s,e)=>picker.Enabled=button.Enabled;tips.SetToolTip(button,"Click the swatch or Choose to open the color picker.");return button;
        }
        void StylePreview(TableLayoutPanel table,string name){var picture=new Preview(owner){Height=130,Name="StylePreview_"+name,Editable=false};stylePreviews.Add(picture);Wide(table,picture);var mode=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="PreviewMode_"+name};mode.Items.AddRange(new[]{"Live input","Inactive inputs","Pressed inputs"});Row(table,"Preview state",mode);mode.SelectedIndexChanged+=(s,e)=>{picture.InputPreviewMode=mode.SelectedIndex;picture.Invalidate();};mode.SelectedIndex=0;}
        void BuildGeneral(){var page=Page("General");
            Heading(page,"Game detection");Note(page,"Character, plane mode and in-game inputs are detected automatically. No separate character or plane configuration is needed.");
            Choice(page,"Follow player","player",new[]{"Player 1","Player 2"},true);
            Heading(page,"Display location");var detached=Check(page,"Show display in a separate window","liveSplit","separateWindow",false);Note(page,"Separate-window mode uses no space in the splits layout. Closing that window returns the display to the layout.");
            var windowOptions=new TableLayoutPanel{ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Name="WindowOptions"};windowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));windowOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));Wide(page,windowOptions);
            Number(windowOptions,"Window width","liveSplit","windowWidth",400,120,2000,0,1);Number(windowOptions,"Window height","liveSplit","windowHeight",170,80,1200,0,1);Check(windowOptions,"Always on top","liveSplit","windowTopMost",false);var titleBar=Check(windowOptions,"Show title bar","liveSplit","windowTitleBar",true);var transparent=Check(windowOptions,"Transparent window background","liveSplit","windowTransparent",false);transparent.CheckedChanged+=(s,e)=>titleBar.Enabled=!transparent.Checked;refreshers.Add(()=>titleBar.Enabled=!transparent.Checked);Note(windowOptions,"Transparent mode hides the title bar and background image. Drag an input sprite to move the window.");
            detached.CheckedChanged+=(s,e)=>{windowOptions.Visible=detached.Checked;if(!refreshing)owner.SyncDisplayWindow();};refreshers.Add(()=>windowOptions.Visible=detached.Checked);
            Heading(page,"Background");
            ColorField(page,"Background color","backgroundColor",Color.Black,()=>owner.Options);

            var green=new Button{Text="Use green screen",AutoSize=true,Name="GreenScreenPreset"};Wide(page,green);green.Click+=(s,e)=>{owner.Options["backgroundMode"]="Solid";owner.Options["backgroundColor"]="#FF00FF00";owner.Options["backgroundOpacity"]=100;RefreshSettings();};
            Heading(page,"Background image");var imageName=new Label{AutoSize=false,Height=22,AutoEllipsis=true,Name="BackgroundImageName"};Wide(page,imageName);refreshers.Add(()=>imageName.Text=Component.Text(owner.Options,"backgroundImage","").Length==0?"No background image":Component.Text(owner.Options,"backgroundImageName","Embedded image"));
            var browse=new Button{Text="Choose image…",AutoSize=true,Name="ChooseBackgroundImage"};var remove=new Button{Text="Remove image",AutoSize=true,Name="RemoveBackgroundImage"};var buttons=new FlowLayoutPanel{AutoSize=true,FlowDirection=FlowDirection.LeftToRight};buttons.Controls.Add(browse);buttons.Controls.Add(remove);Wide(page,buttons);
            browse.Click+=(s,e)=>{using(var dialog=new OpenFileDialog{Filter="Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif",Title="Choose background image"})if(dialog.ShowDialog(this)==DialogResult.OK)try{using(var stream=File.OpenRead(dialog.FileName))owner.Options["backgroundImage"]=BackgroundImages.Encode(stream);owner.Options["backgroundImageName"]=Path.GetFileName(dialog.FileName);RefreshSettings();}catch(Exception ex){MessageBox.Show(this,"Could not load image: "+ex.Message,"Cuphead Input Display",MessageBoxButtons.OK,MessageBoxIcon.Warning);}};
            remove.Click+=(s,e)=>{owner.Options["backgroundImage"]="";owner.Options["backgroundImageName"]="";RefreshSettings();};refreshers.Add(()=>remove.Enabled=Component.Text(owner.Options,"backgroundImage","").Length>0);
            Choice(page,"Image sizing","backgroundImageMode",new[]{"Fill","Fit","Stretch","Center","Tile"},false);
            Heading(page,"Component size");Number(page,"Height (vertical layout)","liveSplit","height",170,50,1000,0,1);Number(page,"Width (horizontal layout)","liveSplit","width",400,80,2000,0,1);
        }
        void BuildAppearance(){var page=Page("Appearance");
            StylePreview(page,"Appearance");Check(page,"Invert pressed / inactive appearance","displaySettings","invertStates",false);
            Choice(page,"Color mode","tintMode",new[]{"Original","Multiply","Colorize","Solid"},false,"displaySettings");
            Heading(page,"Inactive inputs");Number(page,"Brightness (%)","displaySettings","idleBrightness",40,0,100,0,100);Number(page,"Opacity (%)","displaySettings","idleOpacity",100,0,100,0,1);ColorField(page,"Input color","idleColor",Color.White,()=>Component.Map(owner.Config,"displaySettings"));Number(page,"Color strength (%)","displaySettings","idleTintAmount",100,0,100,0,1);
            Heading(page,"Pressed inputs");Number(page,"Brightness (%)","displaySettings","pressedBrightness",100,0,100,0,100);Number(page,"Opacity (%)","displaySettings","pressedOpacity",100,0,100,0,1);ColorField(page,"Input color","pressedColor",Color.White,()=>Component.Map(owner.Config,"displaySettings"));Number(page,"Color strength (%)","displaySettings","pressedTintAmount",100,0,100,0,1);
            Heading(page,"Sizing");
            var enabled=Check(page,"Scale artwork when pressed","displaySettings","scaleOnPress",false);
            var scale=Number(page,"Pressed size (%)","displaySettings","pressScale",100,20,150,0,1);
            enabled.CheckedChanged+=(s,e)=>scale.Enabled=enabled.Checked;refreshers.Add(()=>scale.Enabled=enabled.Checked);
            Check(page,"Smooth image scaling","liveSplit","smooth",true);
            Note(page,"Turn smoothing off for sharp pixel scaling. Each element's base size and visibility are set on the Layout tab.");
        }
        void BuildEffects(){
            var outer=new TabPage("Effects"){UseVisualStyleBackColor=true};tabs.TabPages.Add(outer);var root=PageTable(outer);root.Dock=DockStyle.Fill;root.AutoSize=false;StylePreview(root,"Effects");
            var sections=new TabControl{Dock=DockStyle.Fill,Name="EffectSections",Height=300};Wide(root,sections);root.RowStyles[root.RowCount-1].SizeType=SizeType.Percent;root.RowStyles[root.RowCount-1].Height=100;
            var borders=new TabPage("Borders"){UseVisualStyleBackColor=true,AutoScroll=true};sections.TabPages.Add(borders);var page=PageTable(borders);var borderEnabled=Check(page,"Enable input borders","displaySettings","borderEnabled",false);var borderDetails=new TableLayoutPanel{ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Name="BorderDetails"};borderDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));borderDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));Wide(page,borderDetails);borderEnabled.CheckedChanged+=(s,e)=>borderDetails.Visible=borderEnabled.Checked;refreshers.Add(()=>borderDetails.Visible=borderEnabled.Checked);
            Choice(borderDetails,"Shape","borderStyle",new[]{"Sprite outline","Rounded rectangle","Rectangle","None"},false,"displaySettings");ColorField(borderDetails,"Inactive color","idleBorderColor",Color.Black,()=>Component.Map(owner.Config,"displaySettings"));Number(borderDetails,"Inactive thickness","displaySettings","idleBorderWidth",1,0,12,1,1);ColorField(borderDetails,"Pressed color","pressedBorderColor",Color.White,()=>Component.Map(owner.Config,"displaySettings"));Number(borderDetails,"Pressed thickness","displaySettings","pressedBorderWidth",2,0,12,1,1);Number(borderDetails,"Opacity (%)","displaySettings","borderOpacity",100,0,100,0,1);Number(borderDetails,"Corner radius","displaySettings","cornerRadius",8,0,30,1,1);
            var shadows=new TabPage("Shadow"){UseVisualStyleBackColor=true,AutoScroll=true};sections.TabPages.Add(shadows);page=PageTable(shadows);var shadowEnabled=Check(page,"Enable drop shadow","displaySettings","shadowEnabled",false);var shadowDetails=new TableLayoutPanel{ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Name="ShadowDetails"};shadowDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));shadowDetails.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));Wide(page,shadowDetails);shadowEnabled.CheckedChanged+=(s,e)=>shadowDetails.Visible=shadowEnabled.Checked;refreshers.Add(()=>shadowDetails.Visible=shadowEnabled.Checked);
            ColorField(shadowDetails,"Inactive color","idleShadowColor",Color.Black,()=>Component.Map(owner.Config,"displaySettings"));ColorField(shadowDetails,"Pressed color","pressedShadowColor",Color.Black,()=>Component.Map(owner.Config,"displaySettings"));Number(shadowDetails,"Opacity (%)","displaySettings","shadowOpacity",65,0,100,0,1);Check(shadowDetails,"Pressed style only","displaySettings","shadowPressedOnly",false);
            var advanced=new TableLayoutPanel{ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Name="ShadowAdvanced",Visible=false};advanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,58));advanced.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,42));var more=new Button{Text="More shadow options…",AutoSize=true,Name="ToggleShadowAdvanced"};Wide(shadowDetails,more);Wide(shadowDetails,advanced);more.Click+=(s,e)=>{advanced.Visible=!advanced.Visible;more.Text=advanced.Visible?"Fewer shadow options":"More shadow options…";};
            Number(advanced,"Horizontal offset","displaySettings","shadowOffsetX",3,-30,30,1,1);Number(advanced,"Vertical offset","displaySettings","shadowOffsetY",3,-30,30,1,1);Number(advanced,"Blur","displaySettings","shadowBlur",3,0,12,1,1);Number(advanced,"Spread","displaySettings","shadowSpread",0,0,8,1,1);
        }
        void BuildLayout(){var page=Page("Layout");
            Note(page,"Drag an element in the preview, or set its offsets below. The preview shows the character and mode detected in the game.");
            preview=new Preview(owner){Height=190,Name="ArtworkPreview"};Wide(page,preview);
            element=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList,Name="SelectedElement"};element.Items.AddRange(new[]{"Lock","Swap weapon","Jump","Shoot","Dash","EX / Super","Directional pad"});Row(page,"Element",element);
            elementVisible=new CheckBox{Text="Visible",AutoSize=true,Name="ElementVisible"};Wide(page,elementVisible);
            elementSize=Numeric(30,250,0);elementSize.Name="ElementScale";Row(page,"Base size (%)",elementSize);
            elementX=Numeric(-10000,10000,0);elementX.Name="ElementX";Row(page,"Horizontal offset",elementX);
            elementY=Numeric(-10000,10000,0);elementY.Name="ElementY";Row(page,"Vertical offset",elementY);
            var reset=new Button{Text="Reset position",AutoSize=true,Name="ResetPosition"};Wide(page,reset);
            element.SelectedIndexChanged+=(s,e)=>{preview.Selected=element.SelectedIndex;RefreshElement();};
            elementVisible.CheckedChanged+=(s,e)=>{if(!refreshing&&element.SelectedIndex>=0){ElementConfig()["visible"]=elementVisible.Checked;preview.Invalidate();}};
            elementSize.ValueChanged+=(s,e)=>{if(!refreshing&&element.SelectedIndex>=0){ElementConfig()["scale"]=(double)elementSize.Value/100;preview.Invalidate();}};
            elementX.ValueChanged+=(s,e)=>{if(!refreshing&&element.SelectedIndex>=0){ElementConfig()["offsetX"]=(double)elementX.Value;preview.Invalidate();}};
            elementY.ValueChanged+=(s,e)=>{if(!refreshing&&element.SelectedIndex>=0){ElementConfig()["offsetY"]=(double)elementY.Value;preview.Invalidate();}};
            reset.Click+=(s,e)=>{ElementConfig()["offsetX"]=0;ElementConfig()["offsetY"]=0;RefreshElement();preview.Invalidate();};
            elementCustom=new CheckBox{Text="Custom colors for this element",AutoSize=true,Name="ElementCustomColors"};Wide(page,elementCustom);elementIdleColor=ColorField(page,"Inactive input color","idleColor",Color.White,()=>ElementConfig(),"ElementIdleColor");elementPressedColor=ColorField(page,"Pressed input color","pressedColor",Color.White,()=>ElementConfig(),"ElementPressedColor");
            elementCustom.CheckedChanged+=(s,e)=>{if(!refreshing)ElementConfig()["customColors"]=elementCustom.Checked;elementIdleColor.Enabled=elementCustom.Checked;elementPressedColor.Enabled=elementCustom.Checked;};
            preview.SelectionChanged=index=>{element.SelectedIndex=index;RefreshElement();};
            preview.PositionChanged=RefreshElement;element.SelectedIndex=0;
        }
        Dictionary<string,object> ElementConfig(){return Component.Map(Component.Map(owner.Config,"elementSettings"),Component.Elements[Math.Max(0,element.SelectedIndex)]);}
        void RefreshElement(){if(element==null||element.SelectedIndex<0)return;bool previous=refreshing;refreshing=true;var c=ElementConfig();elementVisible.Checked=Component.Flag(c,"visible",true);elementSize.Value=(decimal)Component.Number(c,"scale",1,0.3,2.5)*100;elementX.Value=(decimal)Component.Number(c,"offsetX",0,-10000,10000);elementY.Value=(decimal)Component.Number(c,"offsetY",0,-10000,10000);if(elementCustom!=null){elementCustom.Checked=Component.Flag(c,"customColors",false);elementIdleColor.Enabled=elementPressedColor.Enabled=elementCustom.Checked;ColorButtonStyle(elementIdleColor,SpriteEffects.ReadColor(c,"idleColor",Color.White));ColorButtonStyle(elementPressedColor,SpriteEffects.ReadColor(c,"pressedColor",Color.White));}refreshing=previous;}
        static string LabelFor(string action){switch(action){case "ex":return "EX / Super";case "swap":return "Swap weapon";default:return char.ToUpper(action[0])+action.Substring(1);}}
        void BuildBindings(){var page=Page("Fallback input");
            Note(page,"In-game input is read automatically. These bindings are only used when game input cannot be read, such as outside an active level. They do not change Cuphead's controls.");
            Heading(page,"Keyboard fallback");foreach(string action in Component.Actions){string captured=action;var button=new Button{AutoSize=true,Name="Key_"+action};keyButtons[action]=button;Row(page,LabelFor(action),button);button.Click+=(s,e)=>BeginKey(captured,button);WireKeyCapture(button);}
            Heading(page,"Controller fallback");Choice(page,"Controller","controller",new[]{"1","2","3","4"},true);Choice(page,"Input source","inputSource",new[]{"Auto","XInput","DirectInput"},false);Number(page,"Stick deadzone (%)","liveSplit","deadzone",30,5,95,0,100);
            foreach(string action in new[]{"jump","shoot","dash","ex","lock","swap"}){string captured=action;var button=new Button{AutoSize=true,Name="Pad_"+action};padButtons[action]=button;Row(page,LabelFor(action),button);button.Click+=(s,e)=>{CancelCapture();waitingPad=captured;padReleased=false;button.Text="Release buttons…";};tips.SetToolTip(button,"Click, release all controller buttons, then press the desired button. Escape cancels.");button.KeyDown+=(s,e)=>{if(e.KeyCode==Keys.Escape){CancelCapture();e.Handled=true;e.SuppressKeyPress=true;}};}
            refreshers.Add(RefreshBindingLabels);
        }
        void BeginKey(string action,Button button){CancelCapture();waitingKey=action;button.Text="Press a key (Esc clears)…";button.Focus();}
        void WireKeyCapture(Button button){button.PreviewKeyDown+=(s,e)=>{if(waitingKey!=null)e.IsInputKey=true;};button.KeyDown+=(s,e)=>{if(waitingKey==null)return;string action=waitingKey;string code=e.KeyCode==Keys.Escape?"":KeyCode(e.KeyCode);if(code==null)return;Component.Map(owner.Config,"keybinds")[action]=code;waitingKey=null;RefreshBindingLabels();e.Handled=true;e.SuppressKeyPress=true;};}
        [DllImport("user32.dll")] static extern short GetKeyState(int key);
        public static string KeyCode(Keys key){int n=(int)key;if(n>=65&&n<=90)return "Key"+(char)n;if(n>=48&&n<=57)return "Digit"+(char)n;if(n>=96&&n<=105)return "Numpad"+(n-96);if(n>=112&&n<=135)return "F"+(n-111);switch(key){case Keys.Up:return "ArrowUp";case Keys.Down:return "ArrowDown";case Keys.Left:return "ArrowLeft";case Keys.Right:return "ArrowRight";case Keys.Space:return "Space";case Keys.Tab:return "Tab";case Keys.Enter:return "Enter";case Keys.Escape:return "Escape";case Keys.Back:return "Backspace";case Keys.ShiftKey:return (GetKeyState(161)&0x8000)!=0?"ShiftRight":"ShiftLeft";case Keys.ControlKey:return (GetKeyState(163)&0x8000)!=0?"ControlRight":"ControlLeft";case Keys.Menu:return (GetKeyState(165)&0x8000)!=0?"AltRight":"AltLeft";case Keys.CapsLock:return "CapsLock";case Keys.Insert:return "Insert";case Keys.Delete:return "Delete";case Keys.Home:return "Home";case Keys.End:return "End";case Keys.PageUp:return "PageUp";case Keys.PageDown:return "PageDown";case Keys.OemMinus:return "Minus";case Keys.Oemplus:return "Equal";case Keys.OemOpenBrackets:return "BracketLeft";case Keys.OemCloseBrackets:return "BracketRight";case Keys.OemPipe:return "Backslash";case Keys.OemSemicolon:return "Semicolon";case Keys.OemQuotes:return "Quote";case Keys.Oemcomma:return "Comma";case Keys.OemPeriod:return "Period";case Keys.OemQuestion:return "Slash";case Keys.Oemtilde:return "Backquote";case Keys.Add:return "NumpadAdd";case Keys.Subtract:return "NumpadSubtract";case Keys.Multiply:return "NumpadMultiply";case Keys.Divide:return "NumpadDivide";case Keys.Decimal:return "NumpadDecimal";default:return null;}}
        public static string KeyLabel(string code){if(string.IsNullOrEmpty(code))return "Unbound";if(code.StartsWith("Key"))return code.Substring(3);if(code.StartsWith("Digit"))return code.Substring(5);if(code.StartsWith("Arrow"))return code.Substring(5);return code.Replace("Left"," (left)").Replace("Right"," (right)");}
        string PadLabel(int index){string[] names={"A","B","X","Y","LB","RB","LT","RT","Back","Start","L3","R3","D-pad up","D-pad down","D-pad left","D-pad right","Guide"};return owner.PadSource!="DirectInput"&&index<names.Length?names[index]:"Button "+(index+1);}
        void RefreshBindingLabels(){foreach(var kv in keyButtons)if(waitingKey!=kv.Key)kv.Value.Text=KeyLabel(Component.Text(Component.Map(owner.Config,"keybinds"),kv.Key,""));foreach(var kv in padButtons)if(waitingPad!=kv.Key)kv.Value.Text=PadLabel((int)Component.Number(Component.Map(owner.Config,"gamepadBinds"),kv.Key,0,0,127));}
        public void RefreshSettings(){if(IsDisposed)return;refreshing=true;foreach(var refresh in refreshers)refresh();RefreshElement();if(preview!=null){preview.Visible=true;preview.Invalidate();}refreshing=false;}
        void CancelCapture(){waitingKey=null;waitingPad=null;RefreshBindingLabels();}
        void RefreshRuntime(){if(!Visible)return;status.Text=owner.Reader.Status+Environment.NewLine+owner.Reader.InputStatus+" · "+owner.PadSource;if(preview!=null)preview.Invalidate();foreach(var picture in stylePreviews)picture.Invalidate();
            if(waitingPad!=null){if(!ContainsFocus){CancelCapture();return;}int pressed=Array.FindIndex(owner.PadButtons,b=>b);if(pressed<0){padReleased=true;padButtons[waitingPad].Text="Press a controller button…";}else if(padReleased){Component.Map(owner.Config,"gamepadBinds")[waitingPad]=pressed;waitingPad=null;RefreshBindingLabels();}}
        }
        protected override void Dispose(bool disposing){if(disposing&&!disposed){disposed=true;owner.Changed-=RefreshSettings;ticker.Stop();ticker.Dispose();tips.Dispose();}base.Dispose(disposing);}
    }
    public sealed class Preview : Panel {
        readonly Component owner;bool dragging;Point start;double oldX,oldY;RectangleF frozen;
        readonly bool[] previewInputs=new bool[10];
        public int Selected;public Action<int> SelectionChanged;public Action PositionChanged;public bool Editable=true;public int InputPreviewMode;
        public Preview(Component owner){this.owner=owner;DoubleBuffered=true;BorderStyle=BorderStyle.FixedSingle;BackColor=Color.FromArgb(32,32,32);Cursor=Cursors.Hand;}
        RectangleF ViewBounds{get{return dragging?frozen:owner.Bounds();}}
        float Fit(RectangleF b){return Math.Min((ClientSize.Width-12)/b.Width,(ClientSize.Height-12)/b.Height);}
        PointF ToLayout(Point p,RectangleF b){float f=Fit(b);return new PointF((p.X-(ClientSize.Width-b.Width*f)/2)/f+b.X,(p.Y-(ClientSize.Height-b.Height*f)/2)/f+b.Y);}
        protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);var b=ViewBounds;if(InputPreviewMode==0)Array.Copy(owner.Pressed,previewInputs,previewInputs.Length);else for(int i=0;i<previewInputs.Length;i++)previewInputs[i]=InputPreviewMode==2;bool transparent=owner.Detached&&Component.Flag(owner.Options,"windowTransparent",false);if(transparent){using(var dark=new SolidBrush(Color.FromArgb(180,180,180)))using(var light=new SolidBrush(Color.FromArgb(225,225,225)))for(int y=0;y<ClientSize.Height;y+=12)for(int x=0;x<ClientSize.Width;x+=12)e.Graphics.FillRectangle(((x/12+y/12)%2==0)?light:dark,x,y,12,12);}owner.Render(e.Graphics,ClientSize.Width,ClientSize.Height,b,previewInputs,!transparent);if(!Editable)return;var rect=owner.ElementBounds(Selected);float f=Fit(b);if(f<=0)return;rect=new RectangleF((ClientSize.Width-b.Width*f)/2+(rect.X-b.X)*f,(ClientSize.Height-b.Height*f)/2+(rect.Y-b.Y)*f,rect.Width*f,rect.Height*f);using(var pen=new Pen(SystemColors.Highlight,1)){pen.DashStyle=System.Drawing.Drawing2D.DashStyle.Dash;e.Graphics.DrawRectangle(pen,rect.X,rect.Y,rect.Width,rect.Height);}}
        protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(!Editable||e.Button!=MouseButtons.Left)return;var b=owner.Bounds();var point=ToLayout(e.Location,b);for(int i=Component.Elements.Length-1;i>=0;i--){var config=Component.Map(Component.Map(owner.Config,"elementSettings"),Component.Elements[i]);if(Component.Flag(config,"visible",true)&&owner.ElementBounds(i).Contains(point)){Selected=i;if(SelectionChanged!=null)SelectionChanged(i);oldX=Component.Number(config,"offsetX",0,-10000,10000);oldY=Component.Number(config,"offsetY",0,-10000,10000);start=e.Location;frozen=b;dragging=true;Capture=true;Invalidate();break;}}}
        protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(!dragging)return;var config=Component.Map(Component.Map(owner.Config,"elementSettings"),Component.Elements[Selected]);float fit=Fit(frozen);if(fit<=0)return;config["offsetX"]=Math.Max(-10000,Math.Min(10000,oldX+(e.X-start.X)/fit));config["offsetY"]=Math.Max(-10000,Math.Min(10000,oldY+(e.Y-start.Y)/fit));if(PositionChanged!=null)PositionChanged();Invalidate();}
        protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);dragging=false;Capture=false;Invalidate();}
    }
}
