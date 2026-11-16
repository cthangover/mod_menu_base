using System;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.UI.Menu;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Registers the built-in main-menu buttons and the settings/save-load panels
    /// in <see cref="MenuRegistry"/>. Runs from
    /// <c>ModInitializerRegistry.NotifyResourcesReady</c>, which fires after the
    /// registry itself is built, so these entries are appended to live registry
    /// state rather than collected from a manifest. Repeated ready notifications
    /// reuse the registration while its owned panel remains in the registry;
    /// clearing the registry permits registration in the replacement generation.
    /// </summary>
    public class BaseMenuInitializer : IModInitializer
    {
        private const string ModId = "menu_base";

        private MenuPanelEntry settingsPanel;

        /// <summary>
        /// Defers menu registration until every mod's resource declarations are available.
        /// </summary>
        public void OnModLoaded(string modId)
        {
        }

        /// <summary>
        /// Adds the menu once per registry lifetime without hiding conflicts with another provider's entries.
        /// </summary>
        public void OnModResourcesReady()
        {
            if (settingsPanel != null && ReferenceEquals(MenuRegistry.GetPanelById(settingsPanel.Id), settingsPanel))
            {
                return;
            }

            settingsPanel = new MenuPanelEntry
            {
                Id = "settings",
                ModId = ModId,
                ScenePath = "scenes/menu/settings_menu.tscn",
                Locale = "menu/settings",
            };
            MenuRegistry.RegisterMenuPanel(settingsPanel);
            MenuRegistry.RegisterMenuPanel(new MenuPanelEntry
            {
                Id = "save_load",
                ModId = ModId,
                ScenePath = "scenes/menu/save_load_menu.tscn",
            });

            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "continue",
                ModId = ModId,
                Section = "game",
                Locale = "menu/continue",
                Variation = "primary",
                Index = 5,
                Visible = GameFlow.HasContinue,
                Handler = BaseMenuActions.Continue,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "play",
                ModId = ModId,
                Section = "game",
                Locale = "menu/play",
                Variation = "primary",
                Index = 10,
                Handler = BaseMenuActions.Play,
            });
            // A new game of the running game; from the front-end games start
            // from the picker instead.
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "new_game",
                ModId = ModId,
                Section = "game",
                Locale = "menu/newgame",
                Index = 15,
                Visible = HasActiveGame,
                Handler = BaseMenuActions.NewGame,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "load",
                ModId = ModId,
                Section = "game",
                Locale = "ui/menu/load",
                Index = 20,
                Handler = BaseMenuActions.Load,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "settings",
                ModId = ModId,
                Section = "game",
                Locale = "menu/settings",
                Index = 30,
                Handler = BaseMenuActions.Settings,
            });
            MenuRegistry.RegisterMenuButton(new MenuButtonEntry
            {
                Id = "exit",
                ModId = ModId,
                Section = "exit",
                Locale = "menu/exit",
                Variation = "danger",
                Index = 1000,
                Handler = BaseMenuActions.Exit,
            });
        }

        private static bool HasActiveGame()
        {
            return !ActiveGame.IsFrontendOnly;
        }
    }

}
