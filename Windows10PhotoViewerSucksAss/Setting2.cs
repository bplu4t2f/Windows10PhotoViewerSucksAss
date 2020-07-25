using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	/// <summary>
	/// Implements <see cref="IGetSet{T}"/> with <see cref="ApplicableGettableSetting2{TSettings, T}"/> by binding it to a specific settings object.
	/// <para>Useful to interoperate with <see cref="GenericSetting{TSettings, TValue}"/>.</para>
	/// </summary>
	sealed class BoundSettingDifferentEffective<TSettings, T> : IGetSet<T>
	{
		public BoundSettingDifferentEffective(TSettings settings, ApplicableGettableSetting2<TSettings, T> setting)
		{
			this.Settings = settings;
			this.Setting = setting ?? throw new ArgumentNullException(nameof(setting));
		}

		public TSettings Settings { get; }
		public ApplicableGettableSetting2<TSettings, T> Setting { get; }

		public T GetEffective() => this.Setting.GetEffective();
		public void Set(T value) => this.Setting.Set(this.Settings, value);
		public void ApplyStored() => this.Setting.ApplyStored(this.Settings);
		public event EventHandler ValueSet
		{
			add { this.Setting.ValueSet += value; }
			remove { this.Setting.ValueSet -= value; }
		}
	}

	/// <summary>
	/// Implements <see cref="IGetSet{T}"/> with <see cref="ApplicableSetting2{TSettings, T}"/> by binding it to a specific settings object.
	/// <para>Useful to interoperate with <see cref="GenericSetting{TSettings, TValue}"/>.</para>
	/// </summary>
	sealed class BoundSetting<TSettings, T> : IGetSet<T>
	{
		public BoundSetting(TSettings settings, ApplicableSetting2<TSettings, T> setting)
		{
			this.Settings = settings;
			this.Setting = setting ?? throw new ArgumentNullException(nameof(setting));
		}

		public TSettings Settings { get; }
		public ApplicableSetting2<TSettings, T> Setting { get; }

		public T GetEffective() => this.Setting.GetStored(this.Settings);
		public void Set(T value) => this.Setting.Set(this.Settings, value);
		public void ApplyStored() => this.Setting.ApplyStored(this.Settings);
		public event EventHandler ValueSet
		{
			add { this.Setting.ValueSet += value; }
			remove { this.Setting.ValueSet -= value; }
		}
	}

	static class BoundSetting
	{
		public static BoundSetting<TSettings, T> Bind<TSettings, T>(this ApplicableSetting2<TSettings, T> setting, TSettings settings)
		{
			return new BoundSetting<TSettings, T>(settings, setting);
		}
		public static BoundSettingDifferentEffective<TSettings, T> Bind<TSettings, T>(this ApplicableGettableSetting2<TSettings, T> setting, TSettings settings)
		{
			return new BoundSettingDifferentEffective<TSettings, T>(settings, setting);
		}
	}

	// ====================================================================================================================================================================
	// ====================================================================================================================================================================
	//
	// IApplicableSetting2<TSettings>
	// Used to encapsulate an individual setting that can be changed at runtime and is stored in the user settings file.
	//

	interface IApplicableSetting2<TSettings>
	{
		/// <summary>
		/// Used on program startup to apply values from the stored object.
		/// </summary>
		void ApplyStored(TSettings from);
		/// <summary>
		/// Used to notify the application that a setting was changed in the settings dialog. The application uses this event to save the settings to disk.
		/// </summary>
		event EventHandler ValueSet;
	}

	/// <summary>
	/// Augments <see cref="ApplicableSetting2{TSettings, T}"/> with a different way to provide the current effective value of a setting.
	/// This is relevant when the application has its own idea of what the current effective value of a setting is, that may be different from what <see cref="ApplicableSetting2{TSettings, T}.GetStored"/> returns.
	/// <para>Example (where this appropriate): Window size and other application object properties.</para>
	/// <para>Counter-example (where this not appropriate): User-defined flags that are not stored anywhere except in the <typeparamref name="TSettings"/> object.</para>
	/// </summary>
	sealed class ApplicableGettableSetting2<TSettings, T> : IApplicableSetting2<TSettings>
	{
		public ApplicableGettableSetting2(ApplicableSetting2<TSettings, T> applicable, Func<T> getEffective)
		{
			this.applicable = applicable ?? throw new ArgumentNullException(nameof(applicable));
			this.getEffective = getEffective ?? throw new ArgumentNullException(nameof(getEffective));
		}

		private readonly ApplicableSetting2<TSettings, T> applicable;
		private readonly Func<T> getEffective;

		public event EventHandler ValueSet
		{
			add { this.applicable.ValueSet += value; }
			remove { this.applicable.ValueSet -= value; }
		}

		public void Set(TSettings to, T value) => this.applicable.Set(to, value);
		public T GetEffective() => this.getEffective();
		public void ApplyStored(TSettings from) => this.applicable.ApplyStored(from);
	}

	/// <summary>
	/// Augments <see cref="ISetting2{TSettings, T}"/> by applying the value.
	/// Applying is application-dependent and means applying a setting so that it takes effect immediately.
	/// Settings are applied when <see cref="ApplyStored"/> is called (which happens on program startup normally, with the value from initially loaded settings object),
	/// or when a setting is changed via <see cref="Set"/> (with the value that was set).
	/// </summary>
	sealed class ApplicableSetting2<TSettings, T> : IApplicableSetting2<TSettings>
	{
		public ApplicableSetting2(ISetting2<TSettings, T> setting, Action<T> apply)
		{
			this.setting = setting ?? throw new ArgumentNullException(nameof(setting));
			this.apply = apply;
		}

		private readonly ISetting2<TSettings, T> setting;
		private readonly Action<T> apply;

		public event EventHandler ValueSet;
		public void Set(TSettings to, T value)
		{
			this.setting.Set(to, value);
			this.ValueSet?.Invoke(this, EventArgs.Empty);
		}
		public void ApplyStored(TSettings from)
		{
			var value = this.setting.Get(from);
			this.apply?.Invoke(value);
		}
		public T GetStored(TSettings from) => this.setting.Get(from);
	}

	static class ApplicableSetting2
	{
		public static ApplicableSetting2<TSettings, T> Applicable<TSettings, T>(this ISetting2<TSettings, T> setting, Action<T> apply)
		{
			return new ApplicableSetting2<TSettings, T>(setting, apply);
		}

		public static ApplicableGettableSetting2<TSettings, T> Gettable<TSettings, T>(this ApplicableSetting2<TSettings, T> applicable, Func<T> getEffective)
		{
			return new ApplicableGettableSetting2<TSettings, T>(applicable, getEffective);
		}
	}

	// ====================================================================================================================================================================
	// ====================================================================================================================================================================
	//
	// Converting implementations of ISetting<TSettings, T>
	// Need these because some settings are stored differently in the xml file than they are used in the application.
	//

	sealed class EnumSetting2<TSettings, T> : ISetting2<TSettings, T>
		where T : struct, Enum
	{
		public Action<TSettings, string> PutSer;
		public Func<TSettings, string> GetSer;

		public EnumSetting2(
			Action<TSettings, string> putSer,
			Func<TSettings, string> getSer)
		{
			this.PutSer = putSer ?? throw new ArgumentNullException(nameof(putSer));
			this.GetSer = getSer ?? throw new ArgumentNullException(nameof(getSer));
		}

		public void Set(TSettings to, T value)
		{
			string ser = value.ToString();
			this.PutSer(to, ser);
		}

		public T Get(TSettings from)
		{
			var ser = this.GetSer(from);
			Enum.TryParse<T>(ser, out T value);
			return value;
		}
	}

	sealed class EncodingSetting2<TSettings, TSer, TApp> : ISetting2<TSettings, TApp>
	{
		public Func<TSer, TApp> Decode;
		public Func<TApp, TSer> Encode;
		public Action<TSettings, TSer> PutSer;
		public Func<TSettings, TSer> GetSer;

		public EncodingSetting2(
			Func<TSer, TApp> decode,
			Func<TApp, TSer> encode,
			Action<TSettings, TSer> putSer,
			Func<TSettings, TSer> getSer)
		{
			this.Decode = decode ?? throw new ArgumentNullException(nameof(decode));
			this.Encode = encode ?? throw new ArgumentNullException(nameof(encode));
			this.PutSer = putSer ?? throw new ArgumentNullException(nameof(putSer));
			this.GetSer = getSer ?? throw new ArgumentNullException(nameof(getSer));
		}

		public void Set(TSettings to, TApp value)
		{
			var ser = this.Encode(value);
			this.PutSer(to, ser);
		}

		public TApp Get(TSettings from)
		{
			var ser = this.GetSer(from);
			var app = this.Decode(ser);
			return app;
		}
	}
}
