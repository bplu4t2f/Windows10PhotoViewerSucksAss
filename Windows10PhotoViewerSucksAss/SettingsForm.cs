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

			this.settingHelper.CheckBox(this.checkBoxSortCaseSensitive, x => x.Setting_SortCaseSensitive, (x, v) => x.Setting_SortCaseSensitive = v);
			this.settingHelper.Color(this.buttonBackgroundColor, x => x.Setting_BackColor, (x, v) => x.Setting_BackColor = v);
			this.settingHelper.Font(this.buttonChangeFont, this.labelCurrentFont, x => x.Setting_Font, (x, v) => x.Setting_Font = v);
			this.settingHelper.IntSlider(this.trackBarSplitterWidth, this.labelSplitterWidth, x => x.Setting_SplitterWidth, (x, v) => x.Setting_SplitterWidth = v);
			var comboBox_mouseWheelMode = this.settingHelper.ComboBox(this.comboBoxMouseWheelMode, x => x.Setting_MouseWheelMode, (x, v) => x.Setting_MouseWheelMode = v);
			this.settingHelper.CheckBox(this.checkBoxUseCurrentImageAsWindowIcon, x => x.Setting_UseCurrentImageAsWindowIcon, (x, v) => x.Setting_UseCurrentImageAsWindowIcon = v);

			comboBox_mouseWheelMode.AddValue(MouseWheelMode.NextPrevious, "Next/Previous File");
			comboBox_mouseWheelMode.AddValue(MouseWheelMode.ZoomAndScroll, "Zoom Image");

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
