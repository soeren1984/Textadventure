using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace SchradinsAdventure
{
    internal static class Program
    {
        // ---- Layout ----
        public const int HUD_H = 1;
        public const int MAP_W = 80;   // Breite
        public const int MAP_H = 50;   // Höhe
        public const int LOG_H = 1;    // exakt eine Zeile
        public const int PAD = 2;

        public static int MinW => MAP_W + PAD;
        public static int MinH => HUD_H + MAP_H + LOG_H + PAD;

        private static World _world = null!;
        private static Player _player = null!;
        private static readonly LogBuffer _log = new(1);
        private static readonly Random _rng = new();

        // Rendersteuerung
        private static bool _running = true;
        private static bool _dirty = true;
        private static int _prevW = -1, _prevH = -1;

        // Spielzustand / Inventar
        private static bool _hasKey = false;
        private static bool _hasSword = false;
        private static bool _hasArmor = false;

        private static bool _keyMonsterAlive = true;
        private static int _keyMonsterHP = 5;   // Standard-Mob
        private static int _bossHP = 12;  // Balancing: ~6 Treffer mit Schwert / 12 ohne

        private static int _stepsSinceItem = 0;   // „spätestens alle 20 Felder ein Objekt“

        // Positionen besonderer Dinge (aus World platziert)
        private static (int x, int y) _keyMonsterPos;
        private static (int x, int y) _bossPos;
        private static (int x, int y) _doorPos;

        private static void Main()
        {
            InitConsole();
            StartNewGame();

            while (_running)
            {
                if (CheckSizeChanged()) _dirty = true;

                if (Console.KeyAvailable)
                {
                    if (HandleInput()) _dirty = true;
                }
                else
                {
                    Thread.Sleep(10);
                }

                if (_dirty)
                {
                    var (offX, offY) = ComputeOffsets();
                    Render(offX, offY);
                    _dirty = false;
                }
            }

            Console.CursorVisible = true;
        }

        private static void StartNewGame()
        {
            int seed = _rng.Next();
            _world = new World(MAP_W, MAP_H, seed);
            _world.Generate();

            // Positionen aus der Welt übernehmen
            _bossPos = _world.BossPos;
            _doorPos = _world.DoorPos;
            _keyMonsterPos = _world.KeyMonsterPos;

            var (px, py) = _world.GetSpawnPoint();
            _player = new Player(px, py);

            _hasKey = _hasSword = _hasArmor = false;
            _keyMonsterAlive = true;
            _keyMonsterHP = 5;
            _bossHP = 12;
            _stepsSinceItem = 0;

            _log.Add("Du betrittst den Dungeon.");
            _dirty = true;
        }

        // ---------- Konsole / Fenster ----------
        private static void InitConsole()
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.CursorVisible = false;

            Console.SetBufferSize(Math.Max(MinW, Console.BufferWidth), Math.Max(MinH, Console.BufferHeight));
            Console.SetWindowSize(Math.Max(MinW, Math.Min(Console.WindowWidth, Console.LargestWindowWidth)),
                                  Math.Max(MinH, Math.Min(Console.WindowHeight, Console.LargestWindowHeight)));
            Console.SetBufferSize(Math.Max(Console.BufferWidth, Console.WindowWidth),
                                  Math.Max(Console.BufferHeight, Console.WindowHeight));

            _prevW = Console.WindowWidth;
            _prevH = Console.WindowHeight;

            Console.TreatControlCAsInput = true;
        }

        private static bool CheckSizeChanged()
        {
            bool clamped = false;
            if (Console.WindowWidth < MinW || Console.WindowHeight < MinH)
            {
                try
                {
                    int newW = Math.Max(MinW, Math.Min(Console.LargestWindowWidth, Console.WindowWidth));
                    int newH = Math.Max(MinH, Math.Min(Console.LargestWindowHeight, Console.WindowHeight));
                    Console.SetBufferSize(Math.Max(newW, Console.BufferWidth), Math.Max(newH, Console.BufferHeight));
                    Console.SetWindowSize(newW, newH);
                    Console.SetBufferSize(Math.Max(Console.BufferWidth, Console.WindowWidth),
                                          Math.Max(Console.BufferHeight, Console.WindowHeight));
                    clamped = true;
                }
                catch { }
            }

            if (clamped || Console.WindowWidth != _prevW || Console.WindowHeight != _prevH)
            {
                _prevW = Console.WindowWidth;
                _prevH = Console.WindowHeight;
                Console.Clear();   // nur bei echter Größenänderung
                return true;
            }
            return false;
        }

        private static (int offX, int offY) ComputeOffsets()
        {
            int totalH = HUD_H + MAP_H + LOG_H;
            int offX = Math.Max(0, (Console.WindowWidth - MAP_W) / 2);
            int offY = Math.Max(0, (Console.WindowHeight - totalH) / 2);
            return (offX, offY);
        }

        // ---------- Rendering ----------
        private static void Render(int offX, int offY)
        {
            // HUD (eine Zeile, fest)
            string hud = $"HP: {_player.HP}/{_player.MaxHP}  Pos: {_player.X},{_player.Y}  Key:{(_hasKey ? '✓' : '✗')}  Sword:{(_hasSword ? '✓' : '✗')}  Armor:{(_hasArmor ? '✓' : '✗')}  Enc: 8%";
            WriteAt(offX, offY, ClipPad(hud, MAP_W));

            // MAP (ohne Fog-of-War)
            for (int y = 0; y < MAP_H; y++)
            {
                var row = new char[MAP_W];
                for (int x = 0; x < MAP_W; x++)
                {
                    char ch = _world.IsWall(x, y) ? Tiles.Wall : Tiles.Floor;

                    // Tür überlagert
                    if (x == _doorPos.x && y == _doorPos.y)
                        ch = _world.DoorIsOpen ? Tiles.DoorOpen : Tiles.DoorClosed;

                    // Boss
                    if ((x, y) == _bossPos && _bossHP > 0)
                        ch = Tiles.Boss;

                    // Schlüssel-Monster
                    if (_keyMonsterAlive && (x, y) == _keyMonsterPos)
                        ch = Tiles.KeyMonster;

                    // Spieler zuletzt zeichnen
                    if (x == _player.X && y == _player.Y)
                        ch = Tiles.Player;

                    row[x] = ch;
                }
                WriteAt(offX, offY + HUD_H + y, new string(row));
            }

            // LOG (eine Zeile direkt unter der Map)
            int logTop = offY + HUD_H + MAP_H;
            WriteAt(offX, logTop, ClipPad(_log.TryPeek(out var msg) ? msg : "", MAP_W));
        }

        private static void WriteAt(int x, int y, string text)
        {
            int max = Math.Max(0, Math.Min(MAP_W, Math.Max(0, Console.WindowWidth - x - 1)));
            string s = text.Length > max ? text[..max] : text.PadRight(max);
            if (y < 0 || y >= Console.WindowHeight) return;
            if (x < 0 || x >= Console.WindowWidth - 1) return;
            Console.SetCursorPosition(x, y);
            Console.Write(s);
        }

        private static string ClipPad(string s, int maxWidth)
            => s.Length > maxWidth ? s[..maxWidth] : s.PadRight(maxWidth);

        // ---------- Input ----------
        private static bool HandleInput()
        {
            var key = Console.ReadKey(intercept: true).Key;
            if (key == ConsoleKey.Q) { _running = false; return false; }

            switch (key)
            {
                case ConsoleKey.W: return TryMove(0, -1);
                case ConsoleKey.S: return TryMove(0, 1);
                case ConsoleKey.A: return TryMove(-1, 0);
                case ConsoleKey.D: return TryMove(1, 0);

                case ConsoleKey.F5:
                    SaveService.Save(_player, _world);
                    _log.Add("Gespeichert."); return true;

                case ConsoleKey.F9:
                    if (File.Exists(SaveService.DefaultPath))
                    {
                        var save = SaveService.Load();
                        _world = World.LoadFromSave(save);
                        _player = Player.LoadFromSave(save);

                        // Sonderobjekte neu initialisieren nach Load
                        _bossPos = _world.BossPos;
                        _doorPos = _world.DoorPos;
                        _keyMonsterPos = _world.KeyMonsterPos;

                        _log.Add("Geladen (Sonderobjekte neu gesetzt).");
                    }
                    else _log.Add("Kein Spielstand gefunden.");
                    return true;

                case ConsoleKey.H:
                    _log.Add("WASD bewegen • F5 speichern • F9 laden • Q beenden");
                    return true;

                default: return false;
            }
        }

        private static bool TryMove(int dx, int dy)
        {
            int nx = Math.Clamp(_player.X + dx, 0, MAP_W - 1);
            int ny = Math.Clamp(_player.Y + dy, 0, MAP_H - 1);

            // Türprüfung: geschlossene Tür blockiert
            if (!_world.DoorIsOpen && (nx, ny) == _doorPos)
            {
                if (_hasKey)
                {
                    _world.OpenDoor();
                    _log.Add("Du öffnest die Tür mit dem Schlüssel.");
                }
                else
                {
                    _log.Add("Die Tür ist verschlossen. Du brauchst einen Schlüssel.");
                    return true;
                }
            }

            if (_world.IsWall(nx, ny)) { _log.Add("Wand."); return true; }

            _player.X = nx; _player.Y = ny;

            // Bossfight, wenn auf Bossfeld
            if ((nx, ny) == _bossPos && _bossHP > 0)
            {
                FightBoss();
                CheckEndConditions();
                return true;
            }

            // Schlüssel-Monsterkampf, wenn auf Feld
            if (_keyMonsterAlive && (nx, ny) == _keyMonsterPos)
            {
                FightKeyMonster();
                CheckEndConditions();
                return true;
            }

            // Encounter 8%
            if (_rng.Next(100) < 8)
            {
                SmallEncounter();
            }

            // Spätestens alle 20 Schritte: Item
            _stepsSinceItem++;
            if (_stepsSinceItem >= 20)
            {
                ForceItem();
                _stepsSinceItem = 0;
            }

            CheckEndConditions();
            return true;
        }

        private static void SmallEncounter()
        {
            int r = _rng.Next(3);
            switch (r)
            {
                case 0:
                    _log.Add("Du hörst ein fernes Knurren...");
                    break;
                case 1:
                    _log.Add("Eine kalte Brise fegt durch den Gang.");
                    break;
                default:
                    int heal = 1;
                    _player.Hp = Math.Min(_player.MaxHP, _player.HP + heal);
                    _log.Add($"+{heal} HP durch eine Kräutersalbe.");
                    break;
            }
        }

        private static void ForceItem()
        {
            int r = _rng.Next(3); // 0=Heilung, 1=Schwert, 2=Rüstung
            if (r == 0)
            {
                int heal = 5;
                int before = _player.HP;
                _player.Hp = Math.Min(_player.MaxHP, _player.HP + heal);
                _log.Add($"Du findest einen Heiltrank (+{_player.HP - before} HP).");
            }
            else if (r == 1)
            {
                if (_hasSword) { _log.Add("Du findest ein Schwert, aber deins ist besser. Du lässt es liegen."); }
                else { _hasSword = true; _log.Add("Du findest ein Schwert (+1 Schaden)."); }
            }
            else
            {
                if (_hasArmor) { _log.Add("Du findest eine Rüstung, hast aber bereits eine."); }
                else { _hasArmor = true; _player.MaxHP += 10; _log.Add("Du ziehst eine Rüstung an (+10 MaxHP)."); }
            }
        }

        private static void FightKeyMonster()
        {
            while (_keyMonsterHP > 0 && _player.HP > 0)
            {
                int dmgPlayer = _hasSword ? 2 : 1;
                _keyMonsterHP -= dmgPlayer;

                if (_keyMonsterHP <= 0) break;

                int dmgMob = 1;
                _player.Damage(dmgMob);
            }

            if (_player.HP <= 0)
            {
                _log.Add("Das Monster besiegt dich...");
                return;
            }

            _keyMonsterAlive = false;
            _hasKey = true;
            _log.Add("Du besiegst das Monster und erhältst den Schlüssel!");
        }

        private static void FightBoss()
        {
            while (_bossHP > 0 && _player.HP > 0)
            {
                int dmgPlayer = _hasSword ? 2 : 1;
                _bossHP -= dmgPlayer;
                if (_bossHP <= 0) break;

                int dmgBoss = 2;
                _player.Damage(dmgBoss);
            }

            if (_player.HP <= 0)
            {
                _log.Add("Der Boss war zu stark... Du fällst.");
                return;
            }

            _log.Add("Du hast den Boss besiegt! Sieg!");
            _running = false; // Spiel gewonnen → beenden
        }

        private static void CheckEndConditions()
        {
            if (_player.HP <= 0)
            {
                _log.Add("Du bist gefallen. Game Over.");
                _running = false;
            }
        }
    }

    internal sealed class LogBuffer
    {
        private readonly Queue<string> _q = new();
        private readonly int _cap;
        public LogBuffer(int capacity) => _cap = Math.Max(1, capacity);

        public void Add(string msg)
        {
            _q.Enqueue(msg);
            while (_q.Count > _cap) _q.Dequeue();
        }

        public bool TryPeek(out string message)
        {
            if (_q.Count > 0) { message = _q.Last(); return true; }
            message = string.Empty; return false;
        }
    }
}
