using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Loader;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.ResettablesTest
{
    /// <summary>
    /// Plan 3.1: a mod that leaves the set takes its new-game owner
    /// (<c>IResettable</c>) with it, and a mod loaded again registers exactly one
    /// owner, from its new assembly. The stats mod (<c>stats_base</c>, id
    /// <c>stats</c>) is the probe: it is in <c>rpg_plus</c> only.
    /// <list type="number">
    /// <item><c>rpg_plus</c> (new game): one <c>stats</c> owner, from the live
    /// <c>stats_base</c> context; a character gains a stat.</item>
    /// <item><c>novel_template</c>: <c>stats_base</c> unloaded, no <c>stats</c>
    /// owner left, its old context released.</item>
    /// <item><c>rpg_plus</c> again: exactly one <c>stats</c> owner, from the new
    /// context; the character gains a stat again.</item>
    /// <item>New game: the loaded state is reset (no characters left).</item>
    /// </list>
    /// The state is read through the mod's own types (reflection), because a suite
    /// cannot depend on <c>stats_base</c> without keeping it loaded in every game.
    /// Prints <c>[TEST_RESULT] name=resettables</c>.
    /// </summary>
    public partial class ResettablesTestDriverNode : Node
    {
        public const string Flag = "--itest=resettables";

        private const string StatsMod = "stats_base";
        private const string StatsOwnerId = "stats";
        private const string StatStateType = "Cthangover.Mods.Stats.StatState";
        private const string Probe = "itest_resettables";
        private const string GameWithStats = "rpg_plus";
        private const string GameWithoutStats = "novel_template";
        private const float TimeoutSeconds = 420f;

        private readonly List<string> failures = new();
        private readonly List<string> unloadReport = new();
        private int passed;
        private double elapsed;
        private bool started;
        private bool done;

        public override void _Ready()
        {
            // The driver keeps ticking across every mod-set switch of the run.
            ProcessMode = ProcessModeEnum.Always;
            GameLogger.LogEmitted += OnLogEmitted;
        }

        public override void _ExitTree()
        {
            GameLogger.LogEmitted -= OnLogEmitted;
        }

        private void OnLogEmitted(string category, string message, LogLevel level)
        {
            if (category != "MODS_UNLOAD")
                return;
            lock (unloadReport)
                unloadReport.Add(message);
        }

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add("timeout");
                Finish();
                return;
            }

            if (started || elapsed < 3 || !Boot.IsReady || GetTree().CurrentScene == null)
                return;
            if (GameSession.IsSwitching || ModManager.Instance.IsReloading)
                return;

            started = true;
            RunAsync();
        }

        private async void RunAsync()
        {
            try
            {
                await Run();
            }
            catch (Exception ex)
            {
                failures.Add($"unhandled {ex.GetType().Name}: {ex.Message}");
            }

            Finish();
        }

        private async System.Threading.Tasks.Task Run()
        {
            // 1. Enter the game with the stats mod: one owner, from the live context.
            var entered = await GameSession.SwitchToAsync(this, GameWithStats);
            Check($"switched into '{GameWithStats}'", entered && ActiveGame.Id == GameWithStats);
            Check($"'{GameWithStats}' released every old context", ModManager.Instance.LastReload?.Leaked.Count == 0);
            Check($"'{StatsMod}' is loaded in '{GameWithStats}'", LiveStatsContexts().Count == 1);
            Check("one stats owner is registered", StatsEntries().Count == 1);
            Check("the stats owner comes from the loaded stats mod", OwnerIsLive());

            Check("stats state is reachable", StateReachable());
            MarkProbe();
            Check("the stats state holds the probe before leaving", ProbeCount() == 1);

            // 2. Leave for a game without the stats mod: its owner goes with it.
            lock (unloadReport)
                unloadReport.Clear();
            entered = await GameSession.SwitchToAsync(this, GameWithoutStats);
            Check($"switched into '{GameWithoutStats}'", entered && ActiveGame.Id == GameWithoutStats);
            Check($"'{StatsMod}' left the set", ModManager.Instance.LastReload?.Plan?.Unload.Contains(StatsMod) == true);
            Check($"'{GameWithoutStats}' released the old stats context", ModManager.Instance.LastReload?.Leaked.Count == 0);
            Check("no stats owner remains after it left", StatsEntries().Count == 0);
            Check("no stats context stays alive", LiveStatsContexts().Count == 0);
            // The unload pass drops the owners itself; the static sweep dropping them
            // behind the owner's back is only a fallback that the report names.
            lock (unloadReport)
            {
                Check("the unload pass dropped the mod's new-game owners itself", unloadReport.Any(m => m.Contains("new-game resettable(s) of mod code")));
                Check("the static sweep did not have to drop a new-game owner", !unloadReport.Any(m => m.Contains("AppServices.resettables: removed")));
            }

            // 3. Back with the stats mod: a single owner, from the new context.
            entered = await GameSession.SwitchToAsync(this, GameWithStats);
            Check($"switched back into '{GameWithStats}'", entered && ActiveGame.Id == GameWithStats);
            Check($"'{GameWithStats}' (again) released every old context", ModManager.Instance.LastReload?.Leaked.Count == 0);
            Check("exactly one stats owner after the reload", StatsEntries().Count == 1);
            Check("the stats owner comes from the reloaded stats mod", OwnerIsLive());

            Check("stats state is reachable after the reload", StateReachable());
            MarkProbe();
            Check("the reloaded state holds the probe before New game", ProbeCount() == 1);

            // 4. New game resets the state of the loaded mod.
            GameData.Instance.StartNewGame();
            Check("New game reset the stats of the loaded mod", ProbeCount() == 0);
            Check($"still in '{GameWithStats}' after New game", ActiveGame.Id == GameWithStats);
        }

        /// <summary>Live (not yet released) contexts of the stats mod.</summary>
        private static List<ModLoadContext> LiveStatsContexts()
            => AssemblyLoadContext.All.OfType<ModLoadContext>()
                .Where(c => c.ModId == StatsMod && !c.IsReleased)
                .ToList();

        private static List<IResettable> StatsEntries()
            => AppServices.Current.Resettables.Where(r => r.Id == StatsOwnerId).ToList();

        /// <summary>True when the single stats owner's assembly is one of the live stats mod's assemblies.</summary>
        private static bool OwnerIsLive()
        {
            var entries = StatsEntries();
            if (entries.Count != 1)
                return false;
            var owner = entries[0].GetType().Assembly;
            return LiveStatsContexts().Any(c => c.Assemblies.Contains(owner));
        }

        // The state objects below are only ever used inside these helpers: a local
        // holding a StatState would keep the stats context alive across the next
        // switch (debug builds extend locals to the end of the method).

        /// <summary>Reads <c>StatState.Instance</c> of the live stats mod; null when it is not reachable.</summary>
        private static object CurrentStatState()
        {
            foreach (var context in LiveStatsContexts())
            {
                foreach (var assembly in context.Assemblies)
                {
                    var type = assembly.GetType(StatStateType);
                    if (type != null)
                        return type.GetProperty("Instance")?.GetValue(null);
                }
            }
            return null;
        }

        private static bool StateReachable() => CurrentStatState() != null;

        /// <summary>Gives the loaded stats state one character with a gained stat.</summary>
        private static void MarkProbe()
        {
            var state = CurrentStatState();
            state?.GetType().GetMethod("Of", new[] { typeof(string) })?.Invoke(state, new object[] { Probe });
        }

        /// <summary>Number of characters with stat gains in the loaded stats state; -1 when unreachable.</summary>
        private static int ProbeCount()
        {
            var state = CurrentStatState();
            var data = state?.GetType().GetProperty("Data")?.GetValue(state);
            var characters = data?.GetType().GetProperty("Characters")?.GetValue(data) as IDictionary;
            return characters?.Count ?? -1;
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"ResettablesTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"ResettablesTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"ResettablesTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=resettables passed={passed} failed={failures.Count}");
            ApplicationExit.Quit(GetTree(), failures.Count == 0 ? 0 : 1);
        }
    }
}
