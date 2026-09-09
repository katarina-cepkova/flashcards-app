using System.Windows.Media;

namespace Flashcards.App.Services
{
    /// <summary>
    /// Converts between System.Windows.Media.Color (used by WPF bindings/resources) and the
    /// packed ARGB int stored on Flashcard.ColorArgb (kept as a plain int there so Core has no
    /// WPF dependency).
    /// </summary>
    internal static class ArgbColorConverter
    {
        /// <summary>Packs a WPF Color into a single ARGB int (0xAARRGGBB), matching Flashcard.ColorArgb's format.</summary>
        public static int ToArgb(Color color) =>
            (color.A << 24) | (color.R << 16) | (color.G << 8) | color.B;

        /// <summary>Unpacks an ARGB int (as stored in Flashcard.ColorArgb) back into a WPF Color.</summary>
        public static Color FromArgb(int argb) => Color.FromArgb(
            (byte)((argb >> 24) & 0xFF),
            (byte)((argb >> 16) & 0xFF),
            (byte)((argb >> 8) & 0xFF),
            (byte)(argb & 0xFF));

        /// <summary>
        /// Perceptual relative luminance of an ARGB color (0 = black, 255 = white), using the
        /// standard weighted formula — human eyes are more sensitive to green than red or blue,
        /// so green is weighted heaviest. Ignores alpha, since flashcard colors are always opaque.
        /// </summary>
        public static double CalculateLuminance(int argb)
        {
            Color color = FromArgb(argb);
            return 0.299 * color.R + 0.587 * color.G + 0.114 * color.B;
        }
    }
}