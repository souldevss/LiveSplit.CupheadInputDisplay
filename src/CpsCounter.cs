using System;
using System.Collections.Generic;
namespace CupheadInputDisplay {
    public sealed class CpsCounter {
        readonly Queue<long> presses=new Queue<long>(64);
        bool active,heldZ,heldA;long lastPress;
        public int Count {get;private set;}
        public double Rate {get;private set;}
        public void Reset(){presses.Clear();active=false;heldZ=heldA=false;Count=0;Rate=0;}
        public bool Update(bool enabled,bool pressed,long milliseconds){return Update(enabled,pressed,false,milliseconds);}
        public bool Update(bool enabled,bool z,bool a,long milliseconds){
            int previous=Count;double previousRate=Math.Round(Rate,2);
            if(!enabled){Reset();return previous!=0||previousRate!=0;}
            if(!active){active=true;heldZ=z;heldA=a;return false;}
            if(presses.Count>0&&milliseconds-lastPress>=1000)presses.Clear();
            while(presses.Count>0&&milliseconds-presses.Peek()>=2000)presses.Dequeue();
            if(z&&!heldZ){presses.Enqueue(milliseconds);lastPress=milliseconds;}
            if(a&&!heldA){presses.Enqueue(milliseconds);lastPress=milliseconds;}
            heldZ=z;heldA=a;Count=0;foreach(long press in presses)if(milliseconds-press<1000)Count++;
            if(presses.Count<2||lastPress==presses.Peek())Rate=Count;
            else{double measured=(presses.Count-1)*1000.0/(lastPress-presses.Peek());Rate=Math.Min(measured,1000.0/Math.Max(1,milliseconds-lastPress));}
            return Count!=previous||Math.Round(Rate,2)!=previousRate;
        }
    }
}
