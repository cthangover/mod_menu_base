using System;
using Cthangover.Core.UI.Theming;
using Cthangover.Mods.Interface.Theming;
using Godot;

namespace Cthangover.BaseMenu
{
    /// <summary>
    /// The look of the menu windows (settings, save browser, game picker) — the
    /// style guide's panel shell: a 0.9 backdrop, the window frame, a header
    /// with the caps title on the left and the ✕ on the right, 1px rules around
    /// the body and a footer with a faint hint. The node layout lives in each
    /// window's scene under the same names (<c>Backdrop</c>, <c>Window/Column/…</c>),
    /// this class only paints it, so the three windows cannot drift apart.
    /// </summary>
    public static class MenuWindowShell
    {
        /// <summary>Header row: <c>Title</c>, optional controls, <c>CloseButton</c>.</summary>
        public const string Header = "Window/Column/Header/Row/";

        /// <summary>Body container of the window.</summary>
        public const string Body = "Window/Column/Body/";

        /// <summary>Footer row: <c>Hint</c> and optional buttons.</summary>
        public const string Footer = "Window/Column/Footer/Row/";

        /// <summary>
        /// Paints the shell of <paramref name="root"/> and wires its close button to
        /// <paramref name="close"/>; the title, the close tooltip and the footer
        /// hint keep their texts (callers translate them).
        /// </summary>
        public static void Style(Control root, Action close)
        {
            CthangoverTheme.Apply(root);
            root.GetNode<ColorRect>("Backdrop").Color = UiTokens.Backdrop;
            root.GetNode<PanelContainer>("Window").AddThemeStyleboxOverride("panel", UiKit.Frame(UiTokens.RadiusLg, UiTokens.Surface0, UiTokens.Line));
            root.GetNode<ColorRect>("Window/Column/RuleHeader").Color = UiTokens.Line;
            root.GetNode<ColorRect>("Window/Column/RuleFooter").Color = UiTokens.Line;

            var title = root.GetNode<Label>(Header + "Title");
            title.AddThemeFontSizeOverride("font_size", UiTokens.FontTitle);
            title.AddThemeColorOverride("font_color", UiTokens.InkBright);

            var closeButton = root.GetNode<Button>(Header + "CloseButton");
            CthangoverTheme.ApplyIcon(closeButton);
            closeButton.TooltipText = TranslationServer.Translate("settings/back");
            closeButton.Pressed += close;

            StyleHint(root.GetNode<Label>(Footer + "Hint"));
        }

        /// <summary>Faint helper text: footer hints and notes under a setting.</summary>
        public static void StyleHint(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontBodySmall);
            label.AddThemeColorOverride("font_color", UiTokens.Faint);
        }

        /// <summary>Section caption: small muted caps, as section titles everywhere in the kit.</summary>
        public static void StyleSection(Label label)
        {
            label.AddThemeFontSizeOverride("font_size", UiTokens.FontSmall);
            label.AddThemeColorOverride("font_color", UiTokens.Muted);
        }
    }
}
