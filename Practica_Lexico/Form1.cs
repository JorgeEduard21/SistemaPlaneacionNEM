using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Practica_Lexico
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            leerArchivo();  
        }

        private void btnAnalizar_Click(object sender, EventArgs e)
        {
            lstSimbolos.Items.Clear();
            lstErrores.Items.Clear();
            string[] lineas = txtCadena.Text.Split(new string[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);

            bool HayComentario = false;


            for (int i = 0; i < lineas.Length; i++)
            {
                int numeroLinea = i + 1;
                string lineaTexto = lineas[i];

                string[] tokens = lineaTexto.Split(new char[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                int indiceBusqueda = 0;

                foreach (string token in tokens)
                {
                    int col = lineaTexto.IndexOf(token, indiceBusqueda) + 1;
                    indiceBusqueda = col + token.Length - 1;

                    if (HayComentario)
                    {
                        if (token.Contains("*/"))
                        {
                            HayComentario = false;
                        }
                        continue;
                    }

                    if (token.StartsWith("/*"))
                    {
                        if (!token.Contains("*/"))
                        {
                            HayComentario = true;
                        }
                        continue;
                    }
                    else if (lstReservadas.Items.Contains(token))
                    {
                        lstSimbolos.Items.Add(token + " -> palabra reservada");
                    }
                    else if ((token == "=") || (token == "<") || (token == ">") || (token == "+") || (token == "-"))
                    {
                        lstSimbolos.Items.Add(token + " -> Operador");
                    }
                    else if (int.TryParse(token, out _))
                    {
                        lstSimbolos.Items.Add(token + " -> numero");
                    }
                    else if (float.TryParse(token, out _))
                    {
                        lstSimbolos.Items.Add(token + " -> numero decimal");
                    }
                    else if (!string.IsNullOrEmpty(token) && char.IsLetter(token[0]))
                    {
                        lstSimbolos.Items.Add(token + " -> variable");
                    }
                    else
                    {
                        MessageBox.Show("Error en la linea " + numeroLinea + " columna " + col + ": token no reconocido -> " + token, "Error de sintaxis", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }

            }
        }
        private void leerArchivo()
        {

            StreamReader Archivo = new StreamReader("C:\\PSBII2026\\LEXICO_RESERVADAS.txt");
                string linea = Archivo.ReadLine();

            this.lstReservadas.Items.Clear();
            while (linea != null)
            {
                this.lstReservadas.Items.Add(linea);
                linea = Archivo.ReadLine();
            }
            Archivo.Close();
        }
    }
}
