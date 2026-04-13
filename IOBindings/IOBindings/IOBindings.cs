using System.Text.Json.Serialization;
using Activator;
using Tools;

namespace IOBinding
{
    public class IOBinding
    {
        public Dictionary<string, string> Schemes { get; set; }
        public string bindconfig { get; set; }
        public IOHostConfig Config { get; set; }
        public IOBinding()
        {
            bindconfig = string.Empty;
            Schemes = new Dictionary<string, string>();
            Config = new IOHostConfig();
        }
        public string Info(bool console, ConsoleColor Color = ConsoleColor.Cyan)
        {
            string space = "";
            if (!console) { space = "\t"; }
            List<string> Nfo = new List<string>();
            if (Schemes.Count == 0)
            {
                Nfo.Add($"{space}Schemes: null");
            }
            else if (Schemes.Count == 1)
            {
                string Key = Schemes.Keys.First();
                Nfo.Add($"{space}Schemes: [ {Key}: {Schemes[Key]} ]");
            }
            else
            {
                Nfo.Add($"{space}Schemes:");
                Nfo.Add($"{space}[");
                foreach (var s in Schemes) { Nfo.Add($"{space}\t{s.Key}: {s.Value}"); }
                Nfo.Add($"{space}]");
            }
            Nfo.Add($"{space}Config:");
            Nfo.Add($"{(Config).Info(false)}");
            string result = string.Join("\n", Nfo);
            if (console)
            {
                Console.ForegroundColor = Color;
                Console.WriteLine(result);
                Console.ResetColor();
            }
            return result;
        }
    }
    public class IOBindings : Dictionary<string, IOBinding>
    {
        public IOBindings() { }
        public static IOBindings? Load(string path)
        {
            if (!File.Exists(path)) { throw new FileNotFoundException($"Bindings configuration file \"{path}\" not found."); }
            char separator = Path.DirectorySeparatorChar;
            string absolutepathDef = $"{separator}{separator}";

            List<JsonConverter> Converters = new List<JsonConverter>();
            IOBindings? Bindings = JsonReader.Load<IOBindings>(path);
            if (Bindings == null) { return null; }
            foreach (var Binding in Bindings.Values)
            {
                string bindconfig = Binding.bindconfig.Trim();
                if (string.IsNullOrEmpty(bindconfig))
                {
                    bindconfig = Path.Combine(Environment.CurrentDirectory, "cfg.json");
                }
                else if (bindconfig.StartsWith(separator) && !bindconfig.StartsWith(absolutepathDef))
                {
                    bindconfig = Path.Combine(Environment.CurrentDirectory, bindconfig.Substring(1));
                }
                try
                {
                    IOHostConfig? Config = IOHostConfig.Load(bindconfig);
                    if (Config != null) { Binding.Config = Config; }
                }
                catch { }
            }
            return Bindings;
        }
        public string Info(bool console, ConsoleColor Color = ConsoleColor.Cyan)
        {
            List<string> Nfo = new List<string>();
            if (Count == 0)
            {
                Nfo.Add($"Bindings: null");
            }
            else if (Count == 1)
            {
                string Key = this.Keys.ToArray()[0];
                string Value = "{\n" + this[Key].Info(false) + "\n}";
                Nfo.Add($"{Key}: {Value}");
            }
            else
            {
                foreach (var binding in this)
                {
                    Nfo.Add($"{binding.Key}:");
                    Nfo.Add("{");
                    Nfo.Add($"{binding.Value.Info(false)}");
                    Nfo.Add("}");
                }
            }
            string result = string.Join("\n", Nfo);
            if (console)
            {
                Console.ForegroundColor = Color;
                Console.WriteLine(result);
                Console.ResetColor();
            }
            return result;
        }
    }
}
