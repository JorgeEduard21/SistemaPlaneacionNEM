using System.ComponentModel;
using System.Windows;
using Frontend.ViewModels;

namespace Frontend
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.CerrarBackend();
            }
        }
    }
}