using System.Collections.Generic;
using System.Threading.Tasks;

namespace KodeRunner
{
    [AttributeUsage(AttributeTargets.Class)]
    public class LanguageIncludeHandlerAttribute : Attribute
    {
        public string Language { get; }
        public int Priority { get; }

        public LanguageIncludeHandlerAttribute(string language, int priority = 0)
        {
            Language = language;
            Priority = priority;
        }
    }

    public interface ILanguageIncludeHandler
    {
        string Language { get; }
        Task<List<string>> ProcessIncludes(string projectPath, string[] includes);
    }
}
