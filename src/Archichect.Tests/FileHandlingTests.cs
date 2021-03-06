﻿using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using Archichect.TestAssembly.dir1.dir3;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Archichect.TestAssemblyÄÖÜß.dir1.dir3;
using Archichect.Transforming.ViolationChecking;

namespace Archichect.Tests {
    [TestClass, ExcludeFromCodeCoverage]
    public class FileHandlingTests {
        private readonly Random _rnd = new Random();
        private string _basePath;

        public static Struct13 DummyForAssemblyCopying;
        public static Test2 DummyForAssembly2Copying;

        // + relativer Pfad zu File, wo's gefunden wurde. DLL/EXE muss woanders stehen!

        [TestInitialize]
        public void TestSetup() {
            _basePath = Path.Combine(Path.GetTempPath(), _rnd.Next(0, 100000).ToString());
        }

        [TestCleanup]
        public void TestCleanup() {
            Directory.Delete(_basePath, true);
        }

        [TestMethod]
        public void TestDOk() {
            WriteDep1To(@"a\b");
            WriteDep2To(@"a\c");
            Assert.AreEqual(Program.OK_RESULT, Run($@"{Program.ConfigureOption} CheckDeps {{ {CheckDeps.RuleRootDirectoryOption} %%\a }}".Split(' ')));
        }

        [TestMethod]
        public void TestDDOk() {
            WriteDep1To(@"a\b\x\y");
            WriteDep2To(@"a\c\x\y\z");
            Option rr = CheckDeps.RuleRootDirectoryOption;
            Assert.AreEqual(Program.OK_RESULT, Run(
                $@"{Program.ConfigureOption} CheckDeps {{ {rr} %%\a\b {rr} %%\a\x {rr} %%\a\c }}".Split(' ')));
        }

        [TestMethod]
        public void TestDPlusOk() {
            WriteDep1PlusTo(@"a\b");
            WriteDep2PlusTo(@"a\b\c");
            Option rr = CheckDeps.RuleRootDirectoryOption;
            Assert.AreEqual(Program.OK_RESULT, Run($@"{Program.ConfigureOption} CheckDeps {{ {rr} %%\a\b }}".Split(' ')));
        }

        [TestMethod]
        public void TestDoublyNestedDefine() {
            Write(@"a\b", "A.dep",
                @"_A := Archichect.TestAssembly
                ");
            Write(@"a\b", "B.dep",
                @"+ A.dep
                _B := _A
                ");
            Write(@"a\b\c", "Archichect.TestAssembly.dll.dep",
                @"+ ..\B.dep
                  $ DOTNETITEM ---> DOTNETITEM
                  _B.** ---> **
                  :* ---? **

                  $ DOTNETASSEMBLY ---> DOTNETASSEMBLY 
                  * ---> *
                ");
            WriteDep2To(@"a\b\c");
            Option rr = CheckDeps.RuleRootDirectoryOption;
            Assert.AreEqual(Program.OK_RESULT, Run($@"{Program.ConfigureOption} CheckDeps {{ {rr} %%\a\b }}".Split(' ')));
        }

        [TestMethod]
        public void TestPushDownDefine() {
            Write(@"a\b", "A.dep",
                @"_A := Archichect.TestAssembly
                  + B.dep
                ");
            Write(@"a\b", "B.dep",
                @"$ DOTNETITEM ---> DOTNETITEM
                  _A.** ---> **
                  :* ---? **
                ");
            Write(@"a\b\c", "Archichect.TestAssembly.dll.dep",
                @"+ ..\A.dep
                ");
            WriteDep2To(@"a\b\c");
            Option rr = CheckDeps.RuleRootDirectoryOption;
            Assert.AreEqual(Program.OK_RESULT, Run($@"{Program.ConfigureOption} CheckDeps {{ {rr} %%\a\b }}".Split(' ')));
        }

        [TestMethod]
        public void TestExcept() {
            WriteDep1To(@"a\b");
            WriteDefaultSetTo(@"a\c");

            int result = Program.Main(new List<string> {
                    $"{Program.ReadOption}=" + GetPath("Archichect.TestAssembly.dll"),
                    $"{Program.ReadOption}=" + "Archichect.TestAssemblyÄÖÜß.*.dll", "-", "Archichect.TestAssemblyÄÖÜß.dll",
                    $"{Program.ConfigureOption}=", "CheckDeps", "{", CheckDeps.RuleRootDirectoryOption.Opt + "=" + _basePath + @"\a", "}"
            }.ToArray());
            Assert.AreEqual(Program.OK_RESULT, result);
        }

        private int Run(params string[] args) {
            return Program.Main(new List<string>(args.Select(s => s.Replace("%%", _basePath))) {
                    GetPath("Archichect.TestAssembly.dll"),
                    "Archichect.TestAssemblyÄÖÜß.*.dll"
                }.ToArray());
        }

        private void WriteDep1To(string directory) {
            Write(directory, "Archichect.TestAssembly.dll.dep",
                @"
                  $ DOTNETITEM ---> DOTNETITEM
                  Archichect.TestAssembly.** ---> **
                  :* ---? **

                  $ DOTNETASSEMBLY ---> DOTNETASSEMBLY
                  ** ---? **
                ");
        }

        private void WriteDep1PlusTo(string directory) {
            Write(directory, "Archichect.TestAssembly.dll.dep", @"+ Dep1Include\Dep1.dep");
            Write(directory + @"\Dep1Include", "Dep1.dep", @"
                  $ DOTNETITEM ---> DOTNETITEM
                  Archichect.TestAssembly.** ---> **
                  :* ---? **

                  $ DOTNETASSEMBLY ---> DOTNETASSEMBLY
                  ** ---? **
                ");
        }

        private void WriteDep2To(string directory) {
            Write(directory, "Archichect.TestAssemblyÄÖÜß.dll.dep",
                @"$ DOTNETITEM ---> DOTNETITEM
                  Archichect.TestAssemblyÄÖÜß.** ---> **

                  $ DOTNETASSEMBLY ---> DOTNETASSEMBLY
                  ** ---? **
                ");
        }

        private void WriteDep2PlusTo(string directory) {
            Write(directory, "Archichect.TestAssemblyÄÖÜß.dll.dep", @"+ Dep2Include\Dep2A.dep");
            Write(directory + @"\Dep2Include", "Dep2A.dep", @"+ ..\Dep2B.dep");
            Write(directory, "Dep2B.dep",
                @"$ DOTNETITEM ---> DOTNETITEM

                Archichect.TestAssemblyÄÖÜß.** ---> **
                Assembly.Name=** ---> Assembly.Name=**");
        }

        private void WriteDefaultSetTo(string directory) {
            Write(directory, "Defaults.dep", @"
                $ DOTNETITEM ---> DOTNETITEM
                ** ---? **

                $ DOTNETASSEMBLY ---> DOTNETASSEMBLY
                * ---> *");
        }

        private void Write(string directory, string ruleFileName, string data) {
            DirectoryInfo di = Directory.CreateDirectory(Path.Combine(_basePath, directory));
            using (TextWriter tw = new StreamWriter(Path.Combine(di.FullName, ruleFileName), false, Encoding.UTF8)) {
                tw.WriteLine(data);
            }
        }

        private string GetPath(string assembly) {
            // ReSharper disable AssignNullToNotNullAttribute - always works in Test
            return Path.Combine(Path.GetDirectoryName(typeof(MainTests).Assembly.Location), assembly);
            // ReSharper restore AssignNullToNotNullAttribute
        }
    }
}
