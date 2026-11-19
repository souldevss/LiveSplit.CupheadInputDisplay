using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
namespace CupheadInputDisplay {
    // Inspects managed metadata and IL only. No game methods are invoked.
    public sealed class InputSchema {
        public FieldInfo Manager,Matrix,Mapping,ActionList,ActionName,PlayerIndex;
        public FieldInfo[] Button,Axis1,Axis2,Deadzone;
        public static readonly string[] Actions={"Jump","Shoot","Dash","Super","Lock","SwitchWeapon","MoveHorizontal","MoveVertical"};
        static readonly Dictionary<short,OpCode> Opcodes=typeof(OpCodes).GetFields(BindingFlags.Public|BindingFlags.Static).Where(f=>f.FieldType==typeof(OpCode)).Select(f=>(OpCode)f.GetValue(null)).ToDictionary(o=>o.Value);
        public static List<MemberInfo> References(MethodBase method){
            var members=new List<MemberInfo>();var body=method.GetMethodBody();if(body==null)return members;byte[] data=body.GetILAsByteArray();int p=0;
            while(p<data.Length){short code=data[p++];if(code==0xfe)code=(short)(0xfe00|data[p++]);OpCode op;if(!Opcodes.TryGetValue(code,out op))throw new InvalidOperationException("Unknown game IL");int bytes;
                switch(op.OperandType){case OperandType.InlineNone:bytes=0;break;case OperandType.ShortInlineBrTarget:case OperandType.ShortInlineI:case OperandType.ShortInlineVar:bytes=1;break;case OperandType.InlineVar:bytes=2;break;case OperandType.InlineI8:case OperandType.InlineR:bytes=8;break;case OperandType.InlineSwitch:int n=BitConverter.ToInt32(data,p);bytes=4+n*4;break;default:bytes=4;break;}
                if(op.OperandType==OperandType.InlineField||op.OperandType==OperandType.InlineMethod){try{members.Add(method.Module.ResolveMember(BitConverter.ToInt32(data,p)));}catch(ArgumentException){}}
                p+=bytes;if(p>data.Length)throw new InvalidOperationException("Invalid game IL");
            }return members;
        }
        static MethodInfo Method(Type type,string name,string parameter){return type.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance).First(m=>m.Name==name&&m.GetParameters().Length==1&&m.GetParameters()[0].ParameterType.FullName==parameter);}
        static FieldInfo[] Fields(MethodBase method){return References(method).OfType<FieldInfo>().ToArray();}
        public static InputSchema Load(string managedDirectory){
            ResolveEventHandler resolver=(s,e)=>{string path=Path.Combine(managedDirectory,new AssemblyName(e.Name).Name+".dll");return File.Exists(path)?Assembly.ReflectionOnlyLoadFrom(path):Assembly.ReflectionOnlyLoad(e.Name);};
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve+=resolver;
            try{
                var assembly=Assembly.ReflectionOnlyLoadFrom(Path.Combine(managedDirectory,"Rewired_Core.dll"));var player=assembly.GetType("Rewired.Player",true);
                var refs=References(Method(player,"GetButton","System.Int32"));var calls=refs.OfType<MethodInfo>().ToArray();if(calls.Length<3)throw new InvalidOperationException("Unsupported Rewired button implementation");
                var schema=new InputSchema();schema.Manager=Fields(calls[0]).First(f=>f.IsStatic);
                schema.PlayerIndex=refs.OfType<FieldInfo>().First(f=>!f.IsStatic&&f.DeclaringType.FullName=="Rewired.Player"&&f.FieldType.FullName=="System.Int32");
                schema.Matrix=Fields(calls[1]).First(f=>f.FieldType.IsArray&&f.FieldType.GetArrayRank()==2);
                schema.Mapping=Fields(calls[1]).First(f=>!f.FieldType.IsArray);
                schema.ActionList=schema.Mapping.FieldType.GetFields(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance).First(f=>f.FieldType.IsGenericType&&f.FieldType.GetGenericTypeDefinition().FullName=="System.Collections.Generic.List`1"&&f.FieldType.GetGenericArguments()[0].FullName=="Rewired.InputAction");
                var action=assembly.GetType("Rewired.InputAction",true);schema.ActionName=Fields(action.GetProperty("name").GetGetMethod(true)).First(f=>f.FieldType.FullName=="System.String");
                schema.Button=Fields(calls[calls.Length-1]).Take(3).ToArray();if(schema.Button.Length!=3||schema.Button[2].FieldType.FullName!="System.Boolean")throw new InvalidOperationException("Unsupported Rewired button state");
                var axisRefs=References(Method(player,"GetAxis","System.Int32")).OfType<MethodInfo>().ToArray();var axisFields=Fields(axisRefs.Last());schema.Axis1=axisFields.Take(3).ToArray();schema.Axis2=axisFields.Skip(3).Take(3).ToArray();
                if(schema.Axis1.Length!=3||schema.Axis2.Length!=3||schema.Axis1[2].FieldType.FullName!="System.Single"||schema.Axis2[2].FieldType.FullName!="System.Single")throw new InvalidOperationException("Unsupported Rewired axis state");
                var behavior=calls.Last().DeclaringType.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic).FirstOrDefault(f=>f.FieldType.FullName=="Rewired.InputBehavior");
                if(behavior!=null){var property=behavior.FieldType.GetProperty("buttonDeadZone");if(property!=null)schema.Deadzone=(new[]{behavior}).Concat(Fields(property.GetGetMethod(true))).ToArray();}
                return schema;
            }finally{AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve-=resolver;}
        }
    }
}
