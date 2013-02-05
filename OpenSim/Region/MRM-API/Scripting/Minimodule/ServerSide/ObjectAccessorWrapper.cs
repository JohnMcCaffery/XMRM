using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class ObjectAccessorWrapper : IObjectAccessor {
        private IObjectAccessor m_objectAccessor;
        
        public ObjectAccessorWrapper (IObjectAccessor objectAccessor) {
            m_objectAccessor = objectAccessor;
        }

        #region IObjectAccessor Members

        public IObject this[int index] {
            get { return m_objectAccessor[index]; }
        }

        public IObject this[uint index] {
            get { return m_objectAccessor[index]; }
        }

        public IObject this[OpenMetaverse.UUID index] {
            get { return m_objectAccessor[index]; }
        }

        public IObject Create(OpenMetaverse.Vector3 position) {
            return m_objectAccessor.Create(position);
        }

        public IObject Create(OpenMetaverse.Vector3 position, OpenMetaverse.Quaternion rotation) {
            return m_objectAccessor.Create(position, rotation);
        }

        public IEnumerable<IObject> GetByName(string name) {
            return m_objectAccessor.GetByName(name);
        }

        #endregion

        #region ICollection<IObject> Members

        public void Add(IObject item) {
            m_objectAccessor.Add(item);
        }

        public void Clear() {
            m_objectAccessor.Clear();
        }

        public bool Contains(IObject item) {
            return m_objectAccessor.Contains(item);
        }

        public void CopyTo(IObject[] array, int arrayIndex) {
            m_objectAccessor.CopyTo(array, arrayIndex);
        }

        public int Count {
            get { return m_objectAccessor.Count; }
        }

        public bool IsReadOnly {
            get { return m_objectAccessor.IsReadOnly; }
        }

        public bool Remove(IObject item) {
            return m_objectAccessor.Remove(item);
        }

        #endregion

        #region IEnumerable<IObject> Members

        public IEnumerator<IObject> GetEnumerator() {
            throw new NotImplementedException();
        }

        #endregion

        #region IEnumerable Members

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() {
            return m_objectAccessor.GetEnumerator();
        }

        #endregion
    }
}
