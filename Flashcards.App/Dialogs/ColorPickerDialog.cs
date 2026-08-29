using System.Windows.Interop;
using System.Windows.Forms;
using System.Drawing;

namespace Flashcards.App.Dialogs
{
    /// <summary>
    /// Shows the system color picker (System.Windows.Forms.ColorDialog) for choosing a
    /// flashcard's background color. Isolated here since ColorDialog has no WPF equivalent,
    /// requiring a WinForms dependency and a Win32 interop bridge that shouldn't leak into
    /// ViewModels.
    /// </summary>
    internal static class ColorPickerDialog
    {
        /// <summary>
        /// Minimal adapter that lets a WPF window act as ColorDialog's owner. ColorDialog (WinForms) only accepts an
        /// IWin32Window as owner, and WPF's Window class doesn't implement that interface itself — so this wraps a raw
        /// window handle in the shape ColorDialog expects, without needing a real WinForms Form.
        /// </summary>
        private sealed class Win32WindowWrapper : System.Windows.Forms.IWin32Window
        {
            /// <summary>The wrapped window's handle — its ID within Windows. IntPtr is the type meant for this.</summary>
            public IntPtr Handle { get; }
            public Win32WindowWrapper(IntPtr handle) => Handle = handle;
        }

        /// <summary>
        /// Shows the system color picker, pre-filled with currentColorArgb, modal over the app's main
        /// window (blocks interaction with it until the dialog is closed). Returns the chosen color as
        /// ARGB if the user confirmed with OK, or null if they cancelled/closed the dialog instead.
        /// </summary>
        public static int? PickColor(int currentColorArgb)
        {
            using var dialog = new ColorDialog();
            dialog.Color = Color.FromArgb(currentColorArgb);

            dialog.CustomColors = new[]
            {
                ToColorRef(0xF6, 0xD8, 0xCE),  // Peach
                ToColorRef(0xF7, 0xEC, 0xC7),  // Butter
                ToColorRef(0xE3, 0xEF, 0xCB),  // Sage
                ToColorRef(0xCD, 0xE9, 0xD4),  // Mint
                ToColorRef(0xC7, 0xE4, 0xEC),  // Powder blue
                ToColorRef(0xD3, 0xD6, 0xF1),  // Periwinkle
                ToColorRef(0xEA, 0xD1, 0xE7),  // Lilac
                ToColorRef(0xF3, 0xD2, 0xDD),  // Blush

                ToColorRef(0x62, 0x2F, 0x1F),  // Terracotta
                ToColorRef(0x75, 0x42, 0x1F),  // Caramel
                ToColorRef(0x3E, 0x52, 0x20),  // Olive
                ToColorRef(0x26, 0x52, 0x37),  // Forest
                ToColorRef(0x1C, 0x4A, 0x52),  // Steel teal
                ToColorRef(0x33, 0x37, 0x63),  // Indigo
                ToColorRef(0x5A, 0x33, 0x60),  // Plum
                ToColorRef(0x68, 0x29, 0x3D),  // Berry
            };

            // MainWindow (WPF) has no Handle of its own we can read directly — WPF doesn't expose
            // it, since apps don't normally need to talk to Windows at this level. WindowInteropHelper
            // reaches underneath WPF to get it anyway, since every WPF window still has a real Win32
            // window (and thus a handle) under the hood.
            IntPtr ownerHandle = new WindowInteropHelper(System.Windows.Application.Current.MainWindow).Handle;

            // ShowDialog needs an IWin32Window, not a bare handle — Win32WindowWrapper just carries
            // ownerHandle in the shape ShowDialog expects.
            var owner = new Win32WindowWrapper(ownerHandle);

            // Blocks here until the user closes the dialog — the rest of this method only runs
            // once that happens.
            if (dialog.ShowDialog(owner) == DialogResult.OK)
                return dialog.Color.ToArgb();

            return null;
        }

        /// <summary>
        /// Converts an R, G, B triple into the Win32 COLORREF format ColorDialog.CustomColors expects
        /// (0x00BBGGRR — bytes in reverse order from typical RGB, and no alpha channel).
        /// </summary>
        private static int ToColorRef(byte r, byte g, byte b) =>
            r | (g << 8) | (b << 16);
    }

}