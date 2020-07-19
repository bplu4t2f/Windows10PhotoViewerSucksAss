namespace Windows10PhotoViewerSucksAss
{
	partial class RenameForm
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
		private void InitializeComponent()
		{
			this.labelDirectoryHint = new System.Windows.Forms.Label();
			this.textBoxDirectory = new System.Windows.Forms.TextBox();
			this.textBoxFrom = new System.Windows.Forms.TextBox();
			this.labelFromHint = new System.Windows.Forms.Label();
			this.labelNewHint = new System.Windows.Forms.Label();
			this.textBoxTo = new System.Windows.Forms.TextBox();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.buttonOK = new System.Windows.Forms.Button();
			this.SuspendLayout();
			// 
			// labelDirectoryHint
			// 
			this.labelDirectoryHint.AutoSize = true;
			this.labelDirectoryHint.Location = new System.Drawing.Point(12, 13);
			this.labelDirectoryHint.Name = "labelDirectoryHint";
			this.labelDirectoryHint.Size = new System.Drawing.Size(62, 13);
			this.labelDirectoryHint.TabIndex = 0;
			this.labelDirectoryHint.Text = "In directory:";
			// 
			// textBoxDirectory
			// 
			this.textBoxDirectory.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxDirectory.Location = new System.Drawing.Point(12, 29);
			this.textBoxDirectory.Name = "textBoxDirectory";
			this.textBoxDirectory.ReadOnly = true;
			this.textBoxDirectory.Size = new System.Drawing.Size(454, 20);
			this.textBoxDirectory.TabIndex = 1;
			// 
			// textBoxFrom
			// 
			this.textBoxFrom.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxFrom.Location = new System.Drawing.Point(12, 71);
			this.textBoxFrom.Name = "textBoxFrom";
			this.textBoxFrom.ReadOnly = true;
			this.textBoxFrom.Size = new System.Drawing.Size(454, 20);
			this.textBoxFrom.TabIndex = 3;
			// 
			// labelFromHint
			// 
			this.labelFromHint.AutoSize = true;
			this.labelFromHint.Location = new System.Drawing.Point(12, 55);
			this.labelFromHint.Name = "labelFromHint";
			this.labelFromHint.Size = new System.Drawing.Size(89, 13);
			this.labelFromHint.TabIndex = 2;
			this.labelFromHint.Text = "Rename file from:";
			// 
			// labelNewHint
			// 
			this.labelNewHint.AutoSize = true;
			this.labelNewHint.Location = new System.Drawing.Point(12, 97);
			this.labelNewHint.Name = "labelNewHint";
			this.labelNewHint.Size = new System.Drawing.Size(75, 13);
			this.labelNewHint.TabIndex = 4;
			this.labelNewHint.Text = "To new name:";
			// 
			// textBoxTo
			// 
			this.textBoxTo.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
			this.textBoxTo.Location = new System.Drawing.Point(12, 113);
			this.textBoxTo.Name = "textBoxTo";
			this.textBoxTo.Size = new System.Drawing.Size(454, 20);
			this.textBoxTo.TabIndex = 5;
			// 
			// buttonCancel
			// 
			this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonCancel.Location = new System.Drawing.Point(391, 204);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 23);
			this.buttonCancel.TabIndex = 7;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
			// 
			// buttonOK
			// 
			this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonOK.Location = new System.Drawing.Point(310, 204);
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.Size = new System.Drawing.Size(75, 23);
			this.buttonOK.TabIndex = 6;
			this.buttonOK.Text = "OK";
			this.buttonOK.UseVisualStyleBackColor = true;
			this.buttonOK.Click += new System.EventHandler(this.ButtonOK_Click);
			// 
			// RenameForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(478, 239);
			this.Controls.Add(this.buttonOK);
			this.Controls.Add(this.buttonCancel);
			this.Controls.Add(this.textBoxTo);
			this.Controls.Add(this.textBoxFrom);
			this.Controls.Add(this.textBoxDirectory);
			this.Controls.Add(this.labelNewHint);
			this.Controls.Add(this.labelFromHint);
			this.Controls.Add(this.labelDirectoryHint);
			this.Name = "RenameForm";
			this.Text = "Rename File";
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Label labelDirectoryHint;
		private System.Windows.Forms.TextBox textBoxDirectory;
		private System.Windows.Forms.TextBox textBoxFrom;
		private System.Windows.Forms.Label labelFromHint;
		private System.Windows.Forms.Label labelNewHint;
		private System.Windows.Forms.TextBox textBoxTo;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonOK;
	}
}