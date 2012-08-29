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
using System.Collections;
using OpenSim.Region.OptionalModules.API.Scripting.Minimodule;

namespace StAndrews.ExternalMRM {
    public class SandboxedObjectAccessor : KillableProxy, IObjectAccessor {
        private IObjectAccessor _accessor;
        private Dictionary<UUID, SandboxedObject> _objects = new Dictionary<UUID,SandboxedObject>();

        internal SandboxedObjectAccessor(IObjectAccessor accessor) {
            _accessor = accessor;
        }

        public IObject this[int index] {
            get {
                IObject obj = _accessor[index];
                if (!obj.Exists)
                    return obj;
                if (!_objects.ContainsKey(obj.GlobalID))
                    lock (_objects)
                        _objects.Add(obj.GlobalID, new SandboxedObject(obj, this)); 
                return _objects[obj.GlobalID];
            }
        }

        public IObject this[uint index] {
            get {
                IObject obj = _accessor[index];
                if (obj == null || !obj.Exists)
                    return obj;
                if (!_objects.ContainsKey(obj.GlobalID))
                    lock (_objects)
                        _objects.Add(obj.GlobalID, new SandboxedObject(obj, this));
                return _objects[obj.GlobalID];
            }
        }

        public IObject this[UUID index] {
            get {
                if (!_objects.ContainsKey(index))
                    lock (_objects)
                        _objects.Add(index, new SandboxedObject(_accessor[index], this));
                return _objects[index];
            }
        }

        public IObject Create(Vector3 position) {
            SandboxedObject obj = new SandboxedObject(_accessor.Create(position), this);
            lock (_objects)
                _objects.Add(obj.GlobalID, obj);
            return obj;
        }

        public IObject Create(Vector3 position, Quaternion rotation) {
            SandboxedObject obj = new SandboxedObject(_accessor.Create(position, rotation), this);
            lock (_objects)
                _objects.Add(obj.GlobalID, obj);
            return obj;
        }

        public void Add(IObject item) {
            _accessor.Add(item);
        }

        public void Clear() {
            _accessor.Clear();
        }

        public bool Contains(IObject item) {
            return _accessor.Contains(item);
        }

        public void CopyTo(IObject[] array, int arrayIndex) {
            _accessor.CopyTo(array, arrayIndex);
        }

        public int Count {
            get { return _accessor.Count; }
        }

        public bool IsReadOnly {
            get { return _accessor.IsReadOnly; }
        }

        public bool Remove(IObject item) {
            lock (_objects)
                _objects.Remove(item.GlobalID);
            return _accessor.Remove(_accessor[item.GlobalID]);
        }

        public IEnumerator<IObject> GetEnumerator() {
            return ConvertObjects(_accessor).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return ((IEnumerable<IObject>)this).GetEnumerator();
        }


        public IEnumerable<IObject> GetByName(string name) {
            return ConvertObjects(_accessor.GetByName(name));
        }

        private IEnumerable<IObject> ConvertObjects(IEnumerable<IObject> objects) {
            return _accessor.Select<IObject, IObject>(obj => {
                if (obj == null || !obj.Exists)
                    return obj;
                if (_objects.ContainsKey(obj.GlobalID))
                    return _objects[obj.GlobalID];
                SandboxedObject newObj = new SandboxedObject(obj, this);
                _objects.Add(obj.GlobalID, newObj);
                return newObj;
            });
        }

        internal void FullClear() {
            _objects.Clear();
        }
    }
}
