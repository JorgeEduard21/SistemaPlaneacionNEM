using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace ViewModels
{
    public class MainViewModel : INotifyPropertyChanged
    {
        private string _textoResultadoMarkdown;
        public string TextoResultadoMarkdown
        {
            get => _textoResultadoMarkdown;
            set
            {
                if (_textoResultadoMarkdown == value) return;
                _textoResultadoMarkdown = value;
                OnPropertyChanged();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string name = null) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}