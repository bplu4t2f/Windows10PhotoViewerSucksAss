using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
	/// <summary>
	/// Implements <see cref="IGetSet{T}"/> with <see cref="ApplicableSetting_DifferentGetter2{TSettings, T}"/> by binding it to a specific settings object.
	/// <para>Useful to interoperate with <see cref="GenericSetting{TSettings, TValue}"/>.</para>
	/// </summary>
	sealed class BoundSetting_DifferentGetter<TSettings, T> : IGetSet<T>
	{
		public BoundSetting_DifferentGetter(TSettings settings, ApplicableSetting_DifferentGetter2<TSettings, T> setting)
		{
			this.Settings = settings;
			this.Setting = setting ?? throw new ArgumentNullException(nameof(setting));
		}

		public string DebugName => this.Setting.DebugName;
		public TSettings Settings { get; }
		public ApplicableSetting_DifferentGetter2<TSettings, T> Setting { get; }

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

		public string DebugName => this.Setting.DebugName;
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
		public static BoundSetting_DifferentGetter<TSettings, T> Bind<TSettings, T>(this ApplicableSetting_DifferentGetter2<TSettings, T> setting, TSettings settings)
		{
			return new BoundSetting_DifferentGetter<TSettings, T>(settings, setting);
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
		string DebugName { get; }
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
	sealed class ApplicableSetting_DifferentGetter2<TSettings, T> : IApplicableSetting2<TSettings>
	{
		public ApplicableSetting_DifferentGetter2(ApplicableSetting2<TSettings, T> applicable, Func<T> getEffective)
		{
			this.applicable = applicable ?? throw new ArgumentNullException(nameof(applicable));
			this.getEffective = getEffective ?? throw new ArgumentNullException(nameof(getEffective));
		}

		public string DebugName => this.applicable.DebugName;
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

		public string DebugName => this.setting.DebugName;
		private readonly ISetting2<TSettings, T> setting;
		private readonly Action<T> apply;

		public event EventHandler ValueSet;
		public void Set(TSettings to, T value)
		{
			this.setting.Set(to, value);
			this.ValueSet?.Invoke(this, EventArgs.Empty);
			this.apply?.Invoke(value);
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

		public static ApplicableSetting_DifferentGetter2<TSettings, T> DifferentGetter<TSettings, T>(this ApplicableSetting2<TSettings, T> applicable, Func<T> getEffective)
		{
			return new ApplicableSetting_DifferentGetter2<TSettings, T>(applicable, getEffective);
		}

		/// <summary>
		/// Doesn't actually add anything to the list. The list is just used for type inference.
		/// <para>Use this method instead of allocating <see cref="Setting2{TSettings, TValue}"/> instances directly.</para>
		/// </summary>
		public static Setting2<TSettings, TValue> Setting<TSettings, TValue>(this List<IApplicableSetting2<TSettings>> list, string debugName, Func<TSettings, TValue> getter, Action<TSettings, TValue> setter)
		{
			return new Setting2<TSettings, TValue>(debugName, getter, setter);
		}

		/// <summary>
		/// Doesn't actually add anything to the list. The list is just used for type inference.
		/// <para>Use this method instead of allocating <see cref="Setting2_GetSet{TSettings, TValue}"/> instances directly.</para>
		/// </summary>
		public static Setting2_GetSet<TSettings, TValue> Setting<TSettings, TValue>(this List<IApplicableSetting2<TSettings>> list, string debugName, Func<TSettings, IGetSet<TValue>> getBound)
		{
			return new Setting2_GetSet<TSettings, TValue>(debugName, getBound);
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
		public EnumSetting2(ISetting2<TSettings, string> inner)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		public string DebugName => this.inner.DebugName;
		private readonly ISetting2<TSettings, string> inner;

		public void Set(TSettings to, T value)
		{
			string ser = value.ToString();
			this.inner.Set(to, ser);
		}

		public T Get(TSettings from)
		{
			var ser = this.inner.Get(from);
			Enum.TryParse<T>(ser, out T value);
			return value;
		}
	}

	static class EnumSetting2
	{
		public static EnumSetting2<TSettings, T> Enum<TSettings, T>(this ISetting2<TSettings, string> inner)
			where T : struct, Enum
		{
			return new EnumSetting2<TSettings, T>(inner);
		}
	}

	sealed class EncodingSetting2<TSettings, TSer, TApp> : ISetting2<TSettings, TApp>
	{
		public EncodingSetting2(
			ISetting2<TSettings, TSer> inner,
			Func<TSer, TApp> decode,
			Func<TApp, TSer> encode
			)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
			this.encode = encode ?? throw new ArgumentNullException(nameof(encode));
		}

		public string DebugName => this.inner.DebugName;
		private readonly ISetting2<TSettings, TSer> inner;
		private readonly Func<TSer, TApp> decode;
		private readonly Func<TApp, TSer> encode;

		public void Set(TSettings to, TApp value)
		{
			var ser = this.encode(value);
			this.inner.Set(to, ser);
		}

		public TApp Get(TSettings from)
		{
			var ser = this.inner.Get(from);
			var app = this.decode(ser);
			return app;
		}
	}

	static class EncodingSetting2
	{
		public static EncodingSetting2<TSettings, TSer, TApp> Encoding<TSettings, TSer, TApp>(this ISetting2<TSettings, TSer> inner, Func<TSer, TApp> decode, Func<TApp, TSer> encode)
		{
			return new EncodingSetting2<TSettings, TSer, TApp>(inner, decode, encode);
		}
	}
}
