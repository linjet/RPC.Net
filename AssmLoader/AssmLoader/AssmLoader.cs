using System.Reflection;
using System.Runtime.Loader;
using System.Security.Cryptography;
using System.Text;
using Activator;

namespace AssmLoader
{
    public static class CalcMD5
    {
        private static MD5 md5Instance = MD5.Create();
        public static byte[] CalcString(string content, Encoding Encod)
        {
            return md5Instance.ComputeHash(Encod.GetBytes(content));
        }
        public static byte[] CalcString(string content)
        {
            return md5Instance.ComputeHash(Encoding.Unicode.GetBytes(content));
        }
        public static byte[] CalcFile(string file)
        {
            byte[] bytes;
            using (var stream = File.OpenRead(file))
            {
                bytes = md5Instance.ComputeHash(stream);
            }
            return bytes;
        }
        public static string GetString(byte[] hash)
        {
            return BitConverter.ToString(hash).Replace("-", "");
        }
        public static string GetFromFile(string file)
        {
            byte[] hash = CalcFile(file);
            return GetString(hash);
        }
        public static string GetFromString(string content, Encoding Encod)
        {
            byte[] hash = CalcString(content, Encod);
            return GetString(hash);
        }
        public static string GetFromString(string content)
        {
            byte[] hash = CalcString(content);
            return GetString(hash);
        }
    }
    public class AssmInfo
    {
        public string? Name { get; set; }
        public string Location { get; set; }
        public Version? Version { get; set; }
        public string FullName { get; set; }
        public string? CultureName { get; set; }
        public AssemblyContentType ContentType { get; set; }
        public string HASH { get; set; }
        public AssmInfo(Assembly Assm)
        {
            AssemblyName assemblyName = Assm.GetName();
            Location = Assm.Location;
            Name = assemblyName.Name;
            FullName = assemblyName.FullName;
            Version = assemblyName.Version;
            CultureName = assemblyName.CultureName;
            ContentType = assemblyName.ContentType;
            HASH = CalcMD5.GetFromFile(Assm.Location);
        }
        public string Info()
        {
            List<string> info = new List<string>();
            info.Add(Location);
            info.Add($"\tAssembly Name:{Name}");
            info.Add($"\tAssembly FullName:{FullName}");
            info.Add($"\tAssembly Version:{Version}");
            info.Add($"\tAssembly CultureName:{CultureName}");
            info.Add($"\tAssembly ContentType:{ContentType}");
            info.Add($"\tAssembly Lib HASH: {HASH}");
            string console = string.Join("\n", info.ToArray());
            info.Clear();
            return console;
        }
    }
    public class CheckAssm
    {
        public string Location { get; private set; }
        public string? Name { get; private set; }
        public string HASH { get; private set; } = string.Empty;
        public string? Message { get; private set; }
        public bool Skipped { get; private set; } = false;
        public bool NameExists { get; private set; } = false;
        public CheckAssm(string Location, Dictionary<string, AssmInfo> ExistsLibsInfo)
        {
            this.Location = Location.ToLower();
            Name = Path.GetFileNameWithoutExtension(Location).ToLower();
            if (File.Exists(this.Location))
            {
                HASH = CalcMD5.GetFromFile(this.Location);
                AssmInfo? ExistsAssembly = ExistsLibsInfo.Values.ToList().Find(x => x.Location.ToLower() == this.Location);
                if (ExistsAssembly != null)
                {
                    Skipped = true;
                    Message = $"The library \"{this.Location}\" is already loaded into the application context";
                    if (ExistsAssembly.HASH != HASH) { Message += ", but their versions do not match."; }
                    return;
                }
                ExistsAssembly = ExistsLibsInfo.Values.ToList().Find(x => x.Name?.ToLower() == Name);
                if (ExistsAssembly != null)
                {
                    NameExists = true;
                    Message = $"A library named \"{Name}\" is already loaded into the application context, but it references a different location.";
                    if (ExistsAssembly.HASH == HASH)
                    {
                        Skipped = true;
                        return;
                    }
                }
            }
            else
            {
                Skipped = true;
                Message = $"The library \"{this.Location}\" not found.";
                return;
            }
        }
        public void Exclusion(Exception e)
        {
            Skipped = true;
            Message = e.ToString();
        }
    }
    public class ILoader : IDisposable
    {
        /*
           правило загрузки библиотеки и вызова проксируемого метода из нее:
           string assembliespath = @"D:\Projects\SwS\sources\TestAssembly\bin\Debug\net8.0\bin\Lib1.dll"; - путь к загружаемой библиотеке
           Activator.ExecProxy Proxy = new Activator.ExecProxy();                                         - посредник вызова методов
           Proxy.schemeSeparator = "/";                                                                   - разделитель в схеме (ключе) сопоставления проксируемых методов
           AssmLoader.ILoader Loader = new AssmLoader.ILoader(assembliespath, Proxy);                     - загрузчик библиотеки в контекст приложения
           string call = "lib1/namespace1/class1/exectemp1";                                              - схема вызова метода
           object? time = Proxy.Exec<object, long>(call, new object());                                   - вызов метода по схеме, где переданный в качестве параметра object - транслируемый параметр в связанный с делегатом метод из загруженной библиотеки

           правило объявления проксируемых методов в загружаемой библиотеке:
           public ClassMethodsOwner() { }                                                                 - класс, методы которого необходимо опубликовать через Activator.ExecProxy, обязательно должен содержать базовый конструктор
           public ClassMethodsOwner(Activator.ExecProxy Proxy)                                            - конструктор класса, при вызове которого будут опубликоманы нужные методы
           {
               Proxy.Add<object, long>(ExecTemp1);                                                        - добавление метода в Activator.ExecProxy
               Proxy.Add<object, long>(ExecTemp2);
           }
           public long ExecTemp1(object? data)
           {
               return DateTime.UtcNow.Ticks;
           }
           public long ExecTemp2(object? data)
           {
               return DateTime.UtcNow.Ticks;
           }
        */
        public string Location { get; private set; }
        public string DependencyLocation { get; private set; }
        public List<string> Schemas { get; private set; } = new List<string>();
        public ExecProxy? RefProxy { get; private set; }
        public AssemblyLoadContext? Context { get; private set; }
        public int maxDestructionAttempts { get; private set; } = 10;
        public bool ThrowIfLoadingScipped { get; set; } = false;
        public bool disposed { get; private set; } = false;
        public ILoader()
        {
            Location = string.Empty;
            DependencyLocation = string.Empty;
        }
        public ILoader(string Location, ExecProxy Proxy)
        {
            if (!File.Exists(Location)) { throw new Exception($"The library at the specified location ({Location}) was not found."); }
            this.Location = Location.ToLower();
            string? Dependency = Path.GetDirectoryName(this.Location);
            if (string.IsNullOrEmpty(Dependency)) { Dependency = Environment.CurrentDirectory.ToLower(); }
            DependencyLocation = Dependency;
            RefProxy = Proxy;
            Load(Proxy);

            try { Load(Proxy); }
            catch { throw; }
        }
        public void Load(ExecProxy Proxy)
        {
            Dictionary<string, AssmInfo> BefoteLibsInfo = GetLoadedAssemblies(out List<string> BefoteLoadingInfo);
            CheckAssm Checking = new CheckAssm(Location, BefoteLibsInfo);
            if (Checking.Skipped || Checking.NameExists)
            {
                if (ThrowIfLoadingScipped) { throw new Exception(Checking.Message); }
                return;
            }

            Context = new AssemblyLoadContext(name: Location, isCollectible: true);
            Assembly assembly = Context.LoadFromAssemblyPath(Location);
            Dictionary<string, AssmInfo> AfterLibsInfo = GetLoadedAssemblies(out List<string> AfterLoadingInfo);

            AssemblyName[] Referenced = assembly.GetReferencedAssemblies();
            List<CheckAssm> SkipDependencies = new List<CheckAssm>();
            foreach (var item in Referenced)
            {
                string dependency = Path.Combine(DependencyLocation, $"{item.Name}.dll");
                CheckAssm Dependency = new CheckAssm(dependency, AfterLibsInfo);
                if (Dependency.Skipped)
                {
                    SkipDependencies.Add(Dependency);
                    continue;
                }
                try { Context.LoadFromAssemblyPath(dependency); }
                catch (Exception e)
                {
                    Dependency.Exclusion(e);
                    SkipDependencies.Add(Dependency);
                }
            }
            if (SkipDependencies.Count > 0)
            {
                /* при возникновении ошибок загрузки зависимостей требуется информировать инициатора об этом */
                /* и, каким-то образом, ожидать его решения продолжить выполнение или нет */
                SkipDependencies.Clear();
            }
            string root = Location;
            foreach (Type InnerType in assembly.GetTypes())
            {
                ConstructorInfo[] Constructors = InnerType.GetConstructors();
                foreach (ConstructorInfo constructorInfo in Constructors)
                {
                    ParameterInfo[] Parameters = constructorInfo.GetParameters();
                    if (Parameters.Length != 1) { continue; }
                    ParameterInfo Parameter = Parameters[0];
                    if (Parameter.ParameterType != Proxy.GetType()) { continue; }
                    if (Parameter == null) { continue; }
                    try
                    {
                        List<string> PrevSchemas = Proxy.Keys.ToList();
                        constructorInfo.Invoke(new object[] { Proxy });
                        List<string> PostSchemas = Proxy.Keys.ToList();
                        List<string> Temp = new List<string>();
                        Temp.AddRange(PrevSchemas);
                        Temp.AddRange(PostSchemas);
                        Schemas = Temp.Distinct().ToList();
                        Temp.Clear();
                    }
                    catch (Exception)
                    {
                        if (ThrowIfLoadingScipped) { throw; }
                    }
                }
            }
        }
        public Dictionary<string, AssmInfo> GetLoadedAssemblies(out List<string> LoadedInfo)
        {
            AppDomain Domain = AppDomain.CurrentDomain;
            return GetLoadedAssemblies(Domain, out LoadedInfo);
        }
        public Dictionary<string, AssmInfo> GetLoadedAssemblies(AppDomain Domain, out List<string> LoadedInfo)
        {
            Assembly[] LoadedAssemblies = Domain.GetAssemblies();
            Dictionary<string, AssmInfo> LibsInfo = new Dictionary<string, AssmInfo>();
            LoadedInfo = new List<string>();
            foreach (Assembly LoadedAssembly in LoadedAssemblies)
            {
                if (!LibsInfo.ContainsKey(LoadedAssembly.Location))
                {
                    LibsInfo.Add(LoadedAssembly.Location, new AssmInfo(LoadedAssembly));
                }
                LoadedInfo.Add(LibsInfo[LoadedAssembly.Location].Info());
            }
            return LibsInfo;
        }
        public void Dispose()
        {
        }
    }
}
