using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using MNN.Unity;
using MNN.Unity.Interop;

// Unity API adapters only; production managed inference and all native imports are
// compiled unchanged. This is host Mono interop evidence, not a Unity Player test.
namespace UnityEngine
{
    public enum RuntimePlatform { OSXEditor=0, OSXPlayer=1, WindowsPlayer=2, WindowsEditor=7, IPhonePlayer=8, Android=11, LinuxPlayer=13, LinuxEditor=16 }
    public static class Application
    {
#if UNITY_STANDALONE_WIN
        public static RuntimePlatform platform => RuntimePlatform.WindowsPlayer;
#else
        public static RuntimePlatform platform => RuntimePlatform.LinuxPlayer;
#endif
        public static string persistentDataPath => Path.GetFullPath("cache");
        public static string unityVersion => "HostMono-UnityAPIAdapters";
    }
    public static class Debug { public static void Log(object value) { Console.WriteLine(value); } public static void LogError(object value) { Console.Error.WriteLine(value); } }
    public static class SystemInfo { public static int processorCount => Environment.ProcessorCount; public static string graphicsDeviceName => "HostMono"; public static string graphicsDeviceVersion => "HostMono"; public static string deviceModel => "HostMono"; }
    public static class Mathf { public static int Clamp(int v, int min, int max) => Math.Max(min, Math.Min(max,v)); public static int Min(int a,int b) => Math.Min(a,b); }
}

namespace UnityEngine { public enum RuntimeInitializeLoadType { BeforeSceneLoad, SubsystemRegistration, AfterAssembliesLoaded } public sealed class RuntimeInitializeOnLoadMethodAttribute : Attribute { public RuntimeInitializeOnLoadMethodAttribute(RuntimeInitializeLoadType t) {} } }

namespace UnityEngine
{
 // JsonUtility's public-field contract only. The parser is the unchanged
 // production model JSON parser; host services do not participate in inference.
 public static class JsonUtility
 {
  public static T FromJson<T>(string text) => (T)ConvertValue(MNN.Unity.MNNModelData.Parse(text),typeof(T));
  static object ConvertValue(object value,Type type)
  {
   if(value==null)return null;
   if(type==typeof(string))return (string)value;
   if(type==typeof(bool))return (bool)value;
   if(type.IsPrimitive)return Convert.ChangeType(value,type,System.Globalization.CultureInfo.InvariantCulture);
   if(type.IsArray)
   {
    var source=(System.Collections.Generic.List<object>)value;
    var result=Array.CreateInstance(type.GetElementType(),source.Count);
    for(int i=0;i<source.Count;i++)result.SetValue(ConvertValue(source[i],type.GetElementType()),i);
    return result;
   }
   var fields=MNN.Unity.MNNModelData.Object(value);var instance=Activator.CreateInstance(type,true);
   foreach(var field in type.GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
    if(fields.TryGetValue(field.Name,out var item))field.SetValue(instance,ConvertValue(item,field.FieldType));
   return instance;
  }
  public static string ToJson(object value)
  {
   var result=new StringBuilder();Append(result,value);return result.ToString();
  }
  static void Append(StringBuilder result,object value)
  {
   if(value==null){result.Append("null");return;}
   if(value is string text){Quote(result,text);return;}
   if(value is bool boolean){result.Append(boolean?"true":"false");return;}
   if(value.GetType().IsPrimitive){result.Append(Convert.ToString(value,System.Globalization.CultureInfo.InvariantCulture));return;}
   if(value is System.Collections.IEnumerable items)
   {
    result.Append('[');bool first=true;foreach(var item in items){if(!first)result.Append(',');first=false;Append(result,item);}result.Append(']');return;
   }
   result.Append('{');bool start=true;
   foreach(var field in value.GetType().GetFields(System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.Instance))
   {if(!start)result.Append(',');start=false;Quote(result,field.Name);result.Append(':');Append(result,field.GetValue(value));}
   result.Append('}');
  }
  static void Quote(StringBuilder result,string text)
  {
   result.Append('"');
   foreach(char c in text)
   {
    if(c=='"'||c=='\\')result.Append('\\').Append(c);
    else if(c<' ')result.Append("\\u").Append(((int)c).ToString("x4"));
    else result.Append(c);
   }
   result.Append('"');
  }
 }
}
