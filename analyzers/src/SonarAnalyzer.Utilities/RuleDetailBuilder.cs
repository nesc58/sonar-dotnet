﻿/*
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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using SonarAnalyzer.Common;
using SonarAnalyzer.Helpers;
using SonarAnalyzer.RuleDescriptors;

namespace SonarAnalyzer.Utilities
{
    public static class RuleDetailBuilder
    {
        public static IEnumerable<RuleDetail> GetAllRuleDetails(AnalyzerLanguage language)
        {
            var resources = language.LanguageName switch
            {
                LanguageNames.CSharp => new ResourceManager("SonarAnalyzer.RspecStrings", typeof(Rules.CSharp.FlagsEnumZeroMember).Assembly),
                LanguageNames.VisualBasic => new ResourceManager("SonarAnalyzer.RspecStrings", typeof(Rules.VisualBasic.FlagsEnumZeroMember).Assembly),
                _ => throw new InvalidOperationException("Unexpected language: " + language)
            };
            return RuleFinder
                .GetAnalyzerTypes(language)
                .Select(x => (DiagnosticAnalyzer)Activator.CreateInstance(x))
                .SelectMany(x => UniqueRuleIds(x).Select(id => new { Id = id, Type = x.GetType() }))
                .GroupBy(x => x.Id)    // Same ruleId can be in multiple classes (see InvalidCastToInterface)
                .Select(x => new RuleDetail(language, resources, x.Key, Parameters(x.Select(item => item.Type))));
        }

        private static IEnumerable<string> UniqueRuleIds(DiagnosticAnalyzer analyzer) =>
            analyzer.SupportedDiagnostics.Select(x => x.Id).Distinct(); // One class can have the same ruleId multiple times, see S3240

        private static IEnumerable<RuleParameter> Parameters(IEnumerable<Type> analyzerTypes) =>
            analyzerTypes
                .SelectMany(x => x.GetProperties())
                .Select(x => x.GetCustomAttributes<RuleParameterAttribute>().SingleOrDefault())
                .WhereNotNull()
                .Select(x => new RuleParameter(x));
    }
}
