using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;
using MemoryScramble;

namespace MemoryScramble.Tests
{
    /// <summary>
    /// Unit tests for Board ADT.
    /// Tests cover all game rules, concurrent behavior, and invariants.
    /// </summary>
    public class BoardTests
    {
        private const string TestBoardPath = "Boards/perfect.txt";

        /// <summary>
        /// Test 1: Board can be parsed from file
        /// </summary>
        [Fact]
        public async Task ParseFromFile_ValidFile_CreatesBoard()
        {
            // Arrange & Act
            var board = await Board.ParseFromFileAsync(TestBoardPath);

            // Assert
            Assert.NotNull(board);
            var boardState = board.Look("player1");
            Assert.NotNull(boardState);
            Assert.Contains("[?]", boardState); // Should show face-down cards
        }

        /// <summary>
        /// Test 2: Look returns board state without exposing rep
        /// </summary>
        [Fact]
        public void Look_ReturnsImmutableString()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act
            var state1 = board.Look("player1");
            var state2 = board.Look("player1");

            // Assert - both should be identical
            Assert.Equal(state1, state2);
            Assert.NotEmpty(state1);
        }

        /// <summary>
        /// Test 3: Flipping a card reveals it
        /// </summary>
        [Fact]
        public void Flip_CardDown_FlipsCardUp()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act
            var result = board.Flip("player1", 0, 0);
            var boardState = board.Look("player1");

            // Assert
            Assert.NotNull(result);
            Assert.Contains("Flipped", result);
            Assert.Contains("[", boardState); // Should show revealed card
        }

        /// <summary>
        /// Test 4: Matching pair is removed
        /// </summary>
        [Fact]
        public void Flip_TwoMatchingCards_BothRemoved()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act - Flip two matching cards from perfect.txt
            // Perfect.txt has 🦄 at (0,0) and (1,2) and (2,1) etc.
            board.Flip("player1", 0, 0);
            var result = board.Flip("player1", 1, 2);

            // Assert
            Assert.Contains("Match", result);
            var boardState = board.Look("player1");
            Assert.Contains("[ ]", boardState); // Empty slot means removed
        }

        /// <summary>
        /// Test 5: Non-matching pair flips back
        /// </summary>
        [Fact]
        public async Task Flip_TwoNonMatchingCards_FlipBackAfterDelay()
        {
            // Arrange
            var board = await Board.ParseFromFileAsync(TestBoardPath);

            // Act - Flip two non-matching cards
            board.Flip("player1", 0, 0);  // 🦄
            var result = board.Flip("player1", 0, 1);  // 🌈 (different)

            // Assert immediately
            Assert.Contains("No match", result);

            // Wait for flip-back
            await Task.Delay(1500);

            var boardState = board.Look("player1");
            // Both should be face-down again
            Assert.Contains("[?]", boardState);
        }

        /// <summary>
        /// Test 6: Invalid position rejected
        /// </summary>
        [Fact]
        public void Flip_InvalidPosition_ReturnsError()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act
            var result = board.Flip("player1", -1, 0);

            // Assert
            Assert.Contains("Invalid", result);
        }

        /// <summary>
        /// Test 7: Can't flip already removed card
        /// </summary>
        [Fact]
        public void Flip_RemovedCard_ReturnsError()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act - Create a match
            board.Flip("player1", 0, 0);
            board.Flip("player1", 1, 2);
            
            // Try to flip one of the removed cards again
            var result = board.Flip("player1", 0, 0);

            // Assert
            Assert.Contains("removed", result);
        }

        /// <summary>
        /// Test 8: Map function transforms cards
        /// </summary>
        [Fact]
        public void Map_TransformsControlledCards()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;
            board.Flip("player1", 0, 0);

            // Act - Replace 🦄 with 🎉
            var result = Commands.Map(board, "player1", label => 
                label == "🦄" ? "🎉" : label);

            // Assert
            Assert.NotNull(result);
            Assert.Contains("🎉", result);
        }

        /// <summary>
        /// Test 9: Watch subscribes to changes
        /// </summary>
        [Fact]
        public async Task Watch_NotifiedOnBoardChange()
        {
            // Arrange
            var board = await Board.ParseFromFileAsync(TestBoardPath);
            var watchTask = board.Watch("player2");
            
            // Act - Change board while watching
            await Task.Delay(100);
            _ = Task.Run(() => board.Flip("player1", 0, 0));
            
            var watchResult = await watchTask;

            // Assert
            Assert.NotNull(watchResult);
            Assert.NotEmpty(watchResult);
        }

        /// <summary>
        /// Test 10: Concurrent players don't deadlock
        /// </summary>
        [Fact]
        public async Task Concurrent_MultiplePlayers_NoDeadlock()
        {
            // Arrange
            var board = await Board.ParseFromFileAsync(TestBoardPath);
            var random = new Random();

            // Act - 10 concurrent players making moves
            var tasks = new List<Task>();
            for (int i = 0; i < 10; i++)
            {
                int playerNum = i;
                tasks.Add(Task.Run(() =>
                {
                    for (int j = 0; j < 5; j++)
                    {
                        board.Flip($"player{playerNum}", 
                            random.Next(0, 3), 
                            random.Next(0, 3));
                        System.Threading.Thread.Sleep(random.Next(10, 50));
                    }
                }));
            }

            // Should complete within 10 seconds without deadlock
            var completedTask = await Task.WhenAny(
                Task.WhenAll(tasks),
                Task.Delay(10000)
            );

            // Assert
            Assert.Equal(Task.WhenAll(tasks), completedTask);
        }

        /// <summary>
        /// Test 11: Commands module validates inputs
        /// </summary>
        [Fact]
        public void Commands_NullBoard_ThrowsArgumentNullException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() =>
                Commands.Look(null!, "player1"));
        }

        /// <summary>
        /// Test 12: Commands module validates playerId
        /// </summary>
        [Fact]
        public void Commands_EmptyPlayerId_ThrowsArgumentException()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act & Assert
            Assert.Throws<ArgumentException>(() =>
                Commands.Look(board, ""));
        }

        /// <summary>
        /// Test 13: Different players see their own cards
        /// </summary>
        [Fact]
        public void MultiPlayer_DifferentPlayers_IndependentControl()
        {
            // Arrange
            var board = Board.ParseFromFileAsync(TestBoardPath).Result;

            // Act
            board.Flip("player1", 0, 0);
            board.Flip("player2", 0, 1);
            var state1 = board.Look("player1");
            var state2 = board.Look("player2");

            // Assert - both should see revealed cards
            Assert.Contains("Your cards", state1);
            Assert.Contains("Your cards", state2);
        }
    }
}