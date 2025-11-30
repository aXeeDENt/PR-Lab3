using System;
using System.Threading.Tasks;

namespace MemoryScramble
{
    internal class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                Console.WriteLine("Usage: dotnet run server PORT FILENAME | simulation");
                return;
            }

            var mode = args[0].ToLower();

            switch (mode)
            {
                case "server":
                    await RunServer(args);
                    break;
                case "simulation":
                    await RunSimulation();
                    break;
                default:
                    Console.WriteLine("Unknown mode. Use 'server' or 'simulation'.");
                    break;
            }
        }

        private static async Task RunServer(string[] args)
        {
            if (args.Length < 3)
            {
                Console.WriteLine("Usage: dotnet run server PORT FILENAME");
                return;
            }

            if (!int.TryParse(args[1], out int port) || port < 0)
            {
                Console.WriteLine("Invalid port number.");
                return;
            }

            var filename = args[2];

            try
            {
                var board = await Board.ParseFromFileAsync(filename);
                var server = new WebServer(board, port);
                await server.StartAsync();

                Console.WriteLine($"Server is running on port {server.Port}");
                Console.WriteLine("Press Ctrl+C to stop...");
                await Task.Delay(-1); 
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting server: {ex.Message}");
            }
        }

        private static async Task RunSimulation()
        {
            try
            {
                await Simulation.RunAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Simulation failed: {ex.Message}");
            }
        }
    }
}
