using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	partial class SettingsForm : Form
	{
		public SettingsForm(Form1 owner)
		{
			this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
			this.InitializeComponent();

			this.Icon = Properties.Resources.generic_picture;

			this.settingHelper.Add(new CheckBoxSetting("SortCaseSensitive", this.checkBoxSortCaseSensitive), x => x.Setting_SortCaseSensitive);
			this.settingHelper.Add(new ColorSetting("BackgroundColor", this.buttonBackgroundColor), x => x.Setting_BackColor);
			this.settingHelper.Add(new ColorSetting("FileListBackColor", this.buttonFileListBackColor), x => x.Setting_FileListBackColor);
			this.settingHelper.Add(new ColorSetting("FileListForeColor", this.buttonFileListForeColor), x => x.Setting_FileListForeColor);
			this.settingHelper.Add(new ColorSetting("FileListForeColorError", this.buttonFileListForeColorError), x => x.Setting_FileListForeColorError);
			this.settingHelper.Add(new FontSetting("Font", this.buttonChangeFont, this.labelCurrentFont), x => x.Setting_ApplicationFont);
			this.settingHelper.Add(new IntSliderSetting("SplitterWidth", this.trackBarSplitterWidth, this.labelSplitterWidth), x => x.Setting_SplitterWidth);
			var comboBox_mouseWheelMode = 
				this.settingHelper.Add(new ComboBoxSetting<MouseWheelMode>("MouseWheelMode", this.comboBoxMouseWheelMode), x => x.Setting_MouseWheelMode);
			this.settingHelper.Add(new CheckBoxSetting("UseCurrentImageAsWindowIcon", this.checkBoxUseCurrentImageAsWindowIcon), x => x.Setting_UseCurrentImageAsWindowIcon);

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

		private static void DisplayReport(Form Owner, string Message)
		{
			using (var Form = new Form())
			{
				Form.Icon = Properties.Resources.generic_picture;
				Form.StartPosition = FormStartPosition.Manual;
				Form.Size = new Size(900, 650);
				Form.Text = "File Association Report";
				Util.CenterControl(Owner, Form);
				var TextBox = new TextBox();
				TextBox.Text = Message;
				TextBox.Dock = DockStyle.Fill;
				TextBox.Multiline = true;
				TextBox.Font = new Font(FontFamily.GenericMonospace, 9.75f);
				TextBox.ReadOnly = true;
				TextBox.WordWrap = false;
				TextBox.ScrollBars = ScrollBars.Both;
				TextBox.SelectionLength = 0;
				TextBox.TabStop = false; // https://stackoverflow.com/a/3421501/653473
				TextBox.BorderStyle = BorderStyle.None;
				Form.Controls.Add(TextBox);
				Form.ShowDialog();
			}
		}

		/// <summary>
		/// May return null.
		/// </summary>
		private string ApplicationPath => this.owner.StartupInfo.ApplicationPathForFileAssociationCommand;

		/// <summary>
		/// May return null.
		/// </summary>
		private string FriendlyAppName => this.owner.StartupInfo.FriendlyAppName;

		private void ButtonCheckFileAssociations_Click(object sender, EventArgs e)
		{
			try
			{
				using (var Writer = new StringWriter())
				{
					var Issues = new IssueTracker(Writer);
					FileAssociationBullshit.CheckFileAssociations(Issues, this.ApplicationPath, this.FriendlyAppName, Install: false);
					DisplayReport(this, $"File Association Check Report ({Issues.NumIssues} issue(s) detected):\r\n\r\n{Issues.ToString()}");
				}
			}
			catch (Exception ex)
			{
				MessageBox.Show($"Error checking File Associations.\r\n{ex.Message}");
			}
		}

		private void ButtonInstallFileAssociations_Click(object sender, EventArgs e)
		{
			var SupportedExtensionNames = String.Join(", ", FileAssociationBullshit.FileAssocationExtensionInfo.Select(x => x.Extension));

			var Path = this.ApplicationPath;
			if (Path == null)
			{
				MessageBox.Show($"Cannot install Application Progid because the applicatin path is not known, and therefore no open verb command can be deduced.");
				return;
			}
			var Choice = MessageBox.Show($"You are about to install application path \"{Path}\" as an available Progid for image files in the registry for the current Windows user (HKEY_CURRENT_USER).\r\n\r\nThis will create a Progid key named \"{FileAssociationBullshit.ThisApplicationProgid}\" (which is the unique identifier for this application) under \"HKCU\\Software\\Classes\", and adds that Progid to the image extension keys for {SupportedExtensionNames}.\r\n\r\nDue to changes in file association handling in Windows 10, it is not possible (at least not officially) to install this program without further user interaction though.\r\nIf all goes well, the next time a file with any of the supported extensions is opened, Windows will ask which program to use, and stores that information in the UserChoice key for the extension. Otherwise, the application will be permanently available in the Open With list for those extensions, and can be set as the default program in the Open With dialog.\r\n\r\nIf this application has been registered preivously, then the previous registration will be overwritten.\r\n\r\nIs this OK?", "File Association Installation", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
			if (Choice != DialogResult.Yes)
			{
				return;
			}
			using (var Writer = new StringWriter())
			{
				var Issues = new IssueTracker(Writer);
				try
				{
					FileAssociationBullshit.CheckFileAssociations(Issues, Path, this.FriendlyAppName, Install: true);
					DisplayReport(this, $"File Associations installed. Report ({Issues.NumIssues} issue(s) detected):\r\n\r\n{Issues.ToString()}");
				}
				catch (Exception ex)
				{
					MessageBox.Show($"Error installing File Associations.\r\n{ex.Message}");
					DisplayReport(this, $"Failed to install File Associations. Report ({Issues.NumIssues} issue(s) detected):\r\n\r\n{Issues.ToString()}");
				}
			}
		}
	}
}
