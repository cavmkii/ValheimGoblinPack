using UnityEngine;

namespace GoblinPack
{
    /// <summary>Dialogue. {player} is replaced with the nearest player's name.</summary>
    internal static class Lines
    {
        public static readonly string[] JoeInsults =
        {
            "Oi, {player}! Your mother was a troll and your father smelled of greydwarf!",
            "{player}, that armour looks like you lost a fight with a boar. And the boar wore it better.",
            "Nice axe, {player}. Did you make it yourself? I can tell.",
            "I've seen necks with more spine than you, {player}.",
            "Hah! {player} builds houses like a drunk dverger. A blind one. With no hands.",
            "Is that your face, {player}, or did a draugr sneeze on you?",
            "Smell that? That's {player}. Odin can smell you from Valhalla.",
            "You call that a beard, {player}? I've seen better moss on a troll's backside.",
            "{player}! Your sailing is so bad the serpent felt sorry for you.",
            "Keep walking, {player}. The deathsquitos need the practice.",
            "Hey {player}, the Elder called. He wants his posture back.",
            "Your stamina bar is shorter than me, {player}, and I'm TINY.",
            "Oh good, {player} is here. Now the wolves have a snack.",
            "{player}, you swing that sword like a fish swinging a fishing rod.",
            "I'd insult your cooking, {player}, but you've never cooked anything that wasn't raw meat on a stick.",
        };

        public static readonly string[] JoeSearching =
        {
            "SEEEEAN! Where are you, you cloud-sniffing fraud?!",
            "Sean owes me money. And an apology. And a new eyebrow.",
            "Anyone seen a dwarf selling weather in jars? Tell him Joe's coming.",
            "Sean! I know you're out here! I can smell your stupid rain!",
            "When I find Sean I'm going to bite his knees. Both of them.",
            "Sean sold me a 'sunny day'. It rained for a WEEK.",
        };

        public static readonly string[] JoeSteal =
        {
            "Ooh, shiny! Mine now!",
            "Finders keepers, {player}!",
            "Thanks for the donation, {player}!",
            "Tax collector! Hehehe!",
        };

        public static readonly string[] JoeProvoked =
        {
            "You HIT me?! Oh, now you've done it, {player}!",
            "That's it, {player}. I'm going to eat your boots.",
            "Ow! OW! Right. RIGHT. You're dead, {player}!",
        };

        public static readonly string[] JoeRage =
        {
            "WHO SAID BABY WARS?! {player}! I'M COMING FOR YOU!",
            "BABY WARS?! You don't get to say that! Nobody gets to say that!",
            "{player} said it. {player} SAID IT. Everybody dies now.",
        };

        public static readonly string[] JoeFightStart =
        {
            "SEEEEEAN!!! FINALLY!",
            "There you are, you weather-peddling weasel!",
            "Sean! Give me back my sunny day!",
        };

        public static readonly string[] JoeFightEnd =
        {
            "This isn't over, Sean! I'll be back with a bigger stick!",
            "Fine! FINE! I'm leaving! But only because I want to!",
            "Ow. Ow. Tactical retreat! TACTICAL!",
        };

        public static readonly string[] JoeDeath =
        {
            "Tell... Sean... he still owes me...",
            "I'll be back! Goblins always come back!",
        };

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
