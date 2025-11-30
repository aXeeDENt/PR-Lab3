using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace MemoryScramble
{
    /// <summary>
    /// Represents the state of a card on the board.
    /// </summary>
    public enum CardState
    {
        Down,    // Face down, not yet flipped
        Up,      // Face up, currently visible
        Removed  // Matched pair, removed from play
    }

    /// <summary>
    /// Immutable record representing a card's label and state.
    /// </summary>
    public sealed class Card
    {
        public string Label { get; set; } = string.Empty;
        public CardState State { get; set; } = CardState.Down;
        public string? ControlledBy { get; set; } = null;

        public Card Copy()
        {
            return new Card
            {
                Label = this.Label,
                State = this.State,
                ControlledBy = this.ControlledBy
            };
        }
    }

    /// <summary>
    /// Immutable record of a card position on the board.
    /// </summary>
    public sealed record CardPosition(int Row, int Column);

    /// <summary>
    /// Board ADT for Memory Scramble game.
    /// 
    /// Representation Invariant (RI):
    /// - _width > 0 and _height > 0
    /// - _cards.Length == _height
    /// - Each _cards[i].Length == _width
    /// - Each card in _cards is not null
    /// - _playerCards keys are valid player IDs (non-empty strings)
    /// - _playerCards values are valid positions within the board
    /// - _previousCards keys are valid player IDs
    /// - All watchers in _watchers are non-null TaskCompletionSource objects
    /// - _waitingForCard keys are valid player IDs
    /// 
    /// Abstraction Function (AF):
    /// The board represents a grid of cards where:
    /// - Each card has a label, state (Down/Up/Removed), and optional controller
    /// - Players can flip cards, which may be controlled by them
    /// - Matching pairs are removed; mismatches flip back down
    /// - Multiple players can interact concurrently with waiting queues
    /// 
    /// Safety from Rep Exposure:
    /// - All fields are private
    /// - _cards is never directly exposed; board state returned as immutable string
    /// - _playerCards, _previousCards, _watchers, _waitingForCard are private
    /// - CheckRep() validates invariants at key points
    /// </summary>
    public sealed class Board
    {
        private readonly int _width;
        private readonly int _height;
        private readonly Card[][] _cards;

        // Tracks which cards each player controls
        private readonly Dictionary<string, List<CardPosition>> _playerCards = new();

        // Tracks previous card flips for each player (for matching logic)
        private readonly Dictionary<string, (List<CardPosition> Positions, bool Matched)> _previousCards = new();

        // Watchers subscribed to board changes
        private readonly HashSet<TaskCompletionSource<object?>> _watchers = new();

        // Queues of players waiting to flip specific cards
        private readonly Dictionary<string, Queue<WaitingEntry>> _waitingForCard = new();

        private readonly object _lock = new();

        private sealed class WaitingEntry
        {
            public TaskCompletionSource<object?> Tcs { get; }
            public CardPosition Position { get; }

            public WaitingEntry(TaskCompletionSource<object?> tcs, CardPosition position)
            {
                Tcs = tcs;
                Position = position;
            }
        }

        /// <summary>
        /// Private constructor. Use ParseFromFileAsync to create instances.
        /// </summary>
        private Board(int width, int height, Card[][] cards)
        {
            _width = width;
            _height = height;
            _cards = cards;
            CheckRep();
        }

        /// <summary>
        /// Parses a board from a file asynchronously.
        /// 
        /// Requires: filename is a valid file path with format:
        ///   [WIDTH]x[HEIGHT]
        ///   [LABEL_1]
        ///   [LABEL_2]
        ///   ...
        ///   [LABEL_N] where N = WIDTH * HEIGHT
        /// 
        /// Effects: Reads file and creates a new Board with all cards face down.
        /// Throws: IOException if file not found; FormatException if format invalid.
        /// </summary>
        public static async Task<Board> ParseFromFileAsync(string filename)
        {
            var lines = await File.ReadAllLinesAsync(filename);
            if (lines.Length < 2)
                throw new FormatException("File must have at least 2 lines");

            var dimensions = lines[0].Split('x');
            if (dimensions.Length != 2 || !int.TryParse(dimensions[0], out int width) || !int.TryParse(dimensions[1], out int height))
                throw new FormatException("First line must be in format: WIDTHxHEIGHT");

            if (width <= 0 || height <= 0)
                throw new FormatException("Width and height must be positive");

            int expectedCards = width * height;
            if (lines.Length - 1 != expectedCards)
                throw new FormatException($"Expected {expectedCards} card labels, found {lines.Length - 1}");

            var cards = new Card[height][];
            int cardIndex = 1;

            for (int row = 0; row < height; row++)
            {
                cards[row] = new Card[width];
                for (int col = 0; col < width; col++)
                {
                    cards[row][col] = new Card
                    {
                        Label = lines[cardIndex].Trim(),
                        State = CardState.Down,
                        ControlledBy = null
                    };
                    cardIndex++;
                }
            }

            return new Board(width, height, cards);
        }

        /// <summary>
        /// Returns the current board state as an immutable string.
        /// 
        /// Requires: playerId is a non-empty string
        /// Effects: Returns a string representation of the board visible to the player
        /// </summary>
        public string Look(string playerId)
        {
            lock (_lock)
            {
                CheckRep();
                var result = new System.Text.StringBuilder();
                result.AppendLine($"Board ({_width}x{_height}):");
                result.AppendLine();

                for (int row = 0; row < _height; row++)
                {
                    for (int col = 0; col < _width; col++)
                    {
                        var card = _cards[row][col];
                        string display = card.State switch
                        {
                            CardState.Down => "[?]",
                            CardState.Up => $"[{card.Label}]",
                            CardState.Removed => "[ ]",
                            _ => "[X]"
                        };
                        result.Append(display);
                        if (col < _width - 1) result.Append(" ");
                    }
                    result.AppendLine();
                }

                result.AppendLine();
                result.AppendLine($"Your cards: {string.Join(", ", _playerCards.ContainsKey(playerId) ? _playerCards[playerId].Select(p => $"({p.Row},{p.Column})") : new List<string>())}");

                CheckRep();
                return result.ToString();
            }
        }

        /// <summary>
        /// Flips a card at the given position.
        /// 
        /// Requires: playerId is non-empty; 0 <= row < _height; 0 <= col < _width
        /// Effects: If card is Down, flips it Up and assigns control to playerId.
        ///   If this is the second card for playerId:
        ///     - If cards match: both stay Up and Removed
        ///     - If cards don't match: both flip back Down after 1 second
        ///   Updates watchers with new board state.
        /// Returns: String description of flip result
        /// </summary>
        public string Flip(string playerId, int row, int col)
        {
            if (row < 0 || row >= _height || col < 0 || col >= _width)
                return "Invalid position";

            lock (_lock)
            {
                CheckRep();
                var card = _cards[row][col];

                // If card is already removed, can't flip
                if (card.State == CardState.Removed)
                    return "Card already removed";

                // If card is Up and controlled by another player, can't flip now
                if (card.State == CardState.Up && card.ControlledBy != playerId)
                    return "Card is controlled by another player";

                // If card is Down, flip it up
                if (card.State == CardState.Down)
                {
                    card.State = CardState.Up;
                    card.ControlledBy = playerId;

                    if (!_playerCards.ContainsKey(playerId))
                        _playerCards[playerId] = new List<CardPosition>();

                    _playerCards[playerId].Add(new CardPosition(row, col));

                    // Check if this is second card
                    if (_playerCards[playerId].Count == 2)
                    {
                        var positions = _playerCards[playerId];
                        var card1 = _cards[positions[0].Row][positions[0].Column];
                        var card2 = _cards[positions[1].Row][positions[1].Column];

                        if (card1.Label == card2.Label)
                        {
                            // Match! Cards stay up and marked as removed
                            card1.State = CardState.Removed;
                            card2.State = CardState.Removed;

                            if (!_previousCards.ContainsKey(playerId))
                                _previousCards[playerId] = (new List<CardPosition>(), false);
                            _previousCards[playerId] = (positions, true);

                            _playerCards[playerId].Clear();
                            NotifyWatchers();
                            CheckRep();
                            return $"Match! Cards {positions[0]} and {positions[1]} removed.";
                        }
                        else
                        {
                            // No match, flip back after delay
                            var p0 = positions[0];
                            var p1 = positions[1];
                            _ = Task.Run(async () =>
                            {
                                await Task.Delay(1000);
                                lock (_lock)
                                {
                                    _cards[p0.Row][p0.Column].State = CardState.Down;
                                    _cards[p0.Row][p0.Column].ControlledBy = null;
                                    _cards[p1.Row][p1.Column].State = CardState.Down;
                                    _cards[p1.Row][p1.Column].ControlledBy = null;
                                    _playerCards[playerId].Clear();

                                    NotifyWatchers();
                                    CheckRep();
                                }
                            });

                            if (!_previousCards.ContainsKey(playerId))
                                _previousCards[playerId] = (new List<CardPosition>(), false);
                            _previousCards[playerId] = (positions, false);

                            NotifyWatchers();
                            CheckRep();
                            return $"No match. Cards will flip back.";
                        }
                    }

                    NotifyWatchers();
                    CheckRep();
                    return $"Flipped card at ({row},{col}): {card.Label}";
                }

                CheckRep();
                return "Card already flipped";
            }
        }

        /// <summary>
        /// Applies a transformation function to all cards controlled by the player.
        /// 
        /// Requires: playerId is non-empty; transform is a non-null function
        /// Effects: For each card controlled by playerId, replaces label via transform function
        /// Returns: String showing results
        /// </summary>
        public string Map(string playerId, Func<string, string> transform)
        {
            lock (_lock)
            {
                CheckRep();

                if (!_playerCards.ContainsKey(playerId) || _playerCards[playerId].Count == 0)
                {
                    CheckRep();
                    return "No cards controlled";
                }

                var positions = _playerCards[playerId];
                var results = new List<string>();

                foreach (var pos in positions)
                {
                    var card = _cards[pos.Row][pos.Column];
                    var oldLabel = card.Label;
                    var newLabel = transform(card.Label);
                    card.Label = newLabel;
                    results.Add($"({pos.Row},{pos.Column}): {oldLabel} → {newLabel}");
                }

                NotifyWatchers();
                CheckRep();
                return string.Join("; ", results);
            }
        }

        /// <summary>
        /// Subscribes to board change notifications.
        /// 
        /// Requires: playerId is non-empty
        /// Effects: Returns a task that completes when the board changes
        /// </summary>
        public async Task<string> Watch(string playerId)
        {
            TaskCompletionSource<object?> tcs = new();

            lock (_lock)
            {
                _watchers.Add(tcs);
            }

            await tcs.Task;

            lock (_lock)
            {
                _watchers.Remove(tcs);
            }

            return Look(playerId);
        }

        /// <summary>
        /// Notifies all watchers that the board has changed.
        /// Must be called while holding _lock.
        /// </summary>
        private void NotifyWatchers()
        {
            var watchersToNotify = _watchers.ToList();
            foreach (var watcher in watchersToNotify)
            {
                watcher.TrySetResult(null);
                _watchers.Remove(watcher);
            }
        }

        /// <summary>
        /// Validates the representation invariant.
        /// Throws InvalidOperationException if invariant is violated.
        /// </summary>
        private void CheckRep()
        {
            if (!(_width > 0 && _height > 0))
                throw new InvalidOperationException("Width and height must be positive");

            if (_cards.Length != _height)
                throw new InvalidOperationException("Cards array length must equal height");

            for (int i = 0; i < _height; i++)
            {
                if (_cards[i].Length != _width)
                    throw new InvalidOperationException("Each card row must have width columns");

                for (int j = 0; j < _width; j++)
                {
                    if (_cards[i][j] == null)
                        throw new InvalidOperationException("No card can be null");
                }
            }

            foreach (var kvp in _playerCards)
            {
                foreach (var pos in kvp.Value)
                {
                    if (pos.Row < 0 || pos.Row >= _height || pos.Column < 0 || pos.Column >= _width)
                        throw new InvalidOperationException($"Invalid player card position: {pos}");
                }
            }
        }
    }
}