using System;
using System.Collections.Generic;
using System.Reflection;
using System.Diagnostics;
using System.Net.WebSockets;

namespace KodeRunner
{
    #region Settings
    /// <summary>
    ///  Settings provider for the runnable manager
    /// 
    /// </summary>
    public class Provider
    {
        /// <summary>
        ///  Interface for the settings provider
        ///  Contains all the settings that can be used to configure the runnable manager or said runner classes.
        /// when you implement this interface, you can use the settings to configure the runnable manager or the runner classes for use with the runnable manager and execute the runnables.
        /// </summary>
        /// <returns></returns>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="ArgumentException"></exception>
        public interface ISettingsProvider
        {
            string Language { get; set; }
            string Framework { get; set; }
            string ProjectType { get; set; }
            string ProjectName { get; set; }
            string ProjectPath { get; set; }
            bool IsWebProject { get; set; }
            bool Run_On_Build { get; set; }
            string Main_File { get; set; }
            string BuildCommand { get; set; }
            string RunCommand { get; set; }
            string TestCommand { get; set; }
            string Output { get; set; }
            string[] BuildArgs { get; set; }
            string[] RunArgs { get; set; }
            string[] TestArgs { get; set; }
            string[] BuildFlags { get; set; }
            string[] RunFlags { get; set; }
            string[] TestFlags { get; set; }
            string[] BuildOptions { get; set; }
            string[] RunOptions { get; set; }
            string[] TestOptions { get; set; }
            string[] BuildEnv { get; set; }
            string[] RunEnv { get; set; }
            WebSocket PmsWebSocket { get; set; }
        }

        public struct SettingsProvider : ISettingsProvider
        {
            public string Language { get; set; }
            public string Framework { get; set; }
            public string ProjectType { get; set; }
            public string ProjectName { get; set; }
            public string ProjectPath { get; set; }
            public bool IsWebProject { get; set; }
            public bool Run_On_Build { get; set; }
            public string Main_File { get; set; }
            public string BuildCommand { get; set; }
            public string RunCommand { get; set; }
            public string TestCommand { get; set; }
            public string Output { get; set; }
            public string[] BuildArgs { get; set; }
            public string[] RunArgs { get; set; }
            public string[] TestArgs { get; set; }
            public string[] BuildFlags { get; set; }
            public string[] RunFlags { get; set; }
            public string[] TestFlags { get; set; }
            public string[] BuildOptions { get; set; }
            public string[] RunOptions { get; set; }
            public string[] TestOptions { get; set; }
            public string[] BuildEnv { get; set; }
            public string[] RunEnv { get; set; }
            public WebSocket PmsWebSocket { get; set; }
        }
    }
    #endregion
}