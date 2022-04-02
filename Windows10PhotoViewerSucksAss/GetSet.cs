using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Windows10PhotoViewerSucksAss
{
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

	// ====================================================================================================================================================================
	// ====================================================================================================================================================================
	//
	// Converting implementations of ISetting<TSettings, T>
	// Need these because some settings are stored differently in the xml file than they are used in the application.
	//

	sealed class GetSet_Enum<TSettings, T> : IGetSet<TSettings, T>
		where T : struct, Enum
	{
		public GetSet_Enum(IGetSet<TSettings, string> inner)
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

	static class GetSet_Enum
	{
		public static GetSet_Enum<TSettings, T> Enum<TSettings, T>(this IGetSet<TSettings, string> inner)
			where T : struct, Enum
		{
			return new GetSet_Enum<TSettings, T>(inner);
		}
	}

	sealed class GetSet_Encoding<TSettings, TSer, TApp> : IGetSet<TSettings, TApp>
	{
		public GetSet_Encoding(
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

	static class GetSet_Encoding
	{
		public static GetSet_Encoding<TSettings, TSer, TApp> Encoding<TSettings, TSer, TApp>(this IGetSet<TSettings, TSer> inner, Func<TSer, TApp> decode, Func<TApp, TSer> encode)
		{
			return new GetSet_Encoding<TSettings, TSer, TApp>(inner, decode, encode);
		}
	}
}
