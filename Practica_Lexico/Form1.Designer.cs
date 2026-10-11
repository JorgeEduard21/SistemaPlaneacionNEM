namespace Practica_Lexico
{
    partial class Form1
    {
        /// <summary>
        /// Variable del diseñador necesaria.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Limpiar los recursos que se estén usando.
        /// </summary>
        /// <param name="disposing">true si los recursos administrados se deben desechar; false en caso contrario.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Código generado por el Diseñador de Windows Forms

        /// <summary>
        /// Método necesario para admitir el Diseñador. No se puede modificar
        /// el contenido de este método con el editor de código.
        /// </summary>
        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(Form1));
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.lstReservadas = new System.Windows.Forms.ListBox();
            this.txtPalabra = new System.Windows.Forms.TextBox();
            this.button1 = new System.Windows.Forms.Button();
            this.label4 = new System.Windows.Forms.Label();
            this.txtCadena = new System.Windows.Forms.TextBox();
            this.btnAnalizar = new System.Windows.Forms.Button();
            this.textBox1 = new System.Windows.Forms.TextBox();
            this.lstSimbolos = new System.Windows.Forms.ListBox();
            this.label3 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.lstErrores = new System.Windows.Forms.ListView();
            this.button3 = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Myanmar Text", 48F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(276, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(668, 142);
            this.label1.TabIndex = 0;
            this.label1.Text = "ANALISIS LEXICO";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label2.Location = new System.Drawing.Point(53, 145);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(217, 25);
            this.label2.TabIndex = 1;
            this.label2.Text = "Palabras Reservadas";
            // 
            // lstReservadas
            // 
            this.lstReservadas.FormattingEnabled = true;
            this.lstReservadas.ItemHeight = 16;
            this.lstReservadas.Location = new System.Drawing.Point(58, 187);
            this.lstReservadas.Name = "lstReservadas";
            this.lstReservadas.Size = new System.Drawing.Size(302, 292);
            this.lstReservadas.TabIndex = 2;
            // 
            // txtPalabra
            // 
            this.txtPalabra.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtPalabra.Location = new System.Drawing.Point(58, 497);
            this.txtPalabra.Name = "txtPalabra";
            this.txtPalabra.Size = new System.Drawing.Size(186, 34);
            this.txtPalabra.TabIndex = 3;
            // 
            // button1
            // 
            this.button1.Image = ((System.Drawing.Image)(resources.GetObject("button1.Image")));
            this.button1.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button1.Location = new System.Drawing.Point(251, 498);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(109, 44);
            this.button1.TabIndex = 4;
            this.button1.Text = "Agregar";
            this.button1.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button1.UseVisualStyleBackColor = true;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label4.Location = new System.Drawing.Point(394, 165);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(179, 25);
            this.label4.TabIndex = 9;
            this.label4.Text = "Introduce cadena";
            // 
            // txtCadena
            // 
            this.txtCadena.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.txtCadena.Location = new System.Drawing.Point(399, 207);
            this.txtCadena.Multiline = true;
            this.txtCadena.Name = "txtCadena";
            this.txtCadena.Size = new System.Drawing.Size(315, 250);
            this.txtCadena.TabIndex = 10;
            // 
            // btnAnalizar
            // 
            this.btnAnalizar.Image = ((System.Drawing.Image)(resources.GetObject("btnAnalizar.Image")));
            this.btnAnalizar.ImageAlign = System.Drawing.ContentAlignment.BottomCenter;
            this.btnAnalizar.Location = new System.Drawing.Point(734, 207);
            this.btnAnalizar.Name = "btnAnalizar";
            this.btnAnalizar.Padding = new System.Windows.Forms.Padding(0, 5, 0, 0);
            this.btnAnalizar.Size = new System.Drawing.Size(102, 102);
            this.btnAnalizar.TabIndex = 11;
            this.btnAnalizar.Text = "Analizar";
            this.btnAnalizar.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnAnalizar.UseVisualStyleBackColor = true;
            this.btnAnalizar.Click += new System.EventHandler(this.btnAnalizar_Click);
            // 
            // textBox1
            // 
            this.textBox1.Font = new System.Drawing.Font("Microsoft Sans Serif", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.textBox1.Location = new System.Drawing.Point(869, 498);
            this.textBox1.Name = "textBox1";
            this.textBox1.Size = new System.Drawing.Size(183, 34);
            this.textBox1.TabIndex = 14;
            // 
            // lstSimbolos
            // 
            this.lstSimbolos.FormattingEnabled = true;
            this.lstSimbolos.ItemHeight = 16;
            this.lstSimbolos.Location = new System.Drawing.Point(869, 187);
            this.lstSimbolos.Name = "lstSimbolos";
            this.lstSimbolos.Size = new System.Drawing.Size(302, 292);
            this.lstSimbolos.TabIndex = 13;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label3.Location = new System.Drawing.Point(864, 145);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(188, 25);
            this.label3.TabIndex = 12;
            this.label3.Text = "Tabla de simbolos";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label5.Location = new System.Drawing.Point(53, 568);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(162, 25);
            this.label5.TabIndex = 16;
            this.label5.Text = "Lista de errores";
            // 
            // lstErrores
            // 
            this.lstErrores.HideSelection = false;
            this.lstErrores.Location = new System.Drawing.Point(58, 617);
            this.lstErrores.Name = "lstErrores";
            this.lstErrores.Size = new System.Drawing.Size(1113, 118);
            this.lstErrores.TabIndex = 17;
            this.lstErrores.UseCompatibleStateImageBehavior = false;
            this.lstErrores.View = System.Windows.Forms.View.Details;
            // 
            // button3
            // 
            this.button3.Image = ((System.Drawing.Image)(resources.GetObject("button3.Image")));
            this.button3.ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
            this.button3.Location = new System.Drawing.Point(1064, 495);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(107, 46);
            this.button3.TabIndex = 18;
            this.button3.Text = "Agregar";
            this.button3.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            this.button3.UseVisualStyleBackColor = true;
            // 
            // Form1
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.LightSteelBlue;
            this.ClientSize = new System.Drawing.Size(1205, 764);
            this.Controls.Add(this.button3);
            this.Controls.Add(this.lstErrores);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textBox1);
            this.Controls.Add(this.lstSimbolos);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnAnalizar);
            this.Controls.Add(this.txtCadena);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.button1);
            this.Controls.Add(this.txtPalabra);
            this.Controls.Add(this.lstReservadas);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.label1);
            this.Name = "Form1";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.ListBox lstReservadas;
        private System.Windows.Forms.TextBox txtPalabra;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox txtCadena;
        private System.Windows.Forms.Button btnAnalizar;
        private System.Windows.Forms.TextBox textBox1;
        private System.Windows.Forms.ListBox lstSimbolos;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ListView lstErrores;
        private System.Windows.Forms.Button button3;
    }
}

