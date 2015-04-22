using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using TestHelper;
using SimpleObjectInitializerAnalyzer;

namespace SimpleObjectInitializerAnalyzer.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        class Foo { public int Prop { get; set; } }
        //No diagnostics expected to show up
        [TestMethod]
        public void EmptyCodeShouldNotTriggerDiagnostic()
        {
            var f = new Foo() { Prop = 1 };
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        //Diagnostic and CodeFix both triggered and checked for
        [TestMethod]
        public void SimpleDeclarationWithInitializerShouldBeFixed()
        {
            var test = @"
class Foo
{
    public string Bar { get; set; }
    public Foo()
    {
        var f = new Foo() { Bar = string.Empty } 
    }
}";
            var expected = new DiagnosticResult
            {
                Id = SimpleObjectInitializerAnalyzer.DiagnosticId,
                Message = "I hate OI",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 27)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
class Foo
{
    public string Bar { get; set; }
    public Foo()
    {
        var f = new Foo();
        f.Bar = string.Empty;
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void SimpleDeclarationWithoutParensAndWithInitializerShouldBeFixed()
        {
            var test = @"
class Foo
{
    public string Bar { get; set; }
    public Foo()
    {
        var f = new Foo { Bar = string.Empty } 
    }
}";
            var expected = new DiagnosticResult
            {
                Id = SimpleObjectInitializerAnalyzer.DiagnosticId,
                Message = "I hate OI",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 7, 25)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

            var fixtest = @"
class Foo
{
    public string Bar { get; set; }
    public Foo()
    {
        var f = new Foo();
        f.Bar = string.Empty;
    }
}";
            VerifyCSharpFix(test, fixtest);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new SimpleObjectInitializerAnalyzerCodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new SimpleObjectInitializerAnalyzer();
        }
    }
}