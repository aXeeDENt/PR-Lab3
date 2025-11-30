using System;
using System.Threading.Tasks;

namespace MemoryScramble
{
    /// <summary>
    /// Commands module - glue layer between web server and Board ADT.
    /// All functions delegate to Board methods and follow the game specification.
    /// </summary>
    public static class Commands
    {
        /// <summary>
        /// Returns the current board state visible to the player.
        /// 
        /// Requires: board is non-null; playerId is non-empty
        /// Effects: Returns immutable string representation of board
        /// </summary>
        public static string Look(Board board, string playerId)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("playerId cannot be empty");

            return board.Look(playerId);
        }

        /// <summary>
        /// Attempts to flip a card at the given position.
        /// 
        /// Requires: board is non-null; playerId is non-empty; 
        ///   0 <= row < board height; 0 <= col < board width
        /// Effects: Flips card Up if Down, may trigger matching logic,
        ///   may cause mismatch flip-back after 1 second
        /// </summary>
        public static string Flip(Board board, string playerId, int row, int column)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("playerId cannot be empty");

            return board.Flip(playerId, row, column);
        }

        /// <summary>
        /// Applies a card transformation function to all cards controlled by player.
        /// Similar to Array.map() but for board cards.
        /// 
        /// Requires: board is non-null; playerId is non-empty; 
        ///   transform is non-null function
        /// Effects: Replaces each controlled card's label via transform function
        /// </summary>
        public static string Map(Board board, string playerId, Func<string, string> transform)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("playerId cannot be empty");
            if (transform == null)
                throw new ArgumentNullException(nameof(transform));

            return board.Map(playerId, transform);
        }

        /// <summary>
        /// Subscribes to board change notifications.
        /// 
        /// Requires: board is non-null; playerId is non-empty
        /// Effects: Waits until board changes, then returns updated board state
        /// </summary>
        public static async Task<string> Watch(Board board, string playerId)
        {
            if (board == null)
                throw new ArgumentNullException(nameof(board));
            if (string.IsNullOrEmpty(playerId))
                throw new ArgumentException("playerId cannot be empty");

            return await board.Watch(playerId);
        }
    }
}