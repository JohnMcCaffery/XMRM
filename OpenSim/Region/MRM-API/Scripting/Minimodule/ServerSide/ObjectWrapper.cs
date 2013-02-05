using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenSim.Region.OptionalModules.Scripting.Minimodule;

namespace OpenSim.Region.MRM.API.Scripting.Minimodule.ServerSide {
    public class ObjectWrapper : IObject {
        private IObject m_object;
        
        public ObjectWrapper (IObject objct) {
            m_object = objct;
        }

        #region IObject Members

        public event OnTouchDelegate OnTouch;

        public bool Exists {
            get { return m_object.Exists; }
        }

        public uint LocalID {
            get { return m_object.LocalID; }
        }

        public string Description {
            get { return m_object.Description; }
            set { m_object.Description = value; }
        }

        public OpenMetaverse.UUID OwnerId {
            get { return m_object.OwnerId; }
        }

        public OpenMetaverse.UUID CreatorId {
            get { return m_object.CreatorId; }
        }

        public IObject Root {
            get { return m_object.Root; }
        }

        public IObject[] Children {
            get { return m_object.Children; }
        }

        public IObjectMaterial[] Materials {
            get { return m_object.Materials; }
        }

        public OpenMetaverse.Vector3 Scale {
            get { return m_object.Scale; }
            set { m_object.Scale = value; }
        }

        public OpenMetaverse.Quaternion WorldRotation {
            get { return m_object.WorldRotation; }
            set { m_object.WorldRotation = value; }
        }

        public OpenMetaverse.Quaternion OffsetRotation {
            get { return m_object.OffsetRotation; }
            set { m_object.OffsetRotation = value; }
        }

        public OpenMetaverse.Vector3 OffsetPosition {
            get { return m_object.OffsetPosition; }
            set { m_object.OffsetPosition = value; }
        }

        public OpenMetaverse.Vector3 SitTarget {
            get { return m_object.SitTarget; }
            set { m_object.SitTarget = value; }
        }

        public string SitTargetText {
            get { return m_object.SitTargetText; }
            set { m_object.SitTargetText = value; }
        }

        public string TouchText {
            get { return m_object.TouchText; }
            set { m_object.TouchText = value; }
        }

        public string Text {
            get { return m_object.Text; }
            set { m_object.Text = value; }
        }

        public bool IsRotationLockedX {
            get { return m_object.IsRotationLockedX; }
            set { m_object.IsRotationLockedX = value; }
        }

        public bool IsRotationLockedY {
            get { return m_object.IsRotationLockedY; }
            set { m_object.IsRotationLockedY = value ; }
        }

        public bool IsRotationLockedZ {
            get { return m_object.IsRotationLockedZ; }
            set { m_object.IsRotationLockedZ = value; }
        }

        public bool IsSandboxed {
            get { return m_object.IsSandboxed; }
            set { m_object.IsSandboxed = value; }
        }

        public bool IsImmotile {
            get { return m_object.IsImmotile; }
            set { m_object.IsImmotile = value; }
        }

        public bool IsAlwaysReturned {
            get { return m_object.IsAlwaysReturned; }
            set { m_object.IsAlwaysReturned = value; }
        }

        public bool IsTemporary {
            get { return m_object.IsTemporary; }
            set { m_object.IsTemporary = value; }
        }

        public bool IsAttachment {
            get { return m_object.IsAttachment; }
        }

        public bool IsFlexible {
            get { return m_object.IsFlexible; }
            set { m_object.IsFlexible = value; }
        }

        public OptionalModules.Scripting.Minimodule.Object.IObjectShape Shape {
            get { return m_object.Shape; }
        }

        public PhysicsMaterial PhysicsMaterial {
            get { return m_object.PhysicsMaterial; }
            set { m_object.PhysicsMaterial = value; }
        }

        public OptionalModules.Scripting.Minimodule.Object.IObjectPhysics Physics {
            get { return m_object.Physics; }
        }

        public OptionalModules.Scripting.Minimodule.Object.IObjectSound Sound {
            get { return m_object.Sound; }
        }

        public void Say(string msg) {
            m_object.Say(msg);
        }

        public void Say(string msg, int channel) {
            m_object.Say(msg, channel);
        }

        public void Say(string msg, int channel, ChatTargetEnum target) {
            m_object.Say(msg, channel, target);
        }

        public void Say(string msg, int channel, bool broadcast, MRMChatTypeEnum type) {
            m_object.Say(msg, channel, broadcast, type);
        }

        public void Dialog(OpenMetaverse.UUID avatar, string message, string[] buttons, int chat_channel) {
            m_object.Dialog(avatar, message, buttons, chat_channel);
        }

        public OptionalModules.Scripting.Minimodule.Object.IObjectInventory Inventory {
            get { return m_object.Inventory; }
        }

        #endregion

        #region IEntity Members

        public string Name {
            get { return m_object.Name; }
            set { m_object.Name = value; }
        }

        public OpenMetaverse.UUID GlobalID {
            get { return m_object.GlobalID; }
        }

        public OpenMetaverse.Vector3 WorldPosition {
            get { return m_object.WorldPosition; }
            set { m_object.WorldPosition = value; }
        }

        #endregion
    }
}
