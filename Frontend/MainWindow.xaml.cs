using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Threading;
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
            Loaded += MainWindow_Loaded;
            Closing += MainWindow_Closing;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is MainViewModel vm)
            {
                vm.PlaneacionGeneradaExitosamente += (_, _) =>
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        try
                        {
                            SeccionResultadoPlaneacion?.BringIntoView();
                        }
                        catch
                        {
                            // Ignorar si la vista aún no está en pantalla
                        }
                    }), DispatcherPriority.Background);
                };
            }
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