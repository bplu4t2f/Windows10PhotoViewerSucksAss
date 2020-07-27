using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
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
	/// Augments <see cref="IGetSet{TSettings, T}"/> by applying the value.
	/// Applying is application-dependent and means applying a setting so that it takes effect immediately.
	/// Settings are applied when <see cref="ApplyStored"/> is called (which happens on program startup normally, with the value from initially loaded settings object),
	/// or when a setting is changed via <see cref="Set"/> (with the value that was set).
	/// </summary>
	sealed class ApplicableSetting2<TSettings, T> : IApplicableSetting2<TSettings>, IGetSet<TSettings, T>
	{
		public ApplicableSetting2(IGetSet<TSettings, T> setting, Action<TSettings, T> apply)
		{
			this.setting = setting ?? throw new ArgumentNullException(nameof(setting));
			this.apply = apply;
		}

		public string DebugName => this.setting.DebugName;
		private readonly IGetSet<TSettings, T> setting;
		private readonly Action<TSettings, T> apply;

		public event EventHandler ValueSet;
		public void Set(TSettings to, T value)
		{
			this.setting.Set(to, value);
			this.ValueSet?.Invoke(this, EventArgs.Empty);
			this.apply?.Invoke(to, value);
		}
		public void ApplyStored(TSettings from)
		{
			var value = this.setting.Get(from);
			this.apply?.Invoke(from, value);
		}
		public T Get(TSettings from) => this.setting.Get(from);
	}

	static class ApplicableSetting2
	{
		public static ApplicableSetting2<TSettings, T> Applicable<TSettings, T>(this IGetSet<TSettings, T> setting, Action<TSettings, T> apply)
		{
			return new ApplicableSetting2<TSettings, T>(setting, apply);
		}

		/// <summary>
		/// Doesn't actually add anything to the list. The list is just used for type inference.
		/// <para>Use this method instead of allocating <see cref="GetSet{TSettings, TValue}"/> instances directly.</para>
		/// </summary>
		public static GetSet<TSettings, TValue> Setting<TSettings, TValue>(this List<IApplicableSetting2<TSettings>> list, string debugName, Func<TSettings, TValue> getter, Action<TSettings, TValue> setter)
		{
			return new GetSet<TSettings, TValue>(debugName, getter, setter);
		}
	}

	// ====================================================================================================================================================================
	// ====================================================================================================================================================================
	//
	// Converting implementations of ISetting<TSettings, T>
	// Need these because some settings are stored differently in the xml file than they are used in the application.
	//

	sealed class EnumSetting2<TSettings, T> : IGetSet<TSettings, T>
		where T : struct, Enum
	{
		public EnumSetting2(IGetSet<TSettings, string> inner)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
		}

		public string DebugName => this.inner.DebugName;
		private readonly IGetSet<TSettings, string> inner;

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
		public static EnumSetting2<TSettings, T> Enum<TSettings, T>(this IGetSet<TSettings, string> inner)
			where T : struct, Enum
		{
			return new EnumSetting2<TSettings, T>(inner);
		}
	}

	sealed class EncodingSetting2<TSettings, TSer, TApp> : IGetSet<TSettings, TApp>
	{
		public EncodingSetting2(
			IGetSet<TSettings, TSer> inner,
			Func<TSer, TApp> decode,
			Func<TApp, TSer> encode
			)
		{
			this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
			this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
			this.encode = encode ?? throw new ArgumentNullException(nameof(encode));
		}

		public string DebugName => this.inner.DebugName;
		private readonly IGetSet<TSettings, TSer> inner;
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
		public static EncodingSetting2<TSettings, TSer, TApp> Encoding<TSettings, TSer, TApp>(this IGetSet<TSettings, TSer> inner, Func<TSer, TApp> decode, Func<TApp, TSer> encode)
		{
			return new EncodingSetting2<TSettings, TSer, TApp>(inner, decode, encode);
		}
	}
}
