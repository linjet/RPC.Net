using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Activator;
using AssmLoader;
using Globe;
using IOMonitor;
using IOObjects;
using IOProcessing;
using Tools;

namespace IOService
{
    public class Actions
    {
        public ExecProxy Proxy { get; set; }
        public List<ILoader>? Assemblies { get; set; }
        public Actions()
        {
            Proxy = new ExecProxy();
            Assemblies = new List<ILoader>();
        }
        public Actions(string[] Assemblies)
        {
            Proxy = new ExecProxy();
            this.Assemblies = new List<ILoader>();
            foreach (var path in Assemblies)
            {
                ILoader Loader = new ILoader(path, Proxy);
                this.Assemblies.Add(Loader);
            }
        }
        public Actions(string binCatalog)
        {
            Proxy = new ExecProxy();
            string[] Assemblies = Directory.GetFiles(binCatalog, "*.dll");
            this.Assemblies = new List<ILoader>();
            foreach (var path in Assemblies)
            {
                ILoader Loader = new ILoader(path, Proxy);
                this.Assemblies.Add(Loader);
            }
        }
        public void Add(string AssemblyPath)
        {
            if (this.Assemblies == null) { this.Assemblies = new List<ILoader>(); }
            ILoader Loader = new ILoader(AssemblyPath, Proxy);
            this.Assemblies.Add(Loader);
        }
        public string Info(bool console)
        {
            List<string> Nfo = new List<string>();
            if (Proxy.Count == 0)
            {
                Nfo.Add($"Actions is NULL");
            }
            else if (Proxy.Count == 1)
            {
                string key = Proxy.Keys.ToList()[0];
                List<object> Fncs = Proxy[key];
                if (Fncs.Count == 1)
                {
                    Nfo.Add($"Action \"{key}\": {Fncs[0].GetType()}");
                }
                else if (Fncs.Count > 1)
                {
                    Nfo.Add($"Actions:");
                    foreach (object ItemAction in Fncs)
                    {
                        Nfo.Add($"\t\"{key}\": {ItemAction.GetType()}");
                    }
                }
            }
            else if (Proxy.Count > 1)
            {
                Nfo.Add($"Actions:");
                foreach (var Item in Proxy)
                {
                    List<object> Actions = Item.Value;
                    if (Actions.Count == 0) { Nfo.Add($"{Item.Key}: null"); }
                    else if (Actions.Count == 1)
                    {
                        object ItemAction = Actions[0];
                        Nfo.Add($"\"{Item.Key}\": {ItemAction.GetType()}");
                    }
                    else
                    {
                        Nfo.Add($"\"{Item.Key}\":");
                        foreach (object ItemAction in Actions)
                        {
                            Nfo.Add($"\t{ItemAction.GetType()}");
                        }
                    }
                }
            }
            string result = string.Join("\n", Nfo);
            if (console)
            {
                Console.ForegroundColor = ConsoleColor.Magenta;
                Console.WriteLine($"{result}");
                Console.ResetColor();
            }
            return result;
        }
    }
    public class TCP
    {
        private readonly object _locker = new object();
        public IOConfig? Config { get; private set; }
        private bool IsFront { get; set; }
        public Guid Id { get; private set; }
        public IPAddress Address { get; set; }
        public int Port { get; set; } = 0;
        public int Buffer { get; set; } = 4096;
        public int Timeout { get; set; } = 30000; // 30 ms.
        public int MaxConnections { get; set; } = 1;
        public IPlugIns? PlugIns { get; set; }
        public Encoding Encoding { get; set; } = Encoding.ASCII;
        public bool Simple { get; set; } = false;   // если Simple = false, то разбираем входящие данные запроса по протоколу HTTP
        public string Tls { get; set; } = string.Empty;
        public bool listen { get; private set; } = false;
        public int observation { get; set; } = 0;
        private X509Certificate2? Certificate { get; set; }
        private Dictionary<IPAddress, IOClient> Connections { get; set; }
        public Actions? Extensions { get; set; }
        public Func<IORequest?, IOConnection?, IOResponse?>? Authorization { get; private set; }
        public IOSender? LogSender { get; private set; }
        public IOSender? MonSender { get; private set; }
        private TcpListener Listener { get; set; }
        private CancellationTokenSource _cts = new CancellationTokenSource();
        public TCP()
        {
            Id = Guid.NewGuid();
            Connections = new Dictionary<IPAddress, IOClient>();
            Address = IPAddress.Any;
            Listener = new TcpListener(Address, Port);
        }
        public TCP(string ip, int port)
        {
            Id = Guid.NewGuid();
            Connections = new Dictionary<IPAddress, IOClient>();
            try
            {
                Address = IPAddress.Parse(ip);
                Port = port;
                Listener = new TcpListener(Address, Port);
            }
            catch { throw; }
        }
        public TCP(IOConfig Config, bool front = true)
        {
            char separator = Path.DirectorySeparatorChar;
            string absolutepathDef = $"{separator}{separator}";
            IsFront = front;
            Id = Guid.NewGuid();
            Connections = new Dictionary<IPAddress, IOClient>();
            Address = IPAddress.None;
            try
            {
                if (IsFront) { IConfigs.FrontConfig = Config; }
                else { IConfigs.BackConfig = Config; }
                PropertyInfo[] PropsCfg = Config.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
                List<PropertyInfo> ThisProps = GetType().GetProperties().ToList();
                foreach (PropertyInfo property in PropsCfg)
                {
                    PropertyInfo? ThisProperty = ThisProps.Find(x => x.Name.ToLower() == property.Name.ToLower());
                    if (ThisProperty == null) { continue; }
                    object? CfgValue = property.GetValue(Config, null);
                    if (CfgValue == null) { continue; }
                    if (property.Name.ToLower() == "address" && CfgValue.GetType() == typeof(string) && (string)CfgValue == "any")
                    {
                        Address = IPAddress.Any;
                        continue;
                    }
                    try { ThisProperty.SetValue(this, CfgValue, null); }
                    catch { }
                }
                if (Address == null || Address == IPAddress.None) { throw new Exception("The service IP address is not defined."); }
                if (Port <= 0) { throw new Exception("The service port is not set."); }
                if (PlugIns != null)
                {
                    int count = PlugIns.Count;
                    IPlugIns NewPlugIns = new IPlugIns();
                    for (int i = 0; i < count; i++)
                    {
                        string plugin = PlugIns[i].Trim();
                        if (string.IsNullOrEmpty(plugin)) { continue; }
                        if (plugin.StartsWith(separator) && !plugin.StartsWith(absolutepathDef))
                        {
                            plugin = plugin.Substring(1);
                            plugin = Path.Combine(Environment.CurrentDirectory, plugin);
                            if (!File.Exists(plugin)) { continue; }
                            NewPlugIns.Add(plugin);
                        }
                        else
                        {
                            if (!File.Exists(plugin)) { continue; }
                            NewPlugIns.Add(plugin);
                        }
                    }
                    if (NewPlugIns.Count == 0) { PlugIns = null; }
                    else
                    {
                        PlugIns = new IPlugIns(NewPlugIns);
                        NewPlugIns.Clear();
                        Extensions = new Actions(PlugIns.ToArray());
                        if (!string.IsNullOrEmpty(Config.Authorization) && Extensions.Proxy.ContainsKey(Config.Authorization))
                        {
                            try
                            {
                                Authorization = Extensions.Proxy.GetExecutor<IORequest, IOConnection, IOResponse>(Config.Authorization);
                            }
                            catch { }
                        }
                    }
                }
                if (Extensions == null)
                {
                    Extensions = new Actions();
                }
                if (!string.IsNullOrEmpty(Tls))
                {
                    string pass = "";
                    Certificate = new X509Certificate2(Tls, pass);
                }
                if (Config.Log != null && Config.Log.Address != null)
                {
                    IPAddress LogAddress = Config.Log.Address;
                    if (LogAddress.Equals(IPAddress.Any)) { LogAddress = IPAddress.Loopback; }
                    LogSender = new IOSender(LogAddress, Config.Log.Port);
                }
                if (Config.Monitor != null && Config.Monitor.Address != null)
                {
                    IPAddress MonAddress = Config.Monitor.Address;
                    if (MonAddress.Equals(IPAddress.Any)) { MonAddress = IPAddress.Loopback; }
                    if (LogSender != null && (!LogSender.EndPoint.Address.Equals(MonAddress) || LogSender.EndPoint.Port != Config.Monitor.Port))
                    {
                        MonSender = new IOSender(MonAddress, Config.Monitor.Port);
                    }
                    else
                    {
                        MonSender = LogSender;
                    }
                }
                Listener = new TcpListener(Address, Port);
            }
            catch { throw; }
        }
        public void AddPlugins(IPlugIns Plugins)
        {
            if (Extensions == null) { throw new Exception("The extension object is not initialized."); }
            foreach (string Plugin in Plugins)
            {
                try { Extensions.Add(Plugin); }
                catch { }
            }
        }
        public void Start()
        {
            try
            {
                _cts = new CancellationTokenSource();
                Listener.Start();
                ConfiguredTaskAwaitable ctaListen = Listen().ConfigureAwait(false);

                if (MonSender != null)
                {
                    IOMonitorIetm MonItem = new IOMonitorIetm(Id, new IPEndPoint(Address, Port), "Start Instance");
                    MonItem.Data = Array.Empty<byte>();
                    MonSender.Send(MonItem.ToBytes());
                }

                ConfiguredTaskAwaitable? ctaObservation = null;
                if (observation > 0)
                {
                    ctaObservation = Observation().ConfigureAwait(false);

                    if (MonSender != null)
                    {
                        IOMonitorIetm MonItem = new IOMonitorIetm(Id, new IPEndPoint(Address, Port), "Start Observation");
                        MonItem.Data = Array.Empty<byte>();
                        MonSender.Send(MonItem.ToBytes());
                    }
                }
            }
            catch (Exception)
            {
                throw;
            }
        }
        public void Stop()
        {
            observation = -1;
            listen = false;
            lock (_locker)
            {
                _cts.Cancel();
                foreach (IOClient Client in Connections.Values)
                {
                    foreach (IOConnection Connection in Client.Connections)
                    {
                        Connection.Dispose();
                    }
                    Client.Connections.Clear();
                }
                Connections.Clear();
            }
            Listener.Stop();

            if (MonSender != null)
            {
                IOMonitorIetm MonItem = new IOMonitorIetm(Id, new IPEndPoint(Address!, Port), "Stop Instance");
                MonItem.Data = Array.Empty<byte>();
                MonSender.Send(MonItem.ToBytes());
            }
        }
        private async Task Listen()
        {
            await Task.Yield();
            listen = true;
            while (listen)
            {
                try
                {
                    TcpClient client = await Listener.AcceptTcpClientAsync(_cts.Token);
                    int Code = CheckAndRegister(client, out IOConnection? Connection);
                    if (Code != 1 || Connection == null)
                    {
                        if (Connection == null) { continue; }
                        Task TRejection = Reject(Connection, Code);

                        if (LogSender != null)
                        {
                            IOMonitorIetm LogItem = new IOMonitorIetm(Connection.Id, Connection.EndPoint, $"Reject Connection Code {Code}");
                            LogItem.Data = Array.Empty<byte>();
                            LogSender.Send(LogItem.ToBytes());
                        }

                        continue;
                    }
                    Task TConnection = Connection.ProcessConnection();
                }
                catch (Exception) { }
            }
        }
        private int CheckAndRegister(TcpClient client, out IOConnection? Connection)
        {
            Connection = null;
            int Code = 0;
            try
            {
                if (client.Client.RemoteEndPoint == null) { return 4; }
                IPAddress Address = (client.Client.RemoteEndPoint as IPEndPoint)!.Address;
                lock (_locker)
                {
                    bool register = false;
                    if (MaxConnections <= 0)
                    {
                        register = true;
                    }
                    else
                    {
                        if (Connections.ContainsKey(Address))
                        {
                            if (Connections[Address].Connections.Count < MaxConnections) { register = true; }
                            else { Code = 2; }
                        }
                        else { register = true; }
                    }
                    if (Certificate != null) { Connection = new IOConnection(client, Certificate); }
                    else { Connection = new IOConnection(client); }
                    Connection.ServiceId = Id;
                    Connection.BufferSize = Buffer;
                    Connection.Simple = Simple;
                    Connection.Authorization = Authorization;
                    if (register)
                    {
                        Code = 1;
                        if (Connections.ContainsKey(Address)) { Connections[Address].Connections.Add(Connection); }
                        else { Connections.Add(Address, new IOClient(Connection)); }
                        return Code;
                    }
                }
            }
            catch { Code = 5; }
            return Code;
        }
        private async Task Reject(IOConnection Connection, int code)
        {
            await Task.Yield();
            /* Возвращаем отказ в подключении,
             * с указанием причины и кода ошибки,
             * после чего принудительно закрываем подключение */
            // code = 1 => ip:port успешно зарегестрирован
            // code = 2 => достигнут максимальный предел подключений с одного IP-адреса
            // code = 3 => ip:port уже ранее зарегестрирован
            // code = 4 => регистрация не допустима, так как ip:port не определен
            string message = "The maximum number of connections from a single IP address has been reached.";
            int HttpCode = 429;
            if (code > 2)
            {
                message = "IP:Port has already been registered.";
                HttpCode = 400;
                if (code == 4)
                {
                    message = "Registration is not allowed because IP:Port is not defined.";
                    HttpCode = 500;
                }
            }
            List<byte> Sent = new List<byte>();
            if (!Simple)
            {
                int Length = Encoding.ASCII.GetBytes(message).Length;
                string responce = $"{HttpCode} Forbidden";
                string headers = $"Content-Length: {Length}\r\n";
                headers += $"Content-Type: text/html\r\n";
                responce = $"HTTP/1.1 {responce}\r\n{headers}\r\n{message}";
                Sent.AddRange(Encoding.ASCII.GetBytes(responce));
            }
            else
            {
                string responce = $"{code} {message}";
                byte[] body = Encoding.ASCII.GetBytes(message);
                Sent.AddRange(BitConverter.GetBytes(body.Length));
                Sent.AddRange(body);
            }
            try { Connection.Socket.Send(Sent.ToArray()); }
            catch { }

            Sent.Clear();
            Connection.Dispose();
        }
        private async Task Observation()
        {
            await Task.Yield();
            while (observation > 0)
            {
                await Task.Delay(1000);
                List<IPAddress> Unregistered = new List<IPAddress>();
                foreach (IOClient Client in Connections.Values)
                {
                    if (Client.Connections.Count == 0)
                    {
                        Unregistered.Add(Client.Address);
                        continue;
                    }
                    List<IOConnection> Disconnected = new List<IOConnection>();
                    foreach (IOConnection Connection in Client.Connections)
                    {
                        if (Connection.Connected) { continue; }

                        Disconnected.Add(Connection);
                    }
                    foreach (IOConnection Connection in Disconnected)
                    {
                        Connection.Dispose();
                        Client.Connections.Remove(Connection);
                    }
                    Disconnected.Clear();
                    if (Client.Connections.Count == 0)
                    {
                        Unregistered.Add(Client.Address);
                    }
                }
                if (Unregistered.Count > 0)
                {
                    lock (_locker)
                    {
                        foreach (IPAddress Address in Unregistered)
                        {
                            Connections.Remove(Address);
                        }
                        Unregistered.Clear();
                    }
                }
            }
        }
        public void DropConnection(IPAddress Address)
        {
            if (Connections.ContainsKey(Address))
            {
                lock (_locker)
                {
                    IOClient Client = Connections[Address];
                    foreach (IOConnection Connection in Client.Connections)
                    {
                        Connection.Dispose();
                        Connections[Address].Connections.Remove(Connection);
                    }
                    Connections.Remove(Address);
                }
            }
        }
        public void DropConnection(IPEndPoint EndPoint)
        {
            IPAddress Address = EndPoint.Address;
            lock (_locker)
            {
                if (Connections.ContainsKey(Address))
                {
                    IOConnection? Connection = Connections[Address].Connections.Find(x => x.EndPoint == EndPoint);
                    if (Connection != null)
                    {
                        Connection.Dispose();
                        Connections[Address].Connections.Remove(Connection);
                        if (Connections[Address].Connections.Count == 0)
                        {
                            Connections.Remove(Address);
                        }
                    }
                }
            }
        }
        public string Info(bool console, ConsoleColor Color = ConsoleColor.Yellow)
        {
            Task.Delay(1000).Wait();
            string result = string.Empty;
            try
            {
                List<string> Nfo = new List<string>();
                foreach (PropertyInfo property in GetType().GetProperties())
                {
                    object? Value = property.GetValue(this, null);
                    if (Value == null) { Nfo.Add($"{property.Name}: null"); }
                    else if (Value.GetType() == typeof(string) && string.IsNullOrEmpty((string)Value)) { Nfo.Add($"{property.Name}: null"); }
                    else if (Value.GetType() == typeof(IPlugIns))
                    {
                        IPlugIns PlugIns = (IPlugIns)Value;
                        if (PlugIns.Count == 0) { Nfo.Add($"{property.Name}: []"); }
                        else if (PlugIns.Count == 1) { Nfo.Add($"{property.Name}: [{PlugIns[0]}]"); }
                        else
                        {
                            Nfo.Add($"{property.Name}:");
                            foreach (string plugin in PlugIns)
                            {
                                Nfo.Add($"\t{plugin}");
                            }
                        }
                    }
                    else { Nfo.Add($"{property.Name}: {Value}"); }
                }
                if (Extensions != null)
                {
                    Nfo.Add(Extensions.Info(false));
                }
                if (Connections.Count == 0)
                {
                    Nfo.Add($"Connections: 0");
                }
                else
                {
                    Nfo.Add($"Connections:");
                    foreach (IOClient Client in Connections.Values)
                    {
                        foreach (IOConnection Connection in Client.Connections)
                        {
                            bool connected = Connection.CheckConnection();
                            string status = "disconnected";
                            if (connected) { status = "connected"; }
                            Nfo.Add($"\t{Connection.EndPoint} (Connected: {Connection.Connected}) {status}");
                        }
                    }
                }
                Console.ForegroundColor = Color;
                result = string.Join("\n", Nfo);
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                result = e.ToString();
            }
            if (console)
            {
                Console.WriteLine($"{result}");
            }
            Console.ResetColor();
            return result;
        }
    }

    public class UDP
    {
    }
}
