using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;

namespace PeachXmlGenerator
{
	public partial class Generate : Form
    {
        public ProgressBar progressBarGenerate;
        FormMain form;
        public Generate()
		{
            form = new FormMain();
			form.InitializeComponent();
		}

        private void InitializeComponent()
        {
            this.progressBarGenerate = new System.Windows.Forms.ProgressBar();
            this.SuspendLayout();
            // 
            // progressBar1
            // 
            this.progressBarGenerate.Location = new System.Drawing.Point(91, 115);
            this.progressBarGenerate.Name = "progressBar1";
            this.progressBarGenerate.Size = new System.Drawing.Size(100, 23);
            this.progressBarGenerate.TabIndex = 1;
            // 
            // Generate
            // 
            this.ClientSize = new System.Drawing.Size(282, 253);
            this.Controls.Add(this.progressBarGenerate);
            this.Name = "Generate";
            this.ResumeLayout(false);

        }
    }
}
