using Activator;
using AssmLoader;
using Globe;
using IOBinding;
using IOMonitor;
using IOObjects;
using IOService;
using System.Reflection;
using Tools;

namespace IOServer
{
    /* endpoint load balancing data transport service */
    /* служба транспорта данных с распределением нагрузки на конечные точки */

    internal class mInterface
    {
        static bool quit = false;
        static string serverconfig = @"./cfg/config.json";
        static string routerconfig = @"./cfg/router.json";
        static string bindingsconfig = @"./cfg/bindings.json";
        static TCP? Server = null;
        static TCP? Router = null;
        static IOBindings? cfgBindings = null;

        static ConsoleColor ServerInfoColor = ConsoleColor.Yellow;
        static ConsoleColor RouterInfoColor = ConsoleColor.Green;
        static void CommandProcessing(string input)
        {
            string command = input.Trim().ToLower();
            string instance = command;
            if (command.IndexOf(" ") > 0)
            {
                instance = command.Substring(0, command.IndexOf(" ")).Trim();
                command = command.Substring(command.IndexOf(" ") + 1).Trim();
            }
            if (command == instance) { command = string.Empty; }
            if (instance == "q")
            {
                quit = true;
                return;
            }
            if (instance == "cls")
            {
                Console.Clear();
            }
            if (instance == "start" && Server != null && !Server.listen)
            {
                Server.Start();
                Server.Info(true, ServerInfoColor);
            }
            if (instance == "stop" && Server != null && Server.listen)
            {
                Server.Stop();
                Server.Info(true, ServerInfoColor);
            }
            if (instance == "info" && Server != null)
            {
                Console.Clear();
                Server.Info(true, ServerInfoColor);
            }
            if (instance == "s" && command == "i" && Server != null)
            {
                Console.Clear();
                Server.Info(true, ServerInfoColor);
            }
            if (instance == "r" && command == "i" && Router != null)
            {
                Console.Clear();
                Router.Info(true, RouterInfoColor);
            }
            if (instance == "sr" && command == "i" && Server != null && Router != null)
            {
                Console.Clear();
                Router.Info(true, RouterInfoColor);
                Console.WriteLine($"");
                Server.Info(true, ServerInfoColor);
            }
        }
        static void Main(string[] args)
        {
            Console.WriteLine("running services ...");
            try
            {
                cfgBindings = IOBindings.Load(bindingsconfig);
                IConfigs.Bindings = cfgBindings;

                IOConfig? RouterConfig = JsonReader.Load<IOConfig>(routerconfig);
                IOConfig? ServerConfig = JsonReader.Load<IOConfig>(serverconfig);

                IOMonitor.IOServer? Logger = null;
                IOMonitor.IOServer? Monitor = null;

                if (RouterConfig != null)
                {
                    Router = new TCP(RouterConfig, false);
                    Router.Simple = true;
                    Router.Start();
                    IOGlobe.BackListener = Router;
                }

                if (ServerConfig != null)
                {
                    if (ServerConfig.Log != null && ServerConfig.Log.Db != null)
                    {
                        Logger = new IOMonitor.IOServer(ServerConfig.Log);
                        Logger.Start();
                    }
                    if (ServerConfig.Monitor != null && ServerConfig.Monitor.Db != null)
                    {
                        if (Logger != null)
                        {
                            if (!Logger.EndPoint.Address.Equals(ServerConfig.Monitor.Address) || Logger.EndPoint.Port != ServerConfig.Monitor.Port)
                            {
                                Monitor = new IOMonitor.IOServer(ServerConfig.Monitor);
                                Monitor.Start();
                            }
                            else
                            {
                                Monitor = Logger;
                            }
                        }
                        else
                        {
                            Monitor = new IOMonitor.IOServer(ServerConfig.Monitor);
                            Monitor.Start();
                        }
                    }
                    Server = new TCP(ServerConfig);
                    if (Server.Extensions != null && Server.Extensions.Proxy != null && Server.Extensions.Assemblies != null)
                    {
                        if (cfgBindings != null)
                        {
                            foreach (IOBinding.IOBinding Binding in cfgBindings.Values)
                            {
                                if (Binding.Config != null && Binding.Config.Bin != null)
                                {
                                    Server.AddPlugins(Binding.Config.Bin);
                                }
                            }
                        }
                        IOGlobe.Proxy = Server.Extensions.Proxy;
                        IOGlobe.Assemblies = Server.Extensions.Assemblies;
                    }
                    Server.Start();
                    IOGlobe.FrontListener = Server;
                }
                IOBinder Binder = new IOBinder();
                Binder.Bind(cfgBindings!, IOGlobe.Proxy!);
                IOGlobe.Bindings = Binder;

                Console.ForegroundColor = ConsoleColor.Green;
                if (Router != null)
                {
                    Console.WriteLine($"\nTCP Router   {Router.Address}:{Router.Port}, listen: {Router.listen.ToString().ToLower()}");
                    if (Router.LogSender != null)
                    {
                        Console.WriteLine($"Router Log Interface {Router.LogSender.EndPoint}");
                    }
                    if (Router.MonSender != null)
                    {
                        if (Router.LogSender != null && Router.MonSender.EndPoint.Address.Equals(Router.LogSender.EndPoint.Address) && Router.MonSender.EndPoint.Port == Router.LogSender.EndPoint.Port)
                        {
                            Console.WriteLine($"Router Log and Monitor Interface {Router.MonSender.EndPoint}");
                        }
                        else
                        {
                            Console.WriteLine($"Router Monitor Interface {Router.MonSender.EndPoint}");
                        }
                    }
                }
                if (Server != null)
                {
                    Console.WriteLine($"TCP Frontend {Server.Address}:{Server.Port}, listen: {Server.listen.ToString().ToLower()}");
                    if (Server.LogSender != null && Server.MonSender != null)
                    {
                        if (Server.MonSender.EndPoint.Address.Equals(Server.LogSender.EndPoint.Address) && Server.MonSender.EndPoint.Port == Server.LogSender.EndPoint.Port)
                        {
                            Console.WriteLine($"Frontend Log and Monitor Interface {Server.MonSender.EndPoint}");
                        }
                        else
                        {
                            Console.WriteLine($"Frontend Log Interface {Server.LogSender.EndPoint}");
                            Console.WriteLine($"Frontend Monitor Interface {Server.MonSender.EndPoint}");
                        }
                    }
                    else if (Server.LogSender != null)
                    {
                        Console.WriteLine($"Frontend Log Interface {Server.LogSender.EndPoint}");
                    }
                    else if (Server.MonSender != null)
                    {
                        Console.WriteLine($"Frontend Monitor Interface {Server.MonSender.EndPoint}");
                    }
                }
                if (Logger != null && Monitor != null)
                {
                    if (!Logger.EndPoint.Address.Equals(Monitor.EndPoint.Address) || Logger.EndPoint.Port != Monitor.EndPoint.Port)
                    {
                        Console.WriteLine($"Logger Host EndPoint: {Logger.EndPoint}");
                        Console.WriteLine($"Monitoring Host EndPoint: {Monitor.EndPoint}");
                    }
                    else
                    {
                        Console.WriteLine($"Logger and Monitoring Host EndPoint: {Logger.EndPoint}");
                    }
                }
                else if (Logger != null)
                {
                    Console.WriteLine($"Logger Host EndPoint: {Logger.EndPoint}");
                }
                else if (Monitor != null)
                {
                    Console.WriteLine($"Monitoring Host EndPoint: {Monitor.EndPoint}");
                }
                Console.WriteLine($"");
                Console.ResetColor();
            }
            catch (Exception e)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine(e.ToString());
                Console.WriteLine($"");
                Console.ResetColor();
            }

            while (!quit)
            {
                string? input = Console.ReadLine();
                if (input == null || string.IsNullOrEmpty(input.Trim())) { continue; }
                input = input.Trim().ToLower();
                try { CommandProcessing(input); }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Red;
                    Console.WriteLine(e);
                    Console.ResetColor();
                }
            }
        }
    }
}
