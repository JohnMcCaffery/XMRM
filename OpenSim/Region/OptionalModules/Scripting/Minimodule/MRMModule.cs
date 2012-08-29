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
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text;
using System.Linq;
using log4net;
using Microsoft.CSharp;
using Nini.Config;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Interfaces;
using OpenSim.Region.Framework.Scenes;
using OpenSim.Services.Interfaces;
using Amib.Threading;
using System.Threading;
using System.Xml;
using OpenSim.Framework.Console;
using Mono.Addins;
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    [Extension(Path = "/OpenSim/RegionModules", NodeName = "RegionModule", Id = "MRMModule")]
    public class MRMModule : INonSharedRegionModule, IMRMModule, IScriptModule {
        private const string MRMFlag = "//MRM:C#";
        private const string XMRMFlag = "//MRM:X";
        private const string ALL = "all";
        private const string NONE = "none";
        

        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Dictionary<Type, object> m_extensions = new Dictionary<Type, object>();
        private readonly Dictionary<UUID, string> m_currentScripts = new Dictionary<UUID, string>();
        private readonly MicroScheduler m_microthreads = new MicroScheduler();

        private readonly EventManager.NewRezScript m_rezListener;
        private readonly EventManager.ScriptResetDelegate m_resetListener;
        private readonly EventManager.RemoveScript m_removeListener;
        private readonly EventManager.ChatFromClientEvent m_chatListener;
        private readonly CommandDelegate m_commandListener;
        
        private readonly object m_startLock = new object();

        private Scene m_scene;

        private IConfigSource m_config;

        private IConfig m_mrmConfig;
        private PolicyLevel m_mrmPolicy, m_xmrmPolicy;
        private string m_currentRegion;
        private bool m_mrmEnabled, m_xmrmEnabled;
        private bool m_sandboxed, m_hidden;
        private bool m_error2Console;

        private ScriptAccessor m_scripts;
        private SmartThreadPool m_threadPool;

        public MRMModule() {
            m_rezListener = OnScriptRez;
            m_resetListener = OnResetScript;
            m_removeListener = OnRemoveScript;
            m_chatListener = OnChatFromClient;
            m_commandListener = OnCommand;
            Script.m_microthreads = m_microthreads;
        }

        #region IMRMModule Members

        public void RegisterExtension<T>(T instance) {
            Type t = typeof(T);
            if (m_extensions.ContainsKey(t))
                m_extensions[t] = instance;
            else
                m_extensions.Add(t, instance);
        }

        public void InitializeMRM(MRMBase mmb, uint localID, UUID itemID) {
            if (!m_mrmEnabled)
                throw new UnauthorizedAccessException("Unable to initialise MRMBase. MRMs are not enabled.");
            IHost host;
            IWorld world;
            GetGlobalEnvironment(localID, out world, out host);
            mmb.InitMiniModule(world, host, itemID);
        }

        private class BasicSponsor : ISponsor {
            internal static bool m_live;
            #region ISponsor Members

            public TimeSpan Renewal(ILease lease) {
                return m_live ? lease.RenewOnCallTime : TimeSpan.Zero;
            }

            #endregion
        }

        public void GetGlobalEnvironment(uint localID, out IWorld world, out IHost host) {
            if (!m_mrmEnabled)
                throw new UnauthorizedAccessException("Unable to initialise MRM global environment. MRMs are not enabled.");
            SceneObjectPart hostSOP = m_scene.GetSceneObjectPart(localID);
            UserAccount god = m_scene.UserAccountService.GetUserAccount(UUID.Zero, hostSOP.OwnerID);

            ISponsor sponsor = new BasicSponsor();
            SEUser securityUser = new SEUser(god.PrincipalID, god.Name, sponsor);
            SecurityCredential creds = new SecurityCredential(securityUser, m_scene);

            world = new World(m_scene, creds, false);
            host = new Host(world.Objects[localID], m_scene, new ExtensionHandler(m_extensions, sponsor), m_microthreads);
        }

        #endregion

        #region Events

        void OnScriptRez(uint localID, UUID itemID, string scriptText, int startParam, bool postOnRez, string engine, int stateSource) {
            if (!m_scripts.ContainsKey(itemID) &&
                ((scriptText.StartsWith(MRMFlag) && m_mrmEnabled) ||
                (scriptText.StartsWith(XMRMFlag) && m_xmrmEnabled))) {
                //No MRM script is registed under this ID and the script is an MRM script
                if (scriptText.StartsWith(MRMFlag))
                    new ScriptMRM(scriptText, itemID, localID, m_config, m_scene, m_mrmPolicy, m_scripts, m_error2Console, m_sandboxed);
                else
                    new ScriptXMRM(scriptText, itemID, localID, m_config, m_scene, m_mrmPolicy, m_scripts, m_error2Console);
            } else if (m_scripts.ContainsKey(itemID)) {
                Script script = m_scripts[itemID];
                if (!scriptText.StartsWith(MRMFlag) && !scriptText.StartsWith(XMRMFlag))
                    //Script is now not an MRM script - remove it
                    script.Dispose();
                else if (scriptText.StartsWith(MRMFlag) && script.Type.Equals(ScriptXMRM.XMRM)) {
                    //Script was an MRM and is now an XMRM - Remove the old one and start a new one
                    script.Dispose();
                    new ScriptMRM(scriptText, itemID, localID, m_config, m_scene, m_mrmPolicy, m_scripts, m_error2Console, m_sandboxed);
                } else if (scriptText.StartsWith(XMRMFlag) && script.Type.Equals(ScriptMRM.MRM)) {
                    //Script was an XMRM and is now an MRM - Remove the old one and start a new one
                    script.Dispose();
                    new ScriptXMRM(scriptText, itemID, localID, m_config, m_scene, m_mrmPolicy, m_scripts, m_error2Console);
                } else
                    //None of the above - change the text
                    script.ScriptText = scriptText;
            }
        }

        void OnResetScript(uint localID, UUID itemID) {
            if (m_scripts.ContainsKey(itemID)) 
                QueueWork(() => m_scripts[itemID].Reset());
        }

        void OnRemoveScript(uint localID, UUID itemID) {
            Console.WriteLine("Script stopped.");
            if (m_scripts.ContainsKey(itemID))
                lock (m_startLock) {
                    m_scripts[itemID].Dispose();
                    OnScriptRemoved(itemID);
                }
        }

        void OnChatFromClient(object sender, OSChatMessage chat) {
            string[] cmd = chat.Message.Split(' ');
            if (cmd.Length > 0 && cmd[0].ToUpper().Equals("XMRM"))
                RunCommand(cmd, chat.Sender.AgentId);
        }

        private void OnCommand(string module, string[] cmd) {
            RunCommand(cmd, UUID.Zero);
        }

        void OnFrame() {
            m_microthreads.Tick(1000);
            foreach (var script in m_scripts)
                script.Tick(1000);
        }

        #endregion

        #region Static Util

        private static SmartThreadPool GetThreadPool(IConfig m_ScriptConfig) {
            int minThreads = m_ScriptConfig.GetInt("MinThreads", 2);
            int maxThreads = m_ScriptConfig.GetInt("MaxThreads", 100);
            int idleTimeout = m_ScriptConfig.GetInt("IdleTimeout", 60);
            int stackSize = m_ScriptConfig.GetInt("ThreadStackSize", 262144);
            int maxScriptQueue = m_ScriptConfig.GetInt("MaxScriptEventQueue", 300);

            string priority = m_ScriptConfig.GetString("Priority", "BelowNormal");

            ThreadPriority threadPriority = ThreadPriority.BelowNormal;
            switch (priority) {
                case "Lowest":
                    threadPriority = ThreadPriority.Lowest;
                    break;
                case "BelowNormal":
                    threadPriority = ThreadPriority.BelowNormal;
                    break;
                case "Normal":
                    threadPriority = ThreadPriority.Normal;
                    break;
                case "AboveNormal":
                    threadPriority = ThreadPriority.AboveNormal;
                    break;
                case "Highest":
                    threadPriority = ThreadPriority.Highest;
                    break;
                default:
                    m_log.ErrorFormat("[XEngine] Invalid thread priority: '{0}'. Assuming BelowNormal", priority);
                    break;
            }

            //m_maxScriptQueue = maxScriptQueue;

            STPStartInfo startInfo = new STPStartInfo();
            startInfo.IdleTimeout = idleTimeout * 1000; // convert to seconds as stated in .ini
            startInfo.MaxWorkerThreads = maxThreads;
            startInfo.MinWorkerThreads = minThreads;
            startInfo.ThreadPriority = threadPriority; ;
            startInfo.StackSize = stackSize;
            startInfo.StartSuspended = false;

            return new SmartThreadPool(startInfo);
        }

        private static string ConvertMRMKeywords(string script) {
            script = script.Replace("microthreaded void", "IEnumerable");
            script = script.Replace("relax;", "yield return null;");

            return script;
        }

        /// <summary>
        /// Create an app domain policy restricting code to execute
        /// with only the permissions granted by a named permission set
        /// </summary>
        /// <param name="permissionSetName">name of the permission set to restrict to</param>
        /// <param name="appDomainName">'friendly' name of the appdomain to be created</param>
        /// <exception cref="ArgumentNullException">
        /// if <paramref name="permissionSetName"/> is null
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">
        /// if <paramref name="permissionSetName"/> is empty
        /// </exception>
        /// <returns>AppDomain with a restricted security policy</returns>
        /// <remarks>Substantial portions of this function from: http://blogs.msdn.com/shawnfa/archive/2004/10/25/247379.aspx
        /// Valid permissionSetName values are:
        /// * FullTrust
        /// * SkipVerification
        /// * Execution
        /// * Nothing
        /// * LocalIntranet
        /// * Internet
        /// * Everything
        /// </remarks>
        private static PolicyLevel GetAppDomainPolicy(string permissionSetName) {
            if (permissionSetName == null)
                throw new ArgumentNullException("permissionSetName");
            if (permissionSetName.Length == 0)
                throw new ArgumentOutOfRangeException("permissionSetName", permissionSetName,
                                                        "Cannot have an empty permission set name");

            // Default to all code getting nothing
            PolicyStatement emptyPolicy = new PolicyStatement(new PermissionSet(PermissionState.None));
            UnionCodeGroup policyRoot = new UnionCodeGroup(new AllMembershipCondition(), emptyPolicy);

            bool foundName = false;
            PermissionSet setIntersection = new PermissionSet(PermissionState.Unrestricted);

            // iterate over each policy level
            IEnumerator levelEnumerator = SecurityManager.PolicyHierarchy();
            while (levelEnumerator.MoveNext()) {
                PolicyLevel level = levelEnumerator.Current as PolicyLevel;

                // if this level has defined a named permission set with the
                // given name, then intersect it with what we've retrieved
                // from all the previous levels
                if (level != null) {
                    PermissionSet levelSet = level.GetNamedPermissionSet(permissionSetName);
                    if (levelSet != null) {
                        foundName = true;
                        if (setIntersection != null)
                            setIntersection = setIntersection.Intersect(levelSet);
                    }
                }
            }

            // Intersect() can return null for an empty set, so convert that
            // to an empty set object. Also return an empty set if we didn't find
            // the named permission set we were looking for
            if (setIntersection == null || !foundName)
                setIntersection = new PermissionSet(PermissionState.None);
            else
                setIntersection = new NamedPermissionSet(permissionSetName, setIntersection);

            // if no named permission sets were found, return an empty set,
            // otherwise return the set that was found
            PolicyStatement permissions = new PolicyStatement(setIntersection);
            policyRoot.AddChild(new UnionCodeGroup(new AllMembershipCondition(), permissions));

            // create an AppDomain policy level for the policy tree
            PolicyLevel appDomainLevel = PolicyLevel.CreateAppDomainLevel();
            appDomainLevel.RootCodeGroup = policyRoot;

            return appDomainLevel;
        }

        #endregion

        #region Commands

        private void RunCommand(string[] cmd, UUID id) {
            if (m_currentRegion != null && !m_currentRegion.Equals(m_scene.RegionInfo.RegionName))
                return;
            if (!m_currentScripts.ContainsKey(id))
                m_currentScripts.Add(id, null);
            if (cmd.Length < 2) {
                m_log.Warn("[XMRM]: Ignoring XMRM command. No command specified.");
                return;
            }
            string command = cmd[1];
            try {
                switch (command.ToLower()) {
                    case "select": Select(cmd, id); break;
                    case "restart": ResetScript(cmd.Length > 2 ? cmd[2] : m_currentScripts[id], id); break;
                    case "stop": StopScript(cmd.Length > 2 ? cmd[2] : m_currentScripts[id], id); break;
                    case "dispose": DisposeScript(cmd.Length > 2 ? cmd[2] : m_currentScripts[id], id); break;
                    case "start": StartScript(cmd.Length > 2 ? cmd[2] : m_currentScripts[id], id); break;
                    case "list": ListScripts(id); break;
                    case "region": m_currentRegion = cmd.Length > 2 ? cmd[2] : null; break;
                    case "stats": PrintStats(); break;
                    default: m_log.Warn(command + " is not a valid XMRM command. Ignoring."); break;
                }
            } catch (Exception e) {
                m_log.Info("[MRM] Error: " + e);
                Console.WriteLine(e.StackTrace);
                m_scene.ForEachClient(delegate(IClientAPI user) {
                    user.SendAlertMessage(
                        "Compile error while building MRM script, check OpenSim console for more information.");
                });
            }
        }

        private void PrintStats() {
            GC.Collect();
            Console.WriteLine(m_scene.RegionInfo.RegionName + " stats for " + m_scripts.Count() + " scripts:");
            Console.WriteLine("Worlds    - Total: {0,10} - Current {1}", World.TotalCount, World.CurrentCount);
            Console.WriteLine("Objects   - Total: {0,10} - Current {1}", SOPObject.TotalCount, SOPObject.CurrentCount);
            Console.WriteLine("Materials - Total: {0,10} - Current {1}", SOPObjectMaterial.TotalCount, SOPObjectMaterial.CurrentCount);
        }

        private void Select(string[] cmd, UUID id) {
            if (cmd.Length < 3) {
                m_log.Warn("[XMRM]: Ignoring XMRM select command. No script specified.");
                return;
            }
            string script = cmd[2];
            for (int i = 3; i < cmd.Length; i++)
                script += " " + cmd[i];
            if (script.ToLower().Equals(ALL)) {
                m_currentScripts[id] = ALL;
                m_log.Warn("[MRM]: All scripts will be affected.");
            } else if (script.ToLower().Equals(NONE)) {
                m_currentScripts[id] = null;
                m_log.Warn("[MRM]: Default script disabled.");
            } else if (m_scripts.IsScript(script)) {
                m_currentScripts[id] = script;
                m_log.Warn("[MRM]: '" + script + "' selected.");
            } else {
                m_log.Warn("[MRM]: Unable to select script. '" + script + "' is not a known script.");
                m_currentScripts[id] = null;
            }
        }

        private void ResetScript(string scriptName, UUID id) {
            if (scriptName == null)
                return;

            if (scriptName.ToLower().Equals(ALL))
                foreach (var script in m_scripts)
                    ResetScript(script.Name, id);
            else if (m_scripts.IsScript(scriptName))
                QueueWork(() =>m_scripts[scriptName].Reset());
            else
                m_log.Warn("[XMRM]: Unable to restart script. '" + scriptName + "' is not a known script.");
        }

        private void StopScript(string scriptName, UUID id) {
            if (scriptName == null)
                return;

            if (scriptName.ToLower().Equals(ALL))
                foreach (var script in m_scripts)
                    StopScript(script.Name, id);
            else if (m_scripts.IsScript(scriptName))
                m_scripts[scriptName].Stop(id);
            else
                m_log.Warn("[XMRM]: Unable to stop script. '" + scriptName + "' is not a known script.");
        }

        private void DisposeScript(string scriptName, UUID id) {
            if (scriptName == null)
                return;

            if (scriptName.ToLower().Equals(ALL))
                foreach (var script in m_scripts)
                    DisposeScript(script.Name, id);
            else if (m_scripts.IsScript(scriptName))
                m_scripts[scriptName].Dispose();
            else
                m_log.Warn("[XMRM]: Unable to stop script. '" + scriptName + "' is not a known script.");
        }

        private void StartScript(string scriptName, UUID id) {
            if (scriptName == null)
                return;

            if (scriptName.ToLower().Equals(ALL))
                foreach (var script in m_scripts)
                    StartScript(script.Name, id);
            else if (m_scripts.IsScript(scriptName))
                QueueWork(() => m_scripts[scriptName].Start(id));
            else
                m_log.Warn("[XMRM]: Unable to start script. " + scriptName + " is not a known script.");
        }

        private void ListScripts(UUID id) {
            int count = m_scripts.Count();
            string list = count + " MRM scripts running in " + m_scene.RegionInfo.RegionName + ".";
            if (count > 0)
                list += string.Format("\n{0,-70} {1,-15} {2}", "Name", "Owner", "Running?");
            foreach (var script in m_scripts)
                if (script.Owner.PrincipalID.Equals(id) || id.Equals(UUID.Zero))
                    list += string.Format("\n{0,-70} {1,-15} {2}", script.Name, script.Owner.Name, script.IsRunning);
            m_log.Debug(list);
        }

        #endregion

        #region IRegionModuleBase Members

        public string Name {
            get { return "MiniRegionModule"; }
        }

        public void Initialise(IConfigSource source) {
            m_config = source;
            m_mrmConfig = source.Configs["MRM"];
            m_mrmEnabled = m_mrmConfig != null && m_mrmConfig.GetBoolean("MRMEnabled", false);
            m_xmrmEnabled = m_mrmEnabled && m_mrmConfig.GetBoolean("XMRMEnabled", false);
            if (m_mrmEnabled) {
                m_error2Console = m_mrmConfig.GetBoolean("ErrorToConsole", true);
                m_scripts = new ScriptAccessor();
                m_sandboxed = m_mrmConfig.GetBoolean("Sandboxed", true);
                m_hidden = m_mrmConfig.GetBoolean("Hidden", false);
                m_mrmPolicy = m_sandboxed ? GetAppDomainPolicy(m_mrmConfig.Get("MRMSandboxLevel", "Internet")) : null;
                if (m_xmrmEnabled)
                    m_xmrmPolicy = m_sandboxed ? GetAppDomainPolicy(m_mrmConfig.Get("XMRMSandboxLevel", "Everything")) : null;
                m_threadPool = GetThreadPool(m_mrmConfig);
            }
        }

        public void AddRegion(Scene scene) {
            if (m_mrmEnabled) {
                m_scene = scene;
                string name = scene.RegionInfo.RegionName;
                scene.EventManager.OnRezScript += m_rezListener;
                scene.AddCommand(this, "MRM",
                    "MRM <command> [arg] - The valid MRM commands are 'Select', 'Start', 'Restart', 'Stop', 'List' and 'Region'.",
                    "MRM <command> <arg> - Commands that will affect MRMs. <sript> and region can either be names or '" + ALL + "' or '" + NONE + "'.\n" +
                    "MRM list - List all scripts currently initialised.\n" +
                    "MRM reset [script] - Shutdown then restart a script. If [script] is not specified the selected script is reset.\n" +
                    "MRM select <script> - Sets w.Add(name, (module, command) => OnCommand(module, command, scene))hich script is currently being affected by the other commands.\n" +
                    "MRM start [script] - Start a script. If [script] is not specified the selected script is shutdown. If the script is already running nothing happens.\n" +
                    "MRM stop [script] - Shutdown a script. If [script] is not specified the selected script is shutdown.\n" +
                    "MRM region <region> - Set the region in which scripts will be affected by the other commands.",
                    m_commandListener);

                scene.EventManager.OnFrame += OnFrame;
                scene.StackModuleInterface<IScriptModule>(this);
                scene.RegisterModuleInterface<IMRMModule>(this);
            }
        }

        public void RegionLoaded(Scene scene) {
            if (m_mrmEnabled) {
                // when hidden, we don't listen for client initiated script events
                // only making the MRM engine available for region modules
                if (!m_hidden) {
                    scene.EventManager.OnScriptReset += m_resetListener;
                    scene.EventManager.OnRemoveScript += m_removeListener;
                    scene.EventManager.OnChatFromClient += m_chatListener;
                }

                m_log.Info("[MRM]: Loaded region '" + scene.RegionInfo.RegionName + "'.");            
            }
        }

        public void RemoveRegion(Scene scene) {
            scene.EventManager.OnRezScript -= m_rezListener;
            scene.EventManager.OnScriptReset -= m_resetListener;
            scene.EventManager.OnRemoveScript -= m_removeListener;
            scene.EventManager.OnChatFromClient -= m_chatListener;
        }

        public void Close() {
            foreach (Script script in m_scripts) {
                try {
                    script.Dispose();
                } catch (Exception e) {
                    m_log.Info("[MRM] Error: " + e);
                    Console.WriteLine(e.StackTrace);
                }
            }
            BasicSponsor.m_live = false;
        }

        public Type ReplaceableInterface {
            get { return null; }
        }

        #endregion
        
        #region IScriptModule Members

        public event ObjectRemoved OnObjectRemoved;
        public event ScriptRemoved OnScriptRemoved;

        public Dictionary<uint, float> GetObjectScriptsExecutionTimes() {
            Dictionary<uint, float> ret = new Dictionary<uint,float>();
            foreach (var script in m_scripts)
                ret.Add(script.LocalID, script.Time);
            return ret;
        }

        public ArrayList GetScriptErrors(UUID itemID) {
            Thread.Sleep(500);
            if (!m_scripts.ContainsKey(itemID) || m_scripts[itemID].IsRunning)
                return new ArrayList();
            else
                return m_scripts[itemID].Errors;
        }

        public string GetXMLState(UUID itemID) {
            return "";
        }

        public bool PostObjectEvent(UUID sogID, string name, object[] args) {
            return false;
        }

        public bool PostScriptEvent(UUID sogID, string name, object[] args) {
            return false;
        }

        public void ResumeScript(UUID itemID) {
            if (m_scripts.ContainsKey(itemID)) {
                QueueWork(() => {
                    lock (m_startLock)
                        return m_scripts[itemID].Start();
                });
            }
        }

        public void SaveAllState() {
            //throw new NotImplementedException();
        }

        public string ScriptEngineName {
            get { return "MiniRegionModule"; }
        }

        public bool SetXMLState(UUID itemID, string xml) {
            return false;
        }

        public void StartProcessing() {
            m_threadPool.Start();
            Console.WriteLine("Starting processing scripts.");
        }

        public void SuspendScript(UUID sogID) {
        }

        #endregion

        private void QueueWork(Func<object> work) {
            //Console.WriteLine("\n" + new StackTrace() + "\n");
            //Func<object> safeWork = () => {
            //    try {
            //        return work();
            //    } catch (UnauthorizedAccessException e) {
            //        m_log.Error("[MRM] UAE " + e.Message);
            //        m_log.Error("[MRM] " + e.StackTrace);

            //        if (e.InnerException != null)
            //            m_log.Error("[MRM] " + e.InnerException);

            //        m_scene.ForEachClient(delegate(IClientAPI user) {
            //            user.SendAlertMessage(
            //                "MRM UnAuthorizedAccess: " + e);
            //        });
            //    } catch (Exception e) {
            //        m_log.Info("[MRM] Error: " + e);
            //        Console.WriteLine(e.StackTrace);
            //        m_scene.ForEachClient(delegate(IClientAPI user) {
            //            user.SendAlertMessage(
            //                "Compile error while building MRM script, check OpenSim console for more information.");
            //        });
            //    }
            //    return null;
            //};
            m_threadPool.QueueWorkItem(state => work());
            //Util.FireAndForget(state => work());
            //new Thread(safeWork).Start();
            //safeWork();
        }
    }
}
