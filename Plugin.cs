using System.Collections.Generic;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace ReturnToLobby
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "local.returntolobby";
        public const string Name = "ReturnToLobby";
        public const string Version = "1.0.0";

        internal static ManualLogSource Log;

        private void Awake()
        {
            Log = Logger;
            new Harmony(Guid).PatchAll();
            Log.LogInfo($"{Name} {Version} loaded, host can type /lobby in chat");
        }
    }

    // Adds a host-only "/lobby" chat command. It does the same thing as the game's debug-console
    // "/level lobby menu": remember where the run is, go to the lobby menu (which reopens the lobby
    // for joining), and pressing Start resumes from the saved level, shop or truck.
    [HarmonyPatch(typeof(DebugCommandHandler), "Start")]
    internal static class RegisterCommandPatch
    {
        // These game members are private/internal.
        private static readonly MethodInfo ResetLevelProgress = AccessTools.Method(typeof(DebugCommandHandler), "ResetLevelProgress");
        private static readonly AccessTools.FieldRef<DebugCommandHandler, Dictionary<string, DebugCommandHandler.ChatCommand>> Commands =
            AccessTools.FieldRefAccess<DebugCommandHandler, Dictionary<string, DebugCommandHandler.ChatCommand>>("_commands");
        private static readonly AccessTools.FieldRef<RunManager, bool> Restarting = AccessTools.FieldRefAccess<RunManager, bool>("restarting");
        private static readonly AccessTools.FieldRef<RunManager, int> LoadLevel = AccessTools.FieldRefAccess<RunManager, int>("loadLevel");
        private static readonly AccessTools.FieldRef<RunManager, int> SaveLevel = AccessTools.FieldRefAccess<RunManager, int>("saveLevel");

        private static void Postfix(DebugCommandHandler __instance)
        {
            if (DebugCommandHandler.instance != __instance || Commands(__instance).ContainsKey("lobby"))
            {
                return;
            }
            __instance.Register(new DebugCommandHandler.ChatCommand(
                "lobby",
                "Return to the lobby menu so new players can join",
                (_, _) => ReturnToLobby(__instance),
                isEnabled: CanReturn,
                debugOnly: false));
        }

        private static bool CanReturn()
        {
            return SemiFunc.IsMasterClient()
                && RunManager.instance != null
                && !Restarting(RunManager.instance)
                && !SemiFunc.IsMainMenu()
                && !SemiFunc.RunIsTutorial()
                && !SemiFunc.RunIsLobbyMenu()
                // The arena is the game-over level; ChangeLevel ignores the lobby menu request there.
                && !SemiFunc.RunIsArena();
        }

        private static void ReturnToLobby(DebugCommandHandler handler)
        {
            Plugin.Log.LogInfo("Returning to lobby menu");
            // Mid-level this reloads the last save, same as leaving and reloading: loot from the current level is lost.
            ResetLevelProgress.Invoke(handler, null);
            LoadLevel(RunManager.instance) = SaveLevel(RunManager.instance);
            RunManager.instance.ChangeLevel(_completedLevel: false, _levelFailed: false, RunManager.ChangeLevelType.LobbyMenu);
        }
    }
}
