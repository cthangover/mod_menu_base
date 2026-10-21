using System;
using System.Collections.Generic;
using System.Linq;
using Cthangover.Core.Games;
using Cthangover.Core.Mods;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Menu;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Interface.Menu;
using Cthangover.Mods.Interface.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// "Play": every game collection as a card — cover, name, description,
    /// readiness (what is missing and what can be installed), saves and the
    /// last play date — with New game / Continue / Saves / Install / Configure.
    /// Data comes from <see cref="GameCatalog"/> against all installed mods, so
    /// it works from the front-end where only the menu is loaded. The window is
    /// the menu shell (<see cref="MenuWindowShell"/>, scene <c>game_picker.tscn</c>);
    /// its footer says whether the games' mods are still compiled in the
    /// background (<see cref="GameSession.IsWarmingUp"/>) or a start is quick.
    /// </summary>
    public partial class GamePickerPanel : Control
    {
        private const float HintRefreshSeconds = 0.5f;

        private VBoxContainer _list;
        private Label _hint;
        private readonly HashSet<string> _sessionInstalled = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>Raised when the player opens a game's saves; the menu shows its save browser.</summary>
        public event Action<string> SavesRequested;

        public event Action Closed;

        public override void _Ready()
        {
            MenuWindowShell.Style(this, Close);
            GetNode<Label>(MenuWindowShell.Header + "Title").Text = TranslationServer.Translate("menu/play/title");
            _list = GetNode<VBoxContainer>(MenuWindowShell.Body + "Scroll/List");
            _hint = GetNode<Label>(MenuWindowShell.Footer + "Hint");

            // The footer tells whether the games' mods are still compiled in the background.
            var timer = new Timer { WaitTime = HintRefreshSeconds, Autostart = true };
            timer.Timeout += RefreshHint;
            AddChild(timer);

            Visible = false;
        }

        private void RefreshHint()
        {
            if (Visible)
                _hint.Text = TranslationServer.Translate(GameSession.IsWarmingUp ? "menu/play/hint_warming" : "menu/play/hint_ready");
        }

        public void Open()
        {
            Visible = true;
            RefreshHint();
            Rebuild();
        }

        private void Close()
        {
            Visible = false;
            Closed?.Invoke();
        }

        private void Rebuild()
        {
            foreach (var child in _list.GetChildren())
                child.QueueFree();

            var games = GameCatalog.Games(_sessionInstalled);
            if (games.Count == 0)
            {
                _list.AddChild(UiKit.Label(TranslationServer.Translate("menu/play/no_games"), UiTokens.FontBody, UiTokens.Muted));
                return;
            }

            foreach (var game in games)
                _list.AddChild(BuildCard(game));
        }

        private Control BuildCard(ModCollectionGameSummary game)
        {
            var collection = game.Collection;
            var saves = SaveService.ListSaves(collection.Id);
            var newest = saves.OrderByDescending(s => s.SaveTime).FirstOrDefault();

            var card = new PanelContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
            var cardStyle = UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, ActiveGame.Id == collection.Id ? UiTokens.AccentLine : UiTokens.LineCard);
            cardStyle.ContentMarginLeft = cardStyle.ContentMarginRight = UiTokens.Space5;
            cardStyle.ContentMarginTop = cardStyle.ContentMarginBottom = UiTokens.Space5;
            card.AddThemeStyleboxOverride("panel", cardStyle);

            var row = UiKit.Row(UiTokens.Space4);
            row.MouseFilter = MouseFilterEnum.Pass;
            card.AddChild(row);

            row.AddChild(BuildCover(game, collection));

            var info = UiKit.Column(UiTokens.Space1);
            info.SizeFlagsHorizontal = SizeFlags.ExpandFill;
            row.AddChild(info);
            info.AddChild(UiKit.Label(game.Name ?? collection.Id, UiTokens.FontH3, UiTokens.InkBright));
            var description = UiKit.Label(game.Description ?? string.Empty, UiTokens.FontBodySmall, UiTokens.Body);
            description.AutowrapMode = TextServer.AutowrapMode.WordSmart;
            info.AddChild(description);

            var (statusText, statusColor) = Status(game);
            info.AddChild(UiKit.Label(statusText, UiTokens.FontSmall, statusColor));
            info.AddChild(UiKit.Label(saves.Count == 0
                    ? TranslationServer.Translate("menu/play/no_saves")
                    : string.Format(TranslationServer.Translate("menu/play/saves"), saves.Count, newest.SaveTime.ToLocalTime().ToString("dd.MM.yy HH:mm")),
                UiTokens.FontSmall, UiTokens.Faint));

            var buttons = UiKit.Column(UiTokens.Space2);
            buttons.MouseFilter = MouseFilterEnum.Pass;
            buttons.CustomMinimumSize = new Vector2(190, 0);
            row.AddChild(buttons);

            // With a save, Continue is the main action of the card; otherwise New game is.
            if (newest != null)
            {
                var resume = UiKit.Button(TranslationServer.Translate("menu/play/continue"), ButtonKind.Primary);
                resume.Disabled = !game.IsReady;
                resume.Pressed += () => GameFlow.RequestLoad(this, collection.Id, newest.FileName);
                buttons.AddChild(resume);
            }

            var start = UiKit.Button(TranslationServer.Translate("menu/play/new_game"), newest == null ? ButtonKind.Primary : ButtonKind.Secondary);
            start.Disabled = !game.IsReady;
            start.Pressed += () => GameFlow.StartGame(this, collection.Id);
            buttons.AddChild(start);

            if (newest != null)
            {

                var browse = UiKit.Button(TranslationServer.Translate("menu/play/browse_saves"));
                browse.Pressed += () => SavesRequested?.Invoke(collection.Id);
                buttons.AddChild(browse);
            }

            if (!game.IsReady && game.MissingIds.Count == 0 && game.MismatchIds.Count == 0
                && game.ProblemsCount == 0 && game.PlanInstallCount + game.PlanEnableCount > 0)
            {
                var install = UiKit.Button(TranslationServer.Translate("menu/play/install"));
                install.Pressed += () => Install(collection, install);
                buttons.AddChild(install);
            }

            if (MenuRegistry.GetButtonById(ModsHubId) != null)
            {
                var configure = UiKit.Button(TranslationServer.Translate("menu/play/configure"));
                configure.Pressed += () => OpenEditor(collection.Id);
                buttons.AddChild(configure);
            }

            return card;
        }

        /// <summary>The collection's cover in a 16:9 frame; a game without one shows the first letter of its name.</summary>
        private static Control BuildCover(ModCollectionGameSummary game, ModCollection collection)
        {
            var frame = new PanelContainer
            {
                CustomMinimumSize = new Vector2(208, 117),
                SizeFlagsVertical = SizeFlags.ShrinkBegin,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            frame.AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well, UiTokens.Line));

            var cover = GameCatalog.LoadCover(collection);
            if (cover != null)
            {
                frame.AddChild(new TextureRect
                {
                    Texture = cover,
                    ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                    StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
                    MouseFilter = MouseFilterEnum.Ignore,
                });
                return frame;
            }

            var name = game.Name ?? collection.Id;
            var letter = UiKit.Label(string.IsNullOrEmpty(name) ? "?" : name.Substring(0, 1).ToUpperInvariant(), UiTokens.FontH1, UiTokens.Faint);
            letter.HorizontalAlignment = HorizontalAlignment.Center;
            letter.VerticalAlignment = VerticalAlignment.Center;
            frame.AddChild(letter);
            return frame;
        }

        private static (string Text, Color Color) Status(ModCollectionGameSummary game)
        {
            if (game.IsReady)
                return (TranslationServer.Translate("menu/play/status_ready"), UiTokens.SuccessText);
            if (game.MissingIds.Count > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_missing"), string.Join(", ", game.MissingIds)), UiTokens.DangerText);
            if (game.MismatchIds.Count > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_mismatch"), string.Join(", ", game.MismatchIds)), UiTokens.DangerText);
            if (game.ProblemsCount > 0)
                return (string.Format(TranslationServer.Translate("menu/play/status_problems"), game.ProblemsCount), UiTokens.Warning);
            return (string.Format(TranslationServer.Translate("menu/play/status_install"), game.PlanInstallCount, game.PlanEnableCount), UiTokens.Warning);
        }

        private const string ModsHubId = "mods_hub";

        /// <summary>Installs/enables the missing mods; entering the game afterwards reloads the mod set to pick them up.</summary>
        private async void Install(ModCollection collection, Button button)
        {
            button.Disabled = true;
            button.Text = TranslationServer.Translate("menu/play/installing");

            var collections = GameCatalog.LoadCollections();
            var catalog = CatalogService.CachedEntries ?? new List<CatalogExportEntry>();
            var graph = ModCollectionResolver.Resolve(collection, collections, ModRegistry.Instance.AllDiscoveredMods,
                DisabledModsStore.Load(), catalog, _sessionInstalled);
            var ok = await ModCollectionInstaller.ApplyAsync(ModCollectionPlan.Build(graph, catalog), _sessionInstalled);
            if (!IsInstanceValid(this))
                return;

            if (ok)
                GameFlow.ReloadRequired = true;
            GameFlow.Confirm(this,
                TranslationServer.Translate("menu/play/install"),
                TranslationServer.Translate(ok ? "menu/play/install_done" : "menu/play/install_failed"),
                null, null);
            Rebuild();
        }

        /// <summary>Opens the mod manager on the game's scheme (loose coupling through the menu registry).</summary>
        private void OpenEditor(string collectionId)
        {
            var entry = MenuRegistry.GetButtonById(ModsHubId);
            var host = GetParent() as Control ?? this;
            var hub = MenuBuilder.OpenScene(host, entry?.ScenePath, ModsHubId);
            if (hub != null && hub.HasMethod("OpenCollectionEditor"))
                hub.CallDeferred("OpenCollectionEditor", collectionId);
            Close();
        }
    }
}
