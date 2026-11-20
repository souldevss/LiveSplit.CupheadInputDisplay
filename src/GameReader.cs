using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Collections.Generic;

namespace CupheadInputDisplay {
    // Reuses the installed autosplitter's version-aware signatures, but never its timer logic.
    public sealed partial class GameReader : IDisposable {
        object memory; Type type; Process process; int pid, size;
        MethodInfo hookMethod,sceneMethod,saveMethod,pointerMethod;
        PropertyInfo programProperty,versionProperty;FieldInfo levelField;
        readonly byte[] scalarBuffer=new byte[8];
        int nameOffset, fieldsOffset, countOffset, parentOffset;
        readonly Dictionary<string,int> offsets = new Dictionary<string,int>();
        public string Status = "Waiting for Cuphead";
        public string SceneName {get;private set;}
        public bool OnWinScreen {get;private set;}
        public static bool IsWinScene(string scene){return string.Equals(scene,"scene_win",StringComparison.Ordinal);}
        [DllImport("kernel32.dll", SetLastError=true)] static extern IntPtr OpenProcess(uint access, bool inherit, int pid);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
        [DllImport("kernel32.dll")] static extern bool IsWow64Process(IntPtr process, out bool wow);
        [DllImport("kernel32.dll", SetLastError=true)] static extern bool ReadProcessMemory(IntPtr h, IntPtr address, byte[] data, int size, out IntPtr read);
        IntPtr handle;
        byte[] Read(long address, int count) {
            if(address < 0x10000 || count<1 || count>65536) throw new InvalidOperationException("Invalid game pointer");
            var data=new byte[count]; IntPtr n;
            if(!ReadProcessMemory(handle,new IntPtr(address),data,count,out n) || n.ToInt64()!=count) throw new InvalidOperationException("Game memory unavailable");
            return data;
        }
        void Scalar(long address,int count){IntPtr read;if(address<0x10000||!ReadProcessMemory(handle,new IntPtr(address),scalarBuffer,count,out read)||read.ToInt64()!=count)throw new InvalidOperationException("Game memory unavailable");}
        long Ptr(long address) {Scalar(address,size);return size==8?BitConverter.ToInt64(scalarBuffer,0):BitConverter.ToUInt32(scalarBuffer,0);}
        int Int(long address) {Scalar(address,4);return BitConverter.ToInt32(scalarBuffer,0);}
        bool Bool(long address) {Scalar(address,1);byte b=scalarBuffer[0];if(b>1)throw new InvalidOperationException("Invalid game flag");return b!=0;}
        string Text(long address) { var b=Read(address,128); int n=Array.IndexOf(b,(byte)0); return Encoding.UTF8.GetString(b,0,n<0?128:n); }
        long Class(long obj) { return Ptr(Ptr(obj)); }
        bool SelectProfile(long obj) {
            long klass=Class(obj);
            int[][] profiles=size==8 ? new[]{new[]{0x48,0xa8,0x94,0x30},new[]{0x50,0xb0,0x9c,0x30},new[]{0x48,0x98,0x100,0x30},new[]{0x48,0xb0,0x94,0x30}} : new[]{new[]{0x30,0x74,0x64,0x24},new[]{0x34,0x78,0x68,0x24},new[]{0x2c,0x60,0xa4,0x20},new[]{0x30,0x78,0x64,0x24}};
            foreach(var p in profiles) try {
                string name=Text(Ptr(klass+p[0]));
                if(name!="PlayerData") continue;
                nameOffset=p[0]; fieldsOffset=p[1]; countOffset=p[2]; parentOffset=p[3];
                offsets.Clear(); if(Field(obj,"loadouts")>=size*2)return true;
            } catch { }
            return false;
        }
        int Field(long obj,string wanted) {
            return ClassField(Class(obj),wanted);
        }
        int ClassField(long klass,string wanted) {
            string key=klass+":"+wanted; int cached;
            if(offsets.TryGetValue(key,out cached))return cached;
            for(int depth=0;klass!=0 && depth<16;depth++,klass=Ptr(klass+parentOffset)) {
                int count=Int(klass+countOffset); if(count<0 || count>2048)break;
                long fields=Ptr(klass+fieldsOffset);
                for(int i=0;i<count;i++) {
                    long f=fields+i*size*4;
                    string n=Text(Ptr(f+size));
                    if(n==wanted || n=="<"+wanted+">k__BackingField") {
                        int off=Int(f+size*3); if(off<0 || off>65536)throw new InvalidOperationException("Invalid field offset");
                        offsets[key]=off;return off;
                    }
                }
            }
            offsets[key]=-1; return -1;
        }
        long Ref(long obj,string field) { int off=Field(obj,field); if(off<0)throw new InvalidOperationException("Missing field: "+field); return Ptr(obj+off); }
        public bool Poll(int player,out string character,out bool plane) {
            character=null; plane=false;inputReady=false;OnWinScreen=false;SceneName=null;
            try {
                if(memory==null) {
                    string path=System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"Components","LiveSplit.Cuphead.dll");
                    var asm=Assembly.LoadFrom(path); type=asm.GetType("LiveSplit.Cuphead.MemoryManager",true); memory=Activator.CreateInstance(type);
                    hookMethod=type.GetMethod("HookProcess");programProperty=type.GetProperty("Program");versionProperty=type.GetProperty("Version");sceneMethod=type.GetMethod("SceneName",BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance);saveMethod=type.GetMethod("CurrentSave",BindingFlags.NonPublic|BindingFlags.Instance);levelField=type.GetField("Level",BindingFlags.NonPublic|BindingFlags.Static);pointerMethod=levelField==null?null:levelField.FieldType.GetMethod("GetPointer");
                }
                if(!(bool)hookMethod.Invoke(memory,null)) { Status="Waiting for Cuphead"; Release(); return false; }
                process=(Process)programProperty.GetValue(memory,null);
                if(process.Id!=pid) {
                    Release(); pid=process.Id;handle=OpenProcess(0x1010,false,pid);
                    bool wow; IsWow64Process(process.Handle,out wow); size=Environment.Is64BitOperatingSystem&&!wow?8:4;
                    if(IntPtr.Size==4 && size==8) {Status="64-bit Cuphead requires 64-bit LiveSplit";return false;}
                }
                if(sceneMethod!=null)SceneName=sceneMethod.Invoke(memory,null) as string;
                OnWinScreen=IsWinScene(SceneName);
                if(OnWinScreen){Status="Cuphead connected · Win screen";return false;}
                if(saveMethod==null)throw new InvalidOperationException("Unsupported autosplitter memory API");
                long save=((IntPtr)saveMethod.Invoke(memory,null)).ToInt64();
                if(nameOffset==0 && !SelectProfile(save)) {Status="Waiting for valid Cuphead save / Mono metadata";return false;}
                int mugField=Field(save,"isPlayer1Mugman"); bool mug=mugField>=0 && Bool(save+mugField);
                character=(player==0?!mug:mug)?"Cuphead":"Mugman";
                var pp=levelField.GetValue(null); long pointer=((IntPtr)pointerMethod.Invoke(pp,new object[]{process})).ToInt64();
                string version=versionProperty.GetValue(null,null).ToString();
                long level=Ptr(pointer+(version=="SteamDLC"?0:-0x20));
                if(level==0) {Status="Cuphead connected · outside level";return false;}
                int mode=Field(level,"playerMode"); if(mode<0)throw new InvalidOperationException("Missing playerMode");
                int playerMode=Int(level+mode); if(playerMode<0||playerMode>3)throw new InvalidOperationException("Invalid player mode"); plane=playerMode==1;
                long players=Ref(level,"players"); int length=Int(players+size*3);
                if(length<1||length>4||player>=length) {Status="Waiting for selected player";return false;}
                long controller=Ptr(players+size*4+player*size); long stats=Ref(controller,"_stats");
                int chalice=Field(stats,"isChalice"); if(chalice>=0 && Bool(stats+chalice))character="Chalice";
                try{SetupInputs(controller,player);}catch(Exception inputError){InputStatus="Fallback input: "+inputError.Message;}
                Status="Cuphead connected · P"+(player+1)+" · "+character+" · "+(plane?"Plane":"Ground");return true;
            } catch(Exception ex) { OnWinScreen=false;SceneName=null;Status="Auto detection waiting: "+(ex.InnerException??ex).Message;return false; }
        }
        void Release() {if(handle!=IntPtr.Zero)CloseHandle(handle);handle=IntPtr.Zero;pid=0;nameOffset=0;OnWinScreen=false;SceneName=null;offsets.Clear();ResetInputs();}
        public void Dispose(){Release(); if(memory!=null) {var m=type.GetMethod("Dispose");if(m!=null)m.Invoke(memory,null);} memory=null;}
    }
}
