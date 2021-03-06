﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JetBrains.Annotations;

namespace Archichect.Transforming.ViolationChecking {
    public class CheckDeps
        : AbstractTransformerPerContainerUriWithFileConfiguration<DependencyRuleSet, CheckDeps.ConfigureOptions, CheckDeps.TransformOptions> {
        public class ConfigureOptions {
            [NotNull, ItemNotNull]
            public readonly List<DirectoryInfo> SearchRootsForRuleFiles = new List<DirectoryInfo>();
            [NotNull]
            public string RuleFileExtension = ".dep";
            [CanBeNull]
            public DependencyRuleSet DefaultRuleSet;
            [NotNull]
            internal ValuesFrame LocalVars = new ValuesFrame();
        }

        public class TransformOptions {
            public bool ShowUnusedQuestionableRules;
            public bool ShowUnusedRules;
            public bool AddMarker;
        }

        public static readonly Option RuleFileExtensionOption = new Option("re", "rule-extension", "extension", "extension for rule files", @default: ".dep");
        public static readonly Option RuleRootDirectoryOption = new Option("rr", "rule-rootdirectory", "directory", "search directory for rule files (searched recursively)", @default: "no search for rule files", multiple: true);
        public static readonly Option DefaultRuleFileOption = new Option("rf", "rule-defaultfile", "filename", "default rule file", @default: "no default rule file");
        public static readonly Option DefaultRulesOption = new Option("rd", "rule-defaults", "rules", "default rules", @default: "no default rules");

        private static readonly Option[] _configOptions = { RuleFileExtensionOption, RuleRootDirectoryOption, DefaultRuleFileOption, DefaultRulesOption };

        public static readonly Option ShowUnusedQuestionableRulesOption = new Option("sq", "show-unused-questionable", "", "Show unused questionable rules", @default: false);
        public static readonly Option ShowAllUnusedRulesOption = new Option("su", "show-unused-rules", "", "Show all unused rules", @default: false);
        public static readonly Option AddMarkersForBadGroups = new Option("ag", "add-group-marker", "", "Add a marker for each group that marks the dependency as bad", @default: false);

        private static readonly Option[] _transformOptions = { ShowUnusedQuestionableRulesOption, ShowAllUnusedRulesOption, AddMarkersForBadGroups };

        HashSet<DependencyRuleGroup> _allCheckedGroups;

        public override string GetHelp(bool detailedHelp, string filter) {
            return
$@"  Compute dependency violations against defined rule sets.
    
Configuration options: {Option.CreateHelp(_configOptions, detailedHelp, filter)}

Transformer options: {Option.CreateHelp(_transformOptions, detailedHelp, filter)}";
        }

        #region Configure

        internal const string MAY_USE_RECURSIVE = "===>";
        internal const string MAY_USE = "--->";

        internal const string MAY_USE_TAIL = "->";
        internal const string MAY_USE_WITH_WARNING_TAIL = "-?";
        internal const string MUST_NOT_USE_TAIL = "-!";

        protected override ConfigureOptions CreateConfigureOptions(GlobalContext globalContext,
            string configureOptionsString, bool forceReload) {
            var options = new ConfigureOptions();
            Option.Parse(globalContext, configureOptionsString,
                RuleFileExtensionOption.Action((args, j) => {
                    options.RuleFileExtension = '.' + Option.ExtractRequiredOptionValue(args, ref j, "missing extension").TrimStart('.');
                    return j;
                }),
                RuleRootDirectoryOption.Action((args, j) => {
                    options.SearchRootsForRuleFiles.Add(new DirectoryInfo(Option.ExtractRequiredOptionValue(args, ref j, "missing rule-search root directory")));
                    return j;
                }),
                DefaultRuleFileOption.Action((args, j) => {
                    string fullSourceName = Path.GetFullPath(Option.ExtractRequiredOptionValue(args, ref j, "missing default rules filename"));
                    options.DefaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StreamReader(fullSourceName), fullSourceName, globalContext.IgnoreCase, "????", forceReload, options.LocalVars);
                    return j;
                }),
                DefaultRulesOption.Action((args, j) => {
                    options.DefaultRuleSet = GetOrReadChildConfiguration(globalContext,
                        () => new StringReader(string.Join(Environment.NewLine, args.Skip(j + 1))),
                        DefaultRulesOption.ShortName, globalContext.IgnoreCase, "????", forceReload: true, localVars: options.LocalVars);
                    // ... and all args are read in, so the next arg index is past every argument.
                    return int.MaxValue;
                })
            );
            return options;
        }

        protected override DependencyRuleSet CreateConfigurationFromText([NotNull] GlobalContext globalContext,
            string fullConfigFileName, int startLineNo, TextReader tr, bool ignoreCase, string fileIncludeStack,
            bool forceReloadConfiguration, Dictionary<string, string> configValueCollector, ValuesFrame localVars) {

            ItemType usingItemType = null;
            ItemType usedItemType = null;

            string ruleSourceName = fullConfigFileName;

            string previousRawUsingPattern = "";

            var ruleGroups = new List<DependencyRuleGroup>();
            var children = new List<DependencyRuleSet>();

            DependencyRuleGroup mainRuleGroup = new DependencyRuleGroup("", globalContext.IgnoreCase, null, null, "global rule group");
            DependencyRuleGroup currentGroup = mainRuleGroup;
            ruleGroups.Add(currentGroup);

            ProcessTextInner(globalContext, fullConfigFileName, startLineNo, tr, ignoreCase, fileIncludeStack,
                forceReloadConfiguration,
                onIncludedConfiguration: (e, n) => children.Add(e),
                onLineWithLineNo: (line, lineNo) => {
                    if (line.StartsWith("$")) {
                        if (currentGroup != null && !currentGroup.IsGlobalGroup) {
                            return "$ inside '{{ ... }}' not allowed";
                        } else {
                            string typeLine = line.Substring(1).Trim();
                            int i = typeLine.IndexOf(MAY_USE, StringComparison.Ordinal);
                            if (i < 0) {
                                Log.WriteError($"$-line '{line}' must contain " + MAY_USE, ruleSourceName, lineNo);
                                throw new ApplicationException($"$-line '{line}' must contain " + MAY_USE_TAIL);
                            }
                            usingItemType = ItemType.New(typeLine.Substring(0, i).Trim(), globalContext.IgnoreCase);
                            usedItemType = ItemType.New(typeLine.Substring(i + MAY_USE.Length).Trim(), globalContext.IgnoreCase);
                            return null;
                        }
                    } else if (line.EndsWith("{")) {
                        if (currentGroup == null || usingItemType == null) {
                            return $"Itemtypes not defined - $ line is missing in {ruleSourceName}, dependency rules are ignored";
                        } else if (!currentGroup.IsGlobalGroup) {
                            return "Nested '{{ ... {{' not possible";
                        } else {
                            string groupPattern = line.TrimEnd('{').Trim();
                            currentGroup = new DependencyRuleGroup(groupPattern, globalContext.IgnoreCase, usingItemType, usedItemType, ruleSourceName + "_" + lineNo);
                            ruleGroups.Add(currentGroup);
                            return null;
                        }
                    } else if (line == "}") {
                        if (currentGroup != null && !currentGroup.IsGlobalGroup) {
                            currentGroup = mainRuleGroup;
                            return null;
                        } else {
                            return "'}}' without corresponding '... {{'";
                        }
                    } else {
                        string currentRawUsingPattern;
                        bool ok = currentGroup.AddDependencyRules(usingItemType, usedItemType, ruleSourceName,
                                lineNo, line, ignoreCase, previousRawUsingPattern, out currentRawUsingPattern);
                        if (!ok) {
                            return "Could not add dependency rule";
                        } else {
                            previousRawUsingPattern = currentRawUsingPattern;
                            return null;
                        }
                    }
                }, configValueCollector: configValueCollector, localVars: localVars);
            return new DependencyRuleSet(ruleGroups, children);
        }

        #endregion Configure

        #region Transform

        private int _allFilesCt, _okFilesCt;
        private Dictionary<string, int> _matchesByGroup;

        protected override TransformOptions CreateTransformOptions(GlobalContext globalContext,
            string transformOptionsString, Func<string, IEnumerable<Dependency>> findOtherWorkingGraph) {
            var options = new TransformOptions();
            options.ShowUnusedQuestionableRules = options.ShowUnusedRules = options.AddMarker = false;

            Option.Parse(globalContext, transformOptionsString,
                ShowUnusedQuestionableRulesOption.Action((args, j) => {
                    options.ShowUnusedQuestionableRules = true;
                    return j;
                }), ShowAllUnusedRulesOption.Action((args, j) => {
                    options.ShowUnusedRules = true;
                    return j;
                }), AddMarkersForBadGroups.Action((args, j) => {
                    options.AddMarker = true;
                    return j;
                }));

            return options;
        }

        public override void BeforeAllTransforms([NotNull] GlobalContext globalContext, [NotNull] ConfigureOptions configureOptions,
            [NotNull] TransformOptions transformOptions, [NotNull, ItemNotNull] IEnumerable<string> containerNames) {
            _allFilesCt = _okFilesCt = 0;
            _allCheckedGroups = new HashSet<DependencyRuleGroup>();

            _matchesByGroup = new Dictionary<string, int>();

            Log.WriteInfo("Checking dependencies from " + string.Join(", ", containerNames));
        }

        public override int TransformContainer([NotNull] GlobalContext globalContext,
            [NotNull] ConfigureOptions configureOptions, [NotNull] TransformOptions transformOptions,
            [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies, [CanBeNull] string containerName,
            [NotNull] List<Dependency> transformedDependencies) {

            transformedDependencies.AddRange(dependencies);

            var fullRuleFileNames = new List<string>();
            foreach (var root in configureOptions.SearchRootsForRuleFiles) {
                try {
                    fullRuleFileNames.AddRange(
                        root.GetFiles(Path.GetFileName(containerName) + configureOptions.RuleFileExtension,
                            SearchOption.AllDirectories).Select(fi => fi.FullName));
                } catch (IOException ex) {
                    Log.WriteWarning($"Cannot access files in {root} ({ex.Message})");
                }
            }

            fullRuleFileNames = fullRuleFileNames.Distinct().ToList();

            if (!fullRuleFileNames.Any() && containerName != null) {
                fullRuleFileNames = new List<string> { Path.GetFullPath(containerName) + configureOptions.RuleFileExtension };
            }

            DependencyRuleSet ruleSetForAssembly;
            if (fullRuleFileNames.Count > 1) {
                string allFilenames = string.Join(", ", fullRuleFileNames.Select(fi => $"'{fi}'"));
                throw new ApplicationException(
                    $"More than one dependency rule file found for input file {containerName}" + 
                    $"{InAndBelow(configureOptions.SearchRootsForRuleFiles)}: {allFilenames}");
            } else if (!fullRuleFileNames.Any()) {
                ruleSetForAssembly = null;
            } else {
                string fullRuleFileName = fullRuleFileNames[0];
                ruleSetForAssembly = File.Exists(fullRuleFileName)
                    ? GetOrReadChildConfiguration(globalContext, () => new StreamReader(fullRuleFileName),
                        fullRuleFileName, globalContext.IgnoreCase, "...", forceReload: false, localVars: configureOptions.LocalVars)
                    : null;
            }

            // Nothing found - we take the default set.
            if (ruleSetForAssembly == null) {
                if (configureOptions.DefaultRuleSet == null) {
                    throw new ApplicationException(
                        $"No dependency rule file found for input file {containerName}" +
                        $"{InAndBelow(configureOptions.SearchRootsForRuleFiles)}, and no default rules provided");
                } else {
                    ruleSetForAssembly = configureOptions.DefaultRuleSet;
                }
            }

            // TODO: !!!!!!!!!!!!!!!!!! How to reset all "unused counts"? 
            // (a) remember counts before and check after - but how to find all checked rules???
            // (b) reset counts in all rules (that are read in)
            // (c) keep a callback list of checked rules ...

            return CheckDependencies(globalContext, transformOptions, dependencies, containerName, ruleSetForAssembly);
        }

        private static string InAndBelow(List<DirectoryInfo> searchRootsForRuleFiles) {
            return searchRootsForRuleFiles.Any() 
                ? " in and below " + string.Join(", ", searchRootsForRuleFiles) 
                : "";
        }

        private int CheckDependencies([NotNull] GlobalContext globalContext, [NotNull] TransformOptions transformOptions,
                [NotNull, ItemNotNull] IEnumerable<Dependency> dependencies,
                [CanBeNull] string containerName, [CanBeNull] DependencyRuleSet ruleSetForAssembly) {
            if (!dependencies.Any()) {
                return Program.OK_RESULT;
            }

            if (ruleSetForAssembly == null) {
                Log.WriteError("No rule set found for checking " + containerName);
                return Program.NO_RULE_SET_FOUND_FOR_FILE;
            }

            DependencyRuleGroup[] checkedGroups = ruleSetForAssembly.GetAllDependencyGroupsWithRules(globalContext.IgnoreCase).ToArray();
            if (checkedGroups.Any()) {
                int badCount = 0;
                int questionableCount = 0;

                foreach (var group in checkedGroups) {
                    int matchCount = group.Check(dependencies, transformOptions.AddMarker, ref badCount, ref questionableCount);

                    if (!_matchesByGroup.ContainsKey(group.GroupName)) {
                        _matchesByGroup[group.GroupName] = matchCount;
                    } else {
                        _matchesByGroup[group.GroupName] += matchCount;
                    }
                }
                _allCheckedGroups.UnionWith(checkedGroups);

                if (Log.IsVerboseEnabled) {
                    string msg =
                        $"{containerName}: {badCount} bad dependencies, {questionableCount} questionable dependecies";
                    if (badCount > 0) {
                        Log.WriteError(msg);
                    } else if (questionableCount > 0) {
                        Log.WriteWarning(msg);
                    }
                }

                _allFilesCt++;
                if (badCount > 0) {
                    return Program.DEPENDENCIES_NOT_OK;
                } else {
                    _okFilesCt++;
                    return Program.OK_RESULT;
                }
            } else {
                Log.WriteInfo("No rule groups found for " + containerName + " - no dependency checking is done for its dependencies");
                return Program.NO_RULE_GROUPS_FOUND;
            }
        }

        public override IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph) {
            throw new NotImplementedException();
        }

        public override void AfterAllTransforms([NotNull] GlobalContext globalContext,
            [NotNull]ConfigureOptions configureOptions, [NotNull] TransformOptions transformOptions) {
            foreach (var kvp in _matchesByGroup.OrderBy(kvp => kvp.Key)) {
                Log.WriteInfo($"Checked {kvp.Value} dependencies matching group {kvp.Key}");
            }

            foreach (var r in _allCheckedGroups.SelectMany(g => g.AllRules)
                                               .Select(r => r.Source)
                                               .Distinct()
                                               .OrderBy(r => r.RuleSourceName)
                                               .ThenBy(r => r.LineNo)) {
                if (transformOptions.ShowUnusedQuestionableRules && r.IsQuestionableRule && !r.WasHit) {
                    Log.WriteInfo("Questionable rule " + r + " was never matched - maybe you can remove it!");
                } else if (transformOptions.ShowUnusedRules && !r.WasHit) {
                    Log.WriteInfo("Rule " + r + " was never matched - maybe you can remove it!");
                } else {
                    if (Log.IsChattyEnabled) {
                        Log.WriteInfo("Rule " + r + " was hit " + r.HitCount + " times.");
                    }
                }
            }

            if (_allFilesCt == 1) {
                Log.WriteInfo(_okFilesCt == 1 ? "Input file is without violations." : "Input file has violations.");
            } else if (_okFilesCt == _allFilesCt) {
                Log.WriteInfo($"All {_okFilesCt} input files are without violations.");
            } else if (_okFilesCt == 0) {
                Log.WriteInfo($"All {_allFilesCt} input files have violations.");
            } else {
                Log.WriteInfo($"{_allFilesCt - _okFilesCt} input files have violations, {_okFilesCt} are without violations.");
            }
        }

        #endregion Transform
    }
}
