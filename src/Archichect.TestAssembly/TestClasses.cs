// (c) HMM�ller 2006...2010

using System;
using System.Diagnostics.CodeAnalysis;
using Archichect.TestAssembly.dir1.dir3;
using Archichect.TestAssembly.dirUEmlAEutSZ;
using Archichect.TestAssembly.dir�ml�ut�;

namespace Archichect.TestAssembly.dir1.dir3 {
    /// <summary>
    /// This and all other classes in and below
    /// the <c>dir1</c> namespace are used for testing
    /// the Archichect.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public struct Struct13 {
        public int I;
    }

    [ExcludeFromCodeCoverage]
    public class Class13A {
        public string S;
        public string[][] T;

        public Class13A(string t) {
            T[0][0] = t;
            S = "Archichect.TestAssembly.dir1.dir3.Class13d";
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class13B {
        public double D;
    }

    [ExcludeFromCodeCoverage]
    public class Class13C {
        public decimal M;

        public Class13C() {
            M = 1.5m * new Random().Next();
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class13D {
        #region Enum13GInner enum

        public enum Enum13GInner {
            Inner1,
            Inner2
        } ;

        #endregion

        private const Enum13GInner E = Enum13GInner.Inner1;
        public Delegate13 X;

        public Enum13GInner SomeValue() {
            return E;
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class13E {
        public char C;

        #region Nested type: Class13eInner

        public class Class13EInner {
            public bool B;

            public static int InnerClassMethod() {
                return 17;
            }

            #region Nested type: Class13eInnerInner

            public class Class13EInnerInner {
                public Class13D DeepDependency;
                public float F;
            }

            #endregion
        }

        #endregion
    }

    public delegate Class13E Delegate13(Class13D e);
}

[ExcludeFromCodeCoverage]
public class NamespacelessTestClassForArchichect {
    public static int I;

    static NamespacelessTestClassForArchichect() {
        I = new Random().Next();
    }
}

namespace Archichect.TestAssembly.dir1 {
    public interface ISomeInterface {
        Class13A Class13AMethod(Class13B b, ref Class13C c);
        Class13E.Class13EInner.Class13EInnerInner CreateInner3();
    }
}

namespace Archichect.TestAssembly.dir1.dir2 {
    public delegate Struct13 SomeDelegate(Class13A param);

    public enum SomeEnum {
        Enum1,
        Enum2,
        Enum3
    } ;

    // gibt es hier auch Abh�ngigkeiten, z.B. EnumConst = ...?

    [ExcludeFromCodeCoverage]
    public struct SomeStruct {
        public Class13B InstVar;
        public bool B;

        public SomeStruct(Delegate13 d) {
            var auxVar = new Class13C();
            InstVar = new Class13B();
            B = auxVar.M == 123.456m || d == null;
        }

        internal static Class13E Method(Delegate13 x) {
            Class13E auxVar = x(new Class13D());
            const Class13D.Enum13GInner e = Class13D.Enum13GInner.Inner1;
            Console.Out.WriteLine(e);
            return auxVar;
        }
    }

    [ExcludeFromCodeCoverage]
    public class SomeClass {
        public int I;

        public SomeClass(int i) {
            var auxVar = new Class13D();
            I = auxVar.SomeValue() == Class13D.Enum13GInner.Inner2 ? i : -i;
        }

        internal static char SomeMethod() {
            var auxVar = new Class13E();
            bool b = new Class13E.Class13EInner().B;
            return b ? auxVar.C : '�';
        }

        public event EventHandler<EventArgs> SomeEvent;

        public void InvokeSomeEvent(EventArgs e) {
            EventHandler<EventArgs> handler = SomeEvent;
            handler?.Invoke(this, e);
        }

        protected static void OtherMethod(Class13D[] d) {
            var auxVar = new Class13A("Archichect.TestAssembly.dir1.dir3.SomeClass");
            if (auxVar.S == auxVar.T[0][0]) {
                SomeStruct.Method(d[0].X);
            }
        }

        protected internal static int AnotherMethod(Class13D d) {
            // ReSharper disable once ObjectCreationAsStatement
            new Class13A("Archichect.TestAssembly.dir1.dir3.SomeClass");
            return NamespacelessTestClassForArchichect.I;
        }

        public ISomeInterface YetAnotherMethod(ISomeInterface s) {
            return s;
        }

        public void SomeSpecialMethod1() {
            SomeSpecialMethod2();
        }

        public void SomeSpecialMethod2() {
            YetAnotherMethod(null);
        }
    }

    public abstract class AbstractImplementingClass : ISomeInterface {
        public abstract int IntMethod();

        public Class13A Class13AMethod(Class13B b, Class13C c) {
            throw new NotImplementedException();
        }

        public Class13E.Class13EInner.Class13EInnerInner CreateInner3() {
            throw new NotImplementedException();
        }

        char ISomeInterface.CharProperty { get; set; }
    }

    internal class ImplementingClass1 : AbstractImplementingClass {
        public override int IntMethod() {
            throw new NotImplementedException();
        }
    }
    internal class ImplementingClass2 : AbstractImplementingClass {
        private readonly AbstractImplementingClass _delegate;

        public ImplementingClass2(AbstractImplementingClass @delegate) {
            _delegate = @delegate;
        }

        public override int IntMethod() {
            return _delegate.IntMethod();
        }
    }
}

namespace Archichect.TestAssembly.dir1.dir4 {
    [ExcludeFromCodeCoverage]
    public class Class14 {
        #region Nested type: Class13eInner2

        public class Class13EInner2 {
            public int InnerClassMethod() {
                return Class13E.Class13EInner.InnerClassMethod();
            }

            public static void SpecialMethodOfInnerClass() {
                SomeSpecialMethod1();
                ExtraordinaryMethod();
            }
        }

        #endregion

        public static void SomeSpecialMethod1() {
            Class13EInner2.SpecialMethodOfInnerClass();
        }

        public static void ExtraordinaryMethod() {
            Class13EInner2.SpecialMethodOfInnerClass();
        }
    }
}

namespace Archichect.TestAssembly.dir�ml�ut {
    [ExcludeFromCodeCoverage]
    public class Class14 {
        public int Method() {
            return ClassA.M�thod() + Class�.Method();
        }
    }
}

namespace Archichect.TestAssembly.dir�ml�ut� {
    [ExcludeFromCodeCoverage]
    public class ClassA {
        public static int M�thod() {
            return 0;
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class� {
        public static int Method() {
            return 1;
        }
    }
}

namespace Archichect.TestAssembly.dirUEmlAEut {
    [ExcludeFromCodeCoverage]
    public class Class14A {
        public int Method() {
            return ClassA.M�thod() + Class�.Method();
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class14B {
        public int Method() {
            return ClassAA.M�thod() + Class�.Method();
        }
    }
}

namespace Archichect.TestAssembly.dirUEmlAEutSZ {
    [ExcludeFromCodeCoverage]
    public class ClassAA {
        public static int M�thod() {
            return 0;
        }
    }

    [ExcludeFromCodeCoverage]
    public class ClassAE {
        public static int Method() {
            return 1;
        }
    }
}

namespace Archichect.TestAssembly.dirumlaut {
    [ExcludeFromCodeCoverage]
    public class Class14 {
        public int Method() {
            return dirumlauts.ClassA.M�thod() + dirumlauts.Class�.Method();
        }
    }
}

namespace Archichect.TestAssembly.dirumlauts {
    [ExcludeFromCodeCoverage]
    public class ClassA {
        public static int M�thod() {
            return 0;
        }
    }

    [ExcludeFromCodeCoverage]
    public class Class� {
        public static int Method() {
            return 1;
        }
    }
}