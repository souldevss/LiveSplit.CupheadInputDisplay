using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
namespace CupheadInputDisplay {
    public static class AssetStore {
        static readonly Dictionary<string,string> Names=BuildNames();
        static Dictionary<string,string> BuildNames(){var names=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);foreach(var name in Assembly.GetExecutingAssembly().GetManifestResourceNames())names[name]=name;return names;}
        public static Stream Open(string relative) {
            string name;return Names.TryGetValue("CupheadInputDisplay.Asset/"+relative.Replace('\\','/'),out name)?Assembly.GetExecutingAssembly().GetManifestResourceStream(name):null;
        }
    }
}
