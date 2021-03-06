﻿using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace Archichect {
    public interface ITransformer : IPlugin {
        void Configure([NotNull] GlobalContext globalContext, [CanBeNull] string configureOptions, bool forceReload);

        int Transform([NotNull] GlobalContext globalContext, [NotNull] [ItemNotNull] IEnumerable<Dependency> dependencies,
            [CanBeNull] string transformOptionsString, [NotNull] List<Dependency> transformedDependencies,
            Func<string, IEnumerable<Dependency>> findOtherWorkingGraph);

        [NotNull]
        IEnumerable<Dependency> CreateSomeTestDependencies(WorkingGraph transformingGraph);
    }
}
