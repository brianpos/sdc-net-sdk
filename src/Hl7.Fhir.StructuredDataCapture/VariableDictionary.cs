using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hl7.Fhir.ElementModel;
using Hl7.Fhir.Model;

namespace Hl7.Fhir.StructuredDataCapture
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using Hl7.Fhir.ElementModel;

	public class VariableDictionary : IDictionary<string, IEnumerable<ITypedElement>>
	{
		private readonly IDictionary<string, IEnumerable<ITypedElement>> _dictionary;
		private readonly VariableDictionary _parent;

		public VariableDictionary()
		{
			_dictionary = new Dictionary<string, IEnumerable<ITypedElement>>();
		}

		public VariableDictionary(VariableDictionary parent)
		{
			_dictionary = new Dictionary<string, IEnumerable<ITypedElement>>();
			_parent = parent;
		}

		public IEnumerable<ITypedElement> this[string key]
		{
			get
			{
				if (_dictionary.TryGetValue(key, out var value))
					return value;
				else if (_parent != null)
					return _parent[key];
				else
					throw new KeyNotFoundException($"The given key '{key}' was not present in the variables.");
			}
			set
			{
				_dictionary[key] = value;
			}
		}

		public ICollection<string> Keys
		{
			get
			{
				var keys = new HashSet<string>(_dictionary.Keys);
				if (_parent != null)
					keys.UnionWith(_parent.Keys);
				return keys;
			}
		}

		public ICollection<IEnumerable<ITypedElement>> Values
		{
			get
			{
				var values = new List<IEnumerable<ITypedElement>>(_dictionary.Values);
				if (_parent != null)
				{
					foreach (var key in _parent.Keys)
					{
						if (!_dictionary.ContainsKey(key))
							values.Add(_parent[key]);
					}
				}
				return values;
			}
		}

		public int Count
		{
			get
			{
				var keys = new HashSet<string>(_dictionary.Keys);
				if (_parent != null)
					keys.UnionWith(_parent.Keys);
				return keys.Count;
			}
		}

		public bool IsReadOnly => false;

		public void Add(string key, IEnumerable<ITypedElement> value)
		{
			_dictionary.Add(key, value);
		}

		public void Add(KeyValuePair<string, IEnumerable<ITypedElement>> item)
		{
			_dictionary.Add(item);
		}

		public void Clear()
		{
			_dictionary.Clear();
		}

		public bool Contains(KeyValuePair<string, IEnumerable<ITypedElement>> item)
		{
			if (_dictionary.Contains(item))
				return true;
			else if (_parent != null)
				return _parent.Contains(item);
			else
				return false;
		}

		public bool ContainsKey(string key)
		{
			if (_dictionary.ContainsKey(key))
				return true;
			else if (_parent != null)
				return _parent.ContainsKey(key);
			else
				return false;
		}

		public void CopyTo(KeyValuePair<string, IEnumerable<ITypedElement>>[] array, int arrayIndex)
		{
			var allItems = new List<KeyValuePair<string, IEnumerable<ITypedElement>>>(this);
			allItems.CopyTo(array, arrayIndex);
		}

		public IEnumerator<KeyValuePair<string, IEnumerable<ITypedElement>>> GetEnumerator()
		{
			var set = new HashSet<string>();
			foreach (var kvp in _dictionary)
			{
				set.Add(kvp.Key);
				yield return kvp;
			}

			if (_parent != null)
			{
				foreach (var kvp in _parent)
				{
					if (!set.Contains(kvp.Key))
					{
						set.Add(kvp.Key);
						yield return kvp;
					}
				}
			}
		}

		public bool Remove(string key)
		{
			return _dictionary.Remove(key);
		}

		public bool Remove(KeyValuePair<string, IEnumerable<ITypedElement>> item)
		{
			return _dictionary.Remove(item);
		}

		public bool TryGetValue(string key, out IEnumerable<ITypedElement> value)
		{
			if (_dictionary.TryGetValue(key, out value))
				return true;
			else if (_parent != null)
				return _parent.TryGetValue(key, out value);
			else
			{
				value = default;
				return false;
			}
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
	}
}
