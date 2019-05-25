using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Windows10PhotoViewerSucksAss
{
	/// <summary>
	/// Optional helper class to simplify allocation.
	/// </summary>
	class SettingsUIHelper<TSettings>
	{
		public List<ISetting<TSettings>> Settings { get; } = new List<ISetting<TSettings>>();

		public void CheckBox(CheckBox checkBox, Func<TSettings, bool> getter, Action<TSettings, bool> setter)
		{
			this.Settings.Add(new GenericSetting<TSettings, bool>(new CheckBoxSetting(checkBox), getter, setter));
		}

		public void Color(Button button, Func<TSettings, Color> getter, Action<TSettings, Color> setter)
		{
			this.Settings.Add(new GenericSetting<TSettings, Color>(new ColorSetting(button), getter, setter));
		}

		public void Font(Button button, Label label, Func<TSettings, Font> getter, Action<TSettings, Font> setter)
		{
			this.Settings.Add(new GenericSetting<TSettings, Font>(new FontSetting(button, label), getter, setter));
		}
	}


	interface ISetting<TSettings>
	{
		void LoadFrom(TSettings source);
		bool TrySaveTo(TSettings target);
		event EventHandler SomethingChanged;

		bool HasChanged { get; }
		/// <summary>
		/// Called on cancel -- resets this to its last loaded value.
		/// </summary>
		void Revert();
	}


	class GenericSetting<TSettings, TValue> : ISetting<TSettings>
	{
		public GenericSetting(ISettingHandler<TValue> handler, Func<TSettings, TValue> getter, Action<TSettings, TValue> setter)
		{
			this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
			this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
			this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
			this.handler.SomethingChanged += this.HandleSomethingChanged;
		}
		private readonly ISettingHandler<TValue> handler;
		private readonly Func<TSettings, TValue> getter;
		private readonly Action<TSettings, TValue> setter;

		public bool HasChanged { get; private set; }
		
		public event EventHandler SomethingChanged
		{
			add { this.handler.SomethingChanged += value; }
			remove { this.handler.SomethingChanged -= value; }
		}

		private TValue lastLoadedValue;

		public void LoadFrom(TSettings source)
		{
			this.lastLoadedValue = this.getter(source);
			this.Revert();
		}

		public bool TrySaveTo(TSettings destination)
		{
			if (!this.handler.TryGet(out TValue value))
			{
				return false;
			}
			this.setter(destination, value);
			return true;
		}

		public void Revert()
		{
			this.handler.Load(this.lastLoadedValue);
			this.HasChanged = false;
		}

		private void HandleSomethingChanged(object sender, EventArgs e)
		{
			this.HasChanged = true;
		}
	}


	interface ISettingHandler<TValue>
	{
		void Load(TValue value);
		bool TryGet(out TValue value);
		event EventHandler SomethingChanged;
	}


	//    _   _                 _ _               
	//   | | | | __ _ _ __   __| | | ___ _ __ ___ 
	//   | |_| |/ _` | '_ \ / _` | |/ _ \ '__/ __|
	//   |  _  | (_| | | | | (_| | |  __/ |  \__ \
	//   |_| |_|\__,_|_| |_|\__,_|_|\___|_|  |___/
	//                                            


	class CheckBoxSetting : ISettingHandler<bool>
	{
		public CheckBoxSetting(CheckBox checkBox)
		{
			this.checkBox = checkBox ?? throw new ArgumentNullException(nameof(checkBox));
			checkBox.CheckedChanged += (sender, e) => this.SomethingChanged?.Invoke(this, e);
		}

		private readonly CheckBox checkBox;

		public event EventHandler SomethingChanged;

		public void Load(bool value)
		{
			this.checkBox.Checked = value;
		}

		public bool TryGet(out bool value)
		{
			value = this.checkBox.Checked;
			return true;
		}
	}


	class ColorSetting : ISettingHandler<Color>
	{
		public ColorSetting(Button colorPickerButton)
		{
			this.colorPickerButton = colorPickerButton ?? throw new ArgumentNullException(nameof(colorPickerButton));
			this.colorPickerButton.Click += this.HandleButtonClick;
		}

		private readonly Button colorPickerButton;

		public event EventHandler SomethingChanged;

		private Color currentColor;

		public void Load(Color value)
		{
			this.currentColor = value;
			this.UpdateCurrentColor();
		}

		public bool TryGet(out Color value)
		{
			value = this.currentColor;
			return true;
		}

		private void UpdateCurrentColor()
		{
			var c = this.currentColor;
			this.colorPickerButton.BackColor = c;
			bool isColorPrettyBright = c.R * 0.4 + c.G + c.B * 0.2 > 190;
			this.colorPickerButton.ForeColor = isColorPrettyBright ? Color.Black : Color.White;
		}

		private void HandleButtonClick(object sender, EventArgs e)
		{
			using (var dialog = new ColorDialog())
			{
				dialog.Color = this.currentColor;
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					this.currentColor = dialog.Color;
					this.UpdateCurrentColor();
					this.SomethingChanged?.Invoke(this, e);
				}
			}
		}
	}


	class FontSetting : ISettingHandler<Font>
	{
		public FontSetting(Button changeButton, Label label)
		{
			this.changeButton = changeButton ?? throw new ArgumentNullException(nameof(changeButton));
			this.label = label ?? throw new ArgumentNullException(nameof(label));
			this.changeButton.Click += this.HandleButtonClick;
		}

		private readonly Button changeButton;
		private readonly Label label;

		public event EventHandler SomethingChanged;

		private Font currentFont;

		public void Load(Font value)
		{
			this.currentFont = value;
			this.UpdateCurrentFont();
		}

		public bool TryGet(out Font value)
		{
			value = this.currentFont;
			return true;
		}

		private void UpdateCurrentFont()
		{
			var f = this.currentFont;
			string text = f == null ? String.Empty : String.Format(CultureInfo.InvariantCulture, "{0}, {1}, {2} pt", f.FontFamily.Name, f.Style, f.Size);
			this.label.Text = text;
		}

		private void HandleButtonClick(object sender, EventArgs e)
		{
			using (var dialog = new FontDialog())
			{
				dialog.Font = this.currentFont;
				if (dialog.ShowDialog() == DialogResult.OK)
				{
					this.currentFont = dialog.Font;
					this.UpdateCurrentFont();
					this.SomethingChanged?.Invoke(this, e);
				}
			}
		}
	}
}
