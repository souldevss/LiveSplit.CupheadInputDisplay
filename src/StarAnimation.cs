using System;
namespace CupheadInputDisplay {
    // Cuphead animation frames are displayed at 12 fps. A held input never retriggers.
    public sealed class StarAnimation {
        public const int FrameMilliseconds=83,AppearFrames=8;
        bool held;long started=-1;string path;
        public string Path {get{return path??Idle(0);}}
        public void Reset(){held=false;started=-1;path=null;}
        static string Idle(long frame){return "assets/Display/Star/Main/winscreen_main_star_"+(char)('a'+frame%3)+".png";}
        public bool Advance(bool pressed,long milliseconds){
            if(pressed&&!held)started=milliseconds;held=pressed;
            long frame=started<0?AppearFrames:Math.Max(0,milliseconds-started)/FrameMilliseconds;
            string next=frame<AppearFrames?"assets/Display/Star/Appear/winscreen_star_appear_"+(frame+1).ToString("D4")+".png":Idle(started<0?milliseconds/FrameMilliseconds:frame-AppearFrames);
            bool changed=next!=path;path=next;return changed;
        }
    }
}
