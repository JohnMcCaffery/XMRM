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
using OpenSim.Region.OptionalModules.Scripting.Minimodule;
using OpenMetaverse;
using OpenSim.Region.Framework.Scenes;
using log4net;
using System.Reflection;
using System.IO;
using System.Security.Policy;
using OpenSim.Services.Interfaces;
using OpenSim.Framework;
using System.Threading;
using Nini.Config;
using OpenSim.Region.OptionalModules.Scripting.Minimodule.API.Sandboxed;
using Amib.Threading;
using System.Collections;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    internal abstract class Script : KillableProxy {
        private const int STOP_TIMEOUT = 30000;

        #region Static Readonly Variables

        protected static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        protected static readonly IConfigSource m_appConfig = new DotNetConfigSource(AppDomain.CurrentDomain.SetupInformation.ConfigurationFile);

        #endregion

        #region Static Variables

        internal static Dictionary<Type, object> m_extensions = new Dictionary<Type, object>();
        internal static MicroScheduler m_microthreads;

        #endregion

        #region Private Fields

        private readonly string m_name;
        private readonly bool m_error2Console;
        private UserAccount m_god;
        private string m_script, m_oldScript;

        //Initialised variables
        private string m_class;
        private string m_assembly;
        private string[] m_args;
        private AppDomainSetup m_domainSetup;
        private bool m_initialised;

        //Runtime variables
        private AppDomain m_appDomain = null;
        private World m_world = null;
        private Host m_host = null;
        private Root m_root = null;
        private MRMBase m_mrm;
        private bool m_running;
        private float m_time = 0f;
        private DateTime m_start;
        private bool m_stopping;
        private ArrayList m_errors;

        //Basic variables
        protected readonly IConfigSource m_config;
        protected readonly Scene m_scene;
        private readonly SceneObjectPart m_hostSOP;
        private readonly ScriptAccessor m_scriptAccessor;
        private readonly TaskInventoryItem m_asset;
        private readonly PolicyLevel m_policy;
        private readonly SecurityCredential m_creds;

        #endregion

        #region Protected Properties

        protected virtual bool IsGod {
            get { return false; }
        }

        protected string ErrorString {
            get {
                return m_errors.ToArray().Aggregate<object, string>("", (sum, current) => sum + current + "\n").TrimEnd('\n');
            }
            set {
                m_errors = new ArrayList(value.Split('\n').ToList());
            }
        }

        protected string OldScript { get { return m_oldScript; } }

        #endregion

        #region Internal Properties

        internal abstract string Type { get; }
        internal UUID ID { get { return m_asset.ItemID; } }
        internal string Name { get { return m_name; } }
        internal bool IsRunning { get { return m_running; } }
        internal bool IsInitialised { get { return m_initialised; } }
        internal UserAccount Owner { get { return m_god; } }
        internal ArrayList Errors { get { return m_errors; } }
        internal uint LocalID { get { return m_hostSOP.LocalId; } }
        internal float Time { get { return IsRunning ? (float) DateTime.Now.Subtract(m_start).TotalMilliseconds : m_time; } }
        internal string ScriptText { 
            get { return m_script; }
            set {
                if (value == null || value.Equals(m_script))
                    return;
                ErrorString = "";
                m_oldScript = m_script;
                m_script = value;
                m_initialised = Config(out m_assembly, out m_class, out m_domainSetup, out m_args);
                if (m_initialised)
                    m_log.Warn("[" + Type + "] Configured " + Type + " " + Name + ".");
                else if (!m_error2Console)
                    m_log.Warn("[" + Type + "] Unable to configure " + Name + ".\n" + Errors[0]);
                else {
                    m_log.Warn("[" + Type + "] Unable to configure " + Name + ".\n");
                    m_log.Debug(ErrorString);
                }
            }
        }

        #endregion

        #region Init

        #region Constructor

        internal Script(string script, UUID scriptID, uint localID, IConfigSource config, Scene scene, PolicyLevel policy, ScriptAccessor scriptAccessor, bool error2Console) {
            m_hostSOP = scene.GetSceneObjectPart(localID);
            m_asset = m_hostSOP.Inventory.GetInventoryItem(scriptID);
            m_god = scene.UserAccountService.GetUserAccount(UUID.Zero, m_hostSOP.OwnerID);

            m_name = (m_god.Name + "." + m_hostSOP.Name + "." + m_asset.Name).Replace(" ", "");
            m_name = scriptAccessor.AddScript(this);

            m_scene = scene;
            m_config = config;
            m_policy = policy;
            m_error2Console = config.Configs["MRM"].GetBoolean("ErrorToConsole", true);
            m_scriptAccessor = scriptAccessor;
            m_errors = new ArrayList();

            SEUser securityUser = new SEUser(m_god.PrincipalID, m_god.Name, this);
            m_creds = new SecurityCredential(securityUser, m_scene);

            ScriptText = script;
        }

        #endregion

        /// <summary>
        /// Configure the system so that class points to a valid assembly and clazz is a class within that assembly that extends MRMBase.
        /// </summary>
        /// 
        /// <param name="clazz">The class which extends MRMBase and is to be loaded from assembly.</param>
        /// <param name="assembly">The assembly where the class to load is.</param>
        /// <param name="setup">Setup information for the application domain to be created. If this is null assembly and class will be loaded in the same application domain as opensim.</param>
        /// <returns>True if there was enough information to successfully configure the system.</returns>
        protected abstract bool Config(out string assembly, out string clazz, out AppDomainSetup setup, out string[] args);

        public override object InitializeLifetimeService() {
            return null;
        }

        #endregion

        #region Work

        #region Tick

        internal void Tick(int count) {
            if (IsRunning && m_root != null && !m_stopping)
                m_root.Tick(count);
            else if (IsRunning && !m_stopping)
                m_microthreads.Tick(count);
        }

        #endregion

        #region Start

        /// <summary>
        /// Start the script specifying the text of the script so it can be reconfigured.
        /// </summary>
        /// <param name="script">The text of the script.</param>
        internal bool Start(string script) {
            m_oldScript = m_script;
            m_script = script;
            return Start();
        }

        /// <summary>
        /// Start the script specifying which user reqested it be started. Will authorize the request and may turn it down.
        /// </summary>
        /// <param name="id">The user who requested the script be restarted.</param>
        internal bool Start(UUID id) {
            if (Authorize("starting", id))
                return Start();
            return false;
        }

        private void NotifyProblem(string problem) {
            IClientAPI owner;
            if (m_scene.TryGetClient(m_god.PrincipalID, out owner)) {
                owner.SendAlertMessage(problem);
                foreach (var line in Errors)
                    owner.SendAlertMessage(line.ToString());
                //Console.WriteLine("Error2Console: " + m_error2Console + "\nError: " + ErrorString);
                if (m_error2Console) {
                    m_log.Warn("[" + Type + "]: " + problem);
                    if (ErrorString.Trim().Length > 0)
                        m_log.Debug(ErrorString);
                } else
                    m_log.Warn("[" + Type + "]: " + problem + " " + Errors[0]);
            } else {
                m_log.Warn("[" + Type + "]: " + problem);
                if (ErrorString.Trim().Length > 0)
                    m_log.Debug(ErrorString);
            }
            //m_scene.ForEachClient(user =>
            //    user.SendAlertMessage("Unable to start " + Name + ". " + ErrorString + "."));
        }

        /// <summary>
        /// Start the script without specifying the user who caused the script to be started. Will not check authorization.
        /// </summary>
        internal bool Start() {
            //if (m_working)
            //    return false;
            if (!IsInitialised)
                return false;
            //If the script is already running
            if (IsRunning) {
                ErrorString = "";
                NotifyProblem("Unable to start " + Name + ", it is already Running.");
                return false;
            }
            //m_working = true;
            m_log.Info("[" + Type + "]: Starting " + Name + ".");
            ErrorString = "";

            m_world = new World(m_scene, m_creds, IsGod);
            m_host = new Host(m_world.Objects[m_hostSOP.LocalId], m_scene, new ExtensionHandler(m_extensions, this), m_microthreads);

            bool started = m_domainSetup != null ?
                StartInAppDomain() :
                StartLocal();

            if (started) {
                m_start = DateTime.Now;
                m_log.Warn("[" + Type + "]: " + Name + " started.");
                m_running = true;
            } else
                NotifyProblem("Unable to start " + Name + ".");
            //m_working = false;
            return true;
        }

        /// <summary>
        /// Start the script up in its own application domain.
        /// </summary>
        /// <returns>True if the script was started successfully.</returns>
        private bool StartInAppDomain() {
            m_appDomain = AppDomain.CreateDomain(Name, null, m_domainSetup);
            if (m_policy != null)
                m_appDomain.SetAppDomainPolicy(m_policy);

            Root.Unloader unloader = new Root.Unloader(this);
            unloader.UnloadRequest += (message) => new Thread(() => {
                ErrorString = message;
                NotifyProblem("Problem while running " + Name + ".");
                Stop();
            }).Start();

            try {
                Type rootT = typeof(Root);
                m_log.Debug("[" + Type + "] Creating sandboxed script.");
                m_root = (Root)m_appDomain.CreateInstanceAndUnwrap(rootT.Assembly.FullName, rootT.FullName);
                m_log.Debug("[" + Type + "] Starting sandboxed script.");
                string outcome = m_root.Start(m_assembly, m_class, m_host, m_world, ID, Name, unloader, m_args);
                if (outcome != null) {
                    KillAppDomain();
                    ErrorString = outcome;
                    Console.WriteLine("Problem. Outcome = " + outcome);
                    return false;
                }
                return true;
            } catch (Exception e) {                m_world.Shutdown();
                ErrorString = "Unable to start MRM." + e.Message + "\n" + e.StackTrace;
                while (e.InnerException != null) {
                    e = e.InnerException;
                    ErrorString += "\n\nInner Exception: " + e.Message + "\n" + e.StackTrace;
                }
                return false;
            }
        }

        private void KillAppDomain() {
            m_world.Shutdown();
            m_host.Kill(TimeSpan.FromMinutes(1));
            if (m_root != null)
                m_root.Kill(TimeSpan.FromMinutes(1));
            
            if (m_appDomain != null) {
                try {
                    AppDomain.Unload(m_appDomain);
                } catch (Exception e) {
                    m_log.Warn("[" + Type + "]: Problem unloading application domain for " + Name + ". " + e.Message);
                }
            }
            m_world = null;
            m_host = null;
            m_root = null;
            m_appDomain = null;
        }

        /// <summary>
        /// Start the script in the same application domain as opensim.
        /// </summary>
        /// <returns>True if the script was started successfully.</returns>
        private bool StartLocal() {
            try {
                m_log.Debug("[" + Type + "] Creating local script.");
                m_mrm = (MRMBase)Activator.CreateInstanceFrom(m_assembly, m_class).Unwrap();
                m_log.Debug("[" + Type + "] Initialising local script.");
                m_mrm.InitMiniModule(m_world, m_host, ID);
                m_log.Debug("[" + Type + "] Starting local script.");
                m_mrm.Start(m_args);
                return true;
            } catch (Exception e) {
                m_world.Shutdown();
                ErrorString = "Unable to start MRM." + e.Message + "\n" + e.StackTrace;
                while (e.InnerException != null) {
                    e = e.InnerException;
                    ErrorString += "\n\nInner Exception: " + e.Message + "\n" + e.StackTrace;
                }
                return false;
            }
        }

        #endregion

        #region Stop

        /// <summary>
        /// Stop the script specifying which user reqested it be stopped. Will authorize the request and may turn it down.
        /// </summary>
        /// <param name="id">The user who requested the script be restarted.</param>        
        internal bool Stop(UUID id) {
            if (Authorize("stopping", id))
                return Stop();
            return false;
        }

        /// <summary>
        /// Stop the script without specifying the user who caused the script to be stopped. Will not check authorization.
        /// </summary>
        internal bool Stop() {
            //if (m_working)
            //    return false;
            if (!IsRunning) {
                m_log.Warn("[" + Type + "]: Unable to stop " + Name + ". It is not running.");
                return false;
            }
            m_stopping = true;
            //m_working = true;
            m_log.Info("[" + Type + "]: Stopping " + Name + ".");
            StopRunning();
            KillAppDomain();
            m_log.Warn("[" + Type + "]: " + Name + " stopped.");
            //m_working = false;
            m_running = false;
            m_time = (float)DateTime.Now.Subtract(m_start).TotalMilliseconds;
            m_stopping = false;
            return true;
        }

        private void StopRunning() {
            Thread stopThread = new Thread(() => {
                try {
                    if (m_root != null) 
                        m_root.Stop();
                    else
                        m_mrm.Stop();
                } catch (Exception e) {
                    m_log.Warn("[" + Type + "]: Problem stopping " + Name + ". " + e.Message);
                }
            });
            stopThread.Name = Name + " stop thread";
            stopThread.Start();
            //Give the system 30 seconds to stop
            stopThread.Join(STOP_TIMEOUT);
        }

        #endregion

        #region Reset

        internal bool Reset() {
            if (IsRunning)
                Stop();
            return Start();
        }

        internal bool Reset(UUID id) {
            if (IsRunning)
                Stop(id);
            if (m_host.Object.Exists)
                return Start(id);
            return false;
        }

        internal bool Reset(string script) {
            if (IsRunning)
                Stop();
            return Start(script);
        }

        #endregion

        #region Dispose

        public virtual bool Dispose(UUID id) {
            if (!Authorize("Dispose", id))
                return false;
            return Dispose();
        }

        public virtual bool Dispose () {
            bool result = false; ;
            if (IsRunning)
                result = Stop();
            m_scriptAccessor.RemoveScript(ID);
            Kill(TimeSpan.FromMinutes(1));
            return result;
        }

        #endregion

        #endregion

        #region Util
        
        private bool Authorize(string command, UUID id) {
            if (!id.Equals(m_god.PrincipalID) && !id.Equals(UUID.Zero)) {
                UserAccount attempt = m_scene.UserAccountService.GetUserAccount(UUID.Zero, id);
                m_log.Warn("Can't execute " + command + " on " + Name + ". " + (attempt != null ? attempt.FirstName + " " + attempt.LastName + " is not " : "Not ") + "authorized.");
                return false;
            }
            return true;
        }

        protected void SetOwner(UserAccount god) {
            m_god = god;
        }

        #endregion
    }
}
