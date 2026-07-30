using Flashcards.App.Services;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Flashcards.App.ViewModels;

namespace Flashcards.App
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel(new LocalizationService());
            PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            // clicking away from TextBox = puts it outside of focus
            System.Diagnostics.Debug.WriteLine($"Clicked on: {e.OriginalSource}");
            if (e.OriginalSource is not TextBox)
            {
                Keyboard.Focus(this);
            }
            System.Diagnostics.Debug.WriteLine($"After: TextBox.IsFocused = {TopicNameTextBox.IsFocused}");
        }
    }
}