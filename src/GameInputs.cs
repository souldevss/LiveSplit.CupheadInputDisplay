using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
namespace CupheadInputDisplay {
    public sealed partial class GameReader {
        public string InputStatus="Waiting for in-game controls; fallback input available";
        InputSchema inputSchema;bool inputReady;long inputController;DateTime schemaRetry;
        public bool HasInputs {get{return inputReady;}}
        readonly byte[] inputBuffer=new byte[4];readonly float[] inputValues=new float[8];readonly bool[] inputButtons=new bool[6];
        readonly long[] buttonAddresses=new long[8],axis1Addresses=new long[8],axis2Addresses=new long[8],deadzoneAddresses=new long[8];
        readonly Dictionary<string,long> inputClasses=new Dictionary<string,long>();
        void ResetInputs(){inputReady=false;inputController=0;inputClasses.Clear();InputStatus="Waiting for in-game controls; fallback input available";}
        void InputScalar(long at,int count){IntPtr read;if(at<0x10000||!ReadProcessMemory(handle,new IntPtr(at),inputBuffer,count,out read)||read.ToInt64()!=count)throw new InvalidOperationException("In-game input memory unavailable");}
        float Float(long at){InputScalar(at,4);float n=BitConverter.ToSingle(inputBuffer,0);if(float.IsNaN(n)||float.IsInfinity(n))throw new InvalidOperationException("Invalid input value");return n;}
        bool InputBool(long at){InputScalar(at,1);if(inputBuffer[0]>1)throw new InvalidOperationException("Invalid input flag");return inputBuffer[0]!=0;}
        string ManagedText(long obj){int length=Int(obj+size*2);if(length<0||length>256)throw new InvalidOperationException("Invalid input action name");return System.Text.Encoding.Unicode.GetString(Read(obj+size*2+4,Math.Max(2,length*2)),0,length*2);}
        long NativeField(long klass,string name){for(int depth=0;klass!=0&&depth<16;depth++,klass=Ptr(klass+parentOffset)){int count=Int(klass+countOffset);if(count<0||count>2048)break;long fields=Ptr(klass+fieldsOffset);for(int i=0;i<count;i++){long field=fields+i*size*4;if(Text(Ptr(field+size))==name)return field;}}throw new InvalidOperationException("Input field unavailable: "+name);}
        long Address(long obj,FieldInfo[] chain){long address=obj;long klass=Class(obj);bool inline=false;for(int i=0;i<chain.Length;i++){var field=chain[i];long meta=NativeField(klass,field.Name);int offset=Int(meta+size*3);if(offset<0||offset>65536)throw new InvalidOperationException("Invalid input field offset");address+=offset-(inline?size*2:0);if(i==chain.Length-1)return address;if(field.FieldType.IsValueType){klass=Ptr(Ptr(meta));inline=true;}else{address=Ptr(address);klass=Class(address);inline=false;}}throw new InvalidOperationException("Empty input path");}
        long FindInputClass(long image,string wanted){long found;if(inputClasses.TryGetValue(wanted,out found))return found;
            int[] cacheOffsets=size==8?new[]{0x3d0,0x4c0,0x4d0,0x4a8}:new[]{0x2a0,0x354,0x35c,0x340};
            int next=size==8?(nameOffset==0x50||fieldsOffset==0x98?0x108:0x100):(nameOffset==0x34?0xac:0xa8);
            foreach(int cacheOffset in cacheOffsets)try{int count=Int(image+cacheOffset+(size==8?0x18:0xc));long table=Ptr(image+cacheOffset+(size==8?0x20:0x14));if(count<1||count>32768||table<0x10000)continue;for(int bucket=0;bucket<count;bucket++){long klass=Ptr(table+bucket*size);for(int depth=0;klass!=0&&depth<256;depth++,klass=Ptr(klass+next)){string name=Text(Ptr(klass+nameOffset));if(name==wanted){inputClasses[wanted]=klass;return klass;}}}}catch{ }
            throw new InvalidOperationException("Waiting for Rewired runtime metadata");
        }
        long Statics(long klass){bool modern=fieldsOffset==0x98||size==4&&fieldsOffset==0x60;int runtime=size==8?(modern?0xd0:nameOffset==0x50?0x100:0xf8):(modern?0x84:nameOffset==0x34?0xa8:0xa4);long info=Ptr(klass+runtime);long vtable=Ptr(info+size);if(!modern)return Ptr(vtable+(size==8?0x18:0xc));int count=Int(klass+(size==8?0x5c:0x38));if(count<0||count>65536)throw new InvalidOperationException("Invalid input vtable");foreach(int start in size==8?new[]{0x40,0x48}:new[]{0x28,0x2c}){long data=Ptr(vtable+start+count*size);if(data>0x10000)return data;}throw new InvalidOperationException("Waiting for input statics");}
        void SetupInputs(long controller,int player){
            if(controller==inputController){inputReady=true;return;}
            if(inputSchema==null){if(DateTime.UtcNow<schemaRetry)return;schemaRetry=DateTime.UtcNow.AddSeconds(5);string exe=process.MainModule.FileName;string directory=Path.Combine(Path.GetDirectoryName(exe),Path.GetFileNameWithoutExtension(exe)+"_Data","Managed");inputSchema=InputSchema.Load(directory);}
            long rewired=Ref(Ref(controller,"_input"),"actions");long image=Ptr(Class(rewired)+nameOffset-size);
            long klass=FindInputClass(image,inputSchema.Manager.DeclaringType.Name);int offset=ClassField(klass,inputSchema.Manager.Name);if(offset<0)throw new InvalidOperationException("Input manager static unavailable");long manager=Ptr(Statics(klass)+offset);
            int row=Int(Address(rewired,new[]{inputSchema.PlayerIndex}));
            long matrix=Ptr(Address(manager,new[]{inputSchema.Matrix}));long bounds=Ptr(matrix+size*2);int rows=(int)Ptr(bounds),columns=(int)Ptr(bounds+size*2);if(rows<1||rows>64||columns<8||columns>4096||row<0||row>=rows)throw new InvalidOperationException("Invalid input action matrix");
            long list=Ptr(Address(Ptr(Address(manager,new[]{inputSchema.Mapping})),new[]{inputSchema.ActionList}));long items=Ptr(list+size*2);int length=Int(list+size*3);if(length<8||length>4096||length>columns)throw new InvalidOperationException("Invalid input action list");
            var indices=new Dictionary<string,int>(StringComparer.OrdinalIgnoreCase);for(int i=0;i<length;i++){long action=Ptr(items+size*4+i*size);string name=ManagedText(Ptr(Address(action,new[]{inputSchema.ActionName})));indices[name]=i;}
            for(int i=0;i<InputSchema.Actions.Length;i++){int index;if(!indices.TryGetValue(InputSchema.Actions[i],out index))throw new InvalidOperationException("Missing game action "+InputSchema.Actions[i]);long action=Ptr(matrix+size*4+(row*columns+index)*size);buttonAddresses[i]=Address(action,inputSchema.Button);axis1Addresses[i]=Address(action,inputSchema.Axis1);axis2Addresses[i]=Address(action,inputSchema.Axis2);deadzoneAddresses[i]=inputSchema.Deadzone!=null?Address(action,inputSchema.Deadzone):0;}
            inputController=controller;inputReady=true;
        }
        public bool TryInputs(bool[] pressed){if(!inputReady){if(InputStatus.StartsWith("In-game controls"))InputStatus="Waiting for active level; fallback input";return false;}try{var values=inputValues;var buttons=inputButtons;for(int i=0;i<8;i++){float a=Float(axis1Addresses[i]),b=Float(axis2Addresses[i]);values[i]=Math.Abs(a)>=Math.Abs(b)?a:b;if(Math.Abs(a)>1.01f||Math.Abs(b)>1.01f)throw new InvalidOperationException("Invalid in-game action state");if(i<6){float zone=deadzoneAddresses[i]==0?0.5f:Float(deadzoneAddresses[i]);if(zone<0||zone>1)throw new InvalidOperationException("Invalid in-game button threshold");buttons[i]=InputBool(buttonAddresses[i])||values[i]>zone;}}for(int i=0;i<6;i++)pressed[i]=buttons[i];float x=values[6],y=values[7],magnitude=(float)Math.Sqrt(x*x+y*y);pressed[6]=magnitude>=0.375&&y/magnitude>0.38268;pressed[7]=magnitude>=0.375&&y/magnitude< -0.38268;pressed[8]=magnitude>=0.375&&x/magnitude< -0.38268;pressed[9]=magnitude>=0.375&&x/magnitude>0.38268;InputStatus="In-game controls · follows Cuphead bindings";return true;}catch(Exception ex){inputController=0;inputReady=false;InputStatus="Fallback input: "+ex.Message;return false;}}
    }
}


