namespace Windows10PhotoViewerSucksAss
{
	partial class SettingsForm
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
			this.buttonBackgroundColor = new System.Windows.Forms.Button();
			this.checkBoxSortCaseSensitive = new System.Windows.Forms.CheckBox();
			this.buttonOK = new System.Windows.Forms.Button();
			this.buttonCancel = new System.Windows.Forms.Button();
			this.buttonChangeFont = new System.Windows.Forms.Button();
			this.labelCurrentFont = new System.Windows.Forms.Label();
			this.trackBarSplitterWidth = new System.Windows.Forms.TrackBar();
			this.labelSplitterWidth = new System.Windows.Forms.Label();
			this.labelSplitterWidthHint = new System.Windows.Forms.Label();
			((System.ComponentModel.ISupportInitialize)(this.trackBarSplitterWidth)).BeginInit();
			this.SuspendLayout();
			// 
			// buttonBackgroundColor
			// 
			this.buttonBackgroundColor.Location = new System.Drawing.Point(12, 35);
			this.buttonBackgroundColor.Name = "buttonBackgroundColor";
			this.buttonBackgroundColor.Size = new System.Drawing.Size(147, 23);
			this.buttonBackgroundColor.TabIndex = 0;
			this.buttonBackgroundColor.Text = "Background Color...";
			this.buttonBackgroundColor.UseVisualStyleBackColor = true;
			// 
			// checkBoxSortCaseSensitive
			// 
			this.checkBoxSortCaseSensitive.AutoSize = true;
			this.checkBoxSortCaseSensitive.Location = new System.Drawing.Point(12, 12);
			this.checkBoxSortCaseSensitive.Name = "checkBoxSortCaseSensitive";
			this.checkBoxSortCaseSensitive.Size = new System.Drawing.Size(128, 17);
			this.checkBoxSortCaseSensitive.TabIndex = 1;
			this.checkBoxSortCaseSensitive.Text = "Case sensitive sorting";
			this.checkBoxSortCaseSensitive.UseVisualStyleBackColor = true;
			// 
			// buttonOK
			// 
			this.buttonOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonOK.Location = new System.Drawing.Point(381, 283);
			this.buttonOK.Name = "buttonOK";
			this.buttonOK.Size = new System.Drawing.Size(75, 23);
			this.buttonOK.TabIndex = 2;
			this.buttonOK.Text = "OK";
			this.buttonOK.UseVisualStyleBackColor = true;
			this.buttonOK.Click += new System.EventHandler(this.ButtonOK_Click);
			// 
			// buttonCancel
			// 
			this.buttonCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
			this.buttonCancel.Location = new System.Drawing.Point(462, 283);
			this.buttonCancel.Name = "buttonCancel";
			this.buttonCancel.Size = new System.Drawing.Size(75, 23);
			this.buttonCancel.TabIndex = 3;
			this.buttonCancel.Text = "Cancel";
			this.buttonCancel.UseVisualStyleBackColor = true;
			this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
			// 
			// buttonChangeFont
			// 
			this.buttonChangeFont.Location = new System.Drawing.Point(12, 64);
			this.buttonChangeFont.Name = "buttonChangeFont";
			this.buttonChangeFont.Size = new System.Drawing.Size(147, 23);
			this.buttonChangeFont.TabIndex = 4;
			this.buttonChangeFont.Text = "Font...";
			this.buttonChangeFont.UseVisualStyleBackColor = true;
			// 
			// labelCurrentFont
			// 
			this.labelCurrentFont.AutoSize = true;
			this.labelCurrentFont.Location = new System.Drawing.Point(165, 69);
			this.labelCurrentFont.Name = "labelCurrentFont";
			this.labelCurrentFont.Size = new System.Drawing.Size(100, 13);
			this.labelCurrentFont.TabIndex = 5;
			this.labelCurrentFont.Text = "{CURRENT FONT}";
			// 
			// trackBarSplitterWidth
			// 
			this.trackBarSplitterWidth.Location = new System.Drawing.Point(15, 110);
			this.trackBarSplitterWidth.Maximum = 60;
			this.trackBarSplitterWidth.Name = "trackBarSplitterWidth";
			this.trackBarSplitterWidth.Size = new System.Drawing.Size(147, 45);
			this.trackBarSplitterWidth.TabIndex = 6;
			// 
			// labelSplitterWidth
			// 
			this.labelSplitterWidth.AutoSize = true;
			this.labelSplitterWidth.Location = new System.Drawing.Point(165, 119);
			this.labelSplitterWidth.Name = "labelSplitterWidth";
			this.labelSplitterWidth.Size = new System.Drawing.Size(107, 13);
			this.labelSplitterWidth.TabIndex = 7;
			this.labelSplitterWidth.Text = "{SPLITTER WIDTH}";
			// 
			// labelSplitterWidthHint
			// 
			this.labelSplitterWidthHint.AutoSize = true;
			this.labelSplitterWidthHint.Location = new System.Drawing.Point(12, 94);
			this.labelSplitterWidthHint.Name = "labelSplitterWidthHint";
			this.labelSplitterWidthHint.Size = new System.Drawing.Size(73, 13);
			this.labelSplitterWidthHint.TabIndex = 8;
			this.labelSplitterWidthHint.Text = "Splitter Width:";
			// 
			// SettingsForm
			// 
			this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
			this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
			this.ClientSize = new System.Drawing.Size(549, 318);
			this.Controls.Add(this.labelSplitterWidthHint);
			this.Controls.Add(this.labelSplitterWidth);
			this.Controls.Add(this.trackBarSplitterWidth);
			this.Controls.Add(this.labelCurrentFont);
			this.Controls.Add(this.buttonChangeFont);
			this.Controls.Add(this.buttonCancel);
			this.Controls.Add(this.buttonOK);
			this.Controls.Add(this.checkBoxSortCaseSensitive);
			this.Controls.Add(this.buttonBackgroundColor);
			this.Name = "SettingsForm";
			this.Text = "Options";
			((System.ComponentModel.ISupportInitialize)(this.trackBarSplitterWidth)).EndInit();
			this.ResumeLayout(false);
			this.PerformLayout();

		}

		#endregion

		private System.Windows.Forms.Button buttonBackgroundColor;
		private System.Windows.Forms.CheckBox checkBoxSortCaseSensitive;
		private System.Windows.Forms.Button buttonOK;
		private System.Windows.Forms.Button buttonCancel;
		private System.Windows.Forms.Button buttonChangeFont;
		private System.Windows.Forms.Label labelCurrentFont;
		private System.Windows.Forms.TrackBar trackBarSplitterWidth;
		private System.Windows.Forms.Label labelSplitterWidth;
		private System.Windows.Forms.Label labelSplitterWidthHint;
	}
}