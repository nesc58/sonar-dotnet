/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2022 SonarSource SA
 * mailto: contact AT sonarsource DOT com
 *
 * This program is free software; you can redistribute it and/or
 * modify it under the terms of the GNU Lesser General Public
 * License as published by the Free Software Foundation; either
 * version 3 of the License, or (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the GNU
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program; if not, write to the Free Software Foundation,
 * Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
 */

using Microsoft.CodeAnalysis.Operations;
using SonarAnalyzer.SymbolicExecution.Constraints;
using SonarAnalyzer.SymbolicExecution.Roslyn;
using SonarAnalyzer.UnitTest.TestFramework.SymbolicExecution;

namespace SonarAnalyzer.UnitTest.SymbolicExecution.Roslyn
{
    public partial class RoslynSymbolicExecutionTest
    {
        [TestMethod]
        public void Branching_BlockProcessingOrder_CS()
        {
            const string code = @"
Tag(""Entry"");
if (Condition)
{
    Tag(""BeforeTry"");
    try
    {
        Tag(""InTry"");
    }
    catch
    {
        Tag(""InCatch"");
    }
    finally
    {
        Tag(""InFinally"");
    }
    Tag(""AfterFinally"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "Entry",
                "BeforeTry",        // Dequeue for "if" branch
                "Else",             // Dequeue for "else" branch
                "InTry",            // Dequeue after "if" branch
                "End",              // Dequeue after "else" branch, reaching exit block
                "InCatch",          // Dequeue after the "try body" that could throw
                "InFinally",        // Dequeue after the "try body"
                "InFinally",        // Dequeue after the "catch", with Exception
                "AfterFinally");    // Dequeue after "if" branch
        }

        [TestMethod]
        public void Branching_BlockProcessingOrder_VB()
        {
            const string code = @"
Tag(""Entry"")
If Condition Then
    Tag(""BeforeTry"")
    Try
        Tag(""InTry"")
    Catch
        Tag(""InCatch"")
    Finally
        Tag(""InFinally"")
    End Try
    Tag(""AfterFinally"")
Else
    Tag(""Else"")
End If
Tag(""End"")";
            SETestContext.CreateVB(code).Validator.ValidateTagOrder(
                "Entry",
                "BeforeTry",
                "Else",
                "InTry",
                "End",
                "InCatch",
                "InFinally",
                "InFinally",    // With Exception
                "AfterFinally");
        }

        [TestMethod]
        public void Branching_PersistSymbols_BetweenBlocks()
        {
            const string code = @"
var first = true;
var second = false;
if (boolParameter)
{
    var tag = ""If"";
}
else
{
    var tag = ""Else"";
}";
            var validator = SETestContext.CreateCS(code).Validator;
            validator.ValidateTag("If", "first", x => x.HasConstraint(BoolConstraint.True).Should().BeTrue());
            validator.ValidateTag("If", "second", x => x.HasConstraint(BoolConstraint.False).Should().BeTrue());
            validator.ValidateTag("Else", "first", x => x.HasConstraint(BoolConstraint.True).Should().BeTrue());
            validator.ValidateTag("Else", "second", x => x.HasConstraint(BoolConstraint.False).Should().BeTrue());
        }

        [TestMethod]
        public void EndNotifications_SimpleFlow()
        {
            var validator = SETestContext.CreateCS("var a = true;").Validator;
            validator.ValidateExitReachCount(1);
            validator.ValidateExecutionCompleted();
        }

        [TestMethod]
        public void EndNotifications_MaxStepCountReached()
        {
            // var x = true; produces 3 operations
            var code = Enumerable.Range(1, RoslynSymbolicExecution.MaxStepCount / 3 + 1).Select(x => $"var x{x} = true;").JoinStr(Environment.NewLine);
            var validator = SETestContext.CreateCS(code).Validator;
            validator.ValidateExitReachCount(0);
            validator.ValidateExecutionNotCompleted();
        }

        [TestMethod]
        public void EndNotifications_MultipleBranches()
        {
            const string method = @"
public int Method(bool a)
{
    if (a)
        return 1;
    else
        return 2;
}";
            var validator = SETestContext.CreateCSMethod(method).Validator;
            validator.ValidateExitReachCount(1);
            validator.ValidateExecutionCompleted();
        }

        [TestMethod]
        public void EndNotifications_Throw()
        {
            const string method = @"
public int Method(bool a)
{
    if (a)
        throw new System.NullReferenceException();
    else
        return 2;
}";
            var validator = SETestContext.CreateCSMethod(method).Validator;
            validator.ValidateExitReachCount(2);
            validator.ValidateExecutionCompleted();
            validator.ExitStates.Should().HaveCount(2)
                .And.ContainSingle(x => HasNoException(x))
                .And.ContainSingle(x => HasExceptionOfType(x, "NullReferenceException"));
        }

        [TestMethod]
        public void EndNotifications_YieldReturn()
        {
            const string method = @"
public System.Collections.Generic.IEnumerable<int> Method(bool a)
{
    if (a)
        yield return 1;

    yield return 2;
}";
            var validator = SETestContext.CreateCSMethod(method).Validator;
            validator.ValidateExitReachCount(1);
            validator.ValidateExecutionCompleted();
        }

        [TestMethod]
        public void EndNotifications_YieldBreak()
        {
            const string method = @"
public System.Collections.Generic.IEnumerable<int> Method(bool a)
{
    if (a)
        yield break;

    var b = a;
}";
            var validator = SETestContext.CreateCSMethod(method, new PreserveTestCheck("b")).Validator;
            validator.ValidateExitReachCount(2);
            validator.ValidateExecutionCompleted();
        }

        [TestMethod]
        public void Branching_ConstraintTrackedSeparatelyInBranches()
        {
            const string code = @"
bool value;
if (boolParameter)
{
    value = true;
}
else
{
    value = false;
}
var tag = ""End"";";
            var validator = SETestContext.CreateCS(code, new PreserveTestCheck("value")).Validator;
            validator.ValidateExitReachCount(2); // Once with True constraint, once with False constraint on "value"
            validator.TagValues("End", "value").Should().HaveCount(2)
                .And.ContainSingle(x => x.HasConstraint(BoolConstraint.True))
                .And.ContainSingle(x => x.HasConstraint(BoolConstraint.False));
        }

        [TestMethod]
        public void Branching_VisitedProgramState_IsSkipped()
        {
            const string code = @"
bool value;
if (Condition)
{
    value = true;
}
else
{
    value = true;
}
var tag = ""End"";";
            var validator = SETestContext.CreateCS(code).Validator;
            validator.ValidateExitReachCount(1);
            validator.TagValues("End", "value").Should().HaveCount(1).And.ContainSingle(x => x.HasConstraint(BoolConstraint.True));
        }

        [TestMethod]
        public void Branching_VisitedProgramState_IsImmutable()
        {
            const string code = @"
bool value;
if (boolParameter)
{
    value = true;
}
else
{
    value = false;
}
Tag(""End"", value);";
            var captured = new List<(SymbolicValue Value, bool ExpectedHasTrueConstraint)>();
            var postProcess = new PostProcessTestCheck(x =>
            {
                if (x.Operation.Instance.TrackedSymbol() is { } symbol && x.State[symbol] is { } value)
                {
                    captured.Add((value, value.HasConstraint(BoolConstraint.True)));
                }
                return x.State;
            });
            SETestContext.CreateCS(code, postProcess);
            captured.Should().OnlyContain(x => x.Value.HasConstraint(BoolConstraint.True) == x.ExpectedHasTrueConstraint);
        }

        [TestMethod]
        public void Branching_VisitedSymbolicValue_IsImmutable()
        {
            const string code = @"
var value = true;
if (boolParameter)
{
    value.ToString();
    var tag = ""ToString"";
}
else
{
    value.GetHashCode();    // Another invocation to have same instruction count in both branches
    var tag = ""GetHashCode"";
}";
            var postProcess = new PostProcessTestCheck(x =>
                x.Operation.Instance is IInvocationOperation { TargetMethod: { Name: "ToString" } } invocation
                    ? x.SetSymbolConstraint(invocation.Instance.TrackedSymbol(), TestConstraint.First)
                    : x.State);
            var validator = SETestContext.CreateCS(code, postProcess).Validator;
            validator.ValidateTag("ToString", "value", x => x.HasConstraint(TestConstraint.First).Should().BeTrue());
            validator.ValidateTag("GetHashCode", "value", x => x.HasConstraint(TestConstraint.First).Should().BeFalse()); // Nobody set the constraint on that path
            validator.ValidateExitReachCount(1);    // Once as the states are cleaned by LVA.
        }

        [TestMethod]
        public void Branching_TrueConstraint_VisitsIfBranch()
        {
            const string code = @"
var value = true;
if (value)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "If",
                "End");
        }

        [TestMethod]
        public void Branching_TrueConstraintNegated_VisitsElseBranch()
        {
            const string code = @"
var value = true;
if (!value)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "Else",
                "End");
        }

        [TestMethod]
        public void Branching_FalseConstraint_VisitsElseBranch()
        {
            const string code = @"
var value = false;
if (value)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "Else",
                "End");
        }

        [TestMethod]
        public void Branching_FalseConstraintNegated_VisitsIfBranch()
        {
            const string code = @"
var value = false;
if (!value)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "If",
                "End");
        }

        [TestMethod]
        public void Branching_NoConstraint_VisitsBothBranches()
        {
            const string code = @"
var value = boolParameter; // Unknown constraints
if (value)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "If",
                "Else",
                "End");
        }

        [TestMethod]
        public void Branching_OtherConstraint_VisitsBothBranches()
        {
            const string code = @"
if (boolParameter)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            var check = new PostProcessTestCheck(x => x.Operation.Instance.TrackedSymbol() is { } symbol ? x.SetSymbolConstraint(symbol, DummyConstraint.Dummy) : x.State);
            SETestContext.CreateCS(code, check).Validator.ValidateTagOrder(
                "If",
                "Else",
                "End");
        }

        [TestMethod]
        public void Branching_BoolConstraints_ComplexCase()
        {
            const string code = @"
var isTrue = true;
var isFalse = false;
if (isTrue && isTrue && !isFalse)
{
    if (isFalse || !isTrue)
    {
        Tag(""UnreachableIf"");
    }
    else if (isFalse)
    {
        Tag(""UnreachableElseIf"");
    }
    else
    {
        Tag(""Reachable"");
    }
}
else
{
    Tag(""UnreachableElse"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "Reachable",
                "End");
        }

        [TestMethod]
        public void Branching_TrueLiteral_VisitsIfBranch()
        {
            const string code = @"
if (true)
{
    Tag(""If"");
}
else
{
    Tag(""Else"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "If",
                "End");
        }

        [TestMethod]
        public void Branching_TrueConstraint_SwitchStatement_BinaryOperationNotSupported()
        {
            const string code = @"
var isTrue = true;
switch (isTrue)
{
    case true:
        Tag(""True"");
        break;
    case false:
        Tag(""False"");
        break;
    default:
        Tag(""Default"");
}
Tag(""End"");";
            SETestContext.CreateCS(code).Validator.ValidateTagOrder(
                "True",
                "End");
        }

        [TestMethod]
        public void Branching_BoolSymbol_LearnsBoolConstraint()
        {
            const string code = @"
var tag = ""Begin"";
if (boolParameter)          // True constraint is learned
{
    tag = ""True"";
    if (boolParameter)      // True constraint is known
    {
        tag = ""TrueTrue"";
    }
    else
    {
        tag = ""TrueFalse Unreachable"";
    }
}
else                        // False constraint is learned
{
    tag = ""False"";
    if (boolParameter)      // False constraint is known
    {
        tag = ""FalseTrue Unreachable"";
    }
    else
    {
        tag = ""FalseFalse"";
    }
};
tag = ""End"";";
            var validator = SETestContext.CreateCS(code).Validator;
            validator.ValidateTagOrder(
                "Begin",
                "True",
                "False",
                "TrueTrue",
                "FalseFalse",
                "End",
                "End");
            validator.ValidateTag("True", "boolParameter", x => x.HasConstraint(BoolConstraint.True).Should().BeTrue());
            validator.ValidateTag("False", "boolParameter", x => x.HasConstraint(BoolConstraint.False).Should().BeTrue());
            validator.TagStates("End").Should().HaveCount(2)
                .And.ContainSingle(x => x.SymbolsWith(BoolConstraint.True).Count() == 1)
                .And.ContainSingle(x => x.SymbolsWith(BoolConstraint.False).Count() == 1);
        }

        [TestMethod]
        public void Branching_BoolOperation_LearnsBoolConstraint()
        {
            const string code = @"
string tag;
if (collection.IsReadOnly)
{
    tag = ""If"";
}
tag = ""End"";";
            var check = new ConditionEvaluatedTestCheck(x => x.State[x.Operation].HasConstraint(BoolConstraint.True)
                                                                 ? x.SetSymbolConstraint(x.Operation.Instance.AsPropertyReference().Value.Instance.TrackedSymbol(), DummyConstraint.Dummy)
                                                                 : x.State);
            var validator = SETestContext.CreateCS(code, ", ICollection<object> collection", check).Validator;
            validator.ValidateTag("If", "collection", x => x.HasConstraint(DummyConstraint.Dummy).Should().BeTrue());
            validator.TagStates("End").Should().HaveCount(2);
        }

        [TestMethod]
        public void Branching_BoolExpression_LearnsBoolConstraint_NotSupported()
        {
            const string code = @"
if (boolParameter == true)
{
    var tag = ""BoolParameter"";
}
bool value;
if (value = boolParameter)
{
    var tag = ""Value"";
}";
            var validator = SETestContext.CreateCS(code).Validator;
            validator.ValidateTag("BoolParameter", "boolParameter", x => x.Should().BeNull());
            validator.ValidateTag("Value", "value", x => x.Should().BeNull());
        }

        [TestMethod]
        public void Branching_ConditionEvaluated()
        {
            const string code = @"
Tag(""Begin"");
if (boolParameter)
{
    Tag(""If"");
}
Tag(""End"");";
            var check = new ConditionEvaluatedTestCheck(x => x.State[x.Operation].HasConstraint(BoolConstraint.True) ? null : x.State);
            SETestContext.CreateCS(code, check).Validator.ValidateTagOrder("Begin", "End");
        }
    }
}
