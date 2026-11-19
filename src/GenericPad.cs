using System;
using System.Linq;
using System.Windows.Forms;
using SharpDX.DirectInput;
namespace CupheadInputDisplay {
    // Background, non-exclusive controller access: never captures the game's device.
    public sealed class GenericPad : IDisposable {
        DirectInput input; Joystick joystick; NativeWindow window; int selected=-1; DateTime retry;
        public bool[] Buttons=new bool[128];public double X,Y;public bool Up,Down,Left,Right;
        public bool Poll(int index){
            try {
                if(index!=selected){Release();selected=index;retry=DateTime.MinValue;}
                if(joystick==null){
                    if(DateTime.UtcNow<retry)return false;retry=DateTime.UtcNow.AddSeconds(2);
                    if(input==null)input=new DirectInput();
                    var devices=input.GetDevices(DeviceClass.GameControl,DeviceEnumerationFlags.AttachedOnly);
                    if(index>=devices.Count)return false;
                    if(window==null){window=new NativeWindow();window.CreateHandle(new CreateParams());}
                    joystick=new Joystick(input,devices[index].InstanceGuid);
                    joystick.SetCooperativeLevel(window.Handle,CooperativeLevel.Background|CooperativeLevel.NonExclusive);
                    foreach(var obj in joystick.GetObjects())if((obj.ObjectId.Flags&DeviceObjectTypeFlags.Axis)!=0)joystick.GetObjectPropertiesById(obj.ObjectId).Range=new InputRange(-32768,32767);
                    joystick.Acquire();
                }
                joystick.Poll();var state=joystick.GetCurrentState();Buttons=state.Buttons;X=state.X/32768.0;Y=state.Y/32768.0;
                Up=Down=Left=Right=false;foreach(int p in state.PointOfViewControllers){if(p<0)continue;Up|=p>=31500||p<=4500;Right|=p>=4500&&p<=13500;Down|=p>=13500&&p<=22500;Left|=p>=22500&&p<=31500;}
                return true;
            }catch{Release();retry=DateTime.UtcNow.AddSeconds(2);return false;}
        }
        void Release(){if(joystick!=null){try{joystick.Unacquire();}catch{}joystick.Dispose();joystick=null;}Array.Clear(Buttons,0,Buttons.Length);}
        public void Dispose(){Release();if(input!=null)input.Dispose();if(window!=null)window.DestroyHandle();}
    }
}
