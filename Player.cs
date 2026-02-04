using TextAbenteuer.Models;

namespace SchradinsAdventure
{
    internal sealed class Player
    {
        public int X { get; set; }
        public int Y { get; set; }

        public int HP { get; private set; }
        public int MaxHP { get; set; } = 10;

        public int Hp
        {
            get => HP;
            set => SetHP(value);
        }

        public Position Position
        {
            get => new Position(X, Y);
            set { X = value.X; Y = value.Y; }
        }

        public int XP { get; private set; }
        public int Level { get; private set; } = 1;

        public Player(int x, int y) { X = x; Y = y; HP = MaxHP; }
        public Player(int x, int y, int hp) { X = x; Y = y; MaxHP = hp > 0 ? hp : 1; HP = MaxHP; }

        internal void SetHP(int value) => HP = Math.Clamp(value, 0, Math.Max(1, MaxHP));
        public void Damage(int dmg) { HP -= dmg; if (HP < 0) HP = 0; }

        public void GainXP(int amount)
        {
            XP += amount;
            int needed = 10 + (Level - 1) * 10;
            while (XP >= needed)
            {
                XP -= needed; Level++; MaxHP += 1; HP = MaxHP;
                needed = 10 + (Level - 1) * 10;
            }
        }

        public SavePlayer ToSave() => new() { X = X, Y = Y, HP = HP, MaxHP = MaxHP, XP = XP, Level = Level };

        public static Player LoadFromSave(SaveData s)
        {
            var p = s.Player!;
            var pl = new Player(p.X, p.Y) { MaxHP = p.MaxHP };
            pl.HP = p.HP; pl.XP = p.XP; pl.Level = p.Level;
            return pl;
        }
    }
}
