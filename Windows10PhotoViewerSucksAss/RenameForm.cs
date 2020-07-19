using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	public partial class RenameForm : Form
	{
		public RenameForm()
		{
			this.InitializeComponent();
		}

		protected override void OnLoad(EventArgs e)
		{
			base.OnLoad(e);
			this.textBoxTo.Select();
		}

		protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
		{
			if (keyData == Keys.Escape)
			{
				this.Cancel();
				return true;
			}
			else if (keyData == Keys.Enter)
			{
				this.Confirm();
				return true;
			}
			return base.ProcessCmdKey(ref msg, keyData);
		}

		public void Initialize(string directory, string from)
		{
			this.textBoxDirectory.Text = directory;
			this.textBoxFrom.Text = from;
			this.textBoxTo.Text = from;
		}

		/// <summary>
		/// Null if canceled.
		/// </summary>
		public string Choice { get; private set; }

		private void Confirm()
		{
			var to = this.textBoxTo.Text;
			if (string.Equals(this.textBoxFrom.Text, to, StringComparison.Ordinal))
			{
				// No change. Act as if canceled.
				this.Cancel();
				return;
			}
			this.Choice = to;
			this.Close();
		}

		private void Cancel()
		{
			this.Choice = null;
			this.Close();
		}

		private void ButtonOK_Click(object sender, EventArgs e)
		{
			this.Confirm();
		}

		private void ButtonCancel_Click(object sender, EventArgs e)
		{
			this.Cancel();
		}
	}
}
