using Cthangover.Core.Games;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// Handlers for the built-in menu buttons. Each receives the menu root control
    /// so it can locate the panels instantiated as named children by
    /// <see cref="MainMenu"/>.
    /// </summary>
    internal static class BaseMenuActions
    {
        /// <summary>
        /// Starts a new game at the active game's entry scene (<c>start_scene</c>
        /// when no game collection is active) through <see cref="GameSession"/>.
        /// </summary>
        public static void NewGame(Control host)
        {
            GameSession.StartNew(host);
        }

        /// <summary>Loads the last save of the last played game (restarting into it when needed).</summary>
        public static void Continue(Control host)
        {
            GameFlow.Continue(host);
        }

        /// <summary>Opens the game picker.</summary>
        public static void Play(Control host)
        {
            host.GetNodeOrNull<GamePickerPanel>("GamePickerPanel")?.Open();
        }

        /// <summary>Opens the save/load panel in load mode.</summary>
        public static void Load(Control host)
        {
            host.GetNodeOrNull<SaveLoadMenu>("SaveLoadPanel")?.OpenForLoad();
        }

        /// <summary>Shows the settings overlay.</summary>
        public static void Settings(Control host)
        {
            host.GetNodeOrNull<SettingsMenu>("SettingsPanel")?.Open();
        }

        /// <summary>Quits the application after engine mods finish their registered cleanup.</summary>
        public static void Exit(Control host)
        {
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(host.GetTree());
        }
    }
}
