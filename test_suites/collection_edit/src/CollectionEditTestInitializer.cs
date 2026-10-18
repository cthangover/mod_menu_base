using System.Linq;
using Cthangover.Core.Mods;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.CollectionEditTest
{
    /// <summary>Queues <see cref="CollectionEditTestDriverNode"/> only for <c>--itest=collection_edit</c>.</summary>
    public class CollectionEditTestInitializer : IModInitializer
    {
        private static bool queued;

        public void OnModLoaded(string modId) { }

        public void OnModResourcesReady()
        {
            if (queued || !OS.GetCmdlineUserArgs().Contains(CollectionEditTestDriverNode.Flag))
                return;

            queued = true;
            GameLogger.Log("TEST", "CollectionEditTest: activating");
            (Godot.Engine.GetMainLoop() as SceneTree)?.Root?.CallDeferred("add_child", new CollectionEditTestDriverNode());
        }
    }
}
