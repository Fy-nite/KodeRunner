using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Linq;

namespace KodeRunner.Services
{
    public class LanguagePattern
    {
        public string Pattern { get; set; } = "";
        public string Flags { get; set; } = "";
        public string Color { get; set; } = "";
        
        private Regex? _compiledRegex;
        public Regex CompiledRegex
        {
            get
            {
                if (_compiledRegex == null)
                {
                    var options = RegexOptions.None;
                    if (Flags.Contains("i")) options |= RegexOptions.IgnoreCase;
                    if (Flags.Contains("m")) options |= RegexOptions.Multiline;
                    if (Flags.Contains("s")) options |= RegexOptions.Singleline;
                    
                    _compiledRegex = new Regex(Pattern, options);
                }
                return _compiledRegex;
            }
        }
    }
    
    public class LanguageDefinition
    {
        public string Name { get; set; } = "";
        public string[] FileExtensions { get; set; } = Array.Empty<string>();
        public Dictionary<string, LanguagePattern> Patterns { get; set; } = new();
        public string[] TokenOrder { get; set; } = Array.Empty<string>();
    }
    
    public class SyntaxHighlightingService
    {
        private readonly Dictionary<string, LanguageDefinition> _languages = new();
        private readonly Dictionary<string, string> _customExtensionMappings = new();
        
        public SyntaxHighlightingService()
        {
            LoadLanguages();
        }
        
        private void LoadLanguages()
        {
            var languagesDir = Core.GetPath(Core.LanguagesDir);
            
            if (!Directory.Exists(languagesDir))
            {
                Logger.Log("Languages directory not found, creating default", "Warning");
                Directory.CreateDirectory(languagesDir);
                CreateDefaultLanguageFiles(languagesDir);
                return;
            }
            
            try
            {
                var files = Directory.GetFiles(languagesDir, "*.json");
                
                foreach (var file in files)
                {
                    try
                    {
                        var languageData = LoadLanguageFromFile(file);
                        if (languageData != null)
                        {
                            _languages[languageData.Name.ToLower()] = languageData;
                            Logger.Log($"Loaded syntax highlighting for: {languageData.Name}", "Info");
                        }
                    }
                    catch (Exception ex)
                    {
                        Logger.Log($"Error loading language file {file}: {ex.Message}", "Error");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Log($"Error reading languages directory: {ex.Message}", "Error");
            }
        }
        
        private LanguageDefinition? LoadLanguageFromFile(string filePath)
        {
            var json = File.ReadAllText(filePath);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;
            
            var language = new LanguageDefinition
            {
                Name = root.GetProperty("name").GetString() ?? "",
                FileExtensions = root.GetProperty("fileExtensions").EnumerateArray()
                    .Select(x => x.GetString() ?? "").ToArray(),
                TokenOrder = root.GetProperty("tokenOrder").EnumerateArray()
                    .Select(x => x.GetString() ?? "").ToArray()
            };
            
            var patterns = root.GetProperty("patterns");
            foreach (var pattern in patterns.EnumerateObject())
            {
                var patternObj = pattern.Value;
                language.Patterns[pattern.Name] = new LanguagePattern
                {
                    Pattern = patternObj.GetProperty("pattern").GetString() ?? "",
                    Flags = patternObj.GetProperty("flags").GetString() ?? "",
                    Color = patternObj.GetProperty("color").GetString() ?? ""
                };
            }
            
            return language;
        }
        
        public string DetectLanguage(string? filename)
        {
            if (string.IsNullOrEmpty(filename)) return "python"; // Default
            
            var ext = Path.GetExtension(filename).ToLower();
            
            // Check custom mappings first
            if (_customExtensionMappings.TryGetValue(ext, out var customLanguage))
            {
                if (customLanguage.ToLower() == "auto")
                {
                    // Fall through to automatic detection
                }
                else
                {
                    return customLanguage;
                }
            }
            
            foreach (var (langName, langConfig) in _languages)
            {
                if (langConfig.FileExtensions.Contains(ext))
                {
                    return langName;
                }
            }
            
            return "python"; // Default fallback
        }
        
        public void SetLanguageMapping(string extension, string language)
        {
            if (!extension.StartsWith("."))
            {
                extension = "." + extension;
            }
            
            extension = extension.ToLower();
            
            if (language.ToLower() == "auto")
            {
                // Remove custom mapping to use automatic detection
                _customExtensionMappings.Remove(extension);
            }
            else
            {
                _customExtensionMappings[extension] = language.ToLower();
            }
            
            Logger.Log($"Updated language mapping: {extension} -> {language}", "Info");
        }
        
        public string Highlight(string code, string language = "csharp")
        {
            language = "microasm"; // Force microasm for now
            if (!_languages.TryGetValue(language.ToLower(), out var langConfig))
            {
                // If the requested language is not found, try python as fallback
                if (language.ToLower() != "python" && _languages.TryGetValue("python", out var pythonConfig))
                {
                    langConfig = pythonConfig;
                }
                else
                {
                    // If python is also not available or we're already trying python, return plain text
                    return code;
                }
            }
            
            var highlighted = code;
            var tokens = new Dictionary<string, string>();
            var tokenId = 0;
            
            foreach (var tokenType in langConfig.TokenOrder)
            {
                if (!langConfig.Patterns.TryGetValue(tokenType, out var pattern))
                    continue;
                
                try
                {
                    highlighted = pattern.CompiledRegex.Replace(highlighted, (match) =>
                    {
                        // Check if this text is already inside a token
                        if (match.Value.Contains("__TOKEN__"))
                        {
                            return match.Value;
                        }
                        
                        var id = $"__TOKEN__{tokenId++}__";
                        tokens[id] = $"<color=#{pattern.Color}>{match.Value}</color>";
                        return id;
                    });
                }
                catch (Exception ex)
                {
                    Logger.Log($"Error applying pattern {tokenType}: {ex.Message}", "Warning");
                }
            }
            
            // Replace all tokens with their highlighted versions
            foreach (var (id, html) in tokens)
            {
                highlighted = highlighted.Replace(id, html);
            }
            
            return highlighted;
        }
        
        public string[] GetAvailableLanguages()
        {
            return _languages.Keys.ToArray();
        }
        
        private void CreateDefaultLanguageFiles(string languagesDir)
        {
            // Create a default Python language file
            var pythonConfig = new
            {
                name = "Python",
                fileExtensions = new[] { ".py" },
                tokenOrder = new[] { "string", "comment", "keyword", "builtin", "number" },
                patterns = new Dictionary<string, object>
                {
                    ["string"] = new { pattern = @"""([^""\\]|\\.)*""|'([^'\\]|\\.)*'", flags = "g", color = "00FF00" },
                    ["comment"] = new { pattern = @"#.*$", flags = "gm", color = "808080" },
                    ["keyword"] = new { pattern = @"\b(def|class|if|else|elif|for|while|import|from|return|try|except|with|as|pass|break|continue|lambda|yield|global|nonlocal|assert|del|raise|finally|and|or|not|in|is)\b", flags = "g", color = "FF8C00" },
                    ["builtin"] = new { pattern = @"\b(print|len|range|str|int|float|list|dict|tuple|set|bool|type|isinstance|hasattr|getattr|setattr|delattr|callable|iter|next|enumerate|zip|map|filter|sorted|reversed|sum|min|max|abs|round|pow|divmod|chr|ord|bin|oct|hex|repr|format|open|file|input|eval|exec|compile|globals|locals|vars|dir|help|id|hash|object|super|property|staticmethod|classmethod|slice|memoryview|bytearray|bytes)\b", flags = "g", color = "1E90FF" },
                    ["number"] = new { pattern = @"\b\d+(\.\d+)?\b", flags = "g", color = "FF69B4" }
                }
            };
            
            var pythonJson = JsonSerializer.Serialize(pythonConfig, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(Path.Combine(languagesDir, "python.json"), pythonJson);
            
            Logger.Log("Created default Python syntax highlighting configuration", "Info");
        }

        internal object HighlightCode(string code, string language)
        {
            return Highlight(code, language);
        }
    }
}
