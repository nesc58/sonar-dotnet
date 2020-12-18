﻿/*
 * SonarAnalyzer for .NET
 * Copyright (C) 2015-2020 SonarSource SA
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

extern alias csharp;
using csharp::SonarAnalyzer.Rules.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SonarAnalyzer.UnitTest.TestFramework;

namespace SonarAnalyzer.UnitTest.Rules
{
    [TestClass]
    public class ImplementSerializationMethodsCorrectlyTest
    {
        [TestMethod]
        [TestCategory("Rule")]
        public void ImplementSerializationMethodsCorrectly_CS() =>
            Verifier.VerifyAnalyzer(@"TestCases\ImplementSerializationMethodsCorrectly.cs", new ImplementSerializationMethodsCorrectly());

#if NET
        [TestMethod]
        [TestCategory("Rule")]
        public void ImplementSerializationMethodsCorrectly_CS_FromCSharp9() =>
            Verifier.VerifyAnalyzerFromCSharp9Console(@"TestCases\ImplementSerializationMethodsCorrectly.CSharp9.cs", new ImplementSerializationMethodsCorrectly());
#endif

        [TestMethod]
        [TestCategory("Rule")]
        public void ImplementSerializationMethodsCorrectly_CS_InvalidCode() =>
            Verifier.VerifyCSharpAnalyzer(@"
[Serializable]
public class Foo
{
    [OnDeserializing]
    public int  { throw new NotImplementedException(); }
}", new ImplementSerializationMethodsCorrectly(), checkMode: CompilationErrorBehavior.Ignore);

        [TestMethod]
        [TestCategory("Rule")]
        public void ImplementSerializationMethodsCorrectly_VB() =>
            Verifier.VerifyAnalyzer(@"TestCases\ImplementSerializationMethodsCorrectly.vb", new SonarAnalyzer.Rules.VisualBasic.ImplementSerializationMethodsCorrectly());
    }
}

