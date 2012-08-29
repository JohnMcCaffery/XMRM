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
using System.IO;
using System.CodeDom.Compiler;
using OpenSim.Framework;
using Microsoft.CSharp;
using OpenMetaverse;
using Nini.Config;
using System.Security.Policy;
using OpenSim.Region.Framework.Scenes;
using System.Text.RegularExpressions;
using System.Collections;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    internal class ScriptMRM : Script {
        const string ext = ".cs";
        const string FilePrefix = "MiniModule";

        private class CompiledMRM {
            internal readonly HashSet<UUID> InUseBy;
            internal readonly string Assembly;
            internal readonly string Class;

            internal CompiledMRM(string assembly, string clazz, UUID id) {
                InUseBy = new HashSet<UUID>();
                Assembly = assembly;
                Class = clazz;
                InUseBy.Add(id);
            }
        }

        internal const string MRM = "MRM";

        private static readonly CSharpCodeProvider CScodeProvider = new CSharpCodeProvider();

        private static readonly Dictionary<string, CompiledMRM> m_compiledScripts = new Dictionary<string, CompiledMRM>();

        private readonly AppDomainSetup m_setup;

        internal ScriptMRM(string script, UUID id, uint localID, IConfigSource config, Scene scene, PolicyLevel policy, ScriptAccessor scriptAccessor, bool error2Console, bool sandboxed)
            : base(script, id, localID, config, scene, policy, scriptAccessor, error2Console) {

            m_setup = config.Configs["MRM"].GetBoolean("Sandboxed", true) ? new AppDomainSetup() : null;
        }

        #region Abstract Script Members

        internal override string Type { get { return MRM; } }

        public override bool  Dispose() {
            UnReferenceSource(ScriptText);
            return base.Dispose();
        }

        protected override bool IsGod {
            get {
                return m_config.Configs["MRM"].GetBoolean("MRMGodScripts", false);
            }
        }

        protected override bool Config(out string assembly, out string clazz, out AppDomainSetup setup, out string[] args) {
            setup = m_setup;
            args = new string[0];
            if (!ScriptText.Equals(OldScript))
                UnReferenceSource(OldScript);
            //if (ReUseExisting(out assembly, out clazz)) 
            //    return true; 
            
            m_log.Debug("MRM 0");
            //ErrorString += "MRM 0";
            assembly = Step0_GetAssemblyName();

            m_log.Debug("MRM 1");
            //ErrorString += "MRM 1";
            clazz = Step1_GetClassName();
            if (clazz == null) 
                return false;

            m_log.Debug("MRM 2");
            //ErrorString += "MRM 2";
            if (!Step2_ClearOldAssembly(assembly))
                return false;

            m_log.Debug("MRM 3");
            //ErrorString += "MRM 3";
            if (!Step3_WriteSource(assembly)) 
                return false;

            m_log.Debug("MRM 4");
            //ErrorString += "MRM 4";
            CompilerParameters parameters = Step4_MakeParameters(assembly);

            m_log.Debug("MRM 5");
            //ErrorString += "MRM 5";
            CompilerResults results = Step5_Compile(parameters);            

            m_log.Debug("MRM 6");
            //ErrorString += "MRM 6";
            if (!Step6_CheckCompileResults(assembly, results)) {
                ErrorString = "Compiler found errors. \n" + ErrorString;
                return false;
            }
         
            m_log.Debug("MRM 7");
            //ErrorString += "MRM 7";
            Byte[] data = Step7_GetCompiledByteCode(assembly);
            if (data == null) 
                return false;
            
            m_log.Debug("MRM 8");
            //ErrorString += "MRM 8";
            Byte[] buf = Step8_ConvertToBase64(data);
            
            m_log.Debug("MRM 9");
            //ErrorString += "MRM 9";
            Step9_WriteTo64BitLib(assembly, buf);            

            m_log.Debug("MRM 10");
            //ErrorString += "MRM 10";

            SaveExisting(assembly, clazz);
            return true;
        }

        #endregion

        #region Compile Steps

        private string Step0_GetAssemblyName() {
            // Output assembly name
            string assembly = Path.Combine("MiniModules", Path.Combine(
                                                             m_scene.RegionInfo.RegionID.ToString(),
                                                             FilePrefix + "_compiled_" + ID.ToString() + "_" +
                                                             Util.RandomClass.Next(9000) + ".dll"));

            // Create Directories for Assemblies
            if (!Directory.Exists("MiniModules"))
                Directory.CreateDirectory("MiniModules");
            string tmp = Path.Combine("MiniModules", m_scene.RegionInfo.RegionID.ToString());
            if (!Directory.Exists(tmp))
                Directory.CreateDirectory(tmp);

            return assembly;
        }

        /// <summary>
        /// TODO fix this
        /// 
        /// Needs to iterate through every namespace until it finds a class that extends MRMBase
        /// </summary>
        /// <returns></returns>
        private string Step1_GetClassName() {
            //Check that there is a class which extends MRMBase
            string clazz = null;
            if (!Regex.IsMatch(ScriptText, "namespace.*{")) {
                ErrorString = "Unable to compile MRM. No namespace definition found within the code.";
                return null;
            }
            foreach (var nsMatch in Regex.Matches(ScriptText, "namespace.*{")) {
                Match classMatch = Regex.Match(ScriptText, "class.*: MRMBase");
                if (classMatch.Success) {
                    string ns = Regex.Match(ScriptText, "namespace.*{").Value.Replace("namespace", "").Replace("{", "").Trim();
                    string className = classMatch.Value.Replace("class", "").Replace(":", "").Replace("MRMBase", "").Trim();
                    clazz = ns + "." + className;
                }
            }
            if (clazz == null) {
                ErrorString = "Unable to compile MRM. No class found which extends MRMBase.";
            }

            return clazz;
        }

        private bool Step2_ClearOldAssembly(string assembly) {
            try {
                File.Delete(assembly);
                return true;
            } catch (UnauthorizedAccessException e) {
                ErrorString = "Unable to delete old existing " +
                                "script-file before writing new. Compile aborted: " +
                                e;
                return false;
            } catch (IOException e) {
                ErrorString = "Unable to delete old existing "+
                                "script-file before writing new. Compile aborted: " +
                                e;
                return false;
            }
        }

        private bool Step3_WriteSource(string assembly) {
            // DEBUG - write source to disk
            string srcFileName = FilePrefix + "_source_" +
                                 Path.GetFileNameWithoutExtension(assembly) + ext;
            try {
                File.WriteAllText(Path.Combine(Path.Combine(
                                                   "MiniModules",
                                                   m_scene.RegionInfo.RegionID.ToString()),
                                               srcFileName), ScriptText);
                return true;
            } catch (Exception ex) //NOTLEGIT - Should be just FileIOException
            {
                ErrorString = "[Compiler]: Exception while " + 
                        "trying to write script source to file \"" +
                        srcFileName + "\": " + ex;
                return false;
            }
        }

        private CompilerParameters Step4_MakeParameters(string assembly) {
            // Do actual compile
            CompilerParameters parameters = new CompilerParameters();

            parameters.IncludeDebugInformation = true;

            string rootPath = Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory);

            List<string> libraries = new List<string>();
            string[] lines = ScriptText.Split(new string[] { "\n" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in lines) {
                if (s.StartsWith("//@DEPENDS:")) {
                    libraries.Add(s.Replace("//@DEPENDS:", ""));
                }
            }

            libraries.Add(Path.GetFileName(typeof(MRMBase).Assembly.Location));
            libraries.Add("OpenSim.Region.OptionalModules.dll");
            libraries.Add("OpenMetaverseTypes.dll");
            libraries.Add("log4net.dll");

            foreach (string library in libraries) {
                parameters.ReferencedAssemblies.Add(Path.Combine(rootPath, library));
            }

            parameters.GenerateExecutable = false;
            parameters.OutputAssembly = assembly;
            parameters.IncludeDebugInformation = true;
            parameters.TreatWarningsAsErrors = false;

            return parameters;
        }

        private CompilerResults Step5_Compile(CompilerParameters parameters) {
            return CScodeProvider.CompileAssemblyFromSource(parameters, ScriptText);
        }

        private bool Step6_CheckCompileResults(string assembly, CompilerResults results) {
            int display = 5;
            if (results.Errors.Count > 0) {
                string errtext = String.Empty;
                foreach (CompilerError CompErr in results.Errors) {
                    // Show 5 errors max
                    //
                    if (display <= 0)
                        break;
                    display--;

                    string severity = "Error";
                    if (CompErr.IsWarning) {
                        severity = "Warning";
                    }

                    string text = CompErr.ErrorText;

                    // The Second Life viewer's script editor begins
                    // counting lines and columns at 0, so we subtract 1.
                    errtext += String.Format("Line ({0},{1}): {4} {2}: {3}\n",
                                             CompErr.Line - 1, CompErr.Column - 1,
                                             CompErr.ErrorNumber, text, severity);
                }

                if (!File.Exists(assembly)) {
                    ErrorString += errtext;
                    return false;
                }
            }
            return true;
        }

        private byte[] Step7_GetCompiledByteCode(string assembly) {
            if (!File.Exists(assembly)) {
                string errtext = String.Empty;
                ErrorString = "No compile error. But not able to locate compiled file.";
                return null;
            }

            FileInfo fi = new FileInfo(assembly);

            Byte[] data = new Byte[fi.Length];

            try {
                FileStream fs = File.Open(assembly, FileMode.Open, FileAccess.Read);
                fs.Read(data, 0, data.Length);
                fs.Close();
                return data;
            } catch (IOException) {
                string errtext = String.Empty;
                ErrorString = "No compile error. But not able to locate compiled file.";
                return null;
            }
        }

        private Byte[] Step8_ConvertToBase64(byte[] data) {
            // Convert to base64
            //
            string filetext = Convert.ToBase64String(data);

            ASCIIEncoding enc = new ASCIIEncoding();

            return enc.GetBytes(filetext);
        }

        private void Step9_WriteTo64BitLib(string assembly, byte[] buf) {
            FileStream sfs = File.Create(assembly + ".cil.b64");
            sfs.Write(buf, 0, buf.Length);
            sfs.Close();
        }

        #endregion 

        #region Util

        private void UnReferenceSource(string scriptText) {
            if (scriptText != null && m_compiledScripts.ContainsKey(scriptText)) {
                //Remove this asset from the list of assets that use the compilation of the old text of the script.
                CompiledMRM oldScript = m_compiledScripts[scriptText];
                oldScript.InUseBy.Remove(ID);
                //If there are no longer any scripts compiled from the text of the old script no longer track that compilation.
                if (oldScript.InUseBy.Count == 0)
                    m_compiledScripts.Remove(scriptText);
            }
        }

        private bool ReUseExisting(out string assembly, out string clazz)  {
            assembly = null;
            clazz = null;
            if (m_compiledScripts.ContainsKey(ScriptText)) {
                //If there is already a script that has been compiled from this exact code re-use that.
                CompiledMRM mrm = m_compiledScripts[ScriptText];
                clazz = mrm.Class;
                assembly = mrm.Assembly;
                mrm.InUseBy.Add(ID);
                return true;
            }
            return false;   
        }

        private void SaveExisting(string assembly, string clazz) {
            if (!m_compiledScripts.ContainsKey(ScriptText)) {
                //If there is already a script that has been compiled from this exact code re-use that.
                m_compiledScripts[ScriptText] = new CompiledMRM(assembly, clazz, ID);
            }
        }

        #endregion
    }
}
