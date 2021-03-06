using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Gibraltar;
using JetBrains.Annotations;
using Archichect.Markers;
using Archichect.Matching;

namespace Archichect {
    public abstract class ItemSegment {
        [NotNull]
        private readonly ItemType _type;
        [NotNull]
        public readonly string[] Values;
        [NotNull]
        public readonly string[] CasedValues;

        private readonly int _hash;
        

        protected ItemSegment([NotNull] ItemType type, [NotNull] string[] values) {
            if (type == null) {
                throw new ArgumentNullException(nameof(type));
            }
            _type = type;
            for (int i = 0; i < values.Length; i++) {
                if (values[i] != null && values[i].Length < 50) {
                    values[i] = string.Intern(values[i]);
                }
            }
            IEnumerable<string> enoughValues = values.Length < type.Length ? values.Concat(Enumerable.Range(0, type.Length - values.Length).Select(i => "")) : values;
            Values = values == enoughValues ? values : enoughValues.ToArray();
            CasedValues = type.IgnoreCase ? enoughValues.Select(v => v.ToUpperInvariant()).ToArray() : Values;

            unchecked {
                int h = _type.GetHashCode();

                foreach (var t in CasedValues) {
                    if (t == null) {
                        h *= 11;
                    } else {
                        h = 157204211 * h + t.GetHashCode();
                    }
                }
                _hash = h;
            }
        }

        public ItemType Type => _type;

        [DebuggerStepThrough]
        protected bool EqualsSegment(ItemSegment other) {
            if (other == null) {
                return false;
            } else if (ReferenceEquals(other, this)) {
                return true;
            } else if (other._hash != _hash) {
                return false;
            } else if (!Type.Equals(other.Type)) {
                return false;
            } else if (Values.Length != other.Values.Length) {
                return false;
            } else {
                for (int i = 0; i < CasedValues.Length; i++) {
                    if (CasedValues[i] != other.CasedValues[i]) {
                        return false;
                    }
                }
                return true;
            }
        }

        [DebuggerStepThrough]
        protected int SegmentHashCode() {
            return _hash;
        }
    }

    public sealed class ItemTail : ItemSegment {
        private ItemTail([NotNull]ItemType type, [NotNull, ItemNotNull] string[] values) : base(type, values) {
        }

        public static ItemTail New(Intern<ItemTail> cache, [NotNull] ItemType type, [NotNull, ItemNotNull] string[] values) {
            return cache.GetReference(new ItemTail(type, values));
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return "ItemTail(" + Type + ":" + string.Join(":", Values) + ")";
        }

        public override bool Equals(object other) {
            return EqualsSegment(other as ItemTail);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return SegmentHashCode();
        }
    }

    // See http://stackoverflow.com/questions/31562791/what-makes-the-visual-studio-debugger-stop-evaluating-a-tostring-override
    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public abstract class AbstractItem<TItem> : ItemSegment, IMatchableObject where TItem : AbstractItem<TItem> {
        private string _asString;
        private string _asFullString;

        [NotNull]
        public abstract IMarkerSet MarkerSet {
            get;
        }

        protected AbstractItem([NotNull] ItemType type, string[] values) : base(type, values) {
            if (type.Length < values.Length) {
                throw new ArgumentException(
                    $"ItemType '{type.Name}' is defined as '{type}' with {type.Length} fields, but item is created with {values.Length} fields '{string.Join(":", values)}'",
                    nameof(values));
            }
        }

        public string Name => AsString();

        public bool IsEmpty() => Values.All(s => s == "");

        public string GetCasedValue(int i) {
            return i < 0 || i >= CasedValues.Length ? null : CasedValues[i];
        }

        [DebuggerStepThrough]
        public override bool Equals(object obj) {
            AbstractItem<TItem> other = obj as AbstractItem<TItem>;
            return other != null && EqualsSegment(other);
        }

        [DebuggerHidden]
        public override int GetHashCode() {
            return SegmentHashCode();
        }

        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return AsFullString(300);
        }

        [NotNull]
        public string AsFullString(int maxLength = 250) {
            if (_asFullString == null) {
                string s = AsString();
                _asFullString = Type.Name + ":" + s + MarkerSet.AsFullString(maxLength - s.Length);
            }
            return _asFullString;
        }

        protected void MarkersHaveChanged() {
            _asFullString = null;
        }

        public bool IsMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals) {
            return MarkerSet.IsMatch(evals);
        }

        [NotNull]
        public string AsString() {
            if (_asString == null) {
                var sb = new StringBuilder();
                string sep = "";
                for (int i = 0; i < Type.Length; i++) {
                    sb.Append(sep);
                    sb.Append(Values[i]);
                    sep = i < Type.Length - 1 && Type.Keys[i + 1] == Type.Keys[i] ? ";" : ":";
                }
                _asString = sb.ToString();
            }
            return _asString;
        }

        public static Dictionary<TItem, TDependency[]> CollectIncomingDependenciesMap<TDependency>(
                [NotNull, ItemNotNull] IEnumerable<TDependency> dependencies, Func<TItem, bool> selectItem = null)
                where TDependency : AbstractDependency<TItem> {
            return CollectMap(dependencies, d => selectItem == null || selectItem(d.UsedItem) ? d.UsedItem : null, d => d)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        }

        public static Dictionary<TItem, TDependency[]> CollectOutgoingDependenciesMap<TDependency>(
                [NotNull, ItemNotNull] IEnumerable<TDependency> dependencies, Func<TItem, bool> selectItem = null)
                where TDependency : AbstractDependency<TItem> {
            return CollectMap(dependencies, d => selectItem == null || selectItem(d.UsingItem) ? d.UsingItem : null, d => d)
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value.ToArray());
        }

        public static Dictionary<TItem, List<TResult>> CollectMap<TDependency, TResult>(
            [NotNull, ItemNotNull] IEnumerable<TDependency> dependencies, [NotNull] Func<TDependency, TItem> getItem,
            [NotNull] Func<TDependency, TResult> createT) {
            var result = new Dictionary<TItem, List<TResult>>();
            foreach (var d in dependencies) {
                TItem key = getItem(d);
                if (key != null) {
                    List<TResult> list;
                    if (!result.TryGetValue(key, out list)) {
                        result.Add(key, list = new List<TResult>());
                    }
                    list.Add(createT(d));
                }
            }
            return result;
        }

        ////public static void Reset() {
        ////    Intern<ItemTail>.Reset();
        ////    Intern<Item>.Reset();
        ////}

        public bool IsMatch([CanBeNull] IEnumerable<ItemMatch> matches, [CanBeNull] IEnumerable<ItemMatch> excludes) {
            return (matches == null || !matches.Any() || matches.Any(m => m.Matches(this).Success)) &&
                   (excludes == null || !excludes.Any() || excludes.All(m => !m.Matches(this).Success));
        }
    }

    public class ReadOnlyItem : AbstractItem<ReadOnlyItem> {
        [NotNull]
        private readonly ReadOnlyMarkerSet _markerSet;

        protected ReadOnlyItem([NotNull] ItemType type, string[] values) : base(type, values) {
            _markerSet = new ReadOnlyMarkerSet(type.IgnoreCase, markers: Enumerable.Empty<string>());
        }

        public override IMarkerSet MarkerSet => _markerSet;
    }

    public interface IItemAndDependencyFactory : IPlugin {
        Item CreateItem([NotNull] ItemType type, [ItemNotNull] string[] values);

        Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem, [CanBeNull] ISourceLocation source,
            [CanBeNull] IMarkerSet markers, int ct, int questionableCt = 0, int badCt = 0, string notOkReason = null,
            [CanBeNull] string exampleInfo = null);
    }

    public class DefaultItemAndDependencyFactory : IItemAndDependencyFactory {
        public Item CreateItem([NotNull] ItemType type, [ItemNotNull] string[] values) {
            return new Item(type, values);
        }

        public Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem, [CanBeNull] ISourceLocation source,
            [CanBeNull] IMarkerSet markers, int ct, int questionableCt = 0, int badCt = 0, string notOkReason = null,
            [CanBeNull] string exampleInfo = null) {
            return new Dependency(usingItem, usedItem, source, markers: markers,
                ct: ct, questionableCt: questionableCt, badCt: badCt, notOkReason: notOkReason, exampleInfo: exampleInfo);
        }

        public string GetHelp(bool detailedHelp, string filter) {
            return "Default factory for generic items and dependencies";
        }
    }

    public class Item : AbstractItem<Item>, IWithMutableMarkerSet {
        private WorkingGraph _workingGraph;
        internal Item SetWorkingGraph(WorkingGraph workingGraph) {
            _workingGraph = workingGraph;
            return this;
        }

        [NotNull, ItemNotNull]
        public IEnumerable<Dependency> GetOutgoing() => _workingGraph.VisibleOutgoingVisible.Get(this) ?? Enumerable.Empty<Dependency>();

        [NotNull, ItemNotNull]
        public IEnumerable<Dependency> GetIncoming() => _workingGraph.VisibleIncomingVisible.Get(this) ?? Enumerable.Empty<Dependency>();

        [NotNull]
        private readonly MutableMarkerSet _markerSet;

        protected internal Item([NotNull] ItemType type, string[] values) : base(type, values) {
            _markerSet = new MutableMarkerSet(type.IgnoreCase, markers: null);
        }

        public override IMarkerSet MarkerSet => _markerSet;

        [NotNull]
        public Item Append(WorkingGraph graph, [CanBeNull] ItemTail additionalValues) {
            return additionalValues == null
                ? this
                : graph.CreateItem(additionalValues.Type, Values.Concat(additionalValues.Values.Skip(Type.Length)).ToArray());
        }

        public void MergeWithMarkers(IMarkerSet markers) {
            _markerSet.MergeWithMarkers(markers);
            MarkersHaveChanged();
        }

        public void IncrementMarker(string marker) {
            _markerSet.IncrementMarker(marker);
            MarkersHaveChanged();
        }

        public void SetMarker(string marker, int value) {
            _markerSet.SetMarker(marker, value);
            MarkersHaveChanged();
        }

        public void RemoveMarkers(string markerPattern, bool ignoreCase) {
            _markerSet.RemoveMarkers(markerPattern, ignoreCase);
            MarkersHaveChanged();
        }

        public void RemoveMarkers(IEnumerable<string> markerPatterns, bool ignoreCase) {
            _markerSet.RemoveMarkers(markerPatterns, ignoreCase);
            MarkersHaveChanged();
        }

        public void ClearMarkers() {
            _markerSet.ClearMarkers();
            MarkersHaveChanged();
        }

        public static readonly string ITEM_HELP = @"
TBD

Item matches
============

An item match is a string that is matched against items for various
plugins. An item match has the following format (unfortunately, not all
plugins follow this format as of today):
   
    [ typename : ] positionfieldmatch {{ : positionfieldmatch }} [markerpattern]

or 

    typename : namedfieldmatch {{ : namedfieldmatch }} [markerpattern]

For more information on types, see the help topic for 'type'.
The marker pattern is described in the help text for 'marker'.

A positionfieldmatch has the following format:

    TBD

A namedfieldmatch has the following format:

    name=positionfieldmatch

TBD
";
    }
}