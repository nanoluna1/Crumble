using System.IO;
using MelonLoader;
using MelonLoader.Preferences;

namespace Main
{
    public static class Preferences
    {
        private const string Dir = "UserData/Crumble/";
        private const string File = "config.cfg";

        public static MelonPreferences_Category Category;
        public static MelonPreferences_Entry<bool>  Enabled;
        public static MelonPreferences_Entry<int>   ChunksPerBreak;
        public static MelonPreferences_Entry<bool>  ChunkStyleMinecraft;
        public static MelonPreferences_Entry<float> ChunkLifetime;
        public static MelonPreferences_Entry<float> MassScale;
        public static MelonPreferences_Entry<float> AngularDrag;
        public static MelonPreferences_Entry<int>   MaxConcurrentDebris;
        public static MelonPreferences_Entry<bool>  PersistUntilSceneChange;
        public static MelonPreferences_Entry<float> SliceRandomness;
        public static MelonPreferences_Entry<bool>  DebugLogging;
        public static MelonPreferences_Entry<bool>  RockCamVisibility;
        public static MelonPreferences_Entry<int>   ChunkSize;

        public static void Init()
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);
            Category = MelonPreferences.CreateCategory("Crumble", "Settings");
            Category.SetFilePath(Path.Combine(Dir, File));

            Enabled             = Category.CreateEntry("Enabled", true, "Enabled", "Toggle the whole mod on/off");
            ChunksPerBreak      = Category.CreateEntry<int>("ChunksPerBreak", 8, "Chunks Per Break", "Exact number of chunks spawned per destruction",
                                    false, false, (ValueValidator)(object)new ValueRange<int>(1, 10), (string)null);
            ChunkStyleMinecraft = Category.CreateEntry("ChunkStyleMinecraft", false, "Chunk Style: Minecraft", "false = attempt real mesh slices, true = force box shards");
            ChunkLifetime       = Category.CreateEntry<float>("ChunkLifetime", 6f, "Chunk Lifetime (s)", "Seconds before a chunk despawns",
                                    false, false, (ValueValidator)(object)new ValueRange<float>(0.5f, 60f), (string)null);
            MassScale           = Category.CreateEntry<float>("MassScale", 1f, "Mass Scale", "Multiplier on chunk mass (scaled by size)",
                                    false, false, (ValueValidator)(object)new ValueRange<float>(0.01f, 100f), (string)null);
            AngularDrag         = Category.CreateEntry<float>("AngularDrag", 0.5f, "Angular Drag", "Higher = chunks stop spinning sooner",
                                    false, false, (ValueValidator)(object)new ValueRange<float>(0f, 20f), (string)null);
            MaxConcurrentDebris = Category.CreateEntry<int>("MaxConcurrentDebris", 65, "Chunk Limit", "Hard cap on live chunks (VR safety). Applies in all modes, including Chaos",
                                    false, false, (ValueValidator)(object)new ValueRange<int>(1, 500), (string)null);
            PersistUntilSceneChange = Category.CreateEntry("PersistUntilSceneChange", false, "Persist Until Scene Change", "If true, chunks never despawn on a timer and the concurrent cap is ignored; they clear only on map/scene change (VR perf risk if you spam destructions)");
            SliceRandomness     = Category.CreateEntry<float>("SliceRandomness", 0.5f, "Slice Randomness", "0 = uniform cuts, 1 = chaotic cuts",
                                    false, false, (ValueValidator)(object)new ValueRange<float>(0f, 1f), (string)null);
            DebugLogging        = Category.CreateEntry("DebugLogging", false, "Debug Logging", "Verbose Crumble logs");
            RockCamVisibility   = Category.CreateEntry("RockCamVisibility", true, "Rock Cam Visibility", "Show debris chunks in the in-game live/recording (Rock) camera");
            ChunkSize           = Category.CreateEntry<int>("ChunkSize", 5, "Chunk Size", "Chunk size multiplier (5 = normal)",
                                    false, false, (ValueValidator)(object)new ValueRange<int>(1, 10), (string)null);
        }

        public static void Log(string msg)
        {
            if (DebugLogging != null && DebugLogging.Value) Main.Logger.Msg(msg);
        }
    }
}
