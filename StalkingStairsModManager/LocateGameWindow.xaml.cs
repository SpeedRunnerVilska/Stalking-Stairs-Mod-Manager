using System.Windows;
using System.Windows.Forms;

namespace StalkingStairsModManager
{
    public partial class LocateGameWindow : Window
    {
        public string SelectedPath { get; private set; }

        public LocateGameWindow()
        {
            InitializeComponent();
        }

        private void Locate_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new FolderBrowserDialog();

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                SelectedPath = dialog.SelectedPath;
                DialogResult = true;
                Close();
            }
        }
    }
}
