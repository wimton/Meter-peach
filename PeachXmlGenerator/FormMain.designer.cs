namespace PeachXmlGenerator
{
	partial class FormMain
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose(bool disposing)
		{
			if (disposing && (components != null))
			{
				components.Dispose();
			}
			base.Dispose(disposing);
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		public void InitializeComponent()
		{
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(FormMain));
            this.pictureBox1 = new System.Windows.Forms.PictureBox();
            this.label1 = new System.Windows.Forms.Label();
            this.textBoxDtd = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label5 = new System.Windows.Forms.Label();
            this.textBoxRootNamespace = new System.Windows.Forms.TextBox();
            this.textBoxRootElement = new System.Windows.Forms.TextBox();
            this.buttonDtdBrowse = new System.Windows.Forms.Button();
            this.label6 = new System.Windows.Forms.Label();
            this.buttonSamplesBrowse = new System.Windows.Forms.Button();
            this.textBoxSamples = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.textBoxCount = new System.Windows.Forms.TextBox();
            this.buttonGenerate = new System.Windows.Forms.Button();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).BeginInit();
            this.SuspendLayout();
            // 
            // pictureBox1
            // 
            this.pictureBox1.Image = ((System.Drawing.Image)(resources.GetObject("pictureBox1.Image")));
            this.pictureBox1.InitialImage = ((System.Drawing.Image)(resources.GetObject("pictureBox1.InitialImage")));
            this.pictureBox1.Location = new System.Drawing.Point(276, 185);
            this.pictureBox1.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.pictureBox1.Name = "pictureBox1";
            this.pictureBox1.Size = new System.Drawing.Size(340, 160);
            this.pictureBox1.SizeMode = System.Windows.Forms.PictureBoxSizeMode.Zoom;
            this.pictureBox1.TabIndex = 0;
            this.pictureBox1.TabStop = false;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(100, 18);
            this.label1.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(67, 17);
            this.label1.TabIndex = 1;
            this.label1.Text = "DTD File:";
            // 
            // textBoxDtd
            // 
            this.textBoxDtd.Location = new System.Drawing.Point(177, 15);
            this.textBoxDtd.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxDtd.Name = "textBoxDtd";
            this.textBoxDtd.Size = new System.Drawing.Size(281, 22);
            this.textBoxDtd.TabIndex = 2;
            this.textBoxDtd.Text = "svg.dtd";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(37, 50);
            this.label2.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(129, 17);
            this.label2.TabIndex = 3;
            this.label2.Text = "Root XML Element:";
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(12, 82);
            this.label3.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(153, 17);
            this.label3.TabIndex = 4;
            this.label3.Text = "Root XML Namespace:";
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Location = new System.Drawing.Point(468, 50);
            this.label4.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(68, 17);
            this.label4.TabIndex = 5;
            this.label4.Text = "(optional)";
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(468, 82);
            this.label5.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(68, 17);
            this.label5.TabIndex = 6;
            this.label5.Text = "(optional)";
            // 
            // textBoxRootNamespace
            // 
            this.textBoxRootNamespace.Location = new System.Drawing.Point(177, 79);
            this.textBoxRootNamespace.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxRootNamespace.Name = "textBoxRootNamespace";
            this.textBoxRootNamespace.Size = new System.Drawing.Size(281, 22);
            this.textBoxRootNamespace.TabIndex = 7;
            this.textBoxRootNamespace.Text = "http://www.w3.org/2000/svg";
            // 
            // textBoxRootElement
            // 
            this.textBoxRootElement.Location = new System.Drawing.Point(177, 47);
            this.textBoxRootElement.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxRootElement.Name = "textBoxRootElement";
            this.textBoxRootElement.Size = new System.Drawing.Size(281, 22);
            this.textBoxRootElement.TabIndex = 8;
            this.textBoxRootElement.Text = "svg";
            // 
            // buttonDtdBrowse
            // 
            this.buttonDtdBrowse.Location = new System.Drawing.Point(472, 12);
            this.buttonDtdBrowse.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.buttonDtdBrowse.Name = "buttonDtdBrowse";
            this.buttonDtdBrowse.Size = new System.Drawing.Size(100, 28);
            this.buttonDtdBrowse.TabIndex = 9;
            this.buttonDtdBrowse.Text = "Browse";
            this.buttonDtdBrowse.UseVisualStyleBackColor = true;
            this.buttonDtdBrowse.Click += new System.EventHandler(this.buttonDtdBrowse_Click);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(44, 114);
            this.label6.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(124, 17);
            this.label6.TabIndex = 10;
            this.label6.Text = "Sample XML Files:";
            // 
            // buttonSamplesBrowse
            // 
            this.buttonSamplesBrowse.Location = new System.Drawing.Point(472, 118);
            this.buttonSamplesBrowse.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.buttonSamplesBrowse.Name = "buttonSamplesBrowse";
            this.buttonSamplesBrowse.Size = new System.Drawing.Size(100, 28);
            this.buttonSamplesBrowse.TabIndex = 12;
            this.buttonSamplesBrowse.Text = "Browse";
            this.buttonSamplesBrowse.UseVisualStyleBackColor = true;
            this.buttonSamplesBrowse.Click += new System.EventHandler(this.buttonSamplesBrowse_Click);
            // 
            // textBoxSamples
            // 
            this.textBoxSamples.Location = new System.Drawing.Point(177, 111);
            this.textBoxSamples.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxSamples.Name = "textBoxSamples";
            this.textBoxSamples.Size = new System.Drawing.Size(281, 22);
            this.textBoxSamples.TabIndex = 11;
            this.textBoxSamples.Text = "C:\\Program Files\\peach\\PeachXmlGenerator\\samples-png";
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(12, 146);
            this.label7.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(158, 17);
            this.label7.TabIndex = 13;
            this.label7.Text = "Number of Output Files:";
            // 
            // textBoxCount
            // 
            this.textBoxCount.Location = new System.Drawing.Point(177, 143);
            this.textBoxCount.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.textBoxCount.Name = "textBoxCount";
            this.textBoxCount.Size = new System.Drawing.Size(115, 22);
            this.textBoxCount.TabIndex = 14;
            this.textBoxCount.Text = "100";
            // 
            // buttonGenerate
            // 
            this.buttonGenerate.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.buttonGenerate.Location = new System.Drawing.Point(245, 206);
            this.buttonGenerate.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.buttonGenerate.Name = "buttonGenerate";
            this.buttonGenerate.Size = new System.Drawing.Size(100, 28);
            this.buttonGenerate.TabIndex = 15;
            this.buttonGenerate.Text = "Generate!";
            this.buttonGenerate.UseVisualStyleBackColor = true;
            this.buttonGenerate.Click += new System.EventHandler(this.buttonGenerate_Click);
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(597, 293);
            this.Controls.Add(this.buttonGenerate);
            this.Controls.Add(this.textBoxCount);
            this.Controls.Add(this.label7);
            this.Controls.Add(this.buttonSamplesBrowse);
            this.Controls.Add(this.textBoxSamples);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.buttonDtdBrowse);
            this.Controls.Add(this.textBoxRootElement);
            this.Controls.Add(this.textBoxRootNamespace);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.textBoxDtd);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.pictureBox1);
            this.Margin = new System.Windows.Forms.Padding(4, 4, 4, 4);
            this.Name = "FormMain";
            this.Text = "Peach XML Generator";
            ((System.ComponentModel.ISupportInitialize)(this.pictureBox1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.PictureBox pictureBox1;
		private System.Windows.Forms.Label label1;
		private System.Windows.Forms.TextBox textBoxDtd;
		private System.Windows.Forms.Label label2;
		private System.Windows.Forms.Label label3;
		private System.Windows.Forms.Label label4;
		private System.Windows.Forms.Label label5;
		private System.Windows.Forms.TextBox textBoxRootNamespace;
		private System.Windows.Forms.TextBox textBoxRootElement;
		private System.Windows.Forms.Button buttonDtdBrowse;
		private System.Windows.Forms.Label label6;
		private System.Windows.Forms.Button buttonSamplesBrowse;
		private System.Windows.Forms.TextBox textBoxSamples;
		private System.Windows.Forms.Label label7;
		private System.Windows.Forms.TextBox textBoxCount;
		private System.Windows.Forms.Button buttonGenerate;
    }
}