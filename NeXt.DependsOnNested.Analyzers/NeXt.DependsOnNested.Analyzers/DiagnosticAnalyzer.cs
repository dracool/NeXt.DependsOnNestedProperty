using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NeXt.DependsOnNested.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class DependsOnNestedPathAnalyzerAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "DependsOnNestedPathAnalyzer";
        private const string Category = "Syntax";


        private static readonly DiagnosticDescriptor InvalidPropertyPathRule = new DiagnosticDescriptor(
            DiagnosticId,
            "Invalid nested property path",
            "The property {0} of {1} could not be found on {2}",
            Category,
            DiagnosticSeverity.Error,
            true,
            "Path in Attribute MUST be valid"
        );

        private static readonly DiagnosticDescriptor SelfReferencingPropertyPathRule = new DiagnosticDescriptor(
            DiagnosticId,
            "Self referencing property path",
            "The property {0} of {1} is a self-reference",
            Category,
            DiagnosticSeverity.Error,
            true,
            "Path MUST NEVER reference itself"
        );
        
        private const string AttributeClassName = "DependsOnNestedAttribute";
        private const string AttributeAssemblyName = "NeXt.DependsOnNestedProperty";
        private const string AttributeAssemblyPublicKeyToken = "bd60f046a0f38f5e";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(InvalidPropertyPathRule, SelfReferencingPropertyPathRule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSymbolAction(AnalyzeProperty, SymbolKind.Property);
        }
        
        private static void CreateInvalidPropertyPathDiagnostic(SymbolAnalysisContext context, string propertyName, string path, string typeName)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                InvalidPropertyPathRule,
                context.Symbol.Locations.First(),
                propertyName,
                path,
                typeName
            ));
        }

        private static void CreateSelfReferencingPropertyPathDiagnostic(SymbolAnalysisContext context, string propertyName, string path)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                SelfReferencingPropertyPathRule,
                context.Symbol.Locations.FirstOrDefault(l => l.IsInSource) ?? context.Symbol.Locations.First(),
                propertyName,
                path
            ));
        }
        
        /// <summary>
        /// Creates a lower case hex string from a byte array
        /// </summary>
        /// <param name="bytes"></param>
        /// <returns></returns>
        private static string TokenFromByteStream(IEnumerable<byte> bytes)
        {
            return string.Concat(bytes.Select(b => $"{b:x2}"));
        }

        private static void AnalyzeProperty(SymbolAnalysisContext context)
        {
            var symbol = (IPropertySymbol) context.Symbol;

            var attribs = symbol.GetAttributes()
                .Where(a => a.AttributeClass.ContainingAssembly.Identity.Name == AttributeAssemblyName)
                .Where(a => a.AttributeClass.ContainingAssembly.Identity.IsStrongName)
                .Where(a => TokenFromByteStream(a.AttributeClass.ContainingAssembly.Identity.PublicKeyToken) == AttributeAssemblyPublicKeyToken)
                .Where(a => a.AttributeClass.Name == AttributeClassName);
            
            foreach (var attrib in attribs)
            {
                var syntax = attrib.ApplicationSyntaxReference.GetSyntax() as AttributeSyntax;
                var argtype = syntax?.ArgumentList.Arguments.First().Expression as LiteralExpressionSyntax;
                var path = argtype?.Token.Value.ToString();

                if (path == null) return;

                var pslit = path.Split('.');

                if (pslit[0] == symbol.Name)
                {
                    CreateSelfReferencingPropertyPathDiagnostic(context, symbol.Name, path);
                    return;
                }

                AnalyzePathBranch(context, symbol.ContainingType, pslit);
            }
        }

        /// <summary>
        /// recursively checks if a type symbol satisfies the requirements for a given branch
        /// </summary>
        /// <param name="context"></param>
        /// <param name="type"></param>
        /// <param name="path"></param>
        /// <param name="index"></param>
        private static void AnalyzePathBranch(SymbolAnalysisContext context, ITypeSymbol type, IReadOnlyList<string> path, int index = 0)
        {
            if (index >= path.Count) return;

            var any = false;

            foreach (var property in type.GetMembers(path[index]).Where(s => s is IPropertySymbol && !s.IsStatic).Cast<IPropertySymbol>())
            {
                any = true;
                AnalyzePathBranch(context, property.Type, path, index + 1);
            }

            if (!any)
            {
                CreateInvalidPropertyPathDiagnostic(context, path[index], string.Join(".", path), type.Name);
            }
        }
    }
}
