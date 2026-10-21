using System;
using System.Collections.Generic;
using System.IO;
using Cthangover.BaseMenu;
using Cthangover.Core.Bootstrap;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.Utils;
using Godot;

namespace Mods.DisplaySettingsTest
{
    /// <summary>
    /// Display tab of the settings menu driven like a player, one step per tick
    /// so the window settles: windowed mode with a picked size gives a window of
    /// that size centred on the monitor; borderless fullscreen disables the size
    /// list; the frame rate cap and V-Sync are applied; every change is in the
    /// settings file at once; a "restart" (the file read anew and applied to a
    /// window put in another state) restores it all; reset returns the game
    /// defaults. The player's settings file is restored at the end. With a shots
    /// directory the main menu and every settings tab are saved as PNG. Prints
    /// <c>[TEST_RESULT] name=display_settings</c>.
    /// </summary>
    public partial class DisplaySettingsTestDriverNode : Node
    {
        private const string Display = "Window/Column/Body/Pages/Display/";
        private const float TimeoutSeconds = 60f;
        private const string ResetButton = "Window/Column/Footer/Row/ResetButton";
        private static readonly Vector2I WindowSize = new(1280, 720);

        private readonly List<string> failures = new();
        private readonly string settingsPath = ProjectSettings.GlobalizePath("user://settings.json");
        private readonly string backup;
        private readonly string shotsDir;
        private SettingsMenu menu;
        private int passed;
        private int step;
        private int shotTab;
        private double elapsed;
        private double stepAt;
        private bool done;

        public DisplaySettingsTestDriverNode(string shotsDir)
        {
            this.shotsDir = shotsDir;
            backup = File.Exists(settingsPath) ? File.ReadAllText(settingsPath) : null;
        }

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

        private void RunStep()
        {
            switch (step)
            {
                case 0:
                    if (!Boot.IsReady)
                        return;
                    if (GetTree().CurrentScene?.FindChild("LauncherPlay", true, false) is Button launcherPlay)
                    {
                        launcherPlay.EmitSignal(BaseButton.SignalName.Pressed);
                        return;
                    }
                    if (GetTree().CurrentScene is not MainMenu main)
                        return;
                    menu = main.GetNodeOrNull<SettingsMenu>("SettingsPanel");
                    Check("settings panel exists", menu != null);
                    if (menu == null)
                    {
                        Finish();
                        return;
                    }
                    Shot("main_menu");
                    menu.Open();
                    shotTab = 0;
                    step = 100;
                    return;

                case 100:
                    // One tick per settings tab for the screenshots, then the Display tab checks.
                    if (shotTab > 0)
                        Shot("settings_" + shotTab);
                    if (shotTab < 4 && !string.IsNullOrEmpty(shotsDir))
                    {
                        menu.ShowTab(shotTab++);
                        return;
                    }
                    menu.ShowTab(2);
                    Check("the Display tab shows its page and the display reset",
                        menu.GetNode<Control>("Window/Column/Body/Pages/Display").Visible && !menu.GetNode<Control>("Window/Column/Body/Pages/Audio").Visible
                        && menu.GetNode<Button>(ResetButton).Visible);
                    var monitor = menu.GetNode<OptionButton>(Display + "MonitorRow/MonitorSelect");
                    var primary = DisplayServer.GetPrimaryScreen();
                    var screen = DisplayServer.ScreenGetSize(primary);
                    Check("monitors are listed, the primary one marked",
                        monitor.ItemCount == DisplayServer.GetScreenCount()
                        && monitor.GetItemText(primary) == string.Format(TranslationServer.Translate("settings/monitor_screen_primary"), primary + 1, screen.X, screen.Y));
                    Pick("ModeRow/ModeSelect", 0);
                    step = 1;
                    return;

                case 1:
                    var sizes = menu.GetNode<OptionButton>(Display + "ResRow/ResSelect");
                    Check("windowed mode enables the size list and hides the hint", !sizes.Disabled && !menu.GetNode<Label>(Display + "ResHint").Visible);
                    var index = ItemIndex(sizes, $"{WindowSize.X}×{WindowSize.Y}");
                    Check("size list offers 1280×720", index >= 0);
                    Pick("ResRow/ResSelect", index);
                    break;

                case 2:
                {
                    var area = DisplayServer.ScreenGetUsableRect(DisplayServer.WindowGetCurrentScreen());
                    Check($"window is windowed ({DisplayServer.WindowGetMode()})", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed);
                    Check($"window has the picked size ({DisplayServer.WindowGetSize()})", DisplayServer.WindowGetSize() == WindowSize);
                    Check($"window is centred ({DisplayServer.WindowGetPosition()} in {area})", DisplayServer.WindowGetPosition() == area.Position + (area.Size - WindowSize) / 2);
                    var saved = Saved();
                    Check("windowed mode and size are saved",
                        saved.WindowMode == (int)DisplayServer.WindowMode.Windowed && saved.ResolutionWidth == WindowSize.X && saved.ResolutionHeight == WindowSize.Y);
                    Pick("ModeRow/ModeSelect", DisplayOptions.WindowModeIndex((int)DisplayServer.WindowMode.Fullscreen));
                    break;
                }

                case 3:
                    Check($"borderless fullscreen is on ({DisplayServer.WindowGetMode()})", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
                    Check("fullscreen disables the size list and shows the hint",
                        menu.GetNode<OptionButton>(Display + "ResRow/ResSelect").Disabled && menu.GetNode<Label>(Display + "ResHint").Visible);
                    Check("fullscreen mode is saved, the window size kept", Saved().WindowMode == (int)DisplayServer.WindowMode.Fullscreen && Saved().ResolutionWidth == WindowSize.X);
                    Pick("FpsRow/FpsSelect", DisplayOptions.FrameRateIndex(60));
                    var vsync = menu.GetNode<CheckButton>(Display + "VSyncRow/VSyncToggle");
                    vsync.ButtonPressed = !vsync.ButtonPressed;
                    break;

                case 4:
                {
                    var vsyncOn = menu.GetNode<CheckButton>(Display + "VSyncRow/VSyncToggle").ButtonPressed;
                    Check($"frame rate cap applied ({Godot.Engine.MaxFps})", Godot.Engine.MaxFps == 60);
                    var saved = Saved();
                    Check("frame rate cap and V-Sync are saved", saved.MaxFps == 60 && saved.VsyncEnabled == vsyncOn);
                    // "Restart": the window in another state (settled for a tick), then the start-up restore from the file.
                    DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
                    DisplayServer.WindowSetSize(new Vector2I(800, 600));
                    Godot.Engine.MaxFps = 0;
                    break;
                }

                case 5:
                    Check($"window left fullscreen before the restore ({DisplayServer.WindowGetMode()})", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Windowed);
                    DisplaySettingsService.Apply(Saved());
                    break;

                case 6:
                    Check($"restore from the file brings fullscreen back ({DisplayServer.WindowGetMode()})", DisplayServer.WindowGetMode() == DisplayServer.WindowMode.Fullscreen);
                    Check("restore from the file brings the frame rate cap back", Godot.Engine.MaxFps == 60);
                    menu.GetNode<Button>(ResetButton).EmitSignal(BaseButton.SignalName.Pressed);
                    break;

                case 7:
                {
                    var defaults = new SettingsData();
                    var saved = Saved();
                    Check("reset saves the game defaults",
                        saved.WindowMode == defaults.WindowMode && saved.ResolutionWidth == defaults.ResolutionWidth
                        && saved.ResolutionHeight == defaults.ResolutionHeight && saved.MaxFps == defaults.MaxFps && saved.VsyncEnabled == defaults.VsyncEnabled);
                    Check("reset shows the default mode",
                        menu.GetNode<OptionButton>(Display + "ModeRow/ModeSelect").Selected == DisplayOptions.WindowModeIndex(defaults.WindowMode));
                    Check($"reset applies the default frame rate cap ({Godot.Engine.MaxFps})", Godot.Engine.MaxFps == defaults.MaxFps);
                    Finish();
                    return;
                }
            }
            step++;
        }

        private void Shot(string name)
        {
            if (string.IsNullOrEmpty(shotsDir) || DisplayServer.GetName() == "headless")
                return;
            var dir = ProjectSettings.GlobalizePath(shotsDir);
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, name + ".png");
            GetViewport().GetTexture().GetImage().SavePng(path);
            GameLogger.Log("TEST", $"DisplaySettingsTest: screenshot {path}");
        }

        /// <summary>Selects item <paramref name="index"/> of a Display tab list the way a click does.</summary>
        private void Pick(string path, int index)
        {
            var option = menu.GetNode<OptionButton>(Display + path);
            option.Select(index);
            option.EmitSignal(OptionButton.SignalName.ItemSelected, (long)index);
        }

        private static int ItemIndex(OptionButton option, string text)
        {
            for (var i = 0; i < option.ItemCount; i++)
            {
                if (option.GetItemText(i) == text)
                    return i;
            }
            return -1;
        }

        /// <summary>The settings as the next start reads them from the file.</summary>
        private static SettingsData Saved()
        {
            var settings = new SettingsData();
            settings.Load();
            return settings;
        }

        private void Check(string name, bool ok)
        {
            if (ok)
            {
                passed++;
                GameLogger.Log("TEST", $"DisplaySettingsTest: [PASS] {name}");
            }
            else
            {
                failures.Add(name);
            }
        }

        private void Finish()
        {
            if (done)
                return;
            done = true;

            if (backup != null)
                File.WriteAllText(settingsPath, backup);
            else if (File.Exists(settingsPath))
                File.Delete(settingsPath);
            GameData.Instance?.Settings?.Load();

            foreach (var failure in failures)
                GameLogger.Log("TEST", $"DisplaySettingsTest: failed: {failure}");
            GameLogger.Log("TEST", $"[TEST_RESULT] name=display_settings passed={passed} failed={failures.Count}");
            GetTree().Quit(failures.Count == 0 ? 0 : 1);
        }
    }
}
