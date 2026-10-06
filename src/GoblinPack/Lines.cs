using UnityEngine;

namespace GoblinPack
{
    /// <summary>
    /// Dialogue. {player} is replaced with the nearest player's name.
    /// Joe's lines come from <see cref="JoeLines"/> (an editable file); Sean's are below.
    /// </summary>
    internal static class Lines
    {
        public static string[] JoeAmbient => JoeLines.Get("ambient");
        public static string[] JoeSteal => JoeLines.Get("steal");
        public static string[] JoeProvoked => JoeLines.Get("provoked");
        public static string[] JoeRage => JoeLines.Get("rage");
        public static string[] JoeFightStart => JoeLines.Get("fight_start");
        public static string[] JoeFightEnd => JoeLines.Get("fight_end");
        public static string[] JoeDeath => JoeLines.Get("death");
        public static string[] JoePuke => JoeLines.Get("puke");

        public static readonly string[] SeanGreet =
        {
            "Ah, a customer! Fair winds to you, friend.",
            "Storm's coming. Good thing I sell umbrellas. Well. Charms. Close enough.",
            "Sean's Weatherworks, open for business! Mind the puddles.",
            "Welcome! Have you seen a small, rude goblin? No? Good.",
        };

        public static readonly string[] SeanPitch =
        {
            "Bottled sunshine! Guaranteed to work, mostly!",
            "Lightning-forged axes, fresh from the clouds!",
            "Frost pendants! Never shiver again!",
            "Everything must go before the next thunderstorm!",
        };

        public static readonly string[] SeanFightStart =
        {
            "Oh no. Not you again, Joe.",
            "Joe! I told you, NO REFUNDS!",
            "Stand back, customers. This one bites.",
        };

        public static readonly string[] SeanFightEnd =
        {
            "And STAY gone! Now, where was I? Ah yes, discounts!",
            "Sorry about the craters. Those are free, by the way.",
        };

        public static readonly string[] SeanBuy = { "Pleasure doing business!", "May the skies be kind to you!" };
        public static readonly string[] SeanSell = { "I'll find a use for this. Probably.", "Hmm, a bit damp, but fine." };
        public static readonly string[] SeanBye = { "Watch the sky!", "Come back when it's raining!" };

        public static string Pick(string[] lines, string playerName = null)
        {
            string line = lines[Random.Range(0, lines.Length)];
            return line.Replace("{player}", string.IsNullOrEmpty(playerName) ? "you" : playerName);
        }
    }
}
