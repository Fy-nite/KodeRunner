using System;

namespace KodeRunner
{
    #region Interfaces
    /// <summary>
    /// Interface for all runnables.
    /// </summary>
    public interface IRunnable
    {
        void Execute(Provider.ISettingsProvider settings);
        string Name { get; }
        string Language { get; }
        int Priority { get; }
        string description { get; }
    }
    #endregion

    #region Attributes
    /// <summary>
    /// Attribute to be used on classes that implement IRunnable.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public class RunnableAttribute : Attribute
    {
        public string Name { get; }
        public string Language { get; }
        public int Priority { get; }

        public RunnableAttribute(string name, string language, int priority = 0)
        {
            Name = name;
            Language = language;
            Priority = priority;
        }
    }
    #endregion
}
