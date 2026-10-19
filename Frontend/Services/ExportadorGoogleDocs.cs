using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using Frontend.Models;

namespace Frontend.Services
{
    /// <summary>
    /// Convierte el Markdown de la planeación a HTML con estilos cálidos NEM y lo coloca
    /// en el portapapeles de Windows en formato CF_HTML + texto plano para pegarlo en Google Docs con Ctrl+V.
    /// </summary>
    public static class ExportadorGoogleDocs
    {
        private static readonly Regex InlineRegex = new(@"(\*\*[^*]+\*\*|\*[^*]+\*|__[^_]+__|_[^_]+_)", RegexOptions.Compiled);
        private static readonly Regex SeparadorTablaRegex = new(@"^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$", RegexOptions.Compiled);

        public static void CopiarConFormatoParaGoogleDocs(Planeacion planeacion)
        {
            var htmlBody = ConvertirMarkdownAHtml(planeacion);
            var dataObject = new DataObject();

            var cfHtml = EmpaquetarCfHtml(htmlBody);
            dataObject.SetData(DataFormats.Html, cfHtml);
            dataObject.SetData(DataFormats.UnicodeText, planeacion.Contenido ?? "");
            dataObject.SetData(DataFormats.Text, planeacion.Contenido ?? "");

            Clipboard.SetDataObject(dataObject, true);
        }

        public static string ConvertirMarkdownAHtml(Planeacion planeacion)
        {
            var sb = new StringBuilder();
            sb.Append("<div style=\"font-family:'Calibri','Arial',sans-serif;font-size:11pt;color:#1E293B;line-height:1.5;\">");

            var p = planeacion.Perfil;
            var encabezado = string.Join(" · ", new[] { p.Escuela, p.NombreDocente, p.CicloEscolar }.Where(x => !string.IsNullOrWhiteSpace(x)));
            if (!string.IsNullOrWhiteSpace(encabezado))
            {
                sb.Append($"<p style=\"font-size:9.5pt;color:#64748B;border-bottom:1px solid #E5D8C5;padding-bottom:6px;margin-bottom:14px;\">{WebUtility.HtmlEncode(encabezado)}</p>");
            }

            var lineas = (planeacion.Contenido ?? "").Replace("\r\n", "\n").Split('\n');
            int i = 0;
            bool enListaUl = false;
            bool enListaOl = false;

            void CerrarListas()
            {
                if (enListaUl) { sb.Append("</ul>"); enListaUl = false; }
                if (enListaOl) { sb.Append("</ol>"); enListaOl = false; }
            }

            while (i < lineas.Length)
            {
                var linea = lineas[i].TrimEnd();
                var sinEspacios = linea.Trim();

                if (string.IsNullOrEmpty(sinEspacios) || Regex.IsMatch(sinEspacios, @"^(-{3,}|\*{3,}|_{3,})$"))
                {
                    CerrarListas();
                    i++;
                    continue;
                }

                // Tablas Markdown
                if (sinEspacios.StartsWith("|"))
                {
                    CerrarListas();
                    var bloque = new List<string>();
                    while (i < lineas.Length && lineas[i].Trim().StartsWith("|"))
                    {
                        bloque.Add(lineas[i]);
                        i++;
                    }
                    RenderizarTabla(sb, bloque);
                    continue;
                }

                // Encabezados
                var mHeading = Regex.Match(sinEspacios, @"^(#{1,6})\s+(.*)$");
                if (mHeading.Success)
                {
                    CerrarListas();
                    int nivel = Math.Min(mHeading.Groups[1].Value.Length, 4);
                    var texto = mHeading.Groups[2].Value.Trim().Trim('*');
                    var color = nivel == 1 ? "#C25A38" : "#4E7D6E";
                    var size = nivel switch { 1 => "18pt", 2 => "14pt", 3 => "12pt", _ => "11pt" };
                    sb.Append($"<h{nivel} style=\"color:{color};font-size:{size};margin-top:14px;margin-bottom:6px;font-weight:bold;\">{FormatearInline(texto)}</h{nivel}>");
                    i++;
                    continue;
                }

                // Lista con viñetas
                var mBullet = Regex.Match(linea, @"^(\s*)[-*+]\s+(.*)$");
                if (mBullet.Success)
                {
                    if (enListaOl) { sb.Append("</ol>"); enListaOl = false; }
                    if (!enListaUl) { sb.Append("<ul style=\"margin-top:4px;margin-bottom:8px;\">"); enListaUl = true; }
                    var texto = mBullet.Groups[2].Value;
                    var mCasilla = Regex.Match(texto, @"^\[( |x|X)\]\s*(.*)$");
                    if (mCasilla.Success)
                        texto = (mCasilla.Groups[1].Value.Equals("x", StringComparison.OrdinalIgnoreCase) ? "☑ " : "☐ ") + mCasilla.Groups[2].Value;
                    sb.Append($"<li style=\"margin-bottom:3px;\">{FormatearInline(texto)}</li>");
                    i++;
                    continue;
                }

                // Lista numerada
                var mNum = Regex.Match(linea, @"^(\s*)\d+[.)]\s+(.*)$");
                if (mNum.Success)
                {
                    if (enListaUl) { sb.Append("</ul>"); enListaUl = false; }
                    if (!enListaOl) { sb.Append("<ol style=\"margin-top:4px;margin-bottom:8px;\">"); enListaOl = true; }
                    sb.Append($"<li style=\"margin-bottom:3px;\">{FormatearInline(mNum.Groups[2].Value)}</li>");
                    i++;
                    continue;
                }

                // Cita
                if (sinEspacios.StartsWith(">"))
                {
                    CerrarListas();
                    var cita = sinEspacios.TrimStart('>', ' ');
                    sb.Append($"<blockquote style=\"border-left:3px solid #C25A38;padding-left:10px;color:#475569;font-style:italic;margin:8px 0;\">{FormatearInline(cita)}</blockquote>");
                    i++;
                    continue;
                }

                CerrarListas();
                sb.Append($"<p style=\"margin:6px 0;\">{FormatearInline(sinEspacios)}</p>");
                i++;
            }

            CerrarListas();
            sb.Append("</div>");
            return sb.ToString();
        }

        private static void RenderizarTabla(StringBuilder sb, List<string> filasRaw)
        {
            var datos = filasRaw
                .Where(f => !SeparadorTablaRegex.IsMatch(f))
                .Select(ExtraerCeldas)
                .ToList();

            if (datos.Count == 0) return;

            int cols = datos.Max(f => f.Count);
            sb.Append("<table style=\"border-collapse:collapse;width:100%;margin:10px 0;font-size:10pt;\">");
            for (int r = 0; r < datos.Count; r++)
            {
                var fila = datos[r];
                bool esHeader = r == 0;
                sb.Append("<tr>");
                for (int c = 0; c < cols; c++)
                {
                    var val = c < fila.Count ? fila[c] : "";
                    var tag = esHeader ? "th" : "td";
                    var bg = esHeader ? "background-color:#F3E6DC;font-weight:bold;color:#1E293B;" : "background-color:#FFFFFF;";
                    sb.Append($"<{tag} style=\"border:1px solid #CBD5E1;padding:6px 8px;text-align:left;vertical-align:top;{bg}\">{FormatearInline(val)}</{tag}>");
                }
                sb.Append("</tr>");
            }
            sb.Append("</table>");
        }

        private static List<string> ExtraerCeldas(string fila)
        {
            fila = fila.Trim();
            if (fila.StartsWith("|")) fila = fila[1..];
            if (fila.EndsWith("|")) fila = fila[..^1];
            return fila.Split('|').Select(c => c.Trim()).ToList();
        }

        private static string FormatearInline(string texto)
        {
            var sb = new StringBuilder();
            foreach (var parte in InlineRegex.Split(texto))
            {
                if (string.IsNullOrEmpty(parte)) continue;
                if ((parte.StartsWith("**") && parte.EndsWith("**")) || (parte.StartsWith("__") && parte.EndsWith("__")))
                {
                    sb.Append("<b>").Append(WebUtility.HtmlEncode(parte[2..^2])).Append("</b>");
                }
                else if ((parte.StartsWith("*") && parte.EndsWith("*")) || (parte.StartsWith("_") && parte.EndsWith("_")))
                {
                    sb.Append("<i>").Append(WebUtility.HtmlEncode(parte[1..^1])).Append("</i>");
                }
                else
                {
                    sb.Append(WebUtility.HtmlEncode(parte));
                }
            }
            return sb.ToString();
        }

        private static string EmpaquetarCfHtml(string htmlFragment)
        {
            const string marcadorInicioHtml = "<html><body><!--StartFragment-->";
            const string marcadorFinHtml = "<!--EndFragment--></body></html>";
            const string plantillaCabecera =
                "Version:0.9\r\n" +
                "StartHTML:{0:D10}\r\n" +
                "EndHTML:{1:D10}\r\n" +
                "StartFragment:{2:D10}\r\n" +
                "EndFragment:{3:D10}\r\n";

            var cabeceraDummy = string.Format(plantillaCabecera, 0, 0, 0, 0);
            int startHtml = Encoding.UTF8.GetByteCount(cabeceraDummy);
            int startFragment = startHtml + Encoding.UTF8.GetByteCount(marcadorInicioHtml);
            int endFragment = startFragment + Encoding.UTF8.GetByteCount(htmlFragment);
            int endHtml = endFragment + Encoding.UTF8.GetByteCount(marcadorFinHtml);

            var cabeceraReal = string.Format(plantillaCabecera, startHtml, endHtml, startFragment, endFragment);
            return cabeceraReal + marcadorInicioHtml + htmlFragment + marcadorFinHtml;
        }
    }
}
