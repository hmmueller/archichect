using System;
using System.Collections.Generic;
using Archichect.Reading.DipReading;
using JetBrains.Annotations;

namespace Archichect.Reading {
    public abstract class AbstractDependencyReader : IDependencyReader {
        protected AbstractDependencyReader([NotNull] string fullFileName, string containerUri) {
            if (string.IsNullOrWhiteSpace(fullFileName)) {
                throw new ArgumentException("fileName must be non-empty", nameof(fullFileName));
            }
            FullFileName = fullFileName;
            ContainerUri = containerUri;
        }

        [NotNull]
        public string FullFileName { get; }

        [NotNull]
        protected string ContainerUri { get; }

        [NotNull]
        public abstract IEnumerable<Dependency> ReadDependencies(WorkingGraph readingGraph, int depth, bool ignoreCase);
    }
}