using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.ResettablesTest
{
    /// <summary>Queues <see cref="ResettablesTestDriverNode"/> only for <c>--itest=resettables</c>.</summary>
    public class ResettablesTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains(ResettablesTestDriverNode.Flag))
                return;

            queued = true;
            GameLogger.Log("TEST", "ResettablesTest: activating");
            ModRoot.AddDeferred(new ResettablesTestDriverNode());
        }
    }
}
