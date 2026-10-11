using System.Windows.Input;

namespace Frontend.ViewModels
{
    public class RelayCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Predicate<object?>? _canExecute;
        public RelayCommand(Action<object?> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }
        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }
        public bool CanExecute(object? parameter) => _canExecute == null || _canExecute(parameter);
        public void Execute(object? parameter) => _execute(parameter);
    }

    /// <summary>
    /// Comando asíncrono que evita ejecuciones simultáneas y canaliza los errores
    /// a un manejador central (notificaciones de la ventana principal).
    /// </summary>
    public class AsyncRelayCommand : ICommand
    {
        public static Action<Exception>? ManejadorErroresGlobal { get; set; }

        private readonly Func<object?, Task> _execute;
        private readonly Predicate<object?>? _canExecute;
        private bool _ejecutando;

        public AsyncRelayCommand(Func<object?, Task> execute, Predicate<object?>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter) => !_ejecutando && (_canExecute == null || _canExecute(parameter));

        public async void Execute(object? parameter)
        {
            if (!CanExecute(parameter)) return;
            _ejecutando = true;
            CommandManager.InvalidateRequerySuggested();
            try
            {
                await _execute(parameter);
            }
            catch (OperationCanceledException) { /* cancelado por el usuario */ }
            catch (Exception ex)
            {
                if (ManejadorErroresGlobal != null) ManejadorErroresGlobal(ex);
                else throw;
            }
            finally
            {
                _ejecutando = false;
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
}

