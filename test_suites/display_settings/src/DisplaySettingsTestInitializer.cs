using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.DisplaySettingsTest
{
    /// <summary>Queues <see cref="DisplaySettingsTestDriverNode"/> only for <c>--itest=display_settings</c>; <c>--shots=&lt;dir&gt;</c> makes it save screenshots of the menu and every settings tab.</summary>
    public class DisplaySettingsTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            var args = OS.GetCmdlineUserArgs();
            if (queued || !args.Contains("--itest=display_settings"))
                return;

            string shots = null;
            foreach (var arg in args)
            {
                if (arg.StartsWith("--shots="))
                    shots = arg.Substring("--shots=".Length);
            }

            queued = true;
            GameLogger.Log("TEST", "DisplaySettingsTest: activating");
            ModRoot.AddDeferred(new DisplaySettingsTestDriverNode(shots));
        }
    }
}
