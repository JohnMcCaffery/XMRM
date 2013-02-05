/*
 * Copyright (c) Contributors, http://opensimulator.org/
 * See CONTRIBUTORS.TXT for a full list of copyright holders.
 *
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 *     * Redistributions of source code must retain the above copyright
 *       notice, this list of conditions and the following disclaimer.
 *     * Redistributions in binary form must reproduce the above copyright
 *       notice, this list of conditions and the following disclaimer in the
 *       documentation and/or other materials provided with the distribution.
 *     * Neither the name of the OpenSimulator Project nor the
 *       names of its contributors may be used to endorse or promote products
 *       derived from this software without specific prior written permission.
 *
 * THIS SOFTWARE IS PROVIDED BY THE DEVELOPERS ``AS IS'' AND ANY
 * EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
 * WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE CONTRIBUTORS BE LIABLE FOR ANY
 * DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
 * (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
 * LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
 * ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
 * SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Nini.Config;
using System.IO;
using System.Reflection;
using System.Security.Policy;
using System.Security;
using System.Security.Permissions;
using System.Collections;
using OpenSim.Region.Framework.Scenes;
using OpenMetaverse;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    internal class ScriptXMRM : Script {
        private class ScriptTester : MarshalByRefObject {
            internal string ParameterTest(string configFile, string assembly, string clazz) {
                return TestParameters(configFile, assembly, clazz);
            }

            /// <summary>
            /// Test to make sure the supplied class and assembly information resolves to a valid MRM.
            /// </summary>
            /// <param name="assembly">The assembly where the MRM is compiled to.</param>
            /// <param name="clazz">The MRM class.</param>
            /// <returns>True if clazz is a valid MRM.</returns>
            internal static string TestParameters(string configFile, string assembly, string clazz) {
                if (assembly == null && clazz == null)
                    return ReportProblem("No assembly or class specified.\nPlease include the flags '-a', '-c'.\nAlernatively specify a config file with the '-f' flag. Add 'Assembly' and 'Class' to the 'XMRM' section of the config file.", configFile, assembly, clazz);
                if (assembly == null)
                    return ReportProblem("No assembly specified.\nPlease include the flag '-a'.\nAlernatively specify a config file with the '-f' flag. Add 'Assembly' to the 'XMRM' section of the config file.", configFile, assembly, clazz);
                if (clazz == null)
                    return ReportProblem("No class specified.\nPlease include the flag '-c'.\nAlernatively specify a config file with the '-f' flag. Add 'Class' to the 'XMRM' section of the config file.", configFile, assembly, clazz);

                if (!File.Exists(assembly))
                    return ReportProblem("Assembly  does not exist.", configFile, assembly, clazz);
                try {
                    Type t = Assembly.LoadFrom(assembly).GetType(clazz);
                    if (t == null)
                        return ReportProblem("Class does not exist in Assembly.", configFile, assembly, clazz);
                    else if (!t.IsSubclassOf(typeof(MRMBase)))
                        return ReportProblem("Class does not inherit from MRMBase.", configFile, assembly, clazz);
                    else if (!t.IsPublic)
                        return ReportProblem("Class is not public.", configFile, assembly, clazz);
                    return null;
                } catch (BadImageFormatException e) {
                    return ReportProblem("Assembly is not a valid assembly file. (" + e.Message + ").", configFile, assembly, clazz);
                }
            }

            private static string ReportProblem(string msg, string configFile, string assembly, string clazz) {
                msg = string.Format("{0}\nClass      : {2}\nAssembly   : {3}\nConfig File: {1}", msg, configFile, assembly, clazz);
                m_log.Warn(msg);
                return msg;
            }
        }

        internal const string XMRM = "XMRM";
        private const string CONFIG_FILE = "ConfigFile";
        private const string ASSEMBLY = "Assembly";
        private const string CLASS = "Class";
        private const string BASE_FOLDER = "BaseFolder";
        private const string SHADOW_COPY = "ShadowCopy";
        private const string GOD = "IsGod";

        internal override string Type { get { return XMRM; } }

        internal ScriptXMRM(string script, UUID id, uint localID, IConfigSource config, Scene scene, PolicyLevel policy, ScriptAccessor scriptAccessor, bool error2Console)
            : base(script, id, localID, config, scene, policy, scriptAccessor, error2Console) {
        }

        private bool m_isGod;

        protected override bool Config(out string assembly, out string clazz, out AppDomainSetup setup, out string[] args) {
            IConfig mrmConfig = LoadConfigValues(out args);
            assembly = mrmConfig.Get(ASSEMBLY);
            clazz = mrmConfig.Get(CLASS);
            setup = new AppDomainSetup();

            m_isGod = m_config.Configs["MRM"].GetBoolean("XMRMGodScripts", false) || mrmConfig.GetBoolean(GOD, false);

            string configFile = mrmConfig.Get(CONFIG_FILE);

            //Default base folder is the folder the config file is in if there is one or the folder the assembly file is in if there isn't.
            string defaultBaseFolder = Path.GetDirectoryName(Path.GetFullPath(configFile != null ? configFile : assembly));
            string baseFolder = mrmConfig.Get(BASE_FOLDER, defaultBaseFolder);

            //If the config file specified is relative take it relative to the default base folder.
            if (!Path.IsPathRooted(baseFolder))
                baseFolder = Path.Combine(defaultBaseFolder, baseFolder);
            if (assembly != null && configFile != null)
                assembly = Path.Combine(baseFolder, assembly);
            else if (assembly != null)
                assembly = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, assembly);

            if (!TestConfig(configFile, assembly, clazz))
                return false;
            
            if (configFile != null)
                setup.ConfigurationFile = Path.GetFullPath(configFile);
            setup.ApplicationBase = baseFolder;
            setup.ShadowCopyFiles = mrmConfig.GetBoolean(SHADOW_COPY, true).ToString().ToLower();

            return true;
        }

        protected override bool IsGod {
            get { return m_isGod; }
        }

        private bool TestConfig(string configFile, string assembly, string clazz) {
            AppDomain testDomain = AppDomain.CreateDomain("Test Domain");

            Type t = typeof(ScriptTester);
            Assembly a = t.Assembly;

            ScriptTester tester = (ScriptTester)testDomain.CreateInstanceFromAndUnwrap(a.Location, t.FullName);
            string result = tester.ParameterTest(configFile, assembly, clazz);            
            AppDomain.Unload(testDomain);

            if (result != null) {
                ErrorString = "Unable to initialise XMRM.\n" + result;
                return false;
            }
            return true;
        }

        #region Initialise Config

        /// <summary>
        /// Load in all the configuration values from the XMRM section of various configuration sources.
        /// 
        /// The order is:
        /// 1 - the Opensim application config file.
        /// 2 - opensim.ini
        /// 3 - The parameters passed in in the body of the text are parsed
        /// [4] - any application config file pointed to by one of the previous 3 configs.
        /// 
        /// Each set of config values overrides the previous so if "ShadowCopy" is set to "true" in the opensim config file, opensim.ini and
        /// in the script but "false" in the config file the final value will be false.
        /// </summary>
        /// <param name="script">The script to parse for information.</param>
        /// <returns>A config object with the values taken from the script.</returns>
        private IConfig LoadConfigValues(out string[] scriptArguments) {
            //Load in the arguments as command line arguments
            CommandLineConfig argConfig = new CommandLineConfig(ScriptText, true, "\n", " ");

            argConfig.AddSetting(XMRM, CONFIG_FILE, "f");
            argConfig.AddSetting(XMRM, CONFIG_FILE, "F");
            
            argConfig.AddSetting(XMRM, ASSEMBLY, "a");
            argConfig.AddSetting(XMRM, ASSEMBLY, "A");

            argConfig.AddSetting(XMRM, CLASS, "c");
            argConfig.AddSetting(XMRM, CLASS, "C");

            argConfig.AddSetting(XMRM, BASE_FOLDER, "b");
            argConfig.AddSetting(XMRM, BASE_FOLDER, "B");

            argConfig.AddFlag(XMRM, SHADOW_COPY, "s", false);
            argConfig.AddFlag(XMRM, SHADOW_COPY, "S", false);

            argConfig.AddFlag(XMRM, GOD, "g", false);
            argConfig.AddFlag(XMRM, GOD, "G", false);

            scriptArguments = argConfig.Argument.Length == 0 ? new string[0] : argConfig.Argument.Split(' ', '\n');

            //Merge the three guaranteed config sources
            IConfigSource config = new IniConfigSource();
            config.Merge(m_appConfig);
            config.Merge(m_config);
            config.Merge(argConfig);

            IConfig mrmConfig = config.Configs[XMRM];

            //If a config file is specified merge those values in
            if (mrmConfig.Contains(CONFIG_FILE)) {
                try {
                    IConfigSource fileSource = new DotNetConfigSource(mrmConfig.Get(CONFIG_FILE));
                    config.Merge(fileSource);
                } catch (Exception e) {
                    ErrorString = ("Unable to load config file from '" + mrmConfig.Get(CONFIG_FILE) + "'. " + e.Message);
                }
            }
            return config.Configs[XMRM];
        }

        #endregion
    }

    public class CommandLineConfig : ConfigSourceBase, IConfigSource {
        private List<string> _args = new List<string>();

        public CommandLineConfig(string argstring, bool includesProgram, params string[] splitCharacters) {
            int i = 0;
            foreach (var arg in argstring.Split(new string[] { "\"" }, StringSplitOptions.RemoveEmptyEntries)) {
                if (i % 2 == 0)
                    _args.AddRange(arg.Split(splitCharacters, StringSplitOptions.RemoveEmptyEntries));
                else
                    _args.Add(arg);
                i++;
            }

            if (includesProgram)
                _args.Remove(_args[0]);
        }

        #region IConfigSource Members

        public void AddSetting(string configName, string longName, string shortName) {
            IConfig config = GetConfig(configName);
            for (int i = 0; i < _args.Count - 1; i++) {
                string arg = _args[i];
                if (
                    ((arg.StartsWith("-") || arg.StartsWith("/")) && arg.Substring(1).Equals(shortName)) ||
                    (arg.StartsWith("--") && arg.Substring(2).Equals(longName))) {
                    config.Set(longName, _args[i + 1]);
                    _args.RemoveRange(i, 2);
                    break;
                }
            }
        }

        public void AddFlag(string configName, string longName, string shortName, bool value) {
            IConfig config = GetConfig(configName);
            for (int i = 0; i < _args.Count; i++) {
                string arg = _args[i];
                if ((arg.StartsWith("-") || arg.StartsWith("/")) && arg.Substring(1).Equals(shortName)) {
                    config.Set(longName, value);
                    _args.RemoveRange(i, 1);
                    break;
                }
            }
        }

        public string Argument {
            get {
                return _args.Count > 0 ? _args.Aggregate((sum, current) => sum + " " + current).Trim() : "";
            }
        }

        public IConfig GetConfig(string configName) {
            IConfig config = Configs[configName];
            if (config == null) {
                AddConfig(configName);
                config = Configs[configName];
            }
            return config;
        }

        #endregion
    }
}
