﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Textamina.Markdig.Helpers;
using Textamina.Markdig.Syntax;

namespace Textamina.Markdig.Formatters
{
    public class HtmlFormatter
    {
        private readonly HtmlTextWriter writer;

        private readonly Dictionary<Type, Action<object>> registeredWriters;

        private bool implicitParagraph;

        public HtmlFormatter(TextWriter writer)
        {
            if (writer == null) throw new ArgumentNullException(nameof(writer));
            writer.NewLine = "\n";
            this.writer = new HtmlTextWriter(writer);
            registeredWriters = new Dictionary<Type, Action<object>>
            {
                [typeof(ListBlock)] = o => Write((ListBlock)o),
                [typeof(FencedCodeBlock)] = o => Write((FencedCodeBlock)o),
                [typeof(CodeBlock)] = o => Write((CodeBlock)o),
                [typeof(HeadingBlock)] = o => Write((HeadingBlock)o),
                [typeof(BreakBlock)] = o => Write((BreakBlock)o),
                [typeof(QuoteBlock)] = o => Write((QuoteBlock)o),
                [typeof(ParagraphBlock)] = o => Write((ParagraphBlock)o),
                [typeof(HtmlBlock)] = o => Write((HtmlBlock)o),
                [typeof(LiteralInline)] = o => Write((LiteralInline)o),
                [typeof(CodeInline)] = o => Write((CodeInline)o),
                [typeof(LinkInline)] = o => Write((LinkInline)o),
                [typeof(AutolinkInline)] = o => Write((AutolinkInline)o),
                [typeof(HtmlInline)] = o => Write((HtmlInline)o),
                [typeof(EmphasisInline)] = o => Write((EmphasisInline)o),
                [typeof(HardlineBreakInline)] = o => Write((HardlineBreakInline)o),
                [typeof(ContainerInline)] = o => WriteChildren((ContainerInline)o),
            };
        }

        public void Write(Document document)
        {
            WriteContainer((ContainerBlock) document);
        }

        protected void Write(ListBlock listBlock)
        {
            writer.EnsureLine();
            if (listBlock.IsOrdered)
            {
                writer.WriteConstant("<ol");
                if (listBlock.OrderedStart != 1)
                {
                    writer.WriteConstant(" start=\"");
                    writer.WriteConstant(listBlock.OrderedStart.ToString(CultureInfo.InvariantCulture));
                    writer.WriteConstant("\"");
                }
                writer.WriteLineConstant(">");
            }
            else
            {
                writer.WriteLineConstant("<ul>");
            }
            foreach (var item in listBlock.Children)
            {
                var listItem = (ListItemBlock) item;
                var previousImplicit = implicitParagraph;
                implicitParagraph = !listBlock.IsLoose;
                Write(listItem, listBlock.IsLoose);
                implicitParagraph = previousImplicit;
            }
            writer.WriteLineConstant(listBlock.IsOrdered ? "</ol>": "</ul>");
        }

        protected void Write(ListItemBlock listBlockItem, bool isLoose)
        {
            writer.EnsureLine();
            writer.WriteConstant("<li>");
            WriteContainer(listBlockItem);
            writer.WriteLineConstant("</li>");
        }

        protected void Write(FencedCodeBlock fencedCodeBlock)
        {
            Write(fencedCodeBlock, fencedCodeBlock.Language, fencedCodeBlock.Arguments);
        }

        protected void Write(CodeBlock codeBlock)
        {
            Write(codeBlock, null, null);
        }

        protected void Write(CodeBlock codeBlock, string language, string arguments)
        {
            writer.EnsureLine();
            // class="language-ruby
            writer.WriteConstant("<pre><code");
            if (string.IsNullOrEmpty(language))
            {
                writer.WriteConstant(">");
            }
            else
            {
                writer.WriteConstant(" class=\"language-");
                HtmlHelper.EscapeHtml(language, writer);
                writer.WriteConstant("\">");
            }
            WriteLeaf(codeBlock, true, true);
            writer.WriteLineConstant("</code></pre>");
        }


        protected void Write(HtmlBlock codeBlock)
        {
            WriteLeaf(codeBlock, true, false);
        }

        protected void Write(HtmlInline htmlInline)
        {
            writer.WriteConstant(htmlInline.Tag);
        }

        protected void Write(HeadingBlock headingBlock)
        {
            var heading = headingBlock.Level.ToString(CultureInfo.InvariantCulture);
            writer.WriteConstant("<h");
            writer.WriteConstant(heading);
            writer.WriteConstant(">");
            WriteLeaf(headingBlock, false, false);
            writer.WriteConstant("</h");
            writer.WriteConstant(heading);
            writer.WriteLineConstant(">");
        }

        protected void Write(BreakBlock breakBlock)
        {
            writer.WriteLineConstant("<hr />");
        }

        protected void Write(QuoteBlock quoteBlock)
        {
            writer.EnsureLine();
            writer.WriteLineConstant("<blockquote>");
            var savedImplicitParagraph = implicitParagraph;
            implicitParagraph = false;
            WriteContainer(quoteBlock);
            implicitParagraph = savedImplicitParagraph;
            writer.WriteLineConstant("</blockquote>");
        }

        protected void Write(ParagraphBlock paragraph)
        {
            writer.EnsureLine();
            if (!implicitParagraph)
            {
                writer.WriteConstant("<p>");
            }
            WriteLeaf(paragraph, false, false);
            if (!implicitParagraph)
            {
                writer.WriteLineConstant("</p>");
            }
        }

        protected void Write(LiteralInline literal)
        {
            HtmlHelper.EscapeHtml(literal.Content, writer);
        }

        protected void Write(AutolinkInline autolink)
        {
            writer.WriteConstant("<a href=\"");
            if (autolink.IsEmail)
            {
                writer.WriteConstant("mailto:");
            }
            HtmlHelper.EscapeUrl(autolink.Url, writer);
            writer.WriteConstant("\">");
            HtmlHelper.EscapeHtml(autolink.Url, writer);
            writer.WriteConstant("</a>");
        }

        protected void Write(CodeInline code)
        {
            writer.WriteConstant("<code>");
            HtmlHelper.EscapeHtml(code.Content, writer);
            writer.WriteConstant("</code>");
        }

        protected void Write(LinkInline link)
        {
            writer.WriteConstant(link.IsImage ? "<img src=\"" : "<a href=\"");
            HtmlHelper.EscapeUrl(link.Url, writer);
            writer.WriteConstant("\"");
            if (link.IsImage)
            {
                writer.WriteConstant(" alt=\"");
                WriteChildren(link);
                writer.WriteConstant("\"");
            }

            if (!string.IsNullOrEmpty(link.Title))
            {
                writer.WriteConstant(" title=\"");
                HtmlHelper.EscapeHtml(link.Title, writer);
                writer.WriteConstant("\"");
            }

            if (link.IsImage)
            {
                writer.WriteConstant(" />");
            }
            else
            {
                writer.WriteConstant(">");
                WriteChildren(link);
                writer.WriteConstant("</a>");
            }
        }

        protected void Write(EmphasisInline emphasisInline)
        {
            if (emphasisInline.Strong)
            {
                writer.WriteConstant("<strong>");
                WriteChildren(emphasisInline);
                writer.WriteConstant("</strong>");
            }
            else
            {
                writer.WriteConstant("<em>");
                WriteChildren(emphasisInline);
                writer.WriteConstant("</em>");
            }
        }

        protected void Write(HardlineBreakInline hardlineBreak)
        {
            writer.WriteConstant("<br />");
        }

        protected void WriteChildren(ContainerInline containerInline)
        {
            var inline = containerInline.FirstChild;
            while (inline != null)
            {
                Action<object> writerAction;
                if (registeredWriters.TryGetValue(inline.GetType(), out writerAction))
                {
                    writerAction(inline);
                }

                inline = inline.NextSibling;
            }
        }

        protected void WriteLeaf(LeafBlock leafBlock, bool writeEndOfLines, bool escape)
        {
            var inline = leafBlock.Inline;
            if (inline != null)
            {
                while (inline != null)
                {
                    Action<object> writerAction;
                    if (registeredWriters.TryGetValue(inline.GetType(), out writerAction))
                    {
                        writerAction(inline);
                    }

                    inline = inline.NextSibling;
                }
            }
            else
            {
                var lines = leafBlock.Lines;
                for (int i = 0; i < lines.Count; i++)
                {
                    if (!writeEndOfLines && i > 0)
                    {
                        writer.WriteLine();
                    }
                    var line = lines[i];
                    if (escape)
                    {
                        HtmlHelper.EscapeHtml(line.ToString(), writer);
                    }
                    else
                    {
                        writer.WriteConstant(line.ToString());
                    }
                    if (writeEndOfLines)
                    {
                        writer.WriteLine();
                    }
                }
            }
        }

        protected void WriteContainer(ContainerBlock container)
        {
            foreach (var child in container.Children)
            {
                Action<object> writerAction;
                if (registeredWriters.TryGetValue(child.GetType(), out writerAction))
                {
                    writerAction(child);
                }
            }
        }
    }
}