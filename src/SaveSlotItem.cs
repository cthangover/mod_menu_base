using System;
using Cthangover.Core.Settings;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Interface.Base.Lists;
using Cthangover.Mods.Interface.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
	/// <summary>
	/// A single save slot card in the save/load grid. Extends ListItem with
	/// SaveSlotInfo as the model. Renders differently for empty vs. occupied
	/// slots: an empty slot is a dark well with "new save"; occupied slots display the
	/// screenshot, in-game date (from TimeData), real-world save timestamp, and
	/// scene name. The stats label shows party size. Button click emits SlotPressed
	/// with the file name for parent handling. Destruct disconnects the button
	/// signal before QueueFree to avoid phantom callbacks.
	/// </summary>
	public partial class SaveSlotItem : ListItem<SaveSlotInfo>
	{
		private PanelContainer card;
		private TextureRect screenshotRect;
		private Label dateLabel;
		private Label timeLabel;
		private Label sceneLabel;
		private Label emptyLabel;
		private Button slotButton;
		private Label statsLabel;

		/// <summary>
		/// Raised when this specific slot is clicked, carrying the save file name so the parent
		/// <see cref="SaveSlotList"/> can forward it to <see cref="SaveLoadMenu"/>. A plain C#
		/// event because mod assemblies are compiled without the Godot source generators.
		/// </summary>
		public event Action<string> SlotPressed;

		public override void _Ready()
		{
			card = GetNode<PanelContainer>("Card");
			screenshotRect = GetNode<TextureRect>("Card/Column/Shot/ScreenshotRect");
			GetNode<PanelContainer>("Card/Column/Shot").AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusSm, UiTokens.Well));
			dateLabel = GetNode<Label>("Card/Column/Info/Column/DateLabel");
			sceneLabel = GetNode<Label>("Card/Column/Info/Column/SceneLabel");
			timeLabel = GetNode<Label>("Card/Column/Info/Column/Meta/TimeLabel");
			statsLabel = GetNode<Label>("Card/Column/Info/Column/Meta/StatsLabel");
			emptyLabel = GetNode<Label>("EmptyLabel");
			slotButton = GetNode<Button>("SlotButton");

			Style(dateLabel, UiTokens.FontBodyLarge, UiTokens.InkBright);
			Style(sceneLabel, UiTokens.FontBodySmall, UiTokens.Muted);
			Style(timeLabel, UiTokens.FontSmall, UiTokens.Faint);
			Style(statsLabel, UiTokens.FontSmall, UiTokens.Muted);
			Style(emptyLabel, UiTokens.FontBody, UiTokens.Faint);

			// The whole card is one flat button: hover and press only draw the gold frame.
			var hover = UiKit.Frame(UiTokens.RadiusMd, new Color(0, 0, 0, 0), UiTokens.AccentLine);
			slotButton.AddThemeStyleboxOverride("normal", new StyleBoxEmpty());
			slotButton.AddThemeStyleboxOverride("hover", hover);
			slotButton.AddThemeStyleboxOverride("pressed", UiKit.Frame(UiTokens.RadiusMd, UiTokens.AccentSoft, UiTokens.AccentLineStrong));
			slotButton.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
		}

		private static void Style(Label label, int size, Color color)
		{
			label.AddThemeFontSizeOverride("font_size", size);
			label.AddThemeColorOverride("font_color", color);
		}

		/// <summary>
		/// Populates the slot visuals from the <see cref="SaveSlotInfo"/> model. For occupied slots,
		/// loads the screenshot texture, formats the game date from <see cref="SaveSlotInfo.GameTime"/>,
		/// shows the real-world save timestamp, and displays party size. Empty slots show only a
		/// localized "empty" label. Wires the button click signal to emit <see cref="SlotPressed"/>.
		/// </summary>
		public override void Construct(SaveSlotInfo model)
		{
			base.Construct(model);

			card.AddThemeStyleboxOverride("panel", model.IsEmpty
				? UiKit.Frame(UiTokens.RadiusMd, UiTokens.Well, UiTokens.Line)
				: UiKit.Frame(UiTokens.RadiusMd, UiTokens.Surface2, UiTokens.LineCard));

			if (model.IsEmpty)
			{
				card.GetNode<Control>("Column").Visible = false;
				emptyLabel.Visible = true;
				emptyLabel.Text = TranslationServer.Translate("menu/save/new_slot");
			}
			else
			{
				emptyLabel.Visible = false;
				card.GetNode<Control>("Column").Visible = true;

				var tex = SaveScreenshotService.LoadScreenshotFromPath(model.ScreenshotPath);
				if (tex != null)
					screenshotRect.Texture = tex;

				var gameTimeObj = new TimeData(model.GameTime);
				dateLabel.Text = TranslationServer.Translate("ui/save/game_day").ToString()
					.Replace("{day}", (gameTimeObj.Days + 1).ToString())
					.Replace("{time}", gameTimeObj.Text);

				timeLabel.Text = model.SaveTime != System.DateTime.MinValue
					? model.SaveTime.ToLocalTime().ToString("dd.MM.yy HH:mm")
					: "";

				sceneLabel.Text = model.SceneName;
				sceneLabel.Visible = !string.IsNullOrEmpty(model.SceneName) && model.SceneName != "unknown";

				statsLabel.Text = TranslationServer.Translate("ui/save/party_size").ToString()
					.Replace("{count}", model.CharacterCount.ToString());
			}

			slotButton.Pressed += OnPressed;
		}

		private void OnPressed()
		{
			SlotPressed?.Invoke(Model.FileName);
		}

		/// <summary>
		/// Disconnects the button signal to prevent callbacks on freed nodes, then queues
		/// this item for deletion. Called by the <see cref="SaveSlotList"/> when rebuilding
		/// the grid, ensuring no dangling signal references.
		/// </summary>
		public override void Destruct()
		{
			if (slotButton != null && GodotObject.IsInstanceValid(slotButton))
				slotButton.Pressed -= OnPressed;
			QueueFree();
		}
	}
}
