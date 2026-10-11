using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Frontend.ViewModels
{
    public class ViewModelBase : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>Asigna el valor y notifica solo si cambió. Devuelve true si hubo cambio.</summary>
        protected bool SetProperty<T>(ref T campo, T valor, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(campo, valor)) return false;
            campo = valor;
            OnPropertyChanged(propertyName);
            return true;
        }
    }
}