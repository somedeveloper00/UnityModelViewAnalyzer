using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using System.Threading.Tasks;
using System;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Threading;
using Microsoft.CodeAnalysis.CSharp;

namespace Analyzers {
    internal static class Utils {
        public static bool IsDerivedFrom(INamedTypeSymbol baseType, string targetNamespace, string targetType) {
            while (baseType != null) {
                if (baseType.Name == targetType && baseType.ContainingNamespace.Name == targetNamespace)
                    return true;
                baseType = baseType.BaseType;
            }
            return false;
        }

        public static bool IsOfType(ISymbol baseType, string targetNamespace, string targetType) {
            return baseType.Name == targetType && baseType.ContainingNamespace.Name == targetNamespace;
        }

        public static async Task<T> GetFixableNodeAsync<T>(this CodeFixContext context) where T : SyntaxNode {
            return await GetFixableNodeAsync<T>(context, _ => true);
        }

        public static async Task<T> GetFixableNodeAsync<T>(this CodeFixContext context, Func<T, bool> predicate) where T : SyntaxNode {
            var root = await context
                .Document
                .GetSyntaxRootAsync(context.CancellationToken)
                .ConfigureAwait(false);

            return root?
                .FindNode(context.Span)
                .DescendantNodesAndSelf()
                .OfType<T>()
                .FirstOrDefault(predicate);
        }

        /// <summary>
        /// Removes base class from the list of <see cref="TypeDeclarationSyntax.BaseList"/>.
        /// </summary>
        public static TypeDeclarationSyntax RemoveBase(TypeDeclarationSyntax typeDeclaration, SemanticModel semanticModel) {

            if (typeDeclaration.BaseList == null || typeDeclaration.BaseList.Types.Count == 0) {
                return typeDeclaration;
            }

            if (typeDeclaration.BaseList != null && typeDeclaration.BaseList.Types != null && typeDeclaration.BaseList.Types.Count > 0) {
                BaseTypeSyntax firstTypeSyntax = typeDeclaration.BaseList.Types[0];
                var typeInfo = semanticModel.GetTypeInfo(firstTypeSyntax.Type, CancellationToken.None);
                if (typeInfo.Type != null && typeInfo.Type.TypeKind != TypeKind.Interface) {
                    var newTypes = typeDeclaration.BaseList.Types.RemoveAt(0);
                    BaseListSyntax newBaseList = typeDeclaration.BaseList.WithTypes(newTypes);
                    // taken from https://github.com/dotnet/roslyn/blob/3630eb1758c131d0f359704a0e8cc874e109d269/src/VisualStudio/CSharp/Impl/CodeModel/CSharpCodeModelService.cs#L3802
                    // apparently internally a null is preferred to a count==0 
                    if (newBaseList.Types.Count == 0) newBaseList = null; 
                    return typeDeclaration.WithBaseList(newBaseList);
                }
            }
            return typeDeclaration;
        }

        /// <summary>
        /// Adds base class to the list of <see cref="BaseTypeDeclarationSyntax.BaseList"/>
        /// </summary>
        public static TypeDeclarationSyntax AddBase(TypeDeclarationSyntax newClassDecleration, SimpleBaseTypeSyntax monoBehaviourTypeSyntax) {
            var newBaseList = newClassDecleration.BaseList ?? SyntaxFactory.BaseList();
            newBaseList = newBaseList.WithTypes(SyntaxFactory.SeparatedList(newBaseList.Types.Insert(0, monoBehaviourTypeSyntax)));
            return newClassDecleration.WithBaseList(newBaseList);
        }

        /// <summary>
        /// returns the exact type (<see cref="SimpleBaseTypeSyntax"/>) from namespace and class name
        /// </summary>
        public static SimpleBaseTypeSyntax GetTypeSyntax(string @namespace, string className) {
            return SyntaxFactory.SimpleBaseType(SyntaxFactory.ParseTypeName($"{@namespace}.{className}"));
        }


        /// <summary>
        /// Converts struct syntax to class syntax, keeping everything else as it was
        /// </summary>
        public static ClassDeclarationSyntax ConvertStructToClassSyntax(StructDeclarationSyntax node) {
            SyntaxToken keyword = SyntaxFactory.Token(node.Keyword.LeadingTrivia, SyntaxKind.ClassKeyword, node.Keyword.TrailingTrivia);
            return SyntaxFactory.ClassDeclaration(node.AttributeLists, node.Modifiers, keyword, node.Identifier, node.TypeParameterList, node.BaseList, node.ConstraintClauses,
                            node.OpenBraceToken, node.Members, node.CloseBraceToken, node.SemicolonToken);
        }
    }
}