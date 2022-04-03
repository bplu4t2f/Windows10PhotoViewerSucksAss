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
	partial class StashOptionsForm : Form
	{
		public StashOptionsForm(List<StashInfo> infoList, string hint)
		{
			InitializeComponent();

			this.labelHint.Text = hint;

			this.flowLayoutPanel1.SuspendLayout();
			foreach (var item in infoList)
			{
				var checkBox = new CheckBox();
				checkBox.Checked = true;
				checkBox.AutoSize = true;
				checkBox.Text = item.Path;
				checkBox.Margin = new Padding(3, 3, 3, 3);
				this.flowLayoutPanel1.Controls.Add(checkBox);
				this.displayItems.Add(new StashInfoDisplayItem(item, checkBox));
			}
			this.flowLayoutPanel1.ResumeLayout();
		}

		private readonly List<StashInfoDisplayItem> displayItems = new List<StashInfoDisplayItem>();

		private sealed class StashInfoDisplayItem
		{
			public StashInfoDisplayItem(StashInfo stashInfo, CheckBox checkBox)
			{
				this.StashInfo = stashInfo ?? throw new ArgumentNullException(nameof(stashInfo));
				this.CheckBox = checkBox ?? throw new ArgumentNullException(nameof(checkBox));
			}

			public StashInfo StashInfo { get; }
			public CheckBox CheckBox { get; }
		}

		public List<StashInfo> SelectedItems { get; private set; }

		private void buttonOK_Click(object sender, EventArgs e)
		{
			var itemsToStash = new List<StashInfo>();
			foreach (var item in this.displayItems)
			{
				if (item.CheckBox.Checked)
				{
					itemsToStash.Add(item.StashInfo);
				}
			}
			this.SelectedItems = itemsToStash;

			this.DialogResult = DialogResult.OK;
			this.Close();
		}

		private void buttonCancel_Click(object sender, EventArgs e)
		{
			this.DialogResult = DialogResult.Cancel;
			this.Close();
		}

		private void buttonCheckAll_Click(object sender, EventArgs e)
		{
			foreach (var item in this.displayItems)
			{
				item.CheckBox.Checked = true;
			}
		}

		private void buttonUncheckAll_Click(object sender, EventArgs e)
		{
			foreach (var item in this.displayItems)
			{
				item.CheckBox.Checked = false;
			}
		}
	}
}
