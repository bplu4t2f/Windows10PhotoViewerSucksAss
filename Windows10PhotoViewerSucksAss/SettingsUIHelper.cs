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

		public THandler Add<THandler, TValue>(THandler handler, Func<TSettings, TValue> getter, Action<TSettings, TValue> setter)
			where THandler : ISettingHandler<TValue>
		{
			this.Settings.Add(new GenericSetting<TSettings, TValue>(handler, new GetSet<TSettings, TValue>(handler.DebugName, getter, setter)));
			return handler;
		}

		public THandler Add<THandler, TValue>(THandler handler, IGetSet<TSettings, TValue> setting)
			where THandler : ISettingHandler<TValue>
		{
			this.Settings.Add(new GenericSetting<TSettings, TValue>(handler, setting));
			return handler;
		}
	}


	/// <summary>
	/// This interface can be used to iterate over multiple <see cref="GenericSetting{TSettings, TValue}"/> instaces with the same settings container type.
	/// </summary>
	interface ISetting<TSettings>
	{
		string DebugName { get; }

		void LoadFrom(TSettings source);
		bool TrySaveTo(TSettings target);
		event EventHandler SomethingChanged;

		bool HasChanged { get; }
		/// <summary>
		/// Called on cancel -- resets this to its last loaded value.
		/// </summary>
		void Revert();
	}


	/// <summary>
	/// The settings GUI uses this to read current setting values and write changed setting values.
	/// </summary>
	interface IGetSet<TSettings, T>
	{
		string DebugName { get; }
		T Get(TSettings from);
		void Set(TSettings to, T value);
	}


	/// <summary>
	/// Implementation of <see cref="IGetSet{TSettings, T}"/> with 2 delegates.
	/// </summary>
	sealed class GetSet<TSettings, TValue> : IGetSet<TSettings, TValue>
	{
		public GetSet(string debugName, Func<TSettings, TValue> getter, Action<TSettings, TValue> setter)
		{
			this.DebugName = debugName;
			this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
			this.setter = setter ?? throw new ArgumentNullException(nameof(setter));
		}

		public string DebugName { get; }
		private readonly Func<TSettings, TValue> getter;
		private readonly Action<TSettings, TValue> setter;

		public TValue Get(TSettings from) => this.getter(from);
		public void Set(TSettings to, TValue value) => this.setter(to, value);
	}

	/// <summary>
	/// Augments <see cref="IGetSet{TSettings, T}"/> with a different way to provide the current effective value of a setting.
	/// This is relevant when the application has its own idea of what the current effective value of a setting is, that may be different from what <see cref="IGetSet{TSettings, T}.Get"/> returns.
	/// <para>Example (where this appropriate): Window size and other application object properties.</para>
	/// <para>Counter-example (where this not appropriate): User-defined flags that are not stored anywhere except in the <typeparamref name="TSettings"/> object.</para>
	/// </summary>
	sealed class GetSet_DifferentGetter<TSettings, TValue> : IGetSet<TSettings, TValue>
	{
		public GetSet_DifferentGetter(IGetSet<TSettings, TValue> inner, Func<TSettings, TValue> getter)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			this.getter = getter ?? throw new ArgumentNullException(nameof(getter));
		}

		public string DebugName => this.inner.DebugName;
		private readonly IGetSet<TSettings, TValue> inner;
		private readonly Func<TSettings, TValue> getter;

		public TValue Get(TSettings from) => this.getter(from);
		public void Set(TSettings to, TValue value) => this.inner.Set(to, value);
	}

	static class GetSet_DifferentGetter
	{
		public static GetSet_DifferentGetter<TSettings, TValue> DifferentGetter<TSettings, TValue>(this IGetSet<TSettings, TValue> getset, Func<TSettings, TValue> newGetter)
		{
			return new GetSet_DifferentGetter<TSettings, TValue>(getset, newGetter);
		}
	}

	sealed class GetSetW<TSettingsNew, TSettingsOld, TValue> : IGetSet<TSettingsNew, TValue>
	{
		public GetSetW(Func<TSettingsNew, TSettingsOld> selector, IGetSet<TSettingsOld, TValue> inner)
		{
			this.selector = selector ?? throw new ArgumentNullException(nameof(selector));
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		public string DebugName => this.inner.DebugName;

		private readonly Func<TSettingsNew, TSettingsOld> selector;
		private readonly IGetSet<TSettingsOld, TValue> inner;

		public TValue Get(TSettingsNew from) => this.inner.Get(this.selector(from));
		public void Set(TSettingsNew to, TValue value) => this.inner.Set(this.selector(to), value);
	}

	/// <summary>
	/// This is a (probably insane) helper class for allocating <see cref="GetSetW{TSettingsNew, TSettingsOld, TValue}"/> instances with an extension method on <see cref="IGetSet{TSettings, T}"/>.
	/// </summary>
	static class GetSetW
	{
		public static LiftHelper<TSettingsOld, TValue> Lift<TSettingsOld, TValue>(this IGetSet<TSettingsOld, TValue> getset)
		{
			return new LiftHelper<TSettingsOld, TValue>(getset);
		}

		public struct LiftHelper<TSettingsOld, TValue>
		{
			public LiftHelper(IGetSet<TSettingsOld, TValue> getset)
			{
				this.getset = getset;
			}

			private readonly IGetSet<TSettingsOld, TValue> getset;

			public GetSetW<TSettingsNew, TSettingsOld, TValue> Wrap<TSettingsNew>(Func<TSettingsNew, TSettingsOld> selector)
			{
				if (this.getset == null) return null;
				return new GetSetW<TSettingsNew, TSettingsOld, TValue>(selector, this.getset);
			}
		}
	}

	class GenericSetting<TSettings, TValue> : ISetting<TSettings>
	{
		public GenericSetting(ISettingHandler<TValue> handler, IGetSet<TSettings, TValue> getset)
		{
			this.getset = getset ?? throw new ArgumentNullException(nameof(getset));
			this.handler = handler ?? throw new ArgumentNullException(nameof(handler));
			this.handler.SomethingChanged += this.HandleSomethingChanged;
		}

		public string DebugName => this.getset.DebugName;
		private readonly IGetSet<TSettings, TValue> getset;
		private readonly ISettingHandler<TValue> handler;

		public bool HasChanged { get; private set; }
		
		public event EventHandler SomethingChanged
		{
			add { this.handler.SomethingChanged += value; }
			remove { this.handler.SomethingChanged -= value; }
		}

		private TValue lastLoadedValue;

		public void LoadFrom(TSettings source)
		{
			this.lastLoadedValue = this.getset.Get(source);
			this.Revert();
		}

		public bool TrySaveTo(TSettings destination)
		{
			if (!this.handler.TryGet(out TValue value))
			{
				return false;
			}
			this.getset.Set(destination, value);
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
		string DebugName { get; }
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
		public CheckBoxSetting(string debugName, CheckBox checkBox)
		{
			this.DebugName = debugName;
			this.checkBox = checkBox ?? throw new ArgumentNullException(nameof(checkBox));
			checkBox.CheckedChanged += (sender, e) => this.SomethingChanged?.Invoke(this, e);
		}

		public string DebugName { get; }
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
		public ColorSetting(string debugName, Button colorPickerButton)
		{
			this.DebugName = debugName;
			this.colorPickerButton = colorPickerButton ?? throw new ArgumentNullException(nameof(colorPickerButton));
			this.colorPickerButton.Click += this.HandleButtonClick;
		}

		public string DebugName { get; }
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
		public FontSetting(string debugName, Button changeButton, Label label)
		{
			this.DebugName = debugName;
			this.changeButton = changeButton ?? throw new ArgumentNullException(nameof(changeButton));
			this.label = label ?? throw new ArgumentNullException(nameof(label));
			this.changeButton.Click += this.HandleButtonClick;
		}

		public string DebugName { get; }
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


	class IntSliderSetting : ISettingHandler<int>
	{
		public IntSliderSetting(string debugName, TrackBar trackBar, Label label)
		{
			this.DebugName = debugName;
			this.trackBar = trackBar ?? throw new ArgumentNullException(nameof(trackBar));
			this.label = label;
			if (label != null)
			{
				this.trackBar.ValueChanged += this.HandleTrackBarValueChanged;
			}
		}

		public string DebugName { get; }
		private readonly TrackBar trackBar;
		private readonly Label label;
		private bool updating = false;

		public event EventHandler SomethingChanged
		{
			add { this.trackBar.ValueChanged += value; }
			remove { this.trackBar.ValueChanged -= value; }
		}

		public void Load(int value)
		{
			this.updating = true;
			this.trackBar.Value = value;
			this.updating = false;
			this.UpdateText();
		}

		public bool TryGet(out int value)
		{
			value = this.trackBar.Value;
			return true;
		}

		private void UpdateText()
		{
			this.label.Text = this.trackBar.Value.ToString(CultureInfo.InvariantCulture);
		}

		private void HandleTrackBarValueChanged(object sender, EventArgs e)
		{
			if (!this.updating)
			{
				this.UpdateText();
			}
		}
	}


	class ComboBoxSetting<T> : ISettingHandler<T>
	{
		public ComboBoxSetting(string debugName, ComboBox comboBox)
		{
			this.DebugName = debugName;
			this.comboBox = comboBox ?? throw new ArgumentNullException(nameof(comboBox));
		}

		public string DebugName { get; }
		private readonly ComboBox comboBox;

		public void AddValue(T value, string displayText)
		{
			this.comboBox.Items.Add(new ComboBoxItem(value, displayText));
		}

		private sealed class ComboBoxItem
		{
			public ComboBoxItem(T value, string displayText)
			{
				this.value = value;
				this.displayText = displayText;
			}
			public readonly T value;
			public readonly string displayText;
			public override string ToString()
			{
				return this.displayText;
			}
		}

		public event EventHandler SomethingChanged
		{
			add { this.comboBox.SelectedIndexChanged += value; }
			remove { this.comboBox.SelectedIndexChanged -= value; }
		}

		public void Load(T value)
		{
			for (int i = 0; i < this.comboBox.Items.Count; ++i)
			{
				if (this.comboBox.Items[i] is ComboBoxItem item)
				{
					if (Object.Equals(item.value, value))
					{
						this.comboBox.SelectedIndex = i;
						return;
					}
				}
			}
			this.comboBox.SelectedIndex = -1;
		}

		public bool TryGet(out T value)
		{
			if (this.comboBox.SelectedItem is ComboBoxItem item)
			{
				value = item.value;
				return true;
			}
			else
			{
				value = default(T);
				return false;
			}
		}
	}
}
