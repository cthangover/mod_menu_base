using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Events;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Mods.HotReload;
using Cthangover.Core.Scenes;
using Cthangover.Core.UI;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.CollectionEditTest
{
    /// <summary>
    /// The player edits the collection of the game that is running, in ONE process:
    /// <list type="number">
    /// <item>The novel template is entered; it matches its collection and the
    /// editors are not loaded.</item>
    /// <item>The collection gets the developer toolset (<c>dev_tools</c>: editors,
    /// the scenario studio, …), saved the way the collection editor saves it (a
    /// user package with the same id): the running game is now outdated.</item>
    /// <item>The pause menu's "Main menu" button leaves the game like a player
    /// does, and the pause menu does not stay above the title screen.</item>
    /// <item>Menu → Play → New game of the same game switches the mod set in
    /// place: the editors and the studio are loaded, the studio's button is on
    /// the game's toolbar and the game matches its collection again.</item>
    /// </list>
    /// A player's own package of the game is moved aside for the run and put
    /// back afterwards. Prints <c>[TEST_RESULT] name=collection_edit</c>.
    /// </summary>
    public partial class CollectionEditTestDriverNode : Node
    {
        public const string Flag = "--itest=collection_edit";

        private const string GameId = "novel_template";
        private const string AddedToolset = "dev_tools";
        private const string AddedMod = "editor_tools";
        private const string StudioMod = "scenario_studio";
        private const string GameMenuId = "game_menu";
        private const float TimeoutSeconds = 240f;

        private readonly List<string> failures = new();
        private readonly List<string> enteredScenes = new();
        private readonly List<(string From, string To)> movedAside = new();
        private bool startedOnPlayerPackage;
        private int passed;
        private int step;
        private double elapsed;
        private double stepAt;
        private bool done;

        public override void _Ready()
        {
            // The driver must keep ticking across the mod-set switch.
            ProcessMode = ProcessModeEnum.Always;
            ModEventBus.Subscribe<SceneEnteredEvent>(OnSceneEntered);
        }

        public override void _ExitTree()
        {
            ModEventBus.Unsubscribe<SceneEnteredEvent>(OnSceneEntered);
        }

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add($"timeout at step {step} (scene='{SceneManager.GetCurrentSceneName()}', game='{ActiveGame.Id}')");
                Finish();
                return;
            }

            if (elapsed - stepAt < 0.25)
                return;
            stepAt = elapsed;

            try
            {
                RunStep();
            }
            catch (Exception ex)
            {
                failures.Add($"step {step}: {ex.GetType().Name}: {ex.Message}");
                Finish();
            }
        }

        private Control Menu => GetTree().CurrentScene as Control;

        private static bool Busy => GameSession.IsSwitching || ModManager.Instance.IsReloading || ModHotReloadService.IsReloadRunning;

        private static bool EditorsLoaded => ModRegistry.Instance.Mods.ContainsKey(AddedMod);

        /// <summary>Scenes of the game entered since the last clear; the main menu does not count.</summary>
        private void OnSceneEntered(SceneEnteredEvent e)
        {
            if (!string.Equals(e.SceneName, "mainmenu", StringComparison.OrdinalIgnoreCase))
                enteredScenes.Add(e.SceneName);
        }

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady || Busy || ActiveGame.Id != GameId || enteredScenes.Count == 0)
                        return;
                    Check($"game '{GameId}' entered ('{enteredScenes[0]}')", true);
                    Check("a freshly entered game matches its collection", !GameCatalog.IsActiveOutdated());
                    MoveUserPackageAside();
                    // A player's own package of the game (say, with the editors added) was what
                    // the game started on: then the editors may already be loaded and New game
                    // need not switch the set. The package is restored at the end.
                    startedOnPlayerPackage = movedAside.Count > 0;
                    if (startedOnPlayerPackage)
                        GameLogger.Log("TEST", "CollectionEditTest: the game started on the player's own package; the before/switch checks are skipped");
                    else
                        Check($"'{AddedMod}' is not loaded before the edit", !EditorsLoaded);

                    AddEditorsToCollection();
                    Check("the edited collection makes the running game outdated", GameCatalog.IsActiveOutdated());
                    AppServices.Current.Ui.ToggleEntry(GameMenuId);
                    break;

                case 1:
                {
                    var open = UiHost.Instance.TryGetInstance(GameMenuId, out var menu) && menu.Visible;
                    Check("the pause menu is open", open);
                    if (menu?.FindChild("MainMenuBtn", true, false) is not Button mainMenu)
                    {
                        failures.Add("the pause menu has no Main menu button");
                        Finish();
                        return;
                    }
                    mainMenu.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                }

                case 2:
                    if (Busy || GetTree().CurrentScene is not Cthangover.BaseMenu.MainMenu)
                        return;
                    Check("the pause menu is closed on the title screen",
                        !UiHost.Instance.TryGetInstance(GameMenuId, out var left) || !left.IsVisibleInTree());
                    break;

                case 3:
                    if (Busy || !PressMenuPlay())
                        return;
                    enteredScenes.Clear();
                    break;

                case 4:
                    if (!StartFromPicker())
                        return;
                    break;

                case 5:
                {
                    if (Busy || ActiveGame.Id != GameId || enteredScenes.Count == 0)
                        return;
                    var reload = ModManager.Instance.LastReload;
                    if (!startedOnPlayerPackage)
                        Check("New game after the edit switched the mod set", reload?.Applied == true && reload.Plan?.Load.Contains(AddedMod) == true);
                    Check($"'{AddedMod}' is loaded after the edit", EditorsLoaded);
                    Check($"'{StudioMod}' is loaded after the edit", ModRegistry.Instance.Mods.ContainsKey(StudioMod));
                    Check("the studio's button is on the game's toolbar", HasToolbarButton(StudioButtonTooltip()));
                    Check("the game matches its edited collection", !GameCatalog.IsActiveOutdated());
                    Check($"entered a scene of '{GameId}' ('{enteredScenes[0]}')", true);
                    Finish();
                    return;
                }
            }

            step++;
        }

        /// <summary>
        /// Saves the game's collection with the developer toolset included, as a
        /// user package — what the collection editor does when the player adds it.
        /// </summary>
        private void AddEditorsToCollection()
        {
            var source = GameCatalog.LoadCollections()[GameId];
            var edited = JsonSerializer.Deserialize<ModCollection>(JsonSerializer.Serialize(source));
            edited.Package = source.Package;
            edited.Includes ??= new List<ModCollectionInclude>();
            edited.Includes.Add(new ModCollectionInclude { Ref = AddedToolset + ">=1", Role = "tools" });

            var user = ModCollectionStore.LoadUser().Where(c => !string.Equals(c.Id, GameId, StringComparison.OrdinalIgnoreCase)).ToList();
            user.Add(edited);
            ModCollectionStore.SaveUser(user);
        }

        private void MoveUserPackageAside()
        {
            // UserPath ends with a separator: without the trim the "parent" is the
            // collections folder itself, the backup was scanned as a package and
            // Cleanup's RemoveUser(GameId) deleted the player's package with it.
            var root = ModCollectionStore.UserPath.TrimEnd('/', '\\');
            foreach (var path in new[] { Path.Combine(root, GameId), Path.Combine(root, GameId + ".zip") })
            {
                var aside = Path.Combine(Path.GetDirectoryName(root) ?? root, $"collection_edit_backup_{Path.GetFileName(path)}");
                if (Directory.Exists(path))
                    Directory.Move(path, aside);
                else if (File.Exists(path))
                    File.Move(path, aside);
                else
                    continue;
                movedAside.Add((path, aside));
            }
        }

        private void Cleanup()
        {
            ModCollectionStore.RemoveUser(GameId);
            foreach (var (from, aside) in movedAside)
            {
                if (Directory.Exists(aside))
                    Directory.Move(aside, from);
                else if (File.Exists(aside))
                    File.Move(aside, from);
            }
        }

        /// <summary>
        /// Tooltip of the studio's toolbar button, read from the panel entry the studio
        /// registered: the suite does not depend on the studio (it is added by the edit),
        /// so its locale key must not be hard-coded here.
        /// </summary>
        private static string StudioButtonTooltip()
        {
            var locale = UiRegistry.PanelEntries.FirstOrDefault(e => e.ModId == StudioMod && e.Button != null)?.Button.Locale;
            return locale == null ? null : TranslationServer.Translate(locale).ToString();
        }

        private bool HasToolbarButton(string tooltip)
            => GetTree().Root.FindChildren("*", "Button", true, false).OfType<Button>().Any(b => b.TooltipText == tooltip && b.IsVisibleInTree());

        /// <summary>The main menu's Play button, pressed; false while it is not on screen.</summary>
        private bool PressMenuPlay()
        {
            if (Menu?.FindChild("play", true, false) is not Button play)
                return false;
            play.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        /// <summary>Presses New game on the game's card in the picker; false until the picker is open.</summary>
        private bool StartFromPicker()
        {
            var picker = Menu?.FindChild("GamePickerPanel", true, false) as Control;
            if (picker?.Visible != true)
                return false;
            var game = GameCatalog.Games().FirstOrDefault(g => g.Collection.Id == GameId);
            var start = StartButton(picker, game?.Name ?? GameId);
            Check($"'{GameId}' card has an enabled New game button", start is { Disabled: false });
            if (start == null || start.Disabled)
            {
                Finish();
                return false;
            }

            start.EmitSignal(BaseButton.SignalName.Pressed);
            return true;
        }

        /// <summary>The New game button on the card titled <paramref name="title"/>.</summary>
        private static Button StartButton(Node picker, string title)
        {
            var newGameText = TranslationServer.Translate("menu/play/new_game").ToString();
            // A card is the innermost panel holding exactly one New game button (the picker frame holds them all).
            foreach (var card in picker.FindChildren("*", "PanelContainer", true, false))
            {
                var starts = card.FindChildren("*", "Button", true, false).OfType<Button>().Where(b => b.Text == newGameText).ToList();
                if (starts.Count == 1 && card.FindChildren("*", "Label", true, false).OfType<Label>().Any(l => l.Text == title))
                    return starts[0];
            }
            return null;
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"CollectionEditTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"CollectionEditTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            Cleanup();
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"CollectionEditTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=collection_edit passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
