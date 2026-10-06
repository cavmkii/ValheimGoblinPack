using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;

namespace GoblinPack
{
    /// <summary>
    /// Joe only talks about two things: Sean, and the Baby Wars. His lines live in
    /// BepInEx/config/GoblinPack/joe_lines.txt (written with defaults on first run) so they can be
    /// edited without rebuilding. Lines are picked on the client that owns Joe and sent to everyone,
    /// so in multiplayer the host's file is the one that matters most.
    /// </summary>
    internal static class JoeLines
    {
        private static readonly Dictionary<string, string[]> Defaults = new Dictionary<string, string[]>
        {
            ["ambient"] = new[]
            {
                "Sean owes me $50 for fact-checking fees",
                "Where is President Seanald Trump?",
                "Sean I am pleased to inform you that I, Hi-Fido, have completed my awesome tiny keyboard and gotten the number function to work, thus making I, Hi-Fido 2,000,000,000x cooler than you @ImperfectPK",
                "I was walking behind Sean this morning and saw his phone. Mf listens to Mumford and Seans",
                "Sean be like \"No One Gets Me Im Moving to Svalbaard\"",
                "Me when I hop on the Sean type beat",
                "Woke up thinking about Sean I'm soooooo obsessed",
                "I know your past, present, and future and every variation of every Sean",
                "{player}! Have you seen Sean? Smug dwarf. Smells like rain. Owes me money.",
                "Sean thinks he can hide from me. Sean is WRONG.",
                "Everyone knows Sean started the Baby Wars. EVERYONE.",
                "Don't say it. Don't you DARE say 'baby wars', {player}.",
                "I was there, {player}. In the Baby Wars. You weren't. You don't get opinions.",
                "Sean sells weather. WEATHER. What kind of man sells weather?",
                "When I find Sean I'm biting his knees. Both of them.",
                "Sean sold me a sunny day. It rained for a week.",
                "{player}, if you see Sean, tell him Joe remembers the Baby Wars.",
                "Nobody talks about the Baby Wars. Nobody but me. Constantly.",
                "SEEEEAN! I can smell your stupid drizzle!",
                "The Baby Wars took everything from me. Also Sean did. Mostly Sean.",
                "You look like someone Sean would sell a raincloud to, {player}.",
                "Sean's charms don't work. I know. I stole one. Twice.",
                "There was a Baby Wars memorial once. Sean knocked it over.",
            },
            ["steal"] = new[]
            {
                "This is going in the Sean revenge fund!",
                "Thanks, {player}! Sean's paying me back for this one way or another!",
                "Confiscated! In the name of the Baby Wars!",
                "Sean took everything from me. Now I take from you. That's economics.",
            },
            ["provoked"] = new[]
            {
                "Did SEAN put you up to this, {player}?!",
                "You hit like Sean. That's not a compliment.",
                "That's a Baby Wars move, {player}! You're going DOWN!",
            },
            ["rage"] = new[]
            {
                "WHO SAID BABY WARS?! {player}! I'M COMING FOR YOU!",
                "BABY WARS?! You don't get to say that! Nobody gets to say that!",
                "{player} said it. {player} SAID IT. Everybody dies now.",
                "I've waited years for someone to say it again, {player}.",
            },
            ["fight_start"] = new[]
            {
                "SEEEEEAN!!! FINALLY!",
                "There you are, you weather-peddling weasel!",
                "This is for the Baby Wars, Sean!",
                "Sean! Give me back my sunny day!",
            },
            ["fight_end"] = new[]
            {
                "This isn't over, Sean! The Baby Wars never end!",
                "Fine! FINE! I'm leaving! But only because I want to, Sean!",
                "Tactical retreat! Sean hasn't seen the last of me!",
            },
            ["death"] = new[]
            {
                "Tell... Sean... he still owes me...",
                "The Baby Wars... never... ended...",
                "Sean... did this... somehow...",
            },
            ["puke"] = new[]
            {
                "Sean's soup. It was Sean's soup. BLARGH!",
                "Thinking about the Baby Wars again... HURRK!",
                "Sean sold me 'bottled sunshine'. It was NOT sunshine. BLEUGH!",
                "Every time someone mentions Sean... BLAAARGH!",
            },
        };

        private static Dictionary<string, string[]> _loaded = new Dictionary<string, string[]>();

        public static string FilePath => Path.Combine(Path.Combine(Paths.ConfigPath, "GoblinPack"), "joe_lines.txt");

        public static string[] Get(string section)
        {
            return _loaded.TryGetValue(section, out string[] lines) && lines.Length > 0 ? lines : Defaults[section];
        }

        /// <summary>Reads the lines file, creating it with the defaults if it doesn't exist.</summary>
        public static string Load()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
                    File.WriteAllLines(FilePath, Render());
                }

                var parsed = new Dictionary<string, List<string>>();
                List<string> current = null;
                foreach (string raw in File.ReadAllLines(FilePath))
                {
                    string line = raw.Trim();
                    if (line.Length == 0 || line.StartsWith("#"))
                    {
                        continue;
                    }
                    if (line.StartsWith("[") && line.EndsWith("]"))
                    {
                        string name = line.Substring(1, line.Length - 2).Trim().ToLowerInvariant();
                        current = Defaults.ContainsKey(name) ? (parsed[name] = new List<string>()) : null;
                        continue;
                    }
                    current?.Add(line);
                }

                _loaded = new Dictionary<string, string[]>();
                int count = 0;
                foreach (var kv in parsed)
                {
                    _loaded[kv.Key] = kv.Value.ToArray();
                    count += kv.Value.Count;
                }
                return $"Loaded {count} Joe lines from {FilePath}";
            }
            catch (Exception e)
            {
                _loaded = new Dictionary<string, string[]>();
                return $"Couldn't read {FilePath}, using built-in lines: {e.Message}";
            }
        }

        private static IEnumerable<string> Render()
        {
            yield return "# Joe's dialogue. He only ever talks about Sean or the Baby Wars.";
            yield return "# One line per entry under each [section]. {player} becomes the nearest player's name.";
            yield return "# Delete a whole section to fall back to the built-in lines for it.";
            yield return "# Reload in-game with the console command: goblinpack_reloadlines";
            foreach (var kv in Defaults)
            {
                yield return "";
                yield return $"[{kv.Key}]";
                foreach (string line in kv.Value)
                {
                    yield return line;
                }
            }
        }
    }
}
