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
using OpenMetaverse;
using System.Collections;
using System.Text.RegularExpressions;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule {
    internal class ScriptAccessor : IEnumerable<Script> {
        private readonly Dictionary<UUID, Script> m_scripts;
        private readonly Dictionary<string, Script> m_scriptNames;

        internal ScriptAccessor() {
            m_scripts = new Dictionary<UUID, Script>();
            m_scriptNames = new Dictionary<string, Script>();
        }

        /// <summary>
        /// Get a script that exists with a given ID whether it is running or not.
        /// </summary>
        /// <param name="id">The script to get.</param>
        /// <returns>The script registered under 'ID'.</returns>
        internal Script this[UUID id] {
            get {
                return m_scripts[id];
            }
            //set {
            //    m_scripts[id] = value;
            //}
        }

        /// <summary>
        /// Get a script that is running under a given name.
        /// </summary>
        /// <param name="name">The name the script is running under.</param>
        /// <returns>The script running under 'name'.</returns>
        internal Script this[string name] {
            get {
                return m_scriptNames[name];
            }
        }

        /// <summary>
        /// Check whether a script is running under a given ID.
        /// </summary>
        /// <param name="id">The id to check for.</param>
        /// 
        /// <returns>True if there is a script (running or stopped) with the given ID.</returns>
        internal bool ContainsKey(UUID id) {
            return m_scripts.ContainsKey(id);
        }

        /// <summary>
        /// Check whether a script is running with a given name.
        /// </summary>
        /// <param name="name">The name to check for.</param>
        /// <returns>True if there is a script running called 'name'.</returns>
        internal bool IsScript(string name) {
            return m_scriptNames.ContainsKey(name);
        }

        /// <summary>
        /// Register that a script has been created.
        /// </summary>
        /// <param name="script"></param>
        internal string AddScript(Script script) {
            lock (this) {
                string name = script.Name;
                if (NameExists(name)) {
                    int append = m_scripts.Values.
                        Where(test => test.Name.Equals(name) || Regex.IsMatch(test.Name, name + "_\\d*$")).
                        OrderBy(test => test.Name).
                        Aggregate<Script, int>(0, (count, test) => {
                            if ((count == 0 && test.Name.Equals(name)) || (count > 0 && test.Name.Equals(name + "_" + count)))
                                count++;
                            return count;
                        });
                    name = append > 0 ? name + "_" + append : name;
                }
                m_scriptNames[name] = script;
                m_scripts[script.ID] = script;
                return name;
            }
        }

        /// <summary>
        /// Remove a script that has been created.
        /// </summary>
        /// <param name="scriptID">The script to remove.</param>
        internal void RemoveScript(UUID scriptID) {
            lock (this) {
                if (m_scripts.ContainsKey(scriptID)) {
                    Script script = this[scriptID];
                    m_scripts.Remove(scriptID);
                    m_scriptNames.Remove(script.Name);
                }
            }
        }

        /// <summary>
        /// Count how many scripts are running with a given name.
        /// Searches for scripts with a given name or scripts that fit the pattern 'name_X' where X is any integer
        /// </summary>
        /// <param name="name">The name to check for.</param>
        /// <returns>How many of the scripts that are currently running are called 'name'.</returns>
        private bool NameExists(string name) {
            return m_scripts.Values.FirstOrDefault(script => script.Name.Equals(name) || Regex.IsMatch(script.Name, name + "_\\d*$")) != null;
        }

        #region IEnumerable<MRMScript> Members

        /// <summary>
        /// Iterate through all scripts that are currently running.
        /// </summary>
        /// <returns>An enumerator for all currently running scripts.</returns>
        public IEnumerator<Script> GetEnumerator() {
            return m_scriptNames.Values.ToList().GetEnumerator();
        }

        #endregion

        #region IEnumerable Members

        /// <summary>
        /// Iterate through all scripts that are currently running.
        /// </summary>
        /// <returns>An enumerator for all currently running scripts.</returns>
        IEnumerator IEnumerable.GetEnumerator() {
            return m_scriptNames.Values.ToArray().GetEnumerator();
        }

        #endregion
    }
}
