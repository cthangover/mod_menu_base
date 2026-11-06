using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Cthangover.BaseMenu;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Games;
using Cthangover.Core.Localization;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Cthangover.Mods.Interface.View;
using Godot;

namespace Mods.MenuFlowTest
{
    /// <summary>
    /// Front-end walk-through, one step per tick so deferred UI work settles:
    /// only the menu mods are loaded; the menu offers Play (no New game
    /// without a game); Play opens a card per game; the save browser lists a
    /// game's save; a player's own game can be deleted from the collection
    /// editor (a built-in one cannot) while its mods stay; the composition of
    /// a game is computed without loading it; Continue sees the last session.
    /// <c>--shots=&lt;dir&gt;</c> saves the game picker and the save browser. Prints <c>[TEST_RESULT] name=menu_flow</c>.
    /// </summary>
    public partial class MenuFlowTestDriverNode : Node
    {
        private const string GameId = "novel_template";
        private const string Slot = "slot_942";
        private const string DeletableId = "menu_flow_deletable";
        private const float TimeoutSeconds = 90f;

        private readonly List<string> failures = new();
        private int passed;
        private int step;
        private double elapsed;
        private double stepAt;
        private bool done;
        private Node hub;
        private string initialLocale;
        private int languageIndex;
        private bool languageReady;

        public override void _Process(double delta)
        {
            if (done)
                return;

            elapsed += delta;
            if (elapsed > TimeoutSeconds)
            {
                failures.Add($"timeout at step {step}");
                Finish();
                return;
            }

            if (elapsed - stepAt < 0.5)
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

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                        return;
                    // First launch shows the launcher screen; go on like a player.
                    if (GetTree().CurrentScene?.FindChild("LauncherPlay", true, false) is Button launcherPlay)
                    {
                        launcherPlay.EmitSignal(BaseButton.SignalName.Pressed);
                        return;
                    }
                    if (GetTree().CurrentScene is not MainMenu)
                        return;
                    Check("front-end only", ActiveGame.IsFrontendOnly && !ActiveGame.IsActive);
                    initialLocale = TranslationServer.GetLocale();
                    Check("menu mods loaded", ModRegistry.Instance.Mods.ContainsKey("menu_base") && ModRegistry.Instance.Mods.ContainsKey("mod_manager"));
                    Check("game content not loaded", !ModRegistry.Instance.Mods.ContainsKey("cthangover_game"));
                    CheckUnloadedModTitle("cooking");
                    Check("Play button shown", Menu.FindChild("play", true, false) is Button);
                    Check("New game hidden without a game", Menu.FindChild("new_game", true, false) == null);
                    break;

                case 1:
                    ((Button)Menu.FindChild("play", true, false)).EmitSignal(BaseButton.SignalName.Pressed);
                    break;

                case 2:
                {
                    var picker = Menu.GetNodeOrNull<GamePickerPanel>("GamePickerPanel");
                    Check("Play opens the game picker", picker?.Visible == true);
                    if (languageIndex <= LanguageCode.Supported.Length)
                    {
                        if (!languageReady)
                        {
                            LocaleLoader.LoadLocale(languageIndex < LanguageCode.Supported.Length
                                ? LanguageCode.Supported[languageIndex] : initialLocale);
                            picker.Open();
                            languageReady = true;
                            return;
                        }
                        if (languageIndex < LanguageCode.Supported.Length)
                        {
                            CheckPickerLanguage(picker, LanguageCode.Supported[languageIndex]);
                            languageIndex++;
                            languageReady = false;
                            return;
                        }
                        languageIndex++;
                    }
                    Shot("game_picker");
                    var games = GameCatalog.Games();
                    CheckDevelopmentCovers(picker, games);
                    var newGameText = TranslationServer.Translate("menu/play/new_game").ToString();
                    var startButtons = Buttons(picker).Where(b => b.Text == newGameText).ToList();
                    Check($"one card per game ({games.Count})", games.Count >= 2 && startButtons.Count == games.Count);
                    var ready = games.Count(g => g.IsReady);
                    Check("ready games can start", startButtons.Count(b => !b.Disabled) == ready && ready > 0);
                    var configureText = TranslationServer.Translate("menu/play/configure").ToString();
                    var configure = Buttons(picker).FirstOrDefault(b => b.Text == configureText);
                    Check("cards offer Configure", configure != null);
                    configure?.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                }

                case 3:
                {
                    hub = Menu.FindChildren("*", "", true, false).FirstOrDefault(n => n.HasMethod("OpenCollectionEditor"));
                    Check("Configure opens the mod manager", hub != null);
                    var duplicateText = TranslationServer.Translate("mod_manager/collection_duplicate").ToString();
                    Check("editor opened with Duplicate", hub != null && Buttons(hub).Any(b => b.Text == duplicateText && b.IsVisibleInTree()));
                    Check("a built-in game offers no Delete", DeleteButton() == null);

                    var user = ModCollectionStore.LoadUser();
                    user.Add(new ModCollection { Id = DeletableId, Name = "Deletable", Kind = ModCollectionKind.Game });
                    ModCollectionStore.SaveUser(user);
                    hub?.Call("OpenCollectionEditor", DeletableId);
                    break;
                }

                case 4:
                {
                    var delete = DeleteButton();
                    Check("a player's own game offers Delete", delete != null);
                    delete?.EmitSignal(BaseButton.SignalName.Pressed);
                    break;
                }

                case 5:
                {
                    var dialog = hub?.FindChildren("*", "ConfirmationDialog", true, false).OfType<ConfirmationDialog>().FirstOrDefault(d => d.Visible);
                    var expected = string.Format(TranslationServer.Translate("mod_manager/collection_delete_confirm"), "Deletable", 0);
                    Check("Delete warns that the mods stay", dialog?.DialogText == expected);
                    dialog?.EmitSignal(AcceptDialog.SignalName.Confirmed);
                    Check("the collection is deleted", ModCollectionStore.LoadAll().All(c => c.Id != DeletableId));
                    Check("its mods stay in the system", ModRegistry.Instance.AllDiscoveredMods.ContainsKey("card_battle"));
                    hub?.QueueFree();
                    break;
                }

                case 6:
                    WriteFakeSave();
                    Menu.GetNode<SaveLoadMenu>("SaveLoadPanel").OpenForGame(GameId);
                    break;

                case 7:
                {
                    var browser = Menu.GetNode<SaveLoadMenu>("SaveLoadPanel");
                    var slots = browser.FindChildren("*", "", true, false).OfType<SaveSlotItem>().ToList();
                    Check("save browser lists the game's save", slots.Any(s => s.Model?.FileName == Slot));
                    Check("save list carries the game id", SaveService.ListSaves(GameId).Any(s => s.FileName == Slot && s.GameId == GameId));
                    Shot("save_browser");
                    browser.Visible = false;

                    var composition = GameCatalog.Composition(GameId);
                    Check("composition computed without loading the game", composition?.Any(m => m.Id == "my_novel_game") == true);
                    var diff = SaveCompositionDiff.Compute(
                        new[] { new SaveGameMod { Id = "cthangover_game", Version = "2.0.0" }, new SaveGameMod { Id = "ghost_mechanic", Version = "1.0.0" } },
                        composition);
                    Check("comparison reports removed and added mechanics", diff.Removed.Contains("ghost_mechanic") && diff.Added.Contains("my_novel_game"));

                    LastSessionStore.Record(GameId, Slot);
                    Check("Continue sees the last session", LastSessionStore.Load()?.GameId == GameId);
                    Cleanup();
                    Finish();
                    break;
                }
            }

            step++;
        }

        /// <summary>With <c>--shots=&lt;dir&gt;</c> saves the window as <paramref name="name"/>.png (windowed runs only).</summary>
        private void Shot(string name)
        {
            string dir = null;
            foreach (var arg in OS.GetCmdlineUserArgs())
            {
                if (arg.StartsWith("--shots="))
                    dir = arg.Substring("--shots=".Length);
            }
            if (string.IsNullOrEmpty(dir) || DisplayServer.GetName() == "headless")
                return;
            dir = ProjectSettings.GlobalizePath(dir);
            Directory.CreateDirectory(dir);
            ViewportCapture.SavePng(GetViewport(), Path.Combine(dir, name + ".png"));
        }

        private Button DeleteButton()
        {
            var text = TranslationServer.Translate("mod_manager/collection_delete").ToString();
            return Buttons(hub).FirstOrDefault(b => b.Text == text && b.IsVisibleInTree());
        }

        private void CheckDevelopmentCovers(GamePickerPanel picker, IReadOnlyList<ModCollectionGameSummary> games)
        {
            var frames = picker.FindChildren("DevelopmentFrame", "", true, false).OfType<TextureRect>().ToList();
            Check("one development overlay per game cover", frames.Count == games.Count);
            Check("development overlays match game versions", frames.Select(f => f.Visible).SequenceEqual(
                games.Select(g => VersionHelper.IsInDevelopment(g.Collection.Version))));
            Check("game overlays have generated art and pass clicks through", frames.All(f =>
                f.Texture != null && f.MouseFilter == Control.MouseFilterEnum.Ignore));
            Check("game version reset keeps every built-in game playable", games.Where(g =>
                g.Collection.Origin == ModCollectionOrigin.BuiltIn).All(g => g.Collection.Version == "0.1.0" && g.IsReady));
        }

        private void CheckPickerLanguage(GamePickerPanel picker, string language)
        {
            var expectedCaptions = new[] { "В разработке", "In development", "En desarrollo", "开发中", "開発中", "개발 중", "Đang phát triển" };
            var expected = expectedCaptions[languageIndex];
            var frames = picker.FindChildren("DevelopmentFrame", "", true, false).OfType<TextureRect>().ToList();
            Check($"{language}: development captions are translated", frames.Count > 0 && frames.All(f =>
                f.GetNode<Label>("Plaque/Caption").Tr("ui/cover/in_development").ToString() == expected));
            foreach (var frame in frames)
            {
                var caption = frame.GetNode<Label>("Plaque/Caption");
                Check($"{language}: rendered caption fits the plaque", caption.GetTotalCharacterCount() == expected.Length
                    && caption.GetThemeFont("font").GetStringSize(expected, fontSize: caption.GetThemeFontSize("font_size")).X <= caption.Size.X);
                Check($"{language}: blank plaque replaces baked text", frame.GetNode<TextureRect>("Plaque/Blank").Texture is AtlasTexture);
                Check($"{language}: caption follows cover size", frame.GetNode<Control>("Plaque").Scale.IsEqualApprox(frame.Size / frame.Texture.GetSize()));
            }

            var junior = GameCatalog.Games().First(g => g.Collection.Id == "junior_reaper_paper_grief");
            var entry = junior.Collection.Locale.FirstOrDefault(l => l.Lang == language);
            Check($"{language}: Junior Reaper has a translated description", !string.IsNullOrWhiteSpace(entry?.Description)
                && junior.Description == entry.Description && (language == "ru" || junior.Description != junior.Collection.GetLocalizedDescription("ru")));
            Check($"{language}: the game card shows the translated description", picker.FindChildren("*", "Label", true, false)
                .OfType<Label>().Any(l => l.Text == junior.Description));
            Shot("game_picker_" + language);
        }

        private static IEnumerable<Button> Buttons(Node root)
            => root == null ? Enumerable.Empty<Button>() : root.FindChildren("*", "Button", true, false).OfType<Button>();

        private static void WriteFakeSave()
        {
            var package = new SavePackage
            {
                SaveId = Slot,
                Timestamp = DateTime.UtcNow,
                CoreVersion = "0.1.0",
                Game = new SaveGameInfo { Id = GameId, Name = "My Novel", Version = "1.0.0" },
            };
            package.ModData[SavePackage.CoreSlotKey] = new ModSlot();
            var dir = ProjectSettings.GlobalizePath(SaveService.GameDirectory(GameId));
            Directory.CreateDirectory(dir);
            File.WriteAllText(Path.Combine(dir, Slot + ".json"), JsonSerializer.Serialize(package, JsonOptionsProvider.WriteCompactOptions));
        }

        private static void Cleanup()
        {
            var save = Path.Combine(ProjectSettings.GlobalizePath(SaveService.GameDirectory(GameId)), Slot + ".json");
            if (File.Exists(save))
                File.Delete(save);
            ModCollectionStore.RemoveUser(DeletableId);
            var last = ProjectSettings.GlobalizePath("user://last_session.json");
            if (File.Exists(last))
                File.Delete(last);
        }

        /// <summary>The mod manager lists mods the shell does not load: their names come in the current language.</summary>
        private void CheckUnloadedModTitle(string modId)
        {
            // Russian, not the run's English: an English name would equal the manifest's fallback.
            var was = TranslationServer.GetLocale();
            LocaleLoader.LoadLocale("ru");
            var mod = ModRegistry.Instance.AllDiscoveredMods.GetValueOrDefault(modId);
            var lines = mod?.FileProvider.ReadFileText($"locale/{TranslationServer.GetLocale()}.properties")?.Split('\n');
            string Text(string key)
            {
                var line = lines?.FirstOrDefault(l => l.StartsWith(key + " ") || l.StartsWith(key + "="));
                return line?.Substring(line.IndexOf('=') + 1).Trim();
            }

            Check($"{modId} not loaded, its name in '{TranslationServer.GetLocale()}': {mod?.DisplayTitle}",
                !ModRegistry.Instance.Mods.ContainsKey(modId) && Text($"mod_{modId}_name") is { } name && mod.DisplayTitle == name
                && Text($"mod_{modId}_desc") is { } desc && mod.LocalizedDescription == desc);
            LocaleLoader.LoadLocale(was);
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"MenuFlowTest: [OK] {name}");
            }
            else
            {
                failures.Add(name);
                GameLogger.Log("TEST", $"MenuFlowTest: [FAIL] {name}", LogLevel.Error);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;
            foreach (var failure in failures)
                GameLogger.Log("TEST", $"MenuFlowTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=menu_flow passed={passed} failed={failures.Count}");
            Cthangover.Core.Bootstrap.ApplicationExit.Quit(GetTree(), failures.Count == 0 ? 0 : 1);
        }
    }
}
