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
using System.Collections;
using System.Collections.Generic;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;
using IEnumerable=System.Collections.IEnumerable;
using log4net;
using System.Reflection;
using System.Runtime.Remoting.Lifetime;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace OpenSim.Region.OptionalModules.Scripting.Minimodule
{

    internal class IObjEnum : System.MarshalByRefObject, IEnumerator<IObject> {
        private readonly Scene m_scene;
        private readonly IEnumerator<EntityBase> m_sogEnum;
        private readonly ISecurityCredential m_security;
        private readonly List<EntityBase> m_entities;
        private readonly IObjectAccessor m_accessor;
        
        public IObjEnum(Scene scene, ISecurityCredential security, IObjectAccessor accessor)
        {
            m_scene = scene;
            m_security = security;
            m_entities = new List<EntityBase>(m_scene.Entities.GetEntities());
            m_sogEnum = m_entities.GetEnumerator();
            m_accessor = accessor;
        }

        public void Dispose()
        {
            m_sogEnum.Dispose();
        }

        public bool MoveNext()
        {
            return m_sogEnum.MoveNext();
        }

        public void Reset()
        {
            m_sogEnum.Reset();
        }

        public IObject Current
        {
            get
            {
                return m_accessor[m_sogEnum.Current.LocalId];
            }
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }
    }


    public class ObjectAccessor : KillableProxy, IObjectAccessor, ISponsor {
        private static readonly ILog m_log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType);

        private readonly Scene m_scene;
        private readonly ISecurityCredential m_security;
        private readonly Dictionary<UUID, SOPObject> m_objects;
        internal readonly bool IsGod;

        public ObjectAccessor(Scene scene, ISecurityCredential security, bool isGod)
        {
            m_scene = scene;
            m_security = security;
            m_objects = new Dictionary<UUID, SOPObject>();
            IsGod = isGod;
        }

        /// <summary>
        /// Remove any touch listener from any object that was accessed through this object accessor.
        /// </summary>
        internal void Shutdown() {
            lock (m_objects)
                foreach (var obj in m_objects.Values)
                    obj.Dispose();
            m_objects.Clear();
            Kill(TimeSpan.FromMinutes(1));
        }

        public int Count {
            get { return m_objects.Count; }
        }


        /// <summary>
        /// Updated by John McCaffery
        /// 
        /// Added check to make sure index exists in scene graph
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IObject this[int index] {
            get {
                try {
                    EntityBase sop = m_scene.Entities[(uint)index];
                    if (sop == null)
                        return null;
                    if (m_objects.ContainsKey(sop.UUID))
                        return m_objects[sop.UUID];
                    SOPObject obj = new SOPObject(m_scene, sop.LocalId, m_security, this);
                    lock (m_objects)
                        m_objects.Add(obj.GlobalID, obj);
                    return obj;
                } catch (Exception e) {
                    m_log.Debug("Object accessor unable to look up object. " + e.Message);
                    return null;
                }
            }
        }

        /// <summary>
        /// Updated by John McCaffery
        /// 
        /// Added check to make sure index exists in scene graph
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IObject this[uint index] {
            get {
                try {
                    SceneObjectPart sop = m_scene.GetSceneObjectPart(index);
                    if (sop == null)
                        return null;
                    if (m_objects.ContainsKey(sop.UUID))
                        return m_objects[sop.UUID];
                    SOPObject obj = new SOPObject(m_scene, index, m_security, this);
                    lock (m_objects)
                        m_objects.Add(obj.GlobalID, obj);
                    return obj;
                } catch (Exception e) {
                    m_log.Debug("Object accessor unable to look up object. " + e.Message);
                    return null;
                }
            }
        }


        /// <summary>
        /// Updated by John McCaffery
        /// 
        /// Added check to make sure index exists in scene graph
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public IObject this[UUID index] {
            get {
                try {
                    if (m_objects.ContainsKey(index)) 
                        return m_objects[index];
                    SceneObjectPart sop = m_scene.GetSceneObjectPart(index);
                    if (sop == null) {
                        SceneObjectGroup sog = m_scene.GetSceneObjectGroup(index);
                        if (sog == null) 
                            return null;
                        sop = sog.RootPart;
                        if (m_objects.ContainsKey(sop.UUID)) 
                            return m_objects[sop.UUID];
                    }
                    SOPObject obj = new SOPObject(m_scene, sop.LocalId, m_security, this);
                    lock (m_objects)
                        m_objects.Add(obj.GlobalID, obj);
                    return obj;
                } catch (Exception e) {
                    m_log.Debug("Object accessor unable to look up object. " + e.Message);
                    return null;
                }
            }
        }

        public IObject Create(Vector3 position)
        {
            return Create(position, Quaternion.Identity);
        }

        public IObject Create(Vector3 position, Quaternion rotation)
        {

            SceneObjectGroup sog = m_scene.AddNewPrim(m_security.owner.GlobalID,
                                                      UUID.Zero,
                                                      position,
                                                      rotation,
                                                      PrimitiveBaseShape.CreateBox());

            SOPObject ret = new SOPObject(m_scene, sog.LocalId, m_security, this);
            lock (m_objects)
                m_objects.Add(ret.GlobalID, ret);
            return ret;
        }

        public IEnumerator<IObject> GetEnumerator()
        {
            return new IObjEnum(m_scene, m_security, this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public void Add(IObject item)
        {
            throw new NotSupportedException("Collection is read-only. This is an API TODO FIX, creation of objects is presently impossible.");
        }

        public void Clear()
        {
            throw new NotSupportedException("Collection is read-only. TODO FIX.");
        }

        public bool Contains(IObject item)
        {
            return m_scene.Entities.ContainsKey(item.LocalID);
        }

        public void CopyTo(IObject[] array, int arrayIndex)
        {
            for (int i = arrayIndex; i < Count + arrayIndex; i++)
            {
                array[i] = this[i - arrayIndex];
            }
        }

        /// <summary>
        /// Updated by John McCaffery
        /// 
        /// WARNING
        /// Calls to this require a lock to remove all scripts. 
        /// If there is a a script destructor blocking because it is being shut down as its host object has been removed this method will deadlock.
        /// </summary>
        public bool Remove(IObject item) {
            try {
                //Can only delete a primitive if it is the root of its link set / not part of a link set.
                if (item.Root != null && !item.Equals(item.Root))
                    return false;

                UUID id = item.GlobalID;
                if (m_objects.ContainsKey(id)) {
                    m_objects[id].Dispose();
                    lock (m_objects)
                        m_objects.Remove(id);
                }
                SceneObjectPart part = m_scene.GetSceneObjectPart(id);
                if (part == null || part.ParentGroup == null)
                    return false;
                try {
                    part.ParentGroup.DeleteGroupFromScene(false);
                    return true;
                } catch (Exception e) {
                    m_log.ErrorFormat("[Minimodule]: Error while removing object from scene: {0}", e);
                    return false;
                }
            } catch (Exception e) {
                m_log.Debug("[Minimodle]: Object accessor unable to remove object.");
            }
            return false;
        }

        public bool IsReadOnly
        {
            get { return true; }
        }

        public IEnumerable<IObject> GetByName(string name) {
            LinkedList<IObject> toReturn = new LinkedList<IObject>();
            m_scene.Entities.ForEach(entity => {
                if (entity.Name.Equals(name))
                    toReturn.AddLast(this[entity.UUID]);
            });
            return toReturn;
        }
    }
}
