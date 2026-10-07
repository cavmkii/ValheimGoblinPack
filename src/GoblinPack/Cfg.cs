using BepInEx.Configuration;

namespace GoblinPack
{
    internal enum ScalingMode
    {
        /// <summary>Highest average skill level among players near Joe.</summary>
        Skills,
        /// <summary>Number of bosses defeated in the world.</summary>
        Bosses,
        /// <summary>Whichever of the two is higher.</summary>
        Highest
    }

    /// <summary>
    /// All settings are admin-only, so Jotunn syncs the server's values to clients.
    /// Most behaviour runs on whichever client owns Joe or Sean, so they have to agree.
    /// </summary>
    internal static class Cfg
    {
        // General
        public static ConfigEntry<bool> JoeEnabled;
        public static ConfigEntry<bool> SeanEnabled;
        public static ConfigEntry<float> JoeRespawnMinutes;
        public static ConfigEntry<float> SeanRespawnMinutes;
        public static ConfigEntry<float> SpawnDistanceMin;
        public static ConfigEntry<float> SpawnDistanceMax;

        // Joe
        public static ConfigEntry<float> ProvokeSeconds;
        public static ConfigEntry<string> RagePhrases;
        public static ConfigEntry<float> RageSeconds;
        public static ConfigEntry<bool> StealEnabled;
        public static ConfigEntry<float> StealCooldownSeconds;
        public static ConfigEntry<int> StealMaxStack;
        public static ConfigEntry<float> TauntIntervalMin;
        public static ConfigEntry<float> TauntIntervalMax;
        public static ConfigEntry<float> SeanSeekChance;
        public static ConfigEntry<float> JoeScale;
        public static ConfigEntry<bool> PukeEnabled;
        public static ConfigEntry<float> PukeIntervalMin;
        public static ConfigEntry<float> PukeIntervalMax;

        // Scaling
        public static ConfigEntry<ScalingMode> Scaling;
        public static ConfigEntry<float> HealthMultMin;
        public static ConfigEntry<float> HealthMultMax;
        public static ConfigEntry<float> DamageMultMin;
        public static ConfigEntry<float> DamageMultMax;

        // Sean
        public static ConfigEntry<float> SeanHealth;
        public static ConfigEntry<bool> SellWeatherGear;
        public static ConfigEntry<float> RestrainingDistance;
        public static ConfigEntry<float> SeanNonPlayerDamageTaken;

        // Baby Wars
        public static ConfigEntry<bool> WarEnabled;
        public static ConfigEntry<int> WarTriggerCount;
        public static ConfigEntry<float> WarTriggerWindowMinutes;
        public static ConfigEntry<int> WarWaves;
        public static ConfigEntry<int> WarWaveBaseSize;
        public static ConfigEntry<int> WarWaveGrowth;
        public static ConfigEntry<float> WarWaveIntervalSeconds;
        public static ConfigEntry<float> WarMaxMinutes;
        public static ConfigEntry<float> WarCooldownMinutes;
        public static ConfigEntry<int> WarSecurityCount;
        public static ConfigEntry<float> BabyHealthScale;
        public static ConfigEntry<float> BabyDamageScale;

        // Fight
        public static ConfigEntry<float> FightTriggerRange;
        public static ConfigEntry<float> FightSeconds;
        public static ConfigEntry<float> FightCooldownMinutes;
        public static ConfigEntry<float> BystanderDamageMultiplier;

        public static void Bind(ConfigFile config)
        {
            JoeEnabled = B(config, "General", "JoeEnabled", true, "Spawn and maintain Joe in the world.");
            SeanEnabled = B(config, "General", "SeanEnabled", true, "Spawn and maintain Sean in the world.");
            JoeRespawnMinutes = B(config, "General", "JoeRespawnMinutes", 10f, "Minutes after Joe dies before he respawns.");
            SeanRespawnMinutes = B(config, "General", "SeanRespawnMinutes", 20f, "Minutes after Sean dies before he respawns.");
            SpawnDistanceMin = B(config, "General", "SpawnDistanceMin", 60f, "Minimum distance from a player that Joe/Sean (re)spawn at.");
            SpawnDistanceMax = B(config, "General", "SpawnDistanceMax", 140f, "Maximum distance from a player that Joe/Sean (re)spawn at.");

            ProvokeSeconds = B(config, "Joe", "ProvokeSeconds", 90f, "How long Joe stays hostile to a player after that player hits him.");
            RagePhrases = B(config, "Joe", "RagePhrases", "babywars,baby wars", "Comma-separated chat phrases (case-insensitive) that send Joe into a rage against everyone.");
            RageSeconds = B(config, "Joe", "RageSeconds", 120f, "How long a rage phrase keeps Joe hostile to all players.");
            StealEnabled = B(config, "Joe", "StealEnabled", true, "Whether Joe pickpockets players.");
            StealCooldownSeconds = B(config, "Joe", "StealCooldownSeconds", 180f, "Minimum seconds between thefts.");
            StealMaxStack = B(config, "Joe", "StealMaxStack", 10, "Most items Joe takes from a single stack per theft.");
            TauntIntervalMin = B(config, "Joe", "TauntIntervalMin", 18f, "Minimum seconds between Joe's insults.");
            TauntIntervalMax = B(config, "Joe", "TauntIntervalMax", 40f, "Maximum seconds between Joe's insults.");
            JoeScale = B(config, "Joe", "Scale", 0.8f, "Joe's size relative to a normal Fuling (0.3-3). Applied at game start.");
            PukeEnabled = B(config, "Joe", "PukeEnabled", true, "Joe occasionally throws up (the Pukeberries effect) when players are nearby.");
            PukeIntervalMin = B(config, "Joe", "PukeIntervalMin", 60f, "Minimum seconds between Joe's vomiting fits.");
            PukeIntervalMax = B(config, "Joe", "PukeIntervalMax", 150f, "Maximum seconds between Joe's vomiting fits.");
            SeanSeekChance = B(config, "Joe", "SeanSeekChance", 0.5f, "Chance (0-1) that Joe's next wander target is Sean's last known position.");

            Scaling = B(config, "Scaling", "Mode", ScalingMode.Highest, "What 'player level' means for Joe's scaling. Skills = highest average of top-5 skills among nearby players. Bosses = bosses defeated in the world. Highest = whichever is greater.");
            HealthMultMin = B(config, "Scaling", "HealthMultiplierMin", 0.6f, "Joe's health multiplier (relative to a vanilla Fuling) at level 0.");
            HealthMultMax = B(config, "Scaling", "HealthMultiplierMax", 10f, "Joe's health multiplier at max level.");
            DamageMultMin = B(config, "Scaling", "DamageMultiplierMin", 0.25f, "Joe's damage multiplier at level 0. Fuling weapons are Plains-tier, so this is low on purpose.");
            DamageMultMax = B(config, "Scaling", "DamageMultiplierMax", 2.5f, "Joe's damage multiplier at max level.");

            SellWeatherGear = B(config, "Sean", "SellWeatherGear", false, "Also sell the original weather charms, bottled weather and weather weapons. Read at game start.");
            RestrainingDistance = B(config, "Joe", "RestrainingOrderDistance", 15f, "How far Joe must stay from anyone wearing the Restraining Order.");
            SeanHealth = B(config, "Sean", "Health", 3000f, "Sean's max health.");
            SeanNonPlayerDamageTaken = B(config, "Sean", "NonPlayerDamageTaken", 0.1f, "Multiplier on damage Sean takes from ordinary monsters, so wolves don't keep killing the shop.");

            WarEnabled = B(config, "BabyWars", "Enabled", true, "Saying the rage phrase enough times starts the Baby Wars.");
            WarTriggerCount = B(config, "BabyWars", "TriggerCount", 3, "How many times 'baby wars' must be said (by anyone) within the window to start the war.");
            WarTriggerWindowMinutes = B(config, "BabyWars", "TriggerWindowMinutes", 60f, "The window for TriggerCount, in minutes.");
            WarWaves = B(config, "BabyWars", "Waves", 4, "Number of baby waves.");
            WarWaveBaseSize = B(config, "BabyWars", "WaveBaseSize", 6, "Babies in the first wave.");
            WarWaveGrowth = B(config, "BabyWars", "WaveGrowth", 3, "Extra babies per later wave.");
            WarWaveIntervalSeconds = B(config, "BabyWars", "WaveIntervalSeconds", 75f, "Next wave comes after this long, or sooner once most of the current wave is dead.");
            WarMaxMinutes = B(config, "BabyWars", "MaxMinutes", 15f, "The war ends after this long no matter what.");
            WarCooldownMinutes = B(config, "BabyWars", "CooldownMinutes", 30f, "Minutes before another war can start.");
            WarSecurityCount = B(config, "BabyWars", "SecurityCount", 3, "How many of Sean's Security guards show up.");
            BabyHealthScale = B(config, "BabyWars", "BabyHealthScale", 0.35f, "Baby health relative to Joe's scaled health.");
            BabyDamageScale = B(config, "BabyWars", "BabyDamageScale", 0.5f, "Baby damage relative to Joe's scaled damage.");

            FightTriggerRange = B(config, "Fight", "TriggerRange", 35f, "Joe and Sean start fighting when this close.");
            FightSeconds = B(config, "Fight", "DurationSeconds", 45f, "Length of a fight.");
            FightCooldownMinutes = B(config, "Fight", "CooldownMinutes", 10f, "Minutes before they can fight again.");
            BystanderDamageMultiplier = B(config, "Fight", "BystanderDamageMultiplier", 0.5f, "Multiplier on spell damage to anyone other than Joe and Sean caught in the blast. 0 disables.");
        }

        private static ConfigEntry<T> B<T>(ConfigFile config, string section, string key, T value, string description)
        {
            return config.Bind(section, key, value,
                new ConfigDescription(description, null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        }
    }
}
