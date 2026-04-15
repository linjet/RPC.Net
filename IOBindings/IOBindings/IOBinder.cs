using System.Reflection;
using Activator;
using IOObjects;
using Tools;

namespace IOBinding
{
    public class IOBinder : Dictionary<string, Func<IOSRequest?, IOSResponse?>>
    {
        public class MethodDefine
        {
            public string Separator { get; private set; }       // разделитель сущностей в схеме
            public string Location { get; private set; }        // путь к файлу библиотеки
            public string Alias { get; private set; }           // псевдоним библиотеки - имя dll-файла без расширения
            public string? Namespace { get; private set; }
            public string ClassName { get; private set; }
            public string Scheme { get; private set; }          // путь к методу: <псевдоним библиотеки><разделитель><пространство имен><разделитель><полное имя класса><разделитель><имя метода в классе>
            public object? Delegate { get; private set; }        // делегат, к которому привязан метод класса
            public Assembly? Assembly { get; private set; }      // сборка, в которой определен класс
            public MethodInfo? MethodInfo { get; private set; }  // метод, назначаемый делегату
            public Type? TargetType { get; private set; }        // класс, в котором определен метод
            public IOHostConfig? Config { get; set; }
            public MethodDefine(ExecProxy.SchemeInfo baseInfo)
            {
                Separator = baseInfo.Separator;
                Scheme = baseInfo.Scheme;
                Location = baseInfo.Location;
                Alias = baseInfo.Alias;
                Namespace = baseInfo.Namespace;
                ClassName = baseInfo.ClassName;
                MethodInfo = baseInfo.MethodInfo;
                TargetType = baseInfo.TargetType;
                Assembly = baseInfo.Assembly;
                Delegate = baseInfo.Delegate;
            }
        }
        public class IOMethod
        {
            public Func<IOSRequest?, IOSResponse?>? Delegate { get; private set; }
            public IOHostConfig? Config { get; private set; }
            public IOMethod() { }
            public IOMethod(Func<IOSRequest?, IOSResponse?> Delegate) { this.Delegate = Delegate; }
            public IOMethod(Func<IOSRequest?, IOSResponse?> Delegate, IOHostConfig? Config)
            {
                this.Delegate = Delegate;
                this.Config = Config;
            }
        }
        public string schemeSeparator { get; private set; } = "/";
        public Dictionary<string, string> Map { get; private set; } = new Dictionary<string, string>();
        public Dictionary<string, MethodDefine> MethodsMap { get; private set; } = new Dictionary<string, MethodDefine>();
        public IOBinder() { }
        public void Bind(IOBindings Bindings, ExecProxy SchemesProxy)
        {
            schemeSeparator = SchemesProxy.schemeSeparator;
            foreach (var Binding in Bindings)
            {
                foreach (var Pair in Binding.Value.Schemes)
                {
                    string alias = Pair.Key.ToLower();
                    string value = Pair.Value.ToLower();
                    Dictionary<string, Executor<IOSRequest, IOSResponse>> Selected = SchemesProxy.Get<Executor<IOSRequest, IOSResponse>>(value);
                    if (Selected.Count == 0)
                    {
                        value += schemeSeparator;
                        Selected = SchemesProxy.Where<Executor<IOSRequest, IOSResponse>>(value);
                    }
                    if (Selected.Count > 1 && !alias.EndsWith(schemeSeparator)) { alias += schemeSeparator; }
                    if (Selected.Count > 0)
                    {
                        foreach (var Scheme in Selected)
                        {
                            if (Scheme.Value.Exec == null) { continue; }
                            string Key = Scheme.Key.ToLower();
                            if (Key.StartsWith(value))
                            {
                                Key = Key.Substring(value.Length);
                                if (!alias.EndsWith(schemeSeparator)) { alias += schemeSeparator; }
                                Key = alias + Key;
                            }
                            if (Key.EndsWith(schemeSeparator) && Key != schemeSeparator) { Key = Key.Substring(0, Key.Length - 1); }
                            if (ContainsKey(Key)) { continue; }
                            Add(Key, Scheme.Value.Exec);
                            Map.Add(Key, Scheme.Key);

                            ExecProxy.SchemeInfo? sInfo = SchemesProxy.Schemes.Find(x => x.Scheme == Scheme.Key);
                            if (sInfo != null)
                            {
                                MethodDefine Definition = new MethodDefine(sInfo);
                                Definition.Config = Binding.Value.Config;
                                MethodsMap.Add(Key, Definition);
                            }
                        }
                    }
                }
            }
        }
        public IOMethod? Find(string url)
        {
            IOHostConfig? Config = null;
            IOMethod? mInfo = null;
            if (ContainsKey(url))
            {
                if (MethodsMap.ContainsKey(url)) { Config = MethodsMap[url].Config; }
                mInfo = new IOMethod(this[url], Config);
                return mInfo;
            }
            while (url.Length > 0)
            {
                int lastIndex = url.LastIndexOf(schemeSeparator);
                if (lastIndex < 0) { break; }
                if (lastIndex == 0) { lastIndex = 1; }
                url = url.Substring(0, lastIndex);
                if (ContainsKey(url))
                {
                    if (MethodsMap.ContainsKey(url)) { Config = MethodsMap[url].Config; }
                    mInfo = new IOMethod(this[url], Config);
                    break;
                }
                if (url == schemeSeparator) { break; }
            }
            return mInfo;
        }
        public Dictionary<string, object?>? FindInfo(string url)
        {
            Dictionary<string, object?>? Result = null;
            Func<IOSRequest?, IOSResponse?>? Delegate = null;
            IOHostConfig? Config = null;
            string definition = "null";
            string baseDefinition = "none";
            if (ContainsKey(url))
            {
                Result = new Dictionary<string, object?>();
                Delegate = this[url];
                definition = Map[url];
                if (MethodsMap.ContainsKey(url))
                {
                    MethodDefine mInfo = MethodsMap[url];
                    if (mInfo.MethodInfo != null)
                    {
                        Config = mInfo.Config;
                        baseDefinition = $"{mInfo.Namespace}.{mInfo.ClassName}.{mInfo.MethodInfo.Name} in the library {mInfo.Location}";
                    }
                }
                Result.Add("Delegate", Delegate);
                Result.Add("Request", url);
                Result.Add("FindBy", url);
                Result.Add("Definition", definition);
                Result.Add("Base", baseDefinition);
                Result.Add("Config", Config);
                return Result;
            }
            bool exists = false;
            string baseUrl = url;

            while (url.Length > 0)
            {
                int lastIndex = url.LastIndexOf(schemeSeparator);
                if (lastIndex < 0) { break; }
                if (lastIndex == 0) { lastIndex = 1; }
                url = url.Substring(0, lastIndex);
                if (ContainsKey(url))
                {
                    exists = true;
                    break;
                }
                if (url == schemeSeparator) { break; }
            }

            if (exists)
            {
                Delegate = this[url];
                definition = Map[url];
                if (MethodsMap.ContainsKey(url))
                {
                    MethodDefine mInfo = MethodsMap[url];
                    if (mInfo.MethodInfo != null)
                    {
                        Config = mInfo.Config;
                        baseDefinition = $"{mInfo.Namespace}.{mInfo.ClassName}.{mInfo.MethodInfo.Name} in the library {mInfo.Location}";
                    }
                }
                Result = new Dictionary<string, object?>();
                Result.Add("Delegate", Delegate);
                Result.Add("Request", baseUrl);
                Result.Add("FindBy", url);
                Result.Add("Definition", definition);
                Result.Add("Base", baseDefinition);
                Result.Add("Config", Config);
            }
            return Result;
        }
        public IOSResponse? Exec(IOSRequest Request)
        {
            IOSResponse? Response = null;
            if (string.IsNullOrEmpty(Request.HttpRequest))
            {
                IOObjects.IOException e = new IOObjects.IOException("The purpose of the request is not defined.", 400);
                e.BuildExceptionResponce(Request, out Response);
                return Response;
            }
            string url = Request.HttpRequest;
            if (ContainsKey(url))
            {
                try
                {
                    object? responce = this[url](Request);
                    if (responce != null && responce.GetType() == typeof(IOSResponse)) { return (IOSResponse)responce; }
                    else if (responce == null)
                    {
                        IOObjects.IOException e = new IOObjects.IOException($"The requested method {url} returned a null result.", 400);
                        e.BuildExceptionResponce(Request, out Response);
                        return Response;
                    }
                    else
                    {
                        IOObjects.IOException e = new IOObjects.IOException($"The requested method {url} returned data of the wrong type \"({responce.GetType()})\". The type \"{typeof(IOSResponse)}\" was expected.", 400);
                        e.BuildExceptionResponce(Request, out Response);
                        return Response;
                    }
                }
                catch (Exception e)
                {
                    IOObjects.IOException ioe = new IOObjects.IOException(e, 400);
                    ioe.BuildExceptionResponce(Request, out Response);
                    return Response;
                }
            }
            bool exists = false;
            string baseUrl = url;
            while (url.Length > 0)
            {
                int lastIndex = url.LastIndexOf(schemeSeparator);
                if (lastIndex < 0) { break; }
                if (lastIndex == 0) { lastIndex = 1; }
                url = url.Substring(0, lastIndex);
                if (ContainsKey(url))
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                IOObjects.IOException e = new IOObjects.IOException($"The requested resource {baseUrl} was not found.", 404);
                e.BuildExceptionResponce(Request, out Response);
                return Response;
            }
            try
            {
                object? responce = this[url](Request);
                if (responce != null && responce.GetType() == typeof(IOSResponse)) { Response = (IOSResponse)responce; }
                else if (responce == null)
                {
                    IOObjects.IOException e = new IOObjects.IOException($"The requested method {url} returned a null result.", 400);
                    e.BuildExceptionResponce(Request, out Response);
                }
                else
                {
                    IOObjects.IOException e = new IOObjects.IOException($"The requested method {url} returned data of the wrong type \"({responce.GetType()})\". The type \"{typeof(IOSResponse)}\" was expected.", 400);
                    e.BuildExceptionResponce(Request, out Response);
                }
            }
            catch (Exception e)
            {
                IOObjects.IOException ioe = new IOObjects.IOException(e, 400);
                ioe.BuildExceptionResponce(Request, out Response);
            }
            return Response;
        }
        public string Info(bool console, ConsoleColor Color = ConsoleColor.DarkYellow)
        {
            List<string> Nfo = new List<string>();
            Nfo.Add($"Binding methods: ");
            foreach (var Pair in Map)
            {
                string baseMethod = "none";
                if (MethodsMap.ContainsKey(Pair.Key))
                {
                    MethodDefine mInfo = MethodsMap[Pair.Key];
                    if (mInfo.MethodInfo != null)
                    {
                        baseMethod = $"{mInfo.Namespace}.{mInfo.ClassName}.{mInfo.MethodInfo.Name}";
                    }
                }
                Nfo.Add($"query \"{Pair.Key}\" is bound to method \"{Pair.Value}\" from base \"{baseMethod}\"");
            }
            string result = string.Join("\n", Nfo);
            if (console)
            {
                Console.ForegroundColor = Color;
                Console.WriteLine($"{result}");
                Console.ResetColor();
            }
            return result;
        }
    }
}
