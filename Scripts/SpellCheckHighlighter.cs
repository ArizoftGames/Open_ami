using OpenAmi.Scripts;
using Godot;
using Godot.Collections;
using System;
using System.Text.RegularExpressions;

namespace OpenAmi.Scripts
{
    public partial class SpellCheckHighlighter : SyntaxHighlighter
    {
        private AppManager _appManager;
        private TextEdit _textEdit;

#pragma warning disable IDE0290 // Use primary constructor
        public SpellCheckHighlighter(AppManager appManager, TextEdit textEdit)
#pragma warning restore IDE0290 // Use primary constructor
        {
            _appManager = appManager;
            _textEdit = textEdit;
        }

        public override Dictionary _GetLineSyntaxHighlighting(int line)
        {
            try
            {
                if (_appManager._spellDictionary == null)
                    return (Dictionary)new Dictionary<int, Dictionary>();

                // Get text for line
                string lineText = _textEdit.GetLine(line);

                // Regex match words
                var regex = new Regex(@"\b\w+\b");
                var matches = regex.Matches(lineText);

                var regions = new Dictionary<int, Dictionary>();

                foreach (Match match in matches)
                {
                    if (!_appManager._spellDictionary.Check(match.Value))
                    {
                        var region = new Dictionary
                        {
                            ["color"] = new Color(1f, 0.3f, 0.4f), 
                        };
                        regions[match.Index] = region;

                        // Add reset region with default text color
                        var resetRegion = new Dictionary
                        {
                            ["color"] = _textEdit.GetThemeColor("font_color"),
                        };
                        regions[match.Index + match.Length] = resetRegion;
                    }
                }

                return (Dictionary)regions;
            }
            catch (Exception e)
            {
                GD.PrintErr($"SpellCheckHighlighter:_get_line_syntax_highlighting - Error: {e.Message}");
                return (Dictionary)new Dictionary<int, Dictionary>();
            }
        }
    }
}
