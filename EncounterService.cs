using System;

namespace SchradinsAdventure
{
    internal class EncounterService
    {
        private static readonly Random _rng = new();

        public EncounterService() { }
        public EncounterService(object? _ignored) { }
        public EncounterService(int _ignored) { }

        public static string? TryEncounter(Player player, int chancePercent)
        {
            if (_rng.Next(100) >= chancePercent) return null;

            int roll = _rng.Next(3);
            return roll switch
            {
                0 => "Du hörst ein fernes Knurren...",
                1 => "Die Luft wird kälter.",
                _ => "Es raschelt in der Ferne."
            };
        }

        public int Roll(int sides = 100)
            => (sides <= 1) ? 1 : _rng.Next(1, sides + 1);

        public bool ResolveDisarm(Player p, int difficulty = 50)
        {
            int test = Roll(100);
            return test >= Math.Clamp(difficulty, 1, 99);
        }
    }
}
