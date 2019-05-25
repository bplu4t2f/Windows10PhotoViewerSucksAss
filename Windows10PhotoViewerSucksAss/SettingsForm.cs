using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	public partial class SettingsForm : Form
	{
		public SettingsForm(Form1 owner)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.InitializeComponent();

			//this.settingHelper.CheckBox(this.checkBoxSortCaseSensitive, x => x.Setting_SortCaseSensitive, (x, v) => x.Setting_SortCaseSensitive = v);
			this.settingHelper.Color(this.button1, x => x.Setting_BackColor, (x, v) => x.Setting_BackColor = v);

			foreach (var setting in this.settingHelper.Settings)
			{
				setting.LoadFrom(owner);
				setting.SomethingChanged += (sender, e) => setting.TrySaveTo(owner);
			}
		}

		private readonly Form1 owner;
		private readonly SettingsUIHelper<Form1> settingHelper = new SettingsUIHelper<Form1>();

		private void ButtonOK_Click(object sender, EventArgs e)
		{
			this.Close();
		}

		private void ButtonCancel_Click(object sender, EventArgs e)
		{
			foreach (var setting in this.settingHelper.Settings)
			{
				if (setting.HasChanged)
				{
					setting.Revert();
					setting.TrySaveTo(this.owner);
				}
			}
			this.Close();
		}
	}
}
