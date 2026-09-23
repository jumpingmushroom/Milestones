using System;
using System.Collections.Generic;

namespace Milestones.Core.Model
{
    /// <summary>Up to three achievement ids, in pin order. Stored as "id1,id2,id3".</summary>
    public sealed class PinList
    {
        public const int Max = 3;

        private readonly List<string> _ids = new List<string>(Max);

        public IReadOnlyList<string> Ids => _ids;
        public int Count => _ids.Count;
        public bool IsFull => _ids.Count >= Max;

        public static PinList Parse(string raw)
        {
            var list = new PinList();
            if (string.IsNullOrEmpty(raw))
                return list;
            foreach (string part in raw.Split(','))
                list.Add(part.Trim());
            return list;
        }

        public string Serialize()
        {
            return string.Join(",", _ids);
        }

        public bool Contains(string id)
        {
            return _ids.Contains(id);
        }

        public bool Add(string id)
        {
            if (string.IsNullOrEmpty(id) || id.IndexOf(',') >= 0 || IsFull || _ids.Contains(id))
                return false;
            _ids.Add(id);
            return true;
        }

        public bool Remove(string id)
        {
            return _ids.Remove(id);
        }

        public int RemoveWhere(Func<string, bool> drop)
        {
            return _ids.RemoveAll(id => drop(id));
        }
    }
}
