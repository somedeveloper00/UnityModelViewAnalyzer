using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using Analyzers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Sample.Analyzers {
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ViewAnalyzers : DiagnosticAnalyzer {

        internal static DiagnosticDescriptor Rule001 =
            new DiagnosticDescriptor("MV001", Resources.AnalyzeTitle_MV001, Resources.AnalyzeDesc_MV001, "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

        internal static DiagnosticDescriptor Rule002 =
            new DiagnosticDescriptor("MV002", Resources.AnalyzeTitle_MV002, Resources.AnalyzeDesc_MV002, "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

        internal static DiagnosticDescriptor Rule003 =
            new DiagnosticDescriptor("MV003", Resources.AnalyzeTitle_MV003, Resources.AnalyzeDesc_MV003, "Usage", DiagnosticSeverity.Error, isEnabledByDefault: true);

        internal static DiagnosticDescriptor Rule004 =
            new DiagnosticDescriptor(
                "MV004",
                Resources.AnalyzeTitle_MV004,
                Resources.AnalyzeDesc_MV004,
                "Usage",
                DiagnosticSeverity.Error,
                isEnabledByDefault: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule001, Rule002, Rule003, Rule004);

        public override void Initialize(AnalysisContext context) {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterSymbolAction(AnalyzeSymbol, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context) {
            INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)context.Symbol;

            if (ImplementsIView(namedTypeSymbol)) {
                // 003
                if (ImplementsBaseIView(namedTypeSymbol)) {
                    context.ReportDiagnostic(Diagnostic.Create(Rule003, namedTypeSymbol.Locations[0], namedTypeSymbol.Name));
                }
                // 001
                else if (!ViewIsClass(namedTypeSymbol)) {
                    context.ReportDiagnostic(Diagnostic.Create(Rule001, namedTypeSymbol.Locations[0], namedTypeSymbol.Name));
                }
                else if (!IsAbstract(namedTypeSymbol)) {
                    // 002
                    if (!IsMonoBehaviour(namedTypeSymbol)) {
                        context.ReportDiagnostic(Diagnostic.Create(Rule002, namedTypeSymbol.Locations[0], namedTypeSymbol.Name));
                    }
                    // 004
                    else if (!UsesRequireComponentOfViewGameObjectAttribute(namedTypeSymbol)) {
                        context.ReportDiagnostic(Diagnostic.Create(Rule004, namedTypeSymbol.Locations[0], namedTypeSymbol.Name));
                    }
                }
            }
        }

        private static bool ViewIsClass(INamedTypeSymbol namedTypeSymbol) => namedTypeSymbol.TypeKind == TypeKind.Class;
        private static bool IsAbstract(INamedTypeSymbol namedTypeSymbol) => namedTypeSymbol.IsAbstract;
        private static bool IsMonoBehaviour(INamedTypeSymbol namedTypeSymbol) => Utils.IsDerivedFrom(namedTypeSymbol, Resources.UnityEngine_Namespace, Resources.MonoBehaviour);
        private static bool ImplementsIView(INamedTypeSymbol namedTypeSymbol) {
            foreach (var @interface in namedTypeSymbol.Interfaces)
                if (Utils.IsDerivedFrom(@interface, Resources.IView_Namespace, Resources.IVew))
                    return true;
            return false;
        }
        private static bool ImplementsBaseIView(INamedTypeSymbol namedTypeSymbol) {
            foreach (var @interface in namedTypeSymbol.Interfaces) {
                if (@interface.Name == Resources.IVew && @interface.ContainingNamespace.Name == Resources.IView_Namespace) {
                    if (@interface.TypeArguments.Length == 0) {
                        return true;
                    }
                }
            }
            return false;            
        }
        private static bool UsesRequireComponentOfViewGameObjectAttribute(INamedTypeSymbol namedTypeSymbol) {
            foreach (var attribute in namedTypeSymbol.GetAttributes()) {
                if (Utils.IsDerivedFrom(attribute.AttributeClass, Resources.UnityEngine_Namespace, Resources.RequireComponent)) {
                    foreach (TypedConstant argument in attribute.ConstructorArguments) {
                        if (argument.Type is null) continue;
                        if (argument.Value is INamedTypeSymbol namedType && Utils.IsDerivedFrom(namedType, Resources.IView_Namespace, Resources.ViewGameObject)) {
                            return true;
                        }
                    }
                }
            }
            return false;
        }
    }

    /// <summary>
    /// Converts the struct to class.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Fix001)), Shared]
    class Fix001 : CodeFixProvider {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ViewAnalyzers.Rule001.Id);
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<StructDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.AnalyzeFix_MV001 ,
                    createChangedDocument: ct => ChangeStructToClassAsync(context.Document, declaration, ct),
                    equivalenceKey: Resources.AnalyzeFix_MV001),
                diagnostic);
        }

        private async Task<Document> ChangeStructToClassAsync(Document document, StructDeclarationSyntax structDeclaration, CancellationToken cancellationToken) {
            var root = await document.GetSyntaxRootAsync(cancellationToken);
            var newClassDeclaration = Utils.ConvertStructToClassSyntax(structDeclaration);
            var newRoot = root.ReplaceNode(structDeclaration, newClassDeclaration);
            return document.WithSyntaxRoot(newRoot);
        }

        
    }

    /// <summary>
    /// Inherits Monobehaviour.
    /// </summary>
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(Fix002)), Shared]
    class Fix002 : CodeFixProvider {
        public sealed override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create(ViewAnalyzers.Rule002.Id);
        public sealed override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;
        public sealed override async Task RegisterCodeFixesAsync(CodeFixContext context) {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);

            var diagnostic = context.Diagnostics[0];
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var declaration = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<ClassDeclarationSyntax>().First();

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: Resources.AnalyzeFix_MV002 ,
                    createChangedDocument: ct => InheritMonoBehaviour(context.Document, declaration, ct),
                    equivalenceKey: Resources.AnalyzeFix_MV002),
                diagnostic);
        }

        private static async Task<Document> InheritMonoBehaviour(Document document, ClassDeclarationSyntax classDecleration, CancellationToken cancellationToken) {
            var root = await document.GetSyntaxRootAsync(cancellationToken);

            SimpleBaseTypeSyntax monoBehaviourTypeSyntax = Utils.GetTypeSyntax(Resources.UnityEngine_Namespace, Resources.MonoBehaviour);

            // replace or add MonoBehaviour at 1st place (base class goes before interface)
            var newClassDecleration = Utils.RemoveBase(classDecleration, await document.GetSemanticModelAsync());
            newClassDecleration = Utils.AddBase(newClassDecleration, monoBehaviourTypeSyntax);

            var newRoot = root.ReplaceNode(classDecleration, newClassDecleration);
            return document.WithSyntaxRoot(newRoot);
        }
    }
}