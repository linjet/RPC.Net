using Activator;
using IOObjects;
using System.IO;
using System.IO.Compression;
using ZstdNet;
using Tools;
using IOException = IOObjects.IOException;

namespace IOContent
{
    public class MediaItem
    {
        public bool Binary { get; set; } = false;
        public string Name { get; set; } = string.Empty;
    }
    public class HConfig : IOHostConfig
    {
        public List<string>? PcaText { get; set; }
        public List<string>? PcaBinary { get; set; }
        public HConfig() { }
    }
    public class HttpGet
    {
        public static Dictionary<string, MediaItem>? Media = null;
        private List<string> PreferredText = new List<string> { "br", "zstd", "gzip", "deflate" };
        private List<string> PreferredBinary = new List<string> { "zstd", "gzip", "deflate" };
        public static HConfig? Config { get; set; }
        public HttpGet() { }
        public HttpGet(ExecProxy Proxy)
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

            string root = Request.Config.Content;
            string pages = Request.Config.Pages;
            if (Config == null)
            {
                try
                {
                    string config = Path.Combine(root, "cfg.json");
                    if (File.Exists(config)) { Config = JsonReader.Load<HConfig>(config); }
                    else { Config = new HConfig(); }
                }
                catch { Config = new HConfig(); }
            }
            if (Config!.PcaText != null) { PreferredText = Config.PcaText; }
            if (Config!.PcaBinary != null) { PreferredBinary = Config.PcaBinary; }

            if (Media == null)
            {
                try
                {
                    string media = Path.Combine(root, "media.json");
                    if (File.Exists(media)) { Media = JsonReader.Load<Dictionary<string, MediaItem>>(media); }
                    else { Media = new Dictionary<string, MediaItem>(); }
                }
                catch { Media = new Dictionary<string, MediaItem>(); }
            }
            Dictionary<string, MediaItem> _Media = Media!;

            string request = Request.HttpRequest.Replace('/', Path.DirectorySeparatorChar).Substring(1);
            string objectRequest = Path.Combine(root, request);
            List<string> Defaults = Request.Config.Default;
            if (File.Exists(objectRequest))
            {
                GetFile(objectRequest, _Media, Request, Response);
            }
            else if (Directory.Exists(objectRequest))
            {
                objectRequest = GetDefault(objectRequest, Defaults);
                if (string.IsNullOrEmpty(objectRequest))
                {
                    objectRequest = Path.Combine(pages, request);
                    objectRequest = GetDefault(objectRequest, Defaults);
                }
                if (string.IsNullOrEmpty(objectRequest))
                {
                    IOException exception = new IOException($"The requested resource \"{Request.HttpRequest}\" was not found.", 404);
                    exception.BuildExceptionResponce(Request, out Response);
                    return Response;
                }
                GetFile(objectRequest, _Media, Request, Response);
            }
            else
            {
                objectRequest = Path.Combine(pages, request);
                objectRequest = GetDefault(objectRequest, Defaults);
                if (string.IsNullOrEmpty(objectRequest))
                {
                    IOException exception = new IOException($"The requested resource \"{Request.HttpRequest}\" was not found.", 404);
                    exception.BuildExceptionResponce(Request, out Response);
                    return Response;
                }
                GetFile(objectRequest, _Media, Request, Response);
            }
            return Response;
        }
        private string GetDefault(string objectRequest, List<string> Defaults)
        {
            string folder = objectRequest;
            objectRequest = string.Empty;
            foreach (string s in Defaults)
            {
                string defaultrequest = Path.Combine(folder, s);
                if (File.Exists(defaultrequest))
                {
                    objectRequest = defaultrequest;
                    break;
                }
            }
            return objectRequest;
        }
        private void GetFile(string path, Dictionary<string, MediaItem> _Media, IOSRequest Request, IOSResponse Response)
        {
            string ext = Path.GetExtension(path).Substring(1).ToLower();
            if (!_Media.ContainsKey(ext))
            {
                IOException exception = new IOException($"The content type is prohibited for the request on this resource.", 403);
                exception.BuildExceptionResponce(Request, out Response);
                return;
            }
            string MediaType = _Media[ext].Name;
            bool asBinary = _Media[ext].Binary;
            Response.HttpHeaders!.Add("Content-Type", MediaType, false);
            if (Request.HttpMethod == "options")
            {
                Response.HttpHeaders!.Add("Allow", "GET, POST, OPTIONS, HEAD", false);
                return;
            }
            byte[] data = File.ReadAllBytes(path);
            data = CompressData(Request, Response, data, _Media[ext]);
            Response.HttpHeaders!.Add("Content-Length", data.Length.ToString(), false);
            if (Request.HttpMethod == "head")
            {
                data = Array.Empty<byte>();
                return;
            }
            Response.Data.AddRange(data);
            data = Array.Empty<byte>();
        }
        public byte[] CompressData(IOSRequest Request, IOSResponse Response, byte[] data, MediaItem media)
        {
            string? compressMethods = Request.HttpHeaders!.Find("accept-encoding");
            if (string.IsNullOrEmpty(compressMethods)) { return data; }
            List<string> Methods = compressMethods.Split(',').ToList();
            List<string> Preferred = PreferredText;
            if (media.Binary)
            {
                Preferred = PreferredBinary;
            }
            if (!Methods.Exists(x => x.Trim().ToLower() == "gzip"))
            {
                Preferred.Remove("gzip");
            }
            if (!Methods.Exists(x => x.Trim().ToLower() == "zstd"))
            {
                Preferred.Remove("zstd");
            }
            if (!Methods.Exists(x => x.Trim().ToLower() == "deflate"))
            {
                Preferred.Remove("deflate");
            }
            if (!Methods.Exists(x => x.Trim().ToLower() == "br"))
            {
                Preferred.Remove("br");
            }
            if (Preferred.Count == 0) { return data; }
            if (Preferred[0] == "gzip")
            {
                data = GzipCompress(data);
                Response.HttpHeaders!.Add("Content-Encoding", "gzip", false);
            }
            else if (Preferred[0] == "zstd")
            {
                data = ZstdCompress(data);
                Response.HttpHeaders!.Add("Content-Encoding", "zstd", false);
            }
            else if (Preferred[0] == "deflate")
            {
                data = DeflateCompress(data);
                Response.HttpHeaders!.Add("Content-Encoding", "deflate", false);
            }
            else if (Preferred[0] == "br")
            {
                data = BrotliCompress(data);
                Response.HttpHeaders!.Add("Content-Encoding", "br", false);
            }
            return data;
        }
        private byte[] GzipCompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return compressedStream.ToArray();
            }
        }
        private byte[] ZstdCompress(byte[] data)
        {
            using (var compressor = new Compressor())
            {
                return compressor.Wrap(data);
            }
        }
        private byte[] BrotliCompress(byte[] data)
        {
            using (var memoryStream = new MemoryStream())
            {
                using (var brotliStream = new BrotliStream(memoryStream, CompressionLevel.Optimal))
                {
                    brotliStream.Write(data, 0, data.Length);
                }
                return memoryStream.ToArray();
            }
        }
        private byte[] DeflateCompress(byte[] data)
        {
            using (var compressedStream = new MemoryStream())
            {
                using (var gzipStream = new DeflateStream(compressedStream, CompressionMode.Compress))
                {
                    gzipStream.Write(data, 0, data.Length);
                }
                return compressedStream.ToArray();
            }
        }
    }
}
