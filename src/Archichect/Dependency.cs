using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using JetBrains.Annotations;
using Archichect.Markers;
using Archichect.Matching;

namespace Archichect {
    public abstract class AbstractDependency<TItem> : IWithMarkerSet, IWithCt, IMatchableObject
        where TItem : AbstractItem<TItem> {
        protected AbstractDependency([NotNull] TItem usingItem, [NotNull] TItem usedItem, ISourceLocation source) {
            UsingItem = usingItem;
            UsedItem = usedItem;
            Source = source;
        }

        public const string DIP_ARROW = "=>";

        [NotNull]
        public TItem UsingItem { get; }
        [NotNull]
        public TItem UsedItem { get; }
        [NotNull]
        public abstract IMarkerSet MarkerSet { get; }

        public abstract int Ct { get; }
        public abstract int QuestionableCt { get; }
        public abstract int BadCt { get; }
        public abstract string NotOkReason { get; }

        [CanBeNull]
        public ISourceLocation Source { get; }
        /// <value>
        /// A guess where the use occurs in the
        /// original source file.
        /// </value>
        [CanBeNull]
        public abstract string ExampleInfo { get; }

        public bool IsMarkerMatch(IEnumerable<CountPattern<IMatcher>.Eval> evals) {
            return MarkerSet.IsMatch(evals);
        }

        public bool IsMarkerMatch(params string[] patterns) {
            return MarkerSet.IsMatch(patterns.Select(p => MarkerMatch.CreateEval(p, false)));
        }

        /// <summary>
        /// Coded name of using item.
        /// </summary>
        [NotNull]
        public string UsingItemAsString => UsingItem.AsString();

        /// <value>
        /// Coded name of used item.
        /// </value>
        [NotNull]
        public string UsedItemAsString => UsedItem.AsString();

        /// <summary>
        /// string representation of a Dependency.
        /// </summary>
        [ExcludeFromCodeCoverage]
        public override string ToString() {
            return UsingItem.AsFullString(50) + " --" + MarkerSet.AsFullString(50) + "-> " + UsedItem.AsFullString(50);
        }

        public int NotOkCt => QuestionableCt + BadCt;

        public int OkCt => Ct - QuestionableCt - BadCt;

        public bool IsMarkerMatch([CanBeNull] IEnumerable<DependencyMatch> matches, [CanBeNull] IEnumerable<DependencyMatch> excludes) {
            return (matches == null || !matches.Any() || matches.Any(m => m.IsMatch(this))) &&
                   (excludes == null || !excludes.Any() || !excludes.Any(m => m.IsMatch(this)));
        }

        public static ISet<Item> GetAllItems([NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, Func<Item, bool> selectItem) {
            var items = new HashSet<Item>(dependencies.SelectMany(d => new[] { d.UsingItem, d.UsedItem }));
            return selectItem == null ? items : new HashSet<Item>(items.Where(i => selectItem(i)));
        }

        /// <summary>
        /// A message presented to the user if this Dependency has a <see cref="BadCt"/>or <see cref="QuestionableCt"/>.
        /// </summary>
        /// <returns></returns>
        public string NotOkMessage(bool simpleRuleOutput, bool newLine, int maxLength = 600) {
            if (simpleRuleOutput) {
                return $"{UsingItem.AsString()} ---? {UsedItem.AsString()}";
            } else {
                string prefix = BadCt > 0
                    ? QuestionableCt > 0 ? "Bad and questionable d" : "Bad d"
                    : QuestionableCt > 0 ? "Questionable d" : "D";
                string nounTail = Ct > 1 ? "ependencies" : "ependency";
                string reason = string.IsNullOrWhiteSpace(NotOkReason) ? "" : " detected by " + NotOkReason;
                string brk = newLine ? Environment.NewLine + "    " : "";
                string ct = BadCt > 0
                    ? QuestionableCt > 0 ? $";{QuestionableCt};{BadCt}" : $";;{BadCt}"
                    : QuestionableCt > 0 ? $";{QuestionableCt};" : ";;";
                string markers = MarkerSet.AsFullString(maxLength / 3);
                return
                    $"{prefix}{nounTail}{reason}: {brk}{UsingItem.AsFullString(maxLength / 3)}{brk}-- {ct}{markers}{brk}-> {UsedItem.AsFullString(maxLength / 3)}" +
                    (Source != null ? $" {brk}" + (Ct > 1 ? "(e.g. at " : "(at ") + Source + ")" : "") +
                    (newLine ? Environment.NewLine : "");
            }
        }

        public string GetDotRepresentation(int? stringLengthForIllegalEdges) {
            // TODO: ?? And there should be a flag (in Edge?) "hasNotOkInfo", depending on whether dependency checking was done or not.
            return "\"" + UsingItem.Name + "\" -> \"" + UsedItem.Name + "\" ["
                       + GetDotLabel(stringLengthForIllegalEdges)
                       + GetDotFontSize()
                       + GetDotEdgePenWidthAndWeight()
                       + "];";
        }

        private string GetDotFontSize() {
            return " fontsize=" + (10 + 5 * Math.Round(Math.Log10(Ct)));
        }

        private string GetDotEdgePenWidthAndWeight() {
            double v = 1 + Math.Round(3 * Math.Log10(Ct));
            return " penwidth=" + v.ToString(CultureInfo.InvariantCulture) + (v < 5 ? " constraint=false" : "");
            //return " penwidth=" + v;
            //return " penwidth=" + v + " weight=" + v;
        }

        private string GetDotLabel(int? stringLengthForIllegalEdges) {
            return "label=\"" + (stringLengthForIllegalEdges.HasValue && ExampleInfo != null
                                ? LimitWidth(ExampleInfo, stringLengthForIllegalEdges.Value) + "\\n"
                                : "") +
                            " (" + Ct + (QuestionableCt + BadCt > 0 ? "(" + QuestionableCt + "?," + BadCt + "!)" : "") + ")" +
                            "\"";
        }

        private static string LimitWidth(string s, int lg) {
            if (s.Length > lg) {
                s = "..." + s.Substring(s.Length - lg + 3);
            }
            return s;
        }

        public string AsLimitableStringWithTypes(bool withExampleInfo, bool threeLines, int maxLength = 600) {
            string nl = threeLines ? Environment.NewLine + "    " : "";
            string exampleInfo = withExampleInfo ? ExampleInfo : null;
            string markers = MarkerSet.AsFullString(maxLength / 3);
            return $"{UsingItem.AsFullString(maxLength / 3)} "
                 + nl + $"{DIP_ARROW} {markers};{Ct};{QuestionableCt};{BadCt};{Source?.AsDipString()};{exampleInfo} "
                 + nl + $"{DIP_ARROW} {UsedItem.AsFullString(maxLength / 3)}";
        }
    }

    public class ReadOnlyDependency : AbstractDependency<ReadOnlyItem> {
        public ReadOnlyDependency(ReadOnlyItem usingItem, ReadOnlyItem usedItem, ISourceLocation source, IMarkerSet markerSet,
                                   int ct, int questionableCt, int badCt, string notOkReason, string exampleInfo)
                : base(usingItem, usedItem, source) {
            MarkerSet = markerSet;
            Ct = ct;
            QuestionableCt = questionableCt;
            BadCt = badCt;
            NotOkReason = notOkReason;
            ExampleInfo = exampleInfo;
        }

        public override IMarkerSet MarkerSet { get; }
        public override int Ct { get; }
        public override int QuestionableCt { get; }
        public override int BadCt { get; }
        public override string NotOkReason { get; }
        public override string ExampleInfo { get; }
    }

    [DebuggerDisplay("{" + nameof(ToString) + "()}")]
    public class Dependency : AbstractDependency<Item>, IWithMutableMarkerSet {
        [NotNull]
        private readonly MutableMarkerSet _markerSet;

        private int _ct;
        private int _questionableCt;
        private int _badCt;

        [CanBeNull]
        private string _notOkReason;

        [CanBeNull]
        private string _exampleInfo;

        public override IMarkerSet MarkerSet => _markerSet;

        public AbstractMarkerSet AbstractMarkerSet => _markerSet;

        public override string NotOkReason => _notOkReason;

        /// <summary>
        /// Create a dependency.
        /// </summary>
        /// <param name="usingItem">The using item.</param>
        /// <param name="usedItem">The used item.</param>
        /// <param name="source">Name of the file.</param>
        /// <param name="markers"></param>
        /// <param name="ct"></param>
        /// <param name="questionableCt"></param>
        /// <param name="badCt"></param>
        /// <param name="notOkReason"></param>
        /// <param name="exampleInfo"></param>
        /// <param name="ignoreCaseDefault"></param>
        internal Dependency([NotNull] Item usingItem, [NotNull] Item usedItem,
            [CanBeNull] ISourceLocation source, [CanBeNull] IMarkerSet markers,
            int ct, int questionableCt = 0, int badCt = 0, string notOkReason = null,
            [CanBeNull] string exampleInfo = null, bool? ignoreCaseDefault = null) : base(usingItem, usedItem, source) {
            if (usingItem == null) {
                throw new ArgumentNullException(nameof(usingItem));
            }
            if (usedItem == null) {
                throw new ArgumentNullException(nameof(usedItem));
            }
            _ct = ct;
            _questionableCt = questionableCt;
            _badCt = badCt;
            _exampleInfo = exampleInfo;
            bool ignoreCase = ignoreCaseDefault ?? usingItem.Type.IgnoreCase | usedItem.Type.IgnoreCase;
            _markerSet = new MutableMarkerSet(ignoreCase, markers);

            // Make sure that "no reason" is null so that ?? works.
            _notOkReason = string.IsNullOrWhiteSpace(notOkReason) ? null : notOkReason;
            CheckNotOkReason();
        }

        //public static Dependency CreateDependency([NotNull] Item usingItem, [NotNull] Item usedItem,
        //    [CanBeNull] ISourceLocation source, [NotNull] string markers, int ct, int questionableCt = 0, int badCt = 0,
        //    [CanBeNull] string exampleInfo = null) {

        //    return CreateDependency(usingItem, usedItem, source, markers: markers.Split('&', '+', ','), ct: ct,
        //        questionableCt: questionableCt, badCt: badCt, exampleInfo: exampleInfo);
        //}

        public override int Ct => _ct;

        public override int QuestionableCt => _questionableCt;

        public override int BadCt => _badCt;

        public override string ExampleInfo => _exampleInfo;

        public void MarkAsBad(string reason) {
            SetBadCount(_ct, reason);
        }

        public void IncrementBad(string reason) {
            SetBadCount(_badCt + 1, reason);
        }

        public void ResetBad() {
            _badCt = 0;
            if (_questionableCt == 0) {
                _notOkReason = null;
            }
            CheckNotOkReason();
        }

        private void SetBadCount(int value, string reason) {
            // First bad example overrides any previous example
            if (_badCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---! " + UsedItemAsString;
            }
            if (_badCt == 0 || _notOkReason == null) {
                _notOkReason = reason;
            }
            _badCt = value;
            CheckNotOkReason();
        }

        public void MarkAsQuestionable(string reason) {
            SetQuestionableCount(_ct, reason);
        }

        public void IncrementQuestionable(string reason) {
            SetQuestionableCount(_questionableCt + 1, reason);
        }

        public void ResetQuestionable() {
            _questionableCt = 0;
            if (_badCt == 0) {
                _notOkReason = null;
            }
            CheckNotOkReason();
        }

        private void SetQuestionableCount(int value, string reason) {
            if (NotOkCt == 0 || _exampleInfo == null) {
                _exampleInfo = UsingItemAsString + " ---? " + UsedItemAsString;
            }
            if (NotOkCt == 0 || _notOkReason == null) {
                _notOkReason = reason;
            }
            _questionableCt = value;
            CheckNotOkReason();
        }

        private void CheckNotOkReason() {
            if (NotOkCt > 0 && string.IsNullOrWhiteSpace(_notOkReason)) {
                throw new ArgumentException($"Each bad or questionable count requires a reason - missing in dependency {this}");
            }
        }

        public void AggregateMarkersAndCounts(Dependency d) {
            _markerSet.MergeWithMarkers(d.MarkerSet);
            _ct += d._ct;
            _questionableCt += d._questionableCt;
            _badCt += d._badCt;
            _exampleInfo = _exampleInfo ?? d._exampleInfo;
            _notOkReason = _notOkReason ?? d._notOkReason;

            CheckNotOkReason();
        }

        private static Item GetOrCreateItem<T>(Dictionary<Item, Item> canonicalItems, Dictionary<Item, List<T>> itemsAndDependencies, Item item) where T : Dependency {
            Item result;
            if (!canonicalItems.TryGetValue(item, out result)) {
                canonicalItems.Add(item, result = item);
            }
            if (!itemsAndDependencies.ContainsKey(result)) {
                itemsAndDependencies.Add(result, new List<T>());
            }
            return result;
        }

        // TODO: Duplicate of Outgoing???
        internal static IDictionary<Item, IEnumerable<Dependency>> Dependencies2ItemsAndDependencies(IEnumerable<Dependency> edges) {
            Dictionary<Item, List<Dependency>> result = Dependencies2ItemsAndDependenciesList(edges);
            return result.ToDictionary<KeyValuePair<Item, List<Dependency>>, Item, IEnumerable<Dependency>>(kvp => kvp.Key, kvp => kvp.Value);
        }

        internal static Dictionary<Item, List<Dependency>> Dependencies2ItemsAndDependenciesList(IEnumerable<Dependency> edges) {
            var canonicalItems = new Dictionary<Item, Item>();
            var result = new Dictionary<Item, List<Dependency>>();
            foreach (var e in edges) {
                Item @using = GetOrCreateItem(canonicalItems, result, e.UsingItem);
                GetOrCreateItem(canonicalItems, result, e.UsedItem);

                result[@using].Add(e);
            }
            return result;
        }

        public void IncrementMarker(string marker) {
            _markerSet.IncrementMarker(marker);
        }

        public void SetMarker(string marker, int value) {
            _markerSet.SetMarker(marker, value);
        }

        public void UnionWithMarkers(IReadOnlyDictionary<string, int> markerPatterns) {
            _markerSet.MergeWithMarkers(markerPatterns);
        }

        public void RemoveMarkers(string markerPattern, bool ignoreCase) {
            _markerSet.RemoveMarkers(markerPattern, ignoreCase);
        }

        public void RemoveMarkers(IEnumerable<string> markerPatterns, bool ignoreCase) {
            _markerSet.RemoveMarkers(markerPatterns, ignoreCase);
        }

        public void ClearMarkers() {
            _markerSet.ClearMarkers();
        }

        public Dependency Clone() {
            return new Dependency(usingItem: UsingItem, usedItem: UsedItem, source: Source, markers: MarkerSet, ct: Ct, 
                                  questionableCt: QuestionableCt, badCt: BadCt, notOkReason: NotOkReason, exampleInfo: ExampleInfo);
        }
    }
}