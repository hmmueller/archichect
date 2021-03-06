using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using Gibraltar;
using JetBrains.Annotations;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Pdb;
using Mono.Collections.Generic;

namespace Archichect.Reading.AssemblyReading {
    public enum DotNetUsage {
        _declaresfield,
        _declaresevent,
        _declaresmethod,
        _declaresparameter,
        _declaresreturntype,
        _declaresvariable,
        _isconstrainedby,
        _usesmember,
        //_usesmemberoftype, // requires declarations of "uses that type" rules, which opens up possibility to use ALL of that type.
        // This is not good. Rather, let the user manually add transitive dependencies via the member if this is needed!
        _usestype,
        _directlyderivedfrom,
        _directlyimplements,
        _usesasgenericargument,
    }

    public abstract class AbstractDotNetAssemblyDependencyReader : AbstractDependencyReader {
        public const string _abstract = nameof(_abstract);
        public const string _array = nameof(_array);
        public const string _class = nameof(_class);
        public const string _const = nameof(_const);
        public const string _ctor = nameof(_ctor);
        public const string _definition = nameof(_definition);
        public const string _enum = nameof(_enum);
        public const string _get = nameof(_get);
        public const string _in = nameof(_in);
        public const string _interface = nameof(_interface);
        public const string _internal = nameof(_internal);
        public const string _nested = nameof(_nested);
        public const string _nestedprivate = nameof(_nestedprivate);
        public const string _notpublic = nameof(_notpublic);
        public const string _notserialized = nameof(_notserialized);
        public const string _optional = nameof(_optional);
        public const string _out = nameof(_out);
        public const string _pinned = nameof(_pinned);
        public const string _primitive = nameof(_primitive);
        public const string _private = nameof(_private);
        public const string _protected = nameof(_protected);
        public const string _public = nameof(_public);
        public const string _readonly = nameof(_readonly);
        public const string _ref = nameof(_ref);
        public const string _return = nameof(_return);
        public const string _runtimespecialname = nameof(_runtimespecialname);
        public const string _sealed = nameof(_sealed);
        public const string _set = nameof(_set);
        public const string _specialname = nameof(_specialname);
        public const string _static = nameof(_static);
        public const string _struct = nameof(_struct);
        public const string _virtual = nameof(_virtual);

        public const string _declaresfield = nameof(DotNetUsage._declaresfield);
        public const string _declaresevent = nameof(DotNetUsage._declaresevent);
        public const string _declaresparameter = nameof(DotNetUsage._declaresparameter);
        public const string _declaresreturntype = nameof(DotNetUsage._declaresreturntype);
        public const string _declaresvariable = nameof(DotNetUsage._declaresvariable);
        public const string _usesmember = nameof(DotNetUsage._usesmember);
        public const string _usestype = nameof(DotNetUsage._usestype);
        public const string _directlyderivedfrom = nameof(DotNetUsage._directlyderivedfrom);
        public const string _directlyimplements = nameof(DotNetUsage._directlyimplements);
        public const string _usesasgenericargument = nameof(DotNetUsage._usesasgenericargument);

        public static readonly ItemType DOTNETASSEMBLY = DotNetAssemblyDependencyReaderFactory.DOTNETASSEMBLY;
        public static readonly ItemType DOTNETEVENT = DotNetAssemblyDependencyReaderFactory.DOTNETEVENT;
        public static readonly ItemType DOTNETFIELD = DotNetAssemblyDependencyReaderFactory.DOTNETFIELD;
        public static readonly ItemType DOTNETITEM = DotNetAssemblyDependencyReaderFactory.DOTNETITEM;
        public static readonly ItemType DOTNETMETHOD = DotNetAssemblyDependencyReaderFactory.DOTNETMETHOD;
        public static readonly ItemType DOTNETPARAMETER = DotNetAssemblyDependencyReaderFactory.DOTNETPARAMETER;
        public static readonly ItemType DOTNETPROPERTY = DotNetAssemblyDependencyReaderFactory.DOTNETPROPERTY;
        public static readonly ItemType DOTNETTYPE = DotNetAssemblyDependencyReaderFactory.DOTNETTYPE;
        public static readonly ItemType DOTNETGENERICPARAMETER = DotNetAssemblyDependencyReaderFactory.DOTNETTYPE;
        public static readonly ItemType DOTNETVARIABLE = DotNetAssemblyDependencyReaderFactory.DOTNETVARIABLE;

        protected readonly DotNetAssemblyDependencyReaderFactory ReaderFactory;
        protected readonly DotNetAssemblyDependencyReaderFactory.ReadingContext _readingContext;
        public readonly string Assemblyname;

        protected readonly Intern<RawUsingItem> _rawUsingItemsCache = new Intern<RawUsingItem>();

        protected readonly string[] GET_MARKER = { _get };
        protected readonly string[] SET_MARKER = { _set };

        private Dictionary<RawUsedItem, Item> _rawItems2Items;

        protected AbstractDotNetAssemblyDependencyReader(DotNetAssemblyDependencyReaderFactory readerFactory, string fileName, 
                DotNetAssemblyDependencyReaderFactory.ReadingContext readingContext)
            : base(Path.GetFullPath(fileName), Path.GetFileName(fileName)) {
            ReaderFactory = readerFactory;
            _readingContext = readingContext;
            Assemblyname = Path.GetFileNameWithoutExtension(fileName);
        }

        public string AssemblyName => Assemblyname;

        internal static void Init() {
#pragma warning disable 168
            // ReSharper disable once UnusedVariable
            // the only purpose of this instruction is to create a reference to Mono.Cecil.Pdb.
            // Otherwise Visual Studio won't copy that assembly to the output path.
            var readerProvider = new PdbReaderProvider();
#pragma warning restore 168
        }

        protected abstract class RawAbstractItem {
            public readonly ItemType ItemType;
            public readonly string NamespaceName;
            public readonly string ClassName;
            public readonly string AssemblyName;
            private readonly string _assemblyVersion;
            private readonly string _assemblyCulture;
            public readonly string MemberName;
            [CanBeNull, ItemNotNull]
            private readonly string[] _markers;
            [NotNull]
            protected readonly WorkingGraph _readingGraph;

            protected RawAbstractItem([NotNull] ItemType itemType, [NotNull] string namespaceName, [NotNull] string className,
                [NotNull] string assemblyName, [CanBeNull] string assemblyVersion, [CanBeNull] string assemblyCulture,
                [CanBeNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, [NotNull] WorkingGraph readingGraph) {
                if (itemType == null) {
                    throw new ArgumentNullException(nameof(itemType));
                }
                if (namespaceName == null) {
                    throw new ArgumentNullException(nameof(namespaceName));
                }
                if (className == null) {
                    throw new ArgumentNullException(nameof(className));
                }
                if (assemblyName == null) {
                    throw new ArgumentNullException(nameof(assemblyName));
                }
                ItemType = itemType;
                NamespaceName = string.Intern(namespaceName);
                ClassName = string.Intern(className);
                AssemblyName = string.Intern(assemblyName);
                _assemblyVersion = string.Intern(assemblyVersion ?? "");
                _assemblyCulture = string.Intern(assemblyCulture ?? "");
                MemberName = string.Intern(memberName ?? "");
                _markers = markers;
                _readingGraph = readingGraph;
            }

            [ExcludeFromCodeCoverage]
            public override string ToString() {
                return NamespaceName + ":" + ClassName + ":" + AssemblyName + ";" + _assemblyVersion + ";" +
                       _assemblyCulture + ":" + MemberName + (_markers == null ? "" : "'" + string.Join("+", _markers));
            }

            [NotNull]
            public virtual Item ToItem() {
                return _readingGraph.CreateItem(ItemType, new[] { NamespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, MemberName }, _markers);
            }

            [NotNull]
            protected RawUsedItem ToRawUsedItem() {
                return RawUsedItem.New(ItemType, NamespaceName, ClassName, AssemblyName, _assemblyVersion, _assemblyCulture, MemberName, _markers, _readingGraph);
            }

            protected bool EqualsRawAbstractItem(RawAbstractItem other) {
                return this == other
                    || other != null
                       && other.NamespaceName == NamespaceName
                       && other.ClassName == ClassName
                       && other.AssemblyName == AssemblyName
                       && other._assemblyVersion == _assemblyVersion
                       && other._assemblyCulture == _assemblyCulture
                       && other.MemberName == MemberName;
            }

            protected int GetRawAbstractItemHashCode() {
                return unchecked(NamespaceName.GetHashCode() ^ ClassName.GetHashCode() ^ AssemblyName.GetHashCode() ^ (MemberName ?? "").GetHashCode());
            }
        }

        protected sealed class RawUsingItem : RawAbstractItem {
            private readonly ItemTail _tail;
            private Item _item;
            private RawUsedItem _usedItem;

            private RawUsingItem([NotNull] ItemType itemType, [NotNull] string namespaceName, [NotNull] string className,
                [NotNull] string assemblyName, [CanBeNull] string assemblyVersion, [CanBeNull] string assemblyCulture,
                [CanBeNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, [CanBeNull] ItemTail tail,
                [NotNull] WorkingGraph readingGraph)
                : base(itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, readingGraph) {
                _tail = tail;
            }

            public static RawUsingItem New([NotNull] Intern<RawUsingItem> cache, [NotNull] ItemType itemType,
                [NotNull] string namespaceName, [NotNull] string className, [NotNull] string assemblyName,
                [CanBeNull] string assemblyVersion, [CanBeNull] string assemblyCulture, [CanBeNull] string memberName,
                [CanBeNull, ItemNotNull] string[] markers, [CanBeNull] ItemTail tail, [NotNull] WorkingGraph readingGraph) {
                return cache.GetReference(new RawUsingItem(itemType, namespaceName, className, assemblyName,
                                                           assemblyVersion, assemblyCulture, memberName, markers, tail, readingGraph));
            }

            public override string ToString() {
                return "RawUsingItem(" + base.ToString() + ":" + _tail + ")";
            }

            public override bool Equals(object obj) {
                return EqualsRawAbstractItem(obj as RawUsingItem);
            }

            public override int GetHashCode() {
                return GetRawAbstractItemHashCode();
            }

            public override Item ToItem() {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (_item == null) {
                    _item = base.ToItem().Append(_readingGraph, _tail);
                }
                return _item;
            }

            public new RawUsedItem ToRawUsedItem() {
                // ReSharper disable once ConvertIfStatementToNullCoalescingExpression
                if (_usedItem == null) {
                    _usedItem = base.ToRawUsedItem();
                }
                return _usedItem;
            }
        }

        protected sealed class RawUsedItem : RawAbstractItem {
            private RawUsedItem([NotNull] ItemType itemType, [NotNull] string namespaceName, [NotNull] string className,
                [NotNull] string assemblyName, [CanBeNull] string assemblyVersion, [CanBeNull] string assemblyCulture,
                [CanBeNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, [NotNull] WorkingGraph readingGraph)
                : base(itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, readingGraph) {
            }

            public static RawUsedItem New([NotNull] ItemType itemType, [NotNull] string namespaceName, [NotNull] string className,
                [NotNull] string assemblyName, [CanBeNull] string assemblyVersion, [CanBeNull] string assemblyCulture,
                [CanBeNull] string memberName, [CanBeNull, ItemNotNull] string[] markers, [NotNull] WorkingGraph readingGraph) {
                //return Intern<RawUsedItem>.GetReference(new RawUsedItem(namespaceName, className, assemblyName,
                //        assemblyVersion, assemblyCulture, memberName, markers));
                // Dont make unique - costs lot of time; and Raw...Items are anyway removed at end of DLL reading.
                return new RawUsedItem(itemType, namespaceName, className, assemblyName, assemblyVersion, assemblyCulture, memberName, markers, readingGraph);
            }

            [ExcludeFromCodeCoverage]
            public override string ToString() {
                return "RawUsedItem(" + base.ToString() + ")";
            }

            [CanBeNull] // null (I think) if assemblies do not match (different compiles) and hence a used item is not found in target reader.
            public Item ToItemWithTail(WorkingGraph readingGraph, AbstractDotNetAssemblyDependencyReader reader,
                                       int depth) {
                return reader.GetFullItemFor(readingGraph, this, depth);
            }

            public override bool Equals(object obj) {
                return EqualsRawAbstractItem(obj as RawUsedItem);
            }

            public override int GetHashCode() {
                return GetRawAbstractItemHashCode();
            }
        }

        [NotNull]
        protected abstract IEnumerable<RawUsingItem> ReadUsingItems(int depth, WorkingGraph readingGraph);

        [CanBeNull] // null (I think) if assemblies do not match (different compiles) and hence a used item is not found in target reader.
        protected Item GetFullItemFor(WorkingGraph readingGraph, RawUsedItem rawUsedItem, int depth) {
            if (_rawItems2Items == null) {
                _rawItems2Items = new Dictionary<RawUsedItem, Item>();
                foreach (var u in ReadUsingItems(depth + 1, readingGraph)) {
                    RawUsedItem usedItem = u.ToRawUsedItem();
                    _rawItems2Items[usedItem] = u.ToItem();
                }
            }
            Item result;
            _rawItems2Items.TryGetValue(rawUsedItem, out result);
            return result;
        }

        protected sealed class RawDependency {
            private readonly SequencePoint _sequencePoint;
            private readonly AbstractDotNetAssemblyDependencyReader _readerForUsedItem;

            public readonly RawUsingItem UsingItem;
            public readonly RawUsedItem UsedItem;
            public readonly DotNetUsage Usage;

            public RawDependency([NotNull] RawUsingItem usingItem, [NotNull] RawUsedItem usedItem,
                DotNetUsage usage, [CanBeNull] SequencePoint sequencePoint, AbstractDotNetAssemblyDependencyReader readerForUsedItem) {
                if (usingItem == null) {
                    throw new ArgumentNullException(nameof(usingItem));
                }
                if (usedItem == null) {
                    throw new ArgumentNullException(nameof(usedItem));
                }
                UsingItem = usingItem;
                UsedItem = usedItem;
                Usage = usage;
                _readerForUsedItem = readerForUsedItem;
                _sequencePoint = sequencePoint;
            }

            public override bool Equals(object obj) {
                var other = obj as RawDependency;
                return this == obj
                    || other != null
                        && Equals(other.UsedItem, UsedItem)
                        && Equals(other.UsingItem, UsingItem)
                        && Equals(other._sequencePoint, _sequencePoint);
            }

            public override int GetHashCode() {
                return UsedItem.GetHashCode() ^ UsingItem.GetHashCode();
            }

            [ExcludeFromCodeCoverage]
            public override string ToString() {
                return "RawDep " + UsingItem + "=>" + UsedItem;
            }

            [NotNull]
            private Dependency ToDependency(WorkingGraph readingGraph, Item usedItem, string containerUri) {
                return readingGraph.CreateDependency(UsingItem.ToItem(), usedItem, _sequencePoint == null
                            ? (ISourceLocation)new LocalSourceLocation(containerUri, UsingItem.NamespaceName + "." + UsingItem.ClassName + (string.IsNullOrWhiteSpace(UsingItem.MemberName) ? "" : "." + UsingItem.MemberName))
                            : new ProgramFileSourceLocation(containerUri, _sequencePoint.Document.Url, _sequencePoint.StartLine, _sequencePoint.StartColumn, _sequencePoint.EndLine, _sequencePoint.EndColumn),
                        Usage.ToString(), 1);
            }

            [NotNull]
            public Dependency ToDependencyWithTail(WorkingGraph readingGraph, int depth, string containerUri) {
                // ?? fires if reader == null (i.e., target assembly is not read in), or if assemblies do not match (different compiles) and hence a used item is not found in target reader.
                Item usedItem = (_readerForUsedItem == null ? null : UsedItem.ToItemWithTail(readingGraph, _readerForUsedItem, depth)) ?? UsedItem.ToItem();
                return ToDependency(readingGraph, usedItem, containerUri);
            }
        }

        [CanBeNull]
        protected ItemTail GetCustomSections(WorkingGraph readingGraph, Collection<CustomAttribute> customAttributes, [CanBeNull] ItemTail customSections) {
            ItemTail result = customSections;
            foreach (var customAttribute in customAttributes) {
                result = ExtractCustomSections(readingGraph, customAttribute, null) ?? result;
            }
            return result;
        }

        private ItemTail ExtractCustomSections(WorkingGraph readingGraph, CustomAttribute customAttribute, ItemTail parent) {
            TypeReference customAttributeTypeReference = customAttribute.AttributeType;
            TypeDefinition attributeType = Resolve(customAttributeTypeReference);
            bool isSectionAttribute = attributeType != null
                && attributeType.Interfaces.Any(i => i.FullName == "Archichect.ISectionAttribute");
            if (isSectionAttribute) {
                string[] keys = attributeType.Properties.Select(property => property.Name).ToArray();
                FieldDefinition itemTypeNameField = attributeType.Fields.FirstOrDefault(f => f.Name == "ITEM_TYPE");
                if (itemTypeNameField == null) {
                    //??? Log.WriteError();
                    throw new Exception("string constant ITEM_TYPE not defined in " + attributeType.FullName);
                } else {
                    string itemTypeName = "" + itemTypeNameField.Constant;
                    ItemType itemType = GetOrDeclareType(itemTypeName, Enumerable.Repeat("CUSTOM", keys.Length), keys.Select(k => "." + k));
                    var args = keys.Select((k, i) => new {
                        Key = k,
                        Index = i,
                        Property = customAttribute.Properties.FirstOrDefault(p => p.Name == k)
                    });
                    string[] values = args.Select(a => a.Property.Name == null
                        ? parent?.Values[a.Index] ?? ""
                        : "" + a.Property.Argument.Value).ToArray();
                    return ItemTail.New(readingGraph.ItemTailCache, itemType, values);
                }
            } else {
                return parent;
            }
        }

        [CanBeNull]
        protected TypeDefinition Resolve(TypeReference typeReference) {
            AssemblyNameReference assemblyNameRef = typeReference.Scope as AssemblyNameReference;            
            string assemblyFullName = assemblyNameRef?.FullName;
            if (assemblyFullName == null || _readingContext.UnresolvableAssemblies.Contains(assemblyFullName)) {
                return null;
            } else {
                try {
                    return typeReference.Resolve();
                } catch (AssemblyResolutionException ex) {
                    _readingContext.ExceptionCount++;
                    if (_readingContext.UnresolvableAssemblies.Add(assemblyFullName)) {
                        Log.WriteInfo("Cannot resolve " + typeReference + " - reason: " + ex.Message);
                    }
                    return null;
                } catch (Exception ex) {
                    _readingContext.ExceptionCount++;
                    Log.WriteWarning("Cannot resolve " + typeReference + " - reason: " + ex.Message);
                    return null;
                }
            }
        }

        private ItemType GetOrDeclareType(string itemTypeName, IEnumerable<string> keys, IEnumerable<string> subkeys) {
            return ReaderFactory.GetOrCreateDotNetType(itemTypeName,
                DotNetAssemblyDependencyReaderFactory.DOTNETITEM.Keys.Concat(keys).ToArray(),
                DotNetAssemblyDependencyReaderFactory.DOTNETITEM.SubKeys.Concat(subkeys).ToArray());
        }

        protected static void GetTypeInfo(TypeReference reference, out string namespaceName, out string className,
            out string assemblyName, out string assemblyVersion, out string assemblyCulture) {
            if (reference.DeclaringType != null) {
                string parentClassName, ignore1, ignore2, ignore3;
                GetTypeInfo(reference.DeclaringType, out namespaceName, out parentClassName, out ignore1, out ignore2, out ignore3);
                className = parentClassName + "/" + CleanClassName(reference.Name);
            } else {
                namespaceName = reference.Namespace;
                className = CleanClassName(reference.Name);
            }

            DotNetAssemblyDependencyReaderFactory.GetTypeAssemblyInfo(reference, out assemblyName, out assemblyVersion, out assemblyCulture);
        }

        private static string CleanClassName(string className) {
            if (!string.IsNullOrEmpty(className)) {
                className = className.TrimEnd('[', ']');
                int pos = className.LastIndexOf('`');
                if (pos > 0) {
                    className = className.Substring(0, pos);
                }
            }
            return className;
        }
    }
}