using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace MemoryScramble
{
    /// <summary>
    /// Simulation of multiple concurrent players making random moves.
    /// Purpose: Verify no deadlocks or crashes occur under concurrent stress.
    /// </summary>
    public static class Simulation
    {
        public static async Task RunAsync()
        {
            string filename = "Boards/perfect.txt";
            Board board = await Board.ParseFromFileAsync(filename);

            int players = 4;
            int movesPerPlayer = 100;
            int minDelayMs = 1;  // 0.1ms rounded to 1ms minimum for Windows
            int maxDelayMs = 2;

            Console.WriteLine($"\n{'='} Starting Simulation {'='}");
            Console.WriteLine($"📋 Board: {filename}");
            Console.WriteLine($"👥 Players: {players}");
            Console.WriteLine($"🎮 Moves per player: {movesPerPlayer}");
            Console.WriteLine($"⏱️  Delay range: {minDelayMs}ms - {maxDelayMs}ms");
            Console.WriteLine($"{'='} Begin {'='}\n");

            var playerTasks = new List<Task>();
            for (int i = 0; i < players; i++)
            {
                int playerNumber = i;
                playerTasks.Add(PlayerAsync(board, playerNumber, movesPerPlayer, minDelayMs, maxDelayMs));
            }

            await Task.WhenAll(playerTasks);

            Console.WriteLine("\n" + new string('=', 50));
            Console.WriteLine("✅ Simulation completed successfully!");
            Console.WriteLine("🎉 No deadlocks or crashes detected!");
            Console.WriteLine(new string('=', 50));

            Console.WriteLine("\n📋 Final Board State:");
            var finalState = Commands.Look(board, "observer");
            Console.WriteLine(finalState);
        }

        /// <summary>
        /// Simulates a single player making random moves.
        /// </summary>
        private static async Task PlayerAsync(Board board, int playerNumber, int moves, int minDelayMs, int maxDelayMs)
        {
            string playerId = $"player{playerNumber}";
            var random = new Random(playerNumber); // Seed by player number for reproducibility

            Console.WriteLine($"▶️  {playerId} started");

            try
            {
                for (int move = 0; move < moves; move++)
                {
                    // Random delay between moves
                    int delayMs = random.Next(minDelayMs, maxDelayMs + 1);
                    await Task.Delay(delayMs);

                    // Random card position (3x3 board: 0-2)
                    int row = random.Next(0, 3);
                    int col = random.Next(0, 3);

                    try
                    {
                        var result = Commands.Flip(board, playerId, row, col);
                        // Optional: log verbose output
                        // Console.WriteLine($"  {playerId}: {result}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"⚠️  {playerId} error on move {move + 1}: {ex.Message}");
                    }
                }

                Console.WriteLine($"✅ {playerId} finished ({moves} moves)");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ {playerId} crashed: {ex.Message}");
                throw;
            }
        }
    }
}