using System.Collections.Generic;
namespace CupheadInputDisplay {
    public sealed class CpsCounter {
        readonly Queue<long> presses=new Queue<long>(64);
        bool active,held;
        public int Count {get{return presses.Count;}}
        public void Reset(){presses.Clear();active=false;held=false;}
        public bool Update(bool enabled,bool pressed,long milliseconds){
            int previous=Count;
            if(!enabled){Reset();return previous!=0;}
            // A key already held when entering results is not a new click.
            if(!active){active=true;held=pressed;return false;}
            while(presses.Count>0&&milliseconds-presses.Peek()>=1000)presses.Dequeue();
            if(pressed&&!held)presses.Enqueue(milliseconds);
            held=pressed;return previous!=Count;
        }
    }
}
