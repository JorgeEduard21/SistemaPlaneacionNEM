using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Frontend.Models;
using Frontend.Services;

namespace Frontend.Views
{
    public partial class AsistenteGoogleDocsWindow : Window
    {
        private static AsistenteGoogleDocsWindow? _instanciaActiva;

        private readonly Planeacion _planeacion;
        private readonly Func<Task>? _onDescargarWord;
        private readonly DispatcherTimer _timer;
        private readonly DateTime _inicio;
        private readonly int _miPid;

        private IntPtr _ultimoNavegadorHwnd = IntPtr.Zero;
        private bool _pegadoAutomaticoRealizado;
        private bool _ejecutandoPegado;

        // Win32 API para detectar la ventana activa del navegador y enviar Ctrl+V automáticamente
        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_CONTROL = 0x11;
        private const byte VK_V = 0x56;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        public static void MostrarEInsertar(Planeacion planeacion, Func<Task>? onDescargarWord = null)
        {
            try
            {
                _instanciaActiva?.Close();
            }
            catch { }

            var ventana = new AsistenteGoogleDocsWindow(planeacion, onDescargarWord);
            _instanciaActiva = ventana;
            ventana.Show();
        }

        public AsistenteGoogleDocsWindow(Planeacion planeacion, Func<Task>? onDescargarWord)
        {
            InitializeComponent();
            _planeacion = planeacion;
            _onDescargarWord = onDescargarWord;
            _inicio = DateTime.UtcNow;
            _miPid = Environment.ProcessId;

            PosicionarEsquinaSuperiorDerecha();

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(450)
            };
            _timer.Tick += Timer_Tick;
            _timer.Start();

            Closed += (_, _) => _timer.Stop();
        }

        private void PosicionarEsquinaSuperiorDerecha()
        {
            var area = SystemParameters.WorkArea;
            Left = Math.Max(20, area.Right - Width - 20);
            Top = Math.Max(20, area.Top + 20);
        }

        private async void Timer_Tick(object? sender, EventArgs e)
        {
            if (_ejecutandoPegado) return;

            // Rastrear la ventana activa si no es nuestra propia aplicación
            var fg = GetForegroundWindow();
            if (fg != IntPtr.Zero && !EsVentanaPropia(fg))
            {
                var tituloFg = ObtenerTituloVentana(fg);
                if (EsVentanaNavegadorODocs(tituloFg))
                {
                    _ultimoNavegadorHwnd = fg;
                }
            }

            if (_pegadoAutomaticoRealizado) return;

            var segundos = (DateTime.UtcNow - _inicio).TotalSeconds;

            // Buscar si ya cargó el documento nuevo de Google Docs ("Documento sin título" / "Untitled document")
            var hwndDocsListo = BuscarVentanaGoogleDocsLista(out bool esDocumentoNuevoConfirmado);

            if (hwndDocsListo != IntPtr.Zero && (esDocumentoNuevoConfirmado || segundos >= 6.5))
            {
                _pegadoAutomaticoRealizado = true;
                _ultimoNavegadorHwnd = hwndDocsListo;

                TxtTituloEstado.Text = "¡Hoja detectada! Pegando planeación...";
                TxtDetalleEstado.Text = "Google Docs ya abrió. Insertando automáticamente tus tablas, colores y actividades...";

                // Esperar ~1.8 segundos para que el lienzo interno de Google Docs ponga el cursor activo
                await EjecutarPegadoEnNavegadorAsync(hwndDocsListo, esperaInicialMs: 1850);
            }
            else if (segundos > 30)
            {
                // Si pasaron más de 30 segundos (ej. internet muy lento o inicio de sesión de Google)
                _pegadoAutomaticoRealizado = true;
                BarraProgreso.Visibility = Visibility.Collapsed;
                TxtIconoEstado.Text = "👆";
                TxtTituloEstado.Text = "¿Ya abrió tu hoja de Google Docs?";
                TxtDetalleEstado.Text = "Haz clic primero dentro de la hoja en blanco de Google Docs y luego presiona el botón naranja de aquí abajo para insertar tu planeación automáticamente.";
            }
        }

        private async Task EjecutarPegadoEnNavegadorAsync(IntPtr targetHwnd, int esperaInicialMs)
        {
            _ejecutandoPegado = true;
            try
            {
                // Asegurar que la planeación esté fresca en el portapapeles
                ExportadorGoogleDocs.CopiarConFormatoParaGoogleDocs(_planeacion);

                if (esperaInicialMs > 0)
                {
                    await Task.Delay(esperaInicialMs);
                }

                if (targetHwnd == IntPtr.Zero || !IsWindowVisible(targetHwnd))
                {
                    targetHwnd = BuscarVentanaGoogleDocsLista(out _);
                }

                if (targetHwnd != IntPtr.Zero)
                {
                    SetForegroundWindow(targetHwnd);
                    await Task.Delay(320);
                }

                EnviarCtrlV();

                // Segundo intento suave por si Google Docs tardó un instante más en enfocar el editor
                await Task.Delay(1100);

                BarraProgreso.Visibility = Visibility.Collapsed;
                TxtIconoEstado.Text = "✅";
                TxtTituloEstado.Text = "¡Planeación insertada en Google Docs!";
                CajaMensaje.Background = new SolidColorBrush(Color.FromRgb(0xF0, 0xFD, 0xF4));
                CajaMensaje.BorderBrush = new SolidColorBrush(Color.FromRgb(0x86, 0xEF, 0xAC));
                TxtDetalleEstado.Foreground = new SolidColorBrush(Color.FromRgb(0x16, 0x65, 0x34));
                TxtDetalleEstado.Text = "¡Listo! Tu planeación ya se envió a la hoja de Google Docs.\n\n💡 Si tu internet tardó en cargar la página y aún la ves en blanco, solo haz clic en el botón naranja de abajo:";
                TxtBotonPegar.Text = "Volver a Insertar Planeación (1 clic)";
            }
            catch
            {
                BarraProgreso.Visibility = Visibility.Collapsed;
                TxtIconoEstado.Text = "📋";
                TxtTituloEstado.Text = "Listo para insertar con 1 clic";
                TxtDetalleEstado.Text = "Haz clic en el botón naranja de abajo para pegar tu planeación automáticamente en Google Docs:";
            }
            finally
            {
                _ejecutandoPegado = false;
            }
        }

        private static void EnviarCtrlV()
        {
            keybd_event(VK_CONTROL, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, 0, UIntPtr.Zero);
            keybd_event(VK_V, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            keybd_event(VK_CONTROL, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
        }

        private bool EsVentanaPropia(IntPtr hWnd)
        {
            GetWindowThreadProcessId(hWnd, out uint pid);
            return pid == (uint)_miPid;
        }

        private static string ObtenerTituloVentana(IntPtr hWnd)
        {
            var sb = new StringBuilder(512);
            GetWindowText(hWnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static bool EsVentanaNavegadorODocs(string titulo)
        {
            if (string.IsNullOrWhiteSpace(titulo)) return false;
            return titulo.Contains("Google Docs", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Documentos de Google", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Documento sin título", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Untitled document", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("docs.google.com", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("docs.new", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Chrome", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Edge", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Firefox", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Brave", StringComparison.OrdinalIgnoreCase) ||
                   titulo.Contains("Opera", StringComparison.OrdinalIgnoreCase);
        }

        private IntPtr BuscarVentanaGoogleDocsLista(out bool esDocumentoNuevoConfirmado)
        {
            IntPtr encontradoConfirmado = IntPtr.Zero;
            IntPtr encontradoNavegador = IntPtr.Zero;

            EnumWindows((hWnd, _) =>
            {
                if (!IsWindowVisible(hWnd) || EsVentanaPropia(hWnd)) return true;

                var titulo = ObtenerTituloVentana(hWnd);
                if (string.IsNullOrWhiteSpace(titulo)) return true;

                if (titulo.Contains("Documento sin título", StringComparison.OrdinalIgnoreCase) ||
                    titulo.Contains("Untitled document", StringComparison.OrdinalIgnoreCase))
                {
                    encontradoConfirmado = hWnd;
                    return false; // encontrado
                }

                if (titulo.Contains("Documentos de Google", StringComparison.OrdinalIgnoreCase) ||
                    titulo.Contains("Google Docs", StringComparison.OrdinalIgnoreCase))
                {
                    encontradoNavegador = hWnd;
                }

                return true;
            }, IntPtr.Zero);

            if (encontradoConfirmado != IntPtr.Zero)
            {
                esDocumentoNuevoConfirmado = true;
                return encontradoConfirmado;
            }

            esDocumentoNuevoConfirmado = false;
            return encontradoNavegador != IntPtr.Zero ? encontradoNavegador : _ultimoNavegadorHwnd;
        }

        private async void BtnPegarManual_Click(object sender, RoutedEventArgs e)
        {
            if (_ejecutandoPegado) return;
            BarraProgreso.Visibility = Visibility.Visible;
            TxtIconoEstado.Text = "⏳";
            TxtTituloEstado.Text = "Insertando planeación...";
            await EjecutarPegadoEnNavegadorAsync(_ultimoNavegadorHwnd, esperaInicialMs: 250);
        }

        private async void BtnWord_Click(object sender, RoutedEventArgs e)
        {
            if (_onDescargarWord != null)
            {
                await _onDescargarWord();
            }
            Close();
        }

        private void BtnCerrar_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
