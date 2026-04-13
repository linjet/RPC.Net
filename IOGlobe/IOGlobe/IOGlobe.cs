using System.Reflection;
using Activator;
using AssmLoader;
using IOBinding;
using Tools;

namespace Globe
{
    public class IConfigs
    {
        public static IOConfig? FrontConfig { get; set; }
        public static IOConfig? BackConfig { get; set; }
        public static IOBindings? Bindings { get; set; }
    }
    public static class IOGlobe
    {
        public static IConfigs Configs = new IConfigs();
        public static ExecProxy? Proxy { get; set; }
        public static List<ILoader>? Assemblies { get; set; }
        public static IOBinder? Bindings { get; set; }
        public static object? FrontListener { get; set; }
        public static object? BackListener { get; set; }
        public static object? Pools { get; set; }  // пул каналов

        public static string GetAssembliesInfo()
        {
            if (Assemblies == null || Assemblies.Count == 0)
            {
                return "There are no assemblies loaded into the service context.";
            }
            AppDomain Domain = AppDomain.CurrentDomain;
            Assembly[] LoadedAssemblies = Domain.GetAssemblies();
            Dictionary<string, int> LibsInfo = new Dictionary<string, int>();
            foreach (Assembly LoadedAssembly in LoadedAssemblies)
            {
                string Key = LoadedAssembly.Location.ToLower();
                if (!LibsInfo.ContainsKey(Key))
                {
                    LibsInfo.Add(Key, 1);
                }
                else
                {
                    LibsInfo[Key] += 1;
                }
            }
            List<string> Nfo = new List<string>();
            foreach (var LoadedAssembly in LibsInfo)
            {
                Nfo.Add($"{LoadedAssembly.Value} : {LoadedAssembly.Key}");
            }
            return string.Join("\n", Nfo);
        }
    }
}
