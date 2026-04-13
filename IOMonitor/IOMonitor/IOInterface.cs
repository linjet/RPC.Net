using System.Net;
using System.Net.Sockets;
using Tools;
using IOObjects;

namespace IOMonitor
{
    public class IOSender
    {
        private readonly object _locker = new object();
        public IPEndPoint EndPoint { get; set; }
        public UdpClient Sender { get; set; }
        public IOSender()
        {
            Sender = new UdpClient();
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
        }
        public IOSender(IPAddress Address, int Port)
        {
            Sender = new UdpClient();
            EndPoint = new IPEndPoint(Address, Port);
        }
        public IOSender(IOLogCfg Config)
        {
            Sender = new UdpClient();
            EndPoint = new IPEndPoint(Config.Address, Config.Port);
        }
        public void Send(byte[] data, IPEndPoint RemoteEndpoint)
        {
            lock (_locker)
            {
                Sender.Send(data, RemoteEndpoint);
            }
        }
        public void Send(byte[] data)
        {
            lock (_locker)
            {
                //Console.WriteLine($"Send {data.Length} bytes");
                Sender.Send(data, EndPoint);
            }
        }
    }
    public class IOServer
    {
        private readonly object _locker = new object();
        public UdpClient Listener { get; set; }
        public int Port { get; set; }
        public bool listen { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public IOServer()
        {
            listen = false;
            Port = 6800;
            Listener = new UdpClient(Port);
            EndPoint = new IPEndPoint(IPAddress.Loopback, Port);
        }
        public IOServer(IOLogCfg Config)
        {
            Port = Config.Port;
            Listener = new UdpClient(Port);
            EndPoint = new IPEndPoint(Config.Address, Config.Port);
        }
        public void Start()
        {
            _ = Listen().ConfigureAwait(false);
        }
        public async Task Listen()
        {
            await Task.Yield();
            listen = true;
            while (listen)
            {
                IPEndPoint RemoteEndpoint = new IPEndPoint(IPAddress.Any, 0);
                byte[] data = Listener.Receive(ref RemoteEndpoint);
                _ = DataProcessing(data, RemoteEndpoint);
            }
        }
        private async Task DataProcessing(byte[] data, IPEndPoint RemoteEndpoint)
        {
            await Task.Yield();
            try
            {
                //IOLogItem LogItem = IOLogItem.FromBytes(data);
                //Console.WriteLine($"Save received data to {EndPoint} {data.Length} bytes");
            }
            catch (Exception)
            {
                //Console.WriteLine($"DataProcessing Exception\n{e}");
            }
        }
        public void Send(byte[] data, IPEndPoint RemoteEndpoint)
        {
            lock (_locker)
            {
                Listener.Send(data, RemoteEndpoint);
            }
        }
    }
}
