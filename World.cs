using System;

namespace SchradinsAdventure
{
    internal static class Tiles
    {
        public const char Wall = '#';
        public const char Floor = '.';
        public const char Player = '@';
        public const char DoorClosed = '+';
        public const char DoorOpen = '/';
        public const char Boss = 'B';
        public const char KeyMonster = 'M';
    }

    internal enum Cell : byte { Wall = 0, Floor = 1 }

    internal sealed partial class World
    {
        public int Width { get; }
        public int Height { get; }
        public int Seed { get; }
        private readonly Cell[,] _cells;
        private readonly Random _rng;

        // Spezielle Orte
        public (int x, int y) BossPos { get; private set; }
        public (int x, int y) DoorPos { get; private set; }
        public (int x, int y) KeyMonsterPos { get; private set; }
        public bool DoorIsOpen { get; private set; }

        public World(int w, int h, int seed)
        {
            Width = w; Height = h; Seed = seed;
            _cells = new Cell[w, h];
            _rng = new Random(seed);
        }

        public void Generate()
        {
            // 1) alles Wand
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    _cells[x, y] = Cell.Wall;

            // 2) drunkard walk + Räume
            int rx = Width / 2, ry = Height / 2;
            int steps = Width * Height * 2;

            for (int i = 0; i < steps; i++)
            {
                _cells[rx, ry] = Cell.Floor;

                int dir = _rng.Next(4);
                if (dir == 0) rx++;
                else if (dir == 1) rx--;
                else if (dir == 2) ry++;
                else ry--;

                rx = Math.Clamp(rx, 1, Width - 2);
                ry = Math.Clamp(ry, 1, Height - 2);

                if (_rng.NextDouble() < 0.10)
                {
                    int rw = _rng.Next(3, 8);
                    int rh = _rng.Next(3, 6);
                    for (int yy = ry - rh / 2; yy <= ry + rh / 2; yy++)
                        for (int xx = rx - rw / 2; xx <= rx + rw / 2; xx++)
                            if (xx > 1 && yy > 1 && xx < Width - 2 && yy < Height - 2)
                                _cells[xx, yy] = Cell.Floor;
                }
            }

            // 3) Außenrahmen als Wand
            for (int x = 0; x < Width; x++) { _cells[x, 0] = Cell.Wall; _cells[x, Height - 1] = Cell.Wall; }
            for (int y = 0; y < Height; y++) { _cells[0, y] = Cell.Wall; _cells[Width - 1, y] = Cell.Wall; }

            // 4) Bossraum unten rechts
            int rw2 = 14, rh2 = 10;
            int rx2 = Width - rw2 - 2;
            int ry2 = Height - rh2 - 2;

            for (int y = ry2; y < ry2 + rh2; y++)
                for (int x = rx2; x < rx2 + rw2; x++)
                    _cells[x, y] = Cell.Floor;

            // Tür vor dem Bossraum (linke Wand mittig)
            int doorY = ry2 + rh2 / 2;
            int doorX = rx2 - 1;
            DoorPos = (doorX, doorY);
            DoorIsOpen = false;

            // Boss im Raum zentriert
            BossPos = (rx2 + rw2 / 2, ry2 + rh2 / 2);

            // Schlüssel-Monster irgendwo außerhalb des Bossraums
            KeyMonsterPos = FindRandomFloor(pos =>
                !(pos.x >= rx2 - 1 && pos.x <= rx2 + rw2 && pos.y >= ry2 - 1 && pos.y <= ry2 + rh2));

            // Korridor zur Tür freihalten
            for (int x = doorX - 6; x <= doorX; x++)
                _cells[Math.Clamp(x, 1, Width - 2), doorY] = Cell.Floor;
        }

        private (int x, int y) FindRandomFloor(Func<(int x, int y), bool>? predicate = null)
        {
            for (int i = 0; i < 10000; i++)
            {
                int x = _rng.Next(1, Width - 1);
                int y = _rng.Next(1, Height - 1);
                if (_cells[x, y] == Cell.Floor && (predicate?.Invoke((x, y)) ?? true))
                    return (x, y);
            }
            // Fallback
            for (int y = 1; y < Height - 1; y++)
                for (int x = 1; x < Width - 1; x++)
                    if (_cells[x, y] == Cell.Floor) return (x, y);
            return (1, 1);
        }

        public void OpenDoor() => DoorIsOpen = true;

        public (int x, int y) GetSpawnPoint()
        {
            for (int r = 0; r < Math.Max(Width, Height) / 2; r++)
            {
                for (int y = Height / 2 - r; y <= Height / 2 + r; y++)
                    for (int x = Width / 2 - r; x <= Width / 2 + r; x++)
                    {
                        if (x < 1 || y < 1 || x >= Width - 1 || y >= Height - 1) continue;
                        if (_cells[x, y] == Cell.Floor) return (x, y);
                    }
            }
            return (1, 1);
        }

        public bool IsWall(int x, int y)
        {
            if (!DoorIsOpen && (x, y) == DoorPos) return true;
            return _cells[x, y] == Cell.Wall;
        }

        public bool IsWalkable(int x, int y) => !IsWall(x, y);

        // ---------- Save/Load (neu) ----------
        public SaveData ToSave(Player p) => new SaveData
        {
            Seed = Seed,
            Width = Width,
            Height = Height,
            Player = p.ToSave()
            // Tür/Boss/Monster werden beim Laden bewusst neu platziert (balancing-sicher)
        };

        public static World LoadFromSave(SaveData s)
        {
            var w = new World(s.Width, s.Height, s.Seed);
            w.Generate(); // setzt Boss/Tür/Monster frisch
            return w;
        }
    }
}
