using System.Collections.Concurrent;
using System.Reflection;

namespace Activator
{
    public class Executor<TResult>
    {
        public Func<TResult?>? Exec { get; set; }
        public Executor() { }
        public Executor(Func<TResult?> Exec) { this.Exec = Exec; }
        public TResult? Execute()
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            return this.Exec();
        }
    }
    public class Executor<T, TResult>
    {
        public Func<T?, TResult?>? Exec { get; set; }
        public Executor() { }
        public Executor(Func<T?, TResult?> Exec) { this.Exec = Exec; }
        public TResult? Execute(T? Param)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            return this.Exec(Param);
        }
    }
    public class Executor<T1, T2, TResult>
    {
        public Func<T1?, T2?, TResult?>? Exec { get; set; }
        public Executor() { }
        public Executor(Func<T1?, T2?, TResult?> Exec) { this.Exec = Exec; }
        public TResult? Execute(T1? Param1, T2? Param2)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            return this.Exec(Param1, Param2);
        }
    }
    public class Executor<T1, T2, T3, TResult>
    {
        public Func<T1?, T2?, T3?, TResult?>? Exec { get; set; }
        public Executor() { }
        public Executor(Func<T1?, T2?, T3?, TResult?> Exec) { this.Exec = Exec; }
        public TResult? Execute(T1? Param1, T2? Param2, T3? Param3)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            return this.Exec(Param1, Param2, Param3);
        }
    }
    public class Executor<T1, T2, T3, T4, TResult>
    {
        public Func<T1?, T2?, T3?, T4?, TResult?>? Exec { get; set; }
        public Executor() { }
        public Executor(Func<T1?, T2?, T3?, T4?, TResult?> Exec) { this.Exec = Exec; }
        public TResult? Execute(T1? Param1, T2? Param2, T3? Param3, T4? Param4)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            return this.Exec(Param1, Param2, Param3, Param4);
        }
    }

    public class Invoker
    {
        public Action? Exec { get; set; }
        public Invoker() { }
        public Invoker(Action Exec) { this.Exec = Exec; }
        public void Execute()
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            this.Exec();
        }
    }
    public class Invoker<T>
    {
        public Action<T?>? Exec { get; set; }
        public Invoker() { }
        public Invoker(Action<T?> Exec) { this.Exec = Exec; }
        public void Execute(T? Param)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            this.Exec(Param);
        }
    }
    public class Invoker<T1, T2>
    {
        public Action<T1?, T2?>? Exec { get; set; }
        public Invoker() { }
        public Invoker(Action<T1?, T2?> Exec) { this.Exec = Exec; }
        public void Execute(T1? Param1, T2? Param2)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            this.Exec(Param1, Param2);
        }
    }
    public class Invoker<T1, T2, T3>
    {
        public Action<T1?, T2?, T3?>? Exec { get; set; }
        public Invoker() { }
        public Invoker(Action<T1?, T2?, T3?> Exec) { this.Exec = Exec; }
        public void Execute(T1? Param1, T2? Param2, T3? Param3)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            this.Exec(Param1, Param2, Param3);
        }
    }
    public class Invoker<T1, T2, T3, T4>
    {
        public Action<T1?, T2?, T3?, T4?>? Exec { get; set; }
        public Invoker() { }
        public Invoker(Action<T1?, T2?, T3?, T4?> Exec) { this.Exec = Exec; }
        public void Execute(T1? Param1, T2? Param2, T3? Param3, T4? Param4)
        {
            if (Exec == null) { throw new Exception("The method being called is not defined."); }
            this.Exec(Param1, Param2, Param3, Param4);
        }
    }

    public class ExecProxy : ConcurrentDictionary<string, List<object>>
    {
        public class SchemeInfo
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
            public SchemeInfo()
            {
                Separator = "/";
                Location = string.Empty;
                Alias = string.Empty;
                Namespace = string.Empty;
                ClassName = string.Empty;
                Scheme = string.Empty;
            }
            public SchemeInfo(object Delegate, Assembly Assm, Type TargetType, MethodInfo mInfo, ExecProxy ParentProxy)
            {
                Alias = Path.GetFileNameWithoutExtension(Assm.Location);
                Namespace = TargetType.Namespace;
                ClassName = TargetType.Name;
                this.Delegate = Delegate;
                Assembly = Assm;
                MethodInfo = mInfo;
                this.TargetType = TargetType;
                Separator = ParentProxy.schemeSeparator;
                Location = Assm.Location;

                //string? TargetFullName = TargetType.FullName;
                //if (string.IsNullOrEmpty(TargetFullName)) { TargetFullName = TargetType.Name; }
                //TargetFullName = TargetFullName.Replace(".", Separator);
                //Scheme = ($"{Separator}{Alias}{Separator}{TargetFullName}{Separator}{mInfo.Name}").ToLower();

                string _Nspace = $"{Separator}{Namespace}";
                if (string.IsNullOrEmpty(Namespace))
                {
                    _Nspace = string.Empty;
                }
                Scheme = ($"{Separator}{Alias}{_Nspace}{Separator}{ClassName}{Separator}{mInfo.Name}").ToLower();
            }
        }
        public List<string> roots { get; private set; } = new List<string>();
        public List<SchemeInfo> Schemes { get; private set; } = new List<SchemeInfo>();
        public string schemeSeparator { get; set; } = "/";
        public ExecProxy() { }
        public void Add<TResult>(Func<TResult?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Executor<TResult> Executor = new Executor<TResult>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T, TResult>(Func<T?, TResult?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Executor<T, TResult> Executor = new Executor<T, TResult>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }

            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2, TResult>(Func<T1?, T2?, TResult?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Executor<T1, T2, TResult> Executor = new Executor<T1, T2, TResult>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2, T3, TResult>(Func<T1?, T2?, T3?, TResult?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Executor<T1, T2, T3, TResult> Executor = new Executor<T1, T2, T3, TResult>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2, T3, T4, TResult>(Func<T1?, T2?, T3?, T4?, TResult?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Executor<T1, T2, T3, T4, TResult> Executor = new Executor<T1, T2, T3, T4, TResult>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add(Action Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Invoker Executor = new Invoker();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T>(Action<T?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Invoker<T> Executor = new Invoker<T>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2>(Action<T1?, T2?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Invoker<T1, T2> Executor = new Invoker<T1, T2>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2, T3>(Action<T1?, T2?, T3?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Invoker<T1, T2, T3> Executor = new Invoker<T1, T2, T3>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }
        public void Add<T1, T2, T3, T4>(Action<T1?, T2?, T3?, T4?> Exec)
        {
            MethodInfo mInfo = Exec.GetMethodInfo();
            Type? TargetType = Exec.Target?.GetType();
            Assembly? Assm = TargetType?.Assembly;
            if (Assm == null || TargetType == null) { return; }
            SchemeInfo Scheme = new SchemeInfo(Exec, Assm, TargetType, mInfo, this);
            Schemes.Add(Scheme);
            string scheme = Scheme.Scheme;

            Invoker<T1, T2, T3, T4> Executor = new Invoker<T1, T2, T3, T4>();
            Executor.Exec = Exec;
            if (ContainsKey(scheme))
            {
                if (base[scheme].Find(x => x.GetType() == Exec.GetType()) != null) { throw new Exception("The scheme already contains such a method."); }
                base[scheme].Add(Exec);
            }
            else { TryAdd(scheme, new List<object> { Executor }); }
            int index = scheme.LastIndexOf(schemeSeparator);
            string root = scheme.Substring(0, index);
            if (roots.IndexOf(root) < 0) { roots.Add(root); }
        }

        public Dictionary<string, T> Get<T>()
        {
            Dictionary<string, T> Selected = new Dictionary<string, T>();
            foreach (var SchemeMethods in this)
            {
                List<object> Overloads = SchemeMethods.Value.Where(x => x.GetType() == typeof(T)).ToList();
                if (Overloads.Count == 0) { continue; }
                Selected.Add(SchemeMethods.Key, (T)Overloads[0]);
            }
            return Selected;
        }
        public Dictionary<string, T> Get<T>(string scheme)
        {
            Dictionary<string, T> Selected = new Dictionary<string, T>();
            foreach (var SchemeMethods in this)
            {
                if (SchemeMethods.Key != scheme) { continue; }
                List<object> Overloads = SchemeMethods.Value.Where(x => x.GetType() == typeof(T)).ToList();
                if (Overloads.Count == 0) { continue; }
                Selected.Add(SchemeMethods.Key, (T)Overloads[0]);
            }
            return Selected;
        }
        public Dictionary<string, T> Where<T>(string schemetemplate)
        {
            schemetemplate = schemetemplate.ToLower();
            Dictionary<string, T> Selected = new Dictionary<string, T>();
            foreach (var SchemeMethods in this)
            {
                if (!SchemeMethods.Key.StartsWith(schemetemplate)) { continue; }
                List<object> Overloads = SchemeMethods.Value.Where(x => x.GetType() == typeof(T)).ToList();
                if (Overloads.Count == 0) { continue; }
                Selected.Add(SchemeMethods.Key, (T)Overloads[0]);
            }
            return Selected;
        }
        public Func<TResult?>? GetExecutor<TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Executor<TResult>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Executor<TResult>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<TResult>)) { continue; }
                Delegate = (Executor<TResult>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Func<T1?, TResult?>? GetExecutor<T1, TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Executor<T1, TResult>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Executor<T1, TResult>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, TResult>)) { continue; }
                Delegate = (Executor<T1, TResult>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Func<T1?, T2?, TResult?>? GetExecutor<T1, T2, TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Executor<T1, T2, TResult>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Executor<T1, T2, TResult>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, TResult>)) { continue; }
                Delegate = (Executor<T1, T2, TResult>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Func<T1?, T2?, T3?, TResult?>? GetExecutor<T1, T2, T3, TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Executor<T1, T2, T3, TResult>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, T3, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Executor<T1, T2, T3, TResult>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, T3, TResult>)) { continue; }
                Delegate = (Executor<T1, T2, T3, TResult>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Func<T1?, T2?, T3?, T4?, TResult?>? GetExecutor<T1, T2, T3, T4, TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Executor<T1, T2, T3, T4, TResult>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, T3, T4, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Executor<T1, T2, T3, T4, TResult>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, T3, T4, TResult>)) { continue; }
                Delegate = (Executor<T1, T2, T3, T4, TResult>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Action? GetInvoker(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Invoker? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Invoker)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Invokers = this[scheme];
            foreach (object item in Invokers)
            {
                if (item.GetType() != typeof(Invoker)) { continue; }
                Delegate = (Invoker)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Action<T?>? GetInvoker<T>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Invoker<T>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Invoker<T>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Invokers = this[scheme];
            foreach (object item in Invokers)
            {
                if (item.GetType() != typeof(Invoker<T>)) { continue; }
                Delegate = (Invoker<T>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Action<T1?, T2?>? GetInvoker<T1, T2>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Invoker<T1, T2>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Invoker<T1, T2>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Invokers = this[scheme];
            foreach (object item in Invokers)
            {
                if (item.GetType() != typeof(Invoker<T1, T2>)) { continue; }
                Delegate = (Invoker<T1, T2>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Action<T1?, T2?, T3?>? GetInvoker<T1, T2, T3>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Invoker<T1, T2, T3>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2, T3>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Invoker<T1, T2, T3>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Invokers = this[scheme];
            foreach (object item in Invokers)
            {
                if (item.GetType() != typeof(Invoker<T1, T2, T3>)) { continue; }
                Delegate = (Invoker<T1, T2, T3>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public Action<T1?, T2?, T3?, T4?>? GetInvoker<T1, T2, T3, T4>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            Invoker<T1, T2, T3, T4>? Delegate = null;
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2, T3, T4>)) { throw new Exception("The scheme does not define any call methods."); }
                Delegate = (Invoker<T1, T2, T3, T4>)this[scheme][0];
                return Delegate.Exec;
            }
            List<object> Invokers = this[scheme];
            foreach (object item in Invokers)
            {
                if (item.GetType() != typeof(Invoker<T1, T2, T3, T4>)) { continue; }
                Delegate = (Invoker<T1, T2, T3, T4>)item;
            }
            if (Delegate == null) { return null; }
            return Delegate.Exec;
        }
        public object? Exec<TResult>(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                try { return ((Executor<TResult>)this[scheme][0]).Execute(); }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<TResult>)) { continue; }
                try { return ((Executor<TResult>)item).Execute(); }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T, TResult>(string scheme, T Param)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                try { return ((Executor<T, TResult>)this[scheme][0]).Execute(Param); }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T, TResult>)) { continue; }
                try { return ((Executor<T, TResult>)item).Execute(Param); }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2, TResult>(string scheme, T1 Param1, T2 Param2)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                try { return ((Executor<T1, T2, TResult>)this[scheme][0]).Execute(Param1, Param2); }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, TResult>)) { continue; }
                try { return ((Executor<T1, T2, TResult>)item).Execute(Param1, Param2); }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2, T3, TResult>(string scheme, T1 Param1, T2 Param2, T3 Param3)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, T3, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                try { return ((Executor<T1, T2, T3, TResult>)this[scheme][0]).Execute(Param1, Param2, Param3); }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, T3, TResult>)) { continue; }
                try { return ((Executor<T1, T2, T3, TResult>)item).Execute(Param1, Param2, Param3); }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2, T3, T4, TResult>(string scheme, T1 Param1, T2 Param2, T3 Param3, T4 Param4)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Executor<T1, T2, T3, T4, TResult>)) { throw new Exception("The scheme does not define any call methods."); }
                try { return ((Executor<T1, T2, T3, T4, TResult>)this[scheme][0]).Execute(Param1, Param2, Param3, Param4); }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Executor<T1, T2, T3, T4, TResult>)) { continue; }
                try { return ((Executor<T1, T2, T3, T4, TResult>)item).Execute(Param1, Param2, Param3, Param4); }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec(string scheme)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker)) { throw new Exception("The scheme does not define any call methods."); }
                try { ((Invoker)this[scheme][0]).Execute(); return true; }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Invoker)) { continue; }
                try { ((Invoker)item).Execute(); return true; }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T>(string scheme, T Param)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T>)) { throw new Exception("The scheme does not define any call methods."); }
                try { ((Invoker<T>)this[scheme][0]).Execute(Param); return true; }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Invoker<T>)) { continue; }
                try { ((Invoker<T>)item).Execute(Param); return true; }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2>(string scheme, T1 Param1, T2 Param2)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2>)) { throw new Exception("The scheme does not define any call methods."); }
                try { ((Invoker<T1, T2>)this[scheme][0]).Execute(Param1, Param2); return true; }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Invoker<T1, T2>)) { continue; }
                try { ((Invoker<T1, T2>)item).Execute(Param1, Param2); return true; }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2, T3>(string scheme, T1 Param1, T2 Param2, T3 Param3)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2, T3>)) { throw new Exception("The scheme does not define any call methods."); }
                try { ((Invoker<T1, T2, T3>)this[scheme][0]).Execute(Param1, Param2, Param3); return true; }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Invoker<T1, T2, T3>)) { continue; }
                try { ((Invoker<T1, T2, T3>)item).Execute(Param1, Param2, Param3); return true; }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public object? Exec<T1, T2, T3, T4>(string scheme, T1 Param1, T2 Param2, T3 Param3, T4 Param4)
        {
            if (!ContainsKey(scheme)) { throw new Exception("The scheme is not defined in the proxy context."); }
            if (this[scheme].Count == 0) { throw new Exception("The scheme does not define any call methods."); }
            if (this[scheme].Count == 1)
            {
                if (this[scheme][0].GetType() != typeof(Invoker<T1, T2, T3, T4>)) { throw new Exception("The scheme does not define any call methods."); }
                try { ((Invoker<T1, T2, T3, T4>)this[scheme][0]).Execute(Param1, Param2, Param3, Param4); return true; }
                catch (Exception e) { return e; }
            }
            List<object> Executors = this[scheme];
            foreach (object item in Executors)
            {
                if (item.GetType() != typeof(Invoker<T1, T2, T3, T4>)) { continue; }
                try { ((Invoker<T1, T2, T3, T4>)item).Execute(Param1, Param2, Param3, Param4); return true; }
                catch (Exception e) { return e; }
            }
            return null;
        }
        public string Info()
        {
            if (Count == 0) { return "The proxy has no associated execution schemes."; }
            List<string> info = new List<string>();
            foreach (var scheme in this)
            {
                List<SchemeInfo> SelectedSchemes = Schemes.Where(x => x.Scheme == scheme.Key).ToList();
                List<string> delegates = new List<string>();
                if (SelectedSchemes.Count == 0)
                {
                    delegates.Add($"There are no delegates associated with the call scheme \"{scheme.Key}\".");
                    continue;
                }
                foreach (SchemeInfo Scheme in SelectedSchemes)
                {
                    if (Scheme.Delegate == null || Scheme.MethodInfo == null) { continue; }
                    Type delegateType = Scheme.Delegate.GetType();
                    string methodInfo = Scheme.MethodInfo.Name;
                    string delegateInfo = delegateType.Name;
                    delegateInfo = delegateInfo.Substring(0, delegateInfo.IndexOf('`'));
                    List<string> Params = new List<string>();
                    Params.Add($"{typeof(string).FullName} scheme");
                    foreach (ParameterInfo parameter in Scheme.MethodInfo.GetParameters())
                    {
                        Params.Add($"{parameter.ParameterType.FullName} {parameter.Name}");
                    }
                    string stringInfo = $"Delegate {delegateInfo} associated with Assembly Method \"{methodInfo}\", call scheme Exec({string.Join(", ", Params)})";
                    delegates.Add(stringInfo);
                }
                string schemeInfo = $"{scheme.Key}:";
                if (delegates.Count == 0)
                {
                    schemeInfo += " null";
                }
                else if (delegates.Count == 1)
                {
                    schemeInfo += $" {delegates[0]}";
                }
                else
                {
                    schemeInfo += $"\n{string.Join("\n\t", delegates)}";
                }
                info.Add(schemeInfo);
            }
            return string.Join("\n", info);
        }
    }
}
