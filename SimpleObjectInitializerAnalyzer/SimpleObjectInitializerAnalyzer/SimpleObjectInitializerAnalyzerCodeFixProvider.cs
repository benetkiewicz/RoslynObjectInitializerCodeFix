using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.Formatting;

namespace SimpleObjectInitializerAnalyzer
{
    [ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SimpleObjectInitializerAnalyzerCodeFixProvider)), Shared]
    public class SimpleObjectInitializerAnalyzerCodeFixProvider : CodeFixProvider
    {
        public override ImmutableArray<string> FixableDiagnosticIds
        {
            get
            {
                return ImmutableArray.Create(SimpleObjectInitializerAnalyzer.DiagnosticId);
            }
        }

        public sealed override FixAllProvider GetFixAllProvider()
        {
            return WellKnownFixAllProviders.BatchFixer;
        }

        public override async Task RegisterCodeFixesAsync(CodeFixContext context)
        {
            var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
            var diagnostic = context.Diagnostics.First();
            var diagnosticSpan = diagnostic.Location.SourceSpan;
            var declarations = root.FindToken(diagnosticSpan.Start).Parent.AncestorsAndSelf().OfType<LocalDeclarationStatementSyntax>();

            if (declarations == null)
            {
                return;
            }

            var declaration = declarations.First();

            context.RegisterCodeFix(
                CodeAction.Create("Rewrite Setters", c => RewriteSetters(context.Document, declaration, c)),
                diagnostic);
        }

        private BlockSyntax GetContainingBlock(SyntaxNode node)
        {
            var block = node.Parent as BlockSyntax;
            if (block != null)
            {
                return block;
            }

            return GetContainingBlock(node.Parent);
        }

        private async Task<Document> RewriteSetters(Document document, LocalDeclarationStatementSyntax declarationWithInitializer, CancellationToken c)
        {
            var root = await document.GetSyntaxRootAsync(c).ConfigureAwait(false);
            var variableDeclarator = declarationWithInitializer.DescendantNodes().OfType<VariableDeclaratorSyntax>().First();
            string variableName = variableDeclarator.Identifier.ToString();

            var objectInitializer = declarationWithInitializer.DescendantNodes().OfType<InitializerExpressionSyntax>().First();
            var initializedProperties = new List<SyntaxNode>();
            foreach (var propInitialization in objectInitializer.Expressions)
            {
                var separatePropInitialization = SyntaxFactory.ParseStatement(variableName + "." + propInitialization + ";");
                separatePropInitialization = separatePropInitialization.WithTrailingTrivia(SyntaxFactory.Whitespace(Environment.NewLine));
                initializedProperties.Add(separatePropInitialization);
            }

            var declarationWithoutInitializer = declarationWithInitializer.RemoveNode(objectInitializer, SyntaxRemoveOptions.KeepNoTrivia);
            declarationWithoutInitializer = declarationWithoutInitializer.WithSemicolonToken(SyntaxFactory.Token(SyntaxKind.SemicolonToken));

            var objCreationExpr = declarationWithoutInitializer.DescendantNodes().OfType<ObjectCreationExpressionSyntax>().ToList()[0];
            if (objCreationExpr.ArgumentList == null)
            {
                ObjectCreationExpressionSyntax objCreationExprWithEmptyArgList = objCreationExpr.WithArgumentList(SyntaxFactory.ArgumentList());
                declarationWithoutInitializer = declarationWithoutInitializer.ReplaceNode(objCreationExpr, objCreationExprWithEmptyArgList);
            }

            var block = GetContainingBlock(declarationWithInitializer);
            var newBlock = block.TrackNodes(declarationWithInitializer);
            var refreshedObjectInitializer = newBlock.GetCurrentNode(declarationWithInitializer);
            newBlock = newBlock.InsertNodesAfter(refreshedObjectInitializer, initializedProperties).WithAdditionalAnnotations(Formatter.Annotation);
            refreshedObjectInitializer = newBlock.GetCurrentNode(declarationWithInitializer);
            newBlock = newBlock.ReplaceNode(refreshedObjectInitializer, declarationWithoutInitializer);

            var newroot = root.ReplaceNode(block, newBlock).WithAdditionalAnnotations(Formatter.Annotation);
            return document.WithSyntaxRoot(newroot);
        }
    }
}