using Activator;
using IOObjects;
using Tools;
using IOException = IOObjects.IOException;

namespace Service
{
    public class Config
    {
        public Config() { }
        public Config(ExecProxy Proxy)
        {
            Proxy.Add<IOSRequest?, IOSResponse?>(Get);
        }
        public IOSResponse? Get(IOSRequest? Request)
        {
            if (Request == null) { throw new Exception("The incoming request object is null."); }
            IOSResponse Response = new IOSResponse(Request);
            if (string.IsNullOrEmpty(Request.HttpRequest))
            {
                IOException exception = new IOException("The URL of the incoming request is zero.", 400);
                exception.BuildExceptionResponce(Request, out Response);
                return Response;
            }
            if (Request.Config == null)
            {
                IOException exception = new IOException("The resource configuration in the request object is not defined.", 400);
                exception.BuildExceptionResponce(Request, out Response);
                return Response;
            }
            if (Request.HttpRequest.IndexOf("/server/") > 0 || Request.HttpRequest.IndexOf("/router/") > 0 || Request.HttpRequest.IndexOf("/bindings/") > 0)
            {
                ServiceCFG(Request, Response);
            }
            else if (Request.HttpRequest.IndexOf("/monitor/") > 0)
            {
                MonitorCFG(Request, Response);
            }
            else
            {
                IOException exception = new IOException("The requested configuration does not exist.", 404);
                exception.BuildExceptionResponce(Request, out Response);
                return Response;
            }
            return Response;
        }
        private void ServiceCFG(IOSRequest Request, IOSResponse Response)
        {
            string cfgPath = $"{Environment.CurrentDirectory}";
            cfgPath = Path.Combine(cfgPath, "cfg");
            if (Request.HttpRequest!.IndexOf("/server/") > 0)
            {
                cfgPath = Path.Combine(cfgPath, "config.json");
            }
            else if (Request.HttpRequest.IndexOf("/router/") > 0)
            {
                cfgPath = Path.Combine(cfgPath, "router.json");
            }
            else if (Request.HttpRequest.IndexOf("/bindings/") > 0)
            {
                cfgPath = Path.Combine(cfgPath, "bindings.json");
            }
            if (!File.Exists(cfgPath))
            {
                IOException exception = new IOException($"The requested configuration file (\"{cfgPath}\") could not be found.", 404);
                exception.BuildExceptionResponce(Request, out Response);
                return;
            }
            if (Response.Data == null) { Response.Data = new List<byte>(); }
            Response.Data.AddRange(File.ReadAllBytes(cfgPath));
            Response.HttpHeaders!.Add("content-type", "application/json; charset=UTF-8", false);
        }
        private void MonitorCFG(IOSRequest Request, IOSResponse Response)
        {

        }
    }
}
