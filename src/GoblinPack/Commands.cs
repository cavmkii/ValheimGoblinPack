using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace GoblinPack
{
    internal static class Commands
    {
        public static void Register()
        {
            CommandManager.Instance.AddConsoleCommand(new WhereCommand());
            CommandManager.Instance.AddConsoleCommand(new SummonCommand());
            CommandManager.Instance.AddConsoleCommand(new ReloadLinesCommand());
        }

        private class WhereCommand : ConsoleCommand
        {
            public override string Name => "goblinpack_where";
            public override string Help => "Show where Joe and Sean were last seen (updates every 10s).";

            public override void Run(string[] args)
            {
                Console.instance.Print(WorldDirector.Describe());
            }
        }

        private class ReloadLinesCommand : ConsoleCommand
        {
            public override string Name => "goblinpack_reloadlines";
            public override string Help => "Reload Joe's dialogue from BepInEx/config/GoblinPack/joe_lines.txt.";

            public override void Run(string[] args)
            {
                Console.instance.Print(JoeLines.Load());
            }
        }

        /// <summary>Cheat command (needs devcommands): replace Joe or Sean with a fresh one in front of you.</summary>
        private class SummonCommand : ConsoleCommand
        {
            public override string Name => "goblinpack_summon";
            public override string Help => "goblinpack_summon joe|sean - respawn Joe or Sean in front of you.";
            public override bool IsCheat => true;

            public override void Run(string[] args)
            {
                Player player = Player.m_localPlayer;
                if (player == null || args.Length < 1)
                {
                    Console.instance.Print("Usage: goblinpack_summon joe|sean");
                    return;
                }
                string who = args[0].ToLowerInvariant();
                if (who != "joe" && who != "sean")
                {
                    Console.instance.Print("Usage: goblinpack_summon joe|sean");
                    return;
                }
                Vector3 spot = player.transform.position + player.transform.forward * 6f + Vector3.up;
                Rpc.SendSummon(who, spot);
                Console.instance.Print($"Summoning {who}.");
            }
        }
    }
}
