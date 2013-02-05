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
using System.Configuration;
using System.IO;
using log4net;
using System.Reflection;
using ExternalMRM.Hack;
using StAndrews.ExternalMRM;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;
using System.Runtime.Remoting.Lifetime;
using System.Threading;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule.Sandboxed {
    public class Root : KillableProxy {
        public class Unloader : KillableProxyChild {
            public Unloader(ISponsor sponsor)
                : base(sponsor) {
            }
            public event Action<string> UnloadRequest;
            public void RequestUnload(string reason) {
                if (UnloadRequest != null)
                    UnloadRequest(reason);
            }
        }

        private static Queue<Action> m_eventQ = new Queue<Action>();
        private static bool m_cont = false;
        private static bool m_stopped = false;

        internal static void QueueEvent(Action evt) {
            evt.Invoke();

            /*
            ThreadPool.QueueUserWorkItem(new WaitCallback(obj => {
                try {
                    evt.Invoke();
                } catch (Exception e) {
                    Kill(e);
                }
            }));
             */

        /*
        if (m_stopped)
            return;
        if (!m_cont) {
            new Thread(() => {
                m_cont = true;
                while (m_cont) {
                    while (m_cont && m_eventQ.Count > 0) {
                        try {
                            m_eventQ.Dequeue().Invoke();
                        } catch (Exception e) {
                            Kill(e);
                        }
                    }
                    Monitor.Wait(m_eventQ);
                }
            }).Start();
        }
        Console.WriteLine(AppDomain.CurrentDomain.FriendlyName + " queueing event.");
        lock (m_eventQ)
            m_eventQ.Enqueue(evt);
        Monitor.Pulse(m_eventQ);
         * */
    }

    internal static void Kill(Exception e) {
            if (e != null)
                    m_log.Warn("[XMRM]: Unhandled exception was caught somewhere within '" + m_name + "'.", e);
                else
                    m_log.Warn("[XMRM]: Unhandled exception was caught somewhere within '" + m_name + "'.");
                _unloader.RequestUnload(FullError(e));
        }

        private static string FullError(Exception e) {
            string ret = e.Message + "\n" + e.StackTrace;
            while (e.InnerException != null) {
                e = e.InnerException;
                ret += "\n\nInner Exception: " + e.Message + "\n" + e.StackTrace;
            }
            return ret;
        }

        private static ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);
        private static Unloader _unloader;
        private static string m_name;

        private MRMBase m_mrm;
        private UUID m_id;

        private SandboxedWorld m_world;
        private SandboxedHost m_host;
        private MicroScheduler m_scheduler;

        public UUID ID { get { return m_id; } }

        public string Start(string assembly, string clazz, IHost host, IWorld world, UUID id, string name, Unloader unloader, string[] args) {
            m_scheduler = new MicroScheduler();
            _unloader = unloader;
            AppDomain.CurrentDomain.UnhandledException += (sender, exceptionArgs) => Kill(exceptionArgs.ExceptionObject as Exception);
            
            try {
                m_id = id;
                m_name = name;
                m_mrm = (MRMBase)Activator.CreateInstanceFrom(assembly, clazz).Unwrap();
                m_world = new SandboxedWorld(world);
                m_host = new SandboxedHost(host, m_world.Objects, m_scheduler);
                m_mrm.InitMiniModule(m_world, m_host, id);
                m_mrm.Start(args);
            } catch (Exception e) {
                return FullError(e);
            } 
            return null;
        }

        public void Tick(int count) {
            //QueueEvent(() => m_scheduler.Tick(count));
        }

        public override object InitializeLifetimeService() {
            return null;
        }

        public string Stop() {
            try {
                /*
                m_cont = false;
                m_stopped = true;
                lock (m_eventQ)
                    Monitor.Pulse(m_eventQ);
                 */
                m_mrm.Stop();
                if (m_world != null) {
                    m_world.Shutdown();
                    m_world.Kill(TimeSpan.FromMinutes(1));
                }
                m_mrm.Kill(TimeSpan.FromMinutes(1));
                m_mrm = null;
                return null;
            } catch (Exception e) {
                m_log.Warn("[XMRM]: Problem shutting down " + m_name + ".", e);
                return e.Message;
            }
        }
    }
}
