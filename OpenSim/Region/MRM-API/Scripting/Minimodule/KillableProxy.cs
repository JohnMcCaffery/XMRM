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
using System.Runtime.Remoting.Lifetime;

namespace OpenSim.Region.OptionalModules.API.Scripting.Minimodule {
    public abstract class KillableProxy : MarshalByRefObject, ISponsor {
        internal const int TIMEOUT_MIN = 1;
        public override object InitializeLifetimeService() {
            ILease lease = (ILease)base.InitializeLifetimeService();
            if (lease.CurrentState == LeaseState.Initial) {
                m_live = true;
                lease.InitialLeaseTime = TimeSpan.FromMinutes(TIMEOUT_MIN);
                lease.RenewOnCallTime = TimeSpan.FromMinutes(TIMEOUT_MIN);
                lease.SponsorshipTimeout = TimeSpan.FromMinutes(TIMEOUT_MIN);
                lease.Register(this);
            }
            return lease;
        }
        #region ISponsor Members

        private bool m_live = true;

        public TimeSpan Renewal(ILease lease) {
            //If the kill delay has expired
            if (m_expireSet && m_expires.CompareTo(DateTime.Now) < 0)
                m_live = false;
            return m_live ? TimeSpan.FromMinutes(TIMEOUT_MIN) : TimeSpan.Zero;
        }

        #endregion

        private DateTime m_expires;
        private bool m_expireSet;

        public void Kill() {
            m_live = false;
        }

        public void Kill(int msDelay) {
            m_expires = DateTime.Now.AddMilliseconds(msDelay);
            m_expireSet = true;
        }

        public void Kill(TimeSpan delay) {
            m_expires = DateTime.Now.Add(delay);
            m_expireSet = true;
        }
    }
}
