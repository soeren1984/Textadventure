using System.IO;
using System.Text.Json;

namespace SchradinsAdventure
{
    internal class SaveService
    {
        public static string DefaultPath => "save.json";

        public static void Save(Player p, World w)
        {
            var data = w.ToSave(p); // World.ToSave existiert jetzt
            var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(DefaultPath, json);
        }

        public static SaveData Load()
        {
            var json = File.ReadAllText(DefaultPath);
            return JsonSerializer.Deserialize<SaveData>(json)!;
        }
    }

    internal sealed class SaveData
    {
        public int Seed { get; set; }
        public int Width { get; set; }
        public int Height { get; set; }
        public SavePlayer? Player { get; set; }
    }

    internal sealed class SavePlayer
    {
        public int X { get; set; }
        public int Y { get; set; }
        public int HP { get; set; }
        public int MaxHP { get; set; }
        public int XP { get; set; }
        public int Level { get; set; }
    }
}
