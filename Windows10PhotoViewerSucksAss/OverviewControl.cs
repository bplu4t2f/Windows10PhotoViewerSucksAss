using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.IO;

namespace Windows10PhotoViewerSucksAss
{
	public partial class OverviewControl : UserControl
	{
		public OverviewControl()
		{
			InitializeComponent();
			this.selectionFont = new Font(this.Font, FontStyle.Bold);
			this.AutoScroll = true;
		}

		private readonly Font selectionFont;
		private Control selectedLabel;

		public void Initialize(IList<string> availableFiles)
		{
			this.Controls.Clear();
			if (availableFiles == null)
			{
				return;
			}

			int y = 0;
			foreach (var file in availableFiles)
			{
				var label = new Label();
				label.Text = Path.GetFileName(file);
				label.Top = y;
				y = label.Bottom;
				this.Controls.Add(label);
			}
		}

		public void SetDisplayIndex(int index)
		{
			if (this.selectedLabel != null)
			{
				this.selectedLabel.Font = this.Font;
				this.selectedLabel = null;
			}

			if (index >= 0 && index < this.Controls.Count)
			{
				this.selectedLabel = this.Controls[index];
				this.selectedLabel.Font = this.selectionFont;
				this.ScrollControlIntoView(this.selectedLabel);
			}
		}
	}
}
