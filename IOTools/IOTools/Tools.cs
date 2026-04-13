using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tools
{
    public class IOTools
    {
        public static IPAddress? GetIPFromInterface(string name)
        {
            IPAddress? Ip = null;
            NetworkInterface[] networkInterfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface networkInterface in networkInterfaces)
            {
                if (networkInterface.Name.ToLower() == name.ToLower())
                {
                    IPInterfaceProperties properties = networkInterface.GetIPProperties();
                    foreach (IPAddressInformation address in properties.UnicastAddresses)
                    {
                        if (address.Address.IsIPv6LinkLocal) { continue; }
                        Ip = address.Address;
                    }
                    break;
                }
            }
            return Ip;
        }
    }
    public class IOConfig
    {
        public IPAddress Address { get; set; } = IPAddress.Any;
        public int Port { get; set; } = 0;
        public int Timeout { get; set; } = 30000; // 30 ms.
        public int Buffer { get; set; } = 4096;
        public IPlugIns? PlugIns { get; set; }
        public string Tls { get; set; } = string.Empty;
        public Encoding Encoding { get; set; } = Encoding.ASCII;
        public int MaxConnections { get; set; } = 3;
        public int observation { get; set; } = -1;
        public string? Authorization { get; set; }
        public string? Dataparser { get; set; }
        public IOLogCfg? Log { get; set; }
        public IOLogCfg? Monitor { get; set; }
        public IOConfig() { }
        public static IOConfig? Load(string path)
        {
            return JsonReader.Load<IOConfig>(path);
        }
    }
    public class IOHostConfig
    {
        public Encoding Encoding { get; set; }
        public List<string> Default { get; set; }
        public string Content { get; set; }
        public string Pages { get; set; }
        public IPlugIns Bin { get; set; }
        public bool Cors { get; set; }
        public IOHostConfig()
        {
            Encoding = Encoding.ASCII;
            Default = new List<string>();
            Bin = new IPlugIns();
            Content = Environment.CurrentDirectory;
            Pages = Environment.CurrentDirectory;
            Cors = false;
        }
        public static IOHostConfig? Load(string path)
        {
            if (!File.Exists(path)) { throw new FileNotFoundException($"Bindings configuration file \"{path}\" not found."); }
            IOHostConfig? Host = JsonReader.Load<IOHostConfig>(path);
            if (Host == null) { return null; }

            char separator = Path.DirectorySeparatorChar;
            string absolutepathDef = $"{separator}{separator}";
            if (string.IsNullOrEmpty(Host.Content))
            {
                Host.Content = Environment.CurrentDirectory;
            }
            else if (Host.Content.StartsWith(separator) && !Host.Content.StartsWith(absolutepathDef))
            {
                Host.Content = Path.Combine(Environment.CurrentDirectory, Host.Content.Substring(1));
            }
            if (!Directory.Exists(Host.Content)) { Host.Content = string.Empty; }
            Host.Content = Host.Content.ToLower();

            if (string.IsNullOrEmpty(Host.Pages))
            {
                Host.Pages = Environment.CurrentDirectory;
            }
            else if (Host.Pages.StartsWith(separator) && !Host.Pages.StartsWith(absolutepathDef))
            {
                Host.Pages = Path.Combine(Environment.CurrentDirectory, Host.Pages.Substring(1));
            }
            if (!Directory.Exists(Host.Pages)) { Host.Pages = string.Empty; }
            Host.Pages = Host.Pages.ToLower();

            IPlugIns Bin = new IPlugIns();
            for (int i = 0; i < Host.Bin.Count; i++)
            {
                string definition = Host.Bin[i];
                if (string.IsNullOrEmpty(definition))
                {
                    definition = Environment.CurrentDirectory;
                }
                else if (definition.StartsWith(separator) && !definition.StartsWith(absolutepathDef))
                {
                    definition = Path.Combine(Environment.CurrentDirectory, definition.Substring(1));
                }
                definition.ToLower();
                if (File.Exists(definition)) { Bin.Add(definition); }
            }
            Host.Bin = Bin;
            return Host;
        }
        public string Info(bool console, ConsoleColor Color = ConsoleColor.Cyan)
        {
            string rspace = "";
            string space = "\t";
            if (!console)
            {
                rspace = "\t";
                space = "\t\t";
            }
            List<string> Nfo = new List<string>();
            Nfo.Add(rspace + "{");
            Nfo.Add($"{space}Encoding: {Encoding.WebName}");
            if (Default.Count == 0)
            {
                Nfo.Add($"{space}Default: null");
            }
            else if (Default.Count == 1)
            {
                Nfo.Add($"{space}Default: [ {Default[0]} ]");
            }
            else
            {
                Nfo.Add($"{space}Default:");
                Nfo.Add($"{space}[");
                foreach (string s in Default) { Nfo.Add($"{space}\t{s}"); }
                Nfo.Add($"{space}]");
            }
            string value = Content;
            if (string.IsNullOrEmpty(value)) { value = "null"; }
            Nfo.Add($"{space}Content: {value}");
            value = Pages;
            if (string.IsNullOrEmpty(value)) { value = "null"; }
            Nfo.Add($"{space}Pages: {value}");
            if (Bin.Count == 0)
            {
                Nfo.Add($"{space}Bins: null");
            }
            else if (Bin.Count == 1)
            {
                Nfo.Add($"{space}Bins: [ {Bin[0]} ]");
            }
            else
            {
                Nfo.Add($"{space}Bins:");
                Nfo.Add($"{space}[");
                foreach (string s in Bin) { Nfo.Add($"{space}\t{s}"); }
                Nfo.Add($"{space}]");
            }
            Nfo.Add($"{space}Cors: {Cors.ToString().ToLower()}");
            Nfo.Add(rspace + "}");
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
    public class IPlugIns : List<string>
    {
        public IPlugIns()
        {
        }
        public IPlugIns(IPlugIns Base) : base(Base)
        {
        }
        public static IPlugIns? Load(string cfgpath, string plugins)
        {
            IPlugIns? PlugIns = JsonReader.Load<IPlugIns>(cfgpath);
            if (PlugIns != null)
            {
                int count = PlugIns.Count;
                for (int i = 0; i < count; i++)
                {
                    var plugIn = PlugIns[i];
                    PlugIns[i] = Path.Combine(plugins, plugIn);
                }
            }
            return PlugIns;
        }
    }
    public class IOLogCfg
    {
        public IPAddress Address { get; set; } = IPAddress.Any;
        public int Port { get; set; } = 0;
        public IODbCfg? Db { get; set; }
        public IOLogCfg() { }
    }
    public class IODbCfg
    {
        public IPEndPoint Host { get; set; }
        public string Database { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string Pass { get; set; } = string.Empty;
        public IODbCfg() { Host = new IPEndPoint(IPAddress.Loopback, 0); }
    }
    public class JsonReader
    {
        public class ICodePage : JsonConverter<Encoding>
        {
            public override Encoding? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? codepage = reader.GetString();
                if (string.IsNullOrEmpty(codepage)) { return Encoding.UTF8; }
                return Encoding.GetEncoding(codepage);
            }
            public override void Write(Utf8JsonWriter writer, Encoding value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.EncodingName);
            }
        }
        public class IDateTime : JsonConverter<DateTime>
        {
            public string format = "yyyy-MM-dd HH:mm:ss";
            public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? datetimestring = reader.GetString();
                if (string.IsNullOrEmpty(datetimestring)) { return DateTime.UtcNow; }
                if (datetimestring.ToLower().Trim() == "now") { return DateTime.Now; }
                if (datetimestring.ToLower().Trim() == "utcnow") { return DateTime.UtcNow; }
                if (DateTime.TryParse(datetimestring, out DateTime dt)) { return dt; }
                return DateTime.MinValue;
            }
            public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
            {
                writer.WriteStringValue(value.ToString(format));
            }
        }
        public class IP : JsonConverter<IPAddress>
        {
            public override IPAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? address = reader.GetString()?.Trim();
                if (string.IsNullOrEmpty(address) || address == "any") { return IPAddress.Any; }
                if (address == "localhost") { return IPAddress.Loopback; }
                if (address.IndexOf(':') > 0)
                {
                    IPAddress ip;
                    try
                    {
                        int port = int.Parse(address.Substring(address.IndexOf(':') + 1).Trim());
                        ip = IPAddress.Parse(address.Substring(0, address.IndexOf(':')).Trim());
                        return ip;
                    }
                    catch { throw; }
                }
                return IPAddress.Parse(address);
            }
            public override void Write(Utf8JsonWriter writer, IPAddress address, JsonSerializerOptions options)
            {
                writer.WriteStringValue(address.ToString());
            }
        }
        public class IEndPoint : JsonConverter<IPEndPoint>
        {
            public override IPEndPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                string? endpoint = reader.GetString()?.Trim().ToLower();
                if (string.IsNullOrEmpty(endpoint) || endpoint == "any" || endpoint == "any:0") { return new IPEndPoint(IPAddress.Any, 0); }
                if (endpoint.StartsWith("localhost:"))
                {
                    try
                    {
                        int Port = int.Parse(endpoint.Substring(endpoint.IndexOf(':') + 1));
                        return new IPEndPoint(IPAddress.Loopback, Port);
                    }
                    catch { throw; }
                }
                if (endpoint.StartsWith("any:"))
                {
                    try
                    {
                        int Port = int.Parse(endpoint.Substring(endpoint.IndexOf(':') + 1));
                        return new IPEndPoint(IPAddress.Any, Port);
                    }
                    catch { throw; }
                }
                try
                {
                    return IPEndPoint.Parse(endpoint);
                }
                catch { throw; }
            }
            public override void Write(Utf8JsonWriter writer, IPEndPoint endpoint, JsonSerializerOptions options)
            {
                writer.WriteStringValue(endpoint.ToString());
            }
        }
        public static T? Load<T>(string path)
        {
            if (!File.Exists(path)) { throw new FileNotFoundException("Configuration file not found.", path); }
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
                Converters = { new ICodePage(), new IDateTime(), new IP(), new IEndPoint() }
            };

            T? Result;
            try
            {
                string json = File.ReadAllText(path);
                Result = JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception) { throw; }
            return Result;
        }
        public static T? Load<T>(string path, IList<JsonConverter> Converters)
        {
            if (!File.Exists(path)) { throw new FileNotFoundException("Configuration file not found.", path); }
            JsonSerializerOptions options = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
                PropertyNameCaseInsensitive = true,
            };
            foreach (JsonConverter converter in Converters) { options.Converters.Add(converter); }
            T? Result;
            try
            {
                string json = File.ReadAllText(path);
                Result = JsonSerializer.Deserialize<T>(json, options);
            }
            catch (Exception) { throw; }
            return Result;
        }
    }
}
