using System.Collections.ObjectModel;

namespace Frontend.ViewModels
{
    /// <summary>Elemento seleccionable (chip o tarjeta) enlazado a un ToggleButton.</summary>
    public class OpcionSeleccionable : ViewModelBase
    {
        private readonly GrupoOpciones _grupo;
        private bool _isSelected;

        public string Nombre { get; }
        public string? Icono { get; }
        public string? Descripcion { get; }

        public OpcionSeleccionable(GrupoOpciones grupo, string nombre, string? icono = null, string? descripcion = null)
        {
            _grupo = grupo;
            Nombre = nombre;
            Icono = icono;
            Descripcion = descripcion;
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                OnPropertyChanged();
                _grupo.NotificarCambio(this);
            }
        }

        internal void EstablecerSinNotificar(bool valor)
        {
            if (_isSelected == valor) return;
            _isSelected = valor;
            OnPropertyChanged(nameof(IsSelected));
        }
    }

    /// <summary>Grupo de opciones de selección única o múltiple.</summary>
    public class GrupoOpciones : ViewModelBase
    {
        public bool Multiple { get; }
        public ObservableCollection<OpcionSeleccionable> Opciones { get; } = new();
        public event EventHandler? Cambio;

        public GrupoOpciones(bool multiple) => Multiple = multiple;

        public GrupoOpciones Agregar(string nombre, string? icono = null, string? descripcion = null)
        {
            Opciones.Add(new OpcionSeleccionable(this, nombre, icono, descripcion));
            return this;
        }

        public string? Seleccion => Opciones.FirstOrDefault(o => o.IsSelected)?.Nombre;
        public List<string> Seleccionadas => Opciones.Where(o => o.IsSelected).Select(o => o.Nombre).ToList();
        public bool TieneSeleccion => Opciones.Any(o => o.IsSelected);

        internal void NotificarCambio(OpcionSeleccionable origen)
        {
            if (!Multiple && origen.IsSelected)
                foreach (var o in Opciones.Where(o => o != origen))
                    o.EstablecerSinNotificar(false);
            Avisar();
        }

        /// <summary>Selecciona exactamente los valores indicados (ignora mayúsculas).</summary>
        public void Establecer(IEnumerable<string>? valores)
        {
            var set = new HashSet<string>(valores ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
            var yaSeleccionada = false;
            foreach (var o in Opciones)
            {
                var sel = set.Contains(o.Nombre) && (Multiple || !yaSeleccionada);
                yaSeleccionada |= sel;
                o.EstablecerSinNotificar(sel);
            }
            Avisar();
        }

        public void Establecer(string? valor) => Establecer(string.IsNullOrEmpty(valor) ? null : new[] { valor });

        private void Avisar()
        {
            OnPropertyChanged(nameof(Seleccion));
            OnPropertyChanged(nameof(Seleccionadas));
            OnPropertyChanged(nameof(TieneSeleccion));
            Cambio?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Lista editable de textos (PDA, BAP, criterios) con caja de captura.</summary>
    public class ListaEditable : ViewModelBase
    {
        private string _nuevo = "";
        public ObservableCollection<string> Elementos { get; } = new();
        public event EventHandler? Cambio;

        public string Nuevo { get => _nuevo; set => SetProperty(ref _nuevo, value); }
        public RelayCommand AgregarCommand { get; }
        public RelayCommand QuitarCommand { get; }
        public bool EstaVacia => Elementos.Count == 0;

        public ListaEditable()
        {
            AgregarCommand = new RelayCommand(_ => Agregar(), _ => !string.IsNullOrWhiteSpace(Nuevo));
            QuitarCommand = new RelayCommand(p => { if (p is string s && Elementos.Remove(s)) Avisar(); });
        }

        private void Agregar()
        {
            // Permite pegar varios elementos separados por saltos de línea.
            var nuevos = Nuevo.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                              .Select(x => x.Trim().TrimStart('-', '•', '*').Trim())
                              .Where(x => x.Length > 0);
            foreach (var n in nuevos)
                if (!Elementos.Any(e => string.Equals(e, n, StringComparison.OrdinalIgnoreCase)))
                    Elementos.Add(n);
            Nuevo = "";
            Avisar();
        }

        public void Establecer(IEnumerable<string>? valores)
        {
            Elementos.Clear();
            foreach (var v in valores ?? Enumerable.Empty<string>()) Elementos.Add(v);
            Nuevo = "";
            Avisar();
        }

        public List<string> ComoLista()
        {
            var lista = Elementos.ToList();
            // Si el docente escribió algo pero no presionó "Agregar", se incluye igual.
            if (!string.IsNullOrWhiteSpace(Nuevo) && !lista.Contains(Nuevo.Trim())) lista.Add(Nuevo.Trim());
            return lista;
        }

        private void Avisar()
        {
            OnPropertyChanged(nameof(EstaVacia));
            Cambio?.Invoke(this, EventArgs.Empty);
        }
    }
}

