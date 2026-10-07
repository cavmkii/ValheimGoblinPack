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
            CommandManager.Instance.AddConsoleCommand(new BabyWarsCommand());
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

        /// <summary>Cheat command (needs devcommands): start or stop the Baby Wars at your position.</summary>
        private class BabyWarsCommand : ConsoleCommand
        {
            public override string Name => "goblinpack_babywars";
            public override string Help => "goblinpack_babywars start|stop - start the Baby Wars here, or call them off.";
            public override bool IsCheat => true;

            public override void Run(string[] args)
            {
                Player player = Player.m_localPlayer;
                string command = args.Length > 0 ? args[0].ToLowerInvariant() : "";
                if (player == null || (command != "start" && command != "stop"))
                {
                    Console.instance.Print("Usage: goblinpack_babywars start|stop");
                    return;
                }
                BabyWar.SendCommand(command, player.transform.position);
                Console.instance.Print(command == "start" ? "The Baby Wars begin..." : "Calling off the Baby Wars.");
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
