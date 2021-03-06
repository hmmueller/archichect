﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Archichect.Transforming.Modifying {
    public class ModifyItems : AbstractTransformerWithFileConfiguration<IEnumerable<ItemAction>,
        ModifyItems.ConfigureOptions, ModifyItems.TransformOptions> {
        public class ConfigureOptions {
            [NotNull, ItemNotNull]
            public IEnumerable<ItemAction> OrderedActions = new ItemAction[0];
        }

        public class TransformOptions {
            // empty
        }

        public static readonly Option ModificationsFileOption = new Option("mf", "modifications-file", "filename", "File containing modifications", @default: "");
        public static readonly Option ModificationsOption = new Option("ml", "modifications-list", "modifications", "Inline modifications", orElse: ModificationsFileOption);

        private static readonly Option[] _configOptions = { ModificationsFileOption, ModificationsOption };

        public override string GetHelp(bool detailedHelp, string filter) {
            string result = $@"Modify counts and markers on items.

Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: None";
            if (detailedHelp) {
                result += @"

Configuration format:

Configuration files support the standard options + for include,
// for comments, macro definitions (see -help files).

Item modifications always have the format

    incomingMatch -> itemMatch -- outgoingMatch => itemAction

Each part can be empty, therefore the simplest modification (which does
nothing) is -> -- => or, equivalently, ->--=>.

An item is modified if
* at least one incoming dependency matches incomingMatch; or incomingMatch is empty;
* the item matches itemMatch; or itemMatch is empty; and
* at least one outgoing dependency matches outgoingMatch; or outgoingMatch is empty.

The four parts have the following syntax:
    incomingMatch, outgoingMatch
        dependency pattern (see -help dependency)

    itemMatch
        item pattern (see -help itempattern)

    itemAction
        a space- or comma-separated list of:
            ignore (or nothing)           is ignored
            +marker                       add marker to item
            -marker                       remove marker from item
            - or delete                   delete item and all its incoming and
                                          outgoing dependences

Examples:
   'From -> -- 'To => +FromTo      Add marker FromTo to items where at least one
                                   incoming dependency has marker From and at least
                                   one outgoing dependency has marker To
   -- 'OnCycle -> -                Delete all items having marker OnCycle";
            }
            return result;
        }

        protected override ConfigureOptions CreateConfigureOptions([NotNull] GlobalContext globalContext,
            [CanBeNull] string configureOptionsString, bool forceReload) {
            var localVars = new ValuesFrame();

            var options = new ConfigureOptions();
            Option.Parse(globalContext, configureOptionsString,
                ModificationsFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing modifications filename"));
                    options.OrderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????", forceReload, localVars);
                    return j;
                }),
                ModificationsOption.Action((args, j) => {
                    options.OrderedActions = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader(string.Join(Environment.NewLine, args.Skip(j + 1))),
                        ModificationsOption.ShortName, globalContext.IgnoreCase, "????", forceReload: true, localVars:localVars);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
            return options;
        }

        protected override IEnumerable<ItemAction> CreateConfigurationFromText([NotNull] GlobalContext globalContext, 
            string fullConfigFileName, int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack,
            bool forceReloadConfiguration, Dictionary<string, string> configValueCollector, ValuesFrame localVars) {

            var actions = new List<ItemAction>();
            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack, 
                forceReloadConfiguration, onIncludedConfiguration: (e, n) => actions.AddRange(e),
                onLineWithLineNo: (line, lineNo) => {
                    actions.Add(new ItemAction(line.Trim(), ignoreCase, fullConfigFileName, startLineNo));
                    return null;
                }, configValueCollector: configValueCollector, localVars: localVars);
            return actions;
        }

        protected override TransformOptions CreateTransformOptions([NotNull] GlobalContext globalContext, 
            [CanBeNull] string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            return new TransformOptions();
        }

        public override int Transform([NotNull] GlobalContext globalContext, [NotNull] ConfigureOptions configureOptions,
            [NotNull] TransformOptions transformOptions, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [NotNull] List<Dependency> transformedDependencies) {

            if (!configureOptions.OrderedActions.Any()) {
                Log.WriteWarning($"No actions configured for {GetType().Name}");
            } else {

                Dictionary<Item, Dependency[]> items2incoming =
                    Item.CollectIncomingDependenciesMap(dependencies);
                Dictionary<Item, Dependency[]> items2outgoing =
                    Item.CollectOutgoingDependenciesMap(dependencies);

                var allItems = new HashSet<Item>(items2incoming.Keys.Concat(items2outgoing.Keys));

                foreach (var i in allItems.ToArray()) {
                    Dependency[] incoming;
                    items2incoming.TryGetValue(i, out incoming);
                    Dependency[] outgoing;
                    items2outgoing.TryGetValue(i, out outgoing);

                    ItemAction firstMatchingAction = configureOptions.OrderedActions.FirstOrDefault(a => a.Matches(incoming, i, outgoing));
                    if (firstMatchingAction == null) {
                        Log.WriteWarning("No match in actions for item " + i + " - item is deleted");
                        allItems.Remove(i);
                    } else {
                        if (!firstMatchingAction.Apply(i)) {
                            allItems.Remove(i);
                        }
                    }
                }

                transformedDependencies.AddRange(
                    dependencies.Where(d => allItems.Contains(d.UsingItem) && allItems.Contains(d.UsedItem)));
            }
            return Program.OK_RESULT;
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            var a = transformingGraph.CreateItem(ItemType.SIMPLE, "A");
            var b = transformingGraph.CreateItem(ItemType.SIMPLE, "B");
            return new[] {
                transformingGraph.CreateDependency(a, a, source: null, markers: "", ct:10, questionableCt:5, badCt:3, notOkReason: "test"),
                transformingGraph.CreateDependency(a, b, source: null, markers: "use+define", ct:1, questionableCt:0,badCt: 0),
                transformingGraph.CreateDependency(b, a, source: null, markers: "define", ct:5, questionableCt:0, badCt:2, notOkReason: "test"),
            };
        }
    }
}
