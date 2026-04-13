using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Globe;
using IOBinding;
using IOObjects;
using Tools;

namespace IOProcessing
{
    public class IOClient
    {
        public IPAddress Address { get; set; }
        public List<IOConnection> Connections { get; set; }
        public IOClient(IOConnection Connection)
        {
            Address = Connection.EndPoint.Address;
            Connections = new List<IOConnection>();
            Connections.Add(Connection);
        }
    }
    public class IOConnection : IDisposable
    {
        public Guid Id { get; private set; }
        public Guid ServiceId { get; set; }
        public Socket Socket { get; private set; }
        public Stream Stream { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        public int BufferSize { get; set; } = 4096;
        private CancellationTokenSource cts { get; set; }
        public bool Simple { get; set; } = false;   // если Simple = false, то разбираем входящие данные запроса по протоколу HTTP -> определено в IOService
        public string ServiceName { get; private set; } = string.Empty;
        public string ServiceToken { get; private set; } = string.Empty;
        public bool Connected { get; private set; } = false;
        public bool authorized { get; private set; } = false;
        public string user { get; private set; } = string.Empty;
        private bool runned { get; set; } = false;
        public Func<IORequest?, IOConnection?, IOResponse?>? Authorization { get; set; }
        public Action<byte[], IORequest>? DoCustomReceiving { get; set; }
        public IOConnection(TcpClient client)
        {
            Id = Guid.NewGuid();
            Socket = client.Client;
            try
            {
                Stream = client.GetStream();
            }
            catch { throw; }
            if (Socket.RemoteEndPoint == null) { throw new Exception("The remote client connection point is not defined."); }
            EndPoint = (Socket.RemoteEndPoint as IPEndPoint)!;
            cts = new CancellationTokenSource();
        }
        public IOConnection(TcpClient client, X509Certificate2 Certificate)
        {
            Id = Guid.NewGuid();
            Socket = client.Client;
            try
            {
                Stream = new SslStream(client.GetStream(), false);
                (Stream as SslStream)!.AuthenticateAsServer(Certificate!, clientCertificateRequired: false, System.Security.Authentication.SslProtocols.Tls13 | System.Security.Authentication.SslProtocols.Tls12, checkCertificateRevocation: true);
            }
            catch { throw; }
            if (client.Client.RemoteEndPoint == null) { throw new Exception(); }
            EndPoint = (client.Client.RemoteEndPoint as IPEndPoint)!;
            cts = new CancellationTokenSource();
        }
        public bool CheckConnection()
        {
            string stack = Environment.StackTrace;
            if (Socket == null) { return false; }
            bool connected = true;
            try
            {
                if (Socket.Poll(0, SelectMode.SelectRead))
                {
                    byte[] buff = new byte[1];
                    if (Socket.Receive(buff, SocketFlags.Peek) == 0) { connected = false; }
                    buff = Array.Empty<byte>();
                }
            }
            catch (Exception e)
            {
                bool isSocketException = e.GetType() == typeof(SocketException);
                bool isObjectDisposedException = e.GetType() == typeof(ObjectDisposedException);
                if (isSocketException || isObjectDisposedException) { connected = false; }
            }
            return connected;
        }
        public async Task ProcessConnection()
        {
            if (runned) { throw new Exception("The process of receiving data from the stream is already running in a different context."); }
            runned = true;
            Connected = true;
            await Task.Yield();
            byte[] buffer = new byte[BufferSize];
            try
            {
                int chanks = 0, full = 0;
                int received;
                IORequest Request = new IORequest(this);
                while ((received = await Stream.ReadAsync(buffer, 0, BufferSize, cts.Token)) > 0)
                {
                    full += received;
                    chanks++;
                    if (received < buffer.Length) { Array.Resize(ref buffer, received); }
                    if (IOGlobe.Pools != null)
                    {
                        CollectDataForPools(Request, buffer);
                        if (Request.complite)
                        {
                            Request.Dispose();
                            Request = new IORequest(this);
                        }
                    }
                    else
                    {
                        Request.Receiving(buffer);
                        if (Request.complite)
                        {
                            OnDataComplite(Request);
                            Request.Dispose();
                            Request = new IORequest(this);
                        }
                    }
                    buffer = new byte[BufferSize];
                }
            }
            catch { }
            finally
            {
                Array.Resize(ref buffer, 0);
                runned = false;
                Disconnected();
            }
        }
        private void CollectDataForPools(IORequest Request, byte[] buffer)
        {
            if (Request.targetcomplite || Request.headercomplite || Request.complite)
            {
                if (!Request.poolsent) { SendDataToPool(Request); }
                else { SendDataToPool(Request, buffer); }
                return;
            }
            Request.Receiving(buffer);
        }
        private void SendDataToPool(IORequest Request)
        {
            byte[] Data = Request.ToBytes();
        }
        private void SendDataToPool(IORequest Request, byte[] buffer)
        {
            byte[] Data = Request.ToBytes(buffer);
        }
        private void OnDataComplite(IORequest Request)
        {
            /* обрабатываем когда все данные были получены */

            Request.TimeReceived = DateTime.UtcNow.Ticks;
            Request.ParsingTimes = Request.TimeReceived - Request.ParsingTimes;
            IOResponse? Response = null;
            if (!authorized && Authorization != null)
            {
                if (Request.AuthInitTime == 0) { Request.AuthInitTime = DateTime.UtcNow.Ticks; }
                try
                {
                    Response = Authorization(Request, this);
                    Request.AuthOutTime = DateTime.UtcNow.Ticks;
                    if (Response == null)
                    {
                        IOObjects.IOException e = new IOObjects.IOException("The request was not authorized due to an internal authorization error that returned a null result.", 400);
                        BuildExceptionResponce(e, Request, out Response);
                    }
                }
                catch (Exception e)
                {
                    IOObjects.IOException ioe = new IOObjects.IOException(e, 403);
                    BuildExceptionResponce(ioe, Request, out Response);
                    AddCors(Request, Response);
                    SendResponce(Response);
                    return;
                }
                if (!authorized)
                {
                    AddCors(Request, Response);
                    SendResponce(Response);
                    return;
                }
            }
            Request.Authorized = user;
            if (Globe.IOGlobe.Proxy == null)
            {
                IOObjects.IOException e = new IOObjects.IOException("The reference to the global attached method list is not defined in the current context.", 500);
                BuildExceptionResponce(e, Request, out Response);
                AddCors(Request, Response);
                SendResponce(Response);
                return;
            }
            if (string.IsNullOrEmpty(Request.HttpRequest))
            {
                IOObjects.IOException e = new IOObjects.IOException("The purpose of the request is not defined.", 400);
                BuildExceptionResponce(e, Request, out Response);
                AddCors(Request, Response);
                SendResponce(Response);
                return;
            }

            /* выполняем связанный с IORequest.HttpRequest метод, который должен вернуть IOSResponse */
            /* выполняем SendResponce(IOSResponse) */
            IOBinder.IOMethod? MethodDefinition = null;
            try
            {
                if (Globe.IOGlobe.Bindings == null)
                {
                    IOObjects.IOException e = new IOObjects.IOException("Method bindings are not defined in the service context.", 500);
                    BuildExceptionResponce(e, Request, out Response);
                    AddCors(Request, Response);
                    SendResponce(Response);
                    return;
                }
                MethodDefinition = Globe.IOGlobe.Bindings.Find(Request.HttpRequest);
            }
            catch (Exception e)
            {
                IOObjects.IOException ioe = new IOObjects.IOException(e, 404);
                BuildExceptionResponce(ioe, Request, out Response);
                AddCors(Request, Response);
                SendResponce(Response);
                return;
            }
            if (MethodDefinition == null || MethodDefinition.Delegate == null)
            {
                IOObjects.IOException e = new IOObjects.IOException($"The requested resource \"{Request.HttpRequest}\" was not found. The binding method was not found.", 404);
                BuildExceptionResponce(e, Request, out Response);
                AddCors(Request, Response);
                SendResponce(Response);
                return;
            }
            IOSRequest SRequest = BuildSimpleRequestFromBase(Request);
            SRequest.Config = MethodDefinition.Config;
            IOSResponse? SResponce = null;
            try
            {
                SResponce = MethodDefinition.Delegate(SRequest);
                if (SResponce == null)
                {
                    IOObjects.IOException e = new IOObjects.IOException("The requested method {SomeMethod} returned a null result.", 400);
                    BuildExceptionResponce(e, Request, out Response);
                    AddCors(Request, Response);
                    SendResponce(Response);
                    return;
                }
                if (!Simple && SResponce.HttpCode == 0) { SResponce.HttpCode = 200; }
            }
            catch (Exception e)
            {
                IOObjects.IOException ioe = new IOObjects.IOException(e, 400);
                BuildExceptionResponce(ioe, Request, out Response);
                AddCors(Request, Response);
                SendResponce(Response);
                return;
            }

            AddCors(Request, SResponce);
            SendResponce(SResponce);
        }
        private IOSRequest BuildSimpleRequestFromBase(IORequest Request)
        {
            IOSRequest SRequest = new IOSRequest();
            SRequest.Id = Request.Id;
            SRequest.ConnectionId = Request.ConnectionId;
            SRequest.EndPoint = Request.EndPoint;
            SRequest.Simple = Request.Simple;
            SRequest.Length = Request.Length;
            SRequest.HttpMethod = Request.HttpMethod;
            SRequest.HttpProtocol = Request.HttpProtocol;
            SRequest.HttpRequest = Request.HttpRequest;
            SRequest.HttpParams = Request.HttpParams;
            SRequest.HttpHeaders = Request.HttpHeaders;
            SRequest.Data = Request.Data;
            SRequest.targetcomplite = Request.targetcomplite;
            SRequest.headercomplite = Request.headercomplite;
            SRequest.complite = Request.complite;
            SRequest.Authorized = Request.Authorized;
            return SRequest;
        }
        public void Authorized(string user)
        {
            this.user = user;
            authorized = true;
        }
        private void BuildExceptionResponce(IOObjects.IOException e, IORequest Request, out IOResponse Response)
        {
            Response = new IOResponse(this);
            Response.CodePage = Encoding.UTF8;
            string InnerException = string.Empty;
            if (e.InnerException != null)
            {
                InnerException = "<br>" + e.InnerException.ToString().Replace("\n", "<br>");
            }
            byte[] msg = Response.CodePage.GetBytes($"<span style=\"color: red;\">{e.ToString().Replace("\n", "<br>")}{InnerException}</span>");
            if (Simple)
            {
                Response.Data.AddRange(BitConverter.GetBytes(msg.Length + 4));
                Response.Data.AddRange(BitConverter.GetBytes(400));
                Response.Data.AddRange(msg);
                return;
            }
            Response.HttpProtocol = Request.HttpProtocol;
            Response.HttpCode = 400;
            if (e.Code > 0)
            {
                Response.HttpCode = e.Code;
            }
            Response.Data.AddRange(msg);
        }
        private void AddCors(IORequest Request, IOResponse Response)
        {
            if (Request.HttpHeaders == null || Request.HttpHeaders.Count == 0) { return; }
            if (Response.HttpHeaders == null || Response.HttpHeaders.Count == 0) { return; }
            KeyValuePair<string, string>? Origin = Request.HttpHeaders!.Find(x => x.Key.ToLower() == "origin");
            string origin = string.Empty;
            if (Origin != null && !string.IsNullOrEmpty(Origin.Value.Value))
            {
                origin = Origin.Value.Value.Trim();
            }
            if (string.IsNullOrEmpty(origin)) { return; }
            Response.HttpHeaders!.Add("Access-Control-Allow-Credentials", "true", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Headers", "Origin, Content-Type", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS, HEAD", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Origin", origin, false);
        }
        private void AddCors(IORequest Request, IOSResponse Response)
        {
            if (Request.HttpHeaders == null || Request.HttpHeaders.Count == 0) { return; }
            if (Response.HttpHeaders == null || Response.HttpHeaders.Count == 0) { return; }
            KeyValuePair<string, string>? Origin = Request.HttpHeaders!.Find(x => x.Key.ToLower() == "origin");
            string origin = string.Empty;
            if (Origin != null && !string.IsNullOrEmpty(Origin.Value.Value))
            {
                origin = Origin.Value.Value.Trim();
            }
            if (string.IsNullOrEmpty(origin)) { return; }
            Response.HttpHeaders!.Add("Access-Control-Allow-Credentials", "true", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Headers", "Origin, Content-Type", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Methods", "POST, GET, OPTIONS, HEAD", false);
            Response.HttpHeaders!.Add("Access-Control-Allow-Origin", origin, false);
        }
        private void SendResponce(IOResponse Response)
        {
            if (Response.CodePage == null) { Response.CodePage = Encoding.UTF8; }
            if (Response.RowSeparator == null) { Response.RowSeparator = new byte[] { (byte)'\r', (byte)'\n' }; }
            string Message = string.Empty;
            int Length = 0;
            if (Simple)
            {
                List<byte> Headers = new List<byte>();
                if (Response.Data == null) { Response.Data = new List<byte>(); }
                if (!string.IsNullOrEmpty(Response.HttpProtocol) || Response.HttpCode >= 0 || !string.IsNullOrEmpty(Response.HttpMessage))
                {
                    if (!string.IsNullOrEmpty(Response.HttpProtocol)) { Message += Response.HttpProtocol.Trim() + " "; }
                    if (Response.HttpCode >= 0) { Message += Response.HttpCode.ToString() + " "; }
                    if (!string.IsNullOrEmpty(Response.HttpMessage)) { Message += Response.HttpMessage.Trim(); }
                    if (!string.IsNullOrEmpty(Message.Trim()))
                    {
                        Headers.AddRange(Response.CodePage.GetBytes(Message.Trim()));
                        Message = string.Empty;
                    }
                }
                if (Response.HttpHeaders != null && Response.HttpHeaders.Count > 0)
                {
                    if (Headers.Count > 0) { Headers.AddRange(Response.RowSeparator); }
                    foreach (var HttpHeader in Response.HttpHeaders)
                    {
                        string Item = $"{HttpHeader.Key}: {HttpHeader.Value}";
                        Headers.AddRange(Response.CodePage.GetBytes(Item.Trim()));
                        Headers.AddRange(Response.RowSeparator);
                    }
                }
                if (Response.Data.Count > 4)
                {
                    Length = BitConverter.ToInt32(Response.Data.GetRange(0, 4).ToArray());
                    Response.Data.RemoveRange(0, 4);
                }
                if (Headers.Count > 0)
                {
                    int CompliteLength = Length + Headers.Count;
                    if (Response.RowSeparator == null) { Response.RowSeparator = new byte[] { (byte)'\r', (byte)'\n' }; }
                    Headers.AddRange(Response.RowSeparator);
                    List<byte> Complite = new List<byte>();
                    Complite.AddRange(BitConverter.GetBytes(CompliteLength));
                    Complite.AddRange(Headers);
                    Complite.AddRange(Response.Data);
                    if (CompliteLength == (Complite.Count - 4))
                    {
                        Response.Data.Clear();
                        Response.Data.AddRange(Complite);
                        Headers.Clear();
                        Complite.Clear();
                    }
                    else if (Length > 0)
                    {
                        Complite = new List<byte>();
                        Complite.AddRange(BitConverter.GetBytes(Length));
                        Complite.AddRange(Response.Data);
                        Response.Data.Clear();
                        Response.Data.AddRange(Complite);
                        Headers.Clear();
                        Complite.Clear();
                    }
                }
                try { Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count); }
                catch
                {
                    int attempts = 1;
                    bool success = false;
                    while (!success || attempts < 4)
                    {
                        Task.Delay(300).Wait();
                        try
                        {
                            Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count);
                            success = true;
                        }
                        catch { attempts++; }
                    }
                    if (!success)
                    {
                        // логировать ошибку отправки данных
                    }
                }

                if (Response.OnDrop)
                {
                    Disconnected();
                }
                Response.Dispose();
                return;
            }
            Response.OnDrop = false;
            Response.HttpMessage = HttpInfo.ResponseCodes[0];
            if (HttpInfo.ResponseCodes.ContainsKey(Response.HttpCode)) { Response.HttpMessage = HttpInfo.ResponseCodes[Response.HttpCode]; }
            if (Response.HttpCode >= 300 && Response.HttpCode < 1000 && Response.HttpCode != 400 && Response.HttpCode != 401 && Response.HttpCode != 404) { Response.OnDrop = true; }
            Message = $"{Response.HttpProtocol} {Response.HttpCode} {Response.HttpMessage}";
            Length = Response.Data.Count;
            if (Response.HttpHeaders == null) { Response.HttpHeaders = new IOList(); }
            Response.HttpHeaders.Add("Date", DateTime.Now.ToUniversalTime().ToString("r"), false);
            if (!string.IsNullOrEmpty(ServiceName)) { Response.HttpHeaders.Add("Server", ServiceName, false); }
            Response.HttpHeaders.Add("Content-Type", $"text/html; {Response.CodePage.WebName}", false);
            Response.HttpHeaders!.Add("Content-Length", Length.ToString(), false);
            List<byte> Content = new List<byte>();
            Content.AddRange(Response.CodePage.GetBytes(Message));
            Content.AddRange(Response.RowSeparator);
            foreach (var HttpHeader in Response.HttpHeaders)
            {
                string Item = $"{HttpHeader.Key}: {HttpHeader.Value}";
                Content.AddRange(Response.CodePage.GetBytes(Item.Trim()));
                Content.AddRange(Response.RowSeparator);
            }
            Content.AddRange(Response.RowSeparator);

            if (Length > 0) { Content.AddRange(Response.Data); }
            try { Stream.Write(Content.ToArray(), 0, Content.Count); }
            catch
            {
                int attempts = 1;
                bool success = false;
                while (!success || attempts < 4)
                {
                    Task.Delay(300).Wait();
                    try
                    {
                        Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count);
                        success = true;
                    }
                    catch { attempts++; }
                }
                if (!success)
                {
                    // логировать ошибку отправки данных
                }
            }
            if (Response.OnDrop) { Disconnecting(); }
            Response.Dispose();
        }
        private void SendResponce(IOSResponse Response)
        {
            if (Response.CodePage == null) { Response.CodePage = Encoding.UTF8; }
            if (Response.RowSeparator == null) { Response.RowSeparator = new byte[] { (byte)'\r', (byte)'\n' }; }
            string Message = string.Empty;
            int Length = 0;
            if (Simple)
            {
                List<byte> Headers = new List<byte>();
                if (Response.Data == null) { Response.Data = new List<byte>(); }
                if (!string.IsNullOrEmpty(Response.HttpProtocol) || Response.HttpCode >= 0 || !string.IsNullOrEmpty(Response.HttpMessage))
                {
                    if (!string.IsNullOrEmpty(Response.HttpProtocol)) { Message += Response.HttpProtocol.Trim() + " "; }
                    if (Response.HttpCode >= 0) { Message += Response.HttpCode.ToString() + " "; }
                    if (!string.IsNullOrEmpty(Response.HttpMessage)) { Message += Response.HttpMessage.Trim(); }
                    if (!string.IsNullOrEmpty(Message.Trim()))
                    {
                        Headers.AddRange(Response.CodePage.GetBytes(Message.Trim()));
                        Message = string.Empty;
                    }
                }
                if (Response.HttpHeaders != null && Response.HttpHeaders.Count > 0)
                {
                    if (Headers.Count > 0) { Headers.AddRange(Response.RowSeparator); }
                    foreach (var HttpHeader in Response.HttpHeaders)
                    {
                        string Item = $"{HttpHeader.Key}: {HttpHeader.Value}";
                        Headers.AddRange(Response.CodePage.GetBytes(Item.Trim()));
                        Headers.AddRange(Response.RowSeparator);
                    }
                }
                if (Response.Data.Count > 4)
                {
                    Length = BitConverter.ToInt32(Response.Data.GetRange(0, 4).ToArray());
                    Response.Data.RemoveRange(0, 4);
                }
                if (Headers.Count > 0)
                {
                    int CompliteLength = Length + Headers.Count;
                    if (Response.RowSeparator == null) { Response.RowSeparator = new byte[] { (byte)'\r', (byte)'\n' }; }
                    Headers.AddRange(Response.RowSeparator);
                    List<byte> Complite = new List<byte>();
                    Complite.AddRange(BitConverter.GetBytes(CompliteLength));
                    Complite.AddRange(Headers);
                    Complite.AddRange(Response.Data);
                    if (CompliteLength == (Complite.Count - 4))
                    {
                        Response.Data.Clear();
                        Response.Data.AddRange(Complite);
                        Headers.Clear();
                        Complite.Clear();
                    }
                    else if (Length > 0)
                    {
                        Complite = new List<byte>();
                        Complite.AddRange(BitConverter.GetBytes(Length));
                        Complite.AddRange(Response.Data);
                        Response.Data.Clear();
                        Response.Data.AddRange(Complite);
                        Headers.Clear();
                        Complite.Clear();
                    }
                }
                try { Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count); }
                catch
                {
                    int attempts = 1;
                    bool success = false;
                    while (!success || attempts < 4)
                    {
                        Task.Delay(300).Wait();
                        try
                        {
                            Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count);
                            success = true;
                        }
                        catch { attempts++; }
                    }
                    if (!success)
                    {
                        // логировать ошибку отправки данных
                    }
                }

                if (Response.OnDrop)
                {
                    Disconnected();
                }
                Response.Dispose();
                return;
            }

            Response.OnDrop = false;
            Response.HttpMessage = HttpInfo.ResponseCodes[0];
            if (HttpInfo.ResponseCodes.ContainsKey(Response.HttpCode)) { Response.HttpMessage = HttpInfo.ResponseCodes[Response.HttpCode]; }
            if (Response.HttpCode >= 300 && Response.HttpCode < 1000 && Response.HttpCode != 400 && Response.HttpCode != 401 && Response.HttpCode != 404) { Response.OnDrop = true; }
            Message = $"{Response.HttpProtocol} {Response.HttpCode} {Response.HttpMessage}";
            Length = Response.Data.Count;
            if (Response.HttpHeaders == null) { Response.HttpHeaders = new IOList(); }
            Response.HttpHeaders.Add("Date", DateTime.Now.ToUniversalTime().ToString("r"), false);
            if (!string.IsNullOrEmpty(ServiceName)) { Response.HttpHeaders.Add("Server", ServiceName, false); }
            Response.HttpHeaders.Add("Content-Type", $"text/html; {Response.CodePage.WebName}", false);
            Response.HttpHeaders!.Add("Content-Length", Length.ToString(), false);
            List<byte> Content = new List<byte>();
            Content.AddRange(Response.CodePage.GetBytes(Message));
            Content.AddRange(Response.RowSeparator);
            foreach (var HttpHeader in Response.HttpHeaders)
            {
                string Item = $"{HttpHeader.Key}: {HttpHeader.Value}";
                Content.AddRange(Response.CodePage.GetBytes(Item.Trim()));
                Content.AddRange(Response.RowSeparator);
            }
            Content.AddRange(Response.RowSeparator);

            if (Length > 0) { Content.AddRange(Response.Data); }
            try { Stream.Write(Content.ToArray(), 0, Content.Count); }
            catch
            {
                int attempts = 1;
                bool success = false;
                while (!success || attempts < 4)
                {
                    Task.Delay(300).Wait();
                    try
                    {
                        Stream.Write(Response.Data.ToArray(), 0, Response.Data.Count);
                        success = true;
                    }
                    catch { attempts++; }
                }
                if (!success)
                {
                    // логировать ошибку отправки данных
                }
            }
            if (Response.OnDrop) { Disconnecting(); }
            Response.Dispose();
        }
        private void Disconnecting()
        {
            try
            {
                Socket.Shutdown(SocketShutdown.Both);
                Socket.Close();
            }
            catch
            {
                if (Stream.CanWrite) { Stream.Close(); }
            }
            finally
            {
                Disconnected();
                Stream.Dispose();
                Socket.Dispose();
            }
        }
        private void Disconnected()
        {
            Connected = false;
        }
        public void Dispose()
        {
            Disconnecting();
        }
    }
    public class IORequest : IDisposable
    {
        public Guid Id { get; private set; }
        public Guid ConnectionId { get; private set; }
        public IPEndPoint EndPoint { get; private set; }
        private List<byte> Template { get; set; }
        public bool complite { get; private set; } = false;
        public bool headercomplite { get; private set; } = false;
        public bool targetcomplite { get; private set; } = false;
        public bool poolsent { get; private set; } = false; // признак отправки объекта IORequest в пул каналов
        public bool Simple { get; set; } = false;           // если Simple == false, то разбираем входящие данные запроса по протоколу HTTP -> определено в IOConnection -> IOService
        public int Length { get; set; } = -1;
        public string? HttpMethod { get; set; }
        public string? HttpProtocol { get; set; }
        public string? HttpRequest { get; set; }
        public IOList? HttpParams { get; set; }
        public IOList? HttpHeaders { get; set; }
        public List<byte> Data { get; set; }
        public long TimeReceiving { get; set; } = 0;
        public long TimeReceived { get; set; } = 0;
        public long ParsingTimes { get; set; } = 0;
        public long AuthInitTime { get; set; } = 0;
        public long AuthOutTime { get; set; } = 0;
        public long AuthTimes { get; set; } = 0;
        public bool TokenSet { get; set; } = false;
        public string? Authorized { get; set; }
        public string Token { get; set; } = string.Empty;
        public Action<byte[], IORequest>? DoCustomReceiving { get; set; }
        public IORequest()
        {
            Id = Guid.NewGuid();
            Template = new List<byte>();
            Data = new List<byte>();
            Simple = true;
            EndPoint = new IPEndPoint(IPAddress.None, 0);
        }
        public IORequest(IOConnection Connection)
        {
            Id = Guid.NewGuid();
            ConnectionId = Connection.Id;
            EndPoint = Connection.EndPoint;
            Template = new List<byte>();
            Data = new List<byte>();
            Simple = Connection.Simple;
            if (!Simple)
            {
                HttpHeaders = new IOList();
            }
        }
        internal void Receiving(byte[] buffer)
        {
            if (ParsingTimes == 0)
            {
                ParsingTimes = DateTime.UtcNow.Ticks;
                TimeReceiving = ParsingTimes;
            }
            if (DoCustomReceiving != null)
            {
                // если объявлен пользовательский метод разбора входящих данных
                DoCustomReceiving(buffer, this);
                return;
            }
            if (Simple)
            {
                if (Length < 0)
                {
                    Template.AddRange(buffer);
                    if (Template.Count < 4) { return; }
                    Length = BitConverter.ToInt32(Template.GetRange(0, 4).ToArray());
                    Template.RemoveRange(0, 4);
                    Data.AddRange(Template);
                    if (Data.Count == Length) { complite = true; }
                    return;
                }
                Data.AddRange(buffer);
                if (Data.Count == Length)
                {
                    complite = true;
                    return;
                }
                return;
            }
            if (!headercomplite)
            {
                if (buffer.Length == 0) { return; }
                Template.AddRange(buffer);
                try { ParseHttpHeaders(); }
                catch (Exception)
                {
                    return;
                }
                if (headercomplite && Length == -1)
                {
                    Length = 0;
                    complite = true;
                }
                return;
            }
            if (Length == -1)
            {
                Length = 0;
                complite = true;
                return;
            }
            Data.AddRange(buffer);
            if (Data.Count == Length)
            {
                complite = true;
            }
        }
        private void ParseHttpHeaders()
        {
            while (Template.Count > 0)
            {
                int indexSeparator = Template.IndexOf((byte)'\r');
                int nextIndexSeparator = Template.IndexOf((byte)'\n');
                if (indexSeparator == -1 || nextIndexSeparator == -1) { break; }
                if ((nextIndexSeparator - indexSeparator) == 1)
                {
                    List<byte> Row = Template.GetRange(0, indexSeparator);
                    string row = Encoding.ASCII.GetString(Row.ToArray()).Trim();
                    Template.RemoveRange(0, nextIndexSeparator + 1);
                    if (string.IsNullOrEmpty(row))
                    {
                        headercomplite = true;
                        break;
                    }
                    if (string.IsNullOrEmpty(HttpRequest))
                    {
                        int index = row.IndexOf(" ");
                        if (index < 0)
                        {
                            HttpRequest = row.Trim();
                            ParseHttpRequest();
                            continue;
                        }
                        HttpMethod = row.Substring(0, index).Trim();
                        int last = row.LastIndexOf(" ");
                        if (last < 0)
                        {
                            HttpRequest = row.Substring(index + 1);
                            ParseHttpRequest();
                            continue;
                        }
                        row = row.Substring(index + 1);
                        last = row.LastIndexOf(" ");
                        HttpRequest = row.Substring(0, last).Trim();
                        HttpProtocol = row.Substring(last + 1).Trim();
                        ParseHttpRequest();
                        continue;
                    }
                    int headerSeparator = row.IndexOf(':');
                    if (headerSeparator < 0) { continue; }
                    string Key = row.Substring(0, headerSeparator).Trim();
                    string Value = row.Substring(headerSeparator + 1).Trim();
                    if (Key.ToLower() == "content-length")
                    {
                        try { Length = int.Parse(Value); }
                        catch { Length = 0; }
                    }
                    HttpHeaders?.Add(new KeyValuePair<string, string>(Key, Value));
                }
            }
            if (headercomplite && Template.Count > 0)
            {
                Data.AddRange(Template);
                Template.Clear();
            }
        }
        private void ParseHttpRequest()
        {
            if (string.IsNullOrEmpty(HttpRequest)) { return; }
            targetcomplite = true;
            int indexRequestSeparator = HttpRequest.IndexOf("?");
            if (indexRequestSeparator < 0) { return; }
            string Params = HttpRequest.Substring(indexRequestSeparator + 1);
            HttpRequest = HttpRequest.Substring(0, indexRequestSeparator);
            if (string.IsNullOrEmpty(Params)) { return; }
            string[] Parts = Params.Split('&');
            if (Parts.Length == 0) { return; }
            HttpParams = new IOList();
            foreach (string Part in Parts)
            {
                string tmp = Part.Trim();
                if (string.IsNullOrEmpty(tmp)) { continue; }
                string Key = tmp;
                string Value = "true";
                int indexSeparator = tmp.IndexOf("=");
                if (indexSeparator > 0)
                {
                    Key = tmp.Substring(0, indexSeparator).Trim();
                    Value = tmp.Substring(indexSeparator + 1).Trim();
                }
                HttpParams.Add(new KeyValuePair<string, string>(Key, Value));
            }
            if (HttpParams.Count == 0) { HttpParams = null; }
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
        public static IORequest FromBytes(byte[] Data)
        {
            IORequest Request = new IORequest();

            return Request;
        }
        public string Info()
        {
            string result = string.Empty;
            List<string> Nfo = new List<string>();
            Nfo.Add($"TimeReceiving: {TimeReceiving} ticks");
            Nfo.Add($"TimeReceived: {TimeReceived} ticks");
            Nfo.Add($"ParsingTimes: {ParsingTimes} ticks");
            Nfo.Add($"AuthInitTime: {AuthInitTime} ticks");
            Nfo.Add($"AuthOutTime: {AuthOutTime} ticks");
            Nfo.Add($"AuthTimes: {AuthTimes} ticks");
            result = string.Join("\n", Nfo);
            return result;
        }
        public void Dispose()
        {
            Template.Clear();
            Data.Clear();
            HttpMethod = string.Empty;
            HttpProtocol = string.Empty;
            HttpRequest = string.Empty;
            HttpHeaders?.Clear();
            HttpParams?.Clear();
            complite = false;
            headercomplite = false;
            Length = -1;
            Simple = false;
            ParsingTimes = 0;
            TimeReceiving = 0;
            TimeReceived = 0;
        }
    }
    public class IOResponse : IDisposable
    {
        public Guid Id { get; private set; }
        public Guid? RequestId { get; private set; }
        public Guid? ConnectionId { get; private set; }
        public IPEndPoint? EndPoint { get; private set; }
        public string? HttpProtocol { get; set; }
        public int HttpCode { get; set; } = 0;
        public string? HttpMessage { get; set; }
        public string? Target { get; set; }
        public IOList? HttpHeaders { get; set; }
        public Encoding? CodePage { get; set; } = Encoding.UTF8;
        public List<byte> Data { get; set; }
        public byte[]? RowSeparator { get; set; } = { (byte)'\r', (byte)'\n' };
        public bool Simple { get; set; } = false;   // если Simple == false, то разбираем входящие данные запроса по протоколу HTTP -> определено в IOConnection -> IOService
        public bool OnDrop { get; set; } = false;

        public IOResponse()
        {
            Id = Guid.NewGuid();
            Data = new List<byte>();
            Simple = true;
        }
        public IOResponse(IOConnection Connection)
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
            OnDrop = false;
        }
    }
}
