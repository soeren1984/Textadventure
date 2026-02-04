using System;

namespace SchradinsAdventure
{
    internal enum EncounterType { None, Rat, Scroll, Steps, Loot, Fight, Trap, Heal, Chest, Boss, Npc }
    internal enum EncounterState { None, Started, Resolved }

    internal sealed partial class World
    {
        public void BuildFixed(string? preset = null) => Generate();
    }
}

namespace TextAbenteuer.Models
{
    public struct Position
    {
        public int X; public int Y;
        public Position(int x, int y) { X = x; Y = y; }
    }
}
