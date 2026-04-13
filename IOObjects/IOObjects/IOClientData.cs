using System;
using System.Net;
using System.Text;
using Tools;

namespace IOObjects
{
    public class IOException : Exception
    {
        public int Code { get; set; }
        public new Exception? InnerException { get; private set; }
        public IOException() : base() { }
        public IOException(string Message) : base(Message) { }
        public IOException(Exception e, int Code)
        {
            this.Code = Code;
            InnerException = e;
        }
        public IOException(string Message, int Code) : base(Message)
        {
            this.Code = Code;
        }
        public void BuildExceptionResponce(IOSRequest Request, out IOSResponse Response)
        {
            Response = new IOSResponse(Request);
            Response.CodePage = Encoding.UTF8;
            string InnerException = string.Empty;
            if (InnerException != null)
            {
                InnerException = "<br>" + InnerException.ToString().Replace("\n", "<br>");
            }
            byte[] msg = Response.CodePage.GetBytes($"<span style=\"color: red;\">{ToString().Replace("\n", "<br>")}{InnerException}</span>");
            if (Request.Simple)
            {
                Response.Data.AddRange(BitConverter.GetBytes(msg.Length + 4));
                Response.Data.AddRange(BitConverter.GetBytes(400));
                Response.Data.AddRange(msg);
                return;
            }
            Response.HttpProtocol = Request.HttpProtocol;
            Response.HttpCode = 400;
            if (Code > 0)
            {
                Response.HttpCode = Code;
            }
            Response.Data.AddRange(msg);
        }
    }
    public class IOSConnection : IDisposable
    {
        public Guid Id { get; set; }
        public Guid ServiceId { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public int BufferSize { get; set; }
        public bool Simple { get; set; }
        public string ServiceName { get; set; }
        public string ServiceToken { get; set; }
        public string user { get; set; }
        public IOSConnection()
        {
            Id = Guid.Empty;
            ServiceId = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
            BufferSize = 0;
            Simple = true;
            ServiceName = string.Empty;
            ServiceToken = string.Empty;
            user = string.Empty;
        }
        public void Dispose()
        {
            Id = Guid.Empty;
            ServiceId = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
            BufferSize = 0;
            Simple = true;
            ServiceName = string.Empty;
            ServiceToken = string.Empty;
            user = string.Empty;
        }
    }
    public class IOSRequest : IDisposable
    {
        public Guid Id { get; set; }
        public Guid ConnectionId { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public IOHostConfig? Config { get; set; }
        public bool complite { get; set; } = false;
        public bool headercomplite { get; set; } = false;
        public bool targetcomplite { get; set; } = false;
        public bool Simple { get; set; } = false;           // если Simple == false, то разбираем входящие данные запроса по протоколу HTTP -> определено в IOConnection -> IOService
        public int Length { get; set; } = -1;
        public string? HttpMethod { get; set; }
        public string? HttpProtocol { get; set; }
        public string? HttpRequest { get; set; }
        public IOList? HttpParams { get; set; }
        public IOList? HttpHeaders { get; set; }
        public List<byte> Data { get; set; }
        public string Token { get; set; }
        public string? Authorized { get; set; }
        public IOSRequest()
        {
            Id = Guid.Empty;
            ConnectionId = Guid.Empty;
            Data = new List<byte>();
            Simple = true;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
            Token = string.Empty;
        }
        public IOSRequest(IOSConnection Connection)
        {
            Id = Guid.NewGuid();
            ConnectionId = Connection.Id;
            EndPoint = Connection.EndPoint;
            Data = new List<byte>();
            Simple = Connection.Simple;
            Token = string.Empty;
            if (!Simple)
            {
                HttpHeaders = new IOList();
            }
        }
        public byte[] ToBytes()
        {
            List<byte> Result = new List<byte>();

            List<byte> Params = new List<byte>();
            Params.AddRange(BitConverter.GetBytes(1));
            Params.AddRange(Id.ToByteArray());
            Params.AddRange(ConnectionId.ToByteArray());
            Params.AddRange(EndPoint.Address.GetAddressBytes());
            Params.AddRange(BitConverter.GetBytes(EndPoint.Port));
            Params.AddRange(BitConverter.GetBytes(Simple));
            Params.AddRange(BitConverter.GetBytes(Length));
            Params.AddRange(BitConverter.GetBytes(targetcomplite));
            Params.AddRange(BitConverter.GetBytes(headercomplite));
            Params.AddRange(BitConverter.GetBytes(complite));

            if (!string.IsNullOrEmpty(Token))
            {
                byte[] bytes = Encoding.ASCII.GetBytes($"{Token}");
                Params.AddRange(BitConverter.GetBytes(bytes.Length));
                Params.AddRange(bytes);
            }
            else
            {
                Params.AddRange(BitConverter.GetBytes(0));
            }

            List<byte> Target = new List<byte>();
            if (targetcomplite)
            {
                List<byte> bTarget = new List<byte>();
                List<byte> Method = new List<byte>();
                if (!string.IsNullOrEmpty(HttpMethod))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes($"{HttpMethod}");
                    Method.AddRange(BitConverter.GetBytes(bytes.Length));
                    Method.AddRange(bytes);
                    bTarget.AddRange(Method);
                }
                List<byte> HRequest = new List<byte>();
                if (!string.IsNullOrEmpty(HttpRequest))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes($"{HttpRequest}");
                    HRequest.AddRange(BitConverter.GetBytes(bytes.Length));
                    HRequest.AddRange(bytes);
                    bTarget.AddRange(HRequest);
                }
                List<byte> Protocol = new List<byte>();
                if (!string.IsNullOrEmpty(HttpProtocol))
                {
                    byte[] bytes = Encoding.ASCII.GetBytes($"{HttpProtocol}");
                    Protocol.AddRange(BitConverter.GetBytes(bytes.Length));
                    Protocol.AddRange(bytes);
                    bTarget.AddRange(Protocol);
                }
                Target.AddRange(BitConverter.GetBytes(bTarget.Count));
                Target.AddRange(bTarget);
            }

            List<byte> Headers = new List<byte>();
            if (headercomplite)
            {
                List<byte> bHeaders = new List<byte>();
                IOList _HttpHeaders = HttpHeaders!;
                foreach (var _Header in _HttpHeaders)
                {
                    byte[] bHeader = Encoding.ASCII.GetBytes($"{_Header.Key}:{_Header.Value}");
                    byte[] hLength = BitConverter.GetBytes(bHeader.Length);
                    List<byte> Header = new List<byte>();
                    Header.AddRange(hLength);
                    Header.AddRange(bHeader);
                    bHeaders.AddRange(Header);
                    Header.Clear();
                }
                Headers.AddRange(BitConverter.GetBytes(bHeaders.Count));
                Headers.AddRange(bHeaders);
                bHeaders.Clear();
            }

            List<byte> Content = new List<byte>();
            if (Data.Count > 0)
            {
                Content.AddRange(Data);
            }



            byte[] Bytes = Result.ToArray();
            Result.Clear();
            return Bytes;
        }
        public byte[] ToBytes(byte[] buffer)
        {
            List<byte> Result = new List<byte>();

            List<byte> Params = new List<byte>();
            Params.AddRange(BitConverter.GetBytes(1));
            Params.AddRange(Id.ToByteArray());
            Params.AddRange(ConnectionId.ToByteArray());
            Params.AddRange(EndPoint.Address.GetAddressBytes());
            Params.AddRange(BitConverter.GetBytes(EndPoint.Port));
            Params.AddRange(BitConverter.GetBytes(Simple));
            Params.AddRange(BitConverter.GetBytes(Length));

            List<byte> Content = new List<byte>();
            if (buffer.Length > 0)
            {
                Content.AddRange(buffer);
            }

            byte[] Bytes = Result.ToArray();
            Result.Clear();
            return Bytes;
        }
        public static IOSRequest FromBytes(byte[] Data)
        {
            IOSRequest Request = new IOSRequest();

            return Request;
        }
        public void Dispose()
        {
            Data.Clear();
            HttpMethod = string.Empty;
            HttpProtocol = string.Empty;
            HttpRequest = string.Empty;
            HttpHeaders?.Clear();
            HttpParams?.Clear();
            complite = false;
            headercomplite = false;
            Length = -1;
            Simple = true;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
        }
    }
    public class IOSResponse : IDisposable
    {
        public Guid Id { get; set; }
        public Guid RequestId { get; set; }
        public Guid ConnectionId { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public string? HttpProtocol { get; set; }
        public int HttpCode { get; set; }
        public string? HttpMessage { get; set; }
        public string? Target { get; set; }
        public IOList? HttpHeaders { get; set; }
        public Encoding? CodePage { get; set; }
        public List<byte> Data { get; set; }
        public byte[]? RowSeparator { get; set; }
        public bool Simple { get; set; }
        public bool OnDrop { get; set; }

        public IOSResponse()
        {
            Id = Guid.Empty;
            RequestId = Guid.Empty;
            ConnectionId = Guid.Empty;
            Data = new List<byte>();
            Simple = true;
            OnDrop = false;
            HttpCode = 0;
            CodePage = Encoding.UTF8;
            RowSeparator = new byte[] { (byte)'\r', (byte)'\n' };
            EndPoint = new IPEndPoint(IPAddress.None, 0);
        }
        public IOSResponse(IOSRequest Request)
        {
            Id = Guid.NewGuid();
            RequestId = Request.Id;
            EndPoint = Request.EndPoint;
            Data = new List<byte>();
            Simple = Request.Simple;
            OnDrop = false;
            if (!Simple)
            {
                HttpProtocol = Request.HttpProtocol;
                HttpHeaders = new IOList();
            }
        }
        public IOSResponse(IOSConnection Connection)
        {
            Id = Guid.NewGuid();
            ConnectionId = Connection.Id;
            EndPoint = Connection.EndPoint;
            Data = new List<byte>();
            Simple = Connection.Simple;
            if (!Simple)
            {
                HttpHeaders = new IOList();
            }
        }
        public void Dispose()
        {
            Data.Clear();
            HttpProtocol = string.Empty;
            HttpHeaders?.Clear();
            HttpHeaders = null;
            CodePage = null;
            RowSeparator = null;
            Simple = false;
        }
    }
    public class IOMonitorIetm
    {
        public Guid InstanceId { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public long InitTime { get; private set; }  // метка времени инициализации
        public long RegisterTime { get; set; }      // метка времени регистрации вызова
        public long InvokeTime { get; set; }        // метка времени входа в метод выполнения
        public string Message { get; set; }         // сообщение об исключительной ситуации
        public byte[] Data { get; set; }
        public IOMonitorIetm()
        {
            InitTime = DateTime.UtcNow.Ticks;
            InstanceId = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            Message = string.Empty;
            Data = new byte[0];
        }
        public IOMonitorIetm(string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            InstanceId = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.Loopback, 0);
            this.Message = Message;
            Data = new byte[0];
        }
        public IOMonitorIetm(Guid InstanceId, IPEndPoint EndPoint, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            InstanceId = Guid.Empty;
            this.EndPoint = EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public byte[] ToBytes()
        {
            List<byte> Bytes = new List<byte>();
            byte[] bInstanceId = InstanceId.ToByteArray();
            byte[] bAddress = EndPoint.Address.GetAddressBytes();
            byte[] bPort = BitConverter.GetBytes(EndPoint.Port);
            byte[] bMessage = Encoding.UTF8.GetBytes(Message);
            byte[] bLength = BitConverter.GetBytes(bMessage.Length);
            Bytes.AddRange(bInstanceId);
            Bytes.AddRange(bAddress);
            Bytes.AddRange(bPort);
            Bytes.AddRange(bLength);
            Bytes.AddRange(bMessage);
            Bytes.AddRange(Data);
            byte[] Result = Bytes.ToArray();
            Bytes.Clear();
            return Result;
        }
        public static IOMonitorIetm FromBytes(byte[] bytes)
        {
            List<byte> Bytes = new List<byte>();
            Bytes.AddRange(bytes);
            Guid InstanceId = new Guid(Bytes.GetRange(0, 16).ToArray());
            Bytes.RemoveRange(0, 16);
            IPAddress Address = new IPAddress(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            int Port = BitConverter.ToInt32(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            IPEndPoint EndPoint = new IPEndPoint(Address, Port);
            int Length = BitConverter.ToInt32(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            string Message = Encoding.UTF8.GetString(Bytes.GetRange(0, Length).ToArray());
            Bytes.RemoveRange(0, Length);
            byte[] Data = new byte[0];
            if (Bytes.Count > 0) { Data = Bytes.ToArray(); }
            Bytes.Clear();
            IOMonitorIetm MonitorIetm = new IOMonitorIetm(InstanceId, EndPoint, Message);
            MonitorIetm.Data = Data;
            return MonitorIetm;
        }
    }
    public class IOLogItem
    {
        public Guid Connection { get; set; }
        public Guid Request { get; set; }
        public IPEndPoint EndPoint { get; set; }
        public long InitTime { get; private set; }  // метка времени инициализации
        public long RegisterTime { get; set; }      // метка времени регистрации вызова
        public long InvokeTime { get; set; }        // метка времени входа в метод выполнения
        public long CompliteTime { get; set; }      // метка времени завершения выполнения
        public string Message { get; set; }         // сообщение об исключительной ситуации
        public byte[] Data { get; set; }
        public IOLogItem()
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = Guid.Empty;
            Request = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
            Message = string.Empty;
            Data = new byte[0];
        }
        public IOLogItem(string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = Guid.Empty;
            Request = Guid.Empty;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
            this.Message = Message;
            Data = new byte[0];
        }
        public IOLogItem(IOSConnection ioConnection, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = ioConnection.Id;
            Request = Guid.Empty;
            EndPoint = ioConnection.EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public IOLogItem(Guid ConnectionId, IPEndPoint EndPoint, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = ConnectionId;
            Request = Guid.Empty;
            this.EndPoint = EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public IOLogItem(Guid ConnectionId, Guid RequestId, IPEndPoint EndPoint, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = ConnectionId;
            Request = RequestId;
            this.EndPoint = EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public IOLogItem(IOSRequest ioRequest, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = ioRequest.ConnectionId;
            Request = ioRequest.Id;
            EndPoint = ioRequest.EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public IOLogItem(IOSResponse ioResponce, string Message = "")
        {
            InitTime = DateTime.UtcNow.Ticks;
            CompliteTime = -1;
            Connection = ioResponce.ConnectionId;
            Request = ioResponce.RequestId;
            EndPoint = ioResponce.EndPoint;
            this.Message = Message;
            Data = new byte[0];
        }
        public byte[] ToBytes()
        {
            List<byte> Bytes = new List<byte>();
            byte[] bConnection = Connection.ToByteArray();
            byte[] bRequest = Request.ToByteArray();
            byte[] bAddress = EndPoint.Address.GetAddressBytes();
            byte[] bPort = BitConverter.GetBytes(EndPoint.Port);
            byte[] bMessage = Encoding.UTF8.GetBytes(Message);
            byte[] bLength = BitConverter.GetBytes(bMessage.Length);
            Bytes.AddRange(bConnection);
            Bytes.AddRange(bRequest);
            Bytes.AddRange(bAddress);
            Bytes.AddRange(bPort);
            Bytes.AddRange(bLength);
            Bytes.AddRange(bMessage);
            Bytes.AddRange(Data);

            byte[] Result = Bytes.ToArray();
            Bytes.Clear();
            return Result;
        }
        public static IOLogItem FromBytes(byte[] bytes)
        {
            List<byte> Bytes = new List<byte>();
            Bytes.AddRange(bytes);
            Guid Connection = new Guid(Bytes.GetRange(0, 16).ToArray());
            Bytes.RemoveRange(0, 16);
            Guid Request = new Guid(Bytes.GetRange(0, 16).ToArray());
            Bytes.RemoveRange(0, 16);
            IPAddress Address = new IPAddress(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            int Port = BitConverter.ToInt32(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            IPEndPoint EndPoint = new IPEndPoint(Address, Port);
            int Length = BitConverter.ToInt32(Bytes.GetRange(0, 4).ToArray());
            Bytes.RemoveRange(0, 4);
            string Message = Encoding.UTF8.GetString(Bytes.GetRange(0, Length).ToArray());
            Bytes.RemoveRange(0, Length);
            byte[] Data = new byte[0];
            if (Bytes.Count > 0) { Data = Bytes.ToArray(); }
            Bytes.Clear();
            IOLogItem LogItem = new IOLogItem(Connection, Request, EndPoint, Message);
            LogItem.Data = Data;
            return LogItem;
        }
        public void Complite()
        {
            if (CompliteTime > 0) { return; }
            CompliteTime = DateTime.UtcNow.Ticks;
        }
    }
}
